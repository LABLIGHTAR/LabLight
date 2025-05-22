using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Lighthouse.MessagePack;

public class SessionManager : MonoBehaviour
{
    public static SessionManager instance;

    // Service Properties
    public IAuthProvider AuthProvider { get; private set; }
    public IDatabase Database { get; private set; }
    public IUIDriver UIDriver { get; private set; }
    public IFileManager FileManager { get; private set; }
    public ILLMChatProvider LLMChatProvider { get; private set; }

    private ARAnchorManager anchorManager;

    private static Transform workspaceTransform;

    [SerializeField] string MussBaseURL;

    // User and Session State
    private string _pendingDisplayName = null;
    private string _pendingEmail = null;
    private string _firebaseUserId = null;
    private string _spacetimeIdentity = null;
    public LocalUserProfileData CurrentLocalUserProfile { get; private set; }
    private CompositeDisposable _disposables = new CompositeDisposable();

    public Transform WorkspaceTransform
    {
        set
        {
            if(workspaceTransform != value)
            {
                workspaceTransform = value;
            }
        }
        get
        {
            return workspaceTransform;
        }
    }

    //for testing AM
    public List<TrackedObject> TrackedObjectsDebug = new List<TrackedObject>();

    [SerializeField]
    private static Transform charucoTransform;
    public Transform CharucoTransform
    {
        set
        {
            if(charucoTransform != value)
            {
                charucoTransform = value;
            }
        }
        get
        {
            return charucoTransform;
        }
    }

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
        }

        anchorManager = this.transform.parent.GetComponent<ARAnchorManager>();

        //planeViewManager = this.transform.GetComponent<ARPlaneViewController>();

        //anchorManager.enabled = false;

        var firbaseAuthProvider = new FirebaseAuthProvider();
        AuthProvider = firbaseAuthProvider;
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
        UIDriver.DisplayUserSelection();

        //Set up default state
        SessionState.deviceId = SystemInfo.deviceName;
        SessionState.currentUserProfile = null;

        //for debug to remove AM
        charucoTransform = transform;
        //workspaceTransform = transform;

        charucoTransform = Instantiate(new GameObject("CharucoTransform"), transform.parent.transform).transform;

        //Setup logger
        /* Add service 
         * debug
         * voice controller
         * audio manager
         * well plate csv provider
         * file upload handler
         */

        SubscribeToEvents();
    }

    void OnDestroy()
    {
        UnsubscribeFromEvents();
        _disposables.Dispose();
        if (instance == this) { instance = null; }
    }

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

    private void HandleAuthStatusChanged(AuthStatus status, string message)
    {
        Debug.Log($"SessionManager: AuthProvider status changed to {status}. Message: {message}");
        switch (status)
        {
            case AuthStatus.Idle:
                _firebaseUserId = null;
                _spacetimeIdentity = null;
                CurrentLocalUserProfile = null;
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
                _firebaseUserId = null;
                _spacetimeIdentity = null;
                CurrentLocalUserProfile = null;
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

    private void HandleDBStatusChanged(DBConnectionStatus status, string message)
    {
        Debug.Log($"SessionManager: Database status changed to {status}. Message: {message}");
        switch (status)
        {
            case DBConnectionStatus.Disconnected:
                _spacetimeIdentity = null;
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
                _spacetimeIdentity = null;
                // UIDriver?.ShowError($"Database Error: {message ?? "Unknown DB error"}");
                break;
        }
    }

    private void HandleAuthSignInSuccessToken(string oidcToken)
    {
        Debug.Log("SessionManager: Auth SignIn Success, received OIDC token.");
        _firebaseUserId = AuthProvider.CurrentUserId;
        if (string.IsNullOrEmpty(_firebaseUserId))
        {
            Debug.LogError("SessionManager: Firebase User ID is null/empty after sign in!");
            HandleAuthError("User ID not available after sign in.");
            return;
        }

        _spacetimeIdentity = null;
        CurrentLocalUserProfile = null;
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

    private async void HandleDatabaseConnectedIdentity(string spacetimeIdentity)
    {
        Debug.Log($"SessionManager: Database Connected with SpacetimeDB Identity: {spacetimeIdentity}");
        _spacetimeIdentity = spacetimeIdentity;

        if (!string.IsNullOrEmpty(_pendingDisplayName) && !string.IsNullOrEmpty(_pendingEmail) && !string.IsNullOrEmpty(_firebaseUserId))
        {
            Debug.Log($"SessionManager: DB Connected. Registering profile for new user: {_pendingDisplayName}, Email: {_pendingEmail}");
            Database.RegisterProfile(_pendingDisplayName);

            var newUserProfile = new LocalUserProfileData
            {
                Id = _firebaseUserId,
                Name = _pendingDisplayName,
                Email = _pendingEmail
            };

            ResultVoid saveResult = await FileManager.SaveLocalUserProfileAsync(newUserProfile);
            if (saveResult.Success)
            {
                CurrentLocalUserProfile = newUserProfile;
                SessionState.currentUserProfile = newUserProfile;
                Debug.Log($"SessionManager: New user profile for {_pendingDisplayName} registered and saved locally.");
                // UIDriver?.ShowUserView();
            }
            else
            {
                Debug.LogError($"SessionManager: Failed to save new user profile locally: {saveResult.Error?.Code} - {saveResult.Error?.Message}");
                // UIDriver?.ShowError("Failed to save user profile locally.");
            }
            _pendingDisplayName = null;
            _pendingEmail = null;
        }
        else if (!string.IsNullOrEmpty(_firebaseUserId))
        {
            Debug.Log($"SessionManager: DB Connected. Attempting to load local profile for user ID: {_firebaseUserId}");
            Result<LocalUserProfileData> loadResult = await FileManager.GetLocalUserProfileAsync(_firebaseUserId);
            if (loadResult.Success && loadResult.Data != null)
            {
                CurrentLocalUserProfile = loadResult.Data;
                SessionState.currentUserProfile = loadResult.Data;
                Debug.Log($"SessionManager: Successfully loaded local profile for {CurrentLocalUserProfile.Name}");
                // UIDriver?.ShowUserView();
            }
            else
            {
                Debug.LogWarning($"SessionManager: Could not load local profile for user ID {_firebaseUserId}. Error: {loadResult.Error?.Message}. This might be okay if profile is only in DB or first login on device.");
                UserData dbProfile = Database.GetCachedUserProfile(_spacetimeIdentity);
                if (dbProfile != null)
                {
                    Debug.Log($"SessionManager: Found profile in DB cache for {dbProfile.Name}. Creating/Saving local version.");
                    var localProfileFromDB = new LocalUserProfileData { Id = _firebaseUserId, Name = dbProfile.Name, Email = string.Empty };
                    await FileManager.SaveLocalUserProfileAsync(localProfileFromDB);
                    CurrentLocalUserProfile = localProfileFromDB;
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
        Debug.Log($"SessionManager: Database UserProfile Updated for ID: {dbProfile?.Id}, Name: {dbProfile?.Name}. SpacetimeID: {_spacetimeIdentity}");
        if (dbProfile != null && dbProfile.Id == _spacetimeIdentity && CurrentLocalUserProfile != null && CurrentLocalUserProfile.Id == _firebaseUserId)
        {
            bool changed = false;
            if (CurrentLocalUserProfile.Name != dbProfile.Name)
            {
                CurrentLocalUserProfile.Name = dbProfile.Name;
                changed = true;
            }

            if (changed)
            {
                Debug.Log($"SessionManager: Updating local profile due to DB changes for {CurrentLocalUserProfile.Name}.");
                FileManager.SaveLocalUserProfileAsync(CurrentLocalUserProfile)
                    .ToObservable()
                    .Subscribe(saveResult => {
                        if (!saveResult.Success)
                        {
                            Debug.LogError($"SessionManager: Failed to re-save local profile after DB update. Error: {saveResult.Error?.Message}");
                        }
                        else
                        {
                            SessionState.currentUserProfile = CurrentLocalUserProfile;
                        }
                    })
                    .AddTo(_disposables);
            }
        }
        else if (dbProfile != null && dbProfile.Id == _spacetimeIdentity && CurrentLocalUserProfile == null && !string.IsNullOrEmpty(_firebaseUserId))
        {
            Debug.Log($"SessionManager: DB profile update for {dbProfile.Name}, but no local profile yet. Creating one.");
            var newLocalProfile = new LocalUserProfileData { Id = _firebaseUserId, Name = dbProfile.Name, Email = string.Empty };
            FileManager.SaveLocalUserProfileAsync(newLocalProfile)
                .ToObservable()
                .Subscribe(saveResult => {
                    if (saveResult.Success)
                    {
                        CurrentLocalUserProfile = newLocalProfile;
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

    private void HandleAuthSignOutSuccess()
    {
        Debug.Log("SessionManager: Auth SignOut Success. State will be reset by AuthStatus.Idle.");
        _pendingDisplayName = null;
        _pendingEmail = null;
    }

    private void HandleAuthError(string errorMessage)
    {
        Debug.LogError($"SessionManager: Auth Error: {errorMessage}");
        _pendingDisplayName = null;
        _pendingEmail = null;
    }

    private void HandleDatabaseDisconnected(string? reason)
    {
        Debug.Log($"SessionManager: Database Disconnected. Reason: {reason ?? "None"}");
    }

    private void HandleDatabaseError(string errorMessage)
    {
        Debug.LogError($"SessionManager: Database Error: {errorMessage}");
    }

    // --- Public Methods for UI Interaction ---
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
            _pendingDisplayName = displayName;
            _pendingEmail = email;
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
            _pendingDisplayName = null;
            _pendingEmail = null;
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

    public void UpdateCalibration(Matrix4x4 pose)
    {
        if (CharucoTransform == null)
        {
            Debug.LogError("Missing CharucoTransform on SessionManager");
            return;
        }

        CharucoTransform.FromMatrix(pose);

        Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);
        CharucoTransform.rotation *= rotation;
        SessionState.onCalibrationUpdated.Invoke();
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

        // If a user is already signed in and DB connected, and we select a DIFFERENT user,
        // we should probably sign out the current user first to avoid inconsistent states.
        // However, if it's the SAME user, this might just be a re-selection, which is fine.
        if (AuthProvider.IsSignedIn && _firebaseUserId != userId)
        {
            Debug.LogWarning($"SessionManager.SelectLocalUserAsync: A different user ({_firebaseUserId}) is already signed in. Signing out first.");
            SignOut(); // This will trigger a state change to Idle, which might re-display user selection.
                       // This needs careful handling of UI flow. For now, let's proceed, but this could be an issue.
            // A better approach might be to prevent selection of a different user if one is fully signed in,
            // or ensure SignOut completes and waits before proceeding.
            // For now, we assume this selection happens when no one is signed in, or it's a re-selection.
        }

        Debug.Log($"SessionManager: Attempting to select and set active local user profile for ID: {userId}");
        Result<LocalUserProfileData> loadResult = await FileManager.GetLocalUserProfileAsync(userId);

        if (loadResult.Success && loadResult.Data != null)
        {
            CurrentLocalUserProfile = loadResult.Data;
            SessionState.currentUserProfile = loadResult.Data;
            _firebaseUserId = userId; // Set the selected user as the current Firebase user context
                                      // This assumes the userId IS the FirebaseID for local profiles.

            // Important: If this selection implies a full login attempt, we should trigger it here.
            // For example, if we have credentials or need to refresh tokens.
            // For now, this method just sets the local state.
            // If a full sign-in is needed, the UIDriver might call AuthProvider.SignIn after this, 
            // or this method could be expanded.
            // Let's assume for now that selecting from the list just makes them the *active context* locally.
            // Full authentication is a separate step if needed (e.g. by AttemptSignIn).

            Debug.Log($"SessionManager: Successfully selected local user profile: {CurrentLocalUserProfile.Name}. FirebaseID context set to {userId}.");
            return true;
        }
        else
        {
            Debug.LogError($"SessionManager: Failed to select/load local user profile for ID {userId}. Error: {loadResult.Error?.Code} - {loadResult.Error?.Message}");
            // Do not clear CurrentLocalUserProfile or _firebaseUserId if a user was already signed in and selection failed.
            // Only clear if this was an initial selection and it failed.
            if (!AuthProvider.IsSignedIn) {
                 CurrentLocalUserProfile = null; 
                 SessionState.currentUserProfile = null;
                 _firebaseUserId = null;
            }
            return false;
        }
    }
}
