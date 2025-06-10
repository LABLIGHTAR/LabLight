using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class SavedProtocolsComponent : VisualElement
{
    private VisualTreeAsset _protocolListItemTemplate;

    private IUIDriver _uiDriver;
    private IFileManager _fileManager;
    private IDatabase _database;
    private IAudioService _audioService;

    private ScrollView _protocolsScrollView;
    private Button _refreshButton;

    private Dictionary<uint, VisualElement> _protocolIdToListItemMap = new Dictionary<uint, VisualElement>();

    public SavedProtocolsComponent(VisualTreeAsset componentAsset, VisualTreeAsset listItemAsset, IUIDriver uiDriver, IFileManager fileManager, IDatabase database)
    {
        componentAsset.CloneTree(this);

        _protocolListItemTemplate = listItemAsset;
        _uiDriver = uiDriver;
        _fileManager = fileManager;
        _database = database;
        _audioService = ServiceRegistry.GetService<IAudioService>();

        _protocolsScrollView = this.Q<ScrollView>("protocols-scroll-view");
        _refreshButton = this.Q<Button>("refresh-button");

        if (_protocolsScrollView == null) Debug.LogError("protocols-scroll-view not found in UXML.");
        if (_protocolListItemTemplate == null) Debug.LogError("ProtocolListItemTemplate is not assigned.");

        _refreshButton?.RegisterCallback<ClickEvent>(OnRefreshClicked);

        RegisterCallback<AttachToPanelEvent>(OnAttach);
        RegisterCallback<DetachFromPanelEvent>(OnDetach);
    }

    private void OnAttach(AttachToPanelEvent evt)
    {
        SubscribeToDBEvents();
        LoadAndDisplayProtocols();
    }

    private void OnDetach(DetachFromPanelEvent evt)
    {
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
    
    private void OnRefreshClicked(ClickEvent evt)
    {
        _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center);
        Debug.Log("Refresh button clicked. Reloading saved protocols.");
        LoadAndDisplayProtocols();
    }

    private async void LoadAndDisplayProtocols()
    {
        if (_fileManager == null || _protocolsScrollView == null || _protocolListItemTemplate == null)
        {
            Debug.LogError("Cannot load saved protocols: File Manager, ScrollView, or Item Template is missing.");
            return;
        }

        ClearProtocolList();

        if (string.IsNullOrEmpty(_database?.CurrentUserId))
        {
            Debug.LogWarning("CurrentUserId is not available from database. Cannot load saved protocols.");
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
                Debug.LogError("IUICallbackHandler service not found. List items may not function correctly.");
            }

            foreach (var protocolDataEntry in result.Data)
            {
                TemplateContainer listItemInstance = _protocolListItemTemplate.Instantiate();
                ProtocolListItemController itemController = listItemInstance.Q<ProtocolListItemController>();

                if (itemController != null)
                {
                    itemController.SetProtocolData(protocolDataEntry, uiCallbackHandler, _database);
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
        _protocolsScrollView?.Clear();
        _protocolIdToListItemMap.Clear();
    }

    private void HandleSavedProtocolAdded(uint protocolId)
    {
        // For simplicity, we just refresh the whole list.
        // A more optimized approach could be to fetch just the new item and add it.
        LoadAndDisplayProtocols();
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