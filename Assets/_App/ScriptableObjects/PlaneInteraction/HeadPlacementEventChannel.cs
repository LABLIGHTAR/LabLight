using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;

[CreateAssetMenu(fileName = "HeadPlacementEventChannel", menuName = "ScriptableObjects/HeadPlacementEventChannel", order = 2)]
public class HeadPlacementEventChannel : ScriptableObject
{
    [SerializeField]
    public UnityEvent<GameObject> SetHeadtrackedObject = new UnityEvent<GameObject>();

    [SerializeField]
    public UnityEvent<ARPlane> PlanePlacementRequested = new UnityEvent<ARPlane>();
    [SerializeField] 
    public UnityEvent<Vector3> HeadPositionPlacement = new UnityEvent<Vector3>();
    [SerializeField]
    public UnityEvent CurrentPrefabLocked = new UnityEvent();
    [SerializeField]
    public UnityEvent RequestDisablePlaneInteractionManager = new UnityEvent();
    // Start is called before the first frame update
    private void Awake()
    {
        if(SetHeadtrackedObject == null)
        {
            SetHeadtrackedObject = new UnityEvent<GameObject>();
        }
        if(PlanePlacementRequested == null)
        {
            PlanePlacementRequested = new UnityEvent<ARPlane>();
        }
        if(HeadPositionPlacement == null)
        {
            HeadPositionPlacement = new UnityEvent<Vector3>();
        }
        if(CurrentPrefabLocked == null)
        {
            CurrentPrefabLocked = new UnityEvent();
        }
        if(RequestDisablePlaneInteractionManager == null)
        {
            RequestDisablePlaneInteractionManager = new UnityEvent();
        }
    }

    public void OnSetHeadtrackedObject(GameObject obj)
    {
        SetHeadtrackedObject.Invoke(obj);
    }
    
    public void OnPlanePlacementRequested(ARPlane plane)
    {
        PlanePlacementRequested.Invoke(plane);
    }

    public void OnCurrentPrefabLocked()
    {
        CurrentPrefabLocked.Invoke();
    }

    public void OnRequestDisablePlaneInteractionManager()
    {
        RequestDisablePlaneInteractionManager.Invoke();
    }
}
