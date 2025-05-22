using System;

[Serializable]
public class LocalUserProfileData : UserData
{
    public string Email;

    // Optional: Constructor for convenience
    public LocalUserProfileData() : base() { }

    public LocalUserProfileData(UserData baseData, string email)
    {
        Id = baseData.Id;
        Name = baseData.Name;
        IsOnline = baseData.IsOnline;
        CreatedAtUtc = baseData.CreatedAtUtc;
        LastOnlineUtc = baseData.LastOnlineUtc;
        Email = email;
    }

    public string GetName()
    {
        return Name;
    }
} 