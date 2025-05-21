using System;

/// <summary>
/// DTO for protocol_ownership table data.
/// </summary>
[Serializable]
public class ProtocolOwnershipData
{
    public uint ProtocolId { get; set; }
    public string OwnerId { get; set; } // Maps from Identity
    public uint OrganizationId { get; set; } // 0 if owned by user
    public string OwnerDisplayName { get; set; } // Added: Denormalized name
} 