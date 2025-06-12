using System.Threading.Tasks;
using UnityEngine; // For Debug.Log, assuming Unity environment
using System.Collections.Generic; // Added for List<T>
using System; // Added for Exception
// TODO: Consider adding System.IO for Path.Combine if robust key sanitization/construction is needed.

public class FileManager : IFileManager
{
    private readonly IDatabase _database;
    private readonly ILocalStorageProvider _localStorageProvider;
    private readonly ILargeFileStorageProvider _largeFileStorageProvider;

    // Constants for error codes, aligned with file_management_plan.md
    private const string ErrorNotImplemented = "NOT_IMPLEMENTED";
    private const string ErrorOfflineCacheMiss = "OFFLINE_CACHE_MISS";
    private const string ErrorStaleDataReturned = "STALE_DATA_RETURNED"; // Example if policy allows
    private const string ErrorDbProtocolNotFound = "DB_PROTOCOL_NOT_FOUND";
    private const string ErrorDbStateNotFound = "DB_STATE_NOT_FOUND";
    private const string ErrorDbError = "DB_ERROR";
    private const string ErrorOfflineOperationUnsupported = "OFFLINE_OPERATION_UNSUPPORTED";
    private const string ErrorDbSaveError = "DB_SAVE_ERROR";
    private const string ErrorDbStateSaveError = "DB_STATE_SAVE_ERROR";
    private const string ErrorDbDeleteError = "DB_DELETE_ERROR";
    private const string ErrorDbStateDeleteError = "DB_STATE_DELETE_ERROR";
    private const string ErrorLocalCacheWriteFailed = "LOCAL_CACHE_WRITE_FAILED"; // Informational
    private const string ErrorLocalCacheDeleteFailed = "LOCAL_CACHE_DELETE_FAILED"; // Informational

    private const string ErrorOfflineMediaCacheMiss = "OFFLINE_MEDIA_CACHE_MISS";
    private const string ErrorMediaNotAvailable = "MEDIA_NOT_AVAILABLE";
    private const string ErrorLfsDownloadError = "LFS_DOWNLOAD_ERROR";
    private const string ErrorLocalSaveFailed = "LOCAL_SAVE_FAILED";
    private const string ErrorDbUploadSlotRequestFailed = "DB_UPLOAD_SLOT_REQUEST_FAILED";
    private const string ErrorLfsUploadError = "LFS_UPLOAD_ERROR";
    private const string ErrorDbUploadConfirmationFailed = "DB_UPLOAD_CONFIRMATION_FAILED";
    private const string ErrorLfsDeleteFailed = "LFS_DELETE_FAILED";
    private const string ErrorDbDeleteConfirmationFailed = "DB_DELETE_CONFIRMATION_FAILED";


    public FileManager(
        IDatabase database,
        ILocalStorageProvider localStorageProvider,
        ILargeFileStorageProvider largeFileStorageProvider)
    {
        _database = database ?? throw new System.ArgumentNullException(nameof(database));
        _localStorageProvider = localStorageProvider ?? throw new System.ArgumentNullException(nameof(localStorageProvider));
        _largeFileStorageProvider = largeFileStorageProvider ?? throw new System.ArgumentNullException(nameof(largeFileStorageProvider));
    }

    // Key generation strategy for local cache.
    // TODO: Consider more robust key sanitization if user-provided parts are used directly.
    private string GetProtocolCacheKey(uint protocolId) => $"protocol_{protocolId}.json";
    private string GetStateCacheKey(uint stateId) => $"state_{stateId}.json";
    private string GetMediaCacheKey(string objectKey) => $"media_{objectKey}"; // Consider extension if known, e.g., from contentType


    #region Protocol Management
    public async Task<Result<string>> GetProtocolAsync(uint protocolId)
    {
        string cacheKey = GetProtocolCacheKey(protocolId);
        Debug.Log($"FileManager.GetProtocolAsync: Attempting to get protocol {protocolId}. Cache key: {cacheKey}");

        // 1. Check Local Cache
        Result<string> localResult = await _localStorageProvider.ReadTextAsync(cacheKey);
        if (localResult.Success && localResult.Data != null)
        {
            Debug.Log($"FileManager.GetProtocolAsync: Protocol {protocolId} found in local cache.");
            // TODO: Implement caching strategy (e.g., TTL, versioning) to decide if cached version is valid/recent enough.
            // For now, if found, return it.
            return Result<string>.CreateSuccess(localResult.Data);
        }

        // 2. Check Online Status
        if (!_database.IsConnected)
        {
            Debug.LogWarning($"FileManager.GetProtocolAsync: Offline and protocol {protocolId} not in cache.");
            return Result<string>.CreateFailure(ErrorOfflineCacheMiss, $"Protocol {protocolId} not found in local cache while offline.");
        }

        // 3. Fetch from Database (If Online)
        Debug.Log($"FileManager.GetProtocolAsync: Protocol {protocolId} not in cache or cache miss, fetching from database.");
        ProtocolData protocolData = _database.GetCachedProtocol(protocolId); // Assuming this is synchronous or effectively so for cache.

        if (protocolData == null)
        {
            Debug.LogWarning($"FileManager.GetProtocolAsync: Protocol {protocolId} not found in database.");
            return Result<string>.CreateFailure(ErrorDbProtocolNotFound, $"Protocol {protocolId} not found in database.");
        }


        string protocolJson = protocolData.Content;

        if (string.IsNullOrEmpty(protocolJson))
        {
            Debug.LogError($"FileManager.GetProtocolAsync: Protocol {protocolId} found in DB, but its content is null or empty.");
            return Result<string>.CreateFailure(ErrorDbError, $"Protocol {protocolId} content is missing from database entry.");
        }

        // 4. Update Local Cache
        Debug.Log($"FileManager.GetProtocolAsync: Updating local cache for protocol {protocolId}.");
        ResultVoid cacheWriteResult = await _localStorageProvider.WriteTextAsync(cacheKey, protocolJson);
        if (!cacheWriteResult.Success)
        {
            // Log warning, but proceed with returning data as DB is primary source.
            Debug.LogWarning($"FileManager.GetProtocolAsync: Failed to write protocol {protocolId} to local cache. Error: {cacheWriteResult.Error?.Code} - {cacheWriteResult.Error?.Message}");
        }

        return Result<string>.CreateSuccess(protocolJson);
    }

