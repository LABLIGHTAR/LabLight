using System; // Needed for Action

public enum AuthStatus
{
    Idle, // Initial state, after explicit sign-out, or before initialization
    Authenticating,
    SignedIn,
    // SignedOut, // Covered by Idle after OnSignOutSuccess, or Error if sign-out failed.
    Error
}

public interface IAuthProvider
{
    // Events
    /// <summary>
    /// Invoked when sign-in is successful. Passes the OIDC ID token.
    /// </summary>
    event Action<string> OnSignInSuccess;
    /// <summary>
    /// Invoked when sign-out is successful.
    /// </summary>
    event Action OnSignOutSuccess;
    /// <summary>
    /// Invoked when an authentication error occurs. Passes an error message.
    /// </summary>
    event Action<string> OnAuthError;
    /// <summary>
    /// Invoked after the initial check of the authentication state when the provider initializes.
    /// Passes true if a user was already signed in, false otherwise.
    /// </summary>
    event Action<bool> OnInitialAuthChecked;

    // State Management
    /// <summary>
    /// Gets the current authentication status.
    /// </summary>
    AuthStatus CurrentAuthStatus { get; }
    /// <summary>
    /// Invoked when the authentication status changes.
    /// Passes the new status and an optional message (e.g., error details).
    /// </summary>
    event Action<AuthStatus, string> OnAuthStatusChanged;

    // Methods (Keep existing)
    /// <summary>
    /// Initiates the sign-up process with the given email and password.
    /// Results are communicated via events (OnSignInSuccess, OnAuthError, OnAuthStatusChanged).
    /// </summary>
    System.Threading.Tasks.Task SignUp(string email, string password);
    /// <summary>
    /// Initiates the sign-in process with the given email and password.
    /// Results are communicated via events (OnSignInSuccess, OnAuthError, OnAuthStatusChanged).
    /// </summary>
    System.Threading.Tasks.Task SignIn(string email, string password);
    /// <summary>
    /// Initiates the sign-out process.
    /// Results are communicated via events (OnSignOutSuccess, OnAuthError, OnAuthStatusChanged).
    /// </summary>
    System.Threading.Tasks.Task SignOut();

    // Optional: Property to check current state easily
    /// <summary>
    /// Gets a value indicating whether the user is currently signed in.
    /// </summary>
    bool IsSignedIn { get; }
    /// <summary>
    /// Gets the current user's unique ID from the authentication provider (e.g., Firebase User ID).
    /// Returns string.Empty if no user is signed in.
    /// </summary>
    string CurrentUserId { get; } // Firebase User ID
    /// <summary>
    /// Gets the current user's email from the authentication provider.
    /// </summary>
    string CurrentUserEmail { get; }

    /// <summary>
    /// Asynchronously retrieves the current user's OIDC ID token.
    /// </summary>
    /// <param name="forceRefresh">If true, the token will be refreshed even if a cached token is available and not expired. If false, a cached token may be returned if valid.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the OIDC ID token, or null if retrieval fails or no user is signed in.</returns>
    System.Threading.Tasks.Task<string> GetIdTokenAsync(bool forceRefresh);
}
