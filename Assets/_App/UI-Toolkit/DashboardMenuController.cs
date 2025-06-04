using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Globalization;

public class DashboardMenuController : MonoBehaviour
{
    private IUIDriver _uiDriver; // For navigation from nav buttons
    private SessionManager _sessionManager;

    // Header Elements
    private VisualElement _profileImage;
    private Label _greetingLabel;
    private Label _userNameLabel;
    private Label _timeLabel;

    // Notices Panel Elements
    private VisualElement _noticesPanel;
    private VisualElement _noticesInnerContent;
    private Button _toggleNoticesButton;
    private bool _areNoticesVisible = true;
    private StyleLength _originalNoticesPanelWidth;

    // Navigation Buttons (example)
    private Button _navSettingsButton;
    private Button _navLogoutButton;
    private Button _navBrowseProtocolsButton;
    private Button _navSavedProtocolsButton;

    void OnEnable()
    {
        _uiDriver = ServiceRegistry.GetService<IUIDriver>();
        _sessionManager = SessionManager.instance;

        var root = GetComponent<UIDocument>().rootVisualElement;
        if (root == null) 
        {
            Debug.LogError("DashboardMenuController: Root VisualElement not found.");
            return;
        }

        // Query Header Elements
        _profileImage = root.Q<VisualElement>("profile-image");
        _greetingLabel = root.Q<Label>("greeting-label");
        _userNameLabel = root.Q<Label>("user-name-label");
        _timeLabel = root.Q<Label>("time-label");

        // Query Notices Panel Elements
        _noticesPanel = root.Q<VisualElement>("notices-panel");
        _noticesInnerContent = root.Q<VisualElement>("notices-inner-content");

        // Query Nav Buttons
        _navSettingsButton = root.Q<Button>("nav-settings");
        _navLogoutButton = root.Q<Button>("nav-logout");
        _navBrowseProtocolsButton = root.Q<Button>("nav-browse-protocols");
        _navSavedProtocolsButton = root.Q<Button>("nav-saved-protocols");

        // Register Callbacks
        _navSettingsButton?.RegisterCallback<ClickEvent>(OnNavSettingsClicked);
        _navLogoutButton?.RegisterCallback<ClickEvent>(OnNavLogoutClicked);
        _navBrowseProtocolsButton?.RegisterCallback<ClickEvent>(OnNavBrowseProtocolsClicked);
        _navSavedProtocolsButton?.RegisterCallback<ClickEvent>(OnNavSavedProtocolsClicked);

        if (_sessionManager != null)
        {
            _sessionManager.OnSessionUserChanged += HandleSessionUserChanged;
            Debug.Log("DashboardMenuController: Subscribed to SessionManager.OnSessionUserChanged.");
            if (SessionState.currentUserProfile != null)
            {
                HandleSessionUserChanged(SessionState.currentUserProfile);
            }
            else
            {
                UpdateUserNameDisplay(null);
            }
        }
        else
        {
            Debug.LogError("DashboardMenuController: SessionManager instance not found. Username updates will not be reactive.");
            UpdateUserNameDisplay(null);
        }

        if (_noticesPanel != null)
        {
            _originalNoticesPanelWidth = _noticesPanel.style.width;
        }
        else
        {
            Debug.LogWarning("Notices panel not found during OnEnable. Defaulting original width or manual setup needed.");
        }

        // Initial Setup
        UpdateTime();
        UpdateGreeting();
        UpdateUserName();
        SetInitialNoticesState();
    }

    void OnDisable()
    {
        _navSettingsButton?.UnregisterCallback<ClickEvent>(OnNavSettingsClicked);
        _navLogoutButton?.UnregisterCallback<ClickEvent>(OnNavLogoutClicked);
        _navBrowseProtocolsButton?.UnregisterCallback<ClickEvent>(OnNavBrowseProtocolsClicked);
        _navSavedProtocolsButton?.UnregisterCallback<ClickEvent>(OnNavSavedProtocolsClicked);

        if (_sessionManager != null)
        {
            _sessionManager.OnSessionUserChanged -= HandleSessionUserChanged;
            Debug.Log("DashboardMenuController: Unsubscribed from SessionManager.OnSessionUserChanged.");
        }
    }

    void Update()
    {
        if (_timeLabel != null && _timeLabel.enabledSelf)
        {
            UpdateTime(); 
        }
    }

    private void UpdateTime()
    {
        if (_timeLabel != null)
        {
            _timeLabel.text = DateTime.Now.ToString("h:mm tt", CultureInfo.InvariantCulture).ToUpper();
        }
    }

