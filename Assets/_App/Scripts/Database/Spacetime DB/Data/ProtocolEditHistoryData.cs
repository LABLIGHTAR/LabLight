using System;

/// <summary>
/// DTO for protocol_edit_history table data.
/// </summary>
public class ProtocolEditHistoryData
{
    public ulong EditId { get; set; }
    public uint ProtocolId { get; set; }
    public string EditorId { get; set; } // Maps from SpacetimeDB Identity
    public DateTime EditedAtUtc { get; set; }
    public ulong Version { get; set; }
    public string PreviousContent { get; set; }
} 