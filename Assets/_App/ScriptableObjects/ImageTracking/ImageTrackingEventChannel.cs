using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "ImageTrackingEventChannel", menuName = "ScriptableObjects/ImageTrackingEventChannel", order = 2)]
public class ImageTrackingEventChannel : ScriptableObject
{
    [SerializeField]
    public UnityEvent<GameObject> SetImageTrackedObject = new UnityEvent<GameObject>();

    [SerializeField]
    public UnityEvent CurrentPrefabLocked = new UnityEvent();

    private void Awake()
    {
        if(SetImageTrackedObject == null)
        {
            SetImageTrackedObject = new UnityEvent<GameObject>();
        }
        if(CurrentPrefabLocked == null)
        {
            CurrentPrefabLocked = new UnityEvent();
        }
    }

    public void OnSetImageTrackedObject(GameObject obj)
    {
        SetImageTrackedObject.Invoke(obj);
    }

    public void OnCurrentPrefabLocked()
    {
        CurrentPrefabLocked.Invoke();
    }
} 