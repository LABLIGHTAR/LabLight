using UnityEngine;
using UnityEngine.UIElements;
using System;

public class UserRegistrationMenuController : MonoBehaviour
{
    private IUIDriver _uiDriver; // Assuming you might need this for navigation or global actions

    private TextField _nameField;
    private TextField _emailField;
    private TextField _passwordField;
    private TextField _confirmPasswordField;
    private Button _registerButton;
    private Button _backButton;
    private Label _errorLabel; // For displaying validation errors

    void OnEnable()
    {
        _uiDriver = ServiceRegistry.GetService<IUIDriver>();

        var root = GetComponent<UIDocument>().rootVisualElement;
        if (root == null)
        {
            Debug.LogError("UserRegistrationMenuController: Root VisualElement not found.");
            return;
        }

        _nameField = root.Q<TextField>("name-field");
        _emailField = root.Q<TextField>("email-field");
        _passwordField = root.Q<TextField>("password-field");
        _confirmPasswordField = root.Q<TextField>("confirm-password-field");
        _registerButton = root.Q<Button>("register-button");
        _backButton = root.Q<Button>("back-button");

        // Optional: Add an error label to your UXML for feedback
        // <Label name="error-label" class="error-message-label" /> 
        _errorLabel = root.Q<Label>("error-label"); 
        if (_errorLabel != null) _errorLabel.style.display = DisplayStyle.None; // Hide initially

        _registerButton?.RegisterCallback<ClickEvent>(OnRegisterClicked);
        _backButton?.RegisterCallback<ClickEvent>(OnBackClicked);

        // Null checks for critical elements
        if (_nameField == null) Debug.LogError("Name field not found in UXML.");
        if (_emailField == null) Debug.LogError("Email field not found in UXML.");
        if (_passwordField == null) Debug.LogError("Password field not found in UXML.");
        if (_confirmPasswordField == null) Debug.LogError("Confirm Password field not found in UXML.");
        if (_registerButton == null) Debug.LogError("Register button not found in UXML.");
        if (_backButton == null) Debug.LogError("Back button not found in UXML.");
    }

    void OnDisable()
    {
        _registerButton?.UnregisterCallback<ClickEvent>(OnRegisterClicked);
        _backButton?.UnregisterCallback<ClickEvent>(OnBackClicked);
    }

    private void OnRegisterClicked(ClickEvent evt)
    {
        if (_errorLabel != null) _errorLabel.style.display = DisplayStyle.None; // Clear previous errors

        string name = _nameField.text;
        string email = _emailField.text;
        string password = _passwordField.text;
        string confirmPassword = _confirmPasswordField.text;

        // Basic Validation
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError("Name cannot be empty.");
            return;
        }
        if (string.IsNullOrWhiteSpace(email) || !email.Contains("@")) // Simple email check
        {
            ShowError("Please enter a valid email address.");
            return;
        }
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            ShowError("Password must be at least 6 characters long.");
            return;
        }
        if (password != confirmPassword)
        {
            ShowError("Passwords do not match.");
            return;
        }

        Debug.Log($"Registration attempt: Name: {name}, Email: {email}");
        _uiDriver?.AuthRegistrationCallback(name, email, password); // Directly call UIDriver
        
        // Consider clearing fields or showing a "processing" message
    }

    private void OnBackClicked(ClickEvent evt)
    {
        Debug.Log("Back button clicked. Navigating to user selection.");
        _uiDriver?.DisplayUserSelection(); // Directly call UIDriver
    }

    private void ShowError(string message)
    {
        Debug.LogWarning($"Validation Error: {message}");
        if (_errorLabel != null)
        {
            _errorLabel.text = message;
            _errorLabel.style.display = DisplayStyle.Flex;
        }
        // Optionally, provide visual feedback on the fields themselves
    }
    
    // Method to be called externally (e.g., by UIDriver) if registration fails server-side
    public void DisplayRegistrationError(string serverErrorMessage)
    {
        ShowError(serverErrorMessage);
    }

    public void ClearForm()
    {
        _nameField?.SetValueWithoutNotify("");
        _emailField?.SetValueWithoutNotify("");
        _passwordField?.SetValueWithoutNotify("");
        _confirmPasswordField?.SetValueWithoutNotify("");
        if (_errorLabel != null) _errorLabel.style.display = DisplayStyle.None;
    }
} 