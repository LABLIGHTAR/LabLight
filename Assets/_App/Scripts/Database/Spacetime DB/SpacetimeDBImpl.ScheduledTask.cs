using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using SpacetimeDB;
using SpacetimeDB.Types;

public partial class SpacetimeDBImpl
{
    #region ScheduledTask Reducer Calls
    public void ScheduleProtocolTask(uint organizationId, uint protocolId, uint protocolStateId, System.Collections.Generic.List<Identity> assigneeIdentities, uint startStep, uint endStep, Timestamp scheduledAt, Timestamp dueAt)
    {
        if (!AssertConnected("schedule protocol task")) return;
        if (assigneeIdentities == null || assigneeIdentities.Count == 0) { LogErrorAndInvoke("Assignee list cannot be empty."); return; }
        Debug.Log($"SpacetimeDB: Requesting schedule task for protocol {protocolId} in org {organizationId}");
        _connection.Reducers.TryScheduleProtocolTask(organizationId, protocolId, protocolStateId, assigneeIdentities, startStep, endStep, scheduledAt, dueAt);
    }

    public void StartScheduledTask(ulong taskId)
    {
        if (!AssertConnected("start scheduled task")) return;
        Debug.Log($"SpacetimeDB: Requesting start for task ID: {taskId}");
        _connection.Reducers.TryStartScheduledTask(taskId);
    }

    public void CompleteScheduledTask(ulong taskId)
    {
        if (!AssertConnected("complete scheduled task")) return;
        Debug.Log($"SpacetimeDB: Requesting complete for task ID: {taskId}");
        _connection.Reducers.TryCompleteScheduledTask(taskId);
    }

    public void CancelScheduledTask(ulong taskId)
    {
        if (!AssertConnected("cancel scheduled task")) return;
        Debug.Log($"SpacetimeDB: Requesting cancel for task ID: {taskId}");
        _connection.Reducers.TryCancelScheduledTask(taskId);
    }
    #endregion

    #region ScheduledTask Reducer Event Handlers
    private void OnTryScheduleProtocolTaskResult(ReducerEventContext ctx, uint organizationId, uint protocolId, uint protocolStateId, System.Collections.Generic.List<Identity> assigneeIdentities, uint startStep, uint endStep, Timestamp scheduledAt, Timestamp dueAt) {
        HandleReducerResultBase(ctx, $"schedule protocol task for protocol {protocolId} in org {organizationId}");
    }

    private void OnTryStartScheduledTaskResult(ReducerEventContext ctx, ulong taskId) {
        HandleReducerResultBase(ctx, $"start scheduled task {taskId}");
    }

    private void OnTryCompleteScheduledTaskResult(ReducerEventContext ctx, ulong taskId) {
        HandleReducerResultBase(ctx, $"complete scheduled task {taskId}");
    }

    private void OnTryCancelScheduledTaskResult(ReducerEventContext ctx, ulong taskId) {
        HandleReducerResultBase(ctx, $"cancel scheduled task {taskId}");
    }
    #endregion

    #region ScheduledTask Data Access
    public IEnumerable<ScheduledProtocolTaskData> GetAllCachedScheduledTasksForOrg(uint organizationId)
    {
        if (!AssertConnected("get all cached scheduled tasks for org") || _connection?.Db?.ScheduledProtocolTask == null)
            return Enumerable.Empty<ScheduledProtocolTaskData>();

        return _connection.Db.ScheduledProtocolTask.OrganizationId.Filter(organizationId)
                                           .Select(MapToScheduledProtocolTaskData)
                                           .Where(dto => dto != null);
    }

    public IEnumerable<ScheduledTaskAssigneeData> GetCachedAssigneesForTask(ulong taskId)
    {
        if (!AssertConnected("get cached assignees for task") || _connection?.Db?.ScheduledTaskAssignee == null)
            return Enumerable.Empty<ScheduledTaskAssigneeData>();

        return _connection.Db.ScheduledTaskAssignee.TaskId.Filter(taskId)
                                                .Select(MapToScheduledTaskAssigneeData)
                                                .Where(dto => dto != null);
    }
    #endregion

    #region ScheduledTask Mapping Functions
    private ScheduledProtocolTaskData MapToScheduledProtocolTaskData(SpacetimeDB.Types.ScheduledProtocolTask spdbTask)
    {
        if (spdbTask == null) return null;
        return new ScheduledProtocolTaskData
        {
            TaskId = spdbTask.TaskId,
            OrganizationId = spdbTask.OrganizationId,
            ProtocolId = spdbTask.ProtocolId,
            ProtocolStateId = spdbTask.ProtocolStateId,
            AssignerId = spdbTask.AssignerIdentity.ToString(),
            StartStep = spdbTask.StartStep,
            EndStep = spdbTask.EndStep,
            ScheduledAtUtc = TimestampToDateTime(spdbTask.ScheduledAt),
            DueAtUtc = TimestampToDateTime(spdbTask.DueAt),
            CreatedAtUtc = TimestampToDateTime(spdbTask.CreatedAt),
            Status = MapToScheduledTaskStatus(spdbTask.Status), 
            CompletedAtUtc = spdbTask.CompletedAt.HasValue ? TimestampToDateTime(spdbTask.CompletedAt.Value) : (DateTime?)null
        };
    }

    private ScheduledTaskAssigneeData MapToScheduledTaskAssigneeData(SpacetimeDB.Types.ScheduledTaskAssignee spdbAssignee)
    {
        if (spdbAssignee == null) return null;
        return new ScheduledTaskAssigneeData
        {
            AssignmentId = spdbAssignee.AssignmentId,
            TaskId = spdbAssignee.TaskId,
            AssigneeId = spdbAssignee.AssigneeIdentity.ToString()
        };
    }

    private ScheduledTaskStatus MapToScheduledTaskStatus(SpacetimeDB.Types.ScheduledTaskStatus spdbStatus)
    {
        if (Enum.TryParse<ScheduledTaskStatus>(spdbStatus.ToString(), true, out var result))
        { return result; }
        else
        {
            Debug.LogWarning($"Failed to parse ScheduledTaskStatus: {spdbStatus.ToString()}. Defaulting to Pending.");
            return ScheduledTaskStatus.Pending;
        }
    }
    #endregion
} 