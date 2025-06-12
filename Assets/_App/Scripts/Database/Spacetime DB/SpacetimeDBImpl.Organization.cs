using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using SpacetimeDB;
using SpacetimeDB.Types;

public partial class SpacetimeDBImpl
{
    #region Organization Table Event Handlers
    private void HandleOrganizationMemberRequestChange(EventContext ctx, SpacetimeDB.Types.OrganizationMemberRequest requestData) // Handles Insert
    {
        Debug.Log($"OrganizationMemberRequest changed (insert/update): Request ID {requestData.Id}, Org ID {requestData.OrganizationId}, Requester {requestData.RequesterIdentity}. Invoking OnOrganizationJoinRequestsChanged.");
        OnOrganizationJoinRequestsChanged?.Invoke();
    }

    private void HandleOrganizationMemberRequestChange(EventContext ctx, ulong requestId, SpacetimeDB.Types.OrganizationMemberRequest? requestData) // Handles Delete
    {
        Debug.Log($"OrganizationMemberRequest deleted: Request ID {requestId}. Invoking OnOrganizationJoinRequestsChanged.");
        OnOrganizationJoinRequestsChanged?.Invoke();
    }
    #endregion

    #region Organization Reducer Calls
    public void CreateOrganization(string orgName)
    {
        if (!AssertConnected("create organization")) return;
        if (string.IsNullOrWhiteSpace(orgName)) { LogErrorAndInvoke("Organization name cannot be empty."); return; }
         Debug.Log($"SpacetimeDB: Requesting organization creation: {orgName}");
        _connection.Reducers.TryCreateOrganization(orgName);
    }

    public void RequestJoinOrganization(uint orgId)
    {
        if (!AssertConnected("request join organization")) return;
         Debug.Log($"SpacetimeDB: Requesting to join organization ID: {orgId}");
        _connection.Reducers.RequestJoinOrganization(orgId);
    }

    public void ApproveJoinRequest(ulong joinRequestId)
    {
        if (!AssertConnected("approve join request")) return;
         Debug.Log($"SpacetimeDB: Requesting approval for join request ID {joinRequestId}");
        _connection.Reducers.TryApproveOrganizationMemberRequest(joinRequestId);
    }

    public void PostOrganizationNotice(uint organizationId, string content, ulong durationSeconds)
    {
        if (!AssertConnected("post organization notice")) return;
        if (string.IsNullOrWhiteSpace(content)) { LogErrorAndInvoke("Notice content cannot be empty."); return; }
        if (durationSeconds == 0) { LogErrorAndInvoke("Notice duration must be greater than 0."); return; }
        Debug.Log($"SpacetimeDB: Requesting post notice in org {organizationId}");
        _connection.Reducers.TryPostOrganizationNotice(organizationId, content, durationSeconds);
    }

    public void DeleteOrganizationNotice(ulong noticeId)
    {
        if (!AssertConnected("delete organization notice")) return;
        Debug.Log($"SpacetimeDB: Requesting delete notice {noticeId}");
        _connection.Reducers.TryDeleteOrganizationNotice(noticeId);
    }

    public void TryLeaveOrganization(uint orgId)
    {
        if (!AssertConnected("leave organization")) return;
        Debug.Log($"SpacetimeDB: Requesting to leave organization ID: {orgId}");
        _connection.Reducers.TryLeaveOrganization(orgId);
    }

    public void DenyJoinRequest(ulong joinRequestId)
    {
        if (!AssertConnected("deny join request")) return;
        Debug.Log($"SpacetimeDB: Requesting to deny join request ID {joinRequestId}");
        _connection.Reducers.TryDenyOrganizationMemberRequest(joinRequestId);
    }
    #endregion

    #region Organization Reducer Event Handlers
    private void OnTryCreateOrganizationResult(ReducerEventContext ctx, string name) {
        if (ctx.Event.CallerIdentity == _spacetimedbIdentity) { 
            switch (ctx.Event.Status) 
            {
                case Status.Committed:
                     Debug.Log($"SpacetimeDB: Successfully requested organization creation for '{name}'.");
                     OnOrganizationCreateSuccess?.Invoke(null);
                    break;
                case Status.Failed failedStatus:
                     LogErrorAndInvoke($"Failed to create organization '{name}': {failedStatus.ToString()}", false);
                     OnOrganizationCreateFailure?.Invoke(name, failedStatus.ToString());
                    break;
                default: 
                     LogErrorAndInvoke($"Failed to create organization '{name}': Non-committed status {ctx.Event.Status}", false);
                     OnOrganizationCreateFailure?.Invoke(name, ctx.Event.Status.ToString()); 
                    break;
            }
        }
    }

     private void OnRequestJoinOrganizationResult(ReducerEventContext ctx, uint orgId) {
         if (ctx.Event.CallerIdentity == _spacetimedbIdentity) {
             if (ctx.Event.Status is Status.Committed) {
                 Debug.Log($"SpacetimeDB: Successfully requested to join organization {orgId}.");
             } else if (ctx.Event.Status is Status.Failed failedStatus) {
                 LogErrorAndInvoke($"Failed to request join for organization {orgId}: {failedStatus.ToString()}");
             } else {
                  LogErrorAndInvoke($"Failed to request join for organization {orgId}: Non-committed status {ctx.Event.Status}");
             }
         }
     }

