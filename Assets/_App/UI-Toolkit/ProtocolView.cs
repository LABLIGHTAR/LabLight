using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic; // For List
using System.Linq; // Added for LINQ operations like FirstOrDefault
using UniRx; // Added for CompositeDisposable and Subscribe extensions

// Ensure this namespace matches your project structure if it's different
// namespace YourApp.UI 
// {
    public class ProtocolView : VisualElement
    {
        // UxmlFactory for instantiating from UXML
        public new class UxmlFactory : UxmlFactory<ProtocolView, UxmlTraits> { }

        // UxmlTraits (can be left empty for now if no custom attributes)
        public new class UxmlTraits : VisualElement.UxmlTraits { }

        // Constants for element names (optional but good practice)
        private const string ChecklistPanelName = "checklist-panel";
        private const string ContentPanelName = "content-panel";
        private const string PreviousStepButtonName = "previous-step-button";
        private const string NextStepButtonName = "next-step-button";
        private const string StepIndicatorLabelName = "step-indicator-label";
        private const string ChecklistContainerName = "checklist-container";
        private const string ProtocolTitleLabelName = "protocol-title-label";
        private const string ProtocolContentContainerName = "protocol-content-container";
        private const string UserAvatarIconName = "user-avatar-icon";
        private const string UserNameLabelName = "user-name-label";
        private const string ProtocolImageName = "protocol-image";
        private const string ProtocolDescriptionTopName = "protocol-description-top";
        private const string ProtocolDescriptionBottomName = "protocol-description-bottom";
        // Add names for footer action buttons if direct interaction is needed from C#
        private const string PdfButtonName = "pdf-button";
        private const string CalculatorButtonName = "calculator-button";
        private const string CommentsButtonName = "comments-button";
        private const string SignOffActionButtonName = "sign-off-action-button";
        private const string SignatureLineLabelName = "signature-line-label";
        private const string CameraButtonName = "camera-button";
        private const string ArViewButtonName = "ar-view-button";

        // Queried Elements - Left Panel
        private Button _previousStepButton;
        private Button _nextStepButton;
        private Label _stepIndicatorLabel;
        private VisualElement _checklistContainer;
        private Image _userAvatarIcon;
        private Label _userNameLabel;

        // Queried Elements - Right Panel
        private Label _protocolTitleLabel;
        private VisualElement _protocolContentContainer;
        private Image _protocolImage;
        private Label _protocolDescriptionTopLabel;
        private Label _protocolDescriptionBottomLabel;
        private Button _pdfButton;
        private Button _calculatorButton;
        private Button _commentsButton;
        private Button _signOffActionButton;
        private Label _signatureLineLabel;
        private Button _cameraButton;
        private Button _arViewButton;
        // Add other action buttons if needed

        // Data models (to be populated from elsewhere, e.g., ProtocolState)
        private ProtocolDefinition _currentProtocol;
        private StepDefinition _currentStepDefinition;
        private int _currentStepIndex = 0;

        // Services and State
        private IUIDriver _uiDriver;
        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        private bool _isSignedOff = false;

        public ProtocolView()
        {
            // Load UXML - typically done by the UI Builder or a parent component that instantiates this view.
            // If this view is the root, it might load its own UXML tree.
            // For custom elements, VisualTreeAsset is often assigned in the constructor or an Init method.
            // Example: 
            // var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_App/UI-Toolkit/Views/ProtocolView/ProtocolView.uxml");
            // visualTree.CloneTree(this);

            // Load Stylesheet
            StyleSheet styleSheet = Resources.Load<StyleSheet>("Styles/ProtocolView"); // Assuming it's in a Resources/Styles folder
                                                                                       // Or use AssetDatabase for Editor context:
            // StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/_App/UI-Toolkit/Views/ProtocolView/ProtocolView.uss");
            // if (styleSheet != null)
            //    styleSheets.Add(styleSheet);
            // else
            //    Debug.LogError("ProtocolView.uss not found");

            // It's common to query elements after the VisualTree has been cloned into this element.
            // This is often done in a separate Initialize() method or after construction.
            // For now, assuming elements will be available post-construction/UXML load.
            
            // RegisterCallback<GeometryChangedEvent>(OnGeometryChanged); // Example for querying elements after they are ready

            // Defer querying elements and event registration until the geometry is ready.
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            // Get IUIDriver instance
            _uiDriver = ServiceRegistry.GetService<IUIDriver>();
            if (_uiDriver == null)
            {
                Debug.LogError("[ProtocolView] UIDriver not found in ServiceRegistry.");
                return;
            }
            
            InitializeView();

            // Subscribe to ProtocolState changes if this view should react automatically
            // This is a more robust way than relying on external calls to UpdateProtocolData/LoadStepData
            ProtocolState.Instance.ActiveProtocol.Subscribe(OnProtocolChanged).AddTo(_disposables);
            ProtocolState.Instance.CurrentStep.Subscribe(OnStepChanged).AddTo(_disposables);
            
            // If we need to react to fine-grained checklist item state changes immediately:
            ProtocolState.Instance.CurrentStepState
                .Select(stepState => stepState?.Checklist?.ToList())
                .Where(checklist => checklist != null)
                .Subscribe(checklistItemList => OnChecklistChanged(checklistItemList))
                .AddTo(_disposables);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            _disposables.Clear(); // Dispose all subscriptions
        }

        // Call this method after the UXML is loaded into this element.
        public void InitializeView()
        {
            // Query elements
            _previousStepButton = this.Q<Button>(PreviousStepButtonName);
            _nextStepButton = this.Q<Button>(NextStepButtonName);
            _stepIndicatorLabel = this.Q<Label>(StepIndicatorLabelName);
            _checklistContainer = this.Q<VisualElement>(ChecklistContainerName);
            _userAvatarIcon = this.Q<Image>(UserAvatarIconName);
            _userNameLabel = this.Q<Label>(UserNameLabelName);

            _protocolTitleLabel = this.Q<Label>(ProtocolTitleLabelName);
            _protocolContentContainer = this.Q<VisualElement>(ProtocolContentContainerName);
            _protocolImage = this.Q<Image>(ProtocolImageName);
            _protocolDescriptionTopLabel = this.Q<Label>(ProtocolDescriptionTopName);
            _protocolDescriptionBottomLabel = this.Q<Label>(ProtocolDescriptionBottomName);
            _pdfButton = this.Q<Button>(PdfButtonName);
            _calculatorButton = this.Q<Button>(CalculatorButtonName);
            _commentsButton = this.Q<Button>(CommentsButtonName);
            _signOffActionButton = this.Q<Button>(SignOffActionButtonName);
            _signatureLineLabel = this.Q<Label>(SignatureLineLabelName);
            _cameraButton = this.Q<Button>(CameraButtonName);
            _arViewButton = this.Q<Button>(ArViewButtonName);

            // Register event handlers
            RegisterEventHandlers();

            // Initial population based on current ProtocolState
            OnProtocolChanged(ProtocolState.Instance.ActiveProtocol.Value);
            // User name from SessionState
            _userNameLabel.text = SessionState.currentUserProfile?.GetName() ?? "User Name";
            UpdateSignOffButtonState();
        }

        private void RegisterEventHandlers()
        {
            _previousStepButton?.RegisterCallback<ClickEvent>(evt => _uiDriver?.StepNavigationCallback(_currentStepIndex - 1));
            _nextStepButton?.RegisterCallback<ClickEvent>(evt => _uiDriver?.StepNavigationCallback(_currentStepIndex + 1));
            _pdfButton?.RegisterCallback<ClickEvent>(OnPdfButtonClicked);
            _calculatorButton?.RegisterCallback<ClickEvent>(evt => _uiDriver?.DisplayCalculator());
            _commentsButton?.RegisterCallback<ClickEvent>(evt => _uiDriver?.DisplayLLMChat());
            _signOffActionButton?.RegisterCallback<ClickEvent>(OnSignOffButtonClicked);
            _cameraButton?.RegisterCallback<ClickEvent>(evt => Debug.Log("Camera Button Clicked"));
            _arViewButton?.RegisterCallback<ClickEvent>(evt => Debug.Log("AR View Button Clicked"));
            // Add other button handlers
        }

        public void UpdateProtocolData(ProtocolDefinition protocol, string userName)
        {
            _currentProtocol = protocol;
            if (_currentProtocol != null)
            {
                _protocolTitleLabel.text = _currentProtocol.title;
                // Potentially load global AR objects or other protocol-wide settings
            }
            if (!string.IsNullOrEmpty(userName))
            {
                _userNameLabel.text = userName;
            }
            else
            {
                _userNameLabel.text = "-"; // Default if no user name
            }
            // LoadStepData(0); // Load the first step of the new protocol
        }

        public void LoadStepData(int stepIndex)
        {
            if (_currentProtocol == null || stepIndex < 0 || stepIndex >= _currentProtocol.steps.Count)
            {
                Debug.LogWarning($"Invalid step index: {stepIndex}");
                // Clear view or show empty state
                _stepIndicatorLabel.text = "Step - / -";
                _checklistContainer.Clear();
                _protocolContentContainer.Clear(); // Or hide elements
                return;
            }
            _currentStepDefinition = _currentProtocol.steps[stepIndex];
            _currentStepIndex = stepIndex;
            _stepIndicatorLabel.text = $"Step {stepIndex + 1} / {_currentProtocol.steps.Count}";
            
            // Use ProtocolState.Instance.CurrentStepState.Value.Checklist for status
            var currentStepChecklistState = ProtocolState.Instance.CurrentStepState?.Value?.Checklist;
            PopulateChecklist(currentStepChecklistState?.ToList());
            PopulateContentPanel(_currentStepDefinition.contentItems);
        }

        private void PopulateChecklist(List<ProtocolState.CheckItemState> checklistItemStatesList)
        {
            _checklistContainer.Clear();
            if (checklistItemStatesList == null || !checklistItemStatesList.Any() || _currentStepDefinition == null || _currentStepDefinition.checklist == null)
            {
                // Optionally show a "No checklist items" label
                var noItemsLabel = new Label("No checklist items for this step.");
                noItemsLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                _checklistContainer.Add(noItemsLabel);
                UpdateSignOffButtonState();
                return;
            }

            for(int i = 0; i < checklistItemStatesList.Count; i++)
            {
                // Ensure index is valid for definition list
                if (i >= _currentStepDefinition.checklist.Count)
                {
                    Debug.LogWarning($"[ProtocolView] Checklist state/definition mismatch at index {i}.");
                    continue;
                }
                var itemState = checklistItemStatesList[i];
                var itemDef = _currentStepDefinition.checklist[i];

                var checkItemVisual = new VisualElement();
                checkItemVisual.AddToClassList("check-item");
                // TODO: Add selection visual state if needed, separate from completion
                // if (i == some_selected_index) checkItemVisual.AddToClassList("check-item-selected");

                var statusIndicator = new VisualElement();
                statusIndicator.AddToClassList("check-item-status-indicator");
                if (itemState.IsChecked.Value)
                {
                    statusIndicator.AddToClassList("check-item-status-indicator-completed");
                }
                
                var checkItemText = new Label(itemDef.Text);
                checkItemText.AddToClassList("check-item-text");

                checkItemVisual.Add(statusIndicator);
                checkItemVisual.Add(checkItemText);

                // Store index for callback
                int itemIndex = i; 
                checkItemVisual.RegisterCallback<ClickEvent>(evt => {
                    if (_isSignedOff) return;

                    if (itemState.IsChecked.Value)
                    {
                        _uiDriver.UncheckItemCallback(itemIndex);
                    }
                    else
                    {
                        _uiDriver.CheckItemCallback(itemIndex);
                    }
                    // ProtocolState change should trigger reactive UI update.
                    // If direct refresh is needed and events are not granular enough:
                    // OnChecklistChanged(ProtocolState.Instance.CurrentStepState.Value.Checklist);
                });

                _checklistContainer.Add(checkItemVisual);
            }
            UpdateSignOffButtonState();
        }

        private void PopulateContentPanel(List<ContentItem> contentItems)
        {
            _protocolContentContainer.Clear();

            if (contentItems == null || !contentItems.Any())
            {
                // Default content if step has no specific content items
                var defaultText = new Label(_currentStepDefinition?.title ?? "No content for this step.");
                defaultText.AddToClassList("text-block"); // Use CoreStyles
                _protocolContentContainer.Add(defaultText);
                return;
            }

            foreach (var contentItem in contentItems)
            {
                VisualElement itemElement = null;
                switch (contentItem.contentType?.ToLower())
                {
                    case "text":
                        if (contentItem.properties.TryGetValue("Text", out object textObj))
                        {
                            var textLabel = new Label(textObj.ToString());
                            textLabel.AddToClassList("text-block");
                            itemElement = textLabel;
                        }
                        break;
                    case "image":
                        if (contentItem.properties.TryGetValue("ImageURL", out object imageUrlObj))
                        {
                            var imageElement = new Image();
                            string imagePath = imageUrlObj.ToString();
                            if (_currentProtocol != null && !string.IsNullOrEmpty(_currentProtocol.mediaBasePath) && !System.IO.Path.IsPathRooted(imagePath))
                            {
                                imagePath = System.IO.Path.Combine(_currentProtocol.mediaBasePath, imagePath);
                            }
                            // Texture2D texture = Resources.Load<Texture2D>(imagePath); // Needs to be in Resources or use Addressables
                            // if (texture == null) texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath); // Editor only
                            // For placeholder if loading fails or not implemented:
                            imageElement.style.backgroundColor = new StyleColor(Color.gray); 
                            imageElement.style.minHeight = 100; // Placeholder size
                            // if (texture != null) imageElement.image = texture; else Debug.LogWarning($"[ProtocolView] Failed to load image: {imagePath}");
                            imageElement.AddToClassList("protocol-main-image");
                            itemElement = imageElement;
                        }
                        break;
                    case "sound": // Example: Button to play sound via UIDriver
                    case "video": // Example: Button to show video via UIDriver
                    case "weburl": // Example: Button to open web page via UIDriver
                         if (contentItem.properties.TryGetValue("url", out object urlObj))
                         {
                            var button = new Button(() => {
                                string url = urlObj.ToString();
                                if (contentItem.contentType.ToLower() == "sound") 
                                {
                                    Debug.Log($"[ProtocolView] Sound play request: {url}. (Actual playback via UIDriver not yet implemented in IUIDriver)"); 
                                }
                                else if (contentItem.contentType.ToLower() == "video") 
                                {
                                    _uiDriver?.DisplayVideoPlayer(url);
                                }
                                else if (contentItem.contentType.ToLower() == "weburl") 
                                {
                                    _uiDriver?.DisplayWebPage(url);
                                }
                            });
                            button.text = $"Open {contentItem.contentType}: {System.IO.Path.GetFileName(urlObj.ToString())}";
                            button.AddToClassList("action-button"); // Use CoreStyles
                            itemElement = button;
                         }
                        break;
                    // Add cases for other ContentItem types as needed (e.g., InformationPanel)
                    default:
                        Debug.LogWarning($"[ProtocolView] Unsupported content type: {contentItem.contentType}");
                        break;
                }
                if (itemElement != null)
                {
                    _protocolContentContainer.Add(itemElement);
                }
            }
        }

        private void OnProtocolChanged(ProtocolDefinition protocol)
        {
            _currentProtocol = protocol;
            if (_currentProtocol != null)
            {
                _protocolTitleLabel.text = _currentProtocol.title;
                // Load the current step from ProtocolState or default to 0
                OnStepChanged(ProtocolState.Instance.CurrentStep.Value); 
            }
            else
            {
                // Clear view or show "No Protocol Loaded" state
                _protocolTitleLabel.text = "No Protocol Loaded";
                _stepIndicatorLabel.text = "Step - / -";
                _checklistContainer.Clear();
                _protocolContentContainer.Clear();
                _isSignedOff = false;
                UpdateSignOffButtonState();
            }
        }

        private void OnStepChanged(int stepIndex)
        {
            _currentStepIndex = stepIndex;
            _isSignedOff = ProtocolState.Instance.CurrentStepState?.Value?.SignedOff?.Value ?? false;

            if (_currentProtocol == null || _currentStepIndex < 0 || _currentStepIndex >= _currentProtocol.steps.Count)
            {
                Debug.LogWarning($"[ProtocolView] Invalid step index: {_currentStepIndex} for protocol '{_currentProtocol?.title}'. Steps available: {_currentProtocol?.steps?.Count}");
                // Clear step-specific parts of the view
                _stepIndicatorLabel.text = "Step - / -";
                _checklistContainer.Clear();
                _protocolContentContainer.Clear();
                _currentStepDefinition = null;
            }
            else
            {
                _currentStepDefinition = _currentProtocol.steps[_currentStepIndex];
                _stepIndicatorLabel.text = $"Step {_currentStepIndex + 1} / {_currentProtocol.steps.Count}";

                var currentStepState = ProtocolState.Instance.CurrentStepState?.Value;
                PopulateChecklist(currentStepState?.Checklist?.ToList());
                PopulateContentPanel(_currentStepDefinition.contentItems);
            }
            ResetSignatureLine();
            UpdateSignOffButtonState();
        }
        
        // Call this if checklist item states change without a full step change
        public void OnChecklistChanged(List<ProtocolState.CheckItemState> checklistItemStatesList)
        {
            // Ensure the current step definition is valid for context when rebuilding checklist
            if (_currentProtocol == null || _currentStepIndex < 0 || _currentStepIndex >= _currentProtocol.steps.Count)
            {
                //This can happen if checklist updates before step definition is fully set during rapid transitions
                return; 
            }
            _currentStepDefinition = _currentProtocol.steps[_currentStepIndex];
            PopulateChecklist(checklistItemStatesList);
            UpdateSignOffButtonState();
        }

        private void OnPdfButtonClicked(ClickEvent evt)
        {
            if (_currentProtocol?.protocolPDFNames?.Count > 0)
            {
                _uiDriver?.DisplayPDFReader(_currentProtocol.protocolPDFNames[0]); 
            }
            else
            {
                Debug.LogWarning("[ProtocolView] No PDFs available for the current protocol.");
                // Optionally show a message to the user in UI via HUD or a temporary label
            }
        }

        private void UpdateSignOffButtonState()
        {
            if (_signOffActionButton == null) return;

            bool allChecked = false;
            var checklist = ProtocolState.Instance.CurrentStepState?.Value?.Checklist;
            if (checklist != null && checklist.Any())
            {
                allChecked = checklist.All(item => item.IsChecked.Value);
            }
            else if (checklist != null && !checklist.Any()) // No items on checklist means it's 'complete'
            {
                allChecked = true;
            }
            
            if (_isSignedOff)
            {
                _signOffActionButton.SetEnabled(false);
            }
            else
            {
                _signOffActionButton.SetEnabled(allChecked);
            }
        }

        private void OnSignOffButtonClicked(ClickEvent evt)
        {
            if (_isSignedOff) return;

            var checklist = ProtocolState.Instance.CurrentStepState?.Value?.Checklist;
            if (checklist == null || !checklist.All(item => item.IsChecked.Value))
            {
                Debug.LogWarning("[ProtocolView] Sign Off clicked but not all items are checked.");
                return;
            }

            string userName = SessionState.currentUserProfile?.GetName() ?? "User";
            _signatureLineLabel.text = userName;
            _signatureLineLabel.AddToClassList("signature-font");
            
            _isSignedOff = true;
            _uiDriver.SignOffChecklistCallback();
            UpdateSignOffButtonState();
        }

        private void ResetSignatureLine()
        {
            if (_signatureLineLabel != null)
            {
                _signatureLineLabel.text = "";
                _signatureLineLabel.RemoveFromClassList("signature-font");
            }
        }

        // Add methods for other interactions (e.g., check item clicked)
    }
// } // End of namespace 