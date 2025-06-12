using Battlehub.Dispatcher;
using Lighthouse.MessagePack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

/// <summary>
/// Class Networking (previously AcamNetworkDiscovery)
/// 
/// Connects to lighthouse and updates SessionState with TrackedObjects
/// 
/// This class is split up across multiple files:
/// See Networking.Lighthouse for ILighthouse implemtation
[System.Serializable]
public partial class Networking : MonoBehaviour
{
    [SerializeField] NetworkingDebugViewController DebugView;

    private double _lastAcamTimeStamp;

    [SerializeField]
    private double ObjectPruningTimeoutInSeconds = 1;

    [SerializeField]
    protected volatile bool _isClientMode = true;

    private volatile bool _isListening = false;
    private Thread _listenThread;
    private System.Timers.Timer _pingTimer;

    private bool _firstReceivedBroadcastFlag;
    private System.DateTime _receivedLastPacketTime;

    private byte _packetVersionNumber;

    [HideInInspector]
    public static string BuildVersion;

    /// <summary>
    /// Local dictionary that maps trackedobject id to trackedobject
    /// </summary>
    private Dictionary<int, TrackedObject> TrackedObjectDictionary = new Dictionary<int, TrackedObject>();

    [SerializeField]
    protected int _broadcastPort = 8888;
    protected int _directPort = 8888;
    protected int _fileServerPort = 8080;
    protected string _directIpAddress = "";
    private int _targetPacketVersionNumber = 16;

    /// <summary>Occurs when [a data packet is received].</summary>
    public event EventHandler<(byte, object[])> ReceivePacket;

    protected UdpClient _udpClient;
    protected IPEndPoint _serverEndPoint;

    protected bool _isServerMode
    {
        get { return !_isClientMode; }
    }

    /// <summary>Gets or sets a value indicating whether this instance is client mode.</summary>
    /// <value>
    ///   <c>true</c> if this instance is client mode; otherwise, <c>false</c>.</value>
    public bool IsClientMode
    {
        get { return _isClientMode; }
        set { if (_isClientMode == value) return; _isClientMode = value; }
    }

    /// <summary>Enum packet_type</summary>
    public enum packet_type
    {
        //// **** from server to client
        packet_server_broadcast_address = 0,                // v1   Broadcast of server IP and port
        packet_server_sensor_transform_quat = 1,            // v1   Not handled
        packet_server_table_plane_quat = 2,                 // v1   Not handled
        packet_server_aruco_markers = 3,                    // v1   Not handled
        packet_server_charuco_board_quat = 4,               // v1   Updated position and orientation of charucoBoard in quaternions
        packet_server_2d_codes = 5,                         // v1   Not handled
        packet_server_detection_quat = 6,                   // v1   Updated list of detected objects

        packet_server_video_recording_started = 7,          // v2 Not handled
        packet_server_video_recording_stopped = 8,          // v2 Not handled
        packet_server_video_replay_started = 9,             // v2 Not handled
        packet_server_video_replay_stopped = 10,            // v2 Not handled

        // WebRTC
        packet_server_webrtc_SDP_description = 20,          // v3 Not handled
        packet_server_webrtc_candidate = 21,                // v3 Not handled
        packet_server_webrtc_request_SDP_description = 22,  // v3 Not handled
        packet_server_webrtc_request_candidate = 23,        // v3 Not handled

        packet_server_settings_table = 24,                  // v4 Not handled
        packet_server_settings_aruco = 25,                  // v4 Stored in SessionState
        packet_server_settings_codes = 26,                  // v4 Not handled
        packet_server_settings_deep = 27,                   // v4 Not handled
        packet_server_settings_deep_models = 28,            // v11 Handled

        packet_server_sensor_transform_mat = 31,            // v5 Not handled
        packet_server_table_plane_mat = 32,                 // v5 Not handled
        packet_server_aruco_marker_mat = 33,                // v5 Not handled
        packet_server_charuco_board_mat = 34,               // v4 Not handled
        packet_server_2d_code_mat = 35,                     // v5 Not handled
        packet_server_detection_mat = 36,                   // v4 Not handled

        packet_server_sensor_transform_vecs = 41,           // v5 Not handled
        packet_server_table_plane_vecs = 42,                // v5 Not handled
        packet_server_aruco_marker_vecs = 43,               // v5 Not handled
        packet_server_charuco_board_vecs = 44,              // v4 Not handled
        packet_server_2d_code_vecs = 45,                    // v5 Not handled
        packet_server_detection_vecs = 46,                  // v4 Not handled