    public async Task<Result<ProtocolSaveResultData>> SaveProtocolAsync(uint? protocolId, string name, string content, bool isPublic, uint organizationId)
    {
        string operation = protocolId.HasValue ? "Updating" : "Creating";
        Debug.Log($"FileManager.SaveProtocolAsync: {operation} protocol. Name: {name}, OrgID: {organizationId}");

        // 1. Check Online Status
        if (!_database.IsConnected)
        {
            // TODO: Offline saving for structured data is a Future Consideration (file_management_plan.md).
            Debug.LogWarning($"FileManager.SaveProtocolAsync: Offline. {operation} protocol is not currently supported offline.");
            return Result<ProtocolSaveResultData>.CreateFailure(ErrorOfflineOperationUnsupported, $"{operation} protocol offline is not currently supported.");
        }

        // 2. Call Database Operation
        uint resultingProtocolId;
        if (protocolId.HasValue) // Update
        {
            resultingProtocolId = protocolId.Value;
            Debug.Log($"FileManager.SaveProtocolAsync: Calling EditProtocol for ID {resultingProtocolId}.");
            _database.EditProtocol(resultingProtocolId, name, content, isPublic, organizationId);
            // EditProtocol is void. Success is assumed if no exception.
            // Error handling for reducers is typically via events or checking state post-call if needed.
        }
        else // Create
        {
            Debug.Log("FileManager.SaveProtocolAsync: Calling CreateProtocol.");
            _database.CreateProtocol(name, content, isPublic, organizationId);
            // CreateProtocol is void. The new ID is not returned directly.
            // The actual ID will come via a database event (e.g., OnProtocolAdded).
            // For the purpose of returning ProtocolSaveResultData, we cannot populate the new ID here.
            // This is a known limitation based on void reducer calls.
            // The ProtocolSaveResultData should ideally reflect this (e.g., nullable ID or only partial data).
            resultingProtocolId = 0; // Placeholder for new protocol ID
            Debug.LogWarning("FileManager.SaveProtocolAsync: CreateProtocol called. The new protocol ID is not available directly from this method. Client should listen to DB events for the new ID.");
        }

        // 3. Update Local Cache
        // For create, the ID isn't known here for cache key. Cache update might be better handled by an event listener that gets the full ProtocolData.
        // For update, we can update the cache.
        if (protocolId.HasValue)
        {
            string cacheKey = GetProtocolCacheKey(resultingProtocolId);
            Debug.Log($"FileManager.SaveProtocolAsync: Updating local cache for protocol {resultingProtocolId}.");
            ResultVoid cacheWriteResult = await _localStorageProvider.WriteTextAsync(cacheKey, content);
            if (!cacheWriteResult.Success)
            {
                Debug.LogWarning($"FileManager.SaveProtocolAsync: Failed to write updated protocol {resultingProtocolId} to local cache. Error: {cacheWriteResult.Error?.Code} - {cacheWriteResult.Error?.Message}");
            }
        }
        else
        {
                Debug.Log("FileManager.SaveProtocolAsync: Local cache for new protocol will not be updated here. It should be updated upon receiving the new protocol data via a DB event.");
        }

        // 4. Return Result
        // For creation, resultingProtocolId will be 0 (or some indicator of unknown).
        // The client should be aware or ProtocolSaveResultData designed to handle this.
        var saveData = new ProtocolSaveResultData { ProtocolId = resultingProtocolId };
        if (!protocolId.HasValue && resultingProtocolId == 0)
        {
            // Indicate that ID is not yet available for new creations through this response
            // No specific error code for this, it's a successful DB call but with data flow limitation.
            // The success of the call is that it was dispatched.
        }
        return Result<ProtocolSaveResultData>.CreateSuccess(saveData);
    }

