using UnityEngine;
using UnityEngine.XR.ARFoundation;

[RequireComponent(typeof(ImageTrackingObjectManager))]
public class ImageTrackingCleanup : MonoBehaviour
{
    private ARTrackedImageManager m_ImageManager;
    private bool isQuitting = false;
    
    void Start()
    {
        // Get reference to the image manager
        m_ImageManager = GetComponentInChildren<ARTrackedImageManager>();
    }
    
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && m_ImageManager != null && !isQuitting)
        {
            m_ImageManager.enabled = false;
        }
    }
    
    void OnApplicationQuit()
    {
        isQuitting = true;
        if (m_ImageManager != null)
        {
            m_ImageManager.enabled = false;
        }
    }

    void OnDisable()
    {
        if (!isQuitting && m_ImageManager != null)
        {
            m_ImageManager.enabled = false;
        }
    }
}