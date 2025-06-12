using UniRx;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Hands;
public class PlaneInteractionManager : MonoBehaviour
{
    public static PlaneInteractionManager instance;
    public HeadPlacementEventChannel headPlacementEventChannel;

    private GameObject currentPrefab;

    [SerializeField] public Queue<GameObject> objectsQueue = new Queue<GameObject>(); //queue of objects to be placed on plane

    [SerializeField] Material planeMaterial;

    [SerializeField] Material invisiblePlaneMaterial;
    public List<ARPlane> availablePlanes;

    ARPlane currentPlane; //used for head reticle placement

    public static PlaneClassifications allowedPlaneClassifications = 
        PlaneClassifications.Table | 
        PlaneClassifications.None;
    private bool delayOn = false;
    private bool prefabTemporarilyLocked = false; //use to track whether the focused prefab should be tracked to the position of the head. This is used to prevent the prefab from being placed on the plane when the user is not ready to place it

    private CheckItemDefinition previousCheckItem;

    // Start is called before the first frame update
    //tips for each finger so that we can detect position of 

    // when enabled default reticle will point to collisions with planes based on the forward direction of the camera

    // a prefab can be passed in which can replace the reticle with a custom object that allows for visualization of any object on plane

    // uses hand interaction scripted object events to detect pinch and release events

    // when pinching the object will be placed on the plane
    private void Awake()
    {
        if(instance == null || instance == this)
        {
            instance = this;
        }
        else
        {
            DestroyImmediate(gameObject);
        }
    }

    private void OnEnable()
    {   
        AddSubscriptions();
    }

    private void OnDisable()
    {
        RemoveSubscriptions();
    }

    private void AddSubscriptions()
    {
        headPlacementEventChannel.SetHeadtrackedObject.AddListener(obj => SetPrefab(obj));
        headPlacementEventChannel.PlanePlacementRequested.AddListener(obj => OnPlanePlacementRequested(obj));
        //headPlacementEventChannel.RequestDisablePlaneInteractionManager.AddListener(ResetObjects);
        ProtocolState.Instance.ProtocolStream.Subscribe(_ => OnProtocolExit()).AddTo(this);
        //ProtocolState.Instance.ChecklistStream.Subscribe(_ => OnNextCheckItem()).AddTo(this);
    }

    private void RemoveSubscriptions()
    {
        headPlacementEventChannel.SetHeadtrackedObject.RemoveListener(SetPrefab);
        headPlacementEventChannel.PlanePlacementRequested.RemoveListener(OnPlanePlacementRequested);
        headPlacementEventChannel.RequestDisablePlaneInteractionManager.RemoveListener(ResetObjects);
        ProtocolState.Instance.ProtocolStream.Subscribe(_ => OnProtocolExit()).Dispose();   
        //ProtocolState.Instance.ChecklistStream.Subscribe(_ => OnNextCheckItem()).Dispose();
    }
    

    private void Update()
    {      
        #if UNITY_EDITOR
        
            if(Input.GetKeyDown(KeyCode.L))
            {
                TestObjectPlacement();
            }
        #endif   
        if(currentPrefab == null || prefabTemporarilyLocked)
        {
            return;
        }
        if(availablePlanes != null && availablePlanes.Count > 0)
        {
            RaycastHit [] hits;
            hits = Physics.RaycastAll(Camera.main.transform.position, Camera.main.transform.forward, 2f, 1); //1 = ignore triggers
            ARPlane plane = hits.Where(hit => hit.transform.TryGetComponent(out ARPlane plane)).Select(hit => hit.transform.GetComponent<ARPlane>()).FirstOrDefault();
            if(plane != null && availablePlanes.Contains(plane))
            {
                RaycastHit hit = hits.Where(hit => hit.transform.TryGetComponent(out ARPlane plane)).FirstOrDefault();
                if(currentPlane != null)
                {
                    currentPlane.GetComponent<MeshRenderer>().SetMaterials(new List<Material>() {invisiblePlaneMaterial});
                    currentPlane = null;
                }
                
                if(currentPlane == null || currentPlane != plane)
                {
                    currentPlane = plane;
                    currentPlane.GetComponent<MeshRenderer>().SetMaterials(new List<Material>() {planeMaterial});
                }

                //Debug.Log("PlaneInteractionManager: Updating current prefab position to " + hit.point);
                Vector3 inverseCameraPosition = new Vector3(-Camera.main.transform.position.x, currentPlane.center.y, -Camera.main.transform.position.z);
                Vector3 inverseHitPoint = new Vector3(-hit.point.x, currentPlane.center.y, -hit.point.z);
                currentPrefab.transform.SetPositionAndRotation(new Vector3(hit.point.x, currentPlane.center.y, hit.point.z), Quaternion.LookRotation(inverseHitPoint - inverseCameraPosition));
            }else if(plane == null && currentPlane != null)
            {
                currentPlane.GetComponent<MeshRenderer>().SetMaterials(new List<Material>() {invisiblePlaneMaterial});
                currentPlane = null;
            }
        }else if(availablePlanes == null || availablePlanes.Count == 0)
        {
            availablePlanes = ARPlaneViewController.instance.GetPlanesByClassification(allowedPlaneClassifications);
        }
    }


