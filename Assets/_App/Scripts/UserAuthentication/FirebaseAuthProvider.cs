using UnityEngine;
using Firebase.Auth;
using System; // Needed for Action
using System.Threading.Tasks; // Needed for Task
using System.Linq; // Added for FirstOrDefault

public class FirebaseAuthProvider : MonoBehaviour, IAuthProvider
{
    FirebaseAuth auth;
    FirebaseUser user;
    private bool _isInitialized = false; // Flag to track initial check

    // --- IAuthProvider Events ---
    public event Action<string> OnSignInSuccess;
    public event Action OnSignOutSuccess;
    public event Action<string> OnAuthError;
    public event Action<bool> OnInitialAuthChecked; // Implements interface event
    public event Action<AuthStatus, string> OnAuthStatusChanged;

    // --- IAuthProvider Properties ---
    private AuthStatus _currentAuthStatus = AuthStatus.Idle;
    public AuthStatus CurrentAuthStatus
    {
        get => _currentAuthStatus;
        private set
        {
            // Basic check to prevent redundant invocations if status is already set
            // More complex logic can be added if needed (e.g. state transition validation)
            if (_currentAuthStatus != value)
            {
                _currentAuthStatus = value;
                // Note: OnAuthStatusChanged is invoked by SetAuthStatus to ensure message is included
            }
        }
    }

    public bool IsSignedIn => CurrentAuthStatus == AuthStatus.SignedIn;
    public string CurrentUserId => user?.UserId ?? string.Empty;
    public string CurrentUserEmail => user?.Email ?? string.Empty;

    public async Task<string> GetIdTokenAsync(bool forceRefresh)
    {
        if (auth == null || auth.CurrentUser == null)
        {
            Debug.LogWarning("FirebaseAuthProvider.GetIdTokenAsync: Auth instance or CurrentUser is null. Cannot get token.");
            return null;
        }

        if (CurrentAuthStatus != AuthStatus.SignedIn)
        {
            Debug.LogWarning($"FirebaseAuthProvider.GetIdTokenAsync: User not in SignedIn state (Current: {CurrentAuthStatus}). Token retrieval might fail or return stale data if not refreshing.");
            // Proceeding as Firebase SDK might still allow token refresh for a valid session even if our internal state thinks otherwise briefly.
        }

        try
        {
            Debug.Log($"FirebaseAuthProvider.GetIdTokenAsync called with forceRefresh: {forceRefresh}");
            string idToken = await auth.CurrentUser.TokenAsync(forceRefresh);
            if (string.IsNullOrEmpty(idToken))
            {
                Debug.LogError("FirebaseAuthProvider.GetIdTokenAsync: TokenAsync returned null or empty token.");
                HandleAuthOperationError("Failed to retrieve a valid ID token.", "GetIdToken");
                return null;
            }
            Debug.Log("FirebaseAuthProvider.GetIdTokenAsync: Successfully retrieved ID token.");
            return idToken;
        }
        catch (Exception ex)
        {
            Debug.LogError($"FirebaseAuthProvider.GetIdTokenAsync: Exception while getting/refreshing ID token: {ex.Message}");
            HandleAuthOperationError($"Token retrieval/refresh exception: {ex.Message}", "GetIdToken");
            return null;
        }
    }

    void Start()
    {
        InitializeFirebase();
    }

    void InitializeFirebase()
    {
        Debug.Log("FirebaseAuthProvider: Initializing...");

        auth = FirebaseAuth.DefaultInstance;
        auth.StateChanged += AuthStateChanged; // Subscribe to state changes FIRST

        if (auth.CurrentUser != null)
        {
            Debug.Log($"FirebaseAuthProvider: Previous user ({auth.CurrentUser.UserId}) detected. Signing out to prevent auto-login.");
            // Set status to Authenticating to indicate activity before AuthStateChanged updates it.
            SetAuthStatus(AuthStatus.Authenticating, "Clearing previous session...");
            auth.SignOut();
            // AuthStateChanged will be triggered by SignOut().
            // It will see _isInitialized = false, currentFirebaseUser = null.
            // It will then set _isInitialized = true, user = null, AuthStatus = Idle, and invoke OnInitialAuthChecked(false).
        }
        else
        {
            Debug.Log("FirebaseAuthProvider: No previous user session found. Proceeding with initial state check.");
            // Set status to Authenticating to indicate activity before AuthStateChanged updates it.
            SetAuthStatus(AuthStatus.Authenticating, "Performing initial authentication check...");
            // Manually call AuthStateChanged to process the initial "no user" state.
            // This will also go through the !_isInitialized path, find currentFirebaseUser as null,
            // set state to Idle, and invoke OnInitialAuthChecked(false).
            AuthStateChanged(this, null);
        }
    }

