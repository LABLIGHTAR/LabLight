using System;
using UnityEngine;
using UnityEngine.UIElements;

public class ExistingUserLoginComponent : VisualElement
{
    public new class UxmlFactory : UxmlFactory<ExistingUserLoginComponent, UxmlTraits> { }

    public event Action<string, string> OnLoginSubmit;
    public event Action OnBack;

    private TextField _emailField;
    private TextField _passwordField;
    private Button _loginButton;
    private Button _backButton;
    private Label _errorLabel;

    public ExistingUserLoginComponent() { }
    
    public ExistingUserLoginComponent(VisualTreeAsset asset)
    {
        AddToClassList("view-container");
        asset.CloneTree(this);

        _emailField = this.Q<TextField>("email-input");
        _passwordField = this.Q<TextField>("password-field");
        _loginButton = this.Q<Button>("login-button");
        _backButton = this.Q<Button>("back-button");
        _errorLabel = this.Q<Label>("error-label");

        if (_errorLabel != null) _errorLabel.style.display = DisplayStyle.None;

        _loginButton?.RegisterCallback<ClickEvent>(OnLoginClicked);
        _backButton?.RegisterCallback<ClickEvent>(OnBackClicked);
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
        
        OnLoginSubmit?.Invoke(email, password);
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
        _emailField?.SetValueWithoutNotify("");
        _passwordField?.SetValueWithoutNotify("");
        if (_errorLabel != null) _errorLabel.style.display = DisplayStyle.None;
    }

    public void SetInteractable(bool interactable)
    {
        _loginButton?.SetEnabled(interactable);
        _backButton?.SetEnabled(interactable);
    }
} 