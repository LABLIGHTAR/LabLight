using System;

/// <summary>
/// DTO for organization_member table data.
/// </summary>
public class OrganizationMemberData
{
    public ulong Id { get; set; }
    public uint OrganizationId { get; set; }
    public string MemberId { get; set; } // Maps from Identity
} 