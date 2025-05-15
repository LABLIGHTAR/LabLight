using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class RawImageUpdater : MonoBehaviour
{
    [SerializeField] private RawImage _preview;

    // private IFrameProvider frameProvider;

    private RenderTexture _renderTexture;

    void Start()
    {
        /*
        frameProvider = ServiceRegistry.GetService<IFrameProvider>();
        frameProvider.OnFrameReceived += OnFrameReceived;

        // Create a render texture to show the camera image on a raw image.
        _renderTexture = new RenderTexture(frameProvider.FrameSize.width, frameProvider.FrameSize.height, 1, RenderTextureFormat.ARGB32);
        _renderTexture.enableRandomWrite = true;
        _renderTexture.Create();
        _preview.texture = _renderTexture;
        */        
    }

    void OnDestroy()
    {
        // frameProvider.OnFrameReceived -= OnFrameReceived;
    }

    private void OnFrameReceived(Texture2D texture)
    {
        /*
        Graphics.Blit(  texture, 
                        _renderTexture, 
                        new Vector2(frameProvider.IsFlippedHorizontally ? -1.0f: 1.0f, frameProvider.IsFlippedVertically ? -1.0f : 1.0f), 
                        new Vector2(frameProvider.IsFlippedHorizontally ? 1.0f : 0.0f, frameProvider.IsFlippedVertically ? 1.0f : 0.0f));

        Unity.PolySpatial.PolySpatialObjectUtils.MarkDirty(_renderTexture);
        */
    }
}
