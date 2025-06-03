using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class UserSelectionMenuController : MonoBehaviour
{
    public VisualTreeAsset userItemTemplate; // Assign this in the Inspector (a simple UXML for a user item)
    private IUIDriver _uiDriver;
    private IFileManager _fileManager;

    private ScrollView _userScrollView;
    private Button _loginButton;
    private Button _registerButton;
    private TextField _newUserNameField; 
    private Button _createUserButton;

    private List<LocalUserProfileData> _userProfiles;
    private string _selectedUserId = null;

    void OnEnable()
    {
        _uiDriver = ServiceRegistry.GetService<IUIDriver>();
        if (_uiDriver == null)
        {
            Debug.LogError("UserSelectionMenuController: UnityUIDriver not found.");
            return;
        }

        _fileManager = ServiceRegistry.GetService<IFileManager>();
        if (_fileManager == null)
        {
            Debug.LogError("UserSelectionMenuController: IFileManager not found.");
            // Handle this case, perhaps disable UI or show an error
            return;
        }

        var root = GetComponent<UIDocument>().rootVisualElement;
        if (root == null)
        {
            Debug.LogError("UserSelectionMenuController: Root VisualElement not found.");
            return;
        }

        _userScrollView = root.Q<ScrollView>("user-scroll-view");
        _loginButton = root.Q<Button>("login-button");
        _registerButton = root.Q<Button>("register-button");

        if (_userScrollView == null) Debug.LogError("UserScrollView is null");
        if (_loginButton == null) Debug.LogError("LoginButton is null");
        if (_registerButton == null) Debug.LogError("RegisterButton is null");


        _loginButton?.RegisterCallback<ClickEvent>(OnLoginClicked);
        _registerButton?.RegisterCallback<ClickEvent>(OnRegisterClicked);

        LoadAndDisplayUserProfiles();
    }

    void OnDisable()
    {
        _loginButton?.UnregisterCallback<ClickEvent>(OnLoginClicked);
        _registerButton?.UnregisterCallback<ClickEvent>(OnRegisterClicked);

        // Clean up user item callbacks
        if (_userScrollView != null)
        {
            var userItems = _userScrollView.Children().ToList();
            foreach (var userItem in userItems)
            {
                // Unregister the specific callback that was registered.
                userItem.UnregisterCallback<ClickEvent, VisualElement>(OnUserItemSelected);
            }
        }
    }

    private async void LoadAndDisplayUserProfiles()
    {
        if (_fileManager == null) return;

        var result = await _fileManager.GetLocalUserProfilesAsync();
        if (result.Success && result.Data != null)
        {
            _userProfiles = result.Data;
            PopulateUserList(_userProfiles);
        }
        else
        {
            Debug.LogError($"Failed to load user profiles: {result.Error?.Message}");
            _userProfiles = new List<LocalUserProfileData>(); // Initialize with empty list
            PopulateUserList(_userProfiles); // Show empty or error state
        }
    }


    void PopulateUserList(List<LocalUserProfileData> users)
    {
        if (_userScrollView == null) return;
        _userScrollView.Clear(); // Clear existing items

        if (users == null || !users.Any()) {
            _userScrollView.style.display = DisplayStyle.None; // Hide the scroll view
            return;
        }

        _userScrollView.style.display = DisplayStyle.Flex; // Ensure scroll view is visible

        foreach (var user in users)
        {
            VisualElement userItem;
            if (userItemTemplate != null)
            {
                userItem = userItemTemplate.Instantiate();
                userItem.userData = user.Id; // Store user ID for click handling
                userItem.Q<Label>("user-name-label").text = user.Name; // Assuming your template has a label with this name
                // Potentially set user image here if your template supports it
                // userItem.Q<VisualElement>("user-image").style.backgroundImage = new StyleBackground(user.ProfilePicture); // Example
                 userItem.AddToClassList("user-item"); // Apply general styling
            }
            else // Fallback if no template is assigned - simple label item
            {
                userItem = new Button(() => OnUserItemSelected(user.Id)) { text = user.Name };
                userItem.AddToClassList("user-item-button-fallback"); // Style this class in USS if needed
                userItem.userData = user.Id; // Store user ID
            }

            userItem.RegisterCallback<ClickEvent, VisualElement>(OnUserItemSelected, userItem);
            _userScrollView.Add(userItem);
        }
    }
    
    // Overload or modified method to handle click event with a stored ID
    void OnUserItemSelected(ClickEvent evt, VisualElement clickedItem)
    {   
        if (_userProfiles == null) 
        {
            Debug.LogError("User profiles list is null. Cannot select user.");
            return;
        }

        if (clickedItem.userData is string userId)
        {
            LocalUserProfileData selectedProfile = _userProfiles.FirstOrDefault(p => p.Id == userId);

            if (selectedProfile != null)
            {
                Debug.Log($"User profile found: {selectedProfile.Name} ({selectedProfile.Id}). Navigating to returning user login.");

                // Optionally, provide visual feedback for selection
                foreach (var child in _userScrollView.Children())
                {
                    child.RemoveFromClassList("user-item-selected"); // Assuming you have a .user-item-selected style
                }
                clickedItem.AddToClassList("user-item-selected");

                _uiDriver?.DisplayReturningUserLogin(selectedProfile); 
            }
            else
            {
                Debug.LogError($"Could not find LocalUserProfileData for ID: {userId}");
                // Potentially display an error to the user or fall back to a generic login
            }
        }
        else
        {
            Debug.LogError("Clicked item userData is not a string (userId).");
        }
    }

    // Keep this if you want explicit user item clicks to do something distinct
    // Or remove if OnUserItemSelected(ClickEvent, VisualElement) is the sole entry point for profile selection
    void OnUserItemSelected(string userId)
    {
        // _selectedUserId = userId;
        Debug.LogWarning($"Direct call to OnUserItemSelected(string userId) with {userId}. This path might need review.");
        // This path likely also needs to find the profile and call DisplayReturningUserLogin
        // For now, let's assume the ClickEvent version is the primary one.
        // If this is still needed, it should also find the LocalUserProfileData object.
        LocalUserProfileData selectedProfile = _userProfiles?.FirstOrDefault(p => p.Id == userId);
        if (selectedProfile != null)
        {
            _uiDriver?.DisplayReturningUserLogin(selectedProfile);
        }
        else
        {
            Debug.LogError($"Could not find LocalUserProfileData for ID: {userId} in direct call.");
             // Fallback or error: perhaps navigate to generic login or show error.
            _uiDriver?.DisplayUserLogin(); // Example fallback
        }
    }


    void OnLoginClicked(ClickEvent evt)
    {
        Debug.Log("Login button clicked. Navigating to user login screen.");
        _uiDriver?.DisplayUserLogin(); 
    }

    void OnRegisterClicked(ClickEvent evt)
    {
        Debug.Log("Register button clicked. Navigating to user registration screen.");
        _uiDriver?.DisplayUserRegistration();
    }

    public void UpdateUserList(List<LocalUserProfileData> updatedProfiles)
    {
        Debug.Log("UserSelectionMenuController: UpdateUserList called.");
        _userProfiles = updatedProfiles;
        PopulateUserList(_userProfiles);
    }

    public void DisplayError(string errorMessage)
    {
        Debug.LogError($"UserSelectionMenuController Error: {errorMessage}");
        // Optionally, display this error in the UI, e.g., by adding a Label to your UXML
        // and setting its text here.
        // Example: _errorLabel.text = errorMessage; _errorLabel.style.display = DisplayStyle.Flex;
    }
} 