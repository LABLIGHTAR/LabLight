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
        private const string ProtocolDescriptionLabelName = "protocol-description-label";
        private const string SaveUnsaveButtonName = "save-unsave-button";
        private const string DeleteProtocolButtonName = "delete-protocol-button";

        // Queried child elements
        private Button _selectProtocolButton;
        private Label _protocolNameLabel;
        private Label _ownerNameLabel;
        private Label _protocolDescriptionLabel;
        private Button _saveUnsaveButton;
        private Button _deleteProtocolButton;

        // Data and dependencies
        private ProtocolData _protocolData; 
        private ProtocolDefinition _protocolDefinition; // Stores the deserialized full protocol definition
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
            _protocolDescriptionLabel = _selectProtocolButton?.Q<Label>(ProtocolDescriptionLabelName);
            _saveUnsaveButton = this.Q<Button>(SaveUnsaveButtonName);
            _deleteProtocolButton = this.Q<Button>(DeleteProtocolButtonName);
            
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
            _protocolDefinition = null; // Reset in case of reuse

            if (_protocolData == null || _uiCallbackHandler == null || _database == null)
            {
                Debug.LogError("ProtocolListItemController: Protocol data, UI callback handler, or Database is null.");
                this.SetEnabled(false);
                if(_protocolNameLabel != null) _protocolNameLabel.text = "Error";
                if(_ownerNameLabel != null) _ownerNameLabel.text = "Error loading data";
                if(_protocolDescriptionLabel != null) _protocolDescriptionLabel.text = "Error loading data";
                return;
            }
            
            // Attempt to parse the full ProtocolDefinition from ProtocolData.Content
            if (!string.IsNullOrEmpty(_protocolData.Content))
            {
                try
                {
                    _protocolDefinition = JsonConvert.DeserializeObject<ProtocolDefinition>(_protocolData.Content);
                    if (_protocolDefinition == null)
                    {
                        Debug.LogWarning($"ProtocolListItemController: Deserialized ProtocolDefinition is null for protocol '{_protocolData.Name}'. Content might be invalid.");
                    }
                }
                catch (JsonException ex)
                {
                    Debug.LogError($"ProtocolListItemController: Failed to deserialize ProtocolData.Content for protocol '{_protocolData.Name}'. Error: {ex.Message}");
                    _protocolDefinition = null; // Ensure it's null if parsing fails
                }
            }
            else
            {
                Debug.LogWarning($"ProtocolListItemController: ProtocolData.Content is null or empty for protocol '{_protocolData.Name}'. Full definition not available.");
            }
            
            // Fallback querying if elements weren't ready during OnAttachToPanel or SetProtocolData called first
            if (_selectProtocolButton == null) _selectProtocolButton = this.Q<Button>(SelectProtocolAreaButtonName);
            if (_protocolNameLabel == null) _protocolNameLabel = _selectProtocolButton?.Q<Label>(ProtocolNameLabelName);
            if (_ownerNameLabel == null) _ownerNameLabel = _selectProtocolButton?.Q<Label>(OwnerNameLabelName);
            if (_protocolDescriptionLabel == null) _protocolDescriptionLabel = _selectProtocolButton?.Q<Label>(ProtocolDescriptionLabelName);
            if (_saveUnsaveButton == null) _saveUnsaveButton = this.Q<Button>(SaveUnsaveButtonName);
            if (_deleteProtocolButton == null) _deleteProtocolButton = this.Q<Button>(DeleteProtocolButtonName);

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

            if (_protocolDescriptionLabel != null)
            {
                _protocolDescriptionLabel.text = _protocolDefinition?.description ?? "Description not available.";
            }

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

            // Handle Delete Button Visibility and Event
            if (_deleteProtocolButton != null)
            {
                ProtocolOwnershipData ownership = _database.GetCachedProtocolOwnership(_protocolData.Id);
                // Assuming ProtocolOwnershipData has an OwnerID field (string type, SpacetimeDB Identity)
                // And IDatabase.CurrentUserId provides the current SpacetimeDB Identity of the logged-in user.
                if (ownership != null && !string.IsNullOrEmpty(ownership.OwnerId) && ownership.OwnerId == _database.CurrentUserId)
                {
                    _deleteProtocolButton.style.display = DisplayStyle.Flex; // Make visible
                    _deleteProtocolButton.SetEnabled(true);
                    _deleteProtocolButton.UnregisterCallback<ClickEvent>(OnDeleteProtocolClicked);
                    _deleteProtocolButton.RegisterCallback<ClickEvent>(OnDeleteProtocolClicked);
                }
                else
                {
                    _deleteProtocolButton.style.display = DisplayStyle.None; // Keep hidden
                    _deleteProtocolButton.SetEnabled(false);
                }
            }
            else { Debug.LogWarning($"'{DeleteProtocolButtonName}' not found for protocol '{_protocolData?.Name}'."); }
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

            if (_protocolDefinition == null)
            {
                Debug.LogError($"ProtocolListItemController: ProtocolDefinition not available for protocol '{_protocolData.Name}' (ID: {_protocolData.Id}). Cannot proceed with selection as full definition is required.");
                return;
            }
            
            Debug.Log($"ProtocolListItemController: Protocol selected - {_protocolDefinition.title} (ID: {_protocolData.Id})");
            // Serializing _protocolDefinition. Ensure downstream handler expects this.
            string protocolJson = JsonConvert.SerializeObject(_protocolDefinition); 
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

        private void OnDeleteProtocolClicked(ClickEvent evt)
        {
            if (_protocolData == null || _uiCallbackHandler == null || _database == null) return;
            
            // Optional: Add a confirmation dialog here before deleting.
            // For now, directly call the handler.
            Debug.Log($"ProtocolListItemController: Delete requested for protocol - {_protocolData.Name} (ID: {_protocolData.Id})");
            _uiCallbackHandler.HandleDeleteProtocol(_protocolData.Id);
        }
    }
// } 