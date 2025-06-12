// using System.Collections.Generic;
// using UnityEngine;

// /// <summary>
// /// Applies smoothed trackedObject position and orientation to the view. 
// /// When TrackedObject is not available anymore it uses the default position/orientation
// /// </summary>
// public class WorldPositionController : ArObjectViewController
// {
//     protected Vector3 currentVelocity;
//     protected Quaternion currentQuaternionVelocity;
//     protected float smoothTime = .1f;

//     // Position tracking
//     protected Vector3 _defaultPosition = Vector3.zero;
//     protected Vector3 positionOnLock;

//     // State flags
//     protected bool lockedToTablePlane;
//     public bool selectedForLocking = false;
//     public bool positionValid = true;
//     public bool positionLocked = false;
//     public bool hasBeenLocked = false;

//     public override void Initialize(ArObject arObject, List<TrackedObject> trackedObjects)
//     {
//         base.Initialize(arObject, trackedObjects);

//         // Set initial position based on CharucoTransform or default
//         transform.position = SessionManager.instance.CharucoTransform != null 
//             ? SessionManager.instance.CharucoTransform.position 
//             : _defaultPosition;
//     }

//     public void UnlockPosition()
//     {
//         positionLocked = false;
//         lockedToTablePlane = false;
//     }

//     public void LockPosition()
//     {
//         hasBeenLocked = true;
//         positionLocked = true;
        
//         // Only lock to table plane if it's a model type
//         if (arObject != null && arObject.rootPrefabName != null)
//         {
//             lockedToTablePlane = true;
//             // Lock to table plane by zeroing Y position
//             transform.localPosition = new Vector3(transform.localPosition.x, 0, transform.localPosition.z);
//         }
        
//         // Store locked position
//         positionOnLock = transform.localPosition;
        
//         // Notify that this object has been locked
//         var headPlacementEventChannel = ServiceRegistry.GetService<HeadPlacementEventChannel>();
//         headPlacementEventChannel?.CurrentPrefabLocked?.Invoke();
//     }

//     // Virtual update method for derived classes
//     public virtual void Update() { }
// }
