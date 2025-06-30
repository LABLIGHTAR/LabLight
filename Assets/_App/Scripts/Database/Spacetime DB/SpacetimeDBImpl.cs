// Assets/Scripts/SpacetimeDBManager.cs
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization; // Needed for Hex To Byte conversion
using SpacetimeDB;
using SpacetimeDB.Types;

public partial class SpacetimeDBImpl : MonoBehaviour, IDatabase
{
    #region Singleton & Configuration
    private static IDatabase _instance;
    public static IDatabase Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<SpacetimeDBImpl>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("SpacetimeDBManager");
                    _instance = go.AddComponent<SpacetimeDBImpl>();
                    Debug.Log("SpacetimeDBManager instance created.");
                }
            }
            return _instance;
        }
    }

    [SerializeField] private string host = "ws://localhost:3000";
    [SerializeField] private string dbName = "lablightdb";
    [SerializeField] private float frameTickRateHz = 30f;
    #endregion

    #region Connection State & Public Properties
    private SpacetimeDB.Identity? _spacetimedbIdentity;
    private SpacetimeDB.Types.DbConnection _connection;
    private DBConnectionStatus _dbStatus;

    private bool _isConnectedState = false;

    public DBConnectionStatus CurrentDBStatus
    {
        get => _dbStatus;
        private set
        {
            if (_dbStatus != value)
            {
                _dbStatus = value;
                // OnDBStatusChanged is invoked by SetDBStatus
            }
        }
    }

    public bool IsConnected => CurrentDBStatus == DBConnectionStatus.Connected;
    public string CurrentUserId => _spacetimedbIdentity?.ToString();
    public string CurrentIdentity => _spacetimedbIdentity?.ToString();

    #endregion

    #region Public Interface
    public UserData CurrentUserProfile { get; private set; }
    public string CurrentUsername => CurrentUserProfile?.Name ?? "N/A";
    public string CurrentSpacetimeIdentity => _spacetimedbIdentity?.ToString();
    public DBConnectionStatus DBStatus => _dbStatus;

    // Public Events
    public event Action<DBConnectionStatus, string> OnDBStatusChanged;
    public event Action<string> OnError;
    public event Action<UserData> OnUserProfileUpdated;
    public event Action<string> OnSpacetimeDBLog;
    #endregion

    #region IDatabase Events
    public event Action<string> OnConnected;
    public event Action<string> OnDisconnected;
    public event Action<ProtocolData> OnProtocolReceived;
    public event Action<ProtocolData> OnSavedProtocolReceived;
    public event Action<MediaMetadata> OnMediaMetadataReceived;
    public event Action<ulong> OnMediaMetadataRemoved;
    public event Action<OrganizationMemberRequestData> OnOrganizationMemberRequestReceived;
    public event Action<ulong> OnOrganizationMemberRequestRemoved;
    public event Action<ConversationData> OnConversationAdded;
    public event Action<ConversationData> OnConversationUpdated;
    public event Action<ulong> OnConversationRemoved;
    public event Action<MessageData> OnMessageReceived;
    public event Action<MessageData> OnMessageUpdated;
    public event Action<string> OnMessagingError;
    #endregion

    #region Events
    // General Connection Events
    public event Action OnConnecting;
    public event Action<string> OnConnectionFailed;

    // User Profile Events
    public event Action<UserData> OnAnyUserProfileUpdated;

    // Organization Events
    public event Action<OrganizationData> OnOrganizationCreateSuccess;
    public event Action<string, string> OnOrganizationCreateFailure;
    public event Action OnOrganizationJoinRequestsChanged;

    // Protocol Events
    public event Action<string> OnProtocolCreateSuccess;
    public event Action<string, string> OnProtocolCreateFailure;
    public event Action<uint> OnSavedProtocolAdded;
    public event Action<uint> OnSavedProtocolRemoved;
    public event Action<ProtocolData> OnProtocolAdded;
    public event Action<uint, string> OnProtocolForkSuccess;
    public event Action<ProtocolData> OnProtocolEditSuccess;
    public event Action<uint, string> OnProtocolEditFailure;

    // IDatabase required event
    public event Action<string> OnMediaMetadataDeleted; // Event for when media metadata is confirmed deleted
    #endregion

    #region Unity Lifecycle Methods
    void Awake()
    {
        if (_instance != null && _instance != this)
        {
             Debug.LogWarning("Duplicate SpacetimeDBManager instance detected. Destroying self.");
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
         Debug.Log("SpacetimeDBManager Awake and Initialized.");
        // Set initial status
        _dbStatus = DBConnectionStatus.Disconnected; 
    }

     void OnDestroy()
    {
        Debug.Log("SpacetimeDBManager OnDestroy.");
        Disconnect();
        if (_instance == this) { _instance = null; }
    }
    #endregion

    #region Connection Management
    public void Connect(string oidcToken)
    {
        if (_isConnectedState) { Debug.LogWarning("SpacetimeDB: Already connected or connecting."); return; }
        if (string.IsNullOrEmpty(oidcToken)) { LogErrorAndInvoke("SpacetimeDB: OIDC token is null or empty. Cannot connect.", true); return; }

        SetDBStatus(DBConnectionStatus.Connecting, "Authenticating with token...");
        
        Debug.Log($"SpacetimeDB: Attempting connection to {host}/{dbName} with OIDC token.");

        try
        {
            _connection = DbConnection.Builder()
                .WithUri(host).WithModuleName(dbName).WithToken(oidcToken)
                .OnConnect(HandleSpacetimeConnect)
                .OnDisconnect(HandleSpacetimeDisconnect)
                .OnConnectError(HandleSpacetimeConnectError)
                .Build();
            
            CancelInvoke(nameof(FrameTick));
            InvokeRepeating(nameof(FrameTick), 1f / frameTickRateHz, 1f / frameTickRateHz);
            Debug.Log($"SpacetimeDB: Connection process initiated. FrameTick scheduled @{frameTickRateHz}Hz.");
        }
        catch (Exception ex)
        {
            HandleSpacetimeConnectError(ex);
        }
    }
    
    public void Connect()
    {
        if (_isConnectedState) { Debug.LogWarning("SpacetimeDB: Already connected or connecting."); return; }

        SetDBStatus(DBConnectionStatus.Connecting, "Connecting anonymously...");
        
        Debug.Log($"SpacetimeDB: Attempting anonymous connection to {host}/{dbName}.");

        try
        {
            _connection = DbConnection.Builder()
                .WithUri(host).WithModuleName(dbName)
                .OnConnect(HandleSpacetimeConnect)
                .OnDisconnect(HandleSpacetimeDisconnect)
                .OnConnectError(HandleSpacetimeConnectError)
                .Build();
            
            CancelInvoke(nameof(FrameTick));
            InvokeRepeating(nameof(FrameTick), 1f / frameTickRateHz, 1f / frameTickRateHz);
            Debug.Log($"SpacetimeDB: Connection process initiated. FrameTick scheduled @{frameTickRateHz}Hz.");
        }
        catch (Exception ex)
        {
            HandleSpacetimeConnectError(ex);
        }
    }

    public void Disconnect()
    {
        if (!_isConnectedState && _connection == null) { Debug.LogWarning("SpacetimeDB: Already disconnected."); return; }
         Debug.Log("SpacetimeDB: Disconnect requested.");
        CancelInvoke(nameof(FrameTick));
        var connToDisconnect = _connection;
        _connection = null;
        _spacetimedbIdentity = null;
        CurrentUserProfile = null;
        _isConnectedState = false;

        connToDisconnect?.Disconnect();
        UnregisterCallbacks(connToDisconnect);
    }

    private void FrameTick()
    {
        if (_connection != null && _isConnectedState)
        {
            try { _connection.FrameTick(); }
            catch (Exception ex)
            {
                Debug.LogError($"Error during SpacetimeDB FrameTick: {ex}");
                HandleSpacetimeDisconnect(_connection, ex);
            }
        }
    }

    private void HandleSpacetimeConnect(DbConnection conn, Identity identity, string refreshedToken)
    {
        Debug.Log($"SpacetimeDB: Connected! Identity: {identity}");
        _connection = conn;
        _spacetimedbIdentity = identity;
        _isConnectedState = true;

        RegisterTableCallbacks();
        RegisterReducerCallbacks();
        SubscribeToTables();
        
        SetDBStatus(DBConnectionStatus.Connected, $"Connected with Identity: {identity}");
        OnConnected?.Invoke(identity.ToString());
    }

    private void HandleSpacetimeConnectError(Exception e)
    {
        _isConnectedState = false;
        _spacetimedbIdentity = null;
        var connToClean = _connection;
        _connection = null;
        CancelInvoke(nameof(FrameTick));
        
        string errorMsg = $"SpacetimeDB Connection Error: {e.Message}";
        SetDBStatus(DBConnectionStatus.Error, errorMsg);
        LogErrorAndInvoke(errorMsg, true);
        UnregisterCallbacks(connToClean);
    }

    private void HandleSpacetimeDisconnect(DbConnection conn, Exception? e)
    {
        string reason = e?.Message ?? "Requested";
        bool wasConnected = _isConnectedState;
        
        _isConnectedState = false;
        _spacetimedbIdentity = null;
        CurrentUserProfile = null;
        var connToClean = _connection;
        _connection = null;

        CancelInvoke(nameof(FrameTick));
        
        if (wasConnected)
        {
            Debug.Log($"SpacetimeDB: Disconnected. Reason: {reason}");
            SetDBStatus(DBConnectionStatus.Disconnected, reason);
            OnDisconnected?.Invoke(reason);
        }
        else if (CurrentDBStatus != DBConnectionStatus.Error)
        {
             SetDBStatus(DBConnectionStatus.Disconnected, "Disconnect called on non-connected client.");
        }

        UnregisterCallbacks(connToClean ?? conn);
        ClearAllCachedData();
    }
    #endregion

    #region SpacetimeDB Subscriptions & Callbacks Registration
    private void SubscribeToTables()
    {
        if (!AssertConnected("subscribe to tables")) return;
        string[] publicTables = new string[] {
            "SELECT * FROM client_device",
            "SELECT * FROM user_profile",
            "SELECT * FROM organization",
            "SELECT * FROM organization_member",
            "SELECT * FROM organization_notice",
            "SELECT * FROM protocol",
            "SELECT * FROM protocol_ownership",
            "SELECT * FROM protocol_edit_history",
            "SELECT * FROM protocol_state",
            "SELECT * FROM protocol_state_ownership",
            "SELECT * FROM protocol_state_edit_history",
            "SELECT * FROM media_metadata",
            "SELECT * FROM scheduled_protocol_task",
            "SELECT * FROM scheduled_task_assignee",
            "SELECT * FROM saved_protocol",
            "SELECT * FROM organization_member_request",
            "SELECT * FROM conversation",
            "SELECT * FROM message",
            "SELECT * FROM conversation_participant"
        };
        Debug.Log($"SpacetimeDB: Subscribing to public tables: {string.Join(", ", publicTables)}");
        try { _connection.SubscriptionBuilder().OnApplied((ctx) => Debug.Log("SpacetimeDB: Subscription applied successfully.")).OnError((errCtx, err) => LogErrorAndInvoke($"Subscription failed: {err.Message}")).Subscribe(publicTables); }
        catch (Exception ex) { LogErrorAndInvoke($"Error initiating subscription: {ex.Message}"); }
    }

    private void RegisterTableCallbacks()
    {
        if (_connection?.Db == null) { Debug.LogError("SpacetimeDB: Cannot register table callbacks, Db object is null."); return; }
         Debug.Log("SpacetimeDB: Registering table callbacks...");
        
        // UserProfile Callbacks
        if (_connection.Db.UserProfile != null)
        { try { _connection.Db.UserProfile.OnInsert += HandleUserProfileInsert; _connection.Db.UserProfile.OnUpdate += HandleUserProfileUpdate; Debug.Log("Registered UserProfile table callbacks."); } catch (Exception ex) { Debug.LogError($"Error registering UserProfile callbacks: {ex.Message}"); } }
        else { Debug.LogWarning("UserProfile table handle is null. Cannot register callbacks."); }
        
        // SavedProtocol Callbacks
        if (_connection.Db.SavedProtocol != null)
        { try { _connection.Db.SavedProtocol.OnInsert += HandleSavedProtocolInsert; _connection.Db.SavedProtocol.OnDelete += HandleSavedProtocolDelete; Debug.Log("Registered SavedProtocol table callbacks."); } catch (Exception ex) { Debug.LogError($"Error registering SavedProtocol callbacks: {ex.Message}"); } }
        else { Debug.LogWarning("SavedProtocol table handle is null. Cannot register callbacks."); }
        
        // Protocol Callbacks
        if (_connection.Db.Protocol != null)
        { try { _connection.Db.Protocol.OnInsert += HandleProtocolInsert; Debug.Log("Registered Protocol table OnInsert callback."); } catch (Exception ex) { Debug.LogError($"Error registering Protocol OnInsert callback: {ex.Message}"); } }
        else { Debug.LogWarning("Protocol table handle is null. Cannot register OnInsert callback."); }

        // MediaMetadata Callbacks
        if (_connection.Db.MediaMetadata != null)
        {
            try
            {
                _connection.Db.MediaMetadata.OnInsert += HandleMediaMetadataInsert;
                _connection.Db.MediaMetadata.OnUpdate += HandleMediaMetadataUpdate;
                _connection.Db.MediaMetadata.OnDelete += HandleMediaMetadataDelete;
                Debug.Log("Registered MediaMetadata table callbacks.");
            } catch (Exception ex) { Debug.LogError($"Error registering MediaMetadata callbacks: {ex.Message}"); }
        }
        else { Debug.LogWarning("MediaMetadata table handle is null. Cannot register callbacks."); }

        // OrganizationMemberRequest Callbacks
        if (_connection.Db.OrganizationMemberRequest != null)
        {
            try 
            {
                _connection.Db.OrganizationMemberRequest.OnInsert += HandleOrganizationMemberRequestChange;
                _connection.Db.OrganizationMemberRequest.OnDelete += HandleOrganizationMemberRequestChange;
                Debug.Log("Registered OrganizationMemberRequest table callbacks.");
            } catch (Exception ex) { Debug.LogError($"Error registering OrganizationMemberRequest callbacks: {ex.Message}"); }
        }
        else { Debug.LogWarning("OrganizationMemberRequest table handle is null. Cannot register callbacks."); }

        // Messaging Callbacks
        if (_connection.Db.Conversation != null)
        {
            try
            {
                _connection.Db.Conversation.OnInsert += HandleConversationInsert;
                _connection.Db.Conversation.OnUpdate += HandleConversationUpdate;
                _connection.Db.Conversation.OnDelete += HandleConversationDelete;
                Debug.Log("Registered Conversation table callbacks.");
            } catch (Exception ex) { Debug.LogError($"Error registering Conversation callbacks: {ex.Message}"); }
        }
        else { Debug.LogWarning("Conversation table handle is null. Cannot register callbacks."); }

        if (_connection.Db.Message != null)
        {
            try
            {
                _connection.Db.Message.OnInsert += HandleMessageInsert;
                _connection.Db.Message.OnUpdate += HandleMessageUpdate;
                Debug.Log("Registered Message table callbacks.");
            } catch (Exception ex) { Debug.LogError($"Error registering Message callbacks: {ex.Message}"); }
        }
        else { Debug.LogWarning("Message table handle is null. Cannot register callbacks."); }

        if (_connection.Db.ConversationParticipant != null)
        {
            try
            {
                _connection.Db.ConversationParticipant.OnInsert += HandleConversationParticipantInsert;
                _connection.Db.ConversationParticipant.OnUpdate += HandleConversationParticipantUpdate;
                _connection.Db.ConversationParticipant.OnDelete += HandleConversationParticipantDelete;
                Debug.Log("Registered ConversationParticipant table callbacks.");
            } catch (Exception ex) { Debug.LogError($"Error registering ConversationParticipant callbacks: {ex.Message}"); }
        }
        else { Debug.LogWarning("ConversationParticipant table handle is null. Cannot register callbacks."); }
    }

    private void RegisterReducerCallbacks()
    {
        if (_connection?.Reducers == null) { Debug.LogError("SpacetimeDB: Cannot register reducer callbacks, Reducers object is null."); return; }
        Debug.Log("SpacetimeDB: Registering reducer callbacks...");

        // User Profile
        _connection.Reducers.OnRegisterProfile += OnRegisterProfileResult;

        // Organization
        _connection.Reducers.OnTryCreateOrganization += OnTryCreateOrganizationResult;
        _connection.Reducers.OnRequestJoinOrganization += OnRequestJoinOrganizationResult;
        _connection.Reducers.OnTryApproveOrganizationMemberRequest += OnTryApproveOrganizationMemberRequestResult;
        _connection.Reducers.OnTryPostOrganizationNotice += OnTryPostOrganizationNoticeResult;
        _connection.Reducers.OnTryDeleteOrganizationNotice += OnTryDeleteOrganizationNoticeResult;
        _connection.Reducers.OnTryLeaveOrganization += OnTryLeaveOrganizationResult;
        _connection.Reducers.OnTryDenyOrganizationMemberRequest += OnTryDenyOrganizationMemberRequestResult;
        _connection.Reducers.OnUpdateOrganizationName += OnUpdateOrganizationNameResult;
        _connection.Reducers.OnTryDeleteOrganization += OnTryDeleteOrganizationResult;

        // Protocol Definition
        _connection.Reducers.OnTryCreateProtocol += OnTryCreateProtocolResult;
        _connection.Reducers.OnTryEditProtocol += OnTryEditProtocolResult;
        _connection.Reducers.OnTryDeleteProtocol += OnTryDeleteProtocolResult;
        _connection.Reducers.OnTryCreateProtocolState += OnTryCreateProtocolStateResult;
        _connection.Reducers.OnTryDeleteProtocolState += OnTryDeleteProtocolStateResult;
        _connection.Reducers.OnTryRollbackProtocol += OnTryRollbackProtocolResult;
        _connection.Reducers.OnTryEditProtocolState += OnTryEditProtocolStateResult;
        _connection.Reducers.OnTrySaveProtocol += OnTrySaveProtocolResult;
        _connection.Reducers.OnTryScheduleProtocolTask += OnTryScheduleProtocolTaskResult;
        _connection.Reducers.OnTryStartScheduledTask += OnTryStartScheduledTaskResult;
        _connection.Reducers.OnTryCompleteScheduledTask += OnTryCompleteScheduledTaskResult;
        _connection.Reducers.OnTryCancelScheduledTask += OnTryCancelScheduledTaskResult;
        _connection.Reducers.OnTryUnsaveProtocol += OnTryUnsaveProtocolResult;
        _connection.Reducers.OnTryForkProtocol += OnTryForkProtocolResult;

        // Media Reducers (New Pattern)
        _connection.Reducers.OnRequestMediaUploadSlot += OnRequestMediaUploadSlotReducerEvent;
        _connection.Reducers.OnConfirmMediaUploadComplete += OnConfirmMediaUploadCompleteReducerEvent;

        // Messaging Reducers
        _connection.Reducers.OnSendDirectMessage += OnSendDirectMessageReducerEvent;
        _connection.Reducers.OnSendConversationMessage += OnSendConversationMessageReducerEvent;
        _connection.Reducers.OnAddUserToConversation += OnAddUserToConversationReducerEvent;
        _connection.Reducers.OnLeaveConversation += OnLeaveConversationReducerEvent;
        _connection.Reducers.OnRenameConversation += OnRenameConversationReducerEvent;
        _connection.Reducers.OnEditMessage += OnEditMessageReducerEvent;
        _connection.Reducers.OnDeleteMessage += OnDeleteMessageReducerEvent;
        _connection.Reducers.OnMarkConversationAsRead += OnMarkConversationAsReadReducerEvent;
    }

    private void UnregisterCallbacks(DbConnection conn)
    {
        if (conn == null) { Debug.LogWarning("SpacetimeDB: DbConnection is null, cannot unregister callbacks."); return; }
        Debug.Log("SpacetimeDB: Unregistering all callbacks...");

        // --- Table Callbacks ---
        // UserProfile
        if (conn.Db?.UserProfile != null) { conn.Db.UserProfile.OnInsert -= HandleUserProfileInsert; conn.Db.UserProfile.OnUpdate -= HandleUserProfileUpdate; }
        // SavedProtocol
        if (conn.Db?.SavedProtocol != null) { conn.Db.SavedProtocol.OnInsert -= HandleSavedProtocolInsert; conn.Db.SavedProtocol.OnDelete -= HandleSavedProtocolDelete; }
        // Protocol
        if (conn.Db?.Protocol != null) { conn.Db.Protocol.OnInsert -= HandleProtocolInsert; }
        // MediaMetadata
        if (conn.Db?.MediaMetadata != null)
        {
            conn.Db.MediaMetadata.OnInsert -= HandleMediaMetadataInsert;
            conn.Db.MediaMetadata.OnUpdate -= HandleMediaMetadataUpdate;
            conn.Db.MediaMetadata.OnDelete -= HandleMediaMetadataDelete;
        }
        // OrganizationMemberRequest
        if (conn.Db?.OrganizationMemberRequest != null) { conn.Db.OrganizationMemberRequest.OnInsert -= HandleOrganizationMemberRequestChange; conn.Db.OrganizationMemberRequest.OnDelete -= HandleOrganizationMemberRequestChange; }

        // Messaging
        if (conn.Db?.Conversation != null)
        {
            conn.Db.Conversation.OnInsert -= HandleConversationInsert;
            conn.Db.Conversation.OnUpdate -= HandleConversationUpdate;
            conn.Db.Conversation.OnDelete -= HandleConversationDelete;
        }
        if (conn.Db?.Message != null)
        {
            conn.Db.Message.OnInsert -= HandleMessageInsert;
            conn.Db.Message.OnUpdate -= HandleMessageUpdate;
        }
        if (conn.Db?.ConversationParticipant != null)
        {
            conn.Db.ConversationParticipant.OnInsert -= HandleConversationParticipantInsert;
            conn.Db.ConversationParticipant.OnUpdate -= HandleConversationParticipantUpdate;
            conn.Db.ConversationParticipant.OnDelete -= HandleConversationParticipantDelete;
        }

        // --- Reducer Callbacks ---
        if (conn.Reducers != null)
        {
            // User Profile
            conn.Reducers.OnRegisterProfile -= OnRegisterProfileResult;
            // Organization
            conn.Reducers.OnTryCreateOrganization -= OnTryCreateOrganizationResult;
            conn.Reducers.OnRequestJoinOrganization -= OnRequestJoinOrganizationResult;
            conn.Reducers.OnTryApproveOrganizationMemberRequest -= OnTryApproveOrganizationMemberRequestResult;
            conn.Reducers.OnTryPostOrganizationNotice -= OnTryPostOrganizationNoticeResult;
            conn.Reducers.OnTryDeleteOrganizationNotice -= OnTryDeleteOrganizationNoticeResult;
            conn.Reducers.OnTryLeaveOrganization -= OnTryLeaveOrganizationResult;
            conn.Reducers.OnTryDenyOrganizationMemberRequest -= OnTryDenyOrganizationMemberRequestResult;
            conn.Reducers.OnUpdateOrganizationName -= OnUpdateOrganizationNameResult;
            conn.Reducers.OnTryDeleteOrganization -= OnTryDeleteOrganizationResult;
            // Protocol Definition
            conn.Reducers.OnTryCreateProtocol -= OnTryCreateProtocolResult;
            conn.Reducers.OnTryEditProtocol -= OnTryEditProtocolResult;
            conn.Reducers.OnTryDeleteProtocol -= OnTryDeleteProtocolResult;
            conn.Reducers.OnTryCreateProtocolState -= OnTryCreateProtocolStateResult;
            conn.Reducers.OnTryDeleteProtocolState -= OnTryDeleteProtocolStateResult;
            conn.Reducers.OnTryRollbackProtocol -= OnTryRollbackProtocolResult;
            conn.Reducers.OnTryEditProtocolState -= OnTryEditProtocolStateResult;
            conn.Reducers.OnTrySaveProtocol -= OnTrySaveProtocolResult;
            conn.Reducers.OnTryScheduleProtocolTask -= OnTryScheduleProtocolTaskResult;
            conn.Reducers.OnTryStartScheduledTask -= OnTryStartScheduledTaskResult;
            conn.Reducers.OnTryCompleteScheduledTask -= OnTryCompleteScheduledTaskResult;
            conn.Reducers.OnTryCancelScheduledTask -= OnTryCancelScheduledTaskResult;
            conn.Reducers.OnTryUnsaveProtocol -= OnTryUnsaveProtocolResult;
            conn.Reducers.OnTryForkProtocol -= OnTryForkProtocolResult;

            // Media Reducers (New Pattern)
            conn.Reducers.OnRequestMediaUploadSlot -= OnRequestMediaUploadSlotReducerEvent;
            conn.Reducers.OnConfirmMediaUploadComplete -= OnConfirmMediaUploadCompleteReducerEvent;

            // Messaging Reducers
            conn.Reducers.OnSendDirectMessage -= OnSendDirectMessageReducerEvent;
            conn.Reducers.OnSendConversationMessage -= OnSendConversationMessageReducerEvent;
            conn.Reducers.OnAddUserToConversation -= OnAddUserToConversationReducerEvent;
            conn.Reducers.OnLeaveConversation -= OnLeaveConversationReducerEvent;
            conn.Reducers.OnRenameConversation -= OnRenameConversationReducerEvent;
            conn.Reducers.OnEditMessage -= OnEditMessageReducerEvent;
            conn.Reducers.OnDeleteMessage -= OnDeleteMessageReducerEvent;
            conn.Reducers.OnMarkConversationAsRead -= OnMarkConversationAsReadReducerEvent;
        }
    }

    #endregion

    #region Reducer Call Helpers
    private void HandleReducerResultBase(ReducerEventContext ctx, string actionDescription) {
       if (ctx.Event.CallerIdentity == _spacetimedbIdentity) {
           if (ctx.Event.Status is Status.Committed) {
                Debug.Log($"SpacetimeDB: Successfully requested {actionDescription}.");
           } else if (ctx.Event.Status is Status.Failed failedStatus) {
                LogErrorAndInvoke($"Failed to {actionDescription}: {failedStatus.ToString()}");
           } else {
                LogErrorAndInvoke($"Failed to {actionDescription}: Non-committed status {ctx.Event.Status}");
           }
       }
    }
    #endregion

    #region General Data Access
    public IEnumerable<ClientDeviceData> GetAllCachedClientDevices()
    {
        if (!AssertConnected("get all cached client devices") || _connection?.Db?.ClientDevice == null)
            return Enumerable.Empty<ClientDeviceData>();

        return _connection.Db.ClientDevice.Iter().Select(MapToClientDeviceData).Where(dto => dto != null);
    }
    #endregion

    #region Utility & Mapping Helpers
    private ClientDeviceData MapToClientDeviceData(SpacetimeDB.Types.ClientDevice spdbDevice)
    {
        if (spdbDevice == null) return null;
        return new ClientDeviceData
        {
            Id = spdbDevice.Identity.ToString(),
            IsConnected = spdbDevice.Connected,
            CreatedAtUtc = TimestampToDateTime(spdbDevice.CreatedAt),
            LastConnectedUtc = TimestampToDateTime(spdbDevice.LastConnected),
            LastDisconnectedUtc = TimestampToDateTime(spdbDevice.LastDisconnected)
        };
    }

    private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private DateTime TimestampToDateTime(Timestamp ts) {
        long ticks = ts.MicrosecondsSinceUnixEpoch * 10;
        try {
            return UnixEpoch.AddTicks(ticks);
        } catch (ArgumentOutOfRangeException ex) {
            Debug.LogWarning($"TimestampToDateTime: Timestamp {ts.MicrosecondsSinceUnixEpoch}us results in DateTime outside valid range. Returning MinValue. Error: {ex.Message}");
            return DateTime.MinValue;
        }
    }

    private bool AssertConnected(string action = "perform action") {
        bool result = _isConnectedState && _connection != null && _spacetimedbIdentity.HasValue;
        if (!result) {
            string reason = !_isConnectedState ? "not connected state" :
                            _connection == null ? "connection object null" :
                            !_spacetimedbIdentity.HasValue ? "identity not set" :
                            "unknown reason";
            Debug.LogWarning($"SpacetimeDB: Cannot {action} - Connection not ready. IsConnected: {_isConnectedState}, ConnectionNull: {_connection == null}, IdentityNull: {!_spacetimedbIdentity.HasValue}. Reason: {reason}.");
            // Potentially invoke OnError or similar callback if UI needs to react broadly to this
        }
        return result;
    }

    private void LogErrorAndInvoke(string message, bool isConnectionError = false) {
        Debug.LogError(message);
        OnError?.Invoke(message);
        if (isConnectionError) OnConnectionFailed?.Invoke(message);
    }

    private void SetDBStatus(DBConnectionStatus newStatus, string message = null)
    {
        var oldStatus = _dbStatus;
        _dbStatus = newStatus;
        
        var logMessage = $"DBConnectionStatus changed from {oldStatus} to {newStatus}." + (string.IsNullOrEmpty(message) ? "" : $" Message: {message}");
        Debug.Log($"SpacetimeDBImpl: {logMessage}");
        OnDBStatusChanged?.Invoke(newStatus, message);
    }
    
    private void ClearAllCachedData() 
    {
        Debug.Log("SpacetimeDBImpl: Clearing all cached table data.");
        // We can't actually clear the tables in the generated client cache,
        // but we can disconnect and on next connect, they will be fresh.
        // This method serves as a placeholder for any manual cache clearing if we implement it.
    }
    #endregion
}