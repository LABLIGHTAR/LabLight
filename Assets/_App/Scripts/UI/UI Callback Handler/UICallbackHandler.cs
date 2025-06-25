using UnityEngine;
using System;
using System.Linq; // Added for FirstOrDefault, Skip etc.
using System.Threading.Tasks;
using Newtonsoft.Json; // For HandleProtocolSelection, assuming ProtocolDefinition might be used
using System.Collections.Generic; // Added for KeyNotFoundException
// We'll need to see what other using statements are required as we fill in methods.

/// <summary>
/// Handles core logic for UI callbacks, providing a centralized implementation
/// for actions triggered by various UI drivers.
/// </summary>
public class UICallbackHandler : IUICallbackHandler
{
    #region Private Fields & Dependencies
    private readonly IFileManager _fileManager;
    private readonly IAuthProvider _authProvider;
    private readonly IDatabase _database;
    private ILLMChatProvider _llmChatProvider;
    #endregion

    #region Constructor
    public UICallbackHandler(IFileManager fileManager, IAuthProvider authProvider, IDatabase database)
    {
        // Dependencies are injected
        _fileManager = fileManager;
        _authProvider = authProvider;
        _database = database;
        _llmChatProvider = ServiceRegistry.GetService<ILLMChatProvider>();

        if (_authProvider == null) Debug.LogError("UICallbackHandler: IAuthProvider is not available from ServiceRegistry.");
        if (_fileManager == null) Debug.LogError("UICallbackHandler: IFileManager is not available from ServiceRegistry.");
        if (_llmChatProvider == null) Debug.LogError("UICallbackHandler: ILLMChatProvider is not available from ServiceRegistry.");
        if (_database == null) Debug.LogError("UICallbackHandler: IDatabase is not available from ServiceRegistry.");
    }
    #endregion

    #region User Session & Profile Callbacks
    public async Task HandleUserSelection(string userId)
    {
        Debug.Log($"UICallbackHandler: Starting UserSelection for ID: {userId}");
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogWarning("UICallbackHandler: HandleUserSelection received null or empty userID.");
            return;
        }

        if (_fileManager == null)
        {
            Debug.LogError("UICallbackHandler: IFileManager is null in HandleUserSelection.");
            throw new InvalidOperationException("IFileManager is not available.");
        }

