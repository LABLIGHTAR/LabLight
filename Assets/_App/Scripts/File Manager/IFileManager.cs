using System.Threading.Tasks;

/// <summary>
/// Defines the contract for the file management service.
/// Orchestrates local caching and interaction with database and large file storage.
/// </summary>
public interface IFileManager
{
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
    Task<ResultVoid> SaveMediaFileAsync(string objectKey, string originalFilename, string contentType, byte[] data);
    Task<ResultVoid> DeleteMediaFileAsync(string objectKey);
}