    public async Task<ResultVoid> DeleteProtocolAsync(uint protocolId)
    {
        Debug.Log($"FileManager.DeleteProtocolAsync: Attempting to delete protocol {protocolId}.");

        // 1. Check Online Status
        if (!_database.IsConnected)
        {
            Debug.LogWarning($"FileManager.DeleteProtocolAsync: Offline. Deleting protocol {protocolId} is not currently supported offline.");
            return ResultVoid.CreateFailure(ErrorOfflineOperationUnsupported, "Deleting protocol offline is not currently supported.");
        }

        // 2. Call Database Operation
        Debug.Log($"FileManager.DeleteProtocolAsync: Calling DeleteProtocol for ID {protocolId} on database.");
        _database.DeleteProtocol(protocolId);
        // DeleteProtocol is void. Success is assumed if no exception.

        // 3. Remove from Local Cache
        string cacheKey = GetProtocolCacheKey(protocolId);
        Debug.Log($"FileManager.DeleteProtocolAsync: Removing protocol {protocolId} from local cache.");
        ResultVoid cacheDeleteResult = await _localStorageProvider.DeleteAsync(cacheKey);
        if (!cacheDeleteResult.Success)
        {
            // Log warning. DB deletion is the critical part.
            // This could also mean the file wasn't there, which is fine.
            Debug.LogWarning($"FileManager.DeleteProtocolAsync: Failed to delete protocol {protocolId} from local cache or it did not exist. Error: {cacheDeleteResult.Error?.Code} - {cacheDeleteResult.Error?.Message}");
        }

        return ResultVoid.CreateSuccess();
    }
    #endregion

    #region Protocol State Management
    public async Task<Result<string>> GetProtocolStateAsync(uint stateId)
    {
        string cacheKey = GetStateCacheKey(stateId);
        Debug.Log($"FileManager.GetProtocolStateAsync: Attempting to get state {stateId}. Cache key: {cacheKey}");

        Result<string> localResult = await _localStorageProvider.ReadTextAsync(cacheKey);
        if (localResult.Success && localResult.Data != null)
        {
            Debug.Log($"FileManager.GetProtocolStateAsync: State {stateId} found in local cache.");
            return Result<string>.CreateSuccess(localResult.Data);
        }

        if (!_database.IsConnected)
        {
            Debug.LogWarning($"FileManager.GetProtocolStateAsync: Offline and state {stateId} not in cache.");
            return Result<string>.CreateFailure(ErrorOfflineCacheMiss, $"Protocol state {stateId} not found in local cache while offline.");
        }

        Debug.Log($"FileManager.GetProtocolStateAsync: State {stateId} not in cache, fetching from database.");
        ProtocolStateData stateData = _database.GetCachedProtocolState(stateId);

        if (stateData == null)
        {
            Debug.LogWarning($"FileManager.GetProtocolStateAsync: State {stateId} not found in database.");
            return Result<string>.CreateFailure(ErrorDbStateNotFound, $"Protocol state {stateId} not found in database.");
        }

        // TODO: Verify the actual property name on `ProtocolStateData` that holds the JSON content string.
        // Assuming stateData.StateContent or similar. For now: stateData.Content
        string stateJson = stateData.State; // Example: adjust to actual property like stateData.JsonContent

        if (string.IsNullOrEmpty(stateJson))
        {
            Debug.LogError($"FileManager.GetProtocolStateAsync: State {stateId} found in DB, but its content is null or empty.");
            return Result<string>.CreateFailure(ErrorDbError, $"Protocol state {stateId} content is missing from database entry.");
        }
        
        Debug.Log($"FileManager.GetProtocolStateAsync: Updating local cache for state {stateId}.");
        ResultVoid cacheWriteResult = await _localStorageProvider.WriteTextAsync(cacheKey, stateJson);
        if (!cacheWriteResult.Success)
        {
            Debug.LogWarning($"FileManager.GetProtocolStateAsync: Failed to write state {stateId} to local cache. Error: {cacheWriteResult.Error?.Code} - {cacheWriteResult.Error?.Message}");
        }

        return Result<string>.CreateSuccess(stateJson);
    }

    public async Task<Result<StateSaveResultData>> SaveProtocolStateAsync(uint? stateId, uint protocolId, uint organizationId, string stateContent)
    {
        string operation = stateId.HasValue ? "Updating" : "Creating";
        Debug.Log($"FileManager.SaveProtocolStateAsync: {operation} protocol state. ProtocolID: {protocolId}, OrgID: {organizationId}");

        if (!_database.IsConnected)
        {
            Debug.LogWarning($"FileManager.SaveProtocolStateAsync: Offline. {operation} protocol state is not currently supported offline.");
            return Result<StateSaveResultData>.CreateFailure(ErrorOfflineOperationUnsupported, $"{operation} protocol state offline is not currently supported.");
        }

        uint resultingStateId;
        if (stateId.HasValue) // Update
        {
            resultingStateId = stateId.Value;
            Debug.Log($"FileManager.SaveProtocolStateAsync: Calling EditProtocolState for ID {resultingStateId}.");
            _database.EditProtocolState(resultingStateId, stateContent);
        }
        else // Create
        {
            Debug.Log("FileManager.SaveProtocolStateAsync: Calling CreateProtocolState.");
            _database.CreateProtocolState(protocolId, organizationId, stateContent);
            resultingStateId = 0; // Placeholder for new state ID
            Debug.LogWarning("FileManager.SaveProtocolStateAsync: CreateProtocolState called. The new state ID is not available directly. Client should use DB events.");
        }

        if (stateId.HasValue)
        {
            string cacheKey = GetStateCacheKey(resultingStateId);
            Debug.Log($"FileManager.SaveProtocolStateAsync: Updating local cache for state {resultingStateId}.");
            ResultVoid cacheWriteResult = await _localStorageProvider.WriteTextAsync(cacheKey, stateContent);
            if (!cacheWriteResult.Success)
            {
                Debug.LogWarning($"FileManager.SaveProtocolStateAsync: Failed to write updated state {resultingStateId} to local cache. Error: {cacheWriteResult.Error?.Code} - {cacheWriteResult.Error?.Message}");
            }
        }
        else
        {
            Debug.Log("FileManager.SaveProtocolStateAsync: Local cache for new state will not be updated here. Update upon receiving DB event.");
        }
        
        var saveData = new StateSaveResultData { StateId = resultingStateId };
            return Result<StateSaveResultData>.CreateSuccess(saveData);
    }

