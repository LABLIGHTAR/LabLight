using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using UniRx;

public class ProtocolWindowController : BaseWindowController
{
    // Constants for element names
    private const string ChecklistPanelName = "checklist-panel";
    private const string ContentPanelName = "content-panel";
    private const string PreviousStepButtonName = "previous-step-button";
    private const string NextStepButtonName = "next-step-button";
    private const string StepIndicatorLabelName = "step-indicator-label";
    private const string ChecklistStepTitleLabelName = "checklist-step-title-label";
    private const string ChecklistContainerName = "checklist-container";
    private const string ProtocolTitleLabelName = "protocol-title-label";
    private const string ProtocolContentContainerName = "protocol-content-container";
    private const string ProtocolImageName = "protocol-image";
    private const string ProtocolDescriptionTopName = "protocol-description-top";
    private const string ProtocolDescriptionBottomName = "protocol-description-bottom";
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
    private Label _checklistStepTitleLabel;
    private VisualElement _checklistContainer;

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

    // Data models
    private ProtocolDefinition _currentProtocol;
    private StepDefinition _currentStepDefinition;
    private int _currentStepIndex = 0;

    // Services and State
    private IUIDriver _uiDriver;
    private IAudioService _audioService;
    private readonly CompositeDisposable _disposables = new CompositeDisposable();
    private bool _isSignedOff = false;

    protected override void OnEnable()
    {
        base.OnEnable();

        _uiDriver = ServiceRegistry.GetService<IUIDriver>();
        _audioService = ServiceRegistry.GetService<IAudioService>();
        if (_uiDriver == null)
        {
            Debug.LogError("[ProtocolViewWindowController] UIDriver not found in ServiceRegistry.");
            return;
        }

        InitializeView();

        Debug.Log("[ProtocolViewWindowController] Subscribing to ProtocolState changes.");
        ProtocolState.Instance.ActiveProtocol.Subscribe(OnProtocolChanged).AddTo(_disposables);
        ProtocolState.Instance.CurrentStep.Subscribe(OnStepChanged).AddTo(_disposables);

        _disposables.Add(ProtocolState.Instance.CurrentStepState
            .Where(stepState => stepState != null)
            .SelectMany(stepState =>
            {
                if (stepState.Checklist == null)
                {
                    return Observable.Return(new List<ProtocolState.CheckItemState>());
                }
                var itemCheckedStreams = stepState.Checklist.Select(checkItem => checkItem.IsChecked.AsUnitObservable());
                return Observable.Merge(itemCheckedStreams)
                    .Merge(stepState.Checklist.ObserveCountChanged(true).AsUnitObservable())
                    .StartWith(Unit.Default)
                    .Select(_ => stepState.Checklist.ToList());
            })
            .Subscribe(
                checklistItemList => OnChecklistChanged(checklistItemList ?? new List<ProtocolState.CheckItemState>()),
                ex => Debug.LogError($"[ProtocolViewWindowController] Error in CurrentStepState Checklist subscription: {ex}")
            )
        );
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        _disposables.Clear();
        UnregisterEventHandlers();
    }
    
    private void InitializeView()
    {
        Debug.Log("[ProtocolViewWindowController] InitializeView called.");
        // Query elements from rootVisualElement
        _previousStepButton = rootVisualElement.Q<Button>(PreviousStepButtonName);
        _nextStepButton = rootVisualElement.Q<Button>(NextStepButtonName);
        _stepIndicatorLabel = rootVisualElement.Q<Label>(StepIndicatorLabelName);
        _checklistStepTitleLabel = rootVisualElement.Q<Label>(ChecklistStepTitleLabelName);
        _checklistContainer = rootVisualElement.Q<VisualElement>(ChecklistContainerName);
        _protocolImage = rootVisualElement.Q<Image>(ProtocolImageName);
        _protocolDescriptionTopLabel = rootVisualElement.Q<Label>(ProtocolDescriptionTopName);
        _protocolDescriptionBottomLabel = rootVisualElement.Q<Label>(ProtocolDescriptionBottomName);
        _pdfButton = rootVisualElement.Q<Button>(PdfButtonName);
        _calculatorButton = rootVisualElement.Q<Button>(CalculatorButtonName);
        _commentsButton = rootVisualElement.Q<Button>(CommentsButtonName);
        _signOffActionButton = rootVisualElement.Q<Button>(SignOffActionButtonName);
        _signatureLineLabel = rootVisualElement.Q<Label>(SignatureLineLabelName);
        _cameraButton = rootVisualElement.Q<Button>(CameraButtonName);
        _arViewButton = rootVisualElement.Q<Button>(ArViewButtonName);
        _protocolTitleLabel = rootVisualElement.Q<Label>(ProtocolTitleLabelName);
        _protocolContentContainer = rootVisualElement.Q<VisualElement>(ProtocolContentContainerName);

        RegisterEventHandlers();

        Debug.Log("[ProtocolViewWindowController] Performing initial population from ProtocolState.");
        OnProtocolChanged(ProtocolState.Instance.ActiveProtocol.Value);
        UpdateSignOffButtonState();
    }
    
