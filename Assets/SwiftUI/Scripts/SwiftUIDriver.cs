using UnityEngine;
using System.Runtime.InteropServices;
using System;
using System.IO;
using System.Collections.Generic;
using AOT;
using UniRx;
using System.Linq;
using Newtonsoft.Json;
using System.Threading.Tasks;
using UnityEngine.Networking;

/// <summary>
/// SwiftUI-specific implementation of the IUIDriver interface.
/// Manages communication with a SwiftUI frontend and delegates callback logic to IUICallbackHandler.
/// </summary>
public class SwiftUIDriver : IUIDriver, IDisposable
{
    #region Singleton Instance
    private static SwiftUIDriver _instance;
    public static SwiftUIDriver Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new SwiftUIDriver();
            }
            return _instance;
        }
    }
    #endregion

    #region Private Fields
    private CompositeDisposable _disposables = new CompositeDisposable();
    private Action DisposeVoice; // For managing voice command subscriptions

    private IAuthProvider _authProvider;
    private IFileManager _fileManager; // Cached for LoadProtocolDefinitions, requestUserProfiles
    private IUICallbackHandler _uiCallbackHandler;
    #endregion

    #region Constructor and Initialization
    public SwiftUIDriver()
    {
        // Set up the native callback immediately for messages from SwiftUI
        SetNativeCallback(OnMessageReceived);
    }

    public void Initialize()
    {
        _authProvider = ServiceRegistry.GetService<IAuthProvider>();
        _fileManager = ServiceRegistry.GetService<IFileManager>();
        _uiCallbackHandler = ServiceRegistry.GetService<IUICallbackHandler>();

        if (_authProvider == null) Debug.LogError("SwiftUIDriver: IAuthProvider is not available from ServiceRegistry.");
        if (_fileManager == null) Debug.LogError("SwiftUIDriver: IFileManager is not available from ServiceRegistry.");
        if (_uiCallbackHandler == null) Debug.LogError("SwiftUIDriver: IUICallbackHandler is not available from ServiceRegistry.");

        // Subscribe to AuthProvider events
        if (_authProvider != null)
        {
            _authProvider.OnSignInSuccess += HandleSignInSuccess;
            _authProvider.OnAuthError += HandleAuthError;
        }
        
        // Subscribe to ProtocolState and SessionState streams
        if (ProtocolState.Instance != null)
        {
            _disposables.Add(ProtocolState.Instance.ProtocolStream.Subscribe(OnProtocolChange));
            _disposables.Add(ProtocolState.Instance.StepStream.Subscribe(OnStepChange));
            _disposables.Add(ProtocolState.Instance.ChecklistStream.Subscribe(OnCheckItemChange));
        }
        else { Debug.LogWarning("SwiftUIDriver: ProtocolState.Instance is null during initialization."); }

        if (SessionState.JsonFileDownloadable != null) // Ensure SessionState itself is not null if JsonFileDownloadable is static
        {
            _disposables.Add(SessionState.JsonFileDownloadable.Subscribe(OnJsonFileDownloadableChange));
        }
        else { Debug.LogWarning("SwiftUIDriver: SessionState.JsonFileDownloadable is null during initialization."); }

        // Subscribe to LLMChatProvider events
        var chatProvider = ServiceRegistry.GetService<ILLMChatProvider>();
        if (chatProvider != null)
        {
            chatProvider.OnResponse.AddListener(OnChatMessageReceived);
        }
        else { Debug.LogWarning("SwiftUIDriver: ILLMChatProvider is null during initialization."); }
    }
    #endregion

    #region IDisposable Implementation
    public void Dispose()
    {
        _disposables.Dispose();
        
        // Unsubscribe from AuthProvider events
        if (_authProvider != null)
        {
            _authProvider.OnSignInSuccess -= HandleSignInSuccess;
            _authProvider.OnAuthError -= HandleAuthError;
        }

        // Unsubscribe from LLMChatProvider events
        var chatProvider = ServiceRegistry.GetService<ILLMChatProvider>();
        if (chatProvider != null)
        {
            chatProvider.OnResponse.RemoveListener(OnChatMessageReceived);
        }

        DisposeVoice?.Invoke();
        DisposeVoice = null;
    }
    #endregion

    #region Auth Event Handlers (Local UI Reactions)
    private void HandleSignInSuccess(string oidcToken) // oidcToken may not be used directly here
    {
        SendAuthStatus(true);
    }

    private void HandleAuthError(string errorMessage) // errorMessage may not be used directly here
    {
        SendAuthStatus(false);
    }
    #endregion

    #region UI Update Methods (Implementing IUIDriver & SwiftUI Communication)
    // These methods are called in response to changes in application state (e.g., ProtocolState)
    // and are responsible for sending the updated state to the SwiftUI frontend.

    public void OnProtocolChange(ProtocolDefinition protocol)
    {
        if (protocol == null) 
        {
            // This case might mean the protocol was closed or no protocol is active.
            // Send a specific message or an empty protocol object based on SwiftUI expectations.
            SendMessageToSwiftUI("protocolChange|null"); // Example: sending null or empty JSON
            return;
        }

        Debug.Log("SwiftUIDriver: OnProtocolChange - " + protocol.title);
        string protocolJson = JsonConvert.SerializeObject(protocol);
        SendMessageToSwiftUI($"protocolChange|{protocolJson}");
        SetupVoiceCommands(); // Setup voice commands when a new protocol is loaded
    }

    public void OnStepChange(ProtocolState.StepState stepState)
    {
        if (ProtocolState.Instance == null) { Debug.LogError("SwiftUIDriver: ProtocolState.Instance is null in OnStepChange."); return; }

        var stepStateData = new StepStateData // Defined within SwiftUIDriver
        {
            CurrentStepIndex = ProtocolState.Instance.CurrentStep.Value,
            IsSignedOff = stepState.SignedOff.Value,
            ChecklistState = stepState.Checklist?.Select(item => new CheckItemStateData
            {
                IsChecked = item.IsChecked.Value,
                CheckIndex = ProtocolState.Instance.CurrentStepState.Value.Checklist.IndexOf(item)
            }).ToList() ?? new List<CheckItemStateData>()
        };
        string stepStateJson = JsonConvert.SerializeObject(stepStateData);
        SendMessageToSwiftUI($"stepChange|{stepStateJson}");
    }

    public void OnCheckItemChange(List<ProtocolState.CheckItemState> checkItemStates)
    {
        if (checkItemStates == null) return;
        
        var checkItemStateDataList = checkItemStates.Select((checkItemState, index) => new CheckItemStateData
        {
            IsChecked = checkItemState.IsChecked.Value,
            CheckIndex = index // Assuming the list is already correctly indexed
        }).ToList();

        string checkItemStatesJson = JsonConvert.SerializeObject(checkItemStateDataList);
        SendMessageToSwiftUI($"checkItemChange|{checkItemStatesJson}");

        // Handle AR Actions like timers associated with check items
        var currentCheckItemDef = ProtocolState.Instance?.CurrentCheckItemDefinition;
        if (currentCheckItemDef != null)
        {
            foreach(var arAction in currentCheckItemDef.arActions)
            {
                if (arAction.actionType == "Timer")
                {
                    if (arAction.properties.TryGetValue("duration", out object durationObj) && 
                        int.TryParse(durationObj.ToString(), out int seconds))
                    {
                        DisplayTimer(seconds);
                    }
                    else
                    {
                        DisplayTimer();
                    }
                }
            }
        }
    }
    
    public void OnJsonFileDownloadableChange(string jsonFileInfo) // From SessionState
    {
        // If jsonFileInfo is empty, it means no file is available or download completed/cleared.
        // If it contains info, SwiftUI might use it to show a download button.
        SendMessageToSwiftUI($"jsonFileDownloadableChange|{jsonFileInfo}");
    }

    public void OnChatMessageReceived(string message) // From ILLMChatProvider event
    {
        SendMessageToSwiftUI($"LLMChatMessage|{message}");
    }

    public void SendAuthStatus(bool isAuthenticated)
    {
        Debug.Log("SwiftUIDriver: Sending auth status to Swift: " + isAuthenticated);
        SendMessageToSwiftUI($"authStatus|{isAuthenticated}");
    }
    
    // Called from HandleMessage when SwiftUI requests user profiles
    public void OnUserProfilesChange(List<LocalUserProfileData> profiles)
    {
        var profilesData = profiles.Select(p => new { userId = p.Id, name = p.Name }).ToList();
        string profilesJson = JsonConvert.SerializeObject(profilesData);
        SendMessageToSwiftUI($"userProfiles|{profilesJson}");
    }
    #endregion

    #region UI Display Methods (Implementing IUIDriver & Opening SwiftUI Windows)
    // These methods are called to instruct the SwiftUI frontend to display specific views/windows.
    public void DisplayUserSelection() 
    {
        Debug.LogWarning("SwiftUIDriver: DisplayUserSelection() called. Consider using DisplayUserSelectionMenu(). Opening UserProfiles view.");
        OpenSwiftUIWindow("UserProfiles"); 
    }

    public void DisplayUserSelectionMenu() 
    {
        OpenSwiftUIWindow("UserProfiles"); 
        Debug.Log("SwiftUIDriver: DisplayUserSelectionMenu called. Instructing SwiftUI to open UserProfiles view.");
    }

    public void DisplayReturningUserLogin(LocalUserProfileData userProfile)
    {
        if (userProfile == null)
        {
            Debug.LogError("SwiftUIDriver: DisplayReturningUserLogin called with null userProfile. Cannot proceed.");
            // Optionally, navigate back to user selection or show an error in SwiftUI
            // DisplayUserSelectionMenu(); 
            // SendMessageToSwiftUI("error|Null profile for returning user login");
            return;
        }

        // Define a simple DTO for sending to SwiftUI, to avoid sending unnecessary data
        var userProfileDto = new SwiftUserProfileDTO
        {
            Id = userProfile.Id, // Assuming UserData.Id is the Firebase UID or relevant ID for SwiftUI
            Name = userProfile.Name,
            Email = userProfile.Email
            // ProfilePictureUrl = userProfile.ProfilePicturePath // Add this if you implement profile pictures
        };
        string profileJson = JsonConvert.SerializeObject(userProfileDto);

        OpenSwiftUIWindow("ReturningUserLoginView"); // Ensure SwiftUI has a view with this name
        SendMessageToSwiftUI($"returningUserLoginData|{profileJson}");
        Debug.Log($"SwiftUIDriver: DisplayReturningUserLogin called for {userProfile.Name}. Instructing SwiftUI to open ReturningUserLoginView and sending data.");
    }

    public void DisplayProtocolMenu() { OpenSwiftUIWindow("ProtocolMenu"); }
    public void DisplayTimer(int? initialSeconds = null)
    {
        OpenSwiftUIWindow("Timer");
        if (initialSeconds.HasValue)
        {
            SendMessageToSwiftUI($"setTimer|{initialSeconds.Value}");
        }
    }
    public void DisplayCalculator() { OpenSwiftUIWindow("Calculator"); }
    public void DisplayWebPage(string url) { OpenSwiftSafariWindow(url); }
    public void DisplayLLMChat() { OpenSwiftUIWindow("LLMChat"); }
    public void DisplayVideoPlayer(string url) { OpenSwiftVideoWindow(url); }
    public void DisplayPDFReader(string url) { OpenSwiftPdfWindow(url); }
    public void DisplayUserRegistration() 
    {
        OpenSwiftUIWindow("UserRegistration"); // Assuming a SwiftUI view named "UserRegistration"
        Debug.Log("SwiftUIDriver: DisplayUserRegistration called. Instructing SwiftUI to open UserRegistration view.");
    }

    public void DisplayUserLogin() // Added to implement IUIDriver
    {
        OpenSwiftUIWindow("UserLogin"); // Assuming a SwiftUI view named "UserLogin"
        Debug.Log("SwiftUIDriver: DisplayUserLogin called. Instructing SwiftUI to open UserLogin view.");
    }

    public void DisplayDashboard() // Added to implement IUIDriver
    {
        OpenSwiftUIWindow("DashboardView"); // Assuming a SwiftUI view named "DashboardView"
        Debug.Log("SwiftUIDriver: DisplayDashboard called. Instructing SwiftUI to open DashboardView.");
    }

    public void DisplayProtocolView()
    {
        OpenSwiftUIWindow("ProtocolView"); // Assuming "ProtocolView" is the SwiftUI view name
        Debug.Log("SwiftUIDriver: DisplayProtocolView called. Instructing SwiftUI to open ProtocolView.");
    }
    #endregion

    #region Callback Methods (Implementing IUIDriver, Delegating to Handler)
    // These methods are called either by native SwiftUI messages (via HandleMessage)
    // or by internal systems (e.g., voice commands).
    // They delegate their core logic to the IUICallbackHandler.

    public async void UserSelectionCallback(string userID)
    {
        if (_uiCallbackHandler == null) { Debug.LogError("SwiftUIDriver: _uiCallbackHandler is null in UserSelectionCallback."); return; }
        Debug.Log($"SwiftUIDriver: UserSelectionCallback attempting for ID: {userID}");
        if (string.IsNullOrEmpty(userID))
        {
            Debug.LogWarning("SwiftUIDriver: UserSelectionCallback received null or empty userID.");
            await _uiCallbackHandler.HandleUserSelection(userID); // For consistent logging
            return;
        }
        try
        {
            await _uiCallbackHandler.HandleUserSelection(userID);
            Debug.Log("SwiftUIDriver: Profile set in SessionState via handler.");
            CloseSwiftUIWindow("UserProfiles");
            DisplayProtocolMenu();
        }
        catch (Exception ex)
        {
            Debug.LogError($"SwiftUIDriver: Error during user selection for {userID}: {ex.Message}");
            // SendMessageToSwiftUI($"userSelectionError|Failed to select profile: {ex.Message}");
        }
    }

    public void StepNavigationCallback(int stepIndex)
    {
        if (_uiCallbackHandler == null) { Debug.LogError("SwiftUIDriver: _uiCallbackHandler is null in StepNavigationCallback."); return; }
        _uiCallbackHandler.HandleStepNavigation(stepIndex);
    }

    public void CheckItemCallback(int index)
    {
        if (_uiCallbackHandler == null) { Debug.LogError("SwiftUIDriver: _uiCallbackHandler is null in CheckItemCallback."); return; }
        _uiCallbackHandler.HandleCheckItem(index);
    }

    public void UncheckItemCallback(int index)
    {
        if (_uiCallbackHandler == null) { Debug.LogError("SwiftUIDriver: _uiCallbackHandler is null in UncheckItemCallback."); return; }
        _uiCallbackHandler.HandleUncheckItem(index);
    }

    public void SignOffChecklistCallback()
    {
        if (_uiCallbackHandler == null) { Debug.LogError("SwiftUIDriver: _uiCallbackHandler is null in SignOffChecklistCallback."); return; }
        _uiCallbackHandler.HandleSignOffChecklist();

        var currentStep = ProtocolState.Instance?.CurrentStepState?.Value;
        if (currentStep != null)
        {
            var stepStateData = new StepStateData
            {
                CurrentStepIndex = ProtocolState.Instance.CurrentStep.Value,
                IsSignedOff = currentStep.SignedOff.Value,
                ChecklistState = currentStep.Checklist?.Select(item => new CheckItemStateData
                {
                    IsChecked = item.IsChecked.Value,
                    CheckIndex = currentStep.Checklist.IndexOf(item)
                }).ToList()
            };
            string json = JsonConvert.SerializeObject(stepStateData);
            SendMessageToSwiftUI($"stepChange|{json}");
        }
        else { Debug.LogWarning("SwiftUIDriver: CurrentStepState is null after SignOff, cannot send stepChange."); }
    }

    public void ProtocolSelectionCallback(string protocolDefinitionJson)
    {
        if (_uiCallbackHandler == null) { Debug.LogError("SwiftUIDriver: _uiCallbackHandler is null in ProtocolSelectionCallback."); return; }
        _uiCallbackHandler.HandleProtocolSelection(protocolDefinitionJson);
    }

    public void CloseProtocolCallback()
    {
        if (_uiCallbackHandler == null) { Debug.LogError("SwiftUIDriver: _uiCallbackHandler is null in CloseProtocolCallback."); return; }
        _uiCallbackHandler.HandleCloseProtocol();
        DisposeVoice?.Invoke(); // Swift-specific voice cleanup
        DisposeVoice = null;
        Debug.Log("SwiftUIDriver: CloseProtocolCallback - Voice Disposed.");
    }

    public void ChatMessageCallback(string message)
    {
        if (_uiCallbackHandler == null) { Debug.LogError("SwiftUIDriver: _uiCallbackHandler is null in ChatMessageCallback."); return; }
        _uiCallbackHandler.HandleChatMessage(message);
    }

    public async void LoginCallback(string username, string password)
    {
        if (_uiCallbackHandler == null) 
        { 
            Debug.LogError("SwiftUIDriver: _uiCallbackHandler is null in LoginCallback."); 
            SendAuthStatus(false); // Ensure auth status is sent if handler can't proceed
            return; 
        }
        Debug.Log($"SwiftUIDriver: Triggering Login via Handler for {username}");
        try
        {
            await _uiCallbackHandler.HandleLogin(username, password);
            // SendAuthStatus is handled by _authProvider event subscriptions
        }
        catch (Exception ex)
        {
            Debug.LogError($"SwiftUIDriver: Login via handler failed for {username}: {ex.Message}");
            SendAuthStatus(false); // Explicitly send false on handler error
        }
    }

    public async void CreateUserCallback(string userName)
    {
        if (_uiCallbackHandler == null) 
        { 
            Debug.LogError("SwiftUIDriver: _uiCallbackHandler is null in CreateUserCallback."); 
            OnUserProfilesChange(new List<LocalUserProfileData>()); 
            return; 
        }
        Debug.Log($"SwiftUIDriver: Creating user {userName} via handler");
        try
        {
            List<LocalUserProfileData> updatedProfiles = await _uiCallbackHandler.HandleCreateUser(userName);
            OnUserProfilesChange(updatedProfiles);
        }
        catch (Exception ex)
        {
            Debug.LogError($"SwiftUIDriver: Error creating user {userName} via handler: {ex.Message}");
            OnUserProfilesChange(new List<LocalUserProfileData>());
        }
    }

    public async void AuthRegistrationCallback(string displayName, string email, string password)
    {
        if (_uiCallbackHandler == null) 
        { 
            Debug.LogError("SwiftUIDriver: _uiCallbackHandler is null in AuthRegistrationCallback."); 
            // Consider sending a specific error message back to SwiftUI if needed
            // SendMessageToSwiftUI("registrationError|Internal error: UI Callback Handler not found.");
            return; 
        }
        Debug.Log($"SwiftUIDriver: Triggering AuthRegistration via Handler for {displayName} ({email})");
        try
        {
            await _uiCallbackHandler.HandleAuthRegistration(displayName, email, password);
            // Similar to LoginCallback, SendAuthStatus is handled by _authProvider event subscriptions
            // upon successful sign-up and subsequent sign-in.
            // If HandleAuthRegistration itself needs to directly inform SwiftUI of success/failure beyond auth state,
            // additional SendMessageToSwiftUI calls could be made here.
        }
        catch (Exception ex)
        {
            Debug.LogError($"SwiftUIDriver: AuthRegistration via handler failed for {email}: {ex.Message}");
            // SendMessageToSwiftUI($"registrationError|{ex.Message}"); // Inform SwiftUI about the error
        }
    }
    #endregion
    
    #region Voice Command Setup
    private void SetupVoiceCommands()
    {
        if (SpeechRecognizer.Instance == null) { Debug.LogWarning("SwiftUIDriver: SpeechRecognizer not found, cannot set up voice commands."); return; }
        DisposeVoice?.Invoke(); // Dispose previous commands

        var commands = new Dictionary<string, Action>();
        if (ProtocolState.Instance != null) // Check ProtocolState before accessing CurrentStep/CurrentCheckNum
        {
            commands.Add("check", () => {
                if (ProtocolState.Instance.CurrentStepState?.Value != null) CheckItemCallback(ProtocolState.Instance.CurrentCheckNum);
                else Debug.LogWarning("VoiceCommand 'check': CurrentStepState is null.");
            });
            commands.Add("uncheck", () => {
                 if (ProtocolState.Instance.CurrentStepState?.Value != null && ProtocolState.Instance.CurrentCheckNum > 0) UncheckItemCallback(ProtocolState.Instance.CurrentCheckNum - 1);
                 else Debug.LogWarning("VoiceCommand 'uncheck': CurrentStepState is null or CurrentCheckNum is 0.");
            });
            commands.Add("next", () => {
                if (ProtocolState.Instance.CurrentStep?.Value < ProtocolState.Instance.Steps?.Count -1 ) StepNavigationCallback(ProtocolState.Instance.CurrentStep.Value + 1);
                else Debug.LogWarning("VoiceCommand 'next': Already on last step or steps not available.");
            });
            commands.Add("previous", () => {
                if (ProtocolState.Instance.CurrentStep?.Value > 0) StepNavigationCallback(ProtocolState.Instance.CurrentStep.Value - 1);
                else Debug.LogWarning("VoiceCommand 'previous': Already on first step or steps not available.");
            });
        }
        commands.Add("sign", () => SignOffChecklistCallback());
        
        DisposeVoice = SpeechRecognizer.Instance.Listen(commands);
    }
    #endregion

    #region Native Message Handling (from SwiftUI)
    // Delegate for native callback
    private delegate void CallbackDelegate(string command);

    // Native callback handler method
    [MonoPInvokeCallback(typeof(CallbackDelegate))]
    private static void OnMessageReceived(string message)
    {
        // Ensure instance is available (it should be if constructor ran)
        if (Instance == null) { Debug.LogError("SwiftUIDriver.OnMessageReceived: Instance is null."); return; }
        Instance.HandleMessage(message);
    }

    // Method to process messages from SwiftUI
    private void HandleMessage(string message)
    {
        Debug.Log("SwiftUIDriver: Message Received from SwiftUI: " + message);
        
        if (ProtocolState.Instance == null && !message.StartsWith("requestUserProfiles") && !message.StartsWith("selectUser") && !message.StartsWith("createUser") && !message.StartsWith("login"))
        {
            Debug.LogError("SwiftUIDriver: ProtocolState.Instance is null. Ignoring message: " + message);
            return;
        }
        if (_uiCallbackHandler == null && !message.StartsWith("requestUserProfiles") && !message.StartsWith("requestProtocolDefinitions")) // Allow some initial requests if handler is not ready
        {
            Debug.LogError("SwiftUIDriver: _uiCallbackHandler is null. Ignoring message: " + message);
            return;
        }
        
        string[] parts = message.Split('|');
        string command = parts[0];
        string data = parts.Length > 1 ? parts[1] : string.Empty;

        try
        {
            switch (command)
            {
                // Commands delegating to IUICallbackHandler (via local methods)
                case "stepNavigation": StepNavigationCallback(int.Parse(data)); break;
                case "checkItem": CheckItemCallback(int.Parse(data)); break;
                case "uncheckItem": UncheckItemCallback(int.Parse(data)); break;
                case "selectProtocol": ProtocolSelectionCallback(data); break;
                case "checklistSignOff": SignOffChecklistCallback(); break;
                case "sendMessage": ChatMessageCallback(data); break;
                case "login": 
                    string[] loginData = data.Split(',');
                    if (loginData.Length == 2) LoginCallback(loginData[0], loginData[1]);
                    else Debug.LogError("SwiftUIDriver: Invalid login data format from SwiftUI.");
                    break;
                case "selectUser": UserSelectionCallback(data); break;
                case "createUser": CreateUserCallback(data); break;
                case "closeProtocol": CloseProtocolCallback(); break;

                // Commands for displaying UI (SwiftUI specific)
                case "requestVideo": DisplayVideoPlayer(data); break;
                case "requestPDF": DisplayPDFReader(data); break;
                case "requestTimer": 
                    if (int.TryParse(data, out int seconds))
                    {
                        DisplayTimer(seconds);
                    }
                    else
                    {
                        DisplayTimer();
                    }
                    break;
                case "requestWebpage": DisplayWebPage(data); break;
                
                // Data requests handled by SwiftUIDriver
                case "requestProtocolDefinitions": LoadProtocolDefinitions(); break;
                case "requestUserProfiles":
                    if (_fileManager == null) { Debug.LogError("SwiftUIDriver: IFileManager is null for requestUserProfiles."); break; }
                    _fileManager.GetLocalUserProfilesAsync().ToObservable().ObserveOnMainThread()
                        .Subscribe(
                            result => OnUserProfilesChange(result.Success && result.Data != null ? result.Data : new List<LocalUserProfileData>()),
                            error => { Debug.LogError($"SwiftUIDriver: Exception fetching user profiles: {error}"); OnUserProfilesChange(new List<LocalUserProfileData>()); }
                        ).AddTo(_disposables);
                    break;
                
                // case "downloadJsonProtocol": // Functionality commented out
                //    break;
                default:
                    Debug.LogWarning($"SwiftUIDriver: Unknown command received from SwiftUI: {command}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"SwiftUIDriver: Exception in HandleMessage for command '{command}': {ex.Message}\nStackTrace: {ex.StackTrace}");
        }
    }
    #endregion

    #region Internal Data Handling & Swift Communication Logic
    private async void LoadProtocolDefinitions() // Changed to async void as it awaits
    {
        if (_fileManager == null)
        {
            Debug.LogError("SwiftUIDriver: IFileManager not found for LoadProtocolDefinitions.");
            SendMessageToSwiftUI("protocolDefinitions|[]");
            return;
        }
        try
        {
            Result<List<ProtocolData>> result = await _fileManager.GetAvailableProtocolsAsync();
            if (result.Success && result.Data != null)
            {
                SendMessageToSwiftUI($"protocolDefinitions|{JsonConvert.SerializeObject(result.Data)}");
            }
            else
            {
                Debug.LogError($"SwiftUIDriver: Error loading available protocols: {result.Error?.Code} - {result.Error?.Message}");
                SendMessageToSwiftUI("protocolDefinitions|[]");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"SwiftUIDriver: Exception in LoadProtocolDefinitions: {ex.Message}");
            SendMessageToSwiftUI("protocolDefinitions|[]");
        }
    }

    /* DownloadJsonProtocolAsync and related callbacks commented out as per plan
    // public void DownloadJsonProtocolCallback()
    // {
    //     DownloadJsonProtocolAsync();
    // }
    // private async Task<string> DownloadJsonProtocolAsync()
    // { ... code ... }
    */
    #endregion

    #region Data Structures for SwiftUI Communication
    public class StepStateData
    {
        public int CurrentStepIndex { get; set; }
        public bool IsSignedOff { get; set; }
        public List<CheckItemStateData> ChecklistState { get; set; }
    }

    public class CheckItemStateData
    {
        public bool IsChecked { get; set; }
        public int CheckIndex { get; set; }
    }

    // DTO for sending essential user profile info to SwiftUI for the returning user login screen
    public class SwiftUserProfileDTO
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        // public string ProfilePictureUrl { get; set; } // Uncomment if you add profile picture URLs
    }
    #endregion

    #region DllImports for SwiftUI Communication
    #if UNITY_VISIONOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void SendMessageToSwiftUI(string message);
    [DllImport("__Internal")] private static extern void SetNativeCallback(CallbackDelegate callback);
    [DllImport("__Internal")] private static extern void OpenSwiftUIWindow(string name);
    [DllImport("__Internal")] private static extern void CloseSwiftUIWindow(string name);
    [DllImport("__Internal")] private static extern void OpenSwiftTimerWindow(int duration);
    [DllImport("__Internal")] private static extern void OpenSwiftSafariWindow(string urlString);
    [DllImport("__Internal")] private static extern void OpenSwiftVideoWindow(string videoTitle);
    [DllImport("__Internal")] private static extern void OpenSwiftPdfWindow(string pdfUrlString);
    #else // Fallback for editor or other platforms
    private static void SendMessageToSwiftUI(string message) { Debug.Log($"Simulated SendMessageToSwiftUI: {message}"); }
    private static void SetNativeCallback(CallbackDelegate callback) { Debug.Log("Simulated SetNativeCallback called."); }
    private static void OpenSwiftUIWindow(string name) { Debug.Log($"Simulated OpenSwiftUIWindow: {name}"); }
    private static void CloseSwiftUIWindow(string name) { Debug.Log($"Simulated CloseSwiftUIWindow: {name}"); }
    private static void OpenSwiftTimerWindow(int duration) { Debug.Log($"Simulated OpenSwiftTimerWindow: {duration}"); }
    private static void OpenSwiftSafariWindow(string urlString) { Debug.Log($"Simulated OpenSwiftSafariWindow: {urlString}"); }
    private static void OpenSwiftVideoWindow(string videoTitle) { Debug.Log($"Simulated OpenSwiftVideoWindow: {videoTitle}"); }
    private static void OpenSwiftPdfWindow(string pdfUrlString) { Debug.Log($"Simulated OpenSwiftPdfWindow: {pdfUrlString}"); }
    #endif
    #endregion

    public void RequestSignOut() // Added to implement IUIDriver
    {
        Debug.Log("SwiftUIDriver: RequestSignOut called. Actual sign-out logic for SwiftUI needs implementation if used.");
        // Typically, this would involve: 
        // 1. Calling SessionManager.Instance?.AttemptSignOut();
        // 2. Potentially sending a message to SwiftUI if it needs to react specifically beyond HandleSignOutSuccess.
        // For now, HandleSignOutSuccess should trigger SendAuthStatus(false) and then SessionManager should trigger DisplayUserSelection.
        SessionManager.instance?.SignOut(); // Call SessionManager to handle the actual sign-out logic
    }
}
