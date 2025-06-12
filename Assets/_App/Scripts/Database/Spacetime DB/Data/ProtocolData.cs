using System;

/// <summary>
/// DTO for protocol table data.
/// Note: This combines info potentially from 'protocol' and 'protocol_ownership'.
/// </summary>
public class ProtocolData
{
    public uint Id { get; set; }
    public string Name { get; set; }
    public string Content { get; set; } // Might be large, consider fetching on demand?
    public DateTime CreatedAtUtc { get; set; }
    public DateTime EditedAtUtc { get; set; }
    public ulong Version { get; set; }
    public bool IsPublic { get; set; }
    public string OwnerId { get; set; } // Mapped from Identity
    public string OwnerDisplayName { get; set; } // Added: Denormalized name (from Ownership table)
    public uint OrganizationId { get; set; } // 0 if owned by user
    // Add OwnerName if needed, fetched separately
}