     private void OnTryApproveOrganizationMemberRequestResult(ReducerEventContext ctx, ulong requestId) {
        if (ctx.Event.CallerIdentity == _spacetimedbIdentity) {
            if (ctx.Event.Status is Status.Committed) {
                 Debug.Log($"SpacetimeDB: Successfully approved join request ID {requestId}.");
            } else if (ctx.Event.Status is Status.Failed failedStatus) {
                 LogErrorAndInvoke($"Failed to approve join request ID {requestId}: {failedStatus.ToString()}");
            } else {
                  LogErrorAndInvoke($"Failed to approve join request ID {requestId}: Non-committed status {ctx.Event.Status}");
             }
        }
    }

    private void OnTryPostOrganizationNoticeResult(ReducerEventContext ctx, uint organizationId, string content, ulong durationSeconds) {
        HandleReducerResultBase(ctx, $"Post organization notice for Org ID: {organizationId}");
    }

    private void OnTryDeleteOrganizationNoticeResult(ReducerEventContext ctx, ulong noticeId) {
        HandleReducerResultBase(ctx, $"Delete organization notice ID: {noticeId}");
    }

    private void OnTryLeaveOrganizationResult(ReducerEventContext ctx, uint orgId)
    {
        HandleReducerResultBase(ctx, $"Leave organization ID: {orgId}");
    }

    private void OnTryDenyOrganizationMemberRequestResult(ReducerEventContext ctx, ulong requestId)
    {
        HandleReducerResultBase(ctx, $"Deny join request ID {requestId}");
        // Optionally, invoke an event here if UI needs to react specifically to denial success/failure
        // OnOrganizationJoinRequestsChanged?.Invoke(); // Already handled by table subscription if successful
    }

    internal void OnUpdateOrganizationNameResult(ReducerEventContext ctx, uint organizationId, string newName)
    {
        if (ctx.Event.CallerIdentity == _spacetimedbIdentity) {
            if (ctx.Event.Status is Status.Committed) {
                Debug.Log($"SpacetimeDB: Successfully updated organization {organizationId} name to '{newName}'. Table updates will reflect changes.");
            } else if (ctx.Event.Status is Status.Failed failedStatus) {
                LogErrorAndInvoke($"Failed to update organization {organizationId} name to '{newName}': {failedStatus.ToString()}");
            } else {
                 LogErrorAndInvoke($"Failed to update organization {organizationId} name to '{newName}': Non-committed status {ctx.Event.Status}");
            }
        }
    }

    internal void OnTryDeleteOrganizationResult(ReducerEventContext ctx, uint organizationId)
    {
        if (ctx.Event.CallerIdentity == _spacetimedbIdentity) {
            if (ctx.Event.Status is Status.Committed) {
                Debug.Log($"SpacetimeDB: Successfully deleted organization {organizationId}. Table updates will reflect changes.");
            } else if (ctx.Event.Status is Status.Failed failedStatus) {
                LogErrorAndInvoke($"Failed to delete organization {organizationId}: {failedStatus.ToString()}");
            } else {
                 LogErrorAndInvoke($"Failed to delete organization {organizationId}: Non-committed status {ctx.Event.Status}");
            }
        }
    }
    #endregion

    #region Organization Data Access
     public IEnumerable<OrganizationData> GetAllCachedOrganizations()
     {
         if (!AssertConnected("get all cached organizations") || _connection?.Db?.Organization == null)
             return Enumerable.Empty<OrganizationData>();

         return _connection.Db.Organization.Iter().Select(MapToOrganizationData).Where(dto => dto != null);
     }

    public IEnumerable<OrganizationNoticeData> GetAllCachedOrganizationNotices(uint organizationId)
    {
        if (!AssertConnected("get all cached organization notices") || _connection?.Db?.OrganizationNotice == null)
            return Enumerable.Empty<OrganizationNoticeData>();

        return _connection.Db.OrganizationNotice.OrganizationId.Filter(organizationId)
                                           .Select(MapToOrganizationNoticeData)
                                           .Where(dto => dto != null);
    }

    public IEnumerable<OrganizationMemberData> GetCachedOrganizationMembers(uint organizationId)
    {
        if (!AssertConnected("get cached organization members") || _connection?.Db?.OrganizationMember == null)
            return Enumerable.Empty<OrganizationMemberData>();

        return _connection.Db.OrganizationMember.Iter()
                                               .Where(member => member.OrganizationId == organizationId)
                                               .Select(MapToOrganizationMemberData)
                                               .Where(dto => dto != null);
    }

