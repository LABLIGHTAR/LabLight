using UnityEngine;
using UnityEngine.UIElements;
using Newtonsoft.Json; // For serializing protocol data

// You might want to place this in a specific namespace
// namespace YourApp.UI.Components
// {
    public class ProtocolListItemController : VisualElement
    {
        // UxmlFactory and UxmlTraits allow this custom element to be used in UXML
        // and to be instantiated by the UI Builder or UXML loading process.
        public new class UxmlFactory : UxmlFactory<ProtocolListItemController, UxmlTraits> { }
        public new class UxmlTraits : VisualElement.UxmlTraits { }

        // Constants for child element names
        private const string SelectProtocolAreaButtonName = "select-protocol-area-button";
        private const string ProtocolNameLabelName = "protocol-name-label";
        private const string OwnerNameLabelName = "owner-name-label";
        private const string SaveUnsaveButtonName = "save-unsave-button";

        // Queried child elements
        private Button _selectProtocolButton;
        private Label _protocolNameLabel;
        private Label _ownerNameLabel;
        private Button _saveUnsaveButton;

        // Data and dependencies
        private ProtocolData _protocolData; // Changed from ProtocolDefinition to ProtocolData for broader compatibility with list sources
        private IUICallbackHandler _uiCallbackHandler;
        private IDatabase _database;

        public ProtocolListItemController()
        {
            // Load the UXML asset for this component.
            // Ensure the path is correct and the UXML file is in a Resources folder if loading this way,
            // or use AssetDatabase if this script is editor-only or for editor tooling.
            // For runtime, if ProtocolListItem.uxml is in the same folder as this controller's eventual
            // UXML representation (if this controller itself is used as a tag in another UXML),
            // then the UI Builder/UXML system might handle cloning.
            // However, for a self-contained component that IS the list item, explicitly loading is common.

            // This approach assumes ProtocolListItem.uxml is loaded by the VisualElement that *uses* this controller.
            // If this controller IS the root of ProtocolListItem.uxml, then the UXML content
            // should be cloned into `this` element when it's created.
            // For simplicity, we'll assume the elements are queried after this component is populated
            // by its parent (e.g. a list view controller that instantiates ProtocolListItem.uxml and then
            // perhaps would have added this script as a manipulator or if this script IS the root element).

            // A common pattern is to have the UXML define this custom element, and then this custom element's constructor
            // or an attached callback (like AttachToPanelEvent) would query its children defined within its own UXML.
            // For now, let's assume child elements are part of this controller's visual tree.

            // Querying elements is best done after they are part of the visual tree.
            // Registering a callback for AttachToPanelEvent is a robust way to do this.
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            // Query child elements once this element is attached to a panel
            _selectProtocolButton = this.Q<Button>(SelectProtocolAreaButtonName);
            // Query labels within the select button's hierarchy
            _protocolNameLabel = _selectProtocolButton?.Q<Label>(ProtocolNameLabelName);
            _ownerNameLabel = _selectProtocolButton?.Q<Label>(OwnerNameLabelName);
            _saveUnsaveButton = this.Q<Button>(SaveUnsaveButtonName);
            
            // If data was set before attach, ensure events are bound and state is refreshed
            if (_protocolData != null && _uiCallbackHandler != null && _database != null)
            {
                BindDataAndEvents();
            }
        }
        
        public void SetProtocolData(ProtocolData protocol, IUICallbackHandler uiCallbackHandler, IDatabase database)
        {
            _protocolData = protocol;
            _uiCallbackHandler = uiCallbackHandler;
            _database = database;

            if (_protocolData == null || _uiCallbackHandler == null || _database == null)
            {
                Debug.LogError("ProtocolListItemController: Protocol data, UI callback handler, or Database is null.");
                this.SetEnabled(false);
                if(_protocolNameLabel != null) _protocolNameLabel.text = "Error";
                if(_ownerNameLabel != null) _ownerNameLabel.text = "Error loading data";
                return;
            }
            
            // Fallback querying if elements weren't ready during OnAttachToPanel or SetProtocolData called first
            if (_selectProtocolButton == null) _selectProtocolButton = this.Q<Button>(SelectProtocolAreaButtonName);
            if (_protocolNameLabel == null) _protocolNameLabel = _selectProtocolButton?.Q<Label>(ProtocolNameLabelName);
            if (_ownerNameLabel == null) _ownerNameLabel = _selectProtocolButton?.Q<Label>(OwnerNameLabelName);
            if (_saveUnsaveButton == null) _saveUnsaveButton = this.Q<Button>(SaveUnsaveButtonName);

            BindDataAndEvents();
        }

        private void BindDataAndEvents()
        {
            if (_protocolData == null || _uiCallbackHandler == null || _database == null) return;

            if (_protocolNameLabel != null)
            {   // Assuming ProtocolData has a 'Name' property
                _protocolNameLabel.text = _protocolData.Name; 
            }
            else { Debug.LogWarning($"'{ProtocolNameLabelName}' not found in ProtocolListItem for protocol '{_protocolData?.Name}'."); }

            if (_ownerNameLabel != null)
            {   // Assuming ProtocolData has an 'OwnerDisplayName' property
                _ownerNameLabel.text = $"Owner: {_protocolData.OwnerDisplayName ?? "N/A"}"; 
            }
            else { Debug.LogWarning($"'{OwnerNameLabelName}' not found in ProtocolListItem for protocol '{_protocolData?.Name}'."); }

            if (_selectProtocolButton != null)
            {
                _selectProtocolButton.UnregisterCallback<ClickEvent>(OnSelectProtocolClicked);
                _selectProtocolButton.RegisterCallback<ClickEvent>(OnSelectProtocolClicked);
            }
            else { Debug.LogWarning($"'{SelectProtocolAreaButtonName}' not found for protocol '{_protocolData?.Name}'."); }

            if (_saveUnsaveButton != null)
            {
                _saveUnsaveButton.UnregisterCallback<ClickEvent>(OnSaveUnsaveClicked);
                _saveUnsaveButton.RegisterCallback<ClickEvent>(OnSaveUnsaveClicked);
                RefreshSaveButtonState(); // Set initial state
            }
            else { Debug.LogWarning($"'{SaveUnsaveButtonName}' not found for protocol '{_protocolData?.Name}'."); }
        }

        public void RefreshSaveButtonState()
        {
            if (_saveUnsaveButton == null || _protocolData == null || _database == null) return;

            if (string.IsNullOrEmpty(_database.CurrentUserId))
            {
                _saveUnsaveButton.text = "N/A";
                _saveUnsaveButton.SetEnabled(false);
                return;
            }
            // Assuming ProtocolData has an 'Id' property (uint)
            bool isSaved = _database.IsProtocolSavedByUser(_protocolData.Id, _database.CurrentUserId);
            _saveUnsaveButton.text = isSaved ? "Unsave" : "Save";
            _saveUnsaveButton.userData = isSaved; 
            _saveUnsaveButton.SetEnabled(true);
        }

        private void OnSelectProtocolClicked(ClickEvent evt)
        {
            if (_protocolData == null || _uiCallbackHandler == null) return;
            
            // IMPORTANT: HandleProtocolSelection typically expects a JSON of the full ProtocolDefinition (with steps).
            // ProtocolData is often a summary. If _protocolData is just ProtocolData (summary from list),
            // you might need to fetch the full ProtocolDefinition using its ID before serializing,
            // or ensure HandleProtocolSelection can work with just an ID or summary to load the full definition itself.
            // For this example, we'll assume HandleProtocolSelection needs the full definition and _protocolData IS that,
            // OR that serializing ProtocolData and having the handler manage it is acceptable.
            // If ProtocolData needs to be converted/fetched to full ProtocolDefinition, that logic goes here or in handler.

            Debug.Log($"ProtocolListItemController: Protocol selected - {_protocolData.Name} (ID: {_protocolData.Id})");
            // Serializing _protocolData (which is ProtocolData type). Ensure downstream handler expects this or can derive full def.
            string protocolJson = JsonConvert.SerializeObject(_protocolData); 
            _uiCallbackHandler.HandleProtocolSelection(protocolJson);
        }

        private void OnSaveUnsaveClicked(ClickEvent evt)
        {
            if (_protocolData == null || _database == null) return;

            bool isCurrentlySaved = (bool)_saveUnsaveButton.userData;
            _saveUnsaveButton.SetEnabled(false); // Disable during operation

            Debug.Log($"ProtocolListItemController: Save/Unsave clicked for - {_protocolData.Name}. Was saved: {isCurrentlySaved}");

            if (isCurrentlySaved)
            {
                _database.UnsaveProtocol(_protocolData.Id);
            }
            else
            {
                _database.SaveProtocol(_protocolData.Id);
            }
            // The button state (text, enabled) will be refreshed when the parent controller's 
            // DB event handler calls RefreshSaveButtonState() on this instance.
        }
    }
// } 