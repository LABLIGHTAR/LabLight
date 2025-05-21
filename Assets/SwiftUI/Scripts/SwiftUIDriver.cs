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

public class SwiftUIDriver : IUIDriver, IDisposable
{
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

    private CompositeDisposable _disposables = new CompositeDisposable();
    private Action DisposeVoice;

    private IAuthProvider authProvider;

    public SwiftUIDriver()
    {
        SetNativeCallback(OnMessageReceived);
    }

    public void Initialize()
    {
        authProvider = ServiceRegistry.GetService<IAuthProvider>();
        if (authProvider != null)
        {
            authProvider.OnSignInSuccess += (_) => SendAuthStatus(true);
            authProvider.OnAuthError += (_) => SendAuthStatus(false);
        }
        else
        {
            Debug.LogError("IAuthProvider is not available");
        }


        if (ProtocolState.Instance != null)
        {
            _disposables.Add(ProtocolState.Instance.ProtocolStream.Subscribe(OnProtocolChange));
            _disposables.Add(ProtocolState.Instance.StepStream.Subscribe(OnStepChange));
            _disposables.Add(ProtocolState.Instance.ChecklistStream.Subscribe(OnCheckItemChange));
            _disposables.Add(SessionState.JsonFileDownloadable.Subscribe(OnJsonFileDownloadableChange));
        }
        else
        {
            Debug.LogWarning("ProtocolState.Instance is null during SwiftUIDriver initialization");
        }

        var chatProvider = ServiceRegistry.GetService<ILLMChatProvider>();
        if (chatProvider != null)
        {
            chatProvider.OnResponse.AddListener(OnChatMessageReceived);
        }
        else
        {
            Debug.LogWarning("ILLMChatProvider is null during SwiftUIDriver initialization");
        }
    }

    public void Dispose()
    {
        _disposables.Dispose();
        var chatProvider = ServiceRegistry.GetService<ILLMChatProvider>();
        if (chatProvider != null)
        {
            chatProvider.OnResponse.RemoveListener(OnChatMessageReceived);
        }

        // Ensure voice commands are disposed
        DisposeVoice?.Invoke();
        DisposeVoice = null;
    }

    // Swift UI Update methods
    public void OnProtocolChange(ProtocolDefinition protocol)
    {
        if (protocol == null)
        {
            return;
        }

        Debug.Log("######LABLIGHT SWIFTUIDRIVER OnProtocolChange: " + protocol.title);
        string protocolJson = JsonConvert.SerializeObject(protocol);
        SendMessageToSwiftUI($"protocolChange|{protocolJson}");

        SetupVoiceCommands();
    }

    private void SetupVoiceCommands()
    {
        if (SpeechRecognizer.Instance == null)
        {
            Debug.LogWarning("SpeechRecognizer not found");
            return;
        }

        // Dispose any existing voice commands first
        DisposeVoice?.Invoke();

        DisposeVoice = SpeechRecognizer.Instance.Listen(new Dictionary<string, Action>()
        {
            {"check", () => CheckItemCallback(ProtocolState.Instance.CurrentCheckNum)},
            {"uncheck", () => UncheckItemCallback(ProtocolState.Instance.CurrentCheckNum - 1)},
            {"sign", () => SignOffChecklistCallback()},
            {"next", () => StepNavigationCallback(ProtocolState.Instance.CurrentStep.Value + 1)},
            {"previous", () => StepNavigationCallback(ProtocolState.Instance.CurrentStep.Value - 1)},
        });
    }