    public IEnumerable<OrganizationMemberRequestData> GetCachedPendingJoinRequestsForCurrentUser()
    {
        if (!AssertConnected("get cached pending join requests for current user")) 
            return Enumerable.Empty<OrganizationMemberRequestData>();

        string currentUserId = this.CurrentUserId;
        if (string.IsNullOrEmpty(currentUserId))
        {
            Debug.LogWarning("GetCachedPendingJoinRequestsForCurrentUser: CurrentUserId is unknown.");
            return Enumerable.Empty<OrganizationMemberRequestData>();
        }

        var requestTable = _connection?.Db?.OrganizationMemberRequest;
        if (requestTable == null)
        {
            Debug.LogWarning("GetCachedPendingJoinRequestsForCurrentUser: OrganizationMemberRequest table handle is null.");
            return Enumerable.Empty<OrganizationMemberRequestData>();
        }

        return requestTable.Iter()
                           .Where(req => req.RequesterIdentity.ToString() == currentUserId)
                           .Select(MapToOrganizationMemberRequestData)
                           .Where(dto => dto != null)
                           .ToList(); 
    }

    public IEnumerable<OrganizationMemberRequestData> GetCachedJoinRequestsForOwnedOrganizations()
    {
        if (!AssertConnected("get cached join requests for owned organizations")) 
            return Enumerable.Empty<OrganizationMemberRequestData>();

        string currentUserId = this.CurrentUserId;
        if (string.IsNullOrEmpty(currentUserId))
        {
            Debug.LogWarning("GetCachedJoinRequestsForOwnedOrganizations: CurrentUserId is unknown.");
            return Enumerable.Empty<OrganizationMemberRequestData>();
        }

        var organizationTable = _connection.Db.Organization;
        var requestTable = _connection.Db.OrganizationMemberRequest;

        if (organizationTable == null || requestTable == null)
        {
            Debug.LogError("GetCachedJoinRequestsForOwnedOrganizations: Required tables are not available.");
            return Enumerable.Empty<OrganizationMemberRequestData>();
        }

        var ownedOrgIds = organizationTable.Iter()
            .Where(org => org.OwnerIdentity.ToString() == currentUserId)
            .Select(org => org.Id)
            .ToHashSet();

        if (!ownedOrgIds.Any())
        {
            return Enumerable.Empty<OrganizationMemberRequestData>(); // No organizations owned by current user
        }

        var filteredRequests = requestTable.Iter()
            .Where(req => ownedOrgIds.Contains(req.OrganizationId))
            .Select(MapToOrganizationMemberRequestData)
            .Where(dto => dto != null)
            .ToList();
        return filteredRequests;
    }

    public OrganizationData GetCachedOrganizationById(uint orgId)
    {
        if (!AssertConnected("get cached organization by id") || _connection?.Db?.Organization == null)
            return null;

        var organizationTable = _connection.Db.Organization;
        if (organizationTable == null)
        {
            Debug.LogWarning("GetCachedOrganizationById: Organization table handle is null.");
            return null;
        }

        var organization = organizationTable.Iter()
            .FirstOrDefault(org => org.Id == orgId);
        if (organization == null)
        {
            Debug.LogWarning($"GetCachedOrganizationById: Organization with ID {orgId} not found.");
            return null;
        }

        return MapToOrganizationData(organization);
    }
    #endregion

    #region Organization Mapping Functions
      private OrganizationData MapToOrganizationData(SpacetimeDB.Types.Organization spdbOrg)
     {
         if (spdbOrg == null) return null;
         return new OrganizationData {
             InternalId = spdbOrg.Id,
             Name = spdbOrg.Name,
             OwnerId = spdbOrg.OwnerIdentity.ToString(),
             OwnerDisplayName = spdbOrg.OwnerDisplayName,
             CreatedAtUtc = TimestampToDateTime(spdbOrg.CreatedAt)
         };
     }

     private OrganizationNoticeData MapToOrganizationNoticeData(SpacetimeDB.Types.OrganizationNotice spdbNotice)
     {
         if (spdbNotice == null) return null;
         return new OrganizationNoticeData
         {
             NoticeId = spdbNotice.NoticeId,
             OrganizationId = spdbNotice.OrganizationId,
             PosterId = spdbNotice.PosterIdentity.ToString(),
             Content = spdbNotice.Content,
             CreatedAtUtc = TimestampToDateTime(spdbNotice.CreatedAt),
             DurationSeconds = spdbNotice.DurationSeconds,
             ExpiresAtUtc = TimestampToDateTime(spdbNotice.ExpiresAt)
         };
     }

    private OrganizationMemberData MapToOrganizationMemberData(SpacetimeDB.Types.OrganizationMember spdbMember)
    {
        if (spdbMember == null) return null;
        return new OrganizationMemberData
        {
            Id = spdbMember.Id,
            OrganizationId = spdbMember.OrganizationId,
            MemberId = spdbMember.MemberIdentity.ToString()
        };
    }

    private OrganizationMemberRequestData MapToOrganizationMemberRequestData(SpacetimeDB.Types.OrganizationMemberRequest spdbRequest)
    {
        if (spdbRequest == null) return null;
        return new OrganizationMemberRequestData
        {
            Id = spdbRequest.Id,
            OrganizationId = spdbRequest.OrganizationId,
            RequesterId = spdbRequest.RequesterIdentity.ToString(),
            CreatedAtUtc = TimestampToDateTime(spdbRequest.CreatedAt)
        };
    }
    #endregion
} 