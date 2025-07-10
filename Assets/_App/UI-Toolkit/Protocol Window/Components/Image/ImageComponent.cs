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
        private readonly Label _captionLabel;
        private EventCallback<GeometryChangedEvent> _geometryChangedCallback;

        public ImageComponent(VisualTreeAsset asset)
        {
            asset.CloneTree(this);
            _imageContainer = this.Q<VisualElement>("image-container");
            _imageElement = this.Q<Image>("image-display");
            _captionLabel = this.Q<Label>("caption-label");
        }

        public async void SetImage(IFileManager fileManager, string imageObjectKey, string captionText = null)
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

            if (!string.IsNullOrEmpty(captionText))
            {
                _captionLabel.text = captionText;
                _captionLabel.RemoveFromClassList("hidden");
            }

            try
            {
                var result = await fileManager.GetMediaFilePathAsync(imageObjectKey);
                if (result.Success && !string.IsNullOrEmpty(result.Data) && File.Exists(result.Data))
                {
                    byte[] imageData = await File.ReadAllBytesAsync(result.Data);
                    var texture = new Texture2D(2, 2);
                    texture.LoadImage(imageData);

                    // Defer size calculation until the element has a resolved style
                    _geometryChangedCallback = (evt) => OnImageGeometryChanged(evt, texture);
                    _imageElement.RegisterCallback<GeometryChangedEvent>(_geometryChangedCallback);
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

        private void OnImageGeometryChanged(GeometryChangedEvent evt, Texture2D texture)
        {
            if (texture == null) return;
            
            // Get the computed max-height from the stylesheet
            float maxHeight = _imageElement.resolvedStyle.maxHeight.value;
            if (maxHeight <= 0) return;

            float imageWidth = texture.width;
            float imageHeight = texture.height;
            
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
            
            // Unregister the callback to avoid it running again on layout changes
            if (_geometryChangedCallback != null)
            {
                _imageElement.UnregisterCallback<GeometryChangedEvent>(_geometryChangedCallback);
            }
        }
    }
}
