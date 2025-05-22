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
    public static SessionState Instance;
    public static string deviceId;
    public static LocalUserProfileData currentUserProfile;

    private static bool _connected = false;
    private static bool _recording;

    public static UnityEvent onCalibrationUpdated = new UnityEvent();

    
    public static ReactiveProperty<bool> enableGenericVisualizations = new ReactiveProperty<bool>();

    public static ReactiveProperty<bool> ShowWorkspaceOrigin = new ReactiveProperty<bool>();

    // Data streams typed bus where required
    public static Subject<bool> connectedStream = new Subject<bool>();
    public static Subject<bool> recordingStream = new Subject<bool>();
    public static ReactiveProperty<ArucoSettings> ArucoSettings = new ReactiveProperty<ArucoSettings>();
    public static ReactiveCollection<TrackedObject> TrackedObjects = new ReactiveCollection<TrackedObject>();

    public static ReactiveProperty<bool> SpatialNoteEditMode = new ReactiveProperty<bool>();
    public static ReactiveCollection<AnchoredObjectController> SpatialNotes = new ReactiveCollection<AnchoredObjectController>();
    

    /// <summary>
    /// Keep track of the last settings that where last used for detection so we can detect if Lighthouse changed settings in the meantime 
    /// </summary>
    public static ArucoSettings LastUsedArucoSettings;

    /// Flag that indicates if lighthouse was calibrated with different Charuco settings than the last Charuco settings used on HoloLens
    public static ReactiveProperty<bool> CalibrationDirty = new ReactiveProperty<bool>();

    // /// CSV file that is marked as available for download 
    public static ReactiveProperty<string> CsvFileDownloadable = new ReactiveProperty<string>();

    public static ReactiveProperty<string> JsonFileDownloadable = new ReactiveProperty<string>();

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

    //setters
    public static bool Connected
    {
        set
        {
            if (_connected != value)
            {
                _connected = value;
                if(_connected)
                {
                    Debug.Log("requesting aruco settings");
                    ServiceRegistry.GetService<ILighthouseControl>()?.RequestArucoSettings();
                }
                connectedStream.OnNext(value);
            }
        }
        get
        {
            return _connected;
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
}
