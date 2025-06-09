using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class BrowseProtocolsComponent : VisualElement
{
    private VisualTreeAsset _protocolListItemTemplate;

    private IUIDriver _uiDriver;
    private IFileManager _fileManager;
    private IDatabase _database;

    private ScrollView _protocolsScrollView;
    private Button _refreshButton;

    private Dictionary<uint, VisualElement> _protocolIdToListItemMap = new Dictionary<uint, VisualElement>();

    public BrowseProtocolsComponent(VisualTreeAsset componentAsset, VisualTreeAsset listItemAsset, IUIDriver uiDriver, IFileManager fileManager, IDatabase database)
    {
        componentAsset.CloneTree(this);

        _protocolListItemTemplate = listItemAsset;
        _uiDriver = uiDriver;
        _fileManager = fileManager;
        _database = database;

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
        Debug.Log("Refresh button clicked. Reloading protocols.");
        LoadAndDisplayProtocols();
    }

    private async void LoadAndDisplayProtocols()
    {
        if (_fileManager == null || _protocolsScrollView == null || _protocolListItemTemplate == null)
        {
            Debug.LogError("Cannot load protocols: File Manager, ScrollView, or Item Template is missing.");
            return;
        }

        if (_database == null)
        {
            Debug.LogError("Database service is not available.");
            return;
        }

        ClearProtocolList();

        var result = await _fileManager.GetAvailableProtocolsAsync();

        if (result.Success && result.Data != null)
        {
            if (!result.Data.Any())
            {
                var noProtocolsLabel = new Label("No protocols available to browse.");
                noProtocolsLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                noProtocolsLabel.style.marginTop = 20;
                _protocolsScrollView.Add(noProtocolsLabel);
                return;
            }
            
            IUICallbackHandler uiCallbackHandler = ServiceRegistry.GetService<IUICallbackHandler>();
            if (uiCallbackHandler == null)
            {
                Debug.LogError("IUICallbackHandler service not found. Protocol items cannot be fully initialized.");
                return;
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
            Debug.LogError($"Failed to load available protocols: {result.Error?.Message}");
            var errorLabel = new Label("Error loading protocols.");
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
        if (_protocolIdToListItemMap.TryGetValue(protocolId, out VisualElement listItemVisualElement))
        {
            ProtocolListItemController itemController = listItemVisualElement.Q<ProtocolListItemController>();
            itemController?.RefreshSaveButtonState();
        }
    }

    private void HandleSavedProtocolRemoved(uint protocolId)
    {
        if (_protocolIdToListItemMap.TryGetValue(protocolId, out VisualElement listItemVisualElement))
        {
            ProtocolListItemController itemController = listItemVisualElement.Q<ProtocolListItemController>();
            itemController?.RefreshSaveButtonState();
        }
    }
} 