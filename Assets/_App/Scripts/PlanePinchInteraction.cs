using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class PlanePinchInteraction : MonoBehaviour
{
    [SerializeField]
    public HeadPlacementEventChannel headPlacementEventChannel;

    private XRSimpleInteractable interactable;

    // Start is called before the first frame update
    public void Start()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        interactable.selectEntered.AddListener(_ => PlaneSelected());
    }

    public void OnDestroy()
    {
        interactable.selectEntered.RemoveListener(_ => PlaneSelected());
    }
    public void PlaneSelected()
    {
        headPlacementEventChannel.OnPlanePlacementRequested(this.GetComponent<ARPlane>());
    }
}
