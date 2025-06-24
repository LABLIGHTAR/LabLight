using System;
using System.Collections.Generic;

public enum DBConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

// Generic Database Interface
public interface IDatabase
{
    // === Connection ===
    // Properties
    bool IsConnected { get; } // Could be derived from CurrentDBStatus == DBConnectionStatus.Connected
    string CurrentUserId { get; } // This is the SpacetimeDB Identity
    
    // --- New State Management ---
    DBConnectionStatus CurrentDBStatus { get; }
    event Action<DBConnectionStatus, string> OnDBStatusChanged; // string for optional message (e.g. error)

    // Events (Existing - provide specific context beyond general status changes)
    event Action OnConnecting; // Useful for UI to show specific "Connecting..." state before token is validated by DB
    event Action<string> OnConnected; // Passes SpacetimeDB Identity
    event Action<string> OnConnectionFailed; // Specific failure reason
    event Action<string?> OnDisconnected; // Optional reason
    event Action<string> OnError; // General SpacetimeDB operational errors, not just connection
    
    // Methods
    void Connect(string oidcToken);
    void Connect();
    void Disconnect();

    // === User Profile ===
    // Properties & Events
    UserData CurrentUserProfile { get; }
    event Action<UserData> OnUserProfileUpdated;
    // Methods
    void RegisterProfile(string displayName);
    UserData GetCachedUserProfile(string userId);

    // === Organization ===
    // Methods (Reducers)
    void CreateOrganization(string orgName);
    void RequestJoinOrganization(uint orgId);
    void ApproveJoinRequest(ulong joinRequestId);
    void PostOrganizationNotice(uint organizationId, string content, ulong durationSeconds);
    void DeleteOrganizationNotice(ulong noticeId);
    void TryLeaveOrganization(uint orgId);
    void DenyJoinRequest(ulong joinRequestId);
    // Events (New)
    event Action OnOrganizationJoinRequestsChanged;
    // Methods (Getters)
    IEnumerable<OrganizationData> GetAllCachedOrganizations();
    IEnumerable<OrganizationNoticeData> GetAllCachedOrganizationNotices(uint organizationId);
    IEnumerable<OrganizationMemberData> GetCachedOrganizationMembers(uint organizationId);
    IEnumerable<OrganizationMemberRequestData> GetCachedPendingJoinRequestsForCurrentUser();
    IEnumerable<OrganizationMemberRequestData> GetCachedJoinRequestsForOwnedOrganizations();

    // === Protocol Definition ===
    // Events
    event Action<string> OnProtocolCreateSuccess;
    event Action<string, string> OnProtocolCreateFailure;
    event Action<ProtocolData> OnProtocolAdded;
    event Action<ProtocolData> OnProtocolEditSuccess;
    event Action<uint, string> OnProtocolEditFailure;
    event Action<uint> OnSavedProtocolAdded; 
    event Action<uint> OnSavedProtocolRemoved; 
    event Action<uint, string> OnProtocolForkSuccess; 
    event Action<OrganizationData> OnOrganizationCreateSuccess; 
    event Action<string, string> OnOrganizationCreateFailure; 
    // Methods (Reducers)
    void CreateProtocol(string name, string content, bool isPublic, uint organizationId);
    void EditProtocol(uint protocolId, string newName, string newContent, bool newIsPublic, uint organizationId);
    void DeleteProtocol(uint protocolId);
    void RollbackProtocol(uint protocolId);
    void SaveProtocol(uint protocolId);
    void UnsaveProtocol(uint protocolId);
    void TryForkProtocol(uint originalProtocolId, string newName, bool isPublic);
    // Methods (Getters)
    IEnumerable<ProtocolData> GetCachedProtocols();
    ProtocolData GetCachedProtocol(uint protocolId);
    IEnumerable<ProtocolEditHistoryData> GetCachedProtocolEditHistory(uint protocolId);
    ProtocolOwnershipData GetCachedProtocolOwnership(uint protocolId);
    IEnumerable<ProtocolData> GetSavedProtocols();