    private void RegisterEventHandlers()
    {
        _previousStepButton?.RegisterCallback<ClickEvent>(OnPreviousStepClicked);
        _nextStepButton?.RegisterCallback<ClickEvent>(OnNextStepClicked);
        _pdfButton?.RegisterCallback<ClickEvent>(OnPdfButtonClicked);
        _calculatorButton?.RegisterCallback<ClickEvent>(OnCalculatorClicked);
        _commentsButton?.RegisterCallback<ClickEvent>(OnCommentsClicked);
        _signOffActionButton?.RegisterCallback<ClickEvent>(OnSignOffButtonClicked);
        _cameraButton?.RegisterCallback<ClickEvent>(OnCameraButtonClicked);
        _arViewButton?.RegisterCallback<ClickEvent>(OnArViewButtonClicked);
    }
    
    private void UnregisterEventHandlers()
    {
        _previousStepButton?.UnregisterCallback<ClickEvent>(OnPreviousStepClicked);
        _nextStepButton?.UnregisterCallback<ClickEvent>(OnNextStepClicked);
        _pdfButton?.UnregisterCallback<ClickEvent>(OnPdfButtonClicked);
        _calculatorButton?.UnregisterCallback<ClickEvent>(OnCalculatorClicked);
        _commentsButton?.UnregisterCallback<ClickEvent>(OnCommentsClicked);
        _signOffActionButton?.UnregisterCallback<ClickEvent>(OnSignOffButtonClicked);
        _cameraButton?.UnregisterCallback<ClickEvent>(OnCameraButtonClicked);
        _arViewButton?.UnregisterCallback<ClickEvent>(OnArViewButtonClicked);
    }
    
