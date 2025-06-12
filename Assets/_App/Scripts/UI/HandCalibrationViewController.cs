using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Linq;
using System;
using UniRx;

public class HandCalibrationViewController : MonoBehaviour
{
    //[SerializeField] MeshRenderer progressRing;
    [SerializeField] GameObject originPrefab;

    [SerializeField] GameObject jointPrefab;

    [SerializeField] Material fillMaterial;

    [SerializeField] public CalibrationManagerScriptableObject calibrationManager;


    public Material planeMaterial;
    public Material removePlaneMaterial;

    public static XRHandJointID[] calibrationJoints = new XRHandJointID[]
    {
        XRHandJointID.IndexTip,
        XRHandJointID.MiddleTip,
        XRHandJointID.RingTip,
        XRHandJointID.LittleTip,
        XRHandJointID.IndexProximal,
        XRHandJointID.MiddleProximal,
        XRHandJointID.RingProximal,
        XRHandJointID.LittleProximal
    };

    public static PlaneClassifications calibrationPlanesClassifications = 
        PlaneClassifications.Table | 
        PlaneClassifications.None;

    List<ARPlane> availableCalibrationPlanes;

    public Dictionary<XRHandJointID, Pose> calibrationJointsPoseDict = new Dictionary<XRHandJointID, Pose>();

    ARPlane planeSelected = null;

    XRHandSubsystem m_HandSubsystem;

    private Coroutine matrixCoroutine = null;
    
    public float distanceToPlaneThreshold = 0.05f;

    public float calibrationDistanceThreshold = 0.03f;

    private bool inCalibration = false;

    private bool usingLeftHand = false;

    private void Awake()
    {
        fillMaterial.SetFloat("_FillRate", -0.4f);
        //StartCoroutine(CalibrationAnimation());
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
    }
 
    private void OnEnable() 
    {
        RequestCalibration();
// #if UNITY_EDITOR
//         StartCoroutine(UnloadCalibration());
// #endif
    }

    private IEnumerator UnloadCalibration()
    {
        yield return new WaitForSeconds(1f);
        SceneLoader.Instance.UnloadScene("Calibration");
    }

    public void RequestCalibration()
    {
        //anchorManager.enabled = true;
        Debug.Log("calibration requested");

        //planeSelected.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { removePlaneMaterial });
        if(m_HandSubsystem != null)
        {
            m_HandSubsystem.updatedHands += OnUpdatedHands;
        }
        //start calibration
        calibrationManager.UpdateCalibrationStatus("Looking for planes");
        availableCalibrationPlanes = ARPlaneViewController.instance.GetPlanesByClassification(calibrationPlanesClassifications);
        //if calibration completed successfully, send calibration data to lighthouse and exit calibration mode
        //store started lighthouse origin and current plane in session manager
    }

    public void TestCalibration()
    {
        Matrix4x4 calibrationMatrix = CalibrationFromMatrix.Calculate_Hand_Coordinate_System_Transform(true, 
            -0.6664742f, 0.827593f, 0.6830118f, //manual calibration values as an example for testing
            -0.6452482f, 0.827593f, 0.6948431f,
            -0.6273278f, 0.827593f, 0.7039112f,
            -0.6043002f, 0.827593f, 0.7051851f,
            -0.7314976f, 0.827593f, 0.7608822f,
            -0.7092083f, 0.827593f, 0.7899398f,
            -0.6229721f, 0.827593f, 0.7843601f,
            -0.6789629f, 0.827593f, 0.7930077f
        );
        SessionManager.instance.UpdateCalibration(calibrationMatrix);
        var originInstance = Instantiate(originPrefab, SessionState.CharucoTransform.position, SessionState.CharucoTransform.rotation);
        Debug.Log("Lablight: origin position " + originInstance.transform.position);
    }

