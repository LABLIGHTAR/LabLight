﻿using System;
using UnityEngine;
using UnityEngine.Video;
using UniRx;
using UnityEngine.UI;

/// <summary>
/// Video content item
/// </summary>
public class VideoController : ContentController<VideoItem>
{
    public VideoPlayer video;
    public RawImage videoTargetImage;
    public GameObject loadingIndicator;
    public IDisposable downloadSubscription;

    public override VideoItem ContentItem
    {
        get => base.ContentItem;
        set
        {
            base.ContentItem = value;
            UpdateView();
        }
    }

    private void UpdateView()
    {
        var videoPath = ProtocolState.Instance.ActiveProtocol.Value.mediaBasePath + "/" + ContentItem.url;

        // Cancel previous download
        downloadSubscription?.Dispose();
        downloadSubscription = null;

        // Show loading indicator
        videoTargetImage.enabled = false;
        loadingIndicator.SetActive(true);

        // Start new download
        downloadSubscription = ServiceRegistry.GetService<IMediaProvider>().GetVideo(videoPath).Subscribe(clip =>
        {
            if (clip == null)
            {
                return;
            }

            video.clip = clip;
            videoTargetImage.enabled = true;
            loadingIndicator.SetActive(false);
            video.targetTexture = new RenderTexture((int)clip.width, (int)clip.height, 1);
            videoTargetImage.texture = video.targetTexture;

            var fitter = this.GetComponent<AspectRatioFitter>();
            if (fitter != null)
            {
                var ratio = (float)clip.width / (float)clip.height;
                fitter.aspectRatio = ratio;
            }
        }, (e) =>
        {
            ServiceRegistry.Logger.LogError("Could not load video " + videoPath + ". " + e.ToString());
        });
    }
}