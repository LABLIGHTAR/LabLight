using UnityEngine;
using System;
using System.Runtime.InteropServices;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.UI;

/// <summary>
/// VisionOS specific frame provider that captures frames from the camera
/// </summary>
public class VisionOSFrameProvider : IFrameProvider
{
    public bool IsAvailable => true;

    public bool IsFlippedHorizontally => false; 

    public bool IsFlippedVertically => true;

    public (int width, int height) FrameSize 
    {
        get => (_width, _height);
    }

    public event Action<Texture2D> OnFrameReceived;

    private Texture2D _texture;
    private IntPtr _texturePtr;

    private int _width = 1920;
    private int _height = 1080;

    private UInt64 lastUpdateCount = 0;

    private SynchronizationContext _unitySyncContext;
    private bool _isRunning = false;

    public VisionOSFrameProvider()
    {
        _unitySyncContext = SynchronizationContext.Current;
    }

    public void RequestPermissions()
    {
        
    }

    public bool Initialize(int width, int height)
    {
        EnhancedTouchSupport.Enable();
        
        return true;
    }

    public void Start()
    {
        startCapture();
        _isRunning = true;
        RunUpdateLoop();
    }

    public void Stop()
    {
        _isRunning = false;
        stopCapture();
    }

    private async void RunUpdateLoop()
    {
        while (_isRunning)
        {
            await Task.Yield(); // Yield to the next frame.
            _unitySyncContext.Post((_) => ProcessUpdate(), null);
        }
    }

    public void ProcessUpdate()
    {
        if (_texture == null)
        {
            IntPtr texturePtr = getTexture();
            if (texturePtr == IntPtr.Zero) return;

            _texturePtr = texturePtr;

            // Create a texture to update the camera image.
            _texture = Texture2D.CreateExternalTexture(_width, _height, TextureFormat.BGRA32, false, false, _texturePtr);
            _texture.UpdateExternalTexture(_texturePtr);
        }

        UInt64 currentUpdateCount = getUpdateCount();

        if (_texture != null && currentUpdateCount != lastUpdateCount)
        {
            lastUpdateCount = currentUpdateCount;
            OnFrameReceived?.Invoke(_texture);
        }
    }

#if UNITY_VISIONOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void startCapture();

    [DllImport("__Internal")]
    private static extern void stopCapture();

    [DllImport("__Internal")]
    private static extern IntPtr getTexture();

    [DllImport("__Internal")]
    private static extern UInt64 getUpdateCount();
#else
    private static void startCapture() {}

    private static void stopCapture() {}

    private static IntPtr getTexture() { return IntPtr.Zero; }

    private static UInt64 getUpdateCount() { return 0; }
#endif
}

