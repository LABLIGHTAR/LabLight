using System;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace LabLight
{
    public class ImageComponent : VisualElement
    {
        private readonly Image _imageElement;
        private readonly VisualElement _imageContainer;

        public ImageComponent(VisualTreeAsset asset)
        {
            asset.CloneTree(this);
            _imageContainer = this.Q<VisualElement>("image-container");
            _imageElement = this.Q<Image>("image-display");
        }

        public async void SetImage(IFileManager fileManager, string imageObjectKey)
        {
            if (fileManager == null)
            {
                Debug.LogError("[ImageComponent] FileManager is not initialized.");
                return;
            }

            if (_imageElement == null)
            {
                Debug.LogError("[ImageComponent] Image element is not found in the UXML.");
                return;
            }

            try
            {
                var result = await fileManager.GetMediaFilePathAsync(imageObjectKey);
                if (result.Success && !string.IsNullOrEmpty(result.Data) && File.Exists(result.Data))
                {
                    byte[] imageData = await File.ReadAllBytesAsync(result.Data);
                    var texture = new Texture2D(2, 2);
                    texture.LoadImage(imageData);

                    // Calculate the display width based on the max-height and aspect ratio
                    float imageWidth = texture.width;
                    float imageHeight = texture.height;
                    const float maxHeight = 400f; // From the USS file
                    
                    if (imageHeight > maxHeight)
                    {
                        float aspectRatio = imageWidth / imageHeight;
                        float displayWidth = maxHeight * aspectRatio;
                        _imageContainer.style.width = displayWidth;
                    }
                    else
                    {
                        _imageContainer.style.width = imageWidth;
                    }
                    
                    _imageElement.image = texture;
                }
                else
                {
                    Debug.LogWarning($"[ImageComponent] Image not found or path is invalid for key: {imageObjectKey}. Error: {result.Error?.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ImageComponent] Error loading image with key {imageObjectKey}: {ex}");
            }
        }
    }
}
