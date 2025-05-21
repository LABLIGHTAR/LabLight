using System;

/// <summary>
/// DTO for client_device table data.
/// </summary>
public class ClientDeviceData
{
    public string Id { get; set; } // Maps from SpacetimeDB Identity
    public bool IsConnected { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastConnectedUtc { get; set; }
    public DateTime LastDisconnectedUtc { get; set; }
} 