    private void UpdateGreeting()
    {
        if (_greetingLabel != null)
        {
            var hour = DateTime.Now.Hour;
            if (hour < 12)
                _greetingLabel.text = "Good morning,";
            else if (hour < 18)
                _greetingLabel.text = "Good afternoon,";
            else
                _greetingLabel.text = "Good evening,";
        }
    }

    private void UpdateUserName()
    {
        if (_userNameLabel != null && SessionState.currentUserProfile != null)
        {
            _userNameLabel.text = SessionState.currentUserProfile.Name;
        }
        else if (_userNameLabel != null)
        {
            _userNameLabel.text = "User"; // Fallback
        }
        // Potentially update _profileImage here too if URL/texture is available
    }

    private void UpdateUserNameDisplay(LocalUserProfileData userProfile) // Renamed from UpdateUserName for clarity
    {
        if (_userNameLabel != null)
        {
            _userNameLabel.text = userProfile?.Name ?? "User"; // Use provided profile or fallback
        }
        // Potentially update _profileImage here too if URL/texture is available from userProfile
    }

    private void HandleSessionUserChanged(LocalUserProfileData userProfile) // New handler for the SessionManager event
    {
        Debug.Log($"DashboardMenuController: HandleSessionUserChanged called for {userProfile?.Name ?? "null user"}. Refreshing display.");
        UpdateUserNameDisplay(userProfile);
        UpdateGreeting();
    }

    private void SetInitialNoticesState()
    {
        UpdateNoticesPanelAppearance();
    }

    private void UpdateNoticesPanelAppearance()
    {
        if (_noticesInnerContent != null)
        {
            _noticesInnerContent.style.display = _areNoticesVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }
        
        if (_noticesPanel != null)
        {
            if (_areNoticesVisible)
            {
                _noticesPanel.style.width = _originalNoticesPanelWidth;
            }
            else
            {
                _noticesPanel.style.width = new StyleLength(20f);
            }
        }
    }

    private void ToggleNoticesPanel(ClickEvent evt)
    {
        _areNoticesVisible = !_areNoticesVisible;
        UpdateNoticesPanelAppearance();
    }

    /* Placeholder for future implementation
    private void CheckInitialNoticesVisibility()
    {
        // Logic to check if there are actual notices
        bool hasNotices = _noticesScrollView?.childCount > 0; // Simple check, could be data-driven
        if (!hasNotices)
        {
            _areNoticesVisible = false;
            if(_noticesScrollView != null) _noticesScrollView.style.display = DisplayStyle.None;
            // Could also hide the entire _noticesPanel or parts of it
        }
    }
    */

    // Example Navigation Handler
    private void OnNavSettingsClicked(ClickEvent evt)
    {
        Debug.Log("Settings button clicked - Navigation to settings view not yet implemented.");
        // _uiDriver?.DisplaySettings(); // When DisplaySettings exists
    }

    private void OnNavLogoutClicked(ClickEvent evt) // New handler for Log Out button
    {
        Debug.Log("Log Out button clicked.");
        _uiDriver?.RequestSignOut(); // Call UIDriver to handle sign out
    }

    private void OnNavBrowseProtocolsClicked(ClickEvent evt)
    {
        Debug.Log("Browse Protocols button clicked.");
        _uiDriver?.DisplayBrowseProtocolsMenu();
    }

    private void OnNavSavedProtocolsClicked(ClickEvent evt)
    {
        Debug.Log("Saved Protocols button clicked.");
        _uiDriver?.DisplaySavedProtocolsMenu();
    }

    // Public method to be called by UIDriver when displaying this dashboard
    public void OnDisplay()
    {
        UpdateTime();
        UpdateGreeting();
        UpdateUserNameDisplay(SessionState.currentUserProfile);
        
        if (SessionState.currentUserProfile != null && _userNameLabel != null)
        {
             _userNameLabel.text = SessionState.currentUserProfile.Name;
        }
        else if (_userNameLabel != null)
        {
            _userNameLabel.text = "User"; // Ensure fallback if no profile
        }

        // Reset notices panel state if needed
        if (_noticesInnerContent != null && !_areNoticesVisible) // If returning to dash and it was collapsed
        {
           // Optionally, decide if it should remember collapsed state or always open expanded
           // For now, let's make it open expanded on display if it was collapsed.
           // _areNoticesVisible = true; 
           // _noticesInnerContent.style.display = DisplayStyle.Flex;
           // if(_noticesPanel != null) _noticesPanel.style.width = _originalNoticesPanelWidth;
        }

        // Ensure panel visibility and button text are correct based on _areNoticesVisible state
        UpdateNoticesPanelAppearance();
    }
} 