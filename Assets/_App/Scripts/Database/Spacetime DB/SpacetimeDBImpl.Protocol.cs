using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using SpacetimeDB;
using SpacetimeDB.Types;

public partial class SpacetimeDBImpl
{
    #region Protocol Table Callback Handlers
    private void HandleProtocolInsert(EventContext ctx, SpacetimeDB.Types.Protocol insertedProtocol)
    {
        Debug.Log($"Protocol inserted: ID={insertedProtocol.Id}, Name='{insertedProtocol.Name}'");
        var mappedData = MapToProtocolData(insertedProtocol);
        if (mappedData != null) {
            OnProtocolAdded?.Invoke(mappedData);
        }
        else {
            Debug.LogWarning($"Failed to map inserted protocol ID {insertedProtocol.Id} to ProtocolData.");
        }
    }

    private void HandleSavedProtocolInsert(EventContext ctx, SpacetimeDB.Types.SavedProtocol insertedRow)
    {
        if (_spacetimedbIdentity.HasValue && 
            insertedRow.UserIdentity.ToString() == _spacetimedbIdentity.Value.ToString())
        {
            Debug.Log($"SavedProtocol inserted for current user. Protocol ID: {insertedRow.ProtocolId}. Invoking event.");
            OnSavedProtocolAdded?.Invoke(insertedRow.ProtocolId);
        }
        else if (_spacetimedbIdentity.HasValue)
        {
             Debug.Log($"SavedProtocol inserted, but for other user ({insertedRow.UserIdentity}) or identity mismatch. Current user: {_spacetimedbIdentity.Value}");
        }
        else
        {
            Debug.LogWarning("SavedProtocol inserted, but local identity is null. Cannot compare.");
        }
    }

    private void HandleSavedProtocolDelete(EventContext ctx, SpacetimeDB.Types.SavedProtocol deletedRow)
    {
        if (_spacetimedbIdentity.HasValue && 
            deletedRow.UserIdentity.ToString() == _spacetimedbIdentity.Value.ToString())
        {
             Debug.Log($"SavedProtocol deleted for current user. Protocol ID: {deletedRow.ProtocolId}. Invoking event.");
            OnSavedProtocolRemoved?.Invoke(deletedRow.ProtocolId);
        }
        else if (_spacetimedbIdentity.HasValue)
        {
             Debug.Log($"SavedProtocol deleted, but for other user ({deletedRow.UserIdentity}) or identity mismatch. Current user: {_spacetimedbIdentity.Value}");
        }
         else
        {
            Debug.LogWarning("SavedProtocol deleted, but local identity is null. Cannot compare.");
        }
    }
    #endregion

    #region Protocol Reducer Calls
    public void CreateProtocol(string name, string content, bool isPublic, uint organizationId) {
        if (!AssertConnected("create protocol")) return;
        if (string.IsNullOrWhiteSpace(name)) { LogErrorAndInvoke("Protocol name cannot be empty."); return; }
         Debug.Log($"SpacetimeDB: Requesting protocol creation: {name}, Public: {isPublic}, OrgID: {organizationId}");
        _connection.Reducers.TryCreateProtocol(name, content, isPublic, organizationId);
    }

    public void EditProtocol(uint protocolId, string newName, string content, bool isPublic, uint organizationId) {
        if (!AssertConnected("edit protocol")) return;
        if (string.IsNullOrWhiteSpace(newName)) { LogErrorAndInvoke("Protocol name cannot be empty."); return; }
        Debug.Log($"SpacetimeDB: Requesting edit for protocol ID: {protocolId}, Name: {newName}, Public: {isPublic}, OrgID: {organizationId}");
        _connection.Reducers.TryEditProtocol(protocolId, newName, content, isPublic, organizationId);
    }

    public void DeleteProtocol(uint protocolId) {
        if (!AssertConnected("delete protocol")) return;
         Debug.Log($"SpacetimeDB: Requesting delete for protocol ID: {protocolId}");
        _connection.Reducers.TryDeleteProtocol(protocolId);
    }