    public async Task<ResultVoid> DeleteProtocolStateAsync(uint stateId)
    {
        Debug.Log($"FileManager.DeleteProtocolStateAsync: Attempting to delete state {stateId}.");
        if (!_database.IsConnected)
        {
            Debug.LogWarning($"FileManager.DeleteProtocolStateAsync: Offline. Deleting state {stateId} is not currently supported offline.");
            return ResultVoid.CreateFailure(ErrorOfflineOperationUnsupported, "Deleting protocol state offline is not currently supported.");
        }

        Debug.Log($"FileManager.DeleteProtocolStateAsync: Calling DeleteProtocolState for ID {stateId} on database.");
        _database.DeleteProtocolState(stateId);

        string cacheKey = GetStateCacheKey(stateId);
        Debug.Log($"FileManager.DeleteProtocolStateAsync: Removing state {stateId} from local cache.");
        ResultVoid cacheDeleteResult = await _localStorageProvider.DeleteAsync(cacheKey);
        if (!cacheDeleteResult.Success)
        {
            Debug.LogWarning($"FileManager.DeleteProtocolStateAsync: Failed to delete state {stateId} from local cache or it did not exist. Error: {cacheDeleteResult.Error?.Code} - {cacheDeleteResult.Error?.Message}");
        }

        return ResultVoid.CreateSuccess();
    }
    #endregion

    #region Media File Management
    public async Task<Result<byte[]>> GetMediaFileAsync(string objectKey)
    {
        if (string.IsNullOrEmpty(objectKey))
        {
            Debug.LogError("FileManager.GetMediaFileAsync: objectKey cannot be null or empty.");
            return Result<byte[]>.CreateFailure("INVALID_ARGUMENT", "Object key must be provided.");
        }

        string cacheKey = GetMediaCacheKey(objectKey);
        Debug.Log($"FileManager.GetMediaFileAsync: Attempting to get media '{objectKey}'. Cache key: {cacheKey}");

        // 1. Check Local Cache
        Result<byte[]> localResult = await _localStorageProvider.ReadBinaryAsync(cacheKey);
        if (localResult.Success && localResult.Data != null)
        {
            Debug.Log($"FileManager.GetMediaFileAsync: Media '{objectKey}' found in local cache.");
            return Result<byte[]>.CreateSuccess(localResult.Data);
        }

        // 2. Check Online Status
        if (!_database.IsConnected)
        {
            Debug.LogWarning($"FileManager.GetMediaFileAsync: Offline and media '{objectKey}' not in cache.");
            return Result<byte[]>.CreateFailure(ErrorOfflineMediaCacheMiss, $"Media file '{objectKey}' not in local cache and client is offline.");
        }

        // 3. Verify Metadata (If Online)
        Debug.Log($"FileManager.GetMediaFileAsync: Media '{objectKey}' not in cache, checking metadata and fetching from LFS.");
        MediaMetadataData metadata = _database.GetCachedMediaMetadata(objectKey);
        if (metadata == null)
        {
            Debug.LogWarning($"FileManager.GetMediaFileAsync: Media metadata not found for '{objectKey}'.");
            return Result<byte[]>.CreateFailure(ErrorMediaNotAvailable, $"Media file metadata not found for '{objectKey}'.");
        }
        // TODO: Define what "Available" status means. Assuming a property like `metadata.Status == "Available"`.
        // For now, I'll assume if metadata exists, we can try to download. Add a specific check if `MediaMetadataData` has a status field.
        // Example: if (metadata.FileStatus != MediaFileStatus.Available) return Result<byte[]>.CreateFailure(ErrorMediaNotAvailable, "Media file is not in an available state.");

        // 4. Download File
        Debug.Log($"FileManager.GetMediaFileAsync: Metadata found for '{objectKey}'. Calling LFS to download.");
        Result<byte[]> downloadResult = await _largeFileStorageProvider.DownloadFileAsync(objectKey);
        if (!downloadResult.Success || downloadResult.Data == null)
        {
            Debug.LogError($"FileManager.GetMediaFileAsync: Failed to download media '{objectKey}' from LFS. Error: {downloadResult.Error?.Code} - {downloadResult.Error?.Message}");
            return Result<byte[]>.CreateFailure(downloadResult.Error?.Code ?? ErrorLfsDownloadError, downloadResult.Error?.Message ?? $"Failed to download file '{objectKey}' from large file storage.");
        }

        // 5. Cache Locally
        Debug.Log($"FileManager.GetMediaFileAsync: Media '{objectKey}' downloaded. Caching locally.");
        ResultVoid cacheWriteResult = await _localStorageProvider.WriteBinaryAsync(cacheKey, downloadResult.Data);
        if (!cacheWriteResult.Success)
        {
            Debug.LogWarning($"FileManager.GetMediaFileAsync: Failed to write media '{objectKey}' to local cache. Error: {cacheWriteResult.Error?.Code} - {cacheWriteResult.Error?.Message}");
        }

        return Result<byte[]>.CreateSuccess(downloadResult.Data);
    }

