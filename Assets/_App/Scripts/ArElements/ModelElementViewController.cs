// using System;
// using System.Text;
// using System.Collections;
// using System.Collections.Generic;
// using System.IO;
// using UnityEngine;
// using System.Linq;
// using UniRx;
// using TMPro;


// //TODO
// //variable to verify if object is currently active in scene

// [Serializable]
// public class HighlightGroup
// {
//     // Part name as used in the procedure
//     public string Name;
//     // Single part may consist of multiple gameobjects
//     public List<GameObject> SubParts;
// }

// [RequireComponent(typeof(CapsuleCollider))]
// [RequireComponent(typeof(AudioSource))]
// public class ModelElementViewController : WorldPositionController
// {
//     public string ObjectName;

//     [Tooltip("Predefined higlights that should highlight the real world model")]
//     [SerializeField]
//     public List<HighlightGroup> HighlightGroups;

//     public GameObject sphere;

//     [SerializeField]
//     public Transform ModelName;

//     public override void Initialize(ArObject arObject, List<TrackedObject> trackedObjects)
//     {
//         base.Initialize(arObject, trackedObjects);

//         // If not targeted (* or specific id) use target based positioning
//         // if (this.arObject.condition == null)
//         // {
//         //     var modelArObject = (ModelArObject)arObject;
//         //     transform.localPosition = modelArObject.position;
//         //     transform.localRotation = modelArObject.rotation;
//         // }

//         this.TrackedObjects.ObserveAdd().Subscribe(_ =>
//         {
//             if(this.TrackedObjects.Count > 1)
//             {
//                 this.TrackedObjects.Remove(this.TrackedObjects[1]);
//             }
//         }).AddTo(this);
//     }

//     public virtual void AlignmentGroup()
//     {
//         return;
//     }

//     //resets model back to previous highlight if there is one
//     public virtual void ResetToCurrentHighlights()
//     {
//         return;
//     }
//             //new imp
//     public virtual void HighlightGroup(List<ArAction> actions)
//     {
//         return;
//     }

//     public virtual void disablePreviousHighlight()
//     {
//         return;
//     }

//     public virtual void Rotate(float degrees)
//     {
//         return;
//     }


//     public override void Update()
//     {
//         // // Use smooth positioning only when targeted (* or specific id) used for object detection, depricated
//         // if (arObject != null && arObject.condition != null)
//         // {
//         //     base.Update();
//         // }
//     }

//     public void PlayAnimation(string animationName)
//     {
//         Debug.LogWarning("Animation playing not implemented yet");
//     }

//     void OnTriggerEnter(Collider other)
//     {
//         var otherView = other.gameObject.GetComponent<ModelElementViewController>();
//         if (otherView != null)
//         {
//             // Only show interaction spheres if the other object is locked and this one isn't
//             if (otherView.positionLocked && !positionLocked)
//             {
//                 if (sphere != null)
//                 {
//                     sphere.SetActive(true);
//                 }
//                 if (otherView.sphere != null)
//                 {
//                     otherView.sphere.SetActive(true);
//                 }
//                 positionValid = false;
//             }
//         }
//     }

//     void OnTriggerExit(Collider other)
//     {
//         if(sphere != null)
//         {
//             sphere.SetActive(false);
//         }
//         positionValid = true;
//     }
// }
