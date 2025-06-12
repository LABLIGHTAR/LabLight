using System;

/// <summary>
/// DTO for organization_notice table data.
/// </summary>
public class OrganizationNoticeData
{
    public ulong NoticeId { get; set; }
    public uint OrganizationId { get; set; }
    public string PosterId { get; set; } // Maps from SpacetimeDB Identity
    public string Content { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public ulong DurationSeconds { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
} 