    public async Task<ResultVoid> SaveMediaFileAsync(string objectKey, string originalFilename, string contentType, byte[] data)
    {
        Debug.Log($"FileManager.SaveMediaFileAsync: Attempting to save media. ObjectKey: {objectKey}, Filename: {originalFilename}");

        // 1. Check Online Status
        if (!_database.IsConnected)
        {
            Debug.LogWarning("FileManager.SaveMediaFileAsync: Offline. Media upload is not supported offline.");
            return ResultVoid.CreateFailure(ErrorOfflineOperationUnsupported, "Media upload is not supported offline.");
        }

        // 2. Request Upload Slot from Database
        Debug.Log($"FileManager.SaveMediaFileAsync: Requesting upload slot for {objectKey}.");
        _database.RequestMediaUploadSlot(objectKey, originalFilename, contentType); // MODIFIED: Reverted to 3 arguments

        // TODO: This needs to be asynchronous and handle the result of RequestMediaUploadSlot.
        // The current IDatabase.RequestMediaUploadSlot is void. This implies that success/failure
        // is communicated via an event (e.g., OnRequestMediaUploadSlotResultReceived in IDatabase).
        // The FileManager should ideally subscribe to this event, await its outcome, and then proceed.
        // For now, assuming the request will succeed and proceeding. This is a simplification.
        // A robust implementation would require a callback or TaskCompletionSource pattern here.

        Debug.LogWarning("FileManager.SaveMediaFileAsync: Assuming DB upload slot request was successful (current IDatabase method is void). Proceeding to LFS upload.");

        // 3. Upload to Large File Storage (LFS)
        Debug.Log($"FileManager.SaveMediaFileAsync: Calling LFS to upload '{objectKey}'.");
        ResultVoid uploadResult = await _largeFileStorageProvider.UploadFileAsync(objectKey, contentType, data);
        if (!uploadResult.Success)
        {
            Debug.LogError($"FileManager.SaveMediaFileAsync: Failed to upload media '{objectKey}' to LFS. Error: {uploadResult.Error?.Code} - {uploadResult.Error?.Message}");
            // TODO: Update DB metadata status to Failed via an IDatabase method.
            // e.g., _database.UpdateMediaMetadataStatus(objectKey, MediaFileStatus.FailedUpload);
            // This method (UpdateMediaMetadataStatus) needs to exist on IDatabase.
            Debug.LogWarning("FileManager.SaveMediaFileAsync: TODO - Call IDatabase to mark media metadata as 'FailedUpload'.");
            return ResultVoid.CreateFailure(uploadResult.Error?.Code ?? ErrorLfsUploadError, uploadResult.Error?.Message ?? $"Failed to upload file '{objectKey}' to large file storage.");
        }

        // 4. Confirm Upload Complete
        Debug.Log($"FileManager.SaveMediaFileAsync: Upload for '{objectKey}' successful. Confirming with database.");
        // Assuming data.Length gives file size.
        _database.ConfirmMediaUploadComplete(objectKey, (ulong)data.Length);
        // Similar to RequestMediaUploadSlot, this is a fire-and-forget reducer.
        // file_management_plan.md: "If confirmation fails ... Return { success: false, error: { code: "DB_UPLOAD_CONFIRMATION_FAILED" ... } }"
        // This also has the same sync feedback limitation.
        // TODO: Handle failure of ConfirmMediaUploadComplete (e.g. listen to OnConfirmMediaUploadCompleteResultReceived)
        // And if it fails, update DB to Failed or PendingUpload.
        Debug.LogWarning("FileManager.SaveMediaFileAsync: ConfirmMediaUploadComplete is fire-and-forget. Robust error handling for this step needs event listening or state checking.");


        Debug.Log($"FileManager.SaveMediaFileAsync: Media '{objectKey}' saved and upload process initiated/completed.");
        return ResultVoid.CreateSuccess();
    }