        packet_server_hand = 60,		                    // v7 Not handled
        packet_server_contrastive = 61,                     // v8 Not handled
        packet_server_csv_file_available = 62,		        // v9 Handled
        packet_server_json_file_available = 63,             // v15 Handled

        //// **** from client to server
        packet_client_ping = 100,                           // v1   Regular ping message to inform lighthouse that this client is still running
        packet_client_request_alignment = 101,              // v1   Request lighthouse to perform aruce/charuco marker detection
        packet_client_set_file_reveice_folder = 102,        // v12 Command to set file recieve folder path in Lighthouse

        packet_client_protocol_state = 111,		            // v13 send current protocol state to Lighthouse

        packet_client_start_timer = 150,                    // v10 see StartTimer function in Networking.Lighthouse
        packet_client_stop_timer = 151,                     // v10 see StopTimer function in Networking.Lighthouse

        // Control Lighthouse settings from HL
        packet_client_send_settings_table = 200,            // v2 Not used
        packet_client_request_settings_table = 201,         // v4 Not used
        packet_client_send_settings_aruco = 300,            // v2 Not used
        packet_client_request_settings_aruco = 301,         // v4 Not used
        packet_client_send_settings_codes = 400,            // v2 Not used
        packet_client_request_settings_codes = 401,         // v4 Not used
        packet_client_send_settings_deep = 500,             // v2 Not used
        packet_client_request_settings_deep = 501,          // v4 Not used
        packet_client_request_settings_deep_models = 502,

        packet_client_detector_mode = 550,                  // v10 see DetectorMode in Networking.Lighthouse

        packet_client_video_start_recording = 600,          // v2 Command to start recording video in Lighthouse
        packet_client_video_stop_recording = 601,           // v2 Command to stop recording video in Lighthouse

        packet_client_video_replay_start = 700,             // v2 Command to start previously recorded video in Lighthouse
        packet_client_video_replay_stop = 701,              // v2 Command to stop previously recorded video in Lighthouse

        // WebRTC
        packet_client_webrtc_SDP_description = 800,         // v3 Not used
        packet_client_webrtc_candidate = 801,               // v3 Not used
        packet_client_webrtc_request_SDP_description = 802, // v3 Not used
        packet_client_webrtc_request_candidate = 803,       // v3 Not used
    };

