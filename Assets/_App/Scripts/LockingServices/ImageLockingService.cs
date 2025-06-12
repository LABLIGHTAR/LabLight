using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UniRx;

/// <summary>
/// Service for handling image tracking-based object locking.
/// Integrates with ImageTrackingObjectManager functionality.
/// </summary>
public class ImageLockingService : MonoBehaviour, ILockingService
{
    public event Action<GameObject> OnObjectLocked;

    public LockingType LockingType => LockingType.Image;
    public bool IsLocking => m_objectToLock != null;

    [SerializeField]
    [Tooltip("Image manager on the AR Session Origin")]
    private ARTrackedImageManager m_ImageManager;

    private ArObjectViewController m_objectToLock;
    private CompositeDisposable disposables = new CompositeDisposable();
    private bool isQuitting = false;

    private void Awake()
    {
        // Verify required components
        if (m_ImageManager == null)
        {
            Debug.LogError($"[{nameof(ImageLockingService)}] ARTrackedImageManager is required but was not set.");
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        if (m_ImageManager != null && m_ImageManager.enabled)
        {
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
            m_ImageManager.trackablesChanged.RemoveListener(ImageManagerOnTrackedImagesChanged);
        }

        disposables.Clear();
        disposables.Dispose();
        m_objectToLock = null;
    }

    public void BeginLocking(GameObject objectToLock)
    {
        if (objectToLock == null)
        {
            Debug.LogWarning("ImageLockingService: Cannot lock null object");
            return;
        }

        m_objectToLock = objectToLock.GetComponent<ArObjectViewController>();
        if (m_objectToLock == null)
        {
            Debug.LogWarning($"ImageLockingService: No ArObjectViewController on {objectToLock.name}.");
            return;
        }

        Debug.Log($"ImageLockingService: Beginning image tracking for {objectToLock.name}");
    }

    public void CancelLocking()
    {
        if (m_objectToLock != null)
        {
            Debug.Log("ImageLockingService: Cancelling image tracking");
            m_objectToLock.gameObject.SetActive(false);
            m_objectToLock = null;
        }
    }

    void ImageManagerOnTrackedImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        if (m_objectToLock == null) return;

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

        Debug.Log($"ImageLockingService: {m_objectToLock.ObjectName} successfully image-tracked and locked.");
        
        // Store reference to locked object before clearing
        var lockedObject = m_objectToLock.gameObject;
        var objectName = m_objectToLock.ObjectName;
        
        // Clear the reference after locking
        m_objectToLock = null;
        
        // Update state and notify listeners
        ProtocolState.Instance.LockingTriggered.Value = true;
        OnObjectLocked?.Invoke(lockedObject);
    }
}