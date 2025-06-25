using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public partial class DashboardWindowController : BaseWindowController
{
    [Header("Component UXML Assets")]
    public VisualTreeAsset dashboardHomeComponentAsset;
    public VisualTreeAsset browseProtocolsComponentAsset;
    public VisualTreeAsset savedProtocolsComponentAsset;
    public VisualTreeAsset conversationsListComponentAsset;
    public VisualTreeAsset conversationListItemAsset;
    public VisualTreeAsset newChatComponentAsset;
    public VisualTreeAsset chatComponentAsset;
    public VisualTreeAsset protocolListItemAsset; // For list items

    private VisualElement _mainContentContainer;
    private SessionManager _sessionManager;

    // Navigation Buttons
    private Button _navHomeButton;
    private Button _navMessagesButton;
    private Button _navBrowseProtocolsButton;
    private Button _navSavedProtocolsButton;
    private Button _navSettingsButton;
    private Button _navLogoutButton;
    
    private List<Button> _navButtons;
    private VisualElement _currentView;
    private ConversationsListComponent _conversationsListComponent;

    // Services
    private IUIDriver _uiDriver;
    private IFileManager _fileManager;
    private IDatabase _database;
    private IAudioService _audioService;

    private readonly Dictionary<string, LocalUserProfileData> _userProfileCache = new Dictionary<string, LocalUserProfileData>();

    protected override void OnEnable()
    {
        base.OnEnable();
        
        // Resolve services
        _uiDriver = ServiceRegistry.GetService<IUIDriver>();
        _fileManager = ServiceRegistry.GetService<IFileManager>();
        _database = ServiceRegistry.GetService<IDatabase>();
        _audioService = ServiceRegistry.GetService<IAudioService>();
        _sessionManager = SessionManager.instance;

        // Add current user to cache if available
        if (SessionState.currentUserProfile != null)
        {
            _userProfileCache[SessionState.currentUserProfile.Id] = SessionState.currentUserProfile;
        }

        // Query UI Elements
        _mainContentContainer = rootVisualElement.Q<VisualElement>("main-content-container");
        _navHomeButton = rootVisualElement.Q<Button>("nav-home");
        _navMessagesButton = rootVisualElement.Q<Button>("nav-messages");
        _navBrowseProtocolsButton = rootVisualElement.Q<Button>("nav-browse-protocols");
        _navSavedProtocolsButton = rootVisualElement.Q<Button>("nav-saved-protocols");
        _navSettingsButton = rootVisualElement.Q<Button>("nav-settings");
        _navLogoutButton = rootVisualElement.Q<Button>("nav-logout");

        _navButtons = new List<Button> { _navHomeButton, _navBrowseProtocolsButton, _navSavedProtocolsButton, _navSettingsButton, _navMessagesButton };

        // Register Callbacks
        _navHomeButton?.RegisterCallback<ClickEvent>(evt => { _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center); ShowHomeComponent(); });
        _navMessagesButton?.RegisterCallback<ClickEvent>(evt => { _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center); ShowConversationsListComponent(); });
        _navBrowseProtocolsButton?.RegisterCallback<ClickEvent>(evt => { _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center); ShowBrowseProtocolsComponent(); });
        _navSavedProtocolsButton?.RegisterCallback<ClickEvent>(evt => { _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center); ShowSavedProtocolsComponent(); });
        _navSettingsButton?.RegisterCallback<ClickEvent>(OnNavSettingsClicked);
        _navLogoutButton?.RegisterCallback<ClickEvent>(OnNavLogoutClicked);

        if (_sessionManager != null)
        {
            _sessionManager.OnSessionUserChanged += HandleSessionUserChanged;
        }

        // Set initial state
        ShowHomeComponent();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (_sessionManager != null)
        {
            _sessionManager.OnSessionUserChanged -= HandleSessionUserChanged;
        }
        
        CleanupCurrentView();
        
        // Unregistering is good practice, though the elements are destroyed with the window.
        _navHomeButton?.UnregisterCallback<ClickEvent>(evt => ShowHomeComponent());
        _navMessagesButton?.UnregisterCallback<ClickEvent>(evt => ShowConversationsListComponent());
        _navBrowseProtocolsButton?.UnregisterCallback<ClickEvent>(evt => ShowBrowseProtocolsComponent());
        _navSavedProtocolsButton?.UnregisterCallback<ClickEvent>(evt => ShowSavedProtocolsComponent());
        _navSettingsButton?.UnregisterCallback<ClickEvent>(OnNavSettingsClicked);
        _navLogoutButton?.UnregisterCallback<ClickEvent>(OnNavLogoutClicked);
    }
    
    private void HandleSessionUserChanged(LocalUserProfileData userProfile)
    {
        // If the current view is the home component, update its username display
        if (_currentView is DashboardHomeComponent homeComponent)
        {
            homeComponent.UpdateUserName(userProfile);
        }
    }

    private void CleanupCurrentView()
    {
        if (_currentView is ConversationsListComponent conversationsComponent)
        {
            conversationsComponent.UnsubscribeFromDbEvents();
        }
        else if (_currentView is ChatComponent chatComponent)
        {
            chatComponent.UnsubscribeFromDbEvents();
        }
        // Future components that need cleanup can be added here
    }

    private async System.Threading.Tasks.Task<LocalUserProfileData> GetUserProfileAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return null;

        // 1. Check cache first
        if (_userProfileCache.TryGetValue(userId, out var cachedProfile) && cachedProfile != null)
        {
            return cachedProfile;
        }

        // 2. Try to get from local file storage
        var result = await _fileManager.GetLocalUserProfileAsync(userId);
        if (result.Success)
        {
            _userProfileCache[userId] = result.Data; // Update cache
            return result.Data;
        }

        // 3. Fallback to get basic data from DB
        var dbUser = _database.GetCachedUserProfile(userId);
        if (dbUser != null)
        {
            var newProfile = new LocalUserProfileData(dbUser, "");
            _userProfileCache[userId] = newProfile; // Update cache
            return newProfile;
        }

        // 4. If not found anywhere, cache a null and return it to prevent re-fetching
        _userProfileCache[userId] = null;
        return null;
    }

    private void ShowHomeComponent()
    {
        CleanupCurrentView();
        var homeComponent = new DashboardHomeComponent(dashboardHomeComponentAsset);
        homeComponent.UpdateUserName(SessionState.currentUserProfile);
        SwapComponent(_mainContentContainer, homeComponent);
        _currentView = homeComponent;
        UpdateNavButtonStyles(_navHomeButton);
    }

    private void ShowBrowseProtocolsComponent()
    {
        CleanupCurrentView();
        var browseComponent = new BrowseProtocolsComponent(browseProtocolsComponentAsset, protocolListItemAsset, _uiDriver, _fileManager, _database);
        SwapComponent(_mainContentContainer, browseComponent);
        _currentView = browseComponent;
        UpdateNavButtonStyles(_navBrowseProtocolsButton);
    }

    private void ShowSavedProtocolsComponent()
    {
        CleanupCurrentView();
        var savedComponent = new SavedProtocolsComponent(savedProtocolsComponentAsset, protocolListItemAsset, _uiDriver, _fileManager, _database);
        SwapComponent(_mainContentContainer, savedComponent);
        _currentView = savedComponent;
        UpdateNavButtonStyles(_navSavedProtocolsButton);
    }

    private async void ShowConversationsListComponent()
    {
        CleanupCurrentView();

        var conversations = _database.GetAllConversations().ToList();
        var userIdentities = conversations
            .SelectMany(c => c.Participants)
            .Select(p => p.ParticipantIdentity)
            .Distinct()
            .ToList();

        // Prime the cache for all participants
        foreach (var identity in userIdentities)
        {
            await GetUserProfileAsync(identity);
        }
        
        _conversationsListComponent = new ConversationsListComponent(
            conversationsListComponentAsset,
            conversationListItemAsset,
            _database,
            _audioService,
            (identity) => _userProfileCache.TryGetValue(identity, out var p) ? p : null);

        _conversationsListComponent.OnNewChatRequested += ShowNewChatComponent;
        _conversationsListComponent.OnConversationSelected += ShowChatComponent;
        
        SwapComponent(_mainContentContainer, _conversationsListComponent);
        _currentView = _conversationsListComponent;
        UpdateNavButtonStyles(_navMessagesButton);
    }

    private async void ShowNewChatComponent()
    {
        CleanupCurrentView();

        // Fetch all possible users
        var allDbUsers = _database.GetAllCachedUserProfiles();
        var localUserProfiles = new List<LocalUserProfileData>();
        foreach (var dbUser in allDbUsers)
        {
            // Use GetUserProfileAsync to ensure cache is populated and we get the best version
            var localProfile = await GetUserProfileAsync(dbUser.Id);
            if (localProfile != null)
            {
                localUserProfiles.Add(localProfile);
            }
        }

        // Filter out the current user BEFORE passing the list to the component
        var potentialRecipients = localUserProfiles
            .Where(p => p.Id != SessionState.currentUserProfile?.Id)
            .ToList();

        var newChatComponent = new NewChatComponent(
            newChatComponentAsset,
            _database,
            _fileManager,
            _audioService,
            potentialRecipients);

        newChatComponent.OnCancel += ShowConversationsListComponent;
        newChatComponent.OnMessageSent += () =>
        {
            ShowConversationsListComponent();
        };

        SwapComponent(_mainContentContainer, newChatComponent);
        _currentView = newChatComponent;
    }

    private async void ShowChatComponent(ConversationData conversation)
    {
        CleanupCurrentView();

        // Prime the cache for this conversation's participants
        foreach (var participant in conversation.Participants)
        {
            await GetUserProfileAsync(participant.ParticipantIdentity);
        }

        var chatComponent = new ChatComponent(
            chatComponentAsset,
            conversation,
            _database,
            _audioService,
            (identity) => _userProfileCache.TryGetValue(identity, out var p) ? p : null);

        chatComponent.OnBack += ShowConversationsListComponent;

        SwapComponent(_mainContentContainer, chatComponent);
        _currentView = chatComponent;
    }

    private void OnNavSettingsClicked(ClickEvent evt)
    {
        CleanupCurrentView();
        _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center);
        Debug.Log("Settings button clicked - Navigation to settings view not yet implemented.");
        // Placeholder for settings component
        var placeholder = new Label("Settings Component (Not Implemented Yet)");
        SwapComponent(_mainContentContainer, placeholder);
        _currentView = placeholder;
        UpdateNavButtonStyles(_navSettingsButton);
    }

    private void OnNavLogoutClicked(ClickEvent evt)
    {
        _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center);
        Debug.Log("Log Out button clicked.");
        _uiDriver?.RequestSignOut();
    }

    private void UpdateNavButtonStyles(Button selectedButton)
    {
        _navButtons.ForEach(button => {
            if (button != null)
            {
                button.RemoveFromClassList("button-primary-selected");
                if (!button.ClassListContains("button-primary"))
                {
                    button.AddToClassList("button-primary");
                }
            }
        });

        if (selectedButton != null)
        {
            selectedButton.RemoveFromClassList("button-primary");
            selectedButton.AddToClassList("button-primary-selected");
        }
    }
}