    // === Protocol State ===
    // Methods (Reducers)
    void CreateProtocolState(uint protocolId, uint organizationId, string state);
    void DeleteProtocolState(uint protocolStateId);
    void EditProtocolState(uint protocolStateId, string newState);
    // Methods (Getters)
    IEnumerable<ProtocolStateData> GetCachedProtocolStatesForOrg(uint organizationId);
    ProtocolStateData GetCachedProtocolState(uint protocolStateId);
    ProtocolStateOwnershipData GetCachedProtocolStateOwnership(uint protocolStateId);
    IEnumerable<ProtocolStateEditHistoryData> GetCachedProtocolStateEditHistory(uint protocolStateId);

    // === Media Metadata ===
    // Events
    event Action<bool> OnRequestMediaUploadSlotResultReceived;
    event Action<bool> OnConfirmMediaUploadCompleteResultReceived;
    event Action OnMediaMetadataUpdate;
    // Methods (Reducers)
    void RequestMediaUploadSlot(string objectKey, string originalFilename, string contentType);
    void ConfirmMediaUploadComplete(string objectKey, ulong fileSize);
    void ConfirmMinioObjectDeleted(string objectKey);
    IEnumerable<MediaMetadataData> GetCachedMediaMetadataEntries();
    MediaMetadataData GetCachedMediaMetadata(string objectKey);
    MediaMetadataData GetCachedMediaMetadataById(ulong mediaId);

    // === Scheduled Tasks ===
    // Methods (Reducers)
    void ScheduleProtocolTask(uint organizationId, uint protocolId, uint protocolStateId, System.Collections.Generic.List<SpacetimeDB.Identity> assigneeIdentities, uint startStep, uint endStep, SpacetimeDB.Timestamp scheduledAt, SpacetimeDB.Timestamp dueAt);
    void StartScheduledTask(ulong taskId);
    void CompleteScheduledTask(ulong taskId);
    void CancelScheduledTask(ulong taskId);
    // Methods (Getters)
    IEnumerable<ScheduledProtocolTaskData> GetAllCachedScheduledTasksForOrg(uint organizationId);
    IEnumerable<ScheduledTaskAssigneeData> GetCachedAssigneesForTask(ulong taskId);

    // === Client Device ===
    // Methods (Getters)
    IEnumerable<ClientDeviceData> GetAllCachedClientDevices();

    // === Messaging ===
    // Events
    event Action<ConversationData> OnConversationAdded;
    event Action<ConversationData> OnConversationUpdated;
    event Action<ulong> OnConversationRemoved;
    event Action<MessageData> OnMessageReceived;
    event Action<MessageData> OnMessageUpdated;
    event Action<string> OnMessagingError;

    // Methods (Reducers)
    void SendDirectMessage(List<string> recipientIdentities, string content);
    void SendConversationMessage(ulong conversationId, string content);
    void AddUserToConversation(ulong conversationId, string userIdentityToAdd);
    void LeaveConversation(ulong conversationId);
    void RenameConversation(ulong conversationId, string newName);
    void EditMessage(ulong messageId, string newContent);
    void DeleteMessage(ulong messageId);
    void MarkConversationAsRead(ulong conversationId);

    // Methods (Getters)
    IEnumerable<ConversationData> GetAllConversations();
    ConversationData GetConversation(ulong conversationId);
    IEnumerable<MessageData> GetMessages(ulong conversationId);

    /// <summary>
    /// Checks if a specific protocol is marked as saved by the specified user.
    /// </summary>
    /// <param name="protocolId">The ID of the protocol to check.</param>
    /// <param name="userId">The Identity string of the user.</param>
    /// <returns>True if the protocol is saved by the user, false otherwise.</returns>
    bool IsProtocolSavedByUser(uint protocolId, string userId);
}