using UnityEngine;
using UnityEngine.UI;
using UniRx;
using System;

/// <summary>
/// Image content item 
/// </summary>
public class ImageController : ContentController<ContentItem>
{
    public Image Image;
    private IDisposable downloadSubscription;

    public override ContentItem ContentItem
    {
        get => base.ContentItem;
        set
        {
            base.ContentItem = value;
            UpdateView();
        }
    }

    private void OnDisable()
    {
        // Cancel previous download
        downloadSubscription?.Dispose();
        downloadSubscription = null;
    }

    private void UpdateView()
    {
        if (ContentItem == null || !ContentItem.properties.TryGetValue("url", out object urlValue)) 
        {
            Debug.LogError("ImageController: No URL found in properties");
            return;
        }

        var imagePath = urlValue.ToString();

        downloadSubscription?.Dispose();
        downloadSubscription = null;
        Image.enabled = false;

        // Start new download
        downloadSubscription = ServiceRegistry.GetService<IMediaProvider>().GetSprite(imagePath).Subscribe(sprite =>
        {
            if (sprite == null)
            {
                return;
            }

            Image.sprite = sprite;
            Image.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, transform.parent.GetComponent<RectTransform>().rect.width);
            Image.enabled = true;

            var fitter = this.GetComponent<AspectRatioFitter>();
            if (fitter != null)
            {
               var ratio = (float)Image.sprite.rect.width / (float)Image.sprite.rect.height;
               fitter.aspectRatio = ratio;
            }
        }, (e) =>
        {
            Debug.Log("######LABLIGHT Could not load image " + imagePath + ". " + e.ToString());
            //ServiceRegistry.Logger.LogError("Could not load image " + imagePath + ". " + e.ToString());
        });
    }
}