#if UNITY_EDITOR
public partial class DashboardWindowController // Using partial class for editor-specific methods
{
    public async void Editor_CreateNewProtocol(string protocolName, string protocolContent, bool isPublic, uint organizationId)
    {
        if (_fileManager == null)
        {
            Debug.LogError("Editor_CreateNewProtocol: IFileManager service is not available. Ensure the component is enabled and services are registered (e.g., in Play Mode).");
            return;
        }

        if (string.IsNullOrWhiteSpace(protocolName))
        {
            Debug.LogError("Editor_CreateNewProtocol: Protocol Name cannot be empty.");
            return;
        }

        Debug.Log($"Attempting to create protocol via editor: Name='{protocolName}', IsPublic={isPublic}, OrgID={organizationId}");

        var result = await _fileManager.SaveProtocolAsync(null, protocolName, protocolContent, isPublic, organizationId);

        if (result.Success)
        {
            Debug.Log($"Editor_CreateNewProtocol: Successfully dispatched protocol creation/update. ID: {result.Data?.ProtocolId}");
            // Optionally, refresh the view if in play mode and this view is active
            if (Application.isPlaying && gameObject.activeInHierarchy && _currentView is BrowseProtocolsComponent browseComponent)
            {
                // To refresh, we can just show the component again
                ShowBrowseProtocolsComponent();
            }
        }
        else
        {
            Debug.LogError($"Editor_CreateNewProtocol: Failed to create protocol. Error: {result.Error?.Code} - {result.Error?.Message}");
        }
    }
}
#endif 