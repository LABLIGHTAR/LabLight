using System;

[Serializable] // Optional: Makes it viewable in Inspector if needed elsewhere
public class UserData
{
    public string Id; // Use string for generic ID representation
    public string Name;
    public bool IsOnline;
    public DateTime CreatedAtUtc; // Use DateTime for generic representation
    public DateTime LastOnlineUtc;

    // Add constructors or helper methods if needed
}