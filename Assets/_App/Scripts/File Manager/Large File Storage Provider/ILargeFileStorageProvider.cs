using System.Threading.Tasks;

/// <summary>
/// Contract for interacting with a large file storage service (e.g., MUSS for MinIO).
/// Handles direct byte uploads/downloads and deletions.
/// </summary>
public interface ILargeFileStorageProvider
{
    Task<ResultVoid> UploadFileAsync(string objectKey, string contentType, byte[] data); // Using byte[] for FileObject
    Task<Result<byte[]>> DownloadFileAsync(string objectKey); // Using byte[] for FileObject
    Task<ResultVoid> DeleteFileAsync(string objectKey);
}