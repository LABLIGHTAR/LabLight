using System;

/// <summary>
/// DTO for protocol_state table data.
/// </summary>
public class ProtocolStateData
{
    public uint Id { get; set; }
    public uint ProtocolId { get; set; }
    public string UserId { get; set; } // Maps from Identity
    public string CreatorId { get; set; } // Maps from Identity
    public uint OrganizationId { get; set; } // 0 if user-specific
    public string State { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime EditedAtUtc { get; set; }
} 