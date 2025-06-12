using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UniRx;
using UnityEngine;

/// <summary>
/// Implements IDataProvider interface for accessing available procedures and updating runtime state
/// </summary>
public class LocalFileDataProvider : IProtocolDataProvider, ITextDataProvider, IAnchorDataProvider, IUserProfileDataProvider
{
    private const string ANCHOR_DATA_FILENAME = "AnchorData.json";
    private const string USER_PROFILES_DIRECTORY = "UserProfiles";
    private const string USER_PROFILE_DATA_FILENAME = "UserProfileData.json";
    private readonly string _persistentDataPath;
    
    public LocalFileDataProvider()
    {
        _persistentDataPath = Application.persistentDataPath;
    }

    public async Task<List<ProtocolDefinition>> GetProtocolList()
    {
        var protocolJsonFiles = new List<string>();
        
        if (!Directory.Exists(_persistentDataPath))
        {
            Debug.LogWarning($"Persistent data path does not exist: {_persistentDataPath}");
            return new List<ProtocolDefinition>();
        }

        try
        {
            var directoryInfo = new DirectoryInfo(_persistentDataPath);
            var jsonFiles = directoryInfo.GetFiles("*.json");
            
            foreach (var file in jsonFiles)
            {
                try
                {
                    using var streamReader = new StreamReader(file.FullName);
                    string jsonContent = await streamReader.ReadToEndAsync();
                    protocolJsonFiles.Add(jsonContent);
                    Debug.Log($"Successfully loaded protocol file: {file.Name}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to read protocol file {file.Name}: {ex.Message}");
                    // Continue with other files even if one fails
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error accessing protocol files: {ex.Message}");
            return new List<ProtocolDefinition>();
        }

        return Parsers.ParseProtocolList(protocolJsonFiles);
    }

    public void DeleteProtocolDefinition(string protocolName)
    {
        if (string.IsNullOrEmpty(protocolName))
        {
            Debug.LogWarning("Attempted to delete protocol with null or empty name");
            return;
        }

        try
        {
            string filePath = Path.Combine(_persistentDataPath, $"{protocolName}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Debug.Log($"Successfully deleted protocol: {protocolName}");
            }
            else
            {
                Debug.LogWarning($"Protocol file not found for deletion: {protocolName}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to delete protocol {protocolName}: {ex.Message}");
        }
    }

    public async Task<string> LoadTextFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogWarning("Attempted to load text file with null or empty path");
            return string.Empty;
        }

        try
        {
            string fullPath = Path.Combine(_persistentDataPath, filePath);
            using var reader = new StreamReader(fullPath);
            var content = await reader.ReadToEndAsync();
            Debug.Log($"Successfully loaded text file: {filePath}");
            return content;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load text file {filePath}: {ex.Message}");
            return string.Empty;
        }
    }

    public async void SaveTextFile(string filePath, string contents)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogWarning("Attempted to save text file with null or empty path");
            return;
        }

