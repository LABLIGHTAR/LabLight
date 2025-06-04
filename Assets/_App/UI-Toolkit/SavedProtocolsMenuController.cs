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

            IUICallbackHandler uiCallbackHandler = ServiceRegistry.GetService<IUICallbackHandler>();
            if (uiCallbackHandler == null)
            {
                Debug.LogError("SavedProtocolsMenuController: IUICallbackHandler service not found. List items may not function correctly.");
                // Decide if we should stop populating or continue with items potentially non-interactive for protocol selection.
            }

            foreach (var protocolDataEntry in result.Data)
            {
                TemplateContainer listItemInstance = protocolListItemTemplate.Instantiate();
                ProtocolListItemController itemController = listItemInstance.Q<ProtocolListItemController>("protocol-item-container");

                if (itemController != null)
                {
                    if (uiCallbackHandler != null) 
                    {
                        itemController.SetProtocolData(protocolDataEntry, uiCallbackHandler, _database);
                    }
                    else
                    {
                         // If uiCallbackHandler is null, SetProtocolData might still be called 
                         // but the item controller will log an error and disable itself or parts of its functionality.
                         // Or, we can prevent calling it if critical dependencies are missing.
                         Debug.LogWarning($"SavedProtocolsMenuController: Skipping SetProtocolData for {protocolDataEntry.Name} due to missing IUICallbackHandler.");
                         itemController.SetEnabled(false); // Example: disable the item
                    }
                }
                else
                {
                    Debug.LogError("Could not find ProtocolListItemController component in instantiated UXML item.");
                    continue; 
                }
                
                _protocolsScrollView.Add(listItemInstance);
                _protocolIdToListItemMap[protocolDataEntry.Id] = listItemInstance;
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

    private void HandleSavedProtocolAdded(uint protocolId)
    {
        if (_protocolIdToListItemMap.TryGetValue(protocolId, out VisualElement listItemVisualElement))
        {
            ProtocolListItemController itemController = listItemVisualElement.Q<ProtocolListItemController>("protocol-item-container");
            itemController?.RefreshSaveButtonState();
        }
        else
        {
            // If the item wasn't in the list (e.g. saved from browse view and this view is active)
            // a full refresh might be needed, or dynamically add the item.
            // For now, if this view is active and an item is saved elsewhere, it will only appear on next refresh.
            // This handler primarily ensures that if an item *is* present, its save button is updated.
            // To dynamically add, we would need the ProtocolData for the added protocolId.
            // LoadAndDisplayProtocols(); // Could force a full refresh, but might be heavy.
        }
    }

    private void HandleSavedProtocolRemoved(uint protocolId)
    {
        if (_protocolIdToListItemMap.TryGetValue(protocolId, out VisualElement listItemVisualElement))
        {
            _protocolsScrollView?.Remove(listItemVisualElement);
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