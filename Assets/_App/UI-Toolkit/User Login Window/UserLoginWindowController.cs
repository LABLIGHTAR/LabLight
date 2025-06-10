using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class UserLoginWindowController : BaseWindowController
{
    [Header("UXML Assets")]
    public VisualTreeAsset userSelectionComponentAsset;
    public VisualTreeAsset existingUserLoginComponentAsset;
    public VisualTreeAsset registerUserComponentAsset;
    public VisualTreeAsset returningUserLoginComponentAsset;
    public VisualTreeAsset userItemTemplate;

    private VisualElement _container;

    private UserSelectionComponent _userSelectionComponent;
    private ExistingUserLoginComponent _existingUserLoginComponent;
    private RegisterUserComponent _registerUserComponent;
    private ReturningUserLoginComponent _returningUserLoginComponent;

    private IFileManager _fileManager;
    private IUIDriver _uiDriver;

    protected override void OnEnable()
    {
        base.OnEnable();
        _fileManager = ServiceRegistry.GetService<IFileManager>();
        _uiDriver = ServiceRegistry.GetService<IUIDriver>();
        
        _container = rootVisualElement.Q<VisualElement>("content-container");

        if (_container == null)
        {
            Debug.LogError("UserLoginWindowController: content-container not found in UXML.");
            return;
        }

        ShowUserSelectionComponent();
    }

    private void ShowUserSelectionComponent()
    {
        _userSelectionComponent = new UserSelectionComponent(userSelectionComponentAsset)
        {
            userItemTemplate = this.userItemTemplate
        };
        _userSelectionComponent.OnLoginClicked += ShowExistingUserLoginComponent;
        _userSelectionComponent.OnRegisterClicked += ShowRegisterUserComponent;
        _userSelectionComponent.OnUserSelected += ShowReturningUserLoginComponent;
        SwapComponent(_container, _userSelectionComponent);
        LoadAndDisplayUserProfiles();
    }
    
    private void ShowExistingUserLoginComponent()
    {
        _existingUserLoginComponent = new ExistingUserLoginComponent(existingUserLoginComponentAsset);
        _existingUserLoginComponent.OnLoginSubmit += OnLoginSubmit;
        _existingUserLoginComponent.OnBack += ShowUserSelectionComponent;
        SwapComponent(_container, _existingUserLoginComponent);
    }
    
    private void ShowRegisterUserComponent()
    {
        _registerUserComponent = new RegisterUserComponent(registerUserComponentAsset);
        _registerUserComponent.OnRegisterSubmit += OnRegisterSubmit;
        _registerUserComponent.OnBack += ShowUserSelectionComponent;
        SwapComponent(_container, _registerUserComponent);
    }

    private void ShowReturningUserLoginComponent(LocalUserProfileData userProfile)
    {
        _returningUserLoginComponent = new ReturningUserLoginComponent(returningUserLoginComponentAsset);
        _returningUserLoginComponent.SetUserProfile(userProfile);
        _returningUserLoginComponent.OnLoginSubmit += OnLoginSubmit;
        _returningUserLoginComponent.OnSwitchUser += ShowUserSelectionComponent;
        SwapComponent(_container, _returningUserLoginComponent);
    }
    
    private async void LoadAndDisplayUserProfiles()
    {
        if (_fileManager == null || _userSelectionComponent == null) return;

        var result = await _fileManager.GetLocalUserProfilesAsync();
        if (result.Success && result.Data != null)
        {
            _userSelectionComponent.PopulateUserList(result.Data);
        }
        else
        {
            Debug.LogError($"Failed to load user profiles: {result.Error?.Message}");
            _userSelectionComponent.PopulateUserList(new List<LocalUserProfileData>());
        }
    }
    
    private void OnLoginSubmit(string email, string password)
    {
        Debug.Log($"Login attempt: Email: {email}");
        _uiDriver?.LoginCallback(email, password);
    }

    private void OnRegisterSubmit(string name, string email, string password)
    {
        Debug.Log($"Registration attempt: Name: {name}, Email: {email}");
        _uiDriver?.AuthRegistrationCallback(name, email, password);
    }
    
    public void UpdateUserProfiles(List<LocalUserProfileData> profiles)
    {
        if (_userSelectionComponent != null && _container.Contains(_userSelectionComponent))
        {
            _userSelectionComponent.PopulateUserList(profiles);
        }
    }

    public void DisplayLoginError(string message)
    {
        if (_existingUserLoginComponent != null && _container.Contains(_existingUserLoginComponent))
        {
            _existingUserLoginComponent.ShowError(message);
        }
        else if (_returningUserLoginComponent != null && _container.Contains(_returningUserLoginComponent))
        {
            _returningUserLoginComponent.SetError(message);
        }
    }

    public void DisplayRegistrationError(string message)
    {
        if (_registerUserComponent != null && _container.Contains(_registerUserComponent))
        {
            _registerUserComponent.ShowError(message);
        }
    }

    public void DisplayError(string message)
    {
        if (_userSelectionComponent != null && _container.Contains(_userSelectionComponent))
        {
            _userSelectionComponent.ShowError(message);
        }
        else if (_existingUserLoginComponent != null && _container.Contains(_existingUserLoginComponent))
        {
            _existingUserLoginComponent.ShowError(message);
        }
        else if (_returningUserLoginComponent != null && _container.Contains(_returningUserLoginComponent))
        {
            _returningUserLoginComponent.SetError(message);
        }
        else if (_registerUserComponent != null && _container.Contains(_registerUserComponent))
        {
            _registerUserComponent.ShowError(message);
        }
    }
} 