    public void OnStepChange(ProtocolState.StepState stepState)
    {
        var stepStateData = new StepStateData
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
            CheckIndex = index
        }).ToList();

        string checkItemStatesJson = JsonConvert.SerializeObject(checkItemStateDataList);
        SendMessageToSwiftUI($"checkItemChange|{checkItemStatesJson}");

        var currentCheckItem = ProtocolState.Instance.CurrentCheckItemDefinition;
        if (currentCheckItem != null)
        {
            foreach(var arAction in currentCheckItem.arActions)
            {
                if (arAction.actionType == "Timer")
                {
                    if (arAction.properties.TryGetValue("duration", out object durationObj) && 
                        int.TryParse(durationObj.ToString(), out int seconds))
                    {
                        DisplayTimer(seconds);
                    }
                }
            }
        }
    }

    public void OnJsonFileDownloadableChange(string jsonFileInfo)
    {
        if(string.IsNullOrEmpty(jsonFileInfo))
        {
            SendMessageToSwiftUI($"jsonFileDownloadableChange|{jsonFileInfo}");
        }
    }

    public void OnChatMessageReceived(string message)
    {
        SendMessageToSwiftUI($"LLMChatMessage|{message}");
    }

    public void SendAuthStatus(bool isAuthenticated)
    {
        Debug.Log("######LABLIGHT SWIFTUIDRIVER sending auth status to Swift: " + isAuthenticated);
        SendMessageToSwiftUI($"authStatus|{isAuthenticated}");
    }

    //Swift UI Display methods
    public void DisplayUserSelection()
    {
        OpenSwiftUIWindow("UserProfiles");
    }

    public void DisplayProtocolMenu()
    {
        OpenSwiftUIWindow("ProtocolMenu");
    }
    
    public void DisplayTimer(int seconds)
    {
        OpenSwiftTimerWindow(seconds);
    }

    public void DisplayCalculator()
    {
        OpenSwiftUIWindow("Calculator");
    }

    public void DisplayWebPage(string url)
    {
        OpenSwiftSafariWindow(url);
    }

    public void DisplayLLMChat()
    {
        OpenSwiftUIWindow("LLMChat");
    }

    public void DisplayVideoPlayer(string url)
    {
        OpenSwiftVideoWindow(url);
    }

    public void DisplayPDFReader(string url)
    {
        OpenSwiftPdfWindow(url);
    }

    // Unity Callback Methods
    public void UserSelectionCallback(string userID)
    {
        Debug.Log($"######LABLIGHT Starting UserSelectionCallback for ID: {userID}");
        var profileProvider = ServiceRegistry.GetService<IUserProfileDataProvider>();
        if (profileProvider == null)
        {
            Debug.LogError("######LABLIGHT IUserProfileDataProvider is null");
            return;
        }

        profileProvider.GetOrCreateUserProfile(userID).Subscribe(
            profile => {
                Debug.Log("######LABLIGHT Profile loaded successfully");
                if (profile == null)
                {
                    Debug.LogError("######LABLIGHT Profile is null after loading");
                    return;
                }
                SessionState.currentUserProfile = profile;
                Debug.Log("######LABLIGHT Profile set in SessionState");
                
                // Close UserProfiles window first
                CloseSwiftUIWindow("UserProfiles");
                Debug.Log("######LABLIGHT UserProfiles window closed");
                
                // Then open Protocol Menu
                Debug.Log("######LABLIGHT About to display protocol menu");
                DisplayProtocolMenu();
            },
            error => {
                Debug.LogError($"######LABLIGHT Error in UserSelectionCallback: {error}");
            }
        );
    }

    public void StepNavigationCallback(int stepIndex)
    {
        ProtocolState.Instance.SetStep(stepIndex);
    }

    public void CheckItemCallback(int index)
    {
        var currentStepState = ProtocolState.Instance.CurrentStepState.Value;
        if (currentStepState == null || currentStepState.Checklist == null)
        {
            return;
        }

        if (index < 0 || index >= currentStepState.Checklist.Count)
        {
            return;
        }

        currentStepState.Checklist[index].IsChecked.Value = true;
        currentStepState.Checklist[index].CompletionTime.Value = DateTime.Now;
        
        if(index + 1 < currentStepState.Checklist.Count)
        {
            ProtocolState.Instance.SetCheckItem(index + 1);
        }
        else
        {
            ProtocolState.Instance.SetCheckItem(index);
        }
    }

    public void UncheckItemCallback(int index)
    {
        ProtocolState.Instance.CurrentStepState.Value.Checklist[index].IsChecked.Value = false;
        if(index - 1 >= 0)
        {
            ProtocolState.Instance.SetCheckItem(index - 1);
        }
        else
        {
            ProtocolState.Instance.SetCheckItem(index);
        }
    }

    public void SignOffChecklistCallback()
    {
        ProtocolState.Instance.SignOff();
        var currentStep = ProtocolState.Instance.CurrentStepState.Value;
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

    public void ProtocolSelectionCallback(string protocolDefinitionJson)
    {
        var protocolDefinition = Parsers.ParseProtocol(protocolDefinitionJson);
        ProtocolState.Instance.SetProtocolDefinition(protocolDefinition);
    }

    public void CloseProtocolCallback()
    {
        Debug.Log("######LABLIGHT SWIFTUIDRIVER CloseProtocolCallback");
        // Dispose of voice commands when protocol is closed
        DisposeVoice?.Invoke();
        DisposeVoice = null;
        
        // Add this line to clear all voice commands
        SpeechRecognizer.Instance.ClearAllKeywords();

        ProtocolState.Instance.ActiveProtocol.Value = null;
        SceneLoader.Instance.LoadSceneClean("ProtocolMenu");
    }

    public void ChatMessageCallback(string message)
    {
        ServiceRegistry.GetService<ILLMChatProvider>().QueryAsync(message);
    }

    public void LoginCallback(string username, string password)
    {
        Debug.Log("######LABLIGHT SWIFTUIDRIVER triggering Login Callback: " + username + " " + password);
        if (authProvider != null)
        {
            authProvider.SignIn(username, password);
        }
        else
        {
            Debug.LogError("IAuthProvider is not available");
            SendAuthStatus(false);
        }
    }

    public void DownloadJsonProtocolCallback()
    {
        DownloadJsonProtocolAsync();
        LoadProtocolDefinitions();
    }

    public void OnUserProfilesChange(List<UserProfileData> profiles)
    {
        var profilesData = profiles.Select(p => new { userId = p.GetUserId(), name = p.GetName() }).ToList();
        string profilesJson = JsonConvert.SerializeObject(profilesData);
        SendMessageToSwiftUI($"userProfiles|{profilesJson}");
    }

    // Native callback handler
    [MonoPInvokeCallback(typeof(CallbackDelegate))]
    private static void OnMessageReceived(string message)
    {
        Instance.HandleMessage(message);
    }

    //Handle message passing from SwiftUI
    private void HandleMessage(string message)
    {
        Debug.Log("######LABLIGHT SWIFTUIDRIVER Message Received from SwiftUI " + message);
        
        if (ProtocolState.Instance == null)
        {
            Debug.LogError("######LABLIGHT SWIFTUIDRIVER HandleMessage - ProtocolState.Instance is null");
            return;
        }
        
        string[] parts = message.Split('|');
        string command = parts[0];
        string data = parts.Length > 1 ? parts[1] : string.Empty;

        try
        {
            switch (command)
            {
                case "stepNavigation":
                    StepNavigationCallback(int.Parse(data));
                    break;
                case "checkItem":
                    CheckItemCallback(int.Parse(data));
                    break;
                case "uncheckItem":
                    UncheckItemCallback(int.Parse(data));
                    break;
                case "selectProtocol":
                    ProtocolSelectionCallback(data);
                    break;
                case "checklistSignOff":
                    SignOffChecklistCallback();
                    break;
                case "sendMessage":
                    ChatMessageCallback(data);
                    break;
                case "login":
                    string[] loginData = data.Split(',');
                    if (loginData.Length == 2)
                    {
                        Debug.Log("######LABLIGHT SWIFTUIDRIVER triggering Login Callback: " + loginData[0] + " " + loginData[1]);
                        LoginCallback(loginData[0], loginData[1]);
                    }
                    break;
                case "requestProtocolDefinitions":
                    LoadProtocolDefinitions();
                    break;
                case "requestVideo":
                    Debug.Log($"######LABLIGHT SWIFTUIDRIVER displaying video: {data}");
                    DisplayVideoPlayer(data);
                    break;
                case "requestPDF":
                    Debug.Log($"######LABLIGHT SWIFTUIDRIVER displaying PDF: {data}");
                    DisplayPDFReader(data);
                    break;
                case "requestTimer":
                    if (int.TryParse(data, out int seconds))
                    {
                        Debug.Log($"######LABLIGHT SWIFTUIDRIVER displaying timer: {seconds} seconds");
                        DisplayTimer(seconds);
                    }
                    else
                    {
                        Debug.LogError($"######LABLIGHT SWIFTUIDRIVER invalid timer duration: {data}");
                    }
                    break;
                case "requestWebpage":
                    Debug.Log($"######LABLIGHT SWIFTUIDRIVER opening webpage: {data}");
                    DisplayWebPage(data);
                    break;
                case "closeProtocol":
                    CloseProtocolCallback();
                    break;
                case "downloadJsonProtocol":
                    DownloadJsonProtocolCallback();
                    break;
                case "requestUserProfiles":
                    var userProfileProvider = ServiceRegistry.GetService<IUserProfileDataProvider>();
                    userProfileProvider.GetAllUserProfiles()
                        .ObserveOnMainThread()
                        .Subscribe(OnUserProfilesChange);
                    break;
                case "selectUser":
                    UserSelectionCallback(data);
                    break;
                case "createUser":
                    var provider = ServiceRegistry.GetService<IUserProfileDataProvider>();
                    provider.SaveUserProfileData(data, new UserProfileData(data));
                    provider.GetAllUserProfiles()
                        .ObserveOnMainThread()
                        .Subscribe(OnUserProfilesChange);
                    break;
                // Add more cases as needed
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"######LABLIGHT SWIFTUIDRIVER HandleMessage - Exception in command {command}: {ex.Message}\nStackTrace: {ex.StackTrace}");
        }
    }

    private async void LoadProtocolDefinitions()
    {
        var protocolDataProvider = ServiceRegistry.GetService<IProtocolDataProvider>();
        if (protocolDataProvider == null)
        {
            Debug.LogError("IProtocolDataProvider not found in ServiceRegistry");
            return;
        }

        try
        {
            var protocolDefinitions = await protocolDataProvider.GetProtocolList();
            protocolDefinitions.AddRange(await ((LocalFileDataProvider)ServiceRegistry.GetService<ITextDataProvider>())?.GetProtocolList());
            string protocolDefinitionsJson = JsonConvert.SerializeObject(protocolDefinitions);
            SendMessageToSwiftUI($"protocolDefinitions|{protocolDefinitionsJson}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error loading protocol list: {ex.Message}");
        }
    }

    private async Task<string> DownloadJsonProtocolAsync()
    {
        string fileServerUri = ServiceRegistry.GetService<ILighthouseControl>()?.GetFileServerUri();

        if (!string.IsNullOrEmpty(fileServerUri))
        {
            string uri;

            bool filenameKnown = !string.IsNullOrEmpty(SessionState.JsonFileDownloadable.Value);
            if (filenameKnown)
            {
                uri = fileServerUri + "/GetFile?Filename=" + SessionState.JsonFileDownloadable.Value;
            }
            else
            {
                uri = fileServerUri + "/GetProtocolJson";
            }

            Debug.Log("Downloading from " + uri);

            UnityWebRequest request = UnityWebRequest.Get(uri);
            request.SendWebRequest();

            while (!request.isDone)
            {
                await Task.Yield();
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                var fileName = filenameKnown ? SessionState.JsonFileDownloadable.Value : request.GetResponseHeader("File-Name");

                if (!string.IsNullOrEmpty(fileName))
                {
                    var protocolName = Path.GetDirectoryName(fileName);

                    var lfdp = new LocalFileDataProvider();
                    
                    lfdp.SaveTextFile(protocolName + ".json", request.downloadHandler.text);
                }
                else
                {
                    Debug.LogError("There is no 'File-Name' in the response header.");
                }

                SessionState.JsonFileDownloadable.Value = string.Empty;
            }
            else
            {
                Debug.LogError(request.error);
            }
        }
        else
        {
            Debug.LogError("Could not retrieve FileServerUri from LightHouse");
        }

        return null;
    }

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

    // DllImports
    #if UNITY_VISIONOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SendMessageToSwiftUI(string message);

    [DllImport("__Internal")]
    private static extern void SetNativeCallback(CallbackDelegate callback);

    [DllImport("__Internal")]
    private static extern void OpenSwiftUIWindow(string name);

    [DllImport("__Internal")]
    private static extern void CloseSwiftUIWindow(string name);

    [DllImport("__Internal")]
    private static extern void OpenSwiftTimerWindow(int duration);

    [DllImport("__Internal")]
    private static extern void OpenSwiftSafariWindow(string urlString);

    [DllImport("__Internal")]
    private static extern void OpenSwiftVideoWindow(string videoTitle);

    [DllImport("__Internal")]
    private static extern void OpenSwiftPdfWindow(string pdfUrlString);
    #else
    private static void SendMessageToSwiftUI(string message) { Debug.Log($"SendMessageToSwiftUI: {message}"); }
    private static void SetNativeCallback(CallbackDelegate callback) { }
    private static void OpenSwiftUIWindow(string name) { Debug.Log($"OpenSwiftUIWindow: {name}"); }
    private static void CloseSwiftUIWindow(string name) { Debug.Log($"CloseSwiftUIWindow: {name}"); }
    private static void OpenSwiftTimerWindow(int duration) { Debug.Log($"OpenSwiftTimerWindow: {duration}"); }
    private static void OpenSwiftSafariWindow(string urlString) { Debug.Log($"OpenSwiftSafariWindow: {urlString}"); }
    private static void OpenSwiftVideoWindow(string videoTitle) { Debug.Log($"OpenSwiftVideoWindow: {videoTitle}"); }
    private static void OpenSwiftPdfWindow(string pdfUrlString) { Debug.Log($"OpenSwiftPdfWindow: {pdfUrlString}"); }
    #endif

    private delegate void CallbackDelegate(string command);
}
