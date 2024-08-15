using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class RelockModelButton : MonoBehaviour
{
    [SerializeField]
    public PlaneInteractionManagerScriptableObject planeInteractionManagerSO;
    private XRSimpleInteractable interactable;

    public GameObject parentModel;

    public void Start()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        interactable.selectEntered.AddListener(_ => RelockModel());
    }

    public void OnDestroy()
    {
        interactable.selectEntered.RemoveListener(_ => RelockModel());
    }

    public void RelockModel()
    {
        Debug.Log("Relocking model");
        planeInteractionManagerSO.SetHeadtrackedObject.Invoke(parentModel);
    }
}