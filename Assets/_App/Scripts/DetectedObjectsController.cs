using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class DetectedObjectsManager : MonoBehaviour
{

    [SerializeField]
    private float duplicateDetectionThreshold = 0.03f;

    private Dictionary<TrackedObject, GameObject> trackedObjectLabels = new Dictionary<TrackedObject, GameObject>();
    [SerializeField]
    private GameObject labelPrefab;

    void Awake()
    {
        SessionState.TrackedObjects.ObserveAdd().Subscribe(trackedObject => processAddedObject(trackedObject.Value)).AddTo(this);
        SessionState.TrackedObjects.ObserveRemove().Subscribe(trackedObject => processRemovedObject(trackedObject.Value)).AddTo(this);
        SessionState.enableGenericVisualizations.Subscribe(_ => ToggleGenericViews()).AddTo(this);
    }

    private void processAddedObject(TrackedObject trackedObject)
    {
        Transform parent = SessionState.CharucoTransform;
        foreach(TrackedObject to in trackedObjectLabels.Keys)
        {
            if(Vector3.Distance(new Vector3(to.position.x, to.position.y, to.position.z), new Vector3(trackedObject.position.x, to.position.y, trackedObject.position.y)) < duplicateDetectionThreshold)
            {
                Debug.Log("Manager: not generating model of label " + trackedObject.label + " because it is too close to tracked object " + to.id);
                return;
            }
        }
        instantiateLabel(SessionState.CharucoTransform, trackedObject);
    }

    private void processRemovedObject(TrackedObject trackedObject)
    {
        //remove label
        Destroy(trackedObjectLabels[trackedObject]);
        trackedObjectLabels.Remove(trackedObject);
    }

    private void instantiateLabel(Transform parent, TrackedObject trackedObject) //worldpositioncontroller still manages the labels position based on data from networking
    {
        GameObject newLabelPrefab = Instantiate(labelPrefab, parent);

        //newLabelPrefab.GetComponent<LabelViewController>().Initialize( new List<TrackedObject> { trackedObject });

        trackedObjectLabels.Add(trackedObject, newLabelPrefab);

        //add to dictionary
    }

    private void ToggleGenericViews()
    {
        // foreach(GameObject label in trackedObjectLabels.Values)
        // {
        //     label.SetActive(SessionState.enableGenericVisualizations.Value);
        // }
        //toggle all labels
    }
}
