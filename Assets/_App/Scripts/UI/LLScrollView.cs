using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LLScrollView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("References")]
    [SerializeField] private RectTransform contentRectTransform;
    
    [Header("Scroll Settings")]
    [SerializeField] private float scrollSpeed = 0.01f;
    [SerializeField] private float smoothTime = 0.2f;
    [SerializeField] private bool enableVerticalScroll = true;
    [SerializeField] private bool enableHorizontalScroll = false;
    
    [Header("Layout Settings")]
    [SerializeField] private float contentSpacing = 5f;
    
    private Vector2 dragStartPosition;
    private Vector2 contentStartPosition;
    private Vector2 currentVelocity;
    private bool isDragging;
    private RectTransform viewportRectTransform;
    private LayoutGroup contentLayoutGroup;
    private ContentSizeFitter contentSizeFitter;
    private bool isPointerDown;

    private void Awake()
    {
        InitializeComponents();
        ConfigureLayoutGroup();
        ConfigureContentSizeFitter();
        ConfigureContentTransform();
        EnsureRaycasterSetup();
    }

    private void InitializeComponents()
    {
        viewportRectTransform = GetComponent<RectTransform>();
        EnsureRequiredComponents();
    }

    private void EnsureRequiredComponents()
    {
        var canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        
        var mask = GetComponent<Mask>() ?? gameObject.AddComponent<Mask>();
        
        if (!GetComponent<Image>())
        {
            var image = gameObject.AddComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = true;
        }
    }

    private void ConfigureLayoutGroup()
    {
        contentLayoutGroup = contentRectTransform.GetComponent<LayoutGroup>();
        if (contentLayoutGroup == null)
        {
            contentLayoutGroup = contentRectTransform.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        if (contentLayoutGroup is VerticalLayoutGroup verticalLayout)
        {
            verticalLayout.childAlignment = TextAnchor.UpperCenter;
            verticalLayout.spacing = contentSpacing;
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = false;
            verticalLayout.childForceExpandWidth = true;
            verticalLayout.childForceExpandHeight = false;
        }
    }

    private void ConfigureContentSizeFitter()
    {
        contentSizeFitter = contentRectTransform.GetComponent<ContentSizeFitter>();
        if (contentSizeFitter == null)
        {
            contentSizeFitter = contentRectTransform.gameObject.AddComponent<ContentSizeFitter>();
        }
        
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    private void ConfigureContentTransform()
    {
        contentRectTransform.anchorMin = new Vector2(0, 1);
        contentRectTransform.anchorMax = new Vector2(1, 1);
        contentRectTransform.pivot = new Vector2(0.5f, 1f);
        contentRectTransform.sizeDelta = new Vector2(0, 0);
    }

    private void EnsureRaycasterSetup()
    {
        // Get the Canvas component
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[LLScrollView] No Canvas found in parents!");
            return;
        }

        // Ensure GraphicRaycaster exists and is configured
        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        // Configure raycaster settings
        raycaster.ignoreReversedGraphics = true;
        raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;

        // Ensure it stays enabled
        raycaster.enabled = true;
    }

    private void OnEnable()
    {
        // Double-check raycaster when object is enabled
        EnsureRaycasterSetup();
    }

    private void Update()
    {
        // Periodically check if raycaster is still active
        if (Time.frameCount % 60 == 0) // Check every 60 frames
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (raycaster != null && !raycaster.enabled)
                {
                    Debug.LogWarning("[LLScrollView] GraphicRaycaster was disabled - re-enabling");
                    raycaster.enabled = true;
                }
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPointerDown = true;
        OnBeginDrag(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerDown = false;
        OnEndDrag(eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        dragStartPosition = GetInputPosition(eventData);
        contentStartPosition = contentRectTransform.anchoredPosition;
        currentVelocity = Vector2.zero;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging && !isPointerDown) return;

        Vector2 currentPosition = GetInputPosition(eventData);
        Vector2 difference = (currentPosition - dragStartPosition) * scrollSpeed;
        Vector2 targetPosition = contentStartPosition;

        if (enableVerticalScroll)
            targetPosition.y += difference.y;
        if (enableHorizontalScroll)
            targetPosition.x += difference.x;

        contentRectTransform.anchoredPosition = ClampScrollPosition(targetPosition);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
    }

    private Vector2 GetInputPosition(PointerEventData eventData)
    {
        #if UNITY_IOS || UNITY_VISIONOS
            // For visionOS/iOS, we might need to handle touch input differently
            if (eventData.pointerId >= 0) // Touch input
            {
                return eventData.position;
            }
        #endif
        
        return eventData.position;
    }

    private void LateUpdate()
    {
        if (!isDragging && !isPointerDown)
        {
            ApplySmoothScrolling();
        }
    }

    private void ApplySmoothScrolling()
    {
        Vector2 targetPosition = contentRectTransform.anchoredPosition;
        Vector2 clampedPosition = ClampScrollPosition(targetPosition);

        contentRectTransform.anchoredPosition = Vector2.SmoothDamp(
            contentRectTransform.anchoredPosition,
            clampedPosition,
            ref currentVelocity,
            smoothTime
        );
    }

    private Vector2 ClampScrollPosition(Vector2 position)
    {
        float maxY = Mathf.Max(0, contentRectTransform.rect.height - viewportRectTransform.rect.height);
        float minX = Mathf.Min(0, -(contentRectTransform.rect.width - viewportRectTransform.rect.width));
        
        return new Vector2(
            Mathf.Clamp(position.x, minX, 0),
            Mathf.Clamp(position.y, 0, maxY)
        );
    }
}