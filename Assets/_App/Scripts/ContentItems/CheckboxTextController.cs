using TMPro;
using System;

/// <summary>
/// Text contentitem
/// </summary>
public class CheckboxTextController : ContentController<ContentItem>
{
    public TextMeshProUGUI Text;

    public override ContentItem ContentItem
    {
        get => base.ContentItem;
        set
        {
            base.ContentItem = value;
            UpdateView();
        }
    }

    private void UpdateView()
    {
        // Get text from properties dictionary with fallback to empty string
        Text.text = ContentItem.properties.TryGetValue("text", out object textValue) 
            ? textValue.ToString() 
            : string.Empty;

        // Get text type from properties with fallback to normal text
        bool isHeader = ContentItem.properties.TryGetValue("textType", out object typeValue) 
            && typeValue.ToString().Equals("Header", StringComparison.OrdinalIgnoreCase);
        Text.fontSize = isHeader ? 8 : 6;
    }
}
