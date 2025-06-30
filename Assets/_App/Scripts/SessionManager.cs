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

        var uiCallbackHandler = new UICallbackHandler(FileManager, AuthProvider, Database);
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

        // If this sign-in is due to a new registration, the PendingDisplayName and PendingEmail will be set.
        // We no longer create a partial profile here. We wait until we have the SpacetimeDB identity.
        
        SessionState.SpacetimeIdentity = null; // Will be set by HandleDatabaseConnectedIdentity

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
        SessionState.currentUserProfile = null;
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
        UIDriver?.UpdateConnectionStatus(status, message);
    }

    private async void HandleDatabaseConnectedIdentity(string spacetimeIdentity)
    {
        Debug.Log($"SessionManager: Database connected with Identity: {spacetimeIdentity}. UserID: {AuthProvider.CurrentUserId}");
        SessionState.SpacetimeIdentity = spacetimeIdentity;

        // Handle Guest Login
        if (string.IsNullOrEmpty(AuthProvider.CurrentUserId))
        {
            Debug.Log("SessionManager: Guest user detected. Creating temporary session profile.");
            SessionState.currentUserProfile = new LocalUserProfileData
            {
                Id = spacetimeIdentity, // Use SpacetimeDB identity for session uniqueness
                Name = "Guest",
                Email = "",
                IsOnline = true,
                CreatedAtUtc = DateTime.UtcNow,
                LastOnlineUtc = DateTime.UtcNow
            };
            UIDriver.DisplayDashboard(); // Navigate to the dashboard for guests
            return;
        }

        // Handle Registered User Login
        // We are connected. The HandleDatabaseUserProfileUpdated event will fire for any existing user.
        // If the user is brand new, we proceed with registration logic.
        // This relies on the new user registration flow setting PendingDisplayName.
        if (!string.IsNullOrEmpty(SessionState.PendingDisplayName))
        {
            Debug.Log($"SessionManager: New user registration flow confirmed for {SessionState.PendingDisplayName}. Registering profile in DB.");

            // Then, trigger the database to create its version of the profile.
            Database.RegisterProfile(SessionState.PendingDisplayName);

            // The HandleDatabaseUserProfileUpdated event will fire when the DB confirms creation.
            // That event is now responsible for creating the complete local profile.
        }
        else
        {
            Debug.Log($"SessionManager: Returning user {AuthProvider.CurrentUserId} connected. Waiting for profile data from database event.");
            // For a returning user, we do nothing here. We wait for the HandleDatabaseUserProfileUpdated
            // event to provide the profile data from the database. This avoids race conditions.
        }
    }

    private async void HandleDatabaseUserProfileUpdated(UserData dbProfile)
    {
        if (dbProfile == null || string.IsNullOrEmpty(dbProfile.SpacetimeId))
        {
            Debug.LogWarning("SessionManager: HandleDatabaseUserProfileUpdated received null or invalid profile data (missing SpacetimeId).");
            return;
        }
        
        // This event now fires for ANY user profile update.
        // We only care about the one that matches our connected identity.
        if (dbProfile.SpacetimeId != Database.CurrentIdentity)
        {
            return; // Not our profile, ignore.
        }

        Debug.Log($"SessionManager: Received OUR user profile update from DB for SpacetimeID: {dbProfile.SpacetimeId}, Name: {dbProfile.Name}");

        string firebaseId = AuthProvider.CurrentUserId;
        if (string.IsNullOrEmpty(firebaseId))
        {
            Debug.LogError("SessionManager: Cannot create/update profile because Firebase User ID is missing from AuthProvider.");
            return;
        }

        // If this is a new registration, create the full profile. Otherwise, update.
        if (!string.IsNullOrEmpty(SessionState.PendingDisplayName) && !string.IsNullOrEmpty(SessionState.PendingEmail))
        {
            // NEW USER REGISTRATION
            Debug.Log($"Finalizing registration for {SessionState.PendingDisplayName}.");
            var newFullProfile = new LocalUserProfileData
            {
                Id = firebaseId,
                SpacetimeId = dbProfile.SpacetimeId,
                Name = dbProfile.Name, // Use name from DB as source of truth
                Email = SessionState.PendingEmail, // Use email from registration form
                IsOnline = dbProfile.IsOnline,
                CreatedAtUtc = dbProfile.CreatedAtUtc,
                LastOnlineUtc = dbProfile.LastOnlineUtc,
            };

            // Save the newly constructed full profile
            await FileManager.CacheUserProfileAsync(newFullProfile);
            Debug.Log($"SessionManager: Successfully created and cached full profile for {newFullProfile.Name}.");

            // Update session state
            SetCurrentUser(newFullProfile);

            // Clear pending registration state
            SessionState.PendingDisplayName = null;
            SessionState.PendingEmail = null;
            
            UIDriver?.DisplayDashboard();
        }
        else
        {
            // RETURNING USER UPDATE
            var localProfileResult = await FileManager.GetUserProfileAsync(firebaseId);
            string userEmail = localProfileResult.Success ? localProfileResult.Data.Email : AuthProvider.CurrentUserEmail;

            var updatedFullProfile = new LocalUserProfileData
            {
                Id = firebaseId,
                SpacetimeId = dbProfile.SpacetimeId,
                Name = dbProfile.Name,
                Email = userEmail,
                IsOnline = dbProfile.IsOnline,
                CreatedAtUtc = dbProfile.CreatedAtUtc, 
                LastOnlineUtc = dbProfile.LastOnlineUtc,
            };

            await FileManager.CacheUserProfileAsync(updatedFullProfile);
            Debug.Log($"SessionManager: Successfully updated and cached profile for returning user {updatedFullProfile.Name}.");

            if (SessionState.currentUserProfile == null || SessionState.currentUserProfile.Id == updatedFullProfile.Id)
            {
                SetCurrentUser(updatedFullProfile);
                UIDriver?.DisplayDashboard(); 
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
        if (AuthProvider != null && AuthProvider.IsSignedIn)
        {
            // For a registered user, trigger the full sign-out flow.
            // The HandleAuthStatusChanged event will handle UI navigation and state cleanup.
            Debug.Log("SessionManager: Signing out registered user.");
            AuthProvider.SignOut();
        }
        else
        {
            // For a guest or if not signed in, just disconnect and reset state locally.
            Debug.Log("SessionManager: Signing out guest user.");
            Database?.Disconnect();
            SessionState.currentUserProfile = null;
            SessionState.SpacetimeIdentity = null;
            UIDriver?.DisplayUserSelectionMenu();
        }
    }

    public async System.Threading.Tasks.Task<bool> SelectLocalUserAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            // This is the case where the user cancels the selection
            SessionState.currentUserProfile = null;
            UIDriver?.DisplayUserSelectionMenu();
            return false;
        }

        Debug.Log($"SessionManager: Selecting local user with ID: {userId}");
        var profileResult = await FileManager.GetUserProfileAsync(userId);

        if (profileResult.Success && profileResult.Data != null)
        {
            SessionState.currentUserProfile = profileResult.Data;
            OnSessionUserChanged?.Invoke(profileResult.Data);
            Debug.Log($"SessionManager: Successfully set current user to: {profileResult.Data.Name}");
            // Based on your app flow, you might want to show the dashboard or a returning user login screen.
            // If the user just has a local profile but isn't authenticated via Firebase, you'd show a login screen.
            // For simplicity here, we assume selecting a user means you go to the dashboard.
            UIDriver?.DisplayDashboard(); 
            return true;
        }
        else
        {
            Debug.LogError($"SessionManager: Failed to get local user profile for ID {userId}. Error: {profileResult.Error?.Message}");
            SessionState.currentUserProfile = null;
            UIDriver?.DisplayUserSelectionMenu(); // Go back to selection on error
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

    private void SetCurrentUser(LocalUserProfileData profile)
    {
        SessionState.currentUserProfile = profile;
        Debug.Log($"SessionManager: Set current session user to {profile?.Name}.");
        OnSessionUserChanged?.Invoke(profile);
    }

    private void OnApplicationQuit()
    {
        SignOut();
    }

    private async void HandleLoginRequestFromUI(string email, string password)
    {
        Debug.Log($"SessionManager: Received login request for {email}.");
        await AuthProvider.SignIn(email, password);
        // The rest of the flow is handled by AuthStateChanged events.
    }

    private async void HandleRegistrationRequestFromUI(string email, string password, string displayName)
    {
        Debug.Log($"SessionManager: Received registration request for {email}.");
        // Step 1: Set pending display name, so we can use it after the user is created.
        SessionState.PendingDisplayName = displayName;
        SessionState.PendingEmail = email;

        // Step 2: Call SignUp, which will trigger the AuthStateChanged flow.
        await AuthProvider.SignUp(email, password);
        // The rest of the flow (DB registration etc.) is handled by AuthStateChanged events.
    }
}
