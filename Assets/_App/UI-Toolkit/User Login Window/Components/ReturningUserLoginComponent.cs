using System;
using UnityEngine;
using UnityEngine.UIElements;

public class ReturningUserLoginComponent : VisualElement
{
    public new class UxmlFactory : UxmlFactory<ReturningUserLoginComponent, UxmlTraits> { }
    
    public event Action<string, string> OnLoginSubmit;
    public event Action OnSwitchUser;

    private VisualElement _profileImage;
    private Label _welcomeLabel;
    private TextField _passwordField;
    private Button _loginButton;
    private Button _backButton; // "Switch User"
    private Label _errorLabel;
    private IAudioService _audioService;
    
    private LocalUserProfileData _currentUserProfile;

    public ReturningUserLoginComponent() { }

    public ReturningUserLoginComponent(VisualTreeAsset asset)
    {
        AddToClassList("view-container");
        asset.CloneTree(this);
        
        _audioService = ServiceRegistry.GetService<IAudioService>();
        
        _profileImage = this.Q<VisualElement>("profile-image");
        _welcomeLabel = this.Q<Label>("welcome-label");
        _passwordField = this.Q<TextField>("password-field");
        _loginButton = this.Q<Button>("login-button");
        _backButton = this.Q<Button>("back-button");
        _errorLabel = this.Q<Label>("error-label");

        _loginButton?.RegisterCallback<ClickEvent>(OnLoginClicked);
        _backButton?.RegisterCallback<ClickEvent>(OnSwitchUserClicked);
        
        ClearError();
    }
    
    public void SetUserProfile(LocalUserProfileData userProfile)
    {
        _currentUserProfile = userProfile;
        if (_currentUserProfile == null)
        {
            // This should be handled by the controller, maybe by switching back to selection view
            return;
        }

        _welcomeLabel.text = $"Welcome back, {_currentUserProfile.Name}!";
        _passwordField.value = string.Empty;
        ClearError();
        _passwordField.Focus();
    }
    
    private void OnLoginClicked(ClickEvent evt)
    {
        _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center);
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

        OnLoginSubmit?.Invoke(email, password);
    }

    private void OnSwitchUserClicked(ClickEvent evt)
    {
        _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center);
        OnSwitchUser?.Invoke();
    }

    public void SetError(string message)
    {
        if (_errorLabel == null) return;
        _errorLabel.text = message;
        _errorLabel.style.display = DisplayStyle.Flex;
    }

    public void UpdateStatus(string message)
    {
        SetError(message);
    }

    public void ClearError()
    {
        if (_errorLabel == null) return;
        _errorLabel.text = string.Empty;
        _errorLabel.style.display = DisplayStyle.None;
    }

    public void SetInteractable(bool interactable)
    {
        _loginButton?.SetEnabled(interactable);
        _backButton?.SetEnabled(interactable);
    }
} 