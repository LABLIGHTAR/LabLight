using UnityEngine;
using UniRx;
using System;
using System.Collections.Generic;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Handles objects that use image-based tracking. When successfully tracked, 
/// locks the object and signals the appropriate channels.
/// </summary>
public class ImageTrackingObjectManager : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Image manager on the AR Session Origin")]
    ARTrackedImageManager m_ImageManager;

    ArObjectViewController m_objectToLock;
    private CompositeDisposable disposables = new CompositeDisposable();
    private bool isQuitting = false;

    public ImageTrackingEventChannel imageTrackingEventChannel;

    private void Awake()
    {
        // Verify required components
        if (m_ImageManager == null)
        {
            Debug.LogError($"[{nameof(ImageTrackingObjectManager)}] ARTrackedImageManager is required but was not set.");
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        if (m_ImageManager != null && m_ImageManager.enabled)
        {
            imageTrackingEventChannel.SetImageTrackedObject.AddListener(HandleImageTrackedObject);
            m_ImageManager.trackablesChanged.AddListener(ImageManagerOnTrackedImagesChanged);
        }
    }

    private void OnDisable()
    {
        if (!isQuitting)
        {
            CleanupResources();
        }
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
        CleanupResources();
    }

    private void OnDestroy()
    {
        CleanupResources();
    }

    private void CleanupResources()
    {
        if (m_ImageManager != null)
        {
            imageTrackingEventChannel.SetImageTrackedObject.RemoveListener(HandleImageTrackedObject);
            m_ImageManager.trackablesChanged.RemoveListener(ImageManagerOnTrackedImagesChanged);
        }

        disposables.Clear();
        disposables.Dispose();
        m_objectToLock = null;

        // Let the ARTrackedImageManager handle its own cleanup
        // instead of manually setting the reference library to null
    }

    public void HandleImageTrackedObject(GameObject obj)
    {
        if (obj == null) return;
        
        m_objectToLock = obj.GetComponent<ArObjectViewController>();
        if (m_objectToLock == null)
        {
            Debug.LogWarning($"HandleImageTrackedObject: No ArObjectViewController on {obj.name}.");
            return;
        }
    }

    void ImageManagerOnTrackedImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        if(m_objectToLock == null) return;

        // Handle both added and updated cases with the same logic
        HandleTrackedImages(eventArgs.added);
        HandleTrackedImages(eventArgs.updated);
    }

    private void HandleTrackedImages(IEnumerable<ARTrackedImage> images)
    {
        foreach (var image in images)
        {
            if (m_objectToLock == null) break;

            UpdateObjectTransform(image);
            LockObject();
        }
    }

    private void UpdateObjectTransform(ARTrackedImage image)
    {
        if (m_objectToLock == null || image == null) return;

        m_objectToLock.transform.position = image.transform.position;
        m_objectToLock.transform.rotation = image.transform.rotation;
        m_objectToLock.gameObject.SetActive(true);
    }

    private void LockObject()
    {
        if (m_objectToLock == null) return;

        Debug.Log($"{m_objectToLock.ObjectName} successfully image-tracked and locked.");
        
        // Clear the reference after locking
        var objectName = m_objectToLock.ObjectName;
        m_objectToLock = null;
        
        // Update state and notify listeners
        ProtocolState.Instance.LockingTriggered.Value = true;
        imageTrackingEventChannel.OnCurrentPrefabLocked();
    }
} 