    private void SetAuthStatus(AuthStatus newStatus, string message = null)
    {
        // Check if the status is genuinely changing or if it's an error being re-reported with a message
        if (CurrentAuthStatus != newStatus || (newStatus == AuthStatus.Error && !string.IsNullOrEmpty(message)))
        {
            string logMessage = $"AuthStatus changed from {CurrentAuthStatus} to {newStatus}.";
            if (!string.IsNullOrEmpty(message)) 
            {
                logMessage += $" Message: {message}";
            }
            CurrentAuthStatus = newStatus; // Set the internal state
            OnAuthStatusChanged?.Invoke(newStatus, message);
            Debug.Log($"FirebaseAuthProvider: {logMessage}");
        }
    }

    private void HandleAuthOperationError(string errorMessage, string operationType = "Operation")
    {
        Debug.LogError($"FirebaseAuthProvider: {operationType} Error: {errorMessage}");
        OnAuthError?.Invoke(errorMessage);
        SetAuthStatus(AuthStatus.Error, errorMessage);
    }

    public async Task SignUp(string email, string password)
    {
        Debug.Log($"Attempting Firebase SignUp: {email}");
        SetAuthStatus(AuthStatus.Authenticating, "Signing up...");
        try
        {
            AuthResult result = await auth.CreateUserWithEmailAndPasswordAsync(email, password);
            // Success: AuthStateChanged will handle the transition to SignedIn
            // and invocation of OnSignInSuccess after token retrieval.
            Debug.LogFormat("Firebase user creation initiated for: {0}, UserID: {1}", result.User.Email, result.User.UserId);
        }
        catch (Exception ex)
        {
            string errorMsg = ex.InnerException?.Message ?? ex.Message ?? "Unknown Sign Up Error";
            HandleAuthOperationError(errorMsg, "SignUp");
        }
    }

    public async Task SignIn(string email, string password)
    {
        Debug.Log($"Attempting Firebase SignIn: {email}");
        SetAuthStatus(AuthStatus.Authenticating, "Signing in...");

        try
        {
            // Create a CancellationTokenSource for timeout
            var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15)); // 15-second timeout

            AuthResult result = await auth.SignInWithEmailAndPasswordAsync(email, password).WithCancellation(cts.Token);
            
