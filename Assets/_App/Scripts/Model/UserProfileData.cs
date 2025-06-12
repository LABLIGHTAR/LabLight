using UnityEngine;
using Newtonsoft.Json;

public class UserProfileData
{
    [JsonProperty("name")]
    private string name;

    [JsonProperty("userId")]
    private string userId;

    public UserProfileData(string name)
    {
        this.name = name;
    }

    public UserProfileData(string userId, string name)
    {
        this.userId = userId;
        this.name = name;
    }

    [JsonConstructor]
    public UserProfileData()
    {
        // Parameterless constructor for JSON deserialization
    }

    public string GetName()
    {
        return name;
    }

    public void SetName(string name)
    {
        this.name = name;
    }

    public string GetUserId()
    {
        return userId;
    }

    public void SetUserId(string userId)
    {
        this.userId = userId;
    }
}
