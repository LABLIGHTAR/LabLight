using UnityEngine;
using UnityEngine.UI;
using UniRx;
using System;
using System.Threading.Tasks;

/// <summary>
/// Image content item 
/// </summary>
public class ImageController : ContentController<ContentItem>
{
    public Image Image;
    private IDisposable downloadSubscription;
    private IFileManager fileManager;

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

    private async void UpdateView()
    {
        if (ContentItem == null || !ContentItem.properties.TryGetValue("url", out object urlValue))
        {
            Debug.LogError("ImageController: No URL/objectKey found in properties");
            return;
        }

        var objectKey = urlValue.ToString();
        if (string.IsNullOrEmpty(objectKey))
        {
            Debug.LogError("ImageController: URL/objectKey is null or empty.");
            return;
        }

        downloadSubscription?.Dispose();
        downloadSubscription = null;
        Image.enabled = false;

        if (fileManager == null)
        {
            fileManager = ServiceRegistry.GetService<IFileManager>();
            if (fileManager == null)
            {
                Debug.LogError("ImageController: IFileManager service not found!");
                return;
            }
        }

        // Start new download using FileManager
        // Convert Task<Result<byte[]>> to IObservable<Result<byte[]>> for UniRx subscription
        downloadSubscription = fileManager.GetMediaFileAsync(objectKey)
            .ToObservable()
            .ObserveOnMainThread() // Ensure UI and texture operations are on the main thread
            .Subscribe(result =>
            {
                if (result.Success && result.Data != null && result.Data.Length > 0)
                {
                    // Convert byte[] to Texture2D, then to Sprite
                    Texture2D texture = new Texture2D(2, 2); // Dimensions will be overwritten by LoadImage
                    if (texture.LoadImage(result.Data)) // LoadImage auto-resizes the texture
                    {
                        Sprite newSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                        Image.sprite = newSprite;
                        Image.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, transform.parent.GetComponent<RectTransform>().rect.width);
                        Image.enabled = true;

                        var fitter = this.GetComponent<AspectRatioFitter>();
                        if (fitter != null)
                        {
                            var ratio = (float)texture.width / (float)texture.height;
                            fitter.aspectRatio = ratio;
                        }
                    }
                    else
                    {
                        Debug.LogError("ImageController: Failed to load image data into texture for " + objectKey);
                    }
                }
                else
                {
                    Debug.LogError($"ImageController: Could not load image '{objectKey}'. Error: {result.Error?.Code} - {result.Error?.Message}");
                }
            }, ex =>
            {
                Debug.LogError($"ImageController: Exception while loading image '{objectKey}'. {ex.ToString()}");
            })
            .AddTo(this); // Add to manage subscription lifecycle
    }
}