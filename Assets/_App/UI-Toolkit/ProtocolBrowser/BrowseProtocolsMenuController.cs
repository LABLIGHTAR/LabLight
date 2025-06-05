using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

public partial class BrowseProtocolsMenuController : MonoBehaviour
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
            Debug.LogError("BrowseProtocolsMenuController: Required services not found.");
            return;
        }

        var uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            Debug.LogError("BrowseProtocolsMenuController: UIDocument component not found on this GameObject.");
            return;
        }
        _root = uiDocument.rootVisualElement;
        if (_root == null)
        {
            Debug.LogError("BrowseProtocolsMenuController: Root VisualElement not found in UIDocument.");
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
        ClearProtocolList(); // Also cleans up item-specific callbacks
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
        // Assuming UIDriver has a method to go back or to a default/previous menu
        _uiDriver?.DisplayDashboard(); // Or another appropriate navigation method
    }

    private void OnRefreshClicked(ClickEvent evt)
    {
        Debug.Log("Refresh button clicked. Reloading protocols.");
        LoadAndDisplayProtocols();
    }

    private async void LoadAndDisplayProtocols()
    {
        if (_fileManager == null || _protocolsScrollView == null || protocolListItemTemplate == null)
        {
            Debug.LogError("BrowseProtocolsMenuController: Cannot load protocols: File Manager, ScrollView, or Item Template is missing.");
            return;
        }

        // Ensure database service is available
        if (_database == null)
        {
            Debug.LogError("BrowseProtocolsMenuController: Database service is not available.");
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
                Debug.LogError("BrowseProtocolsMenuController: IUICallbackHandler service not found. Protocol items cannot be fully initialized.");
                // Optionally, display an error in the UI
                var errorLabel = new Label("Error initializing protocol items: Missing UI handler.");
                errorLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                errorLabel.style.color = Color.red;
                _protocolsScrollView.Add(errorLabel);
                return;
            }

            foreach (var protocolDataEntry in result.Data)
            {
                TemplateContainer listItemInstance = protocolListItemTemplate.Instantiate();
                // Query for the ProtocolListItemController by type, as it's the root of its own UXML.
                ProtocolListItemController itemController = listItemInstance.Q<ProtocolListItemController>();

                if (itemController != null)
                {
                    // Single, safe call to SetProtocolData
                    itemController.SetProtocolData(protocolDataEntry, uiCallbackHandler, _database);
                }
                else
                {
                    Debug.LogError("Could not find ProtocolListItemController component in instantiated UXML item. Ensure ProtocolListItem.uxml's root is <ProtocolListItemController> and it has been correctly registered if needed.");
                    continue; // Skip this item
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
            // Query for the controller by type within the stored visual element
            ProtocolListItemController itemController = listItemVisualElement.Q<ProtocolListItemController>();
            itemController?.RefreshSaveButtonState();
        }
    }

    private void HandleSavedProtocolRemoved(uint protocolId)
    {
        if (_protocolIdToListItemMap.TryGetValue(protocolId, out VisualElement listItemVisualElement))
        {
            // Query for the controller by type within the stored visual element
            ProtocolListItemController itemController = listItemVisualElement.Q<ProtocolListItemController>();
            itemController?.RefreshSaveButtonState();
        }
    }
}

#if UNITY_EDITOR
public partial class BrowseProtocolsMenuController // Using partial class for editor-specific methods
{
    public async void Editor_CreateNewProtocol(string protocolName, string protocolContent, bool isPublic, uint organizationId)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Editor_CreateNewProtocol: This utility should be used when the application is in Play Mode to ensure services are available.");
            // Optionally, you could try to get services here if your ServiceRegistry supports edit-mode access, 
            // but it's often simpler to enforce Play Mode for service-dependent utilities.
            if (_fileManager == null) _fileManager = ServiceRegistry.GetService<IFileManager>();
            if (_fileManager == null) 
            {
                Debug.LogError("Editor_CreateNewProtocol: IFileManager service is not available even after attempting to get it. Please ensure services are initialized or run in Play Mode.");
                return;
            }
        }
        
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
            if (Application.isPlaying && gameObject.activeInHierarchy && _protocolsScrollView != null) 
            {
                LoadAndDisplayProtocols();
            }
        }
        else
        {
            Debug.LogError($"Editor_CreateNewProtocol: Failed to create protocol. Error: {result.Error?.Code} - {result.Error?.Message}");
        }
    }
}
#endif 