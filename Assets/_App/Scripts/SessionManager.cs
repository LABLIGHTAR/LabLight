using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Lighthouse.MessagePack;

public class SessionManager : MonoBehaviour
{
    public static SessionManager instance;

    private ARAnchorManager anchorManager;

    private static Transform workspaceTransform;

    public Transform WorkspaceTransform
    {
        set
        {
            if(workspaceTransform != value)
            {
                workspaceTransform = value;
            }
        }
        get
        {
            return workspaceTransform;
        }
    }

    //for testing AM
    public List<TrackedObject> TrackedObjectsDebug = new List<TrackedObject>();

    [SerializeField]
    private static Transform charucoTransform;
    public Transform CharucoTransform
    {
        set
        {
            if(charucoTransform != value)
            {
                charucoTransform = value;
            }
        }
        get
        {
            return charucoTransform;
        }
    }

    public void Awake()
    {
        if(instance == null)
        {
            instance = this;
        }
        else
        {
            Debug.LogWarning("Multiple SessionManager instances detected. Destroying duplicate (newest).");
            DestroyImmediate(gameObject);
        }

        anchorManager = this.transform.parent.GetComponent<ARAnchorManager>();

        //planeViewManager = this.transform.GetComponent<ARPlaneViewController>();

        //anchorManager.enabled = false;

        var congintoAuthProvider = new CognitoAuthProvider();
        ServiceRegistry.RegisterService<IUserAuthProvider>(congintoAuthProvider);

        var localFileDataProvider = new LocalFileDataProvider();
        ServiceRegistry.RegisterService<ITextDataProvider>(localFileDataProvider);
        ServiceRegistry.RegisterService<IAnchorDataProvider>(localFileDataProvider);
        ServiceRegistry.RegisterService<IUserProfileDataProvider>(localFileDataProvider);

        var resourceFileDataProvider = new ResourceFileDataProvider();
        ServiceRegistry.RegisterService<IProtocolDataProvider>(resourceFileDataProvider);
        ServiceRegistry.RegisterService<IMediaProvider>(resourceFileDataProvider);

        var llmChatProvider = new ClaudeChatProvider();
        ServiceRegistry.RegisterService<ILLMChatProvider>(llmChatProvider);

        #if UNITY_VISIONOS && !UNITY_EDITOR
        var UIDriver = new SwiftUIDriver();
        ServiceRegistry.RegisterService<IUIDriver>(UIDriver);
        Destroy(GetComponent<UnityUIDriver>());
        #elif UNITY_EDITOR
        var UIDriver = GetComponent<UnityUIDriver>();
        ServiceRegistry.RegisterService<IUIDriver>(UIDriver);
        #endif
        UIDriver.DisplayUserSelection();

        //Set up default state
        SessionState.deviceId = SystemInfo.deviceName;
        SessionState.Connected = false;

        //for debug to remove AM
        charucoTransform = transform;
        //workspaceTransform = transform;

        charucoTransform = Instantiate(new GameObject("CharucoTransform"), transform.parent.transform).transform;

        //Setup logger
        /* Add service 
         * debug
         * voice controller
         * audio manager
         * well plate csv provider
         * file upload handler
         */
    }
    public void Update()
    {
        TrackedObjectsDebug = SessionState.TrackedObjects.ToList();
    }

    public void UpdateCalibration(Matrix4x4 pose)
    {
        if (CharucoTransform == null)
        {
            Debug.LogError("Missing CharucoTransform on SessionManager");
            return;
        }

        // Set the stage coordinate frame
        CharucoTransform.FromMatrix(pose);

        Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);
        CharucoTransform.rotation *= rotation;
        SessionState.onCalibrationUpdated.Invoke();
    }
}
