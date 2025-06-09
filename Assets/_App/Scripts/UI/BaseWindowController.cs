using UnityEngine;
using UnityEngine.UIElements;

public class BaseWindowController : MonoBehaviour
{
    [Header("Window Settings")]
    [SerializeField]
    private Vector2Int windowSize = new Vector2Int(800, 600);

    protected VisualElement rootVisualElement;
    protected UIDocument uiDocument;

    protected virtual void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            Debug.LogError("UIDocument not found on this GameObject.", this);
            return;
        }
    }

    protected virtual void OnEnable()
    {
        rootVisualElement = uiDocument.rootVisualElement;
        if (rootVisualElement != null)
        {
            ApplyWindowSize();
        }
    }

    protected virtual void OnDisable()
    {
        // This method is intended to be overridden by derived classes.
    }

    protected virtual void ApplyWindowSize()
    {
        if (rootVisualElement != null)
        {
            var windowPanel = rootVisualElement.Q<VisualElement>("glassmorphic-panel") ?? (rootVisualElement.childCount > 0 ? rootVisualElement.ElementAt(0) : rootVisualElement);
            
            if(windowPanel != null)
            {
                windowPanel.style.width = windowSize.x;
                windowPanel.style.height = windowSize.y;
                windowPanel.style.minWidth = windowSize.x;
                windowPanel.style.maxWidth = windowSize.x;
                windowPanel.style.minHeight = windowSize.y;
                windowPanel.style.maxHeight = windowSize.y;
            }
        }
    }

    protected virtual void SwapComponent(VisualElement container, VisualElement component)
    {
        if (container == null)
        {
            Debug.LogError("Provided container is null. Cannot swap components.", this);
            return;
        }
        if (component == null)
        {
            Debug.LogError("Provided component is null. Cannot swap components.", this);
            return;
        }

        container.Clear();
        container.Add(component);
    }
    
    public virtual void Show()
    {
        if (rootVisualElement != null)
        {
            rootVisualElement.style.display = DisplayStyle.Flex;
        }
    }

    public virtual void Hide()
    {
        if (rootVisualElement != null)
        {
            rootVisualElement.style.display = DisplayStyle.None;
        }
    }
} 