    void OnUpdatedHands(XRHandSubsystem subsystem, XRHandSubsystem.UpdateSuccessFlags updateSuccessFlags, XRHandSubsystem.UpdateType updateType)
    {
        switch(updateType)
        {
            case XRHandSubsystem.UpdateType.Dynamic:
                //initially just check middle finger position
                var trackingDataRightMiddleProximal = m_HandSubsystem.rightHand.GetJoint(XRHandJointID.MiddleProximal);
                //var trackingDataLeftMiddleProximal = m_HandSubsystem.leftHand.GetJoint(XRHandJointID.MiddleProximal);
                if(!inCalibration)
                {
                    Pose rightMiddleProximalPose;
                    //Pose leftMiddleProximalPose;
                    //check if middle finger of either hand is above plane to determine which hand operator is using. If both hands are above plane do not enter calibration
                    if(trackingDataRightMiddleProximal.TryGetPose(out Pose poseRight))
                    {
                        rightMiddleProximalPose = poseRight;
                        if(Physics.Raycast(rightMiddleProximalPose.position, Vector3.down, out RaycastHit hit, 1f))
                        {
                            if(hit.collider.TryGetComponent<ARPlane>(out ARPlane hitPlane))
                            {
                                if((hitPlane.classifications & calibrationPlanesClassifications) != 0)
                                {
                                    if(planeSelected == null)
                                    {
                                        planeSelected = hitPlane;
                                        planeSelected.GetComponent<MeshRenderer>().SetMaterials(new List<Material>(){ planeMaterial });
                                    }else if(planeSelected != hitPlane)
                                    {
                                        planeSelected.GetComponent<MeshRenderer>().SetMaterials(new List<Material>() { removePlaneMaterial });
                                        planeSelected = hitPlane;
                                    }
                                }
                            }
                        }else if(planeSelected != null)//not above a plane and not in calibration
                        {
                            planeSelected.GetComponent<MeshRenderer>().SetMaterials(new List<Material>(){ removePlaneMaterial });
                            planeSelected = null;
                        }
                    }
                    // if(trackingDataLeftMiddleProximal.TryGetPose(out Pose poseLeft))
                    // {
                    //     leftMiddleProximalPose = poseLeft;
                    //     if(Physics.Raycast(leftMiddleProximalPose.position, Vector3.down, out RaycastHit hit, 1f))
                    //     {
                    //         if(hit.collider.TryGetComponent<ARPlane>(out ARPlane hitPlane))
                    //         {
                    //             if(availableCalibrationPlanes.Contains(hitPlane))
                    //             {
                    //                 usingLeftHand = true;
                    //             }
                    //         }
                    //     }
                    // }

                }
                foreach(XRHandJointID jointID in calibrationJoints) // only want joints we care about
                {

                    var trackingDataRight = m_HandSubsystem.rightHand.GetJoint(jointID);
                    //var trackingDataLeft = m_HandSubsystem.leftHand.GetJoint(jointID);

                    if (trackingDataRight.TryGetPose(out Pose poseRight))
                    {
                        calibrationJointsPoseDict[jointID] = poseRight;
                    }

                    // if(trackingDataLeft.TryGetPose(out Pose poseLeft))
                    // {

                    // }
                }
                if(planeSelected != null && calibrationJointsPoseDict.Count == calibrationJoints.Length && !inCalibration)
                {
                    inCalibration = true;
                    StartCalibrationOnPlane();
                }
            break;
        }
    }

    private void StartCalibrationOnPlane()
    {
        calibrationManager.UpdateCalibrationStatus("Plane selected, awaiting hand placement");


        foreach(KeyValuePair<XRHandJointID, Pose> joint in calibrationJointsPoseDict)
        {
            if(Math.Abs(joint.Value.position.y - planeSelected.center.y) > distanceToPlaneThreshold)
            {
                inCalibration = false;
                return;
            }
        }

        //if calibration joints are within distance threshold y of plane, start calibration
        //check if all calibration joints are within distance threshold y of plane
        //only start calibration process if all joints are within distance threshold y of plane
        calibrationManager.CalibrationStarted(inCalibration);
        calibrationManager.UpdateCalibrationStatus("Starting calibration");
        if(matrixCoroutine == null)
        {
            matrixCoroutine = StartCoroutine(getMatrixFromHandPosition());
        }else
        {
            Debug.Log("Calibration coroutine already running");
        }
    }

    private void DeactivateFingerPoints(List<GameObject> fingerPoints)
    {
        foreach(GameObject fingerPoint in fingerPoints)
        {
            Destroy(fingerPoint);
        }
    }

