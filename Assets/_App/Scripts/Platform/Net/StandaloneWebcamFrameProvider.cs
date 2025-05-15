/*
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class StandaloneWebCamFrameProvider : IFrameProvider
{
    private WebCamTexture _webCamTexture;
    private Texture2D _tmpTexture;
    private RenderTexture _renderTexture;
    public string DeviceName { get; set; } = "";

    public bool IsAvailable => WebCamTexture.devices.Length > 0;

    public bool IsFlippedHorizontally => false; 

    public bool IsFlippedVertically => false;

    public (int width, int height) FrameSize 
    { 
        get 
        {
            return (_width, _height);
        }
    }

    public event Action<Texture2D> OnFrameReceived;

    public void RequestPermissions() {}

    private int _width = 1920;
    private int _height = 1080;

    private SynchronizationContext _unitySyncContext;
    private bool _isRunning = false;

    public StandaloneWebCamFrameProvider()
    {
        _unitySyncContext = SynchronizationContext.Current;
    }

    public bool Initialize(int width, int height)
    {
        _width = width;
        _height = height;

        if (!IsAvailable) return false;

        _webCamTexture = new WebCamTexture(DeviceName, width, height);

        return true;
    }

    public void Start()
    {
        _webCamTexture?.Play();
        _isRunning = true;
        RunUpdateLoop();
    }

    public void Stop()
    {
        _isRunning = false;
        _webCamTexture?.Stop();
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
        if (_webCamTexture != null && _webCamTexture.didUpdateThisFrame)
        {
            if (_renderTexture == null)
            {
                _renderTexture = new RenderTexture(_webCamTexture.width, _webCamTexture.height, 1, RenderTextureFormat.ARGB32);
            }

            if (_tmpTexture == null)
            {
                _tmpTexture = new Texture2D(_webCamTexture.width, _webCamTexture.height);
            }

            // Copy the WebCamTexture to the RenderTexture using Graphics.Blit()
            Graphics.Blit(_webCamTexture, _renderTexture);

            // Read the RenderTexture into the Texture2D
            RenderTexture.active = _renderTexture;
            _tmpTexture.ReadPixels(new Rect(0, 0, _renderTexture.width, _renderTexture.height), 0, 0);
            _tmpTexture.Apply();
            RenderTexture.active = null;

            OnFrameReceived?.Invoke(_tmpTexture);
        }
    }
}
*/
