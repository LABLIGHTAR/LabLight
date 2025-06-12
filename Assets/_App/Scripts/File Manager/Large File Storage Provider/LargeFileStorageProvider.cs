using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using UnityEngine; // For JsonUtility and Debug.Log

// TODO: Consider a shared HTTP client factory or service if multiple services need HttpClient.
// TODO: Add appropriate JSON parsing library if JsonUtility is not sufficient or for non-Unity environments (e.g. Newtonsoft.Json).

public class LargeFileStorageProvider : ILargeFileStorageProvider
{
    private readonly HttpClient _httpClient; // Should be long-lived
    private readonly string _lfsServiceBaseUrl; // e.g., MUSS base URL
    private readonly IAuthProvider _authProvider; // Added for OIDC token retrieval

    //region MUSS DTOs
    // Adjusted for JsonUtility: public fields and [System.Serializable]
    [System.Serializable]
    private class MussUploadUrlRequest
    {
        public string object_key;
        public string content_type;
    }

    [System.Serializable]
    private class MussDownloadUrlRequest
    {
        public string object_key;
    }

    [System.Serializable]
    private class MussDeleteObjectRequest
    {
        public string object_key;
    }

    [System.Serializable]
    private class MussUrlResponse
    {
        public string url;
    }

    [System.Serializable]
    private class MussDeleteSuccessResponse
    {
        public bool success;
        // public string message; // Optional, if MUSS returns a message and JsonUtility needs to map it
    }
    //endregion

    public LargeFileStorageProvider(string lfsServiceBaseUrl, IAuthProvider authProvider)
    {
        _httpClient = new HttpClient();
        _lfsServiceBaseUrl = lfsServiceBaseUrl.TrimEnd('/');
        _authProvider = authProvider ?? throw new System.ArgumentNullException(nameof(authProvider));
    }