    public void RollbackProtocol(uint protocolId)
    {
        if (!AssertConnected("rollback protocol")) return;
        Debug.Log($"SpacetimeDB: Requesting rollback for protocol ID: {protocolId}");
        _connection.Reducers.TryRollbackProtocol(protocolId);
    }

    public void SaveProtocol(uint protocolId)
    {
        if (!AssertConnected("save protocol")) return;
        Debug.Log($"SpacetimeDB: Requesting save for protocol ID: {protocolId}");
        _connection.Reducers.TrySaveProtocol(protocolId);
    }

    public void UnsaveProtocol(uint protocolId)
    {
        if (!AssertConnected("unsave protocol")) return;
        Debug.Log($"SpacetimeDB: Requesting unsave for protocol ID: {protocolId}");
        _connection.Reducers.TryUnsaveProtocol(protocolId);
    }

    public void TryForkProtocol(uint originalProtocolId, string newName, bool isPublic)
    {
        if (!AssertConnected("fork protocol")) return;
        if (string.IsNullOrWhiteSpace(newName)) { LogErrorAndInvoke("New name for forked protocol cannot be empty."); return; }
        Debug.Log($"SpacetimeDB: Requesting fork for protocol ID: {originalProtocolId} with new name '{newName}' (Public: {isPublic})");
        _connection.Reducers.TryForkProtocol(originalProtocolId, newName, isPublic);
    }
    #endregion

    #region Protocol Reducer Event Handlers
     private void OnTryCreateProtocolResult(ReducerEventContext ctx, string name, string content, bool isPublic, uint organizationId) {
         HandleReducerResultBase(ctx, $"create protocol '{name}' with OrgID {organizationId}");
         if (ctx.Event.Status is Status.Committed)
         {
             OnProtocolCreateSuccess?.Invoke(name);
         }
         else if (ctx.Event.Status is Status.Failed failedStatus) 
         {
             OnProtocolCreateFailure?.Invoke(name, failedStatus.ToString());
         }
     }

     private void OnTryEditProtocolResult(ReducerEventContext ctx, uint protocolId, string newName, string content, bool isPublic, uint? organizationId) {
         HandleReducerResultBase(ctx, $"edit protocol {protocolId} (OrgID: {organizationId?.ToString() ?? "None"})");
         if (ctx.Event.CallerIdentity == _spacetimedbIdentity)
         {
            if (ctx.Event.Status is Status.Committed)
            {   
                var updatedProtocolData = GetCachedProtocol(protocolId);
                if (updatedProtocolData != null) 
                {
                    OnProtocolEditSuccess?.Invoke(updatedProtocolData);
                }
                else 
                {   
                    Debug.LogWarning($"OnTryEditProtocolResult: Edit committed for {protocolId}, but updated data not found in cache immediately. Success event will not carry data.");
                }
            }
            else if (ctx.Event.Status is Status.Failed failedStatus)
            { 
                 OnProtocolEditFailure?.Invoke(protocolId, failedStatus.ToString());
            }
        }
     }

     private void OnTryDeleteProtocolResult(ReducerEventContext ctx, uint protocolId) {
         HandleReducerResultBase(ctx, $"delete protocol {protocolId}");
     }

    private void OnTryRollbackProtocolResult(ReducerEventContext ctx, uint protocolId) {
        HandleReducerResultBase(ctx, $"rollback protocol {protocolId}");
    }

     private void OnTrySaveProtocolResult(ReducerEventContext ctx, uint protocolId) { 
         HandleReducerResultBase(ctx, $"save protocol {protocolId}"); 
     }

    private void OnTryUnsaveProtocolResult(ReducerEventContext ctx, uint protocolId)
    {
        HandleReducerResultBase(ctx, $"unsave protocol {protocolId}");
    }

