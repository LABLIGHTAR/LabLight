using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Lighthouse.MessagePack;

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

        // Initialize services
        AuthProvider = GetComponent<FirebaseAuthProvider>();
        ServiceRegistry.RegisterService<IAuthProvider>(AuthProvider);

        Database = GetComponent<SpacetimeDBImpl>();
        ServiceRegistry.RegisterService<IDatabase>(Database);

        var localStorageProvider = new LocalStorageProvider();
        var largeFileStorageProvider = new LargeFileStorageProvider(MussBaseURL, AuthProvider);
        FileManager = new FileManager(Database, localStorageProvider, largeFileStorageProvider);
        ServiceRegistry.RegisterService<IFileManager>(FileManager);

        LLMChatProvider = new ClaudeChatProvider();
        ServiceRegistry.RegisterService<ILLMChatProvider>(LLMChatProvider);

        #if UNITY_VISIONOS && !UNITY_EDITOR
        UIDriver = new SwiftUIDriver();
        ServiceRegistry.RegisterService<IUIDriver>(UIDriver);
        Destroy(GetComponent<UnityUIDriver>());
        #elif UNITY_EDITOR
        UIDriver = GetComponent<UnityUIDriver>();
        ServiceRegistry.RegisterService<IUIDriver>(UIDriver);
        #endif
        
        if (UIDriver == null) Debug.LogError("SessionManager: UIDriver failed to initialize!");
        else UIDriver.DisplayUserSelection();

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
    #endregion

    #region Serialized Fields
    //Minimal URL Signing Service base URL, used for interaction with Large File Service (minIO)
    [SerializeField] string MussBaseURL;
    #endregion

    #region Debugging Fields
    //for testing AM
    public List<TrackedObject> TrackedObjectsDebug = new List<TrackedObject>();
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
                // UIDriver?.DisplayUserSelection(); 
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

    private void HandleAuthSignInSuccessToken(string oidcToken)
    {
        Debug.Log("SessionManager: Auth SignIn Success, received OIDC token.");
        SessionState.FirebaseUserId = AuthProvider.CurrentUserId;
        if (string.IsNullOrEmpty(SessionState.FirebaseUserId))
        {
            Debug.LogError("SessionManager: Firebase User ID is null/empty after sign in!");
            HandleAuthError("User ID not available after sign in.");
            return;
        }

        SessionState.SpacetimeIdentity = null;
        SessionState.currentUserProfile = null;

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

            var newUserProfile = new LocalUserProfileData
            {
                Id = SessionState.FirebaseUserId,
                Name = SessionState.PendingDisplayName,
                Email = SessionState.PendingEmail
            };

            ResultVoid saveResult = await FileManager.SaveLocalUserProfileAsync(newUserProfile);
            if (saveResult.Success)
            {
                SessionState.currentUserProfile = newUserProfile;
                Debug.Log($"SessionManager: New user profile for {SessionState.PendingDisplayName} registered and saved locally.");
                // UIDriver?.ShowUserView();
            }
            else
            {
                Debug.LogError($"SessionManager: Failed to save new user profile locally: {saveResult.Error?.Code} - {saveResult.Error?.Message}");
                // UIDriver?.ShowError("Failed to save user profile locally.");
            }
            SessionState.PendingDisplayName = null;
            SessionState.PendingEmail = null;
        }
        else if (!string.IsNullOrEmpty(SessionState.FirebaseUserId))
        {
            Debug.Log($"SessionManager: DB Connected. Attempting to load local profile for user ID: {SessionState.FirebaseUserId}");
            Result<LocalUserProfileData> loadResult = await FileManager.GetLocalUserProfileAsync(SessionState.FirebaseUserId);
            if (loadResult.Success && loadResult.Data != null)
            {
                SessionState.currentUserProfile = loadResult.Data;
                Debug.Log($"SessionManager: Successfully loaded local profile for {SessionState.currentUserProfile.Name}");
                // UIDriver?.ShowUserView();
            }
            else
            {
                Debug.LogWarning($"SessionManager: Could not load local profile for user ID {SessionState.FirebaseUserId}. Error: {loadResult.Error?.Message}. This might be okay if profile is only in DB or first login on device.");
                UserData dbProfile = Database.GetCachedUserProfile(SessionState.SpacetimeIdentity);
                if (dbProfile != null)
                {
                    Debug.Log($"SessionManager: Found profile in DB cache for {dbProfile.Name}. Creating/Saving local version.");
                    var localProfileFromDB = new LocalUserProfileData { Id = SessionState.FirebaseUserId, Name = dbProfile.Name, Email = string.Empty };
                    await FileManager.SaveLocalUserProfileAsync(localProfileFromDB);
                    SessionState.currentUserProfile = localProfileFromDB;
                    // UIDriver?.ShowUserView();
                }
                else
                {
                     Debug.LogError("SessionManager: No local profile and no DB cached profile found after login.");
                     // UIDriver?.ShowError("Failed to load user profile.");
                }
            }
        }
        else
        {
             Debug.LogError("SessionManager: HandleDatabaseConnectedIdentity called but FirebaseUserID is missing.");
        }
    }

    private void HandleDatabaseUserProfileUpdated(UserData dbProfile)
    {
        Debug.Log($"SessionManager: Database UserProfile Updated for ID: {dbProfile?.Id}, Name: {dbProfile?.Name}. SpacetimeID: {SessionState.SpacetimeIdentity}");
        if (dbProfile != null && dbProfile.Id == SessionState.SpacetimeIdentity && SessionState.currentUserProfile != null && SessionState.currentUserProfile.Id == SessionState.FirebaseUserId)
        {
            bool changed = false;
            if (SessionState.currentUserProfile.Name != dbProfile.Name)
            {
                SessionState.currentUserProfile.Name = dbProfile.Name;
                changed = true;
            }

            if (changed)
            {
                Debug.Log($"SessionManager: Updating local profile due to DB changes for {SessionState.currentUserProfile.Name}.");
                FileManager.SaveLocalUserProfileAsync(SessionState.currentUserProfile)
                    .ToObservable()
                    .Subscribe(saveResult => {
                        if (!saveResult.Success)
                        {
                            Debug.LogError($"SessionManager: Failed to re-save local profile after DB update. Error: {saveResult.Error?.Message}");
                        }
                    })
                    .AddTo(_disposables);
            }
        }
        else if (dbProfile != null && dbProfile.Id == SessionState.SpacetimeIdentity && SessionState.currentUserProfile == null && !string.IsNullOrEmpty(SessionState.FirebaseUserId))
        {
            Debug.Log($"SessionManager: DB profile update for {dbProfile.Name}, but no local profile yet. Creating one.");
            var newLocalProfile = new LocalUserProfileData { Id = SessionState.FirebaseUserId, Name = dbProfile.Name, Email = string.Empty };
            FileManager.SaveLocalUserProfileAsync(newLocalProfile)
                .ToObservable()
                .Subscribe(saveResult => {
                    if (saveResult.Success)
                    {
                        SessionState.currentUserProfile = newLocalProfile;
                        Debug.Log("SessionManager: Created and saved local profile from DB update.");
                    }
                    else
                    {
                         Debug.LogError($"SessionManager: Failed to save new local profile from DB update. Error: {saveResult.Error?.Message}");
                    }
                })
                .AddTo(_disposables);
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
