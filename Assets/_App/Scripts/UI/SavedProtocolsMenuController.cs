using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

public class SavedProtocolsMenuController : MonoBehaviour
{
    [Tooltip("UXML template for each protocol item in the list.")]
    public VisualTreeAsset protocolListItemTemplate;

    private IUIDriver _uiDriver;
    private IFileManager _fileManager;
    private IDatabase _database;

    private VisualElement _root;
    private ScrollView _protocolsScrollView;
    private Button _backButton;
    private Button _refreshButton;

    // To keep track of items for dynamic updates
    private Dictionary<uint, VisualElement> _protocolIdToListItemMap = new Dictionary<uint, VisualElement>();

    void OnEnable()
    {
        _uiDriver = ServiceRegistry.GetService<IUIDriver>();
        _fileManager = ServiceRegistry.GetService<IFileManager>();
        _database = ServiceRegistry.GetService<IDatabase>();

        if (_uiDriver == null || _fileManager == null || _database == null)
        {
            Debug.LogError("SavedProtocolsMenuController: Required services not found.");
            return;
        }

        var uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            Debug.LogError("SavedProtocolsMenuController: UIDocument component not found on this GameObject.");
            return;
        }
        _root = uiDocument.rootVisualElement;
        if (_root == null)
        {
            Debug.LogError("SavedProtocolsMenuController: Root VisualElement not found in UIDocument.");
            return;
        }

        _protocolsScrollView = _root.Q<ScrollView>("protocols-scroll-view");
        _backButton = _root.Q<Button>("back-button");
        _refreshButton = _root.Q<Button>("refresh-button");

        if (_protocolsScrollView == null) Debug.LogError("protocols-scroll-view not found in UXML.");
        if (protocolListItemTemplate == null) Debug.LogError("ProtocolListItemTemplate is not assigned in the inspector.");

        _backButton?.RegisterCallback<ClickEvent>(OnBackClicked);
        _refreshButton?.RegisterCallback<ClickEvent>(OnRefreshClicked);
        
