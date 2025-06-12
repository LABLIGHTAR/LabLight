using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UniRx;

/// <summary>
/// Service for handling plane-based object locking.
/// Integrates with PlaneInteractionManager functionality.
/// </summary>
public class PlaneLockingService : MonoBehaviour, ILockingService
{
    public event Action<GameObject> OnObjectLocked;
    public LockingType LockingType => LockingType.Plane;
    public bool IsLocking => currentPrefab != null;

    [SerializeField] private Material planeMaterial;
    [SerializeField] private Material invisiblePlaneMaterial;

    private GameObject currentPrefab;
    private ARPlane currentPlane;
    private List<ARPlane> availablePlanes;
    private bool delayOn = false;

    public static PlaneClassifications allowedPlaneClassifications = 
        PlaneClassifications.Table | 
        PlaneClassifications.None;

    [SerializeField] private HeadPlacementEventChannel headPlacementEventChannel;

    private void Awake()
    {
        // If not assigned in inspector, try to find the HeadPlacementEventChannel
        if (headPlacementEventChannel == null)
        {
            Debug.LogError("PlaneLockingService: HeadPlacementEventChannel not assigned");
        }
    }

    private void OnEnable()
    {
        if (headPlacementEventChannel != null)
        {
            headPlacementEventChannel.PlanePlacementRequested.AddListener(OnPlanePlacementRequested);
        }
        
        ProtocolState.Instance.ProtocolStream.Subscribe(_ => OnProtocolExit()).AddTo(this);
    }

    private void OnDisable()
    {
        if (headPlacementEventChannel != null)
        {
            headPlacementEventChannel.PlanePlacementRequested.RemoveListener(OnPlanePlacementRequested);
        }
    }

    private void Update()
    {
        #if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.L) && currentPrefab != null)
        {
            TestObjectPlacement();
        }
        #endif

        if (currentPrefab == null) return;

        UpdatePlaneTracking();
    }

    private void UpdatePlaneTracking()
    {
        if (availablePlanes == null || availablePlanes.Count == 0)
        {
            availablePlanes = ARPlaneViewController.instance.GetPlanesByClassification(allowedPlaneClassifications);
            return;
        }

        RaycastHit[] hits = Physics.RaycastAll(Camera.main.transform.position, Camera.main.transform.forward, 2f, 1);
        ARPlane plane = hits.Where(hit => hit.transform.TryGetComponent(out ARPlane _))
                            .Select(hit => hit.transform.GetComponent<ARPlane>())
                            .FirstOrDefault();

        if (plane != null && availablePlanes.Contains(plane))
        {
            RaycastHit hit = hits.Where(hit => hit.transform.TryGetComponent(out ARPlane _)).FirstOrDefault();
            
            if (currentPlane != null && currentPlane != plane)
            {
                currentPlane.GetComponent<MeshRenderer>().SetMaterials(new List<Material>() { invisiblePlaneMaterial });
            }

            currentPlane = plane;
            currentPlane.GetComponent<MeshRenderer>().SetMaterials(new List<Material>() { planeMaterial });

            Vector3 inverseCameraPosition = new Vector3(-Camera.main.transform.position.x, currentPlane.center.y, -Camera.main.transform.position.z);
            Vector3 inverseHitPoint = new Vector3(-hit.point.x, currentPlane.center.y, -hit.point.z);
            currentPrefab.transform.SetPositionAndRotation(
                new Vector3(hit.point.x, currentPlane.center.y, hit.point.z), 
                Quaternion.LookRotation(inverseHitPoint - inverseCameraPosition)
            );
        }
        else if (plane == null && currentPlane != null)
        {
            currentPlane.GetComponent<MeshRenderer>().SetMaterials(new List<Material>() { invisiblePlaneMaterial });
            currentPlane = null;
        }
    }

    public void BeginLocking(GameObject objectToLock)
    {
        if (objectToLock == null)
        {
            Debug.LogWarning("PlaneLockingService: Cannot lock null object");
            return;
        }

        Debug.Log($"PlaneLockingService: Beginning locking process for {objectToLock.name}");
        currentPrefab = objectToLock;
        currentPrefab.SetActive(true);
        
        // Refresh available planes
        availablePlanes = ARPlaneViewController.instance.GetPlanesByClassification(allowedPlaneClassifications);
    }

    public void CancelLocking()
    {
        if (currentPrefab != null)
        {
            Debug.Log("PlaneLockingService: Cancelling locking operation");
            currentPrefab.SetActive(false);
            currentPrefab = null;
        }

        if (currentPlane != null)
        {
            currentPlane.GetComponent<MeshRenderer>().SetMaterials(new List<Material>() { invisiblePlaneMaterial });
            currentPlane = null;
        }
    }

    private void OnPlanePlacementRequested(ARPlane plane)
    {
        if (delayOn || currentPlane == null || currentPlane != plane || currentPrefab == null)
        {
            return;
        }

        var audioSource = currentPrefab.GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.Play();
        }

        Debug.Log($"PlaneLockingService: Locking object {currentPrefab.name} on plane");
        
        var lockedObject = currentPrefab;
        lockedObject.transform.position = new Vector3(
            lockedObject.transform.position.x, 
            lockedObject.transform.position.y, 
            lockedObject.transform.position.z
        );

        // Clean up state
        currentPrefab = null;
        if (currentPlane != null)
        {
            currentPlane.GetComponent<MeshRenderer>().SetMaterials(new List<Material>() { invisiblePlaneMaterial });
            currentPlane = null;
        }

        StartCoroutine(DelayNextPlacement());
        OnObjectLocked?.Invoke(lockedObject);
    }

    private void TestObjectPlacement()
    {
        if (delayOn || currentPlane == null || currentPrefab == null) return;
        
        Debug.Log("PlaneLockingService: Test placement");
        var lockedObject = currentPrefab;
        currentPrefab = null;
        
        if (currentPlane != null)
        {
            currentPlane.GetComponent<MeshRenderer>().SetMaterials(new List<Material>() { invisiblePlaneMaterial });
            currentPlane = null;
        }
        
        StartCoroutine(DelayNextPlacement());
        OnObjectLocked?.Invoke(lockedObject);
    }

    private void OnProtocolExit()
    {
        if (ProtocolState.Instance.ActiveProtocol.Value == null)
        {
            CancelLocking();
        }
    }

    private IEnumerator DelayNextPlacement()
    {
        delayOn = true;
        yield return new WaitForSeconds(1f);
        delayOn = false;
    }
}