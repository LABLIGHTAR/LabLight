using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Linq;

public class ARPlaneViewController : MonoBehaviour
{
    public static ARPlaneViewController instance;
    private ARPlaneManager planeManager = null;

    private List<ARPlane> planes = new List<ARPlane>();

    void Awake()
    {
        if(instance == null)
        {
            instance = this;
        }
        else
        {
            Debug.LogWarning("ARPlaneViewController already exists, destroying new instance");
            DestroyImmediate(gameObject);
        }
        planeManager = this.transform.parent.GetComponent<ARPlaneManager>();
        if(planeManager != null)
        {
            Debug.Log("ar plane view controller awake " + planeManager);
            planeManager.trackablesChanged.AddListener(OnTrackablesChanged);
        }
    }

    void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARPlane> changes)
    {
        // Handle added planes
        foreach(var plane in changes.added)
        {
            Debug.Log("added plane " + plane);
            planes.Add(plane);
        }

        // Handle updated planes
        foreach(var plane in changes.updated)
        {
            if(!planes.Contains(plane))
            {
                Debug.Log("updated plane " + plane);
                planes.Add(plane);
            }
        }

        // Handle removed planes
        foreach(var removedPlane in changes.removed)
        {
            var plane = removedPlane.Value;
            if(planes.Contains(plane))
            {
                Debug.Log("removed plane " + plane);
                planes.Remove(plane);
            }
        }
    }

    public List<ARPlane> GetPlanesByClassification(PlaneClassifications classifications)
    {
        List<ARPlane> returnList = new List<ARPlane>();
        foreach(var plane in planes)
        {
            if((plane.classifications & classifications) != 0)
            {
                returnList.Add(plane);
            }
        }
        return returnList;
    }

    public List<ARPlane> GetPlanesWithinDistanceAndClassification(float distance, PlaneClassifications? classifications = null)
    {
        List<ARPlane> returnList = new List<ARPlane>();
        foreach(var plane in planes)
        {
            if(Vector3.Distance(plane.transform.position, Camera.main.transform.position) < distance)
            {
                if(!classifications.HasValue || (plane.classifications & classifications.Value) != 0)
                {
                    returnList.Add(plane);
                }
            }
        }
        return returnList;
    }
}