    public async Task<ResultVoid> DeleteMediaFileAsync(string objectKey)
    {
        if (string.IsNullOrEmpty(objectKey))
        {
            Debug.LogError("FileManager.DeleteMediaFileAsync: objectKey cannot be null or empty.");
            return ResultVoid.CreateFailure("INVALID_ARGUMENT", "Object key must be provided.");
        }

        string cacheKey = GetMediaCacheKey(objectKey);
        Debug.Log($"FileManager.DeleteMediaFileAsync: Deleting media '{objectKey}'. Cache key: {cacheKey}");

        // 1. Check Online Status
        if (!_database.IsConnected)
        {
            // TODO: Implement robust Sync Service for offline queueing (file_management_plan.md Future Consideration).
            // TODO: Consider local delete policy when offline.
            Debug.LogWarning($"FileManager.DeleteMediaFileAsync: Client is offline. Media '{objectKey}' queued for future deletion (feature pending).");
            // Potentially delete locally: await _localStorageProvider.DeleteAsync(cacheKey);
            return ResultVoid.CreateSuccess();
        }

        // 2. (Optional) Mark for Deletion in DB - Plan says this is optional.
        // _database.MarkAsPendingDeletion(objectKey); // If such a method exists.

        // 3. Trigger Deletion in Large File Storage
        Debug.Log($"FileManager.DeleteMediaFileAsync: Online. Calling LFS to delete '{objectKey}'.");
        ResultVoid lfsDeleteResult = await _largeFileStorageProvider.DeleteFileAsync(objectKey);
        if (!lfsDeleteResult.Success)
        {
            Debug.LogError($"FileManager.DeleteMediaFileAsync: Failed to delete media '{objectKey}' from LFS. Error: {lfsDeleteResult.Error?.Code} - {lfsDeleteResult.Error?.Message}");
            // Don't delete from DB or cache if LFS failed, might retry.
            return ResultVoid.CreateFailure(lfsDeleteResult.Error?.Code ?? ErrorLfsDeleteFailed, lfsDeleteResult.Error?.Message ?? $"Failed to delete object '{objectKey}' from large file storage.");
        }

        // 4. Confirm Deletion in DB
        Debug.Log($"FileManager.DeleteMediaFileAsync: LFS deletion for '{objectKey}' successful. Confirming with database.");
        _database.ConfirmMinioObjectDeleted(objectKey);
        // Debug.LogWarning("FileManager.DeleteMediaFileAsync: TODO - Call IDatabase.ConfirmMinioObjectDeleted. Robust error handling for this step needs event listening or specific DB method result.");


        // 5. Delete from Local Cache
        Debug.Log($"FileManager.DeleteMediaFileAsync: Deleting media '{objectKey}' from local cache.");
        ResultVoid localDeleteResult = await _localStorageProvider.DeleteAsync(cacheKey);
        if (!localDeleteResult.Success)
        {
            // This is not critical if LFS and DB are done.
            Debug.LogWarning($"FileManager.DeleteMediaFileAsync: Failed to delete media '{objectKey}' from local cache or it did not exist. Error: {localDeleteResult.Error?.Code} - {localDeleteResult.Error?.Message}");
        }

        Debug.Log($"FileManager.DeleteMediaFileAsync: Media '{objectKey}' deletion process completed.");
        return ResultVoid.CreateSuccess();
    }
    #endregion

    #region User Profile Management
    private const string UserProfilePrefix = "userprofile_";
    private string GetUserProfileCacheKey(string userId) => $"{UserProfilePrefix}{userId}.json";