    private void OnPlanePlacementRequested(ARPlane plane)
    {
        if(delayOn || currentPlane == null || currentPlane != plane || currentPrefab == null)
        {
            return;
        }
        var audioSource = currentPrefab.GetComponent<AudioSource>();
        if(audioSource != null)
        {
            audioSource.Play();
        }
        //place object on plane, stop plane interaction requests
        Debug.Log("PlaneInteractionManager: Placing object on plane" + currentPrefab.name);
        var currentCheckItem = ProtocolState.Instance.CurrentCheckItemDefinition;

        if(currentPrefab != null)
        {
            currentPrefab.transform.position = new Vector3(currentPrefab.transform.position.x, currentPrefab.transform.position.y, currentPrefab.transform.position.z);
            currentPrefab = null;
            StartCoroutine(DelayNextPlacement());
            headPlacementEventChannel.CurrentPrefabLocked.Invoke();
            if(currentPlane != null)
            {
                currentPlane.GetComponent<MeshRenderer>().SetMaterials(new List<Material>() {invisiblePlaneMaterial});
                currentPlane = null;
            }
        }
        // prefabTemporarilyLocked = !prefabTemporarilyLocked;
        // if(prefabTemporarilyLocked)
        // {
        //     currentPlane.GetComponent<MeshRenderer>().SetMaterials(new List<Material>() {invisiblePlaneMaterial});
        // }else
        // {
        //     currentPlane.GetComponent<MeshRenderer>().SetMaterials(new List<Material>() {planeMaterial});
        // }
        //Debug.Log("PlaneInteractionManager: Object locked = " + prefabTemporarilyLocked);

    }

    // private void OnNextCheckItem()
    // {
    //     var currentCheckItem = ProtocolState.Instance.CurrentCheckItemDefinition;
        
    //     // Return early if this is the same checklist item
    //     if (previousCheckItem != null && previousCheckItem == currentCheckItem)
    //     {
    //         return;
    //     }
        
    //     previousCheckItem = currentCheckItem;

    //     if(currentPrefab != null)
    //     {
    //         currentPrefab = null;
    //         StartCoroutine(DelayNextPlacement());
    //         headPlacementEventChannel.CurrentPrefabLocked.Invoke();
    //         if(currentPlane != null)
    //         {
    //             currentPlane.GetComponent<MeshRenderer>().SetMaterials(new List<Material>() {invisiblePlaneMaterial});
    //             currentPlane = null;
    //         }
    //         prefabTemporarilyLocked = false;
    //     }
    // }

    private void TestObjectPlacement()
    {
        headPlacementEventChannel.CurrentPrefabLocked.Invoke();
        StartCoroutine(DelayNextPlacement());
        if(delayOn || currentPlane == null || currentPrefab == null)
        {
            if(currentPrefab == null)
            {
                Debug.Log("No object to place");
            }
            return;
        }
        Debug.Log("PlaneInteractionManager: Placing object on plane");
        //StartCoroutine(DelayNextPlacement());
        currentPrefab = null;
        //headPlacementEventChannel.CurrentPrefabLocked.Invoke();
        currentPlane.GetComponent<MeshRenderer>().SetMaterials(new List<Material>() {invisiblePlaneMaterial});
        currentPlane = null;
    }

    public void SetPrefab(GameObject prefab)
    {
        Debug.Log("PlaneInteractionManager: Setting prefab to " + prefab.name);
        currentPrefab = prefab;
        if(currentPrefab != null)
        {
            currentPrefab.SetActive(true);
        }
        if(currentPlane != null)
        {
            currentPlane.gameObject.SetActive(true);
        }
    }

    private void ResetObjects()
    {
        Debug.Log("PlaneInteractionManager: Resetting objects");
        if(currentPrefab != null)
        {
            Destroy(currentPrefab);
            currentPrefab = null;
        }
        if(currentPlane != null)
        {
            currentPlane.GetComponent<MeshRenderer>().SetMaterials(new List<Material>() {invisiblePlaneMaterial});
            currentPlane = null;
        }
        //availablePlanes = null; 
    }

    private void OnProtocolExit()
    {
        if(ProtocolState.Instance.ActiveProtocol.Value == null)
        {
            ResetObjects();
        }
    }
    private IEnumerator DelayNextPlacement()
    {
        delayOn = true;
        yield return new WaitForSeconds(1f);
        delayOn = false;
        yield break;
    }
}
