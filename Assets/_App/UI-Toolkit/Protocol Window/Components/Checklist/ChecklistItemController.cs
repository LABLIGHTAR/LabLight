using UnityEngine.UIElements;

public class ChecklistItemController : VisualElement
{
    public new class UxmlFactory : UxmlFactory<ChecklistItemController, UxmlTraits> { }
    public new class UxmlTraits : VisualElement.UxmlTraits { }

    private Label _textLabel;
    private VisualElement _statusIndicator;

    public ChecklistItemController()
    {
        RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
    }

    private void OnAttachToPanel(AttachToPanelEvent evt)
    {
        _textLabel = this.Q<Label>("check-item-text");
        _statusIndicator = this.Q<VisualElement>("status-indicator");
    }

    public void SetData(string text, bool isChecked, bool isNext, bool isLocked)
    {
        if (_textLabel == null)
        {
            OnAttachToPanel(null); // Ensure elements are queried
        }

        _textLabel.text = text;

        _statusIndicator.EnableInClassList("icon-check-circle", isChecked);
        _statusIndicator.EnableInClassList("icon-radio-unchecked", !isChecked);

        this.EnableInClassList("check-item-next", isNext);
        this.EnableInClassList("check-item-locked", isLocked && !isNext);
    }
} 