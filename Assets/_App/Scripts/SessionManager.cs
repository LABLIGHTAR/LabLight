using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Lighthouse.MessagePack;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

public enum AuthProviderType
{
    Firebase,
    Unity
}

/// <summary>
/// The SessionManager is the app entry point.
/// Responsible for:
/// - registering services
/// - managing user and session state
/// - handling authentication events
/// - handling database events
/// - providing a public API for UI/external interaction
/// - handling calibration
/// </summary>
public class SessionManager : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Select the authentication provider to use. This is managed by the SessionManagerEditor script.")]
    [HideInInspector]
    public AuthProviderType SelectedAuthProvider;

    #region Singleton and Core Lifecycle
    public static SessionManager instance;

    public void Awake()
    {
        if(instance == null)
        {
            instance = this;
        }
        else
        {
            Debug.LogWarning("Multiple SessionManager instances detected. Destroying duplicate (newest).");
            DestroyImmediate(gameObject);
            return; // Return to prevent further initialization if this is a duplicate
        }

        // --- Auth Provider Setup ---
        // At runtime, we ensure only the selected provider exists by destroying any
        // existing providers and then adding the one specified by `SelectedAuthProvider`.
        foreach (var provider in GetComponents<IAuthProvider>())
        {
            if (provider is MonoBehaviour component)
            {
                Destroy(component);
            }
        }

        switch (SelectedAuthProvider)
        {
            case AuthProviderType.Firebase:
                AuthProvider = gameObject.AddComponent<FirebaseAuthProvider>();
                break;
            case AuthProviderType.Unity:
                AuthProvider = gameObject.AddComponent<UnityAuthProvider>();
                break;
            default:
                Debug.LogError($"SessionManager: Unhandled AuthProviderType '{SelectedAuthProvider}'. Auth provider will be null.", this);
                break;
        }
        
        if (AuthProvider != null)
        {
            Debug.Log($"SessionManager: {AuthProvider.GetType().Name} created and set as the active authentication provider.");
            ServiceRegistry.RegisterService<IAuthProvider>(AuthProvider);
        }
        // --- End Auth Provider Setup ---

        Database = GetComponent<SpacetimeDBImpl>();
        ServiceRegistry.RegisterService<IDatabase>(Database);

        var localStorageProvider = new LocalStorageProvider();
        var largeFileStorageProvider = new LargeFileStorageProvider(MussBaseURL, AuthProvider);
        FileManager = new FileManager(Database, localStorageProvider, largeFileStorageProvider);
        ServiceRegistry.RegisterService<IFileManager>(FileManager);

        LLMChatProvider = new ClaudeChatProvider();
        ServiceRegistry.RegisterService<ILLMChatProvider>(LLMChatProvider);

        var uiCallbackHandler = new UICallbackHandler();
        ServiceRegistry.RegisterService<IUICallbackHandler>(uiCallbackHandler);

        AudioService = GetComponent<AudioService>();
        ServiceRegistry.RegisterService<IAudioService>(AudioService);

        #if UNITY_VISIONOS && !UNITY_EDITOR
        UIDriver = new SwiftUIDriver();
        ServiceRegistry.RegisterService<IUIDriver>(UIDriver);
        UIDriver.Initialize();
        var unityDriverComponent = GetComponent<UnityUIDriver>();
        if (unityDriverComponent != null) Destroy(unityDriverComponent);
        #elif UNITY_EDITOR
        UIDriver = GetComponent<UnityUIDriver>();
        if (UIDriver == null) Debug.LogError("SessionManager: UnityUIDriver component not found in Editor mode!");
        ServiceRegistry.RegisterService<IUIDriver>(UIDriver);
        UIDriver?.Initialize();
        #endif
        
        if (UIDriver == null) Debug.LogError("SessionManager: UIDriver failed to obtain a reference or initialize!");
        else UIDriver.DisplayUserSelectionMenu();

        //Set up default state
        SessionState.deviceId = SystemInfo.deviceName;
        SessionState.currentUserProfile = null;

        SessionState.CharucoTransform = transform; 
        SessionState.WorkspaceTransform = transform; 
        SessionState.CharucoTransform = Instantiate(new GameObject("CharucoTransform")).transform;

        SubscribeToEvents();
    }

    void OnDestroy()
    {
        UnsubscribeFromEvents();
        _disposables.Dispose();
        if (instance == this) { instance = null; }
    }
    #endregion

    #region Service Properties
    private IAuthProvider AuthProvider { get; set; }
    private IDatabase Database { get; set; }
    private IUIDriver UIDriver { get; set; }
    private IFileManager FileManager { get; set; }
    private ILLMChatProvider LLMChatProvider { get; set; }
    private IAudioService AudioService { get; set; }
    #endregion

    #region Serialized Fields
    //Minimal URL Signing Service base URL, used for interaction with Large File Service (minIO)
    [SerializeField] string MussBaseURL;
    #endregion

    #region Debugging Fields
    //for testing AM
    public List<TrackedObject> TrackedObjectsDebug = new List<TrackedObject>();
    #endregion

    #region Public Session Events
    public event Action<LocalUserProfileData> OnSessionUserChanged;
    #endregion

    private CompositeDisposable _disposables = new CompositeDisposable();

    #region Event Management
    private void SubscribeToEvents()
    {
        if (AuthProvider != null)
        {
            AuthProvider.OnSignInSuccess += HandleAuthSignInSuccessToken;
            AuthProvider.OnSignOutSuccess += HandleAuthSignOutSuccess;
            AuthProvider.OnAuthError += HandleAuthError;
            AuthProvider.OnAuthStatusChanged += HandleAuthStatusChanged;
            Debug.Log("SessionManager subscribed to AuthProvider events.");
        }
        if (Database != null)
        {
            Database.OnConnected += HandleDatabaseConnectedIdentity;
            Database.OnDisconnected += HandleDatabaseDisconnected;
            Database.OnError += HandleDatabaseError;
            Database.OnUserProfileUpdated += HandleDatabaseUserProfileUpdated;
            Database.OnDBStatusChanged += HandleDBStatusChanged;
            Debug.Log("SessionManager subscribed to Database events.");
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (AuthProvider != null)
        {
            AuthProvider.OnSignInSuccess -= HandleAuthSignInSuccessToken;
            AuthProvider.OnSignOutSuccess -= HandleAuthSignOutSuccess;
            AuthProvider.OnAuthError -= HandleAuthError;
            AuthProvider.OnAuthStatusChanged -= HandleAuthStatusChanged;
        }
        if (Database != null)
        { 
            Database.OnConnected -= HandleDatabaseConnectedIdentity;
            Database.OnDisconnected -= HandleDatabaseDisconnected;
            Database.OnError -= HandleDatabaseError;
            Database.OnUserProfileUpdated -= HandleDatabaseUserProfileUpdated;
            Database.OnDBStatusChanged -= HandleDBStatusChanged;
        }
        Debug.Log("SessionManager unsubscribed from events.");
    }
    #endregion

    #region Authentication Event Handlers
    private void HandleAuthStatusChanged(AuthStatus status, string message)
    {
        Debug.Log($"SessionManager: AuthProvider status changed to {status}. Message: {message}");
        switch (status)
        {
            case AuthStatus.Idle:
                SessionState.FirebaseUserId = null;
                SessionState.SpacetimeIdentity = null;
                SessionState.currentUserProfile = null;
                if (Database != null && Database.CurrentDBStatus != DBConnectionStatus.Disconnected)
                {
                    Database.Disconnect();
                }
                UIDriver?.DisplayUserSelectionMenu();
                break;
            case AuthStatus.Authenticating:
                // UIDriver?.ShowLoading("Authenticating...");
                break;
            case AuthStatus.SignedIn:
                // UIDriver?.ShowLoading("Connecting to Services...");
                break;
            case AuthStatus.Error:
                SessionState.FirebaseUserId = null;
                SessionState.SpacetimeIdentity = null;
                SessionState.currentUserProfile = null;
                if (Database != null && Database.CurrentDBStatus != DBConnectionStatus.Disconnected)
                {
                    Database.Disconnect();
                }
                // UIDriver?.ShowError($"Authentication Failed: {message ?? "Unknown auth error"}");
                // UIDriver?.DisplayUserSelection();
                break;
        }
    }

    private async Task CreateAndSetLocalProfileAsync(string firebaseUserId, string name, string email, DateTime? dbCreatedAtUtc = null, bool isNewUserRegistration = false)
    {
        Debug.Log($"SessionManager: Creating/updating local profile for FirebaseID: {firebaseUserId}, Name: {name}, Email: {email}");
        var localProfile = new LocalUserProfileData
        {
            Id = firebaseUserId,
            Name = name,
            Email = email,
            CreatedAtUtc = isNewUserRegistration ? DateTime.UtcNow : (dbCreatedAtUtc ?? DateTime.UtcNow),
            LastOnlineUtc = DateTime.UtcNow, 
            IsOnline = true
        };

        ResultVoid saveResult = await FileManager.SaveLocalUserProfileAsync(localProfile);
        if (saveResult.Success)
        {
            SessionState.currentUserProfile = localProfile;
            Debug.Log($"SessionManager: Successfully saved and set local profile for {localProfile.Name} (ID: {localProfile.Id}).");
        }
        else
        {
            Debug.LogError($"SessionManager: Failed to save local profile for {name} (FirebaseID: {firebaseUserId}): {saveResult.Error?.Code} - {saveResult.Error?.Message}");
        }
    }

    private async void HandleAuthSignInSuccessToken(string oidcToken)
    {
        Debug.Log("SessionManager: Auth SignIn Success, received OIDC token.");
        SessionState.FirebaseUserId = AuthProvider.CurrentUserId;
        if (string.IsNullOrEmpty(SessionState.FirebaseUserId))
        {
            Debug.LogError("SessionManager: Firebase User ID is null/empty after sign in!");
            HandleAuthError("User ID not available after sign in.");
            return;
        }

        // If this sign-in is due to a new registration, create the local profile now.
        if (!string.IsNullOrEmpty(SessionState.PendingDisplayName) && !string.IsNullOrEmpty(SessionState.PendingEmail))
        {
            Debug.Log($"SessionManager: New user registration detected for {SessionState.PendingDisplayName}. Creating local profile via helper.");
            await CreateAndSetLocalProfileAsync(SessionState.FirebaseUserId, SessionState.PendingDisplayName, SessionState.PendingEmail, isNewUserRegistration: true);
            // PendingDisplayName and PendingEmail will be cleared by HandleDatabaseConnectedIdentity after DB registration.
        }

        SessionState.SpacetimeIdentity = null; // Will be set by HandleDatabaseConnectedIdentity
        // SessionState.currentUserProfile might have been set above if new user, or will be loaded/set by HandleDatabaseConnectedIdentity for existing users

        if (Database != null)
        {
            Debug.Log("SessionManager: Connecting to Database with token...");
            Database.Connect(oidcToken);
        }
        else
        {
            Debug.LogError("SessionManager: Database service is null, cannot connect after sign-in.");
            // UIDriver?.ShowError("Internal error: Database service unavailable.");
        }
    }

    private void HandleAuthSignOutSuccess()
    {
        Debug.Log("SessionManager: Auth SignOut Success. State will be reset by AuthStatus.Idle.");
        SessionState.PendingDisplayName = null;
        SessionState.PendingEmail = null;
    }

    private void HandleAuthError(string errorMessage)
    {
        Debug.LogError($"SessionManager: Auth Error: {errorMessage}");
        SessionState.PendingDisplayName = null;
        SessionState.PendingEmail = null;
    }
    #endregion

    #region Database Event Handlers
    private void HandleDBStatusChanged(DBConnectionStatus status, string message)
    {
        Debug.Log($"SessionManager: Database status changed to {status}. Message: {message}");
        switch (status)
        {
            case DBConnectionStatus.Disconnected:
                SessionState.SpacetimeIdentity = null;
                if (AuthProvider != null && AuthProvider.IsSignedIn)
                {
                     // UIDriver?.ShowError($"Lost Database Connection: {message ?? "Disconnected"}. Please try signing out and in, or check connectivity.");
                }
                break;
            case DBConnectionStatus.Connecting:
                // UIDriver?.ShowLoading("Connecting to Database...");
                break;
            case DBConnectionStatus.Connected:
                // UIDriver?.ShowLoading("Loading Profile..."); 
                break;
            case DBConnectionStatus.Error:
                SessionState.SpacetimeIdentity = null;
                // UIDriver?.ShowError($"Database Error: {message ?? "Unknown DB error"}");
                break;
        }
    }

    private async void HandleDatabaseConnectedIdentity(string spacetimeIdentity)
    {
        Debug.Log($"SessionManager: Database Connected with SpacetimeDB Identity: {spacetimeIdentity}");
        SessionState.SpacetimeIdentity = spacetimeIdentity;

        if (!string.IsNullOrEmpty(SessionState.PendingDisplayName) && !string.IsNullOrEmpty(SessionState.PendingEmail) && !string.IsNullOrEmpty(SessionState.FirebaseUserId))
        {
            Debug.Log($"SessionManager: DB Connected. Registering profile for new user: {SessionState.PendingDisplayName}, Email: {SessionState.PendingEmail}");
            Database.RegisterProfile(SessionState.PendingDisplayName);

            // Local profile should have been created in HandleAuthSignInSuccessToken.
            // We just clear the pending state here.
            SessionState.PendingDisplayName = null;
            SessionState.PendingEmail = null;
            
            // Invoke OnSessionUserChanged if profile is available
            if (SessionState.currentUserProfile != null) // Should have been set by CreateAndSetLocalProfileAsync
            {
                Debug.Log($"SessionManager: New user registration - Invoking OnSessionUserChanged for {SessionState.currentUserProfile.Name}");
                OnSessionUserChanged?.Invoke(SessionState.currentUserProfile);
            }
            UIDriver?.DisplayDashboard(); 
        }
        else if (!string.IsNullOrEmpty(SessionState.FirebaseUserId))
        {
            Debug.Log($"SessionManager: DB Connected. Attempting to load local profile for user ID: {SessionState.FirebaseUserId}");
            Result<LocalUserProfileData> loadResult = await FileManager.GetLocalUserProfileAsync(SessionState.FirebaseUserId);
            if (loadResult.Success && loadResult.Data != null)
            {
                SessionState.currentUserProfile = loadResult.Data;
                Debug.Log($"SessionManager: Successfully loaded local profile for {SessionState.currentUserProfile.Name}");
                Debug.Log($"SessionManager: Returning user - Invoking OnSessionUserChanged for {SessionState.currentUserProfile.Name}");
                OnSessionUserChanged?.Invoke(SessionState.currentUserProfile);
                UIDriver?.DisplayDashboard();
            }
            else
            {
                Debug.LogWarning($"SessionManager: Could not load local profile for user ID {SessionState.FirebaseUserId}. Error: {loadResult.Error?.Message}. This might be okay if profile is only in DB or first login on device.");
                UserData dbProfile = Database.GetCachedUserProfile(SessionState.SpacetimeIdentity);
                if (dbProfile != null)
                {
                    Debug.Log($"SessionManager: Found profile in DB cache for {dbProfile.Name}. Creating/Saving local version via helper.");
                    
                    string userEmail = AuthProvider?.CurrentUserEmail ?? string.Empty;
                    await CreateAndSetLocalProfileAsync(SessionState.FirebaseUserId, dbProfile.Name, userEmail, dbProfile.CreatedAtUtc, isNewUserRegistration: false);
                    // After CreateAndSetLocalProfileAsync, SessionState.currentUserProfile should be set.
                    if (SessionState.currentUserProfile != null)
                    {
                        Debug.Log($"SessionManager: Profile created from DB cache - Invoking OnSessionUserChanged for {SessionState.currentUserProfile.Name}");
                        OnSessionUserChanged?.Invoke(SessionState.currentUserProfile);
                    }
                    UIDriver?.DisplayDashboard();
                }
                else
                {
                     Debug.LogError("SessionManager: No local profile and no DB cached profile found after login.");
                }
            }
        }
        else
        {
             Debug.LogError("SessionManager: HandleDatabaseConnectedIdentity called but FirebaseUserID is missing.");
        }
    }

    private async void HandleDatabaseUserProfileUpdated(UserData dbProfileFromEvent)
    {
        // Ensure we have the necessary info from the event and session
        if (dbProfileFromEvent == null || string.IsNullOrEmpty(dbProfileFromEvent.Id) || string.IsNullOrEmpty(SessionState.FirebaseUserId))
        {
            Debug.LogWarning($"SessionManager: HandleDatabaseUserProfileUpdated called with insufficient data. DB Profile ID: {dbProfileFromEvent?.Id}, FirebaseUID: {SessionState.FirebaseUserId}");
            return;
        }

        Debug.Log($"SessionManager: Database UserProfile Event for SpacetimeIdentity: {dbProfileFromEvent.Id}, Name: {dbProfileFromEvent.Name}. Current FirebaseUser: {SessionState.FirebaseUserId}");

        // This event's dbProfileFromEvent.Id IS the SpacetimeDB Identity.
        // We check if this SpacetimeDB Identity corresponds to our currently logged-in Firebase user
        // by comparing it with SessionState.SpacetimeIdentity, which should have been set on connection.
        if (dbProfileFromEvent.Id != SessionState.SpacetimeIdentity)
        {
            Debug.Log($"SessionManager: Ignoring UserProfile update for SpacetimeIdentity {dbProfileFromEvent.Id} as it doesn't match current session's SpacetimeIdentity {SessionState.SpacetimeIdentity}.");
            return;
        }

        // At this point, the dbProfileFromEvent IS for the currently connected SpacetimeDB identity.
        // Now, let's work with the local profile.

        if (SessionState.currentUserProfile != null)
        {
            // Ensure the loaded local profile is for the current Firebase user.
            if (SessionState.currentUserProfile.Id == SessionState.FirebaseUserId)
            {
                bool changed = false;
                if (SessionState.currentUserProfile.Name != dbProfileFromEvent.Name)
                {
                    SessionState.currentUserProfile.Name = dbProfileFromEvent.Name;
                    changed = true;
                }
                // TODO: Potentially update other fields like IsOnline, LastOnlineUtc from dbProfileFromEvent
                // Ensure to convert SpacetimeDB.Timestamp to DateTime for CreatedAtUtc/LastOnlineUtc if UserData has them
                // For example:
                // if (dbProfileFromEvent.IsOnline != SessionState.currentUserProfile.IsOnline) { SessionState.currentUserProfile.IsOnline = dbProfileFromEvent.IsOnline; changed = true; }
                // DateTime dbLastOnline = ConvertSpacetimeTimestampToDateTime(dbProfileFromEvent.LastOnline); // Assuming conversion function
                // if (dbLastOnline != SessionState.currentUserProfile.LastOnlineUtc) { SessionState.currentUserProfile.LastOnlineUtc = dbLastOnline; changed = true; }


                if (changed)
                {
                    Debug.Log($"SessionManager: Updating existing local profile for {SessionState.currentUserProfile.Name} (FirebaseUID: {SessionState.FirebaseUserId}) due to DB changes.");
                    ResultVoid saveResult = await FileManager.SaveLocalUserProfileAsync(SessionState.currentUserProfile);
                    if (!saveResult.Success)
                    {
                        Debug.LogError($"SessionManager: Failed to re-save local profile after DB update. Error: {saveResult.Error?.Message}");
                    }
                }
                else
                {
                    Debug.Log($"SessionManager: No changes detected in DB profile for local user {SessionState.currentUserProfile.Name}.");
                }
            }
            else
            {
                Debug.LogError($"SessionManager: Mismatch! SessionState.currentUserProfile.Id ({SessionState.currentUserProfile.Id}) != SessionState.FirebaseUserId ({SessionState.FirebaseUserId}). This should not happen if profile loaded correctly.");
                // This state is problematic. Might need to reload or clear SessionState.currentUserProfile.
            }
        }
        else // SessionState.currentUserProfile is null, but we know the FirebaseUserId and SpacetimeIdentity.
        {
            Debug.Log($"SessionManager: Local profile (SessionState.currentUserProfile) is null for FirebaseUser {SessionState.FirebaseUserId}. DB Profile Event for {dbProfileFromEvent.Name}. Attempting to create/load local profile.");

            // Try to load it one more time, in case of race condition or if HandleDatabaseConnectedIdentity hasn't fully completed
            Result<LocalUserProfileData> loadResult = await FileManager.GetLocalUserProfileAsync(SessionState.FirebaseUserId);
            if (loadResult.Success && loadResult.Data != null)
            {
                SessionState.currentUserProfile = loadResult.Data;
                Debug.Log($"SessionManager: Successfully re-loaded local profile for {SessionState.currentUserProfile.Name}. Will compare and update if needed.");
                 if (SessionState.currentUserProfile.Name != dbProfileFromEvent.Name) // Or other fields
                 {
                    SessionState.currentUserProfile.Name = dbProfileFromEvent.Name;
                    // Update other fields as needed
                    await FileManager.SaveLocalUserProfileAsync(SessionState.currentUserProfile); 
                 }
            }
            else
            {
                // If still null after re-load attempt, create it using FirebaseUserId and data from this dbProfileFromEvent
                Debug.Log($"SessionManager: Still no local profile for {SessionState.FirebaseUserId}. Creating from DB event data ({dbProfileFromEvent.Name}).");
                string userEmail = AuthProvider?.CurrentUserEmail;
                if (string.IsNullOrEmpty(userEmail))
                {
                    Debug.LogError($"SessionManager: Cannot create local profile from DB update because AuthProvider.CurrentUserEmail is empty for FirebaseUID {SessionState.FirebaseUserId}. Profile creation aborted.");
                    return; // Critical data missing
                }

                var newLocalProfile = new LocalUserProfileData
                {
                    Id = SessionState.FirebaseUserId, // Firebase UID
                    Name = dbProfileFromEvent.Name,
                    Email = userEmail, // Email from AuthProvider
                    // Initialize other fields if UserData/LocalUserProfileData supports them and dbProfileFromEvent provides them
                    // e.g., IsOnline = dbProfileFromEvent.IsOnline (assuming UserData has IsOnline)
                    // CreatedAtUtc = DateTime.UtcNow, // Or map from dbProfileFromEvent.CreatedAt if available and convertible
                    // LastOnlineUtc = DateTime.UtcNow // Or map from dbProfileFromEvent.LastOnline
                };
                
                // Set sensible defaults if not mapped
                if (newLocalProfile.CreatedAtUtc == default(DateTime)) newLocalProfile.CreatedAtUtc = DateTime.UtcNow;
                if (newLocalProfile.LastOnlineUtc == default(DateTime)) newLocalProfile.LastOnlineUtc = DateTime.UtcNow;


                ResultVoid saveResult = await FileManager.SaveLocalUserProfileAsync(newLocalProfile);
                if (saveResult.Success)
                {
                    SessionState.currentUserProfile = newLocalProfile;
                    Debug.Log($"SessionManager: Successfully created and saved new local profile from DB update for {newLocalProfile.Name}.");
                }
                else
                {
                    Debug.LogError($"SessionManager: Failed to save new local profile from DB update. Error: {saveResult.Error?.Message}");
                }
            }
        }
    }

    private void HandleDatabaseDisconnected(string? reason)
    {
        Debug.Log($"SessionManager: Database Disconnected. Reason: {reason ?? "None"}");
    }

    private void HandleDatabaseError(string errorMessage)
    {
        Debug.LogError($"SessionManager: Database Error: {errorMessage}");
    }
    #endregion

    #region Public API for UI/External Interaction
    public void AttemptSignUp(string email, string password, string displayName)
    {
        if (AuthProvider == null) {
            Debug.LogError("SessionManager: AuthProvider is null. Cannot sign up.");
            // UIDriver?.ShowError("Authentication service not available.");
            return;
        }

        if (AuthProvider.CurrentAuthStatus == AuthStatus.Idle || AuthProvider.CurrentAuthStatus == AuthStatus.Error)
        {
            if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                // UIDriver?.ShowError("Display Name, Email, and Password cannot be empty.");
                return;
            }
            SessionState.PendingDisplayName = displayName;
            SessionState.PendingEmail = email;
            AuthProvider.SignUp(email, password);
        }
        else
        {
            Debug.LogWarning($"SessionManager: Cannot SignUp. Current Auth Status: {AuthProvider.CurrentAuthStatus}");
            // UIDriver?.ShowError($"Cannot start sign up, process already in progress ({AuthProvider.CurrentAuthStatus}).");
        }
    }

    public void AttemptSignIn(string email, string password)
    {
        if (AuthProvider == null) {
            Debug.LogError("SessionManager: AuthProvider is null. Cannot sign in.");
            // UIDriver?.ShowError("Authentication service not available.");
            return;
        }
        if (AuthProvider.CurrentAuthStatus == AuthStatus.Idle || AuthProvider.CurrentAuthStatus == AuthStatus.Error)
        {
            SessionState.PendingDisplayName = null;
            SessionState.PendingEmail = null;
            AuthProvider.SignIn(email, password);
        }
        else
        {
            Debug.LogWarning($"SessionManager: Cannot SignIn. Current Auth Status: {AuthProvider.CurrentAuthStatus}");
            // UIDriver?.ShowError($"Cannot start sign in, process already in progress ({AuthProvider.CurrentAuthStatus}).");
        }
    }

    public void SignOut()
    {
         if (AuthProvider == null) {
            Debug.LogError("SessionManager: AuthProvider is null. Cannot sign out.");
            return;
        }
        if (AuthProvider.CurrentAuthStatus == AuthStatus.SignedIn || AuthProvider.CurrentAuthStatus == AuthStatus.Error)
        {
            AuthProvider.SignOut();
        }
        else
        {
            Debug.LogWarning($"SessionManager: Cannot SignOut. Current Auth Status: {AuthProvider.CurrentAuthStatus}");
        }
    }

    public async System.Threading.Tasks.Task<bool> SelectLocalUserAsync(string userId)
    {
        if (AuthProvider == null || FileManager == null)
        {
            Debug.LogError("SessionManager.SelectLocalUserAsync: AuthProvider or FileManager is null.");
            return false;
        }

        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogError("SessionManager.SelectLocalUserAsync: userId cannot be null or empty.");
            return false;
        }

        if (AuthProvider.IsSignedIn && SessionState.FirebaseUserId != userId)
        {
            Debug.LogWarning($"SessionManager.SelectLocalUserAsync: A different user ({SessionState.FirebaseUserId}) is already signed in. Signing out first.");
            SignOut(); 
        }

        Debug.Log($"SessionManager: Attempting to select and set active local user profile for ID: {userId}");
        Result<LocalUserProfileData> loadResult = await FileManager.GetLocalUserProfileAsync(userId);

        if (loadResult.Success && loadResult.Data != null)
        {
            SessionState.currentUserProfile = loadResult.Data;
            SessionState.FirebaseUserId = userId; 
            Debug.Log($"SessionManager: Successfully selected local user profile: {SessionState.currentUserProfile.Name}. FirebaseID context set to {userId}.");
            return true;
        }
        else
        {
            Debug.LogError($"SessionManager: Failed to select/load local user profile for ID {userId}. Error: {loadResult.Error?.Code} - {loadResult.Error?.Message}");
            if (!AuthProvider.IsSignedIn) {
                 SessionState.currentUserProfile = null; 
                 SessionState.FirebaseUserId = null;
            }
            return false;
        }
    }
    #endregion

    #region Calibration Methods
    public void UpdateCalibration(Matrix4x4 pose)
    {
        if (SessionState.CharucoTransform == null) 
        {
            Debug.LogError("Missing CharucoTransform on SessionManager (accessed via SessionState)");
            return;
        }

        SessionState.CharucoTransform.FromMatrix(pose); 
        Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);
        SessionState.CharucoTransform.rotation *= rotation; 
        SessionState.onCalibrationUpdated.Invoke();
    }
    #endregion
}
