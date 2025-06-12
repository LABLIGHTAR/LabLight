using Battlehub.Dispatcher;
using Lighthouse.MessagePack;
using UnityEngine;
using System;

/// <summary>
/// Class Networking
/// 
/// ILighthouseControl implementation
public partial class Networking : MonoBehaviour, ILighthouseControl
{
    /// <summary>Request start recording on lighthouse server.</summary>
    public void StartRecordingVideo()
    {
        if (!string.IsNullOrEmpty(_directIpAddress))
        {
            SessionState.Recording = true;
            string filename = ProtocolState.Instance.ProtocolTitle.Value + "_step" + ProtocolState.Instance.CurrentStep.Value;
            var packetType = packet_type.packet_client_video_start_recording;
            PingServer(packetType, filename, 0f);
        }
        else
        {
            Debug.LogWarning("Not connected yet");
        }
    }

    /// <summary>Request stop recording on lighthouse server.</summary>
    public void StopRecordingVideo()
    {
        if (!string.IsNullOrEmpty(_directIpAddress))
        {
            SessionState.Recording = false;
            var packetType = packet_type.packet_client_video_stop_recording;
            PingServer(packetType);
        }
        else
        {
            Debug.LogWarning("Not connected yet");
        }
    }

    //start timer
    void ILighthouseControl.StartTimer(int sec)
    {
        if(!string.IsNullOrEmpty(_directIpAddress))
        {
            // SessionState.Timer.Value = true;
            // var packetType = packet_type.packet_client_start_timer;
            // PingServer(packetType, sec, 1, 1);
        }
        else
        {
            Debug.LogWarning("Not connected yet");
        }
    }
    //stop timer
    void ILighthouseControl.StopTimer()
    {
        if(!string.IsNullOrEmpty(_directIpAddress))
        {
            // SessionState.Timer.Value = false;
            // var packetType = packet_type.packet_client_stop_timer;
            // PingServer(packetType);
        }
        else
        {
            Debug.LogWarning("Not connected yet");
        }
    }

    void ILighthouseControl.DetectorMode(int detector, int mode, float fps)
    {
        if(!string.IsNullOrEmpty(_directIpAddress))
        {
            var packetType = packet_type.packet_client_detector_mode;
            PingServer(packetType, detector, mode, fps);
        }
    }
    /// <summary>Request start playing on lighthouse server.</summary>
    void ILighthouseControl.StartPlayingVideo()
    {
        if (!string.IsNullOrEmpty(_directIpAddress))
        {
            Debug.Log("Start Replay Video command called");
            var packetType = packet_type.packet_client_video_replay_start;
            PingServer(packetType);
        }
        else
        {
            Debug.LogWarning("Not connected yet");
        }
    }

    /// <summary>Request stop playing on lighthouse server.</summary>
    void ILighthouseControl.StopPlayingVideo()
    {
        Debug.Log("Stop Replay Video command called");
        var packetType = packet_type.packet_client_video_replay_stop;
        PingServer(packetType);
    }

    void ILighthouseControl.RequestLighthouseCalibration(int align_type, int markerID)
    {
        if(!string.IsNullOrEmpty(_directIpAddress))
        {
            Debug.Log("Recalibrate lighthouse");
            PingServer(packet_type.packet_client_request_alignment, align_type, markerID);
        }
        else
        {
            Debug.LogWarning("Not connected yet");
        }
    }

    // /// <summary>Request recalibration on lighthouse server.</summary>
    // void ILighthouseControl.RequestLighthouseCalibration()
    // {
    //     if (!string.IsNullOrEmpty(_directIpAddress))
    //     {
    //         Debug.Log("Recalibrate lighthouse");
    //         PingServer(packet_type.packet_client_request_alignment);
    //     }
    //     else
    //     {
    //         Debug.LogWarning("Not connected yet");
    //     }
    // }

    void ILighthouseControl.RequestArucoSettings()
    {
        if (!string.IsNullOrEmpty(_directIpAddress))
        {
            Debug.Log("RequestAruco Settings");
            PingServer(packet_type.packet_client_request_settings_aruco);
        }
        else
        {
            Debug.LogWarning("Not connected yet");
        }
    }

    void ILighthouseControl.ResetTrackedObjects()
    {
        lock (TrackedObjectDictionary)
        {
            TrackedObjectDictionary.Clear();
        }

        Dispatcher.Current.BeginInvoke(() =>
        {
            //SessionState.TrackedObjects.Clear();
        });
    }

    void ILighthouseControl.Request_Deep_Models()
    {
        if(!string.IsNullOrEmpty(_directIpAddress))
        {
            Debug.Log("Request Deep Models");
            PingServer(packet_type.packet_client_request_settings_deep_models);
        }
        else
        {
            Debug.LogWarning("Not connected yet");
        }
    }

    public string GetBuildVersion()
    {
        return Networking.BuildVersion;
    }

    public bool IsInitialized()
    {
        return !string.IsNullOrEmpty(_directIpAddress);
    }

    void ILighthouseControl.SetFileRecieveFolder(string recieveFolderPath)
    {
        if(!string.IsNullOrEmpty(_directIpAddress))
        {
            var packetType = packet_type.packet_client_set_file_reveice_folder;
            PingServer(packetType, recieveFolderPath);
        }
    }

    void ILighthouseControl.SetProtocolStatus()
    {
        if(!string.IsNullOrEmpty(_directIpAddress))
        {
            var packetType = packet_type.packet_client_protocol_state;
            var protocolState = ProtocolState.Instance;
            var currentStep = protocolState.CurrentStepState.Value;
            var currentCheckItem = protocolState.CurrentCheckItemState.Value;

            PingServer
            (
                packetType, 
                protocolState.ProtocolTitle.Value, 
                "operatorName", 
                protocolState.StartTime.Value.ToString(), 
                protocolState.CurrentStep.Value + 1, 
                protocolState.Steps.Count, 
                protocolState.CurrentCheckNum + 1, 
                currentStep?.Checklist?.Count ?? 0,
                currentCheckItem?.Text ?? "", 
                currentStep?.SignedOff.Value ?? false ? 1 : 0,
                false //resume value
            );
        }
    }

    void ILighthouseControl.ResetProtcolStatus()
    {
        if(!string.IsNullOrEmpty(_directIpAddress))
        {
            var packetType = packet_type.packet_client_protocol_state;
            PingServer
            (
                packetType, 
                "", 
                "", 
                "", 
                0, 
                0, 
                0, 
                0, 
                "", 
                0
            );
        }
    }

    public string GetFileServerUri()
    {
        if (!string.IsNullOrEmpty(_directIpAddress))
        {
            return string.Format("http://{0}:{1}", _directIpAddress, _fileServerPort);
        }

        return string.Empty;
    }

    private void updateDeepModels(DeepModelSettings deepModelSettings)
    {
        Debug.Log("Updating deep models");
        Debug.Log("models detected: " + deepModelSettings.Num_Models);
        for(int i = 0; i < (int)deepModelSettings.Num_Models; i ++)
        {
            Debug.Log(deepModelSettings.models[i]);
        }
    }
}