    private async Task<string> GetOidcTokenAsync()
    {
        if (_authProvider == null) // Should be caught by constructor check, but good for safety
        {
            Debug.LogError("LargeFileStorageProvider: IAuthProvider is not initialized.");
            return null;
        }
        
        try
        {
            string token = await _authProvider.GetIdTokenAsync(true); 

            if (string.IsNullOrEmpty(token))
            {
                Debug.LogError("LargeFileStorageProvider: Failed to retrieve OIDC token from AuthProvider (token was null or empty).");
            }
            return token;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"LargeFileStorageProvider: Exception while retrieving OIDC token from AuthProvider: {ex.Message}");
            return null;
        }
    }

    public async Task<ResultVoid> UploadFileAsync(string objectKey, string contentType, byte[] data)
    {
        string token = await GetOidcTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            return ResultVoid.CreateFailure("AUTH_ERROR", "OIDC token not available.");
        }

        try
        {
            // 1. Generate upload URL from MUSS
            var mussRequestPayload = new MussUploadUrlRequest { object_key = objectKey, content_type = contentType };
            string mussJsonPayload = JsonUtility.ToJson(mussRequestPayload);
            
            var mussHttpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_lfsServiceBaseUrl}/generate-upload-url")
            {
                Content = new StringContent(mussJsonPayload, Encoding.UTF8, "application/json")
            };
            mussHttpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpResponseMessage mussResponse = await _httpClient.SendAsync(mussHttpRequest);

            if (!mussResponse.IsSuccessStatusCode)
            {
                string errorContent = await mussResponse.Content.ReadAsStringAsync();
                Debug.LogError($"MUSS GetUploadUrl error ({mussResponse.StatusCode}): {errorContent}");
                return ResultVoid.CreateFailure("MUSS_ERROR", $"Failed to get upload URL: {mussResponse.StatusCode} - {errorContent}");
            }

            string mussResponseContent = await mussResponse.Content.ReadAsStringAsync();
            MussUrlResponse urlResponse = JsonUtility.FromJson<MussUrlResponse>(mussResponseContent);

            if (string.IsNullOrEmpty(urlResponse?.url))
            {
                    Debug.LogError("MUSS GetUploadUrl error: Received empty or invalid URL.");
                return ResultVoid.CreateFailure("MUSS_ERROR", "MUSS did not return a valid upload URL.");
            }

            // 2. Upload file directly to MinIO using the presigned URL
            var minioHttpRequest = new HttpRequestMessage(HttpMethod.Put, urlResponse.url)
            {
                Content = new ByteArrayContent(data)
            };
            minioHttpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            // MinIO presigned URLs usually don't require Authorization headers as the signature is in the URL.

            HttpResponseMessage minioResponse = await _httpClient.SendAsync(minioHttpRequest);

            if (minioResponse.IsSuccessStatusCode)
            {
                return ResultVoid.CreateSuccess();
            }
            else
            {
                string errorContent = await minioResponse.Content.ReadAsStringAsync();
                Debug.LogError($"MinIO Upload error ({minioResponse.StatusCode}): {errorContent}");
                // TODO: Consider a more specific error code if MinIO provides one in the body.
                return ResultVoid.CreateFailure("LFS_UPLOAD_ERROR", $"Failed to upload file to MinIO: {minioResponse.StatusCode} - {errorContent}");
            }
        }
        catch (System.Exception ex) // Catching general exception for JsonUtility or other unexpected issues
        {
            Debug.LogError($"Exception during LFS Upload: {ex.Message}");
            return ResultVoid.CreateFailure("LFS_UPLOAD_ERROR", $"Exception during file upload: {ex.Message}");
        }
    }

    public async Task<Result<byte[]>> DownloadFileAsync(string objectKey)
    {
        string token = await GetOidcTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            return Result<byte[]>.CreateFailure("AUTH_ERROR", "OIDC token not available.");
        }
        
        try
        {
            // 1. Generate download URL from MUSS
            var mussRequestPayload = new MussDownloadUrlRequest { object_key = objectKey };
            string mussJsonPayload = JsonUtility.ToJson(mussRequestPayload);

            var mussHttpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_lfsServiceBaseUrl}/generate-download-url")
            {
                Content = new StringContent(mussJsonPayload, Encoding.UTF8, "application/json")
            };
            mussHttpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpResponseMessage mussResponse = await _httpClient.SendAsync(mussHttpRequest);

            if (!mussResponse.IsSuccessStatusCode)
            {
                string errorContent = await mussResponse.Content.ReadAsStringAsync();
                Debug.LogError($"MUSS GetDownloadUrl error ({mussResponse.StatusCode}): {errorContent}");
                return Result<byte[]>.CreateFailure("MUSS_ERROR", $"Failed to get download URL: {mussResponse.StatusCode} - {errorContent}");
            }
            
            string mussResponseContent = await mussResponse.Content.ReadAsStringAsync();
            MussUrlResponse urlResponse = JsonUtility.FromJson<MussUrlResponse>(mussResponseContent);

            if (string.IsNullOrEmpty(urlResponse?.url))
            {
                Debug.LogError("MUSS GetDownloadUrl error: Received empty or invalid URL.");
                return Result<byte[]>.CreateFailure("MUSS_ERROR", "MUSS did not return a valid download URL.");
            }

            // 2. Download file from MinIO using the presigned URL
            var minioHttpRequest = new HttpRequestMessage(HttpMethod.Get, urlResponse.url);
            // MinIO presigned URLs usually don't require Authorization headers.

            HttpResponseMessage minioResponse = await _httpClient.SendAsync(minioHttpRequest);

            if (minioResponse.IsSuccessStatusCode)
            {
                byte[] fileBytes = await minioResponse.Content.ReadAsByteArrayAsync();
                return Result<byte[]>.CreateSuccess(fileBytes);
            }
            else
            {
                string errorContent = await minioResponse.Content.ReadAsStringAsync();
                Debug.LogError($"MinIO Download error ({minioResponse.StatusCode}): {errorContent}");
                return Result<byte[]>.CreateFailure("LFS_DOWNLOAD_ERROR", $"Failed to download file from MinIO: {minioResponse.StatusCode} - {errorContent}");
            }
        }
        catch (System.Exception ex) // Catching general exception for JsonUtility or other unexpected issues
        {
            Debug.LogError($"Exception during LFS Download: {ex.Message}");
            return Result<byte[]>.CreateFailure("LFS_DOWNLOAD_ERROR", $"Exception during file download: {ex.Message}");
        }
    }

    public async Task<ResultVoid> DeleteFileAsync(string objectKey)
    {
        string token = await GetOidcTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            return ResultVoid.CreateFailure("AUTH_ERROR", "OIDC token not available.");
        }

        try
        {
            // Request deletion through MUSS
            var mussRequestPayload = new MussDeleteObjectRequest { object_key = objectKey };
            string jsonPayload = JsonUtility.ToJson(mussRequestPayload);
            
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_lfsServiceBaseUrl}/delete-minio-object")
            {
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpResponseMessage response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                // Optionally parse MUSS response if it contains useful data, e.g. { "success": true }
                string responseContent = await response.Content.ReadAsStringAsync();
                MussDeleteSuccessResponse deleteResponse = JsonUtility.FromJson<MussDeleteSuccessResponse>(responseContent);
                if (deleteResponse != null && deleteResponse.success)
                {
                    return ResultVoid.CreateSuccess();
                }
                else
                {
                    Debug.LogError($"MUSS Delete error: MUSS reported failure. Response: {responseContent}");
                    return ResultVoid.CreateFailure("LFS_DELETE_ERROR", $"MUSS reported deletion failure: {responseContent}");
                }
            }
            else
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                Debug.LogError($"LFS Delete error ({response.StatusCode}): {errorContent}");
                return ResultVoid.CreateFailure("LFS_DELETE_ERROR", $"Failed to delete file via MUSS: {response.StatusCode} - {errorContent}");
            }
        }
        catch (System.Exception ex) // Catching general exception for JsonUtility or other unexpected issues
        {
            Debug.LogError($"Exception during LFS Delete: {ex.Message}");
            return ResultVoid.CreateFailure("LFS_DELETE_ERROR", $"Exception during file deletion: {ex.Message}");
        }
    }
}