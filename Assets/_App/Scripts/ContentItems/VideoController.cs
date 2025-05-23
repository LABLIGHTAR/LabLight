using System;
using UnityEngine;
using UnityEngine.Video;
using UniRx;
using UnityEngine.UI;
using System.IO; // Added for Path and File operations

/// <summary>
/// Video content item
/// </summary>
public class VideoController : ContentController<ContentItem>
{
    public VideoPlayer videoPlayer; // Renamed from video for clarity
    public RawImage videoTargetImage;
    public GameObject loadingIndicator;
    public IDisposable prepareSubscription; // Renamed from downloadSubscription
    private IFileManager fileManager;
    private string tempVideoFilePath = null; // To store path for cleanup

    public override ContentItem ContentItem
    {
        get => base.ContentItem;
        set
        {
            base.ContentItem = value;
            UpdateView();
        }
    }

    // Added OnDisable for subscription cleanup and resource release
    private void OnDisable()
    {
        prepareSubscription?.Dispose();
        prepareSubscription = null;
        if (videoPlayer != null)
        {
            if (videoPlayer.isPlaying) videoPlayer.Stop();
            if (videoPlayer.targetTexture != null) videoPlayer.targetTexture.Release();
        }
        CleanUpTempFile();
    }

    private void CleanUpTempFile()
    {
        if (!string.IsNullOrEmpty(tempVideoFilePath) && File.Exists(tempVideoFilePath))
        {
            try { File.Delete(tempVideoFilePath); tempVideoFilePath = null; }
            catch (Exception ex) { Debug.LogWarning($"VideoController: Failed to delete temp video file '{tempVideoFilePath}'. {ex.Message}"); }
        }
    }

    private async void UpdateView()
    {
        CleanUpTempFile(); // Clean up any previous temp file first

        if (ContentItem == null || !ContentItem.properties.TryGetValue("URL", out object urlValue)) // Case-sensitive "URL"
        {
            Debug.LogError("VideoController: No URL/objectKey found in properties");
            loadingIndicator.SetActive(false); videoTargetImage.enabled = false;
            return;
        }

        var objectKey = urlValue.ToString();
        if (string.IsNullOrEmpty(objectKey))
        {
            Debug.LogError("VideoController: URL/objectKey is null or empty.");
            loadingIndicator.SetActive(false); videoTargetImage.enabled = false;
            return;
        }

        prepareSubscription?.Dispose();
        prepareSubscription = null;
        
        videoTargetImage.enabled = false;
        loadingIndicator.SetActive(true);

        if (fileManager == null)
        {
            fileManager = ServiceRegistry.GetService<IFileManager>();
            if (fileManager == null)
            {
                Debug.LogError("VideoController: IFileManager service not found!");
                loadingIndicator.SetActive(false);
                return;
            }
        }

        var fileResult = await fileManager.GetMediaFileAsync(objectKey);

        if (!fileResult.Success || fileResult.Data == null || fileResult.Data.Length == 0)
        {
            Debug.LogError($"VideoController: Failed to get video data for '{objectKey}' from FileManager. Error: {fileResult.Error?.Code} - {fileResult.Error?.Message}");
            loadingIndicator.SetActive(false);
            return;
        }

        try
        {            
            string safeFileName = Path.GetFileNameWithoutExtension(objectKey).Replace(" ", "_") + Path.GetExtension(objectKey);
            // Common video extensions, ensure there's one. Default to .mp4 if none.
            if (string.IsNullOrEmpty(Path.GetExtension(safeFileName))) safeFileName += ".mp4"; 
            tempVideoFilePath = Path.Combine(Application.temporaryCachePath, safeFileName);
            
            await File.WriteAllBytesAsync(tempVideoFilePath, fileResult.Data);

            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = "file://" + tempVideoFilePath;
            videoPlayer.Prepare();

            // Subscribe to prepareCompleted event using UniRx
            prepareSubscription = videoPlayer.ObserveEveryValueChanged(vp => vp.isPrepared)
                .Where(isPrepared => isPrepared)
                .Take(1) // We only need the first true event
                .Subscribe(_ => 
                {
                    videoTargetImage.enabled = true;
                    loadingIndicator.SetActive(false);
                    
                    // Release previous render texture if any before creating a new one
                    if (videoPlayer.targetTexture != null) videoPlayer.targetTexture.Release();
                    videoPlayer.targetTexture = new RenderTexture((int)videoPlayer.width, (int)videoPlayer.height, 16, RenderTextureFormat.ARGB32);
                    videoTargetImage.texture = videoPlayer.targetTexture;

                    var fitter = this.GetComponent<AspectRatioFitter>();
                    if (fitter != null)
                    {
                        var ratio = (float)videoPlayer.width / (float)videoPlayer.height;
                        fitter.aspectRatio = ratio;
                    }
                    videoPlayer.Play(); // Play after setup
                }, ex => 
                {
                    Debug.LogError($"VideoController: Error during video preparation or setup for '{objectKey}'. {ex.Message}");
                    loadingIndicator.SetActive(false);
                    CleanUpTempFile(); // Clean up if preparation fails
                })
                .AddTo(this); // Manage subscription lifecycle
        }
        catch (Exception ex)
        {
            Debug.LogError($"VideoController: Exception writing temp video file or starting preparation for '{objectKey}'. {ex.Message}");
            loadingIndicator.SetActive(false);
            CleanUpTempFile();
        }
    }
}