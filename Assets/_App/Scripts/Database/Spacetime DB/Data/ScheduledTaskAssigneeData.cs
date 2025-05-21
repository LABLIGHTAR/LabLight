using System;

/// <summary>
/// DTO for scheduled_task_assignee table data.
/// </summary>
public class ScheduledTaskAssigneeData
{
    public ulong AssignmentId { get; set; }
    public ulong TaskId { get; set; }
    public string AssigneeId { get; set; } // Maps from SpacetimeDB Identity
} 