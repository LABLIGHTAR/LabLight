using System;

/// <summary>
/// DTO for protocol_state_ownership table data.
/// </summary>
[Serializable]
public class ProtocolStateOwnershipData
{
    public uint ProtocolStateId { get; set; }
    public string OwnerId { get; set; } // Maps from Identity (Creator)
    public uint OrganizationId { get; set; } // 0 if owned by user
    public string OwnerDisplayName { get; set; } // Added: Denormalized name
} 