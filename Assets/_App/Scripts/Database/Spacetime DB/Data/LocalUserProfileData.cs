using System;

[Serializable]
public class LocalUserProfileData : UserData
{
    public string Email;
    public string LocalProfileImagePath { get; set; }

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

    // Constructor including LocalProfileImagePath
    public LocalUserProfileData(UserData baseData, string email, string localProfileImagePath) : this(baseData, email)
    {
        LocalProfileImagePath = localProfileImagePath;
    }

    public string GetName()
    {
        return Name;
    }
} 