    private void OnTryForkProtocolResult(ReducerEventContext ctx, uint originalProtocolId, string newName, bool isPublic)
    {
        HandleReducerResultBase(ctx, $"fork protocol {originalProtocolId} into '{newName}'");
        if (ctx.Event.Status is Status.Committed && ctx.Event.CallerIdentity == _spacetimedbIdentity) {
             Debug.Log($"Successfully forked protocol {originalProtocolId} as '{newName}'. Invoking OnProtocolForkSuccess.");
             OnProtocolForkSuccess?.Invoke(originalProtocolId, newName);
        } 
    }
    #endregion

    #region Protocol Data Access
    public IEnumerable<ProtocolData> GetCachedProtocols()
    {
        if (!AssertConnected("get cached protocols") || 
            _connection?.Db?.Protocol == null ||
            _connection?.Db?.ProtocolOwnership == null ||
            _connection?.Db?.OrganizationMember == null)
        {
            Debug.LogWarning("GetCachedProtocols: Required table handles (Protocol, ProtocolOwnership, OrganizationMember) not available.");
            return Enumerable.Empty<ProtocolData>();
        }
        string currentUserId = this.CurrentUserId;
        if (string.IsNullOrEmpty(currentUserId))
        {
            Debug.LogWarning("GetCachedProtocols: Cannot filter protocols, CurrentUserId is unknown.");
            return Enumerable.Empty<ProtocolData>();
        }
        var userOrgIds = _connection.Db.OrganizationMember.Iter()
            .Where(m => m.MemberIdentity.ToString() == currentUserId)
            .Select(m => m.OrganizationId)
            .ToHashSet();
        return FilterAndMapVisibleProtocols(currentUserId, userOrgIds);
    }

    private IEnumerable<ProtocolData> FilterAndMapVisibleProtocols(string currentUserId, HashSet<uint> userOrgIds)
    {
        foreach (var spdbProtocol in _connection.Db.Protocol.Iter())
        {
            bool isVisible = false;
            if (spdbProtocol.IsPublic)
            {
                isVisible = true;
            }
            else
            {
                var ownership = _connection.Db.ProtocolOwnership.ProtocolId.Find(spdbProtocol.Id);
                if (ownership != null)
                {
                    if (ownership.OwnerIdentity.ToString() == currentUserId)
                    {
                        isVisible = true;
                    }
                    else if (ownership.OrganizationId != 0 && userOrgIds.Contains(ownership.OrganizationId))
                    {
                        isVisible = true;
                    }
                }
            }
            if (isVisible)
            {
                var mappedData = MapToProtocolData(spdbProtocol);
                if (mappedData != null)
                {
                    yield return mappedData;
                }
            }
        }
    }

    public ProtocolData GetCachedProtocol(uint protocolId)
    {
         if (!AssertConnected("get cached protocol") || _connection?.Db?.Protocol == null)
            return null;
        var spdbProtocol = _connection.Db.Protocol.Id.Find(protocolId);
        return MapToProtocolData(spdbProtocol);
    }

    public IEnumerable<ProtocolEditHistoryData> GetCachedProtocolEditHistory(uint protocolId)
    {
        if (!AssertConnected("get cached protocol edit history") || _connection?.Db?.ProtocolEditHistory == null)
            return Enumerable.Empty<ProtocolEditHistoryData>();
        return _connection.Db.ProtocolEditHistory.ProtocolId.Filter(protocolId)
                                           .Select(MapToProtocolEditHistoryData)
                                           .Where(dto => dto != null);
    }

    public ProtocolOwnershipData GetCachedProtocolOwnership(uint protocolId)
    {
        if (!AssertConnected("get cached protocol ownership") || _connection?.Db?.ProtocolOwnership == null)
            return null;
        var spdbOwnership = _connection.Db.ProtocolOwnership.ProtocolId.Find(protocolId);
        return MapToProtocolOwnershipData(spdbOwnership);
    }

