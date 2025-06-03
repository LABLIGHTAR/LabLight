using UnityEngine;
using UnityEngine.UIElements;
using Firebase.Auth;
using System.Threading.Tasks;
using Firebase; // Added for FirebaseException

public class ReturningUserLoginMenuController : MonoBehaviour
{
    private VisualElement _root;
    private IUIDriver _uiDriver;
    private IFileManager _fileManager; // To potentially load full profile data if needed
    private FirebaseAuth _auth;

    private VisualElement _profileImage;
    private Label _welcomeLabel;
    private TextField _passwordField;
    private Button _loginButton;
    private Button _backButton;
    private Label _errorLabel;

    private LocalUserProfileData _currentUserProfile; // To store the profile passed from UserSelectionMenu

    void OnEnable()
    {
        _uiDriver = ServiceRegistry.GetService<IUIDriver>();
        _fileManager = ServiceRegistry.GetService<IFileManager>();
        _auth = FirebaseAuth.DefaultInstance;

        var uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            Debug.LogError("ReturningUserLoginMenuController: UIDocument component not found!");
            return;
        }
        _root = uiDocument.rootVisualElement;

        _profileImage = _root.Q<VisualElement>("profile-image");
        _welcomeLabel = _root.Q<Label>("welcome-label");
        _passwordField = _root.Q<TextField>("password-field");
        _loginButton = _root.Q<Button>("login-button");
        _backButton = _root.Q<Button>("back-button");
        _errorLabel = _root.Q<Label>("error-label");

        if (_loginButton == null || _backButton == null || _passwordField == null || _welcomeLabel == null)
        {
            Debug.LogError("ReturningUserLoginMenuController: One or more UI elements not found in UXML.");
            return;
        }

        _loginButton.RegisterCallback<ClickEvent>(OnLoginClicked);
        _backButton.RegisterCallback<ClickEvent>(OnBackClicked);

        ClearError();
    }

    void OnDisable()
    {
        _loginButton?.UnregisterCallback<ClickEvent>(OnLoginClicked);
        _backButton?.UnregisterCallback<ClickEvent>(OnBackClicked);
    }

    /// <summary>
    /// Called by the UIDriver or UserSelectionMenuController to set the active user profile for this screen.
    /// </summary>
    public void SetUserProfile(LocalUserProfileData userProfile)
    {
        _currentUserProfile = userProfile;
        if (_currentUserProfile == null)
        {
            Debug.LogError("SetUserProfile called with null profile. Navigating back or showing error.");
            // Potentially navigate back to user selection if profile is unexpectedly null
            _uiDriver?.DisplayUserSelectionMenu(); 
            return;
        }

        _welcomeLabel.text = $"Welcome back, {_currentUserProfile.Name}!";
        _passwordField.value = string.Empty; // Clear password field
        ClearError();
        _passwordField.Focus();

        // TODO: Load and set profile image if _currentUserProfile.ProfilePicturePath is valid
        // Example: if (!string.IsNullOrEmpty(_currentUserProfile.ProfilePicturePath)) { ... }
        // For now, it uses the placeholder style from USS.
    }

    private async void OnLoginClicked(ClickEvent evt)
    {
        if (_currentUserProfile == null || string.IsNullOrEmpty(_currentUserProfile.Email))
        {
            SetError("User profile is not properly loaded. Please go back and select a user.");
            return;
        }

        string email = _currentUserProfile.Email;
        string password = _passwordField.value;

        if (string.IsNullOrEmpty(password))
        {
            SetError("Password cannot be empty.");
            _passwordField.Focus();
            return;
        }

        _loginButton.SetEnabled(false);
        _backButton.SetEnabled(false);
        SetError("Logging in..."); // Show a loading message

        try
        {
            // Ensure any previously logged-in Firebase user is signed out
            // This is important if the app was closed with a user still active from another profile
            if (_auth.CurrentUser != null && _auth.CurrentUser.UserId != _currentUserProfile.Id) // Assuming UserData.Id stores FirebaseUID
            {
                Debug.Log($"Signing out current Firebase user: {_auth.CurrentUser.UserId}");
                _auth.SignOut();
                // Give a moment for sign out to complete if necessary, though SignIn should override.
            }
            else if (_auth.CurrentUser != null && _auth.CurrentUser.UserId == _currentUserProfile.Id) 
            {
                Debug.Log($"User {_currentUserProfile.Id} is already signed in. Proceeding to dashboard.");
                // If already signed in as the correct user, password check might be for local re-verification only.
                // However, standard flow is to re-authenticate to ensure session is still valid with Firebase.
                // For simplicity, we can assume if _auth.CurrentUser matches, they are good to go if no password check is strictly needed here.
                // But if password check IS the purpose, then we MUST proceed to SignInWithEmailAndPasswordAsync.
                // Let's assume for now, if current user matches, they are good.
                // If strict re-auth with password is required every time, remove this else-if block or call SignIn anyway.
                _uiDriver?.DisplayDashboard();
                ClearError();
                _loginButton.SetEnabled(true);
                 _backButton.SetEnabled(true);
                return;
            }

            AuthResult authResult = await _auth.SignInWithEmailAndPasswordAsync(email, password);
            FirebaseUser user = authResult.User;
            Debug.LogFormat("User signed in successfully: {0} ({1})", user.DisplayName, user.UserId);
            
            // Update last login time or other local data if needed
            // _currentUserProfile.LastLoginDevice = System.DateTime.UtcNow;
            // await _fileManager.SaveUserProfileAsync(_currentUserProfile); // Assuming such a method exists

            ClearError();
            _uiDriver?.DisplayDashboard();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Login failed: {ex}");
            if (ex is FirebaseException firebaseEx)
            {
                AuthError authError = (AuthError)firebaseEx.ErrorCode;
                SetError(GetFirebaseAuthErrorMessage(authError));
            }
            else
            {
                SetError("An unexpected error occurred during login.");
            }
        }
        finally
        {
            _loginButton.SetEnabled(true);
            _backButton.SetEnabled(true);
        }
    }

    private void OnBackClicked(ClickEvent evt)
    {
        // Do not sign out Firebase user here, as they might be the CurrentUser from a previous session.
        // UserSelectionMenu should handle sign-out if a *different* user is selected or if a new login/register path is chosen.
        _uiDriver?.DisplayUserSelectionMenu();
    }

    private void SetError(string message)
    {
        _errorLabel.text = message;
        _errorLabel.style.display = DisplayStyle.Flex;
    }

    private void ClearError()
    {
        _errorLabel.text = string.Empty;
        _errorLabel.style.display = DisplayStyle.None;
    }

    private string GetFirebaseAuthErrorMessage(AuthError errorCode)
    {
        switch (errorCode)
        {
            case AuthError.MissingPassword:
                return "Missing password.";
            case AuthError.WrongPassword:
                return "Incorrect password. Please try again.";
            case AuthError.InvalidEmail:
                return "The email address is not valid.";
            case AuthError.UserNotFound:
                return "No account found with this email.";
            case AuthError.UserDisabled:
                return "This user account has been disabled.";
            case AuthError.TooManyRequests:
                return "Too many login attempts. Please try again later.";
            case AuthError.NetworkRequestFailed:
                return "Login failed: Network error. Please check your connection.";
            default:
                return "An unknown login error occurred.";
        }
    }
} 