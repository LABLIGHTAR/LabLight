using UniRx;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Hands;

public class ProtocolItemLockingManager : MonoBehaviour
{
    public HeadPlacementEventChannel headPlacementEventChannel;

    private Queue<GameObject> objectsQueue = new Queue<GameObject>();

    private void OnEnable()
    {
        AddSubscriptions();
    }

    private void OnDisable()
    {
        RemoveSubscriptions();
        if(objectsQueue.Count > 0)
        {
            headPlacementEventChannel.RequestDisablePlaneInteractionManager.Invoke();
            objectsQueue.Clear();
        }
    }

    private void AddSubscriptions()
    {
        headPlacementEventChannel.CurrentPrefabLocked.AddListener(ObjectLocked);
    }

    private void RemoveSubscriptions()
    {
        headPlacementEventChannel.CurrentPrefabLocked.RemoveListener(ObjectLocked);
    } 

    public void EnqueueObjects(List<GameObject> objectsToLock)
    {
        Debug.Log("Added " + objectsToLock.Count + " objects to lock");
        
        // Clear existing queue if any
        objectsQueue.Clear();
        
        if (objectsToLock.Count > 0)
        {
            // Start with first object
            headPlacementEventChannel.SetHeadtrackedObject.Invoke(objectsToLock[0]);
            
            // Queue remaining objects
            for (int i = 1; i < objectsToLock.Count; i++)
            {
                objectsQueue.Enqueue(objectsToLock[i]);
                Debug.Log("Enqueued remaining object: " + objectsToLock[i].name);
            }
            
            // Enable locking state in ProtocolState
            Debug.Log("Enabling locking state");
            ProtocolState.Instance.LockingTriggered.Value = true;
        }
    }

    private void ObjectLocked()
    {
        Debug.Log("ObjectLocked " + objectsQueue.Count + " objects remaining");
        if (objectsQueue.Count > 0)
        {
            Debug.Log("Dequeuing object: " + objectsQueue.Peek().name);
            headPlacementEventChannel.SetHeadtrackedObject.Invoke(objectsQueue.Dequeue());
        }
        else
        {
            headPlacementEventChannel.RequestDisablePlaneInteractionManager.Invoke();
            Debug.Log("No more objects to lock, disabling plane interaction manager");
            ProtocolState.Instance.LockingTriggered.Value = false;
        }
    }
}