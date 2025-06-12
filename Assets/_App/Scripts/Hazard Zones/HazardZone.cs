using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class HazardZone : MonoBehaviour
{
    [SerializeField] float distanceThreshold = 0.33f;
    [SerializeField] MeshRenderer meshRenderer;
    [SerializeField] Transform zoneTransform;
    MeshFilter zoneMeshFilter;

    [Header("Event Channels")]
    [SerializeField] HeadPlacementEventChannel headPlacementEventChannel;
    [SerializeField] HudEventSO hudEventSO;

    [Header("UI Elements")]
    [SerializeField] XRSimpleInteractable removeButton;
    [SerializeField] XRSimpleInteractable resizeButton;
    [SerializeField] XRSimpleInteractable moveButton;

    [Header("Resize Controls")]
    [SerializeField] GameObject resizeControlParent;

    [SerializeField] Canvas frontControlCanvas;
    [SerializeField] Slider frontSlider;

    [SerializeField] Canvas sideControlCanvas;
    [SerializeField] Slider sideSlider;

    XRHandSubsystem m_HandSubsystem;

    void Awake()
    {
        zoneMeshFilter = zoneTransform.GetComponent<MeshFilter>();
        meshRenderer = zoneTransform.GetComponent<MeshRenderer>();

        var handSubsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(handSubsystems);

        for (var i = 0; i < handSubsystems.Count; ++i)
        {
            var handSubsystem = handSubsystems[i];
            if (handSubsystem.running)
            {
                m_HandSubsystem = handSubsystem;
                break;
            }
        }

        if (m_HandSubsystem != null)
            m_HandSubsystem.updatedHands += OnUpdatedHands;
    }

    void Start()
    {
        frontSlider.value = zoneTransform.localScale.x;
        sideSlider.value = zoneTransform.localScale.z;
        removeButton.selectExited.AddListener(_ => Destroy(gameObject));
        moveButton.selectExited.AddListener(_ => headPlacementEventChannel.OnSetHeadtrackedObject(gameObject));
        resizeButton.selectExited.AddListener(_ => resizeControlParent.SetActive(!resizeControlParent.activeSelf));
    }

    public void Initalize(Mesh mesh, Material material, float size)
    {
        if(mesh.name == "Cube")
        {
            zoneTransform.localPosition = new Vector3(zoneTransform.localPosition.x, zoneTransform.localPosition.y / 2f, zoneTransform.localPosition.z);
        }
        zoneMeshFilter.mesh = mesh;
        meshRenderer.material = material;
        zoneTransform.localScale = new Vector3(size, zoneTransform.localScale.y, size);
        frontSlider.value = size;
        sideSlider.value = size;
        StartCoroutine(StartPlacement());
    }

    void OnUpdatedHands(XRHandSubsystem subsystem, XRHandSubsystem.UpdateSuccessFlags updateSuccessFlags, XRHandSubsystem.UpdateType updateType)
    {
        switch (updateType)
        {
            case XRHandSubsystem.UpdateType.Dynamic:
                var rightHandData = m_HandSubsystem.rightHand.GetJoint(XRHandJointID.MiddleProximal);
                rightHandData.TryGetPose(out Pose poseRight);
                var rightHandDistance = Vector3.Distance(transform.position, poseRight.position);

                var leftHandData = m_HandSubsystem.leftHand.GetJoint(XRHandJointID.MiddleProximal);
                leftHandData.TryGetPose(out Pose poseLeft);
                var leftHandDistance = Vector3.Distance(transform.position, poseLeft.position);

                var smallestDistance = Mathf.Min(rightHandDistance, leftHandDistance);

                // Check if either hand is within a third of a meter of this object
                if (smallestDistance <= distanceThreshold)
                {
                    hudEventSO.DisplayHudWarning("Entering hazard zone");
                    meshRenderer.material.color = new Color(meshRenderer.material.color.r, meshRenderer.material.color.g, meshRenderer.material.color.b, 0.1f);
                }
                else if(smallestDistance > 1.0f)
                {
                    meshRenderer.material.color = new Color(meshRenderer.material.color.r, meshRenderer.material.color.g, meshRenderer.material.color.b, 0.5f);
                }
                else
                {
                    var currentOpacity = meshRenderer.material.color.a;
                    var newOpacity = smallestDistance * 0.5f;
                    meshRenderer.material.color = new Color(meshRenderer.material.color.r, meshRenderer.material.color.g, meshRenderer.material.color.b, newOpacity);
                }
                break;
        }
    }

    public void OnFrontSliderValueChanged(float value)
    {
        zoneTransform.localScale = new Vector3(value, zoneTransform.localScale.y, zoneTransform.localScale.z);
        sideControlCanvas.transform.localPosition = new Vector3(value / 2, sideControlCanvas.transform.localPosition.y, sideControlCanvas.transform.localPosition.z);
    }

    public void OnSideSliderValueChanged(float value)
    {
        zoneTransform.localScale = new Vector3(zoneTransform.localScale.x, zoneTransform.localScale.y, value);
        frontControlCanvas.transform.localPosition = new Vector3(frontControlCanvas.transform.localPosition.x, frontControlCanvas.transform.localPosition.y, value / 2);
    }

    IEnumerator StartPlacement()
    {
        yield return new WaitForSeconds(1.0f);
        headPlacementEventChannel.OnSetHeadtrackedObject(gameObject);
    }
}
