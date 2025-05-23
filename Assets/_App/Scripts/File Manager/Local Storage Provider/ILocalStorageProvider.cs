using System.Threading.Tasks;

/// <summary>
/// Contract for client-side local file system interaction.
/// Handles storage and retrieval of different data types.
/// </summary>
public interface ILocalStorageProvider
{
    Task<Result<bool>> KeyExistsAsync(string key);
    Task<Result<string>> ReadTextAsync(string key);
    Task<ResultVoid> WriteTextAsync(string key, string content);
    Task<Result<byte[]>> ReadBinaryAsync(string key); // Using byte[] for FileObject
    Task<ResultVoid> WriteBinaryAsync(string key, byte[] data); // Using byte[] for FileObject
    Task<ResultVoid> DeleteAsync(string key);
    Task<Result<System.Collections.Generic.List<string>>> ListKeysAsync(string prefix = null);
}