    /// <summary>Called when packet is received].</summary>
    /// <param name="packetType">Type of the packet.</param>
    /// <param name="data">The data.</param>
    private void OnReceivedPacket(byte packetType, object[] data)
    {
        _receivedLastPacketTime = DateTime.Now;

        if (packetType == 0)
        {
            // broadcast message sends packet version as 6th element
            _packetVersionNumber = (byte)data[5];
        }
        else
        {
            // all other messages send packet version as 2nd element
            _packetVersionNumber = (byte)data[1];
        }

        // filter for the correct packet version number, ignore others
        if (_packetVersionNumber > _targetPacketVersionNumber)
        {
            Debug.Log($"Ignoring packet with version {_packetVersionNumber} since target is {_targetPacketVersionNumber}. Your Lighthouse app is newer then anticipated.");
            return;
        }


        // Filter broadcast messages
        if (packetType > 0)
        {
            Debug.Log($"Received packet of type  " + (int)packetType);
        }

        switch (packetType)
        {
            case (byte)packet_type.packet_server_broadcast_address:
                var address = (BroadcastAddress)data;

                _directIpAddress = address.DirectIpAddress;
                _directPort = address.DirectPort;
                _packetVersionNumber = address.PacketVersion;
                BuildVersion = address.BuildName;


                if (!_firstReceivedBroadcastFlag)
                {
                    Debug.Log($"broadcast address {address.ServerName} - {_directIpAddress}:{_directPort}");
                    _firstReceivedBroadcastFlag = true;

                    _serverEndPoint = new IPEndPoint(IPAddress.Parse(_directIpAddress), _directPort);
                    //SwitchToDirectListening();
                    PingServer(packet_type.packet_client_request_alignment);
                }
                break;
            case (byte)packet_type.packet_server_2d_codes:
                Debug.Log($"Lighthouse 2d code detected");
                break;
            case (byte)packet_type.packet_server_aruco_markers:
                Debug.Log($"Lighthouse Aruco detected");
                break;
            case (byte)packet_type.packet_server_aruco_marker_mat:
                Debug.Log("Lighthouse hand detected");
                var hand = (HandData)data;
                UpdatedLighthouseHand(hand);
                break;
            case (byte)packet_type.packet_server_charuco_board_quat:
            case (byte)packet_type.packet_server_charuco_board_mat:
            case (byte)packet_type.packet_server_charuco_board_vecs:
                Debug.Log($"Lighthouse Charuco detected");
                UpdatedLighthouseCharuco((CharucoBoard)data);
                break;
            case (byte)packet_type.packet_server_detection_quat:
            case (byte)packet_type.packet_server_detection_mat:
            case (byte)packet_type.packet_server_detection_vecs:
                var detection = (DetectedObject)data;
                UpdateTrackedObjectDictionary(detection.TimeStamp, detection.TrackingId, detection.ClassId, detection.ClassName, detection.Center, detection.ContourPoints, detection.Color, detection.Bounds);
                break;
            case (byte)packet_type.packet_server_sensor_transform_quat:
            case (byte)packet_type.packet_server_sensor_transform_mat:
            case (byte)packet_type.packet_server_sensor_transform_vecs:
                // sensor data currently does nothing but is ready to use
                var sensor = (Sensor)data;
                Debug.Log($"Lighthouse sensor transform detected");
                break;
            case (byte)packet_type.packet_server_table_plane_quat:
            case (byte)packet_type.packet_server_table_plane_mat:
            case (byte)packet_type.packet_server_table_plane_vecs:
                // table data currently does nothing but is ready to use
                var table = (Table)data;
                Debug.Log($"Lighthouse table plane detected");
                break;
            case (byte)packet_type.packet_server_settings_table:
                Debug.Log($"Lighthouse table detection settings updated");
                break;
            case (byte)packet_type.packet_server_settings_aruco:
                Debug.Log($"Lighthouse aruco detection settings updated");
                Dispatcher.Current.BeginInvoke(() =>
                {
                    //TEMP last used aruco settins should be saved during calibration when aruco settings are requested
                    if(SessionState.LastUsedArucoSettings != null && SessionState.ArucoSettings.Value != null)
                    {
                        SessionState.LastUsedArucoSettings = SessionState.ArucoSettings.Value;
                    }
                    else
                    {
                        SessionState.LastUsedArucoSettings = (ArucoSettings)data;
                    }
                    SessionState.ArucoSettings.Value = (ArucoSettings)data;
                });
                break;
            case (byte)packet_type.packet_server_settings_codes:
                Debug.Log($"Lighthouse server settings updated");
                break;
            case (byte)packet_type.packet_server_settings_deep:
                Debug.Log($"Lighthouse deep learning detection settings updated");
                break;
            case (byte)packet_type.packet_server_settings_deep_models:
                //process txt of model list here and use to confirm relevant models
                updateDeepModels((DeepModelSettings)data);
                break;
            case (byte)packet_type.packet_server_video_recording_started:
                break;
            case (byte)packet_type.packet_server_video_recording_stopped:
                break;
            case (byte)packet_type.packet_server_video_replay_started:
                break;
            case (byte)packet_type.packet_server_video_replay_stopped:
                break;
            case (byte)packet_type.packet_server_csv_file_available:
                Debug.Log("Recieving packet_server_csv_file_available");
                UpdatedCsvAvailable((CsvFileInfo)data);
                break;
            case (byte)packet_type.packet_server_json_file_available:
                Debug.Log("Recieving packet_server_json_file_available");
                UpdatedJsonAvailable((JsonFileInfo)data);
                break;
            default:
                Debug.Log($"Lighthouse Unhandled packet type " + packetType);
                break;
        }
    }

    private void UpdatedLighthouseCharuco(CharucoBoard board)
    {
        Dispatcher.Current.BeginInvoke(() =>
        {
            // Check if client calibration settings match the settings used by Lighthouse
            bool equal = SessionState.ArucoSettings.Value == null ? false : (SessionState.ArucoSettings.Value.BoardNumX == SessionState.LastUsedArucoSettings.BoardNumX) &&
                            (SessionState.ArucoSettings.Value.BoardNumY == SessionState.LastUsedArucoSettings.BoardNumY) &&
                            (SessionState.ArucoSettings.Value.DictionaryType == SessionState.LastUsedArucoSettings.DictionaryType) &&
                            Mathf.Approximately(SessionState.ArucoSettings.Value.BoardSquareSize, SessionState.LastUsedArucoSettings.BoardSquareSize);
            SessionState.CalibrationDirty.Value = !equal;
        });
    }