    private void OnPreviousStepClicked(ClickEvent evt) { _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center); _uiDriver?.StepNavigationCallback(_currentStepIndex - 1); }
    private void OnNextStepClicked(ClickEvent evt) { _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center); _uiDriver?.StepNavigationCallback(_currentStepIndex + 1); }
    private void OnCalculatorClicked(ClickEvent evt) { _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center); _uiDriver?.DisplayCalculator(); }
    private void OnCommentsClicked(ClickEvent evt) { _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center); _uiDriver?.DisplayLLMChat(); }
    private void OnCameraButtonClicked(ClickEvent evt) { _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center); Debug.Log("Camera Button Clicked"); }
    private void OnArViewButtonClicked(ClickEvent evt) { _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center); Debug.Log("AR View Button Clicked"); }

    private void PopulateChecklist(List<ProtocolState.CheckItemState> checklistItemStatesList)
    {
        Debug.Log($"[ProtocolViewWindowController] PopulateChecklist called. Item count: {checklistItemStatesList?.Count ?? -1}");
        if (_checklistContainer == null) { Debug.LogError("[ProtocolViewWindowController] _checklistContainer is null in PopulateChecklist."); return; }
        _checklistContainer.Clear();

        if (checklistItemStatesList == null || !checklistItemStatesList.Any() || _currentStepDefinition == null || _currentStepDefinition.checklist == null)
        {
            Debug.Log("[ProtocolViewWindowController] No checklist items to display or prerequisites missing.");
            var noItemsLabel = new Label("No checklist items for this step.");
            noItemsLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _checklistContainer.Add(noItemsLabel);
            UpdateSignOffButtonState();
            return;
        }
        Debug.Log($"[ProtocolViewWindowController] Populating checklist with {checklistItemStatesList.Count} items based on StepDefinition: '{_currentStepDefinition.title}'");

        int firstUncheckedIndex = -1;
        for (int i = 0; i < checklistItemStatesList.Count; i++)
        {
            if (!checklistItemStatesList[i].IsChecked.Value)
            {
                firstUncheckedIndex = i;
                break;
            }
        }
        Debug.Log($"[ProtocolViewWindowController] PopulateChecklist: firstUncheckedIndex = {firstUncheckedIndex}");

        for (int i = 0; i < checklistItemStatesList.Count; i++)
        {
            if (i >= _currentStepDefinition.checklist.Count)
            {
                Debug.LogWarning($"[ProtocolViewWindowController] Checklist state/definition mismatch at index {i}.");
                continue;
            }
            var itemState = checklistItemStatesList[i];
            var itemDef = _currentStepDefinition.checklist[i];

            var checkItemVisual = new VisualElement();
            checkItemVisual.AddToClassList("check-item");

            var statusIndicator = new VisualElement();
            statusIndicator.AddToClassList("check-item-status-indicator");
            if (itemState.IsChecked.Value)
            {
                statusIndicator.AddToClassList("icon-check-circle");
            }
            else
            {
                statusIndicator.AddToClassList("icon-radio-unchecked");
            }

            var checkItemText = new Label(itemDef.Text);
            checkItemText.AddToClassList("check-item-text");

            checkItemVisual.Add(statusIndicator);
            checkItemVisual.Add(checkItemText);

            checkItemVisual.RemoveFromClassList("check-item-next");
            checkItemVisual.RemoveFromClassList("check-item-locked");

            if (_isSignedOff)
            {
                checkItemVisual.AddToClassList("check-item-locked");
            }
            else
            {
                bool isTheNextItemToBeChecked = (i == firstUncheckedIndex && firstUncheckedIndex != -1);
                bool canThisItemBeUnchecked = itemState.IsChecked.Value &&
                                            ((firstUncheckedIndex == -1 && i == checklistItemStatesList.Count - 1) ||
                                             (firstUncheckedIndex > 0 && i == firstUncheckedIndex - 1));

                if (isTheNextItemToBeChecked)
                {
                    checkItemVisual.AddToClassList("check-item-next");
                }
                else if (!canThisItemBeUnchecked)
                {
                    checkItemVisual.AddToClassList("check-item-locked");
                }
            }

            int itemIndexForCallback = i;
            checkItemVisual.UnregisterCallback<ClickEvent, int>(HandleChecklistItemClicked);
            checkItemVisual.RegisterCallback<ClickEvent, int>(HandleChecklistItemClicked, itemIndexForCallback);

            _checklistContainer.Add(checkItemVisual);
        }
        UpdateSignOffButtonState();
    }

    private void HandleChecklistItemClicked(ClickEvent evt, int itemIndex)
    {
        _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center);
        if (_isSignedOff) return;

        var checklistItemStatesList = ProtocolState.Instance.CurrentStepState?.Value?.Checklist;
        if (checklistItemStatesList == null || itemIndex < 0 || itemIndex >= checklistItemStatesList.Count)
        {
            Debug.LogWarning($"[ProtocolViewWindowController] HandleChecklistItemClicked: Invalid itemIndex {itemIndex} or checklist not found.");
            return;
        }

        var itemState = checklistItemStatesList[itemIndex];

        int firstUncheckedIdx = -1;
        for (int i = 0; i < checklistItemStatesList.Count; i++)
        {
            if (!checklistItemStatesList[i].IsChecked.Value)
            {
                firstUncheckedIdx = i;
                break;
            }
        }
        
        if (!itemState.IsChecked.Value)
        {
            if (itemIndex == firstUncheckedIdx)
            {
                Debug.Log($"[ProtocolViewWindowController] User clicked to CHECK item {itemIndex}");
                _uiDriver.CheckItemCallback(itemIndex);
            }
            else
            {
                Debug.Log($"[ProtocolViewWindowController] Item {itemIndex} cannot be checked (not the next one). Next is {firstUncheckedIdx}.");
            }
        }
        else
        {
            bool canUncheckThis = (firstUncheckedIdx == -1 && itemIndex == checklistItemStatesList.Count - 1) ||
                                  (firstUncheckedIdx > 0 && itemIndex == firstUncheckedIdx - 1);

            if (canUncheckThis)
            {
                Debug.Log($"[ProtocolViewWindowController] User clicked to UNCHECK item {itemIndex}");
                _uiDriver.UncheckItemCallback(itemIndex);
            }
            else
            {
                Debug.Log($"[ProtocolViewWindowController] Item {itemIndex} cannot be unchecked (not the allowed one). First unchecked is {firstUncheckedIdx}.");
            }
        }
    }

    private void PopulateContentPanel(List<ContentItem> contentItems)
    {
        _protocolContentContainer.Clear();

        if (contentItems == null || !contentItems.Any())
        {
            var defaultText = new Label(_currentStepDefinition?.title ?? "No content for this step.");
            defaultText.AddToClassList("text-block");
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
                        imageElement.style.backgroundColor = new StyleColor(Color.gray);
                        imageElement.style.minHeight = 100;
                        imageElement.AddToClassList("protocol-main-image");
                        itemElement = imageElement;
                    }
                    break;
                case "sound":
                case "video":
                case "weburl":
                    if (contentItem.properties.TryGetValue("url", out object urlObj))
                    {
                        var button = new Button();
                        button.RegisterCallback<ClickEvent>(evt =>
                        {
                            _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center);
                            string url = urlObj.ToString();
                            if (contentItem.contentType.ToLower() == "sound")
                            {
                                Debug.Log($"[ProtocolViewWindowController] Sound play request: {url}. (Actual playback via UIDriver not yet implemented in IUIDriver)");
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
                        button.AddToClassList("action-button");
                        itemElement = button;
                    }
                    break;
                default:
                    Debug.LogWarning($"[ProtocolViewWindowController] Unsupported content type: {contentItem.contentType}");
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
            OnStepChanged(ProtocolState.Instance.CurrentStep.Value);
        }
        else
        {
            _protocolTitleLabel.text = "No Protocol Loaded";
            ResetStepUIData();
            _isSignedOff = false;
            UpdateSignOffButtonState();
        }
    }

    private void OnStepChanged(int stepIndex)
    {
        Debug.Log($"[ProtocolViewWindowController] OnStepChanged called. Requested stepIndex: {stepIndex}. Current protocol: '{_currentProtocol?.title ?? "NULL"}'");
        _currentStepIndex = stepIndex;

        if (_currentProtocol == null || _currentProtocol.steps == null || stepIndex < 0 || stepIndex >= _currentProtocol.steps.Count)
        {
            Debug.LogWarning($"[ProtocolViewWindowController] Invalid step index: {stepIndex} for protocol '{_currentProtocol?.title}'. Steps available: {_currentProtocol?.steps?.Count ?? 0}. Clearing step view.");
            ResetStepUIData(stepIndicatorText: (_currentProtocol?.steps != null && _currentProtocol.steps.Any()) ? $"Step ? / {_currentProtocol.steps.Count}" : "Step - / -");
            _currentStepDefinition = null;
            _isSignedOff = false;
            UpdateSignOffButtonState();
            return;
        }
        
        _currentStepDefinition = _currentProtocol.steps[_currentStepIndex];
        Debug.Log($"[ProtocolViewWindowController] Current step definition set to: '{_currentStepDefinition?.title}' at index {_currentStepIndex}");
        
        if (ProtocolState.Instance != null && ProtocolState.Instance.Steps != null && stepIndex >= 0 && stepIndex < ProtocolState.Instance.Steps.Count)
        {
            var newStepState = ProtocolState.Instance.Steps[stepIndex];
            _isSignedOff = newStepState?.SignedOff?.Value ?? false;
            Debug.Log($"[ProtocolViewWindowController] OnStepChanged: Fetched StepState for index {stepIndex}. IsSignedOff from newStepState: {_isSignedOff}");

            if (_isSignedOff)
            {
                if (_signatureLineLabel != null)
                {
                    string userName = SessionState.currentUserProfile?.GetName() ?? "Signed";
                    _signatureLineLabel.text = userName;
                    _signatureLineLabel.AddToClassList("signed-text");
                    Debug.Log($"[ProtocolViewWindowController] OnStepChanged: Step already signed off. Signature line set for: {userName}");
                }
            }
            else
            {
                ResetSignatureLine();
            }
        }
        else
        {
            _isSignedOff = false;
            Debug.LogWarning($"[ProtocolViewWindowController] OnStepChanged: Could not get StepState for index {stepIndex} from ProtocolState.Instance.Steps. Defaulting _isSignedOff to false.");
            ResetSignatureLine();
        }

        if (_stepIndicatorLabel != null) _stepIndicatorLabel.text = $"Step {_currentStepIndex + 1} / {_currentProtocol.steps.Count}";
        else Debug.LogWarning("[ProtocolViewWindowController] _stepIndicatorLabel is null, cannot update step text.");
        
        if (_checklistStepTitleLabel != null)
        {
            _checklistStepTitleLabel.text = _currentStepDefinition?.title ?? "Unnamed Step";
        }

        var currentStepState = ProtocolState.Instance.CurrentStepState?.Value;
        Debug.Log($"[ProtocolViewWindowController] CurrentStepState for step '{_currentStepDefinition?.title}' has checklist count: {currentStepState?.Checklist?.Count ?? -1}");
        PopulateChecklist(currentStepState?.Checklist?.ToList());
        PopulateContentPanel(_currentStepDefinition.contentItems);
        UpdateSignOffButtonState();
    }

    private void OnChecklistChanged(List<ProtocolState.CheckItemState> checklistItemStatesList)
    {
        if (_currentProtocol == null || _currentStepIndex < 0 || _currentStepIndex >= _currentProtocol.steps.Count)
        {
            return;
        }
        _currentStepDefinition = _currentProtocol.steps[_currentStepIndex];
        PopulateChecklist(checklistItemStatesList);
        UpdateSignOffButtonState();
    }

    private void OnPdfButtonClicked(ClickEvent evt)
    {
        _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center);
        if (_currentProtocol?.protocolPDFNames?.Count > 0)
        {
            _uiDriver?.DisplayPDFReader(_currentProtocol.protocolPDFNames[0]);
        }
        else
        {
            Debug.LogWarning("[ProtocolViewWindowController] No PDFs available for the current protocol.");
        }
    }

    private void UpdateSignOffButtonState()
    {
        if (_signOffActionButton == null) return;

        bool allItemsEffectivelyChecked;
        var currentStepState = ProtocolState.Instance.CurrentStepState?.Value;

        if (currentStepState == null || currentStepState.Checklist == null || !currentStepState.Checklist.Any())
        {
            allItemsEffectivelyChecked = true;
        }
        else
        {
            allItemsEffectivelyChecked = currentStepState.Checklist.All(item => item.IsChecked.Value);
        }
        
        if (_isSignedOff)
        {
            _signOffActionButton.SetEnabled(false);
        }
        else
        {
            _signOffActionButton.SetEnabled(allItemsEffectivelyChecked);
        }
    }

    private void OnSignOffButtonClicked(ClickEvent evt)
    {
        _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center);
        if (_isSignedOff) return;

        var checklist = ProtocolState.Instance.CurrentStepState?.Value?.Checklist;
        if (checklist == null || !checklist.All(item => item.IsChecked.Value))
        {
            Debug.LogWarning("[ProtocolViewWindowController] Sign Off clicked but not all items are checked.");
            return;
        }

        string userName = SessionState.currentUserProfile?.GetName() ?? "User";
        _signatureLineLabel.text = userName;
        _signatureLineLabel.AddToClassList("signed-text");

        _isSignedOff = true;
        _uiDriver.SignOffChecklistCallback();
        UpdateSignOffButtonState();
    }

    private void ResetSignatureLine()
    {
        if (_signatureLineLabel != null)
        {
            _signatureLineLabel.text = "";
            _signatureLineLabel.RemoveFromClassList("signed-text");
        }
    }
    
    private void ResetStepUIData(string stepIndicatorText = "Step - / -")
    {
        if (_stepIndicatorLabel != null) _stepIndicatorLabel.text = stepIndicatorText;
        if (_checklistStepTitleLabel != null) _checklistStepTitleLabel.text = "";
        if (_checklistContainer != null) _checklistContainer.Clear();
        if (_protocolContentContainer != null) _protocolContentContainer.Clear();
        ResetSignatureLine();
    }
} 