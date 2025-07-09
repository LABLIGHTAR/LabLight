using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines the contract for the file management service.
/// Orchestrates local caching and interaction with database and large file storage.
/// </summary>
public interface IFileManager
{
    // User Profile Management
    Task<ResultVoid> SaveLocalUserProfileAsync(LocalUserProfileData userProfile);
    Task<Result<List<LocalUserProfileData>>> GetAllLocalUserProfilesAsync();
    Task<Result<LocalUserProfileData>> GetUserProfileAsync(string userId);
    Task<ResultVoid> DeleteLocalUserProfileAsync(string userId);
    Task<Result<List<LocalUserProfileData>>> GetAllUserProfilesAsync();
    Task<ResultVoid> CacheUserProfileAsync(LocalUserProfileData userProfile);

    // Protocol Management
    Task<Result<string>> GetProtocolAsync(uint protocolId);
    Task<Result<ProtocolSaveResultData>> SaveProtocolAsync(uint? protocolId, string name, string content, bool isPublic, uint organizationId);
    Task<ResultVoid> DeleteProtocolAsync(uint protocolId);

    // Protocol State Management
    Task<Result<string>> GetProtocolStateAsync(uint stateId);
    Task<Result<StateSaveResultData>> SaveProtocolStateAsync(uint? stateId, uint protocolId, uint organizationId, string stateContent);
    Task<ResultVoid> DeleteProtocolStateAsync(uint stateId);

    // Media File Management (using byte[] for FileObject)
    Task<Result<byte[]>> GetMediaFileAsync(string objectKey);
    Task<Result<string>> GetMediaFilePathAsync(string objectKey);
    Task<ResultVoid> SaveMediaFileAsync(string objectKey, string originalFilename, string contentType, byte[] data);
    Task<ResultVoid> DeleteMediaFileAsync(string objectKey);

    // Prefab Management
    Task<Result<GameObject>> GetPrefabAsync(string resourcePath); // Path relative to any Resources folder

    // Protocol Discovery
    Task<Result<List<ProtocolData>>> GetAvailableProtocolsAsync();
    Task<Result<List<ProtocolData>>> GetSavedProtocolsAsync();
}