    private IEnumerator getMatrixFromHandPosition()
    {
        Debug.Log("getMatrixFromHandPosition: Starting calibration");
        calibrationManager.UpdateCalibrationStatus("Calibration in progress");

        yield return new WaitForSeconds(1.5f); // to remove, debug waiting 1 second to ensure optimal hand position
        //call send data to lighthouse

        Pose[] initialJointPositions = calibrationJointsPoseDict.Values.ToArray();

        List<GameObject> fingerPoints = new List<GameObject>();
        foreach (XRHandJointID joint in calibrationJoints)
        {
            if(HasMovedOutOfDistance(initialJointPositions, calibrationJointsPoseDict.Values.ToArray())) //wait 2 seconds for hand position to stabalize 
            {
                calibrationManager.UpdateCalibrationStatus("Calibration failed please place hands on plane");
                inCalibration = false;
                DeactivateFingerPoints(fingerPoints);
                matrixCoroutine = null;
                StartCalibrationOnPlane();
                yield break;
            }
            fingerPoints.Append(Instantiate(jointPrefab, calibrationJointsPoseDict[joint].position, calibrationJointsPoseDict[joint].rotation));
            calibrationManager.UpdateCalibrationStatus("getMatrixFromHandPosition: Instantiating joint " + joint);
            yield return new WaitForSeconds(0.25f); //reducing time to 0.25f from 0.5f AM
        }
        Debug.Log("getMatrixFromHandPosition: instantiated all joints");
        Debug.Log("Index Proximal " + calibrationJointsPoseDict[XRHandJointID.IndexProximal].position.x + "," + planeSelected.transform.position.y + "," +calibrationJointsPoseDict[XRHandJointID.IndexProximal].position.z);
        Debug.Log("Middle Proximal " + calibrationJointsPoseDict[XRHandJointID.MiddleProximal].position.x + "," + planeSelected.transform.position.y + "," +calibrationJointsPoseDict[XRHandJointID.MiddleProximal].position.z);
        Debug.Log("Ring Proximal " + calibrationJointsPoseDict[XRHandJointID.RingProximal].position.x + "," + planeSelected.transform.position.y + "," +calibrationJointsPoseDict[XRHandJointID.RingProximal].position.z);
        Debug.Log("Little Proximal " + calibrationJointsPoseDict[XRHandJointID.LittleProximal].position.x + "," + planeSelected.transform.position.y + "," +calibrationJointsPoseDict[XRHandJointID.LittleProximal].position.z);
        Debug.Log("Index Tip " + calibrationJointsPoseDict[XRHandJointID.IndexTip].position.x + "," + planeSelected.transform.position.y + "," +calibrationJointsPoseDict[XRHandJointID.IndexTip].position.z);
        Debug.Log("Middle Tip " + calibrationJointsPoseDict[XRHandJointID.MiddleTip].position.x + "," + planeSelected.transform.position.y + "," +calibrationJointsPoseDict[XRHandJointID.MiddleTip].position.z);
        Debug.Log("Ring Tip " + calibrationJointsPoseDict[XRHandJointID.RingTip].position.x + "," + planeSelected.transform.position.y + "," +calibrationJointsPoseDict[XRHandJointID.RingTip].position.z);
        Debug.Log("Little Tip " + calibrationJointsPoseDict[XRHandJointID.LittleTip].position.x + "," + planeSelected.transform.position.y + "," +calibrationJointsPoseDict[XRHandJointID.LittleTip].position.z);

        Matrix4x4 calibrationMatrix = CalibrationFromMatrix.Calculate_Hand_Coordinate_System_Transform(true, 
            calibrationJointsPoseDict[XRHandJointID.IndexProximal].position.x, planeSelected.transform.position.y, calibrationJointsPoseDict[XRHandJointID.IndexProximal].position.z,
            calibrationJointsPoseDict[XRHandJointID.MiddleProximal].position.x, planeSelected.transform.position.y, calibrationJointsPoseDict[XRHandJointID.MiddleProximal].position.z,
            calibrationJointsPoseDict[XRHandJointID.RingProximal].position.x, planeSelected.transform.position.y, calibrationJointsPoseDict[XRHandJointID.RingProximal].position.z,
            calibrationJointsPoseDict[XRHandJointID.LittleProximal].position.x, planeSelected.transform.position.y, calibrationJointsPoseDict[XRHandJointID.LittleProximal].position.z,
            calibrationJointsPoseDict[XRHandJointID.IndexTip].position.x, planeSelected.transform.position.y, calibrationJointsPoseDict[XRHandJointID.IndexTip].position.z,
            calibrationJointsPoseDict[XRHandJointID.MiddleTip].position.x, planeSelected.transform.position.y, calibrationJointsPoseDict[XRHandJointID.MiddleTip].position.z,
            calibrationJointsPoseDict[XRHandJointID.RingTip].position.x, planeSelected.transform.position.y, calibrationJointsPoseDict[XRHandJointID.RingTip].position.z,
            calibrationJointsPoseDict[XRHandJointID.LittleTip].position.x, planeSelected.transform.position.y, calibrationJointsPoseDict[XRHandJointID.LittleTip].position.z
        );
        ServiceRegistry.GetService<ILighthouseControl>()?.RequestLighthouseCalibration(2, 0);
        SessionManager.instance.UpdateCalibration(calibrationMatrix);
        DeactivateFingerPoints(fingerPoints);
        yield return new WaitForSeconds(2f); //wait for finger points to deactivate
        CompleteCalibration();
        yield return null;
    }

    public void CompleteCalibration()
    {
        inCalibration = false;
        calibrationManager.UpdateCalibrationStatus("Calibration complete");
        calibrationManager.CalibrationStarted(inCalibration);
        //ARAnchor anchor = anchorManager.AttachAnchor(planeSelected, calibrationPose);
        var originInstance = Instantiate(originPrefab, SessionState.CharucoTransform.position, SessionState.CharucoTransform.rotation);

        Debug.Log("Lablight: origin position " + originInstance.transform.position);

        if(m_HandSubsystem != null)
        {
            m_HandSubsystem.updatedHands -= OnUpdatedHands;
        }

        planeSelected.GetComponent<MeshRenderer>().SetMaterials(new List<Material> () { removePlaneMaterial });
        planeSelected = null;
        StartCoroutine(UnloadCalibration());
    }

    public bool HasMovedOutOfDistance(Pose[] initialPositions, Pose[] currentPositions)
    {
        for (int i = 0; i < initialPositions.Length; i++)
        {
            if (Vector3.Distance(initialPositions[i].position, currentPositions[i].position) > calibrationDistanceThreshold)
            {
                Debug.Log("HasMovedOutOfDistance: Hand detected out of distance by " + Vector3.Distance(initialPositions[i].position, currentPositions[i].position) + " units");
                return true;
            }
        }
        return false;
    }

}
