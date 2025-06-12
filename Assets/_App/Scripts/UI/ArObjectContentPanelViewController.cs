using UnityEngine;
using UniRx;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

public class ArObjectContentPanelViewController : MonoBehaviour
{
    [SerializeField] private Transform contentFrame;
    [SerializeField] private TextController textPrefab;
    [SerializeField] private ImageController imagePrefab;
    [SerializeField] private SoundController soundPrefab;

    private ArObject associatedArObject;
    private List<ContentItem> currentContentItems = new List<ContentItem>();
    private List<MonoBehaviour> contentItemInstances = new List<MonoBehaviour>();
    private IUIDriver uiDriver;
    [SerializeField] private TextMeshProUGUI titleText;

    private void Awake()
    {
        uiDriver = ServiceRegistry.GetService<IUIDriver>();
        InitializeSubscriptions();
    }

    private void InitializeSubscriptions()
    {
        if (ProtocolState.Instance == null)
        {
            Debug.LogError("[ArObjectContentPanelViewController] ProtocolState.Instance is null");
            return;
        }

        // Subscribe to step changes
        ProtocolState.Instance.StepStream
            .Subscribe(_ => UpdateContentPanel())
            .AddTo(this);

        // Subscribe to checklist item changes
        ProtocolState.Instance.ChecklistStream
            .Subscribe(_ => UpdateContentPanel())
            .AddTo(this);
    }

    private void UpdateContentPanel()
    {
        if (associatedArObject == null || string.IsNullOrEmpty(associatedArObject.arObjectID))
        {
            Debug.Log($"[ArObjectContentPanelViewController] No valid AR object. ID: {associatedArObject?.arObjectID} Attempting to get it from parent");
            associatedArObject = GetComponentInParent<ArObjectViewController>()?.arObject;
            if (associatedArObject == null || string.IsNullOrEmpty(associatedArObject.arObjectID))
            {
                Debug.LogError("[ArObjectContentPanelViewController] No valid AR object found after attempting to get it from parent");
                return;
            }
            titleText.text = associatedArObject.specificObjectName;
        }

        // Get current step content items
        var stepContentItems = ProtocolState.Instance.CurrentStepDefinition?.contentItems ?? new List<ContentItem>();

        // Get current checklist item content items
        var checklistContentItems = ProtocolState.Instance.CurrentCheckItemDefinition?.contentItems ?? new List<ContentItem>();

        // Combine and filter content items
        var relevantContentItems = stepContentItems
            .Concat(checklistContentItems)
            .Where(item => 
                !string.IsNullOrEmpty(item.arObjectID) && 
                item.arObjectID.Equals(associatedArObject.arObjectID))
            .ToList();

        // If no relevant content items, hide the panel
        if (!relevantContentItems.Any())
        {
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
            }
            ClearContent();
            return;
        }

        // If content hasn't changed, do nothing
        if (currentContentItems != null && currentContentItems.SequenceEqual(relevantContentItems))
        {
            return;
        }

        // Clear existing content and create new content items
        ClearContent();
        foreach (var contentItem in relevantContentItems)
        {
            CreateContentItem(contentItem, contentFrame.GetComponent<LayoutGroup>());
        }
        currentContentItems = relevantContentItems;

        // Show the panel
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(true);
        }

        // Reset the content position to top
        if (contentFrame != null)
        {
            contentFrame.localPosition = Vector3.zero;
        }
    }

    private void CreateContentItem(ContentItem contentItem, LayoutGroup container)
    {
        MonoBehaviour controller = null;

        switch (contentItem.contentType.ToLower())
        {
            case "text":
                var textController = Instantiate(textPrefab, container.transform);
                SetupRectTransform(textController.GetComponent<RectTransform>());
                textController.ContentItem = contentItem;
                controller = textController;
                break;

            case "image":
                var imageController = Instantiate(imagePrefab, container.transform);
                SetupRectTransform(imageController.GetComponent<RectTransform>());
                imageController.ContentItem = contentItem;
                controller = imageController;
                break;

            case "sound":
                var soundController = Instantiate(soundPrefab, container.transform);
                soundController.ContentItem = contentItem;
                controller = soundController;
                break;

            case "video":
                if (contentItem.properties.TryGetValue("URL", out object videoURL))
                {
                    uiDriver.DisplayVideoPlayer(videoURL.ToString());
                }
                break;

            case "weburl":
                if (contentItem.properties.TryGetValue("url", out object webURL))
                {
                    uiDriver.DisplayWebPage(webURL.ToString());
                }
                break;
            case "timer":
                if (contentItem.properties.TryGetValue("durationInSeconds", out object durationObj) && 
                    int.TryParse(durationObj.ToString(), out int duration))
                {
                    uiDriver.DisplayTimer(duration);
                }
                break;
            case "pdf":
                if (contentItem.properties.TryGetValue("url", out object pdfURL))
                {
                    uiDriver.DisplayPDFReader(pdfURL.ToString());
                }
                break;
            default:
                Debug.LogWarning($"[ArObjectContentPanelViewController] Unsupported content type: {contentItem.contentType}");
                break;
        }

        if (controller != null)
        {
            contentItemInstances.Add(controller);
        }
    }

    private void SetupRectTransform(RectTransform rect)
    {
        if (rect == null) return;

        // Set anchors to stretch horizontally
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(0.5f, 0);
        
        // Set a smaller default height
        float preferredHeight = 0.04f;
        
        var layoutElement = rect.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = rect.gameObject.AddComponent<LayoutElement>();
        }
        layoutElement.minHeight = preferredHeight;
        layoutElement.preferredHeight = preferredHeight;
        
        // Reset position
        rect.anchoredPosition = new Vector2(0, 0);
        
        // Set size delta (only height needs to be set since width is controlled by anchors)
        rect.sizeDelta = new Vector2(0, preferredHeight);
    }

    private void ClearContent()
    {
        foreach (var contentItem in contentItemInstances)
        {
            if (contentItem != null && contentItem.gameObject != null)
            {
                Destroy(contentItem.gameObject);
            }
        }
        contentItemInstances.Clear();
        currentContentItems.Clear();
    }

    private void OnDestroy()
    {
        ClearContent();
    }
}