            // Success: Firebase task completed. AuthStateChanged should handle the rest.
            Debug.LogFormat("Firebase sign-in task for {0} completed successfully. UserID: {1}. Waiting for AuthStateChanged to confirm.", result.User.Email, result.User.UserId);
            // Note: No direct call to SetAuthStatus(AuthStatus.SignedIn) here. 
            // AuthStateChanged is responsible for that transition after token validation.
        }
        catch (OperationCanceledException)
        {
            HandleAuthOperationError("Sign-in attempt timed out. Please check your internet connection and try again.", "SignIn-Timeout");
        }
        catch (Exception ex)
        {
            string errorMsg = ex.InnerException?.Message ?? ex.Message ?? "Unknown Sign In Error";
            HandleAuthOperationError(errorMsg, "SignIn-Fault");
        }
    }

    public async Task SignOut()
    {
        Debug.Log("Attempting Firebase SignOut...");
        if (CurrentAuthStatus == AuthStatus.Idle || (CurrentAuthStatus == AuthStatus.Error && user == null))
        {
            Debug.LogWarning("SignOut called but user is already signed out or in a state indicating no active user.");
            SetAuthStatus(AuthStatus.Idle, "Already signed out.");
            OnSignOutSuccess?.Invoke();
            return; // Return Task.CompletedTask or nothing for async void if not truly async here
        }
        SetAuthStatus(AuthStatus.Authenticating, "Signing out...");
        try
        {
            auth.SignOut(); // This is synchronous in Firebase Unity SDK
            // AuthStateChanged will handle the transition to Idle and invocation of OnSignOutSuccess.
            // Since auth.SignOut() is synchronous, we might not need to await anything here
            // but changing to async Task for interface consistency.
            await Task.CompletedTask; // Explicitly return a completed task if SignOut itself isn't async
        }
        catch (Exception ex)
        {
            // This block might be less likely to be hit if auth.SignOut() itself doesn't throw often
            HandleAuthOperationError($"SignOut exception: {ex.Message}", "SignOut");
        }
    }

    // Handle Auth State Changes
    async void AuthStateChanged(object sender, EventArgs eventArgs)
    {
        FirebaseUser currentFirebaseUser = auth.CurrentUser;
        string previousUserId = user?.UserId;

        if (!_isInitialized)
        {
            _isInitialized = true;
            user = currentFirebaseUser;
            if (user != null)
            {
                Debug.Log($"Firebase Initial Check: User {user.UserId} was already signed in.");
                OnInitialAuthChecked?.Invoke(true);
                // Set Authenticating while getting token
                SetAuthStatus(AuthStatus.Authenticating, "Initial user found, verifying token..."); 
                try
                {
                    string idToken = await user.TokenAsync(true);
                    Debug.Log("Firebase ID Token obtained on initial check.");
                    SetAuthStatus(AuthStatus.SignedIn, "Initial sign-in successful.");
                    OnSignInSuccess?.Invoke(idToken);
                }
                catch (Exception ex)
                {
                    HandleAuthOperationError($"Initial session token retrieval failed: {ex.Message}", "InitialCheck");
                }
            }
            else
            {
                Debug.Log("Firebase Initial Check: No user signed in.");
                SetAuthStatus(AuthStatus.Idle, "No user initially signed in.");
                OnInitialAuthChecked?.Invoke(false);
            }
        }
        else // Subsequent changes
        {
            if (currentFirebaseUser?.UserId != previousUserId)
            {
                user = currentFirebaseUser; // Update internal user reference

                if (user != null) // User signed in (or changed)
                {
                    Debug.Log($"Firebase AuthStateChanged: User signed in or changed to {user.UserId}.");
                     // Set Authenticating while getting token
                    SetAuthStatus(AuthStatus.Authenticating, "User signed in, verifying token...");
                    try
                    {
                        string idToken = await user.TokenAsync(true); // Force refresh for SpacetimeDB
                        Debug.Log("Firebase ID Token obtained (Post-Init).");
                        SetAuthStatus(AuthStatus.SignedIn, "Sign-in successful.");
                        OnSignInSuccess?.Invoke(idToken); 
                    }
                    catch (Exception ex)
                    {
                        HandleAuthOperationError($"Failed to get ID Token post-init: {ex.Message}", "TokenRefresh");
                        // Consider if Firebase should be signed out if token is critical and fails
                        // auth.SignOut(); // This would re-trigger AuthStateChanged
                    }
                }
                else // User signed out
                {
                    Debug.Log("Firebase AuthStateChanged: User signed out.");
                    SetAuthStatus(AuthStatus.Idle, "Sign-out successful.");
                    OnSignOutSuccess?.Invoke();
                }
            }
            // If currentFirebaseUser?.UserId == previousUserId, it might be a token refresh.
            // No explicit status change needed unless token retrieval fails, which is handled above.
        }
    }

    void OnDestroy()
    {
        if (auth != null) {
            auth.StateChanged -= AuthStateChanged;
            auth = null;
        }
    }
}

// Helper extension for Task with CancellationToken
public static class TaskExtensions
{
    public static async Task<T> WithCancellation<T>(this Task<T> task, System.Threading.CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();
        using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
        {
            if (task != await Task.WhenAny(task, tcs.Task))
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }
        return await task; // Return the result of the original task
    }
}
