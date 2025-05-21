using System.IO;
using System.Threading.Tasks;
using UnityEngine; // Required for Application.persistentDataPath, assuming Unity environment

public class LocalStorageProvider : ILocalStorageProvider
{
    private string GetFullPath(string key)
    {
        // TODO: Implement robust key sanitization to prevent path traversal vulnerabilities.
        // Ensure key does not contain '..', '/', '\\', etc. or escape them appropriately.
        // For now, simple combination, assuming key is safe.
        return Path.Combine(Application.persistentDataPath, key);
    }

    public async Task<Result<bool>> KeyExistsAsync(string key)
    {
        try
        {
            string filePath = GetFullPath(key);
            bool exists = File.Exists(filePath);
            return await Task.FromResult(Result<bool>.CreateSuccess(exists));
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error checking if key exists '{key}': {ex.Message}");
            return Result<bool>.CreateFailure("LOCAL_STORAGE_ERROR", $"Error checking key existence: {ex.Message}");
        }
    }

    public async Task<Result<string>> ReadTextAsync(string key)
    {
        try
        {
            string filePath = GetFullPath(key);
            if (!File.Exists(filePath))
            {
                return Result<string>.CreateFailure("LOCAL_FILE_NOT_FOUND", $"File not found at key: {key}");
            }
            string content = await File.ReadAllTextAsync(filePath);
            return Result<string>.CreateSuccess(content);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error reading text from key '{key}': {ex.Message}");
            return Result<string>.CreateFailure("LOCAL_STORAGE_ERROR", $"Error reading text file: {ex.Message}");
        }
    }

    public async Task<ResultVoid> WriteTextAsync(string key, string content)
    {
        try
        {
            string filePath = GetFullPath(key);
            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)); 
            await File.WriteAllTextAsync(filePath, content);
            return ResultVoid.CreateSuccess();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error writing text to key '{key}': {ex.Message}");
            return ResultVoid.CreateFailure("LOCAL_STORAGE_ERROR", $"Error writing text file: {ex.Message}");
        }
    }

    public async Task<Result<byte[]>> ReadBinaryAsync(string key)
    {
        try
        {
            string filePath = GetFullPath(key);
            if (!File.Exists(filePath))
            {
                return Result<byte[]>.CreateFailure("LOCAL_FILE_NOT_FOUND", $"File not found at key: {key}");
            }
            byte[] data = await File.ReadAllBytesAsync(filePath);
            return Result<byte[]>.CreateSuccess(data);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error reading binary from key '{key}': {ex.Message}");
            return Result<byte[]>.CreateFailure("LOCAL_STORAGE_ERROR", $"Error reading binary file: {ex.Message}");
        }
    }

    public async Task<ResultVoid> WriteBinaryAsync(string key, byte[] data)
    {
        try
        {
            string filePath = GetFullPath(key);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            await File.WriteAllBytesAsync(filePath, data);
            return ResultVoid.CreateSuccess();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error writing binary to key '{key}': {ex.Message}");
            return ResultVoid.CreateFailure("LOCAL_STORAGE_ERROR", $"Error writing binary file: {ex.Message}");
        }
    }

    public async Task<ResultVoid> DeleteAsync(string key)
    {
        try
        {
            string filePath = GetFullPath(key);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            // Deletion is considered success even if file didn't exist, as the state is achieved.
            return await Task.FromResult(ResultVoid.CreateSuccess()); 
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error deleting file at key '{key}': {ex.Message}");
            return ResultVoid.CreateFailure("LOCAL_STORAGE_ERROR", $"Error deleting file: {ex.Message}");
        }
    }
} 