        try
        {
            var result = await _fileManager.GetAllLocalUserProfilesAsync();
            if (result.Success && result.Data != null)
            {
                var selectedProfile = result.Data.FirstOrDefault(p => p.Id == userId);
                if (selectedProfile != null)
                {
                    Debug.Log("UICallbackHandler: Profile loaded successfully using FileManager.");
                    SessionState.currentUserProfile = selectedProfile;
                    Debug.Log("UICallbackHandler: Profile set in SessionState.");
                }
                else
                {
                    Debug.LogError($"UICallbackHandler: Profile with ID {userId} not found.");
                    throw new KeyNotFoundException($"Profile with ID {userId} not found.");
                }
            }
            else
            {
                Debug.LogError($"UICallbackHandler: Error loading local user profiles: {result.Error?.Code} - {result.Error?.Message}");
                throw new Exception($"Error loading local user profiles: {result.Error?.Code} - {result.Error?.Message}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"UICallbackHandler: Exception in HandleUserSelection: {ex}");
            throw;
        }
    }

    public async Task HandleLogin(string username, string password)
    {
        if (_authProvider == null)
        {
            Debug.LogError("UICallbackHandler: IAuthProvider is not available in HandleLogin.");
            throw new InvalidOperationException("IAuthProvider is not available.");
        }
        if (_fileManager == null)
        {
            Debug.LogError("UICallbackHandler: IFileManager is not available in HandleLogin.");
            throw new InvalidOperationException("IFileManager is not available.");
        }

        Debug.Log($"UICallbackHandler: Attempting login for user: {username}");
        try
        {
            await _authProvider.SignIn(username, password);
            Debug.Log($"UICallbackHandler: SignIn successful for {username}. Checking/creating local profile.");

            // Use properties from IAuthProvider
            string userId = _authProvider.CurrentUserId;
            string userEmail = _authProvider.CurrentUserEmail;

            if (string.IsNullOrEmpty(userId))
            {
                Debug.LogError($"UICallbackHandler: SignIn for {username} seemed successful, but failed to retrieve valid user ID from IAuthProvider.");
                throw new Exception("Authentication succeeded, but failed to retrieve user ID to finalize session setup.");
            }
            
            // The SessionManager will now be solely responsible for profile creation/loading 
            // after both auth and DB connection are successful. This handler's job is just to auth.
            // We can, however, try to pre-load an existing profile into the session state if it exists locally.

            var profilesResult = await _fileManager.GetAllLocalUserProfilesAsync();
            if (profilesResult.Success && profilesResult.Data != null)
            {
                var userProfile = profilesResult.Data.FirstOrDefault(p => p.Id == userId);
                if (userProfile != null)
                {
                    SessionState.currentUserProfile = userProfile;
                    Debug.Log($"UICallbackHandler: Pre-loaded existing local profile for {userProfile.Name} into SessionState.");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"UICallbackHandler: Exception during login process for {username}: {ex.Message} - StackTrace: {ex.StackTrace}");
            SessionState.currentUserProfile = null; // Ensure session state is cleared on any login failure
            throw; 
        }
    }

    public async Task<List<LocalUserProfileData>> HandleCreateUser(string userName)
    {
        if (_fileManager == null)
        {
            Debug.LogError("UICallbackHandler: IFileManager is null in HandleCreateUser.");
            throw new InvalidOperationException("IFileManager is not available for creating user.");
        }

        if (string.IsNullOrWhiteSpace(userName))
        {
            Debug.LogError("UICallbackHandler: User name cannot be empty for HandleCreateUser.");
            throw new ArgumentException("User name cannot be empty.", nameof(userName));
        }

        Debug.Log($"UICallbackHandler: Creating new user profile with name: {userName}");
        var newUserProfile = new LocalUserProfileData
        {
            Id = Guid.NewGuid().ToString(),
            Name = userName,
            Email = "",
            CreatedAtUtc = DateTime.UtcNow,
            LastOnlineUtc = DateTime.UtcNow,
            IsOnline = false
        };

        try
        {
            ResultVoid saveResult = await _fileManager.SaveLocalUserProfileAsync(newUserProfile);
            if (saveResult.Success)
            {
                Debug.Log($"UICallbackHandler: Successfully saved new user profile {userName} with ID {newUserProfile.Id}.");
                Result<List<LocalUserProfileData>> profilesResult = await _fileManager.GetAllLocalUserProfilesAsync();
                if (profilesResult.Success && profilesResult.Data != null)
                {
                    Debug.Log($"UICallbackHandler: Successfully fetched updated user profiles list after creating user {userName}.");
                    return profilesResult.Data;
                }
                else
                {
                    Debug.LogError($"UICallbackHandler: Error fetching user profiles after creating user {userName}: {profilesResult.Error?.Code} - {profilesResult.Error?.Message}");
                    throw new Exception($"Failed to fetch profiles after creating user: {profilesResult.Error?.Code} - {profilesResult.Error?.Message}");
                }
            }
            else
            {
                Debug.LogError($"UICallbackHandler: Error creating user profile {userName}: {saveResult.Error?.Code} - {saveResult.Error?.Message}");
                throw new Exception($"Error creating user profile: {saveResult.Error?.Code} - {saveResult.Error?.Message}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"UICallbackHandler: Exception during HandleCreateUser for {userName}: {ex}");
            throw;
        }
    }

    public async Task HandleAuthRegistration(string displayName, string email, string password)
    {
        if (_authProvider == null)
        {
            Debug.LogError("UICallbackHandler: IAuthProvider is not available in HandleAuthRegistration.");
            throw new InvalidOperationException("IAuthProvider is not available.");
        }
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(displayName))
        {
            Debug.LogError("UICallbackHandler: DisplayName, Email, or password cannot be empty for HandleAuthRegistration.");
            throw new ArgumentException("DisplayName, Email, or password cannot be empty.");
        }

        // Store details for local profile creation after successful Firebase sign-up & sign-in
        SessionState.PendingDisplayName = displayName;
        SessionState.PendingEmail = email;

        Debug.Log($"UICallbackHandler: Attempting registration for user: {displayName} ({email}). Pending details stored.");
        try
        {
            // Step 1: Create the Firebase user
            _authProvider.SignUp(email, password); // Call SignUp directly
            Debug.Log($"UICallbackHandler: Firebase SignUp initiated for {email}. Waiting for AuthStateChanged to confirm sign-in.");

            // Step 2: Local profile creation will now be handled in SessionManager.HandleAuthSignInSuccessToken
            // based on SessionState.PendingDisplayName/Email and the FirebaseUserId obtained upon sign-in.
        }
        catch (Exception ex)
        {
            Debug.LogError($"UICallbackHandler: Exception during HandleAuthRegistration for {email}: {ex.Message}");
            // Clear pending details on error to prevent incorrect profile creation later
            SessionState.PendingDisplayName = null;
            SessionState.PendingEmail = null;
            // Re-throw so the caller (UnityUIDriver) can potentially update the UI with the error.
            throw;
        }
    }
    #endregion

    #region Protocol Navigation & Interaction Callbacks
    public void HandleStepNavigation(int stepIndex)
    {
        if (ProtocolState.Instance == null)
        {
            Debug.LogError("UICallbackHandler: ProtocolState.Instance is null in HandleStepNavigation.");
            return;
        }

        if (ProtocolState.Instance.Steps != null && (stepIndex < 0 || stepIndex >= ProtocolState.Instance.Steps.Count))
        {
            Debug.LogWarning($"UICallbackHandler: Step navigation to index {stepIndex} is out of bounds.");
            return;
        }
        Debug.Log($"UICallbackHandler: Navigating to step {stepIndex}");
        ProtocolState.Instance.SetStep(stepIndex);
    }

    public void HandleCheckItem(int index)
    {
        if (ProtocolState.Instance == null || ProtocolState.Instance.CurrentStepState.Value == null || ProtocolState.Instance.CurrentStepState.Value.Checklist == null)
        {
            Debug.LogWarning("UICallbackHandler: Cannot check item, ProtocolState or Checklist is not properly initialized.");
            return;
        }

        var currentStepState = ProtocolState.Instance.CurrentStepState.Value;
        if (index < 0 || index >= currentStepState.Checklist.Count)
        {
            Debug.LogWarning($"UICallbackHandler: CheckItem index {index} is out of bounds.");
            return;
        }

        currentStepState.Checklist[index].IsChecked.Value = true;
        currentStepState.Checklist[index].CompletionTime.Value = DateTime.Now;

        var nextUncheckedItem = currentStepState.Checklist
            .Skip(index + 1)
            .FirstOrDefault(item => !item.IsChecked.Value);

        if (nextUncheckedItem != null)
        {
            ProtocolState.Instance.SetCheckItem(currentStepState.Checklist.IndexOf(nextUncheckedItem));
        }
        else
        {
            ProtocolState.Instance.SetCheckItem(index);
        }
    }

    public void HandleUncheckItem(int index)
    {
        if (ProtocolState.Instance == null || ProtocolState.Instance.CurrentStepState.Value == null || ProtocolState.Instance.CurrentStepState.Value.Checklist == null)
        {
            Debug.LogWarning("UICallbackHandler: Cannot uncheck item, ProtocolState or Checklist is not properly initialized.");
            return;
        }
        
        var currentStepState = ProtocolState.Instance.CurrentStepState.Value;
        if (index < 0 || index >= currentStepState.Checklist.Count)
        {
            Debug.LogWarning($"UICallbackHandler: UncheckItem index {index} is out of bounds.");
            return;
        }

        currentStepState.Checklist[index].IsChecked.Value = false;

        if (index - 1 >= 0)
        {
            ProtocolState.Instance.SetCheckItem(index - 1);
        }
        else
        {
            ProtocolState.Instance.SetCheckItem(index);
        }
    }

    public void HandleSignOffChecklist()
    {
        if (ProtocolState.Instance == null)
        {
            Debug.LogError("UICallbackHandler: ProtocolState.Instance is null in HandleSignOffChecklist.");
            return;
        }
        ProtocolState.Instance.SignOff();
    }

    public void HandleProtocolSelection(string protocolJson)
    {
        if (ProtocolState.Instance == null)
        {
            Debug.LogError("UICallbackHandler: ProtocolState.Instance is null in HandleProtocolSelection.");
            return;
        }
        try
        {
            var protocolDefinition = Parsers.ParseProtocol(protocolJson);
            if (protocolDefinition == null)
            {
                Debug.LogError("UICallbackHandler: Parsed ProtocolDefinition is null.");
                return;
            }
            ProtocolState.Instance.SetProtocolDefinition(protocolDefinition);
        }
        catch (Exception ex)
        {
            Debug.LogError($"UICallbackHandler: Error parsing or setting protocol definition: {ex.Message}");
        }
    }

    public void HandleCloseProtocol()
    {
        if (ProtocolState.Instance == null)
        {
            Debug.LogError("UICallbackHandler: ProtocolState.Instance is null in HandleCloseProtocol.");
            return;
        }
        if (SpeechRecognizer.Instance != null)
        {
            SpeechRecognizer.Instance.ClearAllKeywords();
        }
        else
        {
            Debug.LogWarning("UICallbackHandler: SpeechRecognizer.Instance is null in HandleCloseProtocol. Cannot clear keywords if it's unavailable.");
        }
        ProtocolState.Instance.ActiveProtocol.Value = null;
    }

    public void HandleDeleteProtocol(uint protocolId)
    {
        if (_database == null)
        {
            Debug.LogError("UICallbackHandler: IDatabase service is not available. Cannot delete protocol.");
            return;
        }
        Debug.Log($"UICallbackHandler: Requesting deletion of protocol ID: {protocolId}");
        _database.DeleteProtocol(protocolId);
        // After deletion, the UI list should refresh. This might happen via database events
        // or the calling UI controller might need to explicitly refresh its list.
    }
    #endregion

    #region Communication Callbacks
    public void HandleChatMessage(string message)
    {
        if (_llmChatProvider == null)
        {
            Debug.LogError("UICallbackHandler: ILLMChatProvider is not available in HandleChatMessage.");
            return;
        }
        _llmChatProvider.QueryAsync(message);
    }
    #endregion

    // Placeholder for RequestSignOut if it's not handled elsewhere
    public void RequestSignOut()
    {
        Debug.Log("UICallbackHandler: RequestSignOut called. Actual sign-out logic should be handled by SessionManager or AuthProvider.");
        // Example: SessionManager.Instance?.SignOut(); 
        // Or: _authProvider?.SignOut();
        // If IUICallbackHandler is meant to trigger it, then:
        // _authProvider?.SignOut(); // Or equivalent method on IAuthProvider
    }

    public async Task InitializeAsync()
    {
        // Pre-fetch or initialize resources if needed
        var result = await _fileManager.GetAllLocalUserProfilesAsync();
        if (!result.Success)
        {
            Debug.LogWarning($"UICallbackHandler: Failed to pre-fetch local user profiles: {result.Error?.Message}");
        }
    }

    public async Task<List<LocalUserProfileData>> HandleGuestLogin()
    {
        Debug.Log("UICallbackHandler: Handling guest login.");
        var guestProfile = new LocalUserProfileData
        {
            Id = "guest-" + Guid.NewGuid().ToString(),
            Name = "Guest",
            Email = "",
            CreatedAtUtc = DateTime.UtcNow,
            LastOnlineUtc = DateTime.UtcNow,
            IsOnline = true
        };

        var saveResult = await _fileManager.SaveLocalUserProfileAsync(guestProfile);
        if (!saveResult.Success)
        {
            Debug.LogError($"UICallbackHandler: Failed to save guest profile: {saveResult.Error?.Message}");
            return new List<LocalUserProfileData>();
        }

        // After saving, we can return the list containing just the guest profile
        var profilesResult = await _fileManager.GetAllLocalUserProfilesAsync();
        if(profilesResult.Success)
        {
            return profilesResult.Data;
        }
        else
        {
            Debug.LogWarning("UICallbackHandler: Could not fetch all profiles after guest login, returning just the new guest profile.");
            return new List<LocalUserProfileData> { guestProfile };
        }
    }
} 