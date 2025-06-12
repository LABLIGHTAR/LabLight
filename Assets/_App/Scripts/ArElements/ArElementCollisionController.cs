using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// This class handles all collisions for ArElement it is attatched to
/// This script requires the following:
///     A rigidbody with Is Kinimatic selected
///     A Collider with Is Trigger selected
/// </summary>
public class ArElementCollisionController : MonoBehaviour
{
    public TrackedObject ThisTrackedObject;
    public string ThisObjectName;
    public ArObject ArObjectDefinition;

    public Material WarningMat;
    public Material DefaultMat;

    public string otherObjectName;

    void Start()
    {
        // Get tracked object and name if available
        var objectViewController = GetComponent<ArObjectViewController>();
        if (objectViewController != null)
        {
            ThisObjectName = objectViewController.ObjectName;
        }

        ConfigureCollider();
    }

    private void ConfigureCollider()
    {
        var boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null) return;

        switch (ThisObjectName)
        {
            case "96 Well Plate":
                boxCollider.size = new Vector3(0.125f, 0.03f, 0.08f);
                break;
            case "50mL Tube Rack":
                boxCollider.size = new Vector3(0.09f, 0.085f, 0.045f);
                break;
            case "50mL Tube":
                boxCollider.size = new Vector3(0.03f, 0.115f, 0.03f);
                break;
            case "Micropipette":
                boxCollider.size = new Vector3(0.05f, 0.25f, 0.04f);
                break;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Handle collision with other AR elements
        var otherViewController = other.gameObject.GetComponent<ArObjectViewController>();
        if (otherViewController != null)
        {
            otherObjectName = otherViewController.ObjectName;
            if (ThisObjectName == otherObjectName) return;

            // Handle specific interactions
            HandleObjectInteraction(true);
        }

        // Handle collision with model elements
        var otherModelView = other.gameObject.GetComponent<ArObjectViewController>();
        if (otherModelView != null)
        {
            otherObjectName = otherModelView.ObjectName;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Handle exit collision with other AR elements
        var otherViewController = other.gameObject.GetComponent<ArObjectViewController>();
        if (otherViewController != null)
        {
            otherObjectName = otherViewController.ObjectName;
            if (ThisObjectName == otherObjectName) return;

            // Handle specific interactions
            HandleObjectInteraction(false);
        }

        // Handle exit collision with model elements
        var otherModelView = other.gameObject.GetComponent<ArObjectViewController>();
        if (otherModelView != null)
        {
            otherObjectName = otherModelView.ObjectName;
        }
    }

    private void HandleObjectInteraction(bool isEntering)
    {
        // Handle specific object interactions
        if (ThisObjectName == "96 Well Plate" && otherObjectName == "Micropipette")
        {
            //Debug.Log(isEntering ? "Started Pipetting" : "Finished Pipetting");
        }
        // Add other specific interactions here
    }
}
