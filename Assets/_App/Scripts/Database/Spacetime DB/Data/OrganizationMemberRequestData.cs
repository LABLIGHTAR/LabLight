using System;

[Serializable]
public class OrganizationMemberRequestData
{
    public ulong Id;
    public uint OrganizationId;
    public string RequesterId; // Assuming Identity is mapped to string
    public DateTime CreatedAtUtc;
} 