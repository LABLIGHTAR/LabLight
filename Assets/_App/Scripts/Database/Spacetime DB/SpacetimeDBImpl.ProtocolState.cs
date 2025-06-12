using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using SpacetimeDB;
using SpacetimeDB.Types;

public partial class SpacetimeDBImpl
{
    #region ProtocolState Reducer Calls
    public void CreateProtocolState(uint protocolId, uint organizationId, string state) {
        if (!AssertConnected("create protocol state")) return;
         Debug.Log($"SpacetimeDB: Requesting creation of state for protocol ID: {protocolId}, Org ID: {organizationId}");
        _connection.Reducers.TryCreateProtocolState(protocolId, organizationId, state);
    }

    public void DeleteProtocolState(uint protocolStateId) {
        if (!AssertConnected("delete protocol state")) return;
         Debug.Log($"SpacetimeDB: Requesting delete for protocol state ID: {protocolStateId}");
        _connection.Reducers.TryDeleteProtocolState(protocolStateId);
    }

    public void EditProtocolState(uint protocolStateId, string newState)
    {
        if (!AssertConnected("edit protocol state")) return;
        if (string.IsNullOrWhiteSpace(newState)) { LogErrorAndInvoke("New state content cannot be empty."); return; }
        Debug.Log($"SpacetimeDB: Requesting edit for protocol state ID: {protocolStateId}");
        _connection.Reducers.TryEditProtocolState(protocolStateId, newState);
    }
    #endregion

    #region ProtocolState Reducer Event Handlers
     private void OnTryCreateProtocolStateResult(ReducerEventContext ctx, uint protocolId, uint organizationId, string state) {
        HandleReducerResultBase(ctx, $"create protocol state for protocol {protocolId}");
     }

     private void OnTryDeleteProtocolStateResult(ReducerEventContext ctx, uint protocolStateId) {
        HandleReducerResultBase(ctx, $"delete protocol state {protocolStateId}");
     }
     
    private void OnTryEditProtocolStateResult(ReducerEventContext ctx, uint protocolStateId, string newState) {
        HandleReducerResultBase(ctx, $"edit protocol state {protocolStateId}");
    }
    #endregion

    #region ProtocolState Data Access
    public IEnumerable<ProtocolStateData> GetCachedProtocolStatesForOrg(uint organizationId)
    {
        if (!AssertConnected("get cached protocol states for org") || _connection?.Db?.ProtocolState == null)
            return Enumerable.Empty<ProtocolStateData>();

        Debug.LogWarning("GetCachedProtocolStatesForOrg: Cannot filter by OrganizationId directly on ProtocolState table. Returning all cached states.");
        return _connection.Db.ProtocolState.Iter()
                                         .Select(MapToProtocolStateData)
                                         .Where(dto => dto != null);
    }

    public ProtocolStateData GetCachedProtocolState(uint protocolStateId)
    {
        if (!AssertConnected("get cached protocol state") || _connection?.Db?.ProtocolState == null)
            return null;
        var spdbState = _connection.Db.ProtocolState.Id.Find(protocolStateId);
        return MapToProtocolStateData(spdbState);
    }

    public ProtocolStateOwnershipData GetCachedProtocolStateOwnership(uint protocolStateId)
    {
         if (!AssertConnected("get cached protocol state ownership") || _connection?.Db?.ProtocolStateOwnership == null)
            return null;
        var spdbOwnership = _connection.Db.ProtocolStateOwnership.ProtocolStateId.Find(protocolStateId);
        return MapToProtocolStateOwnershipData(spdbOwnership);
    }

    public IEnumerable<ProtocolStateEditHistoryData> GetCachedProtocolStateEditHistory(uint protocolStateId)
    {
         if (!AssertConnected("get cached protocol state edit history") || _connection?.Db?.ProtocolStateEditHistory == null)
            return Enumerable.Empty<ProtocolStateEditHistoryData>();
        return _connection.Db.ProtocolStateEditHistory.ProtocolStateId.Filter(protocolStateId)
                                                  .Select(MapToProtocolStateEditHistoryData)
                                                  .Where(dto => dto != null);
    }
    #endregion

    #region ProtocolState Mapping Functions
     private ProtocolStateData MapToProtocolStateData(SpacetimeDB.Types.ProtocolState spdbState)
    {
        if (spdbState == null) return null;
        return new ProtocolStateData
        {
            Id = spdbState.Id,
            ProtocolId = spdbState.ProtocolId,
            CreatorId = spdbState.CreatorIdentity.ToString(),
            State = spdbState.State,
            CreatedAtUtc = TimestampToDateTime(spdbState.CreatedAt),
            EditedAtUtc = TimestampToDateTime(spdbState.EditedAt)
        };
    }

    private ProtocolStateOwnershipData MapToProtocolStateOwnershipData(SpacetimeDB.Types.ProtocolStateOwnership spdbOwnership)
    {
        if (spdbOwnership == null) return null;
        return new ProtocolStateOwnershipData
        {
            ProtocolStateId = spdbOwnership.ProtocolStateId,
            OwnerId = spdbOwnership.OwnerIdentity.ToString(),
            OrganizationId = spdbOwnership.OrganizationId,
            OwnerDisplayName = spdbOwnership.OwnerDisplayName
        };
    }

     private ProtocolStateEditHistoryData MapToProtocolStateEditHistoryData(SpacetimeDB.Types.ProtocolStateEditHistory spdbHistory)
    {
        if (spdbHistory == null) return null;
        return new ProtocolStateEditHistoryData
        {
            EditId = spdbHistory.EditId,
            ProtocolStateId = spdbHistory.ProtocolStateId,
            EditorId = spdbHistory.EditorIdentity.ToString(),
            EditedAtUtc = TimestampToDateTime(spdbHistory.EditedAt)
        };
    }
    #endregion
} 