// Assets/Scripts/Models/OrganizationData.cs
using System;
using System.Collections.Generic; // If storing members later

[Serializable]
public class OrganizationData
{
    public uint InternalId; // Keep internal ID if needed for referencing
    public string Name;
    public string OwnerId; // Use string for generic ID
    public string OwnerDisplayName; // Added: Denormalized name
    public DateTime CreatedAtUtc;
    // Maybe add List<string> MemberIds later if needed by UI directly
}