        try
        {
            string fullPath = Path.Combine(_persistentDataPath, filePath);
            await File.WriteAllTextAsync(fullPath, contents);
            Debug.Log($"Successfully saved text file: {filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save text file {filePath}: {ex.Message}");
        }
    }

    public void DeleteTextFile(string fileName, string fileExt)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogWarning("Attempted to delete file with null or empty name");
            return;
        }

        try
        {
            string fullPath = Path.Combine(_persistentDataPath, $"{fileName}{fileExt}");
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                Debug.Log($"Successfully deleted file: {fileName}{fileExt}");
            }
            else
            {
                Debug.LogWarning($"File not found for deletion: {fileName}{fileExt}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to delete file {fileName}{fileExt}: {ex.Message}");
        }
    }

    public IObservable<AnchorData> GetOrCreateAnchorData()
    {
        return LoadAnchorDataAsync().ToObservable();
    }

    public async Task<AnchorData> LoadAnchorDataAsync()
    {
        string anchorDataPath = Path.Combine(_persistentDataPath, ANCHOR_DATA_FILENAME);
        Debug.Log($"Loading anchor data from: {anchorDataPath}");

        try
        {
            if (File.Exists(anchorDataPath))
            {
                using var reader = new StreamReader(anchorDataPath);
                string jsonContent = await reader.ReadToEndAsync();
                var anchorData = Parsers.ParseAnchorData(jsonContent);
                
                if (anchorData != null)
                {
                    Debug.Log("Successfully loaded anchor data");
                    return anchorData;
                }
            }

            Debug.Log("Creating new anchor data");
            return new AnchorData { version = 1 };
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load anchor data: {ex.Message}");
            return new AnchorData { version = 1 };
        }
    }

    public async void SaveAnchorData(AnchorData anchorData)
    {
        if (anchorData == null)
        {
            Debug.LogWarning("Attempted to save null anchor data");
            return;
        }

        try
        {
            string fullPath = Path.Combine(_persistentDataPath, ANCHOR_DATA_FILENAME);
            string jsonContent = JsonConvert.SerializeObject(anchorData, Formatting.Indented, Parsers.serializerSettings);
            await File.WriteAllTextAsync(fullPath, jsonContent);
            Debug.Log("Successfully saved anchor data");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save anchor data: {ex.Message}");
        }
    }

    private string EnsureUserProfilesDirectory()
    {
        string profilesPath = Path.Combine(_persistentDataPath, USER_PROFILES_DIRECTORY);
        if (!Directory.Exists(profilesPath))
        {
            Directory.CreateDirectory(profilesPath);
        }
        return profilesPath;
    }

    public IObservable<List<UserProfileData>> GetAllUserProfiles()
    {
        return LoadAllUserProfilesAsync().ToObservable();
    }

    private async Task<List<UserProfileData>> LoadAllUserProfilesAsync()
    {
        var profiles = new List<UserProfileData>();
        string profilesPath = EnsureUserProfilesDirectory();

        try
        {
            var directoryInfo = new DirectoryInfo(profilesPath);
            var jsonFiles = directoryInfo.GetFiles("*.json");
            
            foreach (var file in jsonFiles)
            {
                using var reader = new StreamReader(file.FullName);
                string jsonContent = await reader.ReadToEndAsync();
                var profile = JsonConvert.DeserializeObject<UserProfileData>(jsonContent);
                if (profile != null)
                {
                    profiles.Add(profile);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load user profiles: {ex.Message}");
        }

        return profiles;
    }

    public IObservable<UserProfileData> GetOrCreateUserProfile(string userId)
    {
        return LoadUserProfileDataAsync(userId).ToObservable();
    }

    public async Task<UserProfileData> LoadUserProfileDataAsync(string userId)
    {
        string profilesPath = EnsureUserProfilesDirectory();
        string userProfilePath = Path.Combine(profilesPath, $"{userId}_{USER_PROFILE_DATA_FILENAME}");
        Debug.Log($"Loading user profile data from: {userProfilePath}");

        try
        {
            if (File.Exists(userProfilePath))
            {
                using var reader = new StreamReader(userProfilePath);
                string jsonContent = await reader.ReadToEndAsync();
                var userProfileData = JsonConvert.DeserializeObject<UserProfileData>(jsonContent);
                if (userProfileData != null)
                {
                    userProfileData.SetUserId(userId);
                    Debug.Log($"Successfully loaded user profile data for user: {userId}");
                    return userProfileData;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load user profile data for user {userId}: {ex.Message}");
        }
        
        return new UserProfileData(userId, $"User_{userId}");
    }

    public async void SaveUserProfileData(string userId, UserProfileData userProfileData)
    {
        if (userProfileData == null)
        {
            Debug.LogWarning("Attempted to save null user profile data");
            return;
        }

        try
        {
            string profilesPath = EnsureUserProfilesDirectory();
            string fullPath = Path.Combine(profilesPath, $"{userId}_{USER_PROFILE_DATA_FILENAME}");
            userProfileData.SetUserId(userId);
            string jsonContent = JsonConvert.SerializeObject(userProfileData, Formatting.Indented, Parsers.serializerSettings);
            await File.WriteAllTextAsync(fullPath, jsonContent);
            Debug.Log($"Successfully saved user profile data for user: {userId}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save user profile data for user {userId}: {ex.Message}");
        }
    }

    public void DeleteUserProfile(string userId)
    {
        try
        {
            string profilesPath = EnsureUserProfilesDirectory();
            string fullPath = Path.Combine(profilesPath, $"{userId}_{USER_PROFILE_DATA_FILENAME}");
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                Debug.Log($"Successfully deleted user profile for user: {userId}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to delete user profile for user {userId}: {ex.Message}");
        }
    }

    /// <summary>
    /// Deletes all user profile data files from the user profiles directory
    /// </summary>
    public void ClearAllUserProfiles()
    {
        try
        {
            string profilesPath = EnsureUserProfilesDirectory();
            var directoryInfo = new DirectoryInfo(profilesPath);
            var jsonFiles = directoryInfo.GetFiles("*.json");
            
            foreach (var file in jsonFiles)
            {
                try
                {
                    file.Delete();
                    Debug.Log($"Deleted user profile: {file.Name}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to delete user profile {file.Name}: {ex.Message}");
                    // Continue with other files even if one fails
                }
            }
            
            Debug.Log("Finished clearing all user profiles");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to clear user profiles: {ex.Message}");
        }
    }
}