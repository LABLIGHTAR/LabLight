using System;

/// <summary>
/// DTO for scheduled_protocol_task table data.
/// </summary>
public class ScheduledProtocolTaskData
{
    public ulong TaskId { get; set; }
    public uint OrganizationId { get; set; }
    public uint ProtocolId { get; set; }
    public uint ProtocolStateId { get; set; }
    public string AssignerId { get; set; } // Maps from SpacetimeDB Identity
    public uint StartStep { get; set; }
    public uint EndStep { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public DateTime DueAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public ScheduledTaskStatus Status { get; set; } // Use the C# enum
    public DateTime? CompletedAtUtc { get; set; } // Nullable DateTime
} 