    public IEnumerable<ProtocolData> GetSavedProtocols()
    {
        if (!AssertConnected("get saved protocols") || 
            _connection?.Db?.SavedProtocol == null)
        {
             Debug.LogWarning("GetSavedProtocols: SavedProtocol table handle not available.");
            yield break; 
        }
        string currentUserId = this.CurrentUserId;
        if (string.IsNullOrEmpty(currentUserId))
        {
            Debug.LogWarning("GetSavedProtocols: Cannot filter saved protocols, CurrentUserId is unknown.");
            yield break;
        }
        foreach (var savedEntry in _connection.Db.SavedProtocol.Iter()
                     .Where(sp => sp.UserIdentity.ToString() == currentUserId))
        {
            ProtocolData protocolData = GetCachedProtocol(savedEntry.ProtocolId);
            if (protocolData != null)
            {
                yield return protocolData;
            }
            else
            {
                Debug.LogWarning($"GetSavedProtocols: Found saved entry for ProtocolId {savedEntry.ProtocolId}, but couldn't retrieve full ProtocolData.");
            }
        }
    }

    public bool IsProtocolSavedByUser(uint protocolId, string userId)
    {
        if (!AssertConnected("check if protocol is saved") || string.IsNullOrEmpty(userId))
             return false;
        var savedProtocolHandle = _connection?.Db?.SavedProtocol;
        if (savedProtocolHandle == null) {
             Debug.LogWarning("IsProtocolSavedByUser: SavedProtocol table handle is null.");
             return false;
        }
        bool isSaved = savedProtocolHandle.Iter().Any(sp => sp.ProtocolId == protocolId && sp.UserIdentity.ToString() == userId);
        Debug.Log($"IsProtocolSavedByUser Check: ProtocolId={protocolId}, Checking against UserId String='{userId}', Result={isSaved}");
        return isSaved;
    }
    #endregion

    #region Protocol Mapping Functions
    private ProtocolData MapToProtocolData(SpacetimeDB.Types.Protocol spdbProtocol)
    {
        if (spdbProtocol == null) return null;
        var ownership = GetCachedProtocolOwnership(spdbProtocol.Id);
        return new ProtocolData
        {
            Id = spdbProtocol.Id,
            Name = spdbProtocol.Name,
            Content = spdbProtocol.Content,
            CreatedAtUtc = TimestampToDateTime(spdbProtocol.CreatedAt),
            EditedAtUtc = TimestampToDateTime(spdbProtocol.EditedAt),
            Version = spdbProtocol.Version,
            IsPublic = spdbProtocol.IsPublic,
            OwnerId = ownership?.OwnerId ?? "Unknown",
            OwnerDisplayName = ownership?.OwnerDisplayName ?? "Unknown",
            OrganizationId = ownership?.OrganizationId ?? 0 
        };
    }

    private ProtocolEditHistoryData MapToProtocolEditHistoryData(SpacetimeDB.Types.ProtocolEditHistory spdbHistory)
    {
        if (spdbHistory == null) return null;
        return new ProtocolEditHistoryData
        {
            EditId = spdbHistory.EditId,
            ProtocolId = spdbHistory.ProtocolId,
            EditorId = spdbHistory.EditorIdentity.ToString(),
            EditedAtUtc = TimestampToDateTime(spdbHistory.EditedAt),
            Version = spdbHistory.Version,
            PreviousContent = spdbHistory.PreviousContent
        };
    }

    private ProtocolOwnershipData MapToProtocolOwnershipData(SpacetimeDB.Types.ProtocolOwnership spdbOwnership)
    {
        if (spdbOwnership == null) return null;
        return new ProtocolOwnershipData
        {
            ProtocolId = spdbOwnership.ProtocolId,
            OwnerId = spdbOwnership.OwnerIdentity.ToString(),
            OrganizationId = spdbOwnership.OrganizationId,
            OwnerDisplayName = spdbOwnership.OwnerDisplayName
        };
    }
    #endregion
} 