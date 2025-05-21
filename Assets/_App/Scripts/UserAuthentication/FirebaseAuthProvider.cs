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
        SetAuthStatus(AuthStatus.Authenticating, "Initializing Firebase...");
        auth = FirebaseAuth.DefaultInstance;
        auth.StateChanged += AuthStateChanged;
        AuthStateChanged(this, null); // Perform initial check explicitly
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

    public void SignUp(string email, string password)
    {
        Debug.Log($"Attempting Firebase SignUp: {email}");
        SetAuthStatus(AuthStatus.Authenticating, "Signing up...");
        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWith(task => {
            if (task.IsCanceled) {
                HandleAuthOperationError("SignUp canceled.", "SignUp");
                return;
            }
            if (task.IsFaulted) {
                string errorMsg = task.Exception?.InnerExceptions?.FirstOrDefault()?.Message ?? task.Exception?.Message ?? "Unknown Sign Up Error";
                HandleAuthOperationError(errorMsg, "SignUp");
                return;
            }
            // Success: AuthStateChanged will handle the transition to SignedIn
            // and invocation of OnSignInSuccess after token retrieval.
            Debug.LogFormat("Firebase user creation initiated for: {0}", email);
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    public void SignIn(string email, string password)
    {
        Debug.Log($"Attempting Firebase SignIn: {email}");
        SetAuthStatus(AuthStatus.Authenticating, "Signing in...");

        var signInOperationTask = auth.SignInWithEmailAndPasswordAsync(email, password);
        var timeoutDelayTask = Task.Delay(TimeSpan.FromSeconds(15)); // 15-second timeout

        Task.WhenAny(signInOperationTask, timeoutDelayTask).ContinueWith(antecedent =>
        {
            if (antecedent.Result == timeoutDelayTask && !signInOperationTask.IsCompleted)
            {
                // Timeout occurred before the sign-in operation completed
                HandleAuthOperationError("Sign-in attempt timed out. Please check your internet connection and try again.", "SignIn-Timeout");
            }
            else if (signInOperationTask.IsFaulted)
            {
                string errorMsg = signInOperationTask.Exception?.InnerExceptions?.FirstOrDefault()?.Message ?? signInOperationTask.Exception?.Message ?? "Unknown Sign In Error";
                HandleAuthOperationError(errorMsg, "SignIn-Fault");
            }
            else if (signInOperationTask.IsCanceled)
            {
                HandleAuthOperationError("Sign-in attempt was canceled.", "SignIn-Canceled");
            }
            else if (signInOperationTask.IsCompletedSuccessfully) 
            {
                // Success: Firebase task completed. AuthStateChanged should handle the rest.
                // The AuthResult is in signInOperationTask.Result if needed here for user details, 
                // but AuthStateChanged is the primary handler for state transitions.
                Debug.LogFormat("Firebase sign-in task for {0} completed successfully. Waiting for AuthStateChanged to confirm.", email);
                // Note: No direct call to SetAuthStatus(AuthStatus.SignedIn) here. 
                // AuthStateChanged is responsible for that transition after token validation.
            }
            else
            {
                // Should not happen if WhenAny completed and signInOperationTask is the result,
                // but wasn't faulted, canceled, or successful.
                HandleAuthOperationError("Sign-in attempt ended in an unexpected state.", "SignIn-Unexpected");
            }

        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    public void SignOut()
    {
        Debug.Log("Attempting Firebase SignOut...");
        // Prevent multiple sign-out commands if already idle or in error from a failed sign-out
        if (CurrentAuthStatus == AuthStatus.Idle || (CurrentAuthStatus == AuthStatus.Error && user == null) )
        {
            Debug.LogWarning("SignOut called but user is already signed out or in a state indicating no active user.");
            SetAuthStatus(AuthStatus.Idle, "Already signed out."); // Ensure state is correct
            OnSignOutSuccess?.Invoke(); // Still good to invoke for listeners if they missed it
            return;
        }
        SetAuthStatus(AuthStatus.Authenticating, "Signing out...");
        auth.SignOut();
        // AuthStateChanged will handle the transition to Idle and invocation of OnSignOutSuccess.
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
