using System;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace LabLight
{
    public class ImageComponent : VisualElement
    {
        private readonly Image _imageElement;

        public ImageComponent(VisualTreeAsset asset)
        {
            asset.CloneTree(this);
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