    public async Task<ResultVoid> SaveLocalUserProfileAsync(LocalUserProfileData userProfile)
    {
        if (userProfile == null || string.IsNullOrEmpty(userProfile.Id) || string.IsNullOrEmpty(userProfile.Email))
        {
            Debug.LogError("FileManager.SaveLocalUserProfileAsync: UserProfile, ID, or Email cannot be null or empty.");
            return ResultVoid.CreateFailure("INVALID_ARGUMENT", "User profile, ID, or Email must be provided.");
        }

        string cacheKey = GetUserProfileCacheKey(userProfile.Id);
        Debug.Log($"FileManager.SaveLocalUserProfileAsync: Saving user profile for ID {userProfile.Id}. Cache key: {cacheKey}");

        try
        {
            string userProfileJson = JsonUtility.ToJson(userProfile); // Assuming Unity environment for JsonUtility
            ResultVoid writeResult = await _localStorageProvider.WriteTextAsync(cacheKey, userProfileJson);
            if (!writeResult.Success)
            {
                Debug.LogError($"FileManager.SaveLocalUserProfileAsync: Failed to write user profile {userProfile.Id} to local cache. Error: {writeResult.Error?.Code} - {writeResult.Error?.Message}");
                return ResultVoid.CreateFailure(ErrorLocalCacheWriteFailed, writeResult.Error?.Message ?? "Failed to save user profile locally.");
            }
            return ResultVoid.CreateSuccess();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"FileManager.SaveLocalUserProfileAsync: Exception while saving user profile {userProfile.Id}: {ex.Message}");
            return ResultVoid.CreateFailure("LOCAL_STORAGE_ERROR", $"An unexpected error occurred while saving the user profile: {ex.Message}");
        }
    }

    public async Task<Result<System.Collections.Generic.List<LocalUserProfileData>>> GetLocalUserProfilesAsync()
    {
        Debug.Log("FileManager.GetLocalUserProfilesAsync: Attempting to get all local user profiles.");
        Result<System.Collections.Generic.List<string>> keysResult = await _localStorageProvider.ListKeysAsync(UserProfilePrefix);

        if (!keysResult.Success)
        {
            Debug.LogError($"FileManager.GetLocalUserProfilesAsync: Failed to list user profile keys. Error: {keysResult.Error?.Code} - {keysResult.Error?.Message}");
            return Result<System.Collections.Generic.List<LocalUserProfileData>>.CreateFailure(keysResult.Error?.Code ?? "LOCAL_STORAGE_ERROR", keysResult.Error?.Message ?? "Failed to retrieve user profile keys.");
        }

        var profiles = new System.Collections.Generic.List<LocalUserProfileData>();
        if (keysResult.Data == null || keysResult.Data.Count == 0)
        {
            Debug.Log("FileManager.GetLocalUserProfilesAsync: No local user profiles found.");
            return Result<System.Collections.Generic.List<LocalUserProfileData>>.CreateSuccess(profiles); // Return empty list
        }

        foreach (string key in keysResult.Data)
        {
            Result<string> readResult = await _localStorageProvider.ReadTextAsync(key);
            if (readResult.Success && !string.IsNullOrEmpty(readResult.Data))
            {
                try
                {
                    LocalUserProfileData profile = JsonUtility.FromJson<LocalUserProfileData>(readResult.Data);
                    profiles.Add(profile);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"FileManager.GetLocalUserProfilesAsync: Failed to deserialize user profile from key '{key}'. Skipping. Error: {ex.Message}");
                    // Optionally, you could collect these errors or handle them more gracefully.
                }
            }
            else
            {
                Debug.LogWarning($"FileManager.GetLocalUserProfilesAsync: Failed to read user profile data for key '{key}' or data was empty. Error: {readResult.Error?.Code} - {readResult.Error?.Message}");
            }
        }
        return Result<System.Collections.Generic.List<LocalUserProfileData>>.CreateSuccess(profiles);
    }

    public async Task<ResultVoid> DeleteLocalUserProfileAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogError("FileManager.DeleteLocalUserProfileAsync: User ID cannot be null or empty.");
            return ResultVoid.CreateFailure("INVALID_ARGUMENT", "User ID must be provided.");
        }

        string cacheKey = GetUserProfileCacheKey(userId);
        Debug.Log($"FileManager.DeleteLocalUserProfileAsync: Attempting to delete user profile {userId}. Cache key: {cacheKey}");

        ResultVoid deleteResult = await _localStorageProvider.DeleteAsync(cacheKey);
        if (!deleteResult.Success)
        {
            Debug.LogError($"FileManager.DeleteLocalUserProfileAsync: Failed to delete user profile {userId} from local cache. Error: {deleteResult.Error?.Code} - {deleteResult.Error?.Message}");
            return ResultVoid.CreateFailure(ErrorLocalCacheDeleteFailed, deleteResult.Error?.Message ?? "Failed to delete user profile locally.");
        }

        return ResultVoid.CreateSuccess();
    }

    public async Task<Result<LocalUserProfileData>> GetLocalUserProfileAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return Result<LocalUserProfileData>.CreateFailure("ARG_NULL", "User ID cannot be null or empty.");
        }

        string fileName = GetUserProfileCacheKey(userId);
        try
        {
            Result<string> readResult = await _localStorageProvider.ReadTextAsync(fileName);
            if (!readResult.Success || string.IsNullOrEmpty(readResult.Data))
            {
                return Result<LocalUserProfileData>.CreateFailure("NOT_FOUND", $"Local user profile not found for ID: {userId}. Error: {readResult.Error?.Message}");
            }

            LocalUserProfileData userProfile = JsonUtility.FromJson<LocalUserProfileData>(readResult.Data);
            if (userProfile == null)
            {
                return Result<LocalUserProfileData>.CreateFailure("DESERIALIZATION_FAILED", $"Failed to deserialize user profile for ID: {userId}.");
            }
            return Result<LocalUserProfileData>.CreateSuccess(userProfile);
        }
        catch (Exception ex) // Ensure System is imported for Exception
        {
            Debug.LogError($"FileManager: Exception in GetLocalUserProfileAsync for user ID {userId}: {ex.Message}");
            return Result<LocalUserProfileData>.CreateFailure("EXCEPTION", ex.Message);
        }
    }
    #endregion

    #region Prefab Management
    // Constants for prefab related errors if needed
    private const string ErrorPrefabNotFound = "PREFAB_NOT_FOUND";
    private const string ErrorPrefabLoadFailed = "PREFAB_LOAD_FAILED";

    public async Task<Result<GameObject>> GetPrefabAsync(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
        {
            Debug.LogError("FileManager.GetPrefabAsync: resourcePath cannot be null or empty.");
            return Result<GameObject>.CreateFailure("INVALID_ARGUMENT", "Resource path must be provided.");
        }

        Debug.Log($"FileManager.GetPrefabAsync: Attempting to load prefab from Resources path: {resourcePath}");

        try
        {
            // Resources.LoadAsync is asynchronous but doesn't directly return a Task for await.
            // We await its completion using a simple yield pattern if this were a coroutine, 
            // or use TaskCompletionSource for a true async/await pattern.
            // For simplicity and to fit the Task<Result<T>> pattern, we can wrap it.

            var tcs = new TaskCompletionSource<GameObject>();

            ResourceRequest request = Resources.LoadAsync<GameObject>(resourcePath);
            request.completed += operation =>
            {
                if (request.asset == null)
                {
                    Debug.LogWarning($"FileManager.GetPrefabAsync: Prefab not found at Resources path: {resourcePath}");
                    tcs.SetResult(null); // Resolve with null if not found, to be handled by caller
                }
                else
                {
                    tcs.SetResult(request.asset as GameObject);
                }
            };

            GameObject prefab = await tcs.Task;

            if (prefab != null)
            {
                Debug.Log($"FileManager.GetPrefabAsync: Successfully loaded prefab '{prefab.name}' from Resources path: {resourcePath}");
                return Result<GameObject>.CreateSuccess(prefab);
            }
            else
            {
                // This case is already logged by the request.completed callback if asset is null
                return Result<GameObject>.CreateFailure(ErrorPrefabNotFound, $"Prefab not found at Resources path: {resourcePath}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"FileManager.GetPrefabAsync: Error loading prefab from Resources path '{resourcePath}': {ex.Message}");
            return Result<GameObject>.CreateFailure(ErrorPrefabLoadFailed, $"Failed to load prefab: {ex.Message}");
        }
    }
    #endregion

    #region Protocol Discovery
    private const string ErrorProtocolsUnavailable = "PROTOCOLS_UNAVAILABLE";
    private const string ProtocolFilePrefix = "protocol_"; // For ListKeysAsync
    private const string ProtocolFileExtension = ".json"; // For ListKeysAsync

    public async Task<Result<List<ProtocolData>>> GetAvailableProtocolsAsync()
    {
        Debug.Log("FileManager.GetAvailableProtocolsAsync: Attempting to get all available protocols.");

        if (!_database.IsConnected)
        {
            Debug.LogWarning("FileManager.GetAvailableProtocolsAsync: Offline. Attempting to load available protocols from local cache.");
            var localProtocols = new List<ProtocolData>();
            Result<List<string>> keysResult = await _localStorageProvider.ListKeysAsync(ProtocolFilePrefix);

            if (keysResult.Success && keysResult.Data != null)
            {
                foreach (string key in keysResult.Data)
                {
                    if (key.EndsWith(ProtocolFileExtension))
                    {
                        Result<string> contentResult = await _localStorageProvider.ReadTextAsync(key);
                        if (contentResult.Success && !string.IsNullOrEmpty(contentResult.Data))
                        {
                            try
                            {
                                // Extract ID from key: protocol_123.json -> 123
                                string idString = key.Substring(ProtocolFilePrefix.Length, key.Length - ProtocolFilePrefix.Length - ProtocolFileExtension.Length);
                                if (uint.TryParse(idString, out uint protocolId))
                                {
                                    // Create a ProtocolData object. 
                                    // NOTE: This will only have ID and Content populated from the current caching strategy.
                                    // Other metadata fields (Name, Author, etc.) will be default/empty.
                                    // For full metadata offline, the entire ProtocolData object should be cached.
                                    localProtocols.Add(new ProtocolData 
                                    { 
                                        Id = protocolId, 
                                        Content = contentResult.Data,
                                        // Name, IsPublic, OrganizationId, CreatedAtUtc, AuthorName etc. will be missing or default
                                        // Consider adding a flag or property to ProtocolData like "IsPartial" or "IsOfflineCache"
                                        Name = $"Protocol {protocolId} (Offline Cache)" // Placeholder name
                                    });
                                }
                                else
                                {
                                    Debug.LogWarning($"FileManager.GetAvailableProtocolsAsync: Could not parse protocol ID from cache key: {key}");
                                }
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogWarning($"FileManager.GetAvailableProtocolsAsync: Failed to process cached protocol from key '{key}'. Skipping. Error: {ex.Message}");
                            }
                        }
                    }
                }
                Debug.Log($"FileManager.GetAvailableProtocolsAsync: Found {localProtocols.Count} protocols in local cache while offline.");
                return Result<List<ProtocolData>>.CreateSuccess(localProtocols);
            }
            else
            {
                Debug.LogWarning("FileManager.GetAvailableProtocolsAsync: Offline and failed to list protocol keys from local cache or no keys found.");
                return Result<List<ProtocolData>>.CreateSuccess(new List<ProtocolData>()); // Return empty list
            }
        }

        // Online Case: Fetch from Database
        try
        {
            IEnumerable<ProtocolData> protocolsFromDb = _database.GetCachedProtocols();
            if (protocolsFromDb == null)
            {
                 Debug.LogWarning("FileManager.GetAvailableProtocolsAsync: Database returned null for GetCachedProtocols.");
                 return Result<List<ProtocolData>>.CreateSuccess(new List<ProtocolData>());
            }
            
            List<ProtocolData> protocolList = new List<ProtocolData>(protocolsFromDb);
            Debug.Log($"FileManager.GetAvailableProtocolsAsync: Successfully fetched {protocolList.Count} available protocols from database cache.");
            return await Task.FromResult(Result<List<ProtocolData>>.CreateSuccess(protocolList));
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"FileManager.GetAvailableProtocolsAsync: Exception while fetching available protocols: {ex.Message}");
            return Result<List<ProtocolData>>.CreateFailure(ErrorProtocolsUnavailable, $"An error occurred while fetching available protocols: {ex.Message}");
        }
    }

    public async Task<Result<List<ProtocolData>>> GetSavedProtocolsAsync()
    {
        Debug.Log("FileManager.GetSavedProtocolsAsync: Attempting to get saved protocols.");

        if (!_database.IsConnected)
        {
            // TODO: Similar to GetAvailableProtocolsAsync, consider an offline strategy if needed.
            Debug.LogWarning("FileManager.GetSavedProtocolsAsync: Offline. Cannot fetch saved protocols from the database.");
            return Result<List<ProtocolData>>.CreateFailure(ErrorOfflineOperationUnsupported, "Cannot fetch saved protocols while offline.");
        }

        try
        {
            IEnumerable<ProtocolData> savedProtocolsFromDb = _database.GetSavedProtocols(); // Synchronous as per IDatabase
            if (savedProtocolsFromDb == null)
            {
                Debug.LogWarning("FileManager.GetSavedProtocolsAsync: Database returned null for GetSavedProtocols.");
                return Result<List<ProtocolData>>.CreateSuccess(new List<ProtocolData>()); // Return empty list
            }

            List<ProtocolData> savedProtocolList = new List<ProtocolData>(savedProtocolsFromDb);

            Debug.Log($"FileManager.GetSavedProtocolsAsync: Successfully fetched {savedProtocolList.Count} saved protocols from database cache.");
            return await Task.FromResult(Result<List<ProtocolData>>.CreateSuccess(savedProtocolList));
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"FileManager.GetSavedProtocolsAsync: Exception while fetching saved protocols: {ex.Message}");
            return Result<List<ProtocolData>>.CreateFailure(ErrorProtocolsUnavailable, $"An error occurred while fetching saved protocols: {ex.Message}");
        }
    }
    #endregion
}