using System;

/// <summary>
/// DTO for protocol_state_edit_history table data.
/// </summary>
public class ProtocolStateEditHistoryData
{
    public ulong EditId { get; set; }
    public uint ProtocolStateId { get; set; }
    public string EditorId { get; set; } // Maps from Identity
    public DateTime EditedAtUtc { get; set; }
} 