        SubscribeToDBEvents();
        LoadAndDisplayProtocols();
    }

    void OnDisable()
    {
        _backButton?.UnregisterCallback<ClickEvent>(OnBackClicked);
        _refreshButton?.UnregisterCallback<ClickEvent>(OnRefreshClicked);
        UnsubscribeFromDBEvents();
        ClearProtocolList();
    }

    private void SubscribeToDBEvents()
    {
        if (_database != null)
        {
            _database.OnSavedProtocolAdded += HandleSavedProtocolAdded;
            _database.OnSavedProtocolRemoved += HandleSavedProtocolRemoved;
        }
    }

    private void UnsubscribeFromDBEvents()
    {
        if (_database != null)
        {
            _database.OnSavedProtocolAdded -= HandleSavedProtocolAdded;
            _database.OnSavedProtocolRemoved -= HandleSavedProtocolRemoved;
        }
    }

    private void OnBackClicked(ClickEvent evt)
    {
        _uiDriver?.DisplayDashboard();
    }

    private void OnRefreshClicked(ClickEvent evt)
    {
        Debug.Log("Refresh button clicked. Reloading saved protocols.");
        LoadAndDisplayProtocols();
    }

    private async void LoadAndDisplayProtocols()
    {
        if (_fileManager == null || _protocolsScrollView == null || protocolListItemTemplate == null)
        {
            Debug.LogError("Cannot load saved protocols: File Manager, ScrollView, or Item Template is missing.");
            return;
        }

        ClearProtocolList();

        if (string.IsNullOrEmpty(_database?.CurrentUserId))
        {
            Debug.LogWarning("SavedProtocolsMenuController: CurrentUserId is not available from database. Cannot load saved protocols.");
            var noUserLabel = new Label("Please log in to see saved protocols.");
            noUserLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            noUserLabel.style.marginTop = 20;
            _protocolsScrollView.Add(noUserLabel);
            return;
        }

        var result = await _fileManager.GetSavedProtocolsAsync();

        if (result.Success && result.Data != null)
        {
            if (!result.Data.Any())
            {
                var noProtocolsLabel = new Label("You have no saved protocols.");
                noProtocolsLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                noProtocolsLabel.style.marginTop = 20;
                _protocolsScrollView.Add(noProtocolsLabel);
                return;
            }

            foreach (var protocol in result.Data)
            {
                VisualElement listItem = protocolListItemTemplate.Instantiate();
                
                var protocolNameLabel = listItem.Q<Label>("protocol-name-label");
                var ownerNameLabel = listItem.Q<Label>("owner-name-label");
                var saveButton = listItem.Q<Button>("save-unsave-button");

                if (protocolNameLabel != null) protocolNameLabel.text = protocol.Name;
                if (ownerNameLabel != null) ownerNameLabel.text = $"Owner: {protocol.OwnerDisplayName ?? "N/A"}";
                
                if (saveButton != null)
                {
                    UpdateSaveButtonState(saveButton, protocol.Id);
                    saveButton.RegisterCallback<ClickEvent, uint>(HandleSaveUnsaveClicked, protocol.Id);
                }
                _protocolsScrollView.Add(listItem);
                _protocolIdToListItemMap[protocol.Id] = listItem;
            }
        }
        else
        {
            Debug.LogError($"Failed to load saved protocols: {result.Error?.Message}");
            var errorLabel = new Label("Error loading saved protocols.");
            errorLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            errorLabel.style.color = Color.red;
            _protocolsScrollView.Add(errorLabel);
        }
    }

    private void ClearProtocolList()
    {
        if (_protocolsScrollView != null)
        {
            _protocolsScrollView.Clear();
        }
        _protocolIdToListItemMap.Clear();
    }

    private void UpdateSaveButtonState(Button button, uint protocolId)
    {
        if (_database == null || string.IsNullOrEmpty(_database.CurrentUserId))
        {
            button.text = "N/A";
            button.SetEnabled(false);
            return;
        }
        bool isSaved = _database.IsProtocolSavedByUser(protocolId, _database.CurrentUserId);
        button.text = isSaved ? "Unsave" : "Save";
        button.userData = isSaved; 
        button.SetEnabled(true);
    }

    private void HandleSaveUnsaveClicked(ClickEvent evt, uint protocolId)
    {
        if (_database == null) return;

        var button = evt.currentTarget as Button;
        if (button == null) return;

        bool isCurrentlySaved = (bool)button.userData;

        button.SetEnabled(false); 

        if (isCurrentlySaved)
        {
            _database.UnsaveProtocol(protocolId);
        }
        else
        {
            _database.SaveProtocol(protocolId);
        }
    }

    private void HandleSavedProtocolAdded(uint protocolId)
    {
        // If this view is active, we might want to refresh or specifically add the item
        // For now, a full refresh on add/remove is simpler than trying to merge.
        // Or, ensure the button state is updated if the item is already visible.
        if (_protocolIdToListItemMap.TryGetValue(protocolId, out VisualElement listItem))
        {
            var button = listItem.Q<Button>("save-unsave-button");
            if (button != null)
            {
                UpdateSaveButtonState(button, protocolId);
            }
        }
        else
        {
            // If the item wasn't in the list (e.g., saved from browse view), and this view becomes active,
            // it will be picked up by LoadAndDisplayProtocols.
            // If this view IS active, a refresh might be desired. For now, a manual refresh is needed
            // or LoadAndDisplayProtocols could be called here.
            // Consider if this view should auto-refresh if a protocol is saved elsewhere.
            // For now, we only update the button if it's already listed.
        }
    }

    private void HandleSavedProtocolRemoved(uint protocolId)
    {
        // If this view is active, the item should be removed from the list.
        // The current logic relies on the DB event to update the button, but if an item is unsaved,
        // it should disappear from THIS list.
        if (_protocolIdToListItemMap.TryGetValue(protocolId, out VisualElement listItem))
        {
            _protocolsScrollView?.Remove(listItem);
            _protocolIdToListItemMap.Remove(protocolId);

            if (!_protocolIdToListItemMap.Any() && _protocolsScrollView != null)
            {
                var noProtocolsLabel = new Label("You have no saved protocols.");
                noProtocolsLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                noProtocolsLabel.style.marginTop = 20;
                _protocolsScrollView.Add(noProtocolsLabel);
            }
        }
    }
} 