    private void UpdatedLighthouseHand(HandData hand)
    {
        Dispatcher.Current.BeginInvoke(() =>
        {
            SessionState.CalibrationDirty.Value = true;
        });
    }

    private void UpdatedCsvAvailable(CsvFileInfo fileInfo)
    {
        Debug.Log("New Csv file available for download: " + fileInfo.FileName);
        Dispatcher.Current.BeginInvoke(() =>
        {
            SessionState.CsvFileDownloadable.Value = fileInfo.FileName;
        });
    }

    private void UpdatedJsonAvailable(JsonFileInfo fileInfo)
    {
        Debug.Log("New Json file available for download: " + fileInfo.FileName);
        Dispatcher.Current.BeginInvoke(() =>
        {
            SessionState.JsonFileDownloadable.Value = fileInfo.FileName;
        });
    }

    /// <summary>
    /// Writes to SessionState (should happen on Unity AppThread)
    /// 
    /// </summary>
    /// <param name="id"></param>
    /// <param name="center"></param>
    /// <param name="mask"></param>
    /// <param name="color"></param>
    protected void UpdateTrackedObjectDictionary(double timeStamp, int id, int classId, string className, Vector3 center, Vector3[] mask, Color color, Vector3[] bounds)
    {
        // RS We need to use a trackingId for matching earlier trackedobjects coming from Acam
        // Update() does a timeout based pruning of old objects
        TrackedObject trackedObject;
        if (!TrackedObjectDictionary.TryGetValue(id, out trackedObject))
        {
            trackedObject = new TrackedObject()
            {
                id = id,
                classId = classId,
                label = className,
                position = center,
                mask = mask,
                lastUpdate = DateTime.Now,
                color = color,
                bounds = bounds
            };

            lock (TrackedObjectDictionary)
            {
                TrackedObjectDictionary[id] = trackedObject;
            }

            Dispatcher.Current.BeginInvoke(() =>
            {
                SessionState.TrackedObjects.Add(trackedObject);
            });
        }
        else
        {
            // Reuse
            trackedObject.label = className;    // RS: Labels for a item may switch 
            trackedObject.position = center;
            trackedObject.mask = mask;
            trackedObject.lastUpdate = DateTime.Now;
            trackedObject.bounds = bounds;
        }

        if (timeStamp > _lastAcamTimeStamp)
        {
            // Cleanup based on automatic delta
            double delta = timeStamp - _lastAcamTimeStamp;


            if (ObjectPruningTimeoutInSeconds - delta <= 0)
            {
                PruneOldTrackedObjects(delta);
                _lastAcamTimeStamp = timeStamp;
            }
        }
    }

    /// <summary>Switches to direct listening.</summary>
    protected void SwitchToDirectListening()
    {
        try
        {
            Debug.Log("Switching to direct listening");
            Debug.Log("quit current listener");
            QuitListening();
            // allow time to quit
            Debug.Log("create new udp client");
            CreateUdpClient(_directPort);
            Debug.Log("start pinging server");
            StartPingingServer(2000);
            Debug.Log("listen to packets at new address");
            ListenForPackets(new IPEndPoint(IPAddress.Parse(_directIpAddress), _directPort));
        }
        catch (Exception ex)
        {
            Debug.LogError($"listening switch error: {ex}");
        }
    }

    /// <summary>Creates the UDP client.</summary>
    /// <param name="port">The port.</param>
    private void CreateUdpClient(int port)
    {
        try
        {
            _udpClient = new UdpClient(port);
        }
        catch (Exception ex)
        {
            Debug.LogError($"create udp client error: {ex.ToString()}");
        }
    }

    /// <summary>Listens for packets.</summary>
    /// <param name="remoteEndPoint">The remote end point.</param>
    protected void ListenForPackets(IPEndPoint remoteEndPoint)
    {
        Debug.Log($"Listener started at {remoteEndPoint.ToString()}");
        DebugView.Log($"Listener started at {remoteEndPoint.ToString()}");
        ReceivePacket += (a, b) => OnReceivedPacket(b.Item1, b.Item2);

        // create thread for reading UDP messages
        _listenThread = new Thread(new ThreadStart(delegate
        {
            while (_isClientMode && _isListening)
            {
                byte packetType = 0;
                try
                {
                    // receive bytes
                    byte[] bytes = _udpClient.Receive(ref remoteEndPoint);
                    var stream = new MemoryStream(bytes);
                    stream.Seek(0, SeekOrigin.Begin);
                    var data = MessagePack.MessagePackSerializer.Deserialize<object[]>(stream);
                    packetType = (byte)data[0];

                    stream.Close();
                    ReceivePacket?.Invoke(this, (packetType, data));
                }
                catch (ThreadAbortException)
                {
                    Debug.Log("thread aborted");
                }
                catch (Exception err)
                {
                    Debug.LogError($"reading err from {remoteEndPoint.ToString()} {err}");
                }
            }
            Debug.Log("Listener stopped");
        }));
        _isListening = true;
        _listenThread.IsBackground = true;
        _listenThread.Start();
    }
  

