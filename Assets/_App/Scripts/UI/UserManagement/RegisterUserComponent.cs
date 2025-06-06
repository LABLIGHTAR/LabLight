using System;
using UnityEngine;
using UnityEngine.UIElements;

public class RegisterUserComponent : VisualElement
{
    public new class UxmlFactory : UxmlFactory<RegisterUserComponent, UxmlTraits> { }

    public event Action<string, string, string> OnRegisterSubmit;
    public event Action OnBack;

    private TextField _usernameField;
    private TextField _emailField;
    private TextField _passwordField;
    private Button _registerButton;
    private Button _backButton;
    private Label _errorLabel;
    
    public RegisterUserComponent() { }

    public RegisterUserComponent(VisualTreeAsset asset)
    {
        asset.CloneTree(this);

        _usernameField = this.Q<TextField>("username-field");
        _emailField = this.Q<TextField>("email-field");
        _passwordField = this.Q<TextField>("password-field");
        _registerButton = this.Q<Button>("register-button");
        _backButton = this.Q<Button>("back-button");
        _errorLabel = this.Q<Label>("error-label"); 

        if (_errorLabel != null) _errorLabel.style.display = DisplayStyle.None;

        _registerButton?.RegisterCallback<ClickEvent>(OnRegisterClicked);
        _backButton?.RegisterCallback<ClickEvent>(OnBackClicked);
    }
    
    private void OnRegisterClicked(ClickEvent evt)
    {
        if (_errorLabel != null) _errorLabel.style.display = DisplayStyle.None;

        string name = _usernameField.text;
        string email = _emailField.text;
        string password = _passwordField.text;
        
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError("Username cannot be empty.");
            return;
        }
        if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
        {
            ShowError("Please enter a valid email address.");
            return;
        }
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            ShowError("Password must be at least 6 characters long.");
            return;
        }

        OnRegisterSubmit?.Invoke(name, email, password);
    }
    
    private void OnBackClicked(ClickEvent evt)
    {
        OnBack?.Invoke();
    }
    
    public void ShowError(string message)
    {
        if (_errorLabel != null)
        {
            _errorLabel.text = message;
            _errorLabel.style.display = DisplayStyle.Flex;
        }
    }

    public void ClearForm()
    {
        _usernameField?.SetValueWithoutNotify("");
        _emailField?.SetValueWithoutNotify("");
        _passwordField?.SetValueWithoutNotify("");
        if (_errorLabel != null) _errorLabel.style.display = DisplayStyle.None;
    }

    public void SetInteractable(bool interactable)
    {
        _registerButton?.SetEnabled(interactable);
        _backButton?.SetEnabled(interactable);
    }
} 