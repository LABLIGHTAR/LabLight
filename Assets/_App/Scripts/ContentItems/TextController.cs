using TMPro;

/// <summary>
/// Text contentitem
/// </summary>
public class TextController : ContentController<ContentItem>
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
        if (ContentItem.properties.TryGetValue("text", out object text))
        {
            Text.text = text.ToString().Replace("\r", "");
        }

        if (ContentItem.properties.TryGetValue("textType", out object textType))
        {
            Text.fontSize = textType.ToString().ToLower() == "header" ? 0.4f : 0.2f;
        }
    }
}