    /// <summary>Pings the server.</summary>
    /// <param name="packetType">Type of the packet.</param>
    private void PingServer(packet_type packetType = packet_type.packet_client_ping)
    {
        if (!IsInitialized())
        {
            return;
        }

        if (packetType == packet_type.packet_client_ping)
        {
            string localHostName = Dns.GetHostName();
            string localIP = Dns.GetHostEntry(localHostName).AddressList[0].ToString();
            PingServer(packetType, localIP, localHostName, _packetVersionNumber);
        }
        else if (packetType == packet_type.packet_client_request_alignment)
        {
            PingServer(packetType, 1, 1);
        }
        else
        {
            DebugView.Log("Sending " + packetType.ToString());
            var outputStream = new MemoryStream();
            object[] msg = new object[] { packetType };
            MessagePack.MessagePackSerializer.Serialize(outputStream, msg);
            outputStream.Position = 0;
            if (_udpClient != null)
            {
                _udpClient.Send(outputStream.ToArray(), (int)outputStream.Length, _serverEndPoint);
            }
        }
    }

    /// <summary>Pings the server.</summary>
    /// <param name="packetType">Type of the packet.</param>
    /// <param name="message">The message.</param>

    protected void PingServer(packet_type packetType = packet_type.packet_client_ping, params object[] message)
    {
        if (!IsInitialized())
        {
            return;
        }

        var outputStream = new MemoryStream();
        object[] msg = new object[] { packetType }.Concat(message).ToArray();
        MessagePack.MessagePackSerializer.Serialize(outputStream, msg);
        outputStream.Position = 0;
        if (_udpClient != null)
        {
            _udpClient.Send(outputStream.ToArray(), (int)outputStream.Length, _serverEndPoint);
        }
    }

    /// <summary>Starts the pinging server.</summary>
    /// <param name="intervalInMilliseconds">Ping interval</param>
    protected void StartPingingServer(double intervalInMilliseconds)
    {
        string localHostName = Dns.GetHostName();
        string localIP = GetLocalIpAddress().ToString();
        PingServer(packet_type.packet_client_request_alignment);
        PingServer(packet_type.packet_client_ping, localIP, localHostName, _packetVersionNumber);

        // ping every interval in milliseconds
        _pingTimer = new System.Timers.Timer(intervalInMilliseconds);
        _pingTimer.Elapsed += (a, b) => PingServer(packet_type.packet_client_ping, localIP, localHostName, _packetVersionNumber);
        _pingTimer.AutoReset = true;
        _pingTimer.Enabled = true;
    }

    private void Awake()
    {
        ServiceRegistry.RegisterService<ILighthouseControl>(this);
    }

    /// <summary>Starts as client.</summary>
    protected void StartAsClient()
    {
        _serverEndPoint = new IPEndPoint(IPAddress.Any, _broadcastPort);
        CreateUdpClient(_broadcastPort);
        if (_udpClient != null)
        {
            ListenForPackets(_serverEndPoint);
        }
    }

    public void Update()
    {
        // check if connection to acam is alive
        // allow up to 10 second lag
        if (DateTime.Now - _receivedLastPacketTime > TimeSpan.FromSeconds(10))
        {
            SessionState.Connected = false;
        }
        else
        {
            SessionState.Connected = true;
        }

        if (ObjectPruningTimeoutInSeconds > 0)
        {
            PruneOldTrackedObjects(ObjectPruningTimeoutInSeconds);
        }
    }

    private void PruneOldTrackedObjects(double pruningTimeoutTime)
    {
        List<TrackedObject> objectsToRemove = new List<TrackedObject>();

        lock (TrackedObjectDictionary)
        {
            foreach (var trackedObject in TrackedObjectDictionary)
            {
                if (DateTime.Now > (trackedObject.Value.lastUpdate + TimeSpan.FromSeconds(pruningTimeoutTime)))
                {
                    Debug.Log("Lost detection for class id:" + trackedObject.Value.classId + " id:" + trackedObject.Value.id);
                    if(trackedObject.Value.classId != -1) //if classId is from chess protocol
                    {
                        objectsToRemove.Add(trackedObject.Value);
                    }
                }
            }
        }

        foreach (var objectToRemove in objectsToRemove)
        {
            lock (TrackedObjectDictionary)
            {
                TrackedObjectDictionary.Remove(objectToRemove.id);
            }

            Dispatcher.Current.BeginInvoke(() =>
            {
                SessionState.TrackedObjects.Remove(objectToRemove);
            });
        }
    }
  

    /// <summary>Stops the socket listener.</summary>
    protected void StopListener()
    {
        _isListening = false;
        ReceivePacket = null;
        _udpClient.Close();
        _udpClient = null;
    }

    private void OnDestroy()
    {
        if (_pingTimer != null)
        {
            _pingTimer.Enabled = false;
            _pingTimer.Close();
            _pingTimer = null;
        }

        QuitListening();
    }

    /// <summary>Quits the socket listener and aborts listener thread.</summary>
    private void QuitListening()
    {
        ReceivePacket = null;

        if (_isListening)
        {
            StopListener();
        }
        if (_listenThread != null && _listenThread.IsAlive)
        {
            try
            {
                _listenThread.Abort();
            }
            catch (ThreadAbortException)
            {
                Debug.Log("listening thread aborted");
                Thread.ResetAbort();
            }
        }
    }

    public void Start()
    {
        if (_isServerMode)
        {
            Debug.Log("Starting as server ...");
        }
        else if (_isClientMode)
        {
            try
            {
                Debug.Log("Searching for the Lighthouse server ...");
                DebugView.Log("Searching for the Lighthouse server ...");
                StartAsClient();

            }
            catch (Exception ex)
            {
                Debug.Log($"Networking error {ex.Message}");
            }
        }
    }
    #region network utility methods

    /// <summary>Gets the broadcast address.</summary>
    /// <param name="hostIPAddress">The host ip address.</param>
    /// <returns>IPAddress.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException"></exception>
    public static IPAddress GetBroadcastAddress(IPAddress hostIPAddress)
    {
        var subnetAddress = GetSubnetMask(hostIPAddress);

        var deviceAddressBytes = hostIPAddress.GetAddressBytes();
        var subnetAddressBytes = subnetAddress.GetAddressBytes();

        if (deviceAddressBytes.Length != subnetAddressBytes.Length)
            throw new ArgumentOutOfRangeException();

        var broadcastAddressBytes = new byte[deviceAddressBytes.Length];

        for (var i = 0; i < broadcastAddressBytes.Length; i++)
            broadcastAddressBytes[i] = (byte)(deviceAddressBytes[i] | subnetAddressBytes[i] ^ 255);

        return new IPAddress(broadcastAddressBytes);
    }

    /// <summary>Gets the subnet mask.</summary>
    /// <param name="address">The address.</param>
    /// <returns>IPAddress.</returns>
    /// <exception cref="System.ArgumentException"></exception>
    public static IPAddress GetSubnetMask(IPAddress address)
    {
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            foreach (UnicastIPAddressInformation unicastIPAddressInformation in adapter.GetIPProperties().UnicastAddresses)
            {
                if (unicastIPAddressInformation.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    if (address.Equals(unicastIPAddressInformation.Address))
                    {
                        return unicastIPAddressInformation.IPv4Mask;
                    }
                }
            }
        }
        throw new ArgumentException(string.Format("Can't find subnetmask for IP address '{0}'", address));
    }

    /// <summary>Gets the local ip address.</summary>
    /// <returns>IPAddress.</returns>
    public static IPAddress GetLocalIpAddress()
    {
        return IPAddress.Parse(GetAllLocalIPv4(NetworkInterfaceType.Wireless80211).FirstOrDefault());
    }

    /// <summary>Gets all local i PV4.</summary>
    /// <param name="_type">The type.</param>
    /// <returns>System.String[].</returns>
    public static string[] GetAllLocalIPv4(NetworkInterfaceType _type)
    {
        List<string> ipAddrList = new List<string>();
        foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (item.NetworkInterfaceType == _type && item.OperationalStatus == OperationalStatus.Up)
            {
                foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ipAddrList.Add(ip.Address.ToString());
                    }
                }
            }
        }
        return ipAddrList.ToArray();
    }
    #endregion
}