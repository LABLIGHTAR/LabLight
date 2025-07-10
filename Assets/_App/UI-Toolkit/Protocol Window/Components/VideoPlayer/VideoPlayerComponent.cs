using System;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace LabLight
{
    public class VideoPlayerComponent : VisualElement
    {
        private readonly VideoPlayer _videoPlayer;
        private readonly Image _videoImage;
        private readonly VisualElement _playPauseButton;
        private readonly Slider _progressBar;
        private readonly VisualElement _videoContainer;
        
        private readonly CompositeDisposable _videoDisposables = new CompositeDisposable();
        private bool _isPreparing = false;

        public VideoPlayerComponent(VisualTreeAsset asset)
        {
            asset.CloneTree(this);
            
            _videoContainer = this.Q<VisualElement>("video-container");
            _videoImage = this.Q<Image>("video-image");
            _playPauseButton = this.Q<VisualElement>("play-pause-button");
            _progressBar = this.Q<Slider>("progress-bar");
            
            _playPauseButton.SetEnabled(false);
            _progressBar.SetEnabled(false);
            
            _playPauseButton.RegisterCallback<ClickEvent>(OnPlayPauseClicked);
            _progressBar.RegisterValueChangedCallback(OnScrubberChanged);
            
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            
            var videoPlayerGameObject = new GameObject($"ProtocolStepVideoPlayer_{System.Guid.NewGuid()}");
            _videoPlayer = videoPlayerGameObject.AddComponent<VideoPlayer>();
            _videoPlayer.playOnAwake = false;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayer.source = VideoSource.Url;
            
            var renderTexture = new RenderTexture(1280, 720, 16, RenderTextureFormat.ARGB32);
            _videoPlayer.targetTexture = renderTexture;
            _videoImage.image = renderTexture;
            
            _videoPlayer.prepareCompleted += OnVideoPrepared;
            _videoPlayer.loopPointReached += OnVideoLoopPointReached;
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            // The component is now attached to a panel. If the URL is set and we're not already preparing, start now.
            if (!_isPreparing && !string.IsNullOrEmpty(_videoPlayer.url))
            {
                _isPreparing = true;
                _videoPlayer.Prepare();
            }
        }
        
        public async void SetVideo(IFileManager fileManager, string videoObjectKey)
        {
            if (fileManager == null)
            {
                Debug.LogError("[VideoPlayerComponent] FileManager is not initialized.");
                return;
            }
            if (string.IsNullOrEmpty(videoObjectKey))
            {
                Debug.LogWarning("[VideoPlayerComponent] Video object key is null or empty.");
                return;
            }

            var result = await fileManager.GetMediaFilePathAsync(videoObjectKey);

            if (result.Success && !string.IsNullOrEmpty(result.Data))
            {
                _videoPlayer.url = new System.Uri(result.Data).AbsoluteUri;

                // If the component is already attached to a panel, the AttachToPanelEvent won't fire again.
                // In this case, we need to start preparation manually.
                if (panel != null && !_isPreparing)
                {
                    _isPreparing = true;
                    _videoPlayer.Prepare();
                }
            }
            else
            {
                Debug.LogError($"[VideoPlayerComponent] Failed to get video path for {videoObjectKey}: {result.Error.Message}");
            }
        }

        private void OnPlayPauseClicked(ClickEvent evt)
        {
            if (_videoPlayer.isPlaying)
            {
                _videoPlayer.Pause();
                _playPauseButton.RemoveFromClassList("icon-pause");
                _playPauseButton.AddToClassList("icon-play");
            }
            else
            {
                _videoPlayer.Play();
                _playPauseButton.RemoveFromClassList("icon-play");
                _playPauseButton.AddToClassList("icon-pause");
            }
        }
        
        private void OnScrubberChanged(ChangeEvent<float> evt)
        {
            if (_videoPlayer.isPrepared)
            {
                _videoPlayer.time = evt.newValue;
            }
        }

        private void OnVideoPrepared(VideoPlayer source)
        {
            _isPreparing = false;
            // Schedule the UI updates to ensure they happen when the element is fully processed
            // by the UI Toolkit panel. This is a robust way to avoid race conditions.
            this.schedule.Execute(() =>
            {
                // Check if the component is still attached to a panel before updating UI
                if (this.panel == null || source == null) return;

                // Calculate the display width based on the max-height and aspect ratio
                float videoWidth = source.width;
                float videoHeight = source.height;
                const float maxHeight = 400f; // From the USS file
                
                if (videoHeight > maxHeight)
                {
                    float aspectRatio = videoWidth / videoHeight;
                    float displayWidth = maxHeight * aspectRatio;
                    _videoContainer.style.width = displayWidth;
                }
                else
                {
                    _videoContainer.style.width = videoWidth;
                }

                _progressBar.highValue = (float)source.length;
                _progressBar.SetEnabled(true);
                _playPauseButton.SetEnabled(true);

                // Force the first frame to display
                source.Play();
                source.Pause();
            });

            // Set up the progress bar updater.
            Observable.EveryUpdate()
                .Where(_ => _videoPlayer != null && _videoPlayer.isPlaying)
                .Subscribe(_ => {
                    if (_videoPlayer.length > 0)
                    {
                        _progressBar.SetValueWithoutNotify((float)_videoPlayer.time);
                    }
                }).AddTo(_videoDisposables);
        }
        
        private void OnVideoLoopPointReached(VideoPlayer source)
        {
            _videoPlayer.time = 0;
            _videoPlayer.Pause();
            _playPauseButton.RemoveFromClassList("icon-pause");
            _playPauseButton.AddToClassList("icon-play");
        }
        
        public void CleanUp()
        {
            UnregisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            _videoDisposables.Clear();
            if (_videoPlayer != null)
            {
                _videoPlayer.prepareCompleted -= OnVideoPrepared;
                _videoPlayer.loopPointReached -= OnVideoLoopPointReached;
                
                if (_videoPlayer.targetTexture != null)
                {
                    _videoPlayer.targetTexture.Release();
                }
                UnityEngine.Object.Destroy(_videoPlayer.gameObject);
            }
        }
    }
} 