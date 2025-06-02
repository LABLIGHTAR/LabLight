using UnityEngine;
using UnityEngine.UIElements;
using System;

public class UserLoginMenuController : MonoBehaviour
{
    private IUIDriver _uiDriver;

    private TextField _emailField;
    private TextField _passwordField;
    private Button _loginButton;
    private Button _backButton;
    private Label _errorLabel;

    void OnEnable()
    {
        _uiDriver = ServiceRegistry.GetService<IUIDriver>();

        var root = GetComponent<UIDocument>().rootVisualElement;
        if (root == null)
        {
            Debug.LogError("UserLoginMenuController: Root VisualElement not found.");
            return;
        }

        _emailField = root.Q<TextField>("email-field");
        _passwordField = root.Q<TextField>("password-field");
        _loginButton = root.Q<Button>("login-button");
        _backButton = root.Q<Button>("back-button");
        _errorLabel = root.Q<Label>("error-label");

        if (_errorLabel != null) _errorLabel.style.display = DisplayStyle.None; // Hide initially

        _loginButton?.RegisterCallback<ClickEvent>(OnLoginClicked);
        _backButton?.RegisterCallback<ClickEvent>(OnBackClicked);

        if (_emailField == null) Debug.LogError("Email field not found in UXML.");
        if (_passwordField == null) Debug.LogError("Password field not found in UXML.");
        if (_loginButton == null) Debug.LogError("Login button not found in UXML.");
        if (_backButton == null) Debug.LogError("Back button not found in UXML.");
    }

    void OnDisable()
    {
        _loginButton?.UnregisterCallback<ClickEvent>(OnLoginClicked);
        _backButton?.UnregisterCallback<ClickEvent>(OnBackClicked);
    }

    private void OnLoginClicked(ClickEvent evt)
    {
        if (_errorLabel != null) _errorLabel.style.display = DisplayStyle.None;

        string email = _emailField.text;
        string password = _passwordField.text;

        if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
        {
            ShowError("Please enter a valid email address.");
            return;
        }
        if (string.IsNullOrWhiteSpace(password))
        {
            ShowError("Password cannot be empty.");
            return;
        }

        Debug.Log($"Login attempt: Email: {email}");
        _uiDriver?.LoginCallback(email, password); // Directly call UIDriver
    }

    private void OnBackClicked(ClickEvent evt)
    {
        Debug.Log("Back button clicked. Navigating to user selection.");
        _uiDriver?.DisplayUserSelection(); // Assumes DisplayUserSelection exists and is the correct back target
    }

    private void ShowError(string message)
    {
        Debug.LogWarning($"Validation Error: {message}");
        if (_errorLabel != null)
        {
            _errorLabel.text = message;
            _errorLabel.style.display = DisplayStyle.Flex;
        }
    }

    public void DisplayLoginError(string serverErrorMessage)
    {
        ShowError(serverErrorMessage);
    }

    public void ClearForm()
    {
        _emailField?.SetValueWithoutNotify("");
        _passwordField?.SetValueWithoutNotify("");
        if (_errorLabel != null) _errorLabel.style.display = DisplayStyle.None;
    }
} 