using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class UserSelectionComponent : VisualElement
{
    public new class UxmlFactory : UxmlFactory<UserSelectionComponent, UxmlTraits> { }

    public event Action OnLoginClicked;
    public event Action OnRegisterClicked;
    public event Action<LocalUserProfileData> OnUserSelected;

    private ScrollView _userScrollView;
    private Button _loginButton;
    private Button _registerButton;
    private Label _errorLabel;
    private IAudioService _audioService;
    
    public VisualTreeAsset userItemTemplate;

    public UserSelectionComponent()
    {
        // The controller will pass in the VisualTreeAsset
    }
    
    public UserSelectionComponent(VisualTreeAsset asset)
    {
        AddToClassList("view-container");
        asset.CloneTree(this);
        
        _audioService = ServiceRegistry.GetService<IAudioService>();
        
        _userScrollView = this.Q<ScrollView>("user-scroll-view");
        _loginButton = this.Q<Button>("login-button");
        _registerButton = this.Q<Button>("register-button");
        _errorLabel = this.Q<Label>("error-label");

        if (_errorLabel != null) _errorLabel.style.display = DisplayStyle.None;
        
        _loginButton?.RegisterCallback<ClickEvent>(evt => { _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center); OnLoginClicked?.Invoke(); });
        _registerButton?.RegisterCallback<ClickEvent>(evt => { _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center); OnRegisterClicked?.Invoke(); });
    }

    public void ShowError(string message)
    {
        if (_errorLabel != null)
        {
            _errorLabel.text = message;
            _errorLabel.style.display = DisplayStyle.Flex;
        }
    }

    public void PopulateUserList(List<LocalUserProfileData> users)
    {
        if (_userScrollView == null) return;
        _userScrollView.Clear();

        if (users == null || !users.Any())
        {
            _userScrollView.style.display = DisplayStyle.None;
            return;
        }

        _userScrollView.style.display = DisplayStyle.Flex;
        
        foreach (var user in users)
        {
            VisualElement userItem;
            if (userItemTemplate != null)
            {
                userItem = userItemTemplate.Instantiate();
                userItem.userData = user; 
                userItem.Q<Label>("user-name-label").text = user.Name;
                userItem.AddToClassList("user-item"); 
            }
            else
            {
                userItem = new Button(() => OnUserSelected?.Invoke(user)) { text = user.Name };
                userItem.AddToClassList("user-item-button-fallback");
                userItem.userData = user;
            }

            userItem.RegisterCallback<ClickEvent>(evt =>
            {
                if (userItem.userData is LocalUserProfileData selectedUser)
                {
                    _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center);
                    OnUserSelected?.Invoke(selectedUser);
                }
            });
            _userScrollView.Add(userItem);
        }
    }
} 