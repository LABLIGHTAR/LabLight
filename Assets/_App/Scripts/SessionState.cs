using UniRx;
using UnityEngine;
using Lighthouse.MessagePack;
using UnityEngine.Events;
using System.Collections.Generic;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Central state containing observable values
/// </summary>
public class SessionState : MonoBehaviour
{
    #region Singleton and Core Lifecycle
    public static SessionState Instance;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Multiple SessionState instances detected. Destroying duplicate (newest).");
            DestroyImmediate(gameObject);
        }
    }
    #endregion

    #region User and Session Info
    public static string deviceId;
    public static LocalUserProfileData currentUserProfile;
    public static string PendingDisplayName = null;//temp for storage during async registration
    public static string PendingEmail = null;//temp for storage during async registration
    public static string FirebaseUserId = null;
    public static string SpacetimeIdentity = null;
    #endregion

    #region Spatial Transforms
    public static Transform WorkspaceTransform = null;
    public static Transform CharucoTransform = null;
    #endregion

    #region Calibration State
    public static UnityEvent onCalibrationUpdated = new UnityEvent();
    /// <summary>
    /// Keep track of the last settings that where last used for detection so we can detect if Lighthouse changed settings in the meantime 
    /// </summary>
    public static ArucoSettings LastUsedArucoSettings;
    /// Flag that indicates if lighthouse was calibrated with different Charuco settings than the last Charuco settings used on HoloLens
    public static ReactiveProperty<bool> CalibrationDirty = new ReactiveProperty<bool>();
    #endregion

    #region Connection and Recording State
    private static bool _connectedToLighthouse = false;
    private static bool _recording;
    
    // Data streams typed bus where required
    public static Subject<bool> connectedStream = new Subject<bool>();
    public static Subject<bool> recordingStream = new Subject<bool>();

    //setters
    public static bool Connected
    {
        set
        {
            if (_connectedToLighthouse != value)
            {
                _connectedToLighthouse = value;
                if(_connectedToLighthouse)
                {
                    Debug.Log("requesting aruco settings");
                    ServiceRegistry.GetService<ILighthouseControl>()?.RequestArucoSettings();
                }
                connectedStream.OnNext(value);
            }
        }
        get
        {
            return _connectedToLighthouse;
        }
    }

    public static bool Recording
    {
        set
        {
            if (_recording != value)
            {
                _recording = value;
                recordingStream.OnNext(value);
            }
        }
        get
        {
            return _recording;
        }
    }
    #endregion

    #region Visualization and UI State
    public static ReactiveProperty<bool> enableGenericVisualizations = new ReactiveProperty<bool>();
    public static ReactiveProperty<bool> ShowWorkspaceOrigin = new ReactiveProperty<bool>();
    #endregion

    #region Aruco and Tracking State
    public static ReactiveProperty<ArucoSettings> ArucoSettings = new ReactiveProperty<ArucoSettings>();
    public static ReactiveCollection<TrackedObject> TrackedObjects = new ReactiveCollection<TrackedObject>();
    #endregion

    #region Spatial Notes State
    public static ReactiveProperty<bool> SpatialNoteEditMode = new ReactiveProperty<bool>();
    public static ReactiveCollection<AnchoredObjectController> SpatialNotes = new ReactiveCollection<AnchoredObjectController>();
    #endregion
    
    #region File Download State
    // /// CSV file that is marked as available for download 
    public static ReactiveProperty<string> CsvFileDownloadable = new ReactiveProperty<string>();
    public static ReactiveProperty<string> JsonFileDownloadable = new ReactiveProperty<string>();
    #endregion
}
