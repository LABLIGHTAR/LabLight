using System;

[Serializable] // Optional: Makes it viewable in Inspector if needed elsewhere
public class UserData
{
    public string Id; // This will be the Auth Provider's User ID (e.g., FirebaseId)
    public string SpacetimeId; // This will be the Database's primary identity for the user.
    public string Name;
    public bool IsOnline;
    public DateTime CreatedAtUtc; // Use DateTime for generic representation
    public DateTime LastOnlineUtc;

    // Add constructors or helper methods if needed
}