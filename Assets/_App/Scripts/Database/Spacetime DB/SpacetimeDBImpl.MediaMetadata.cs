using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using SpacetimeDB;
using SpacetimeDB.Types;

public partial class SpacetimeDBImpl
{
    public event Action<bool> OnRequestMediaUploadSlotResultReceived;
    public event Action<bool> OnConfirmMediaUploadCompleteResultReceived;
    public event Action OnMediaMetadataUpdate; // Generic event for any media metadata change

    #region Reducer Calls
    public void RequestMediaUploadSlot(string objectKey, string originalFilename, string contentType)
    {
        if (!AssertConnected()) return;
        Debug.Log($"Requesting media upload slot for objectKey: {objectKey}, filename: {originalFilename}");
        _connection.Reducers.RequestMediaUploadSlot(objectKey, originalFilename, contentType);
    }

    public void ConfirmMediaUploadComplete(string objectKey, ulong fileSize)
    {
        if (!AssertConnected()) return;
        Debug.Log($"Confirming media upload complete for objectKey: {objectKey}, size: {fileSize}");
        _connection.Reducers.ConfirmMediaUploadComplete(objectKey, fileSize);
    }

    public void ConfirmMinioObjectDeleted(string objectKey)
    {
        if (!AssertConnected("confirm MinIO object deleted")) return;
        if (string.IsNullOrEmpty(objectKey))
        {
            Debug.LogError("SpacetimeDBImpl.ConfirmMinioObjectDeleted: objectKey cannot be null or empty.");
            return;
        }

        Debug.Log($"SpacetimeDBImpl: Calling reducer to delete media_metadata for objectKey: {objectKey}");
        _connection.Reducers.DeleteMediaMetadata(objectKey);
        // Note: Reducer calls are fire-and-forget.
        // If you need to confirm the reducer's success/failure, you'd typically listen for a SpacetimeDB event
        // or check the result in the reducer callback if it has one (e.g., OnDeleteMediaMetadataDone).
        // For now, we'll rely on the table update event (OnMediaMetadataDelete) if the row is actually deleted.
    }
    #endregion

    #region Reducer Event Handlers
    private void OnRequestMediaUploadSlotReducerEvent(ReducerEventContext ctx, string objectKey, string originalFilename, string contentType)
    {
        string errorMessage = "None";
        if (ctx.Event.Status is Status.Failed failedStatus) { errorMessage = failedStatus.ToString(); }
        Debug.LogFormat("Reducer Event: RequestMediaUploadSlot, Status: {0}, Error: {1}, objectKey: {2}", ctx.Event.Status, errorMessage, objectKey);
        HandleReducerResultBase(ctx, "RequestMediaUploadSlot"); 
        OnRequestMediaUploadSlotResultReceived?.Invoke(ctx.Event.Status is Status.Committed);
    }

    private void OnConfirmMediaUploadCompleteReducerEvent(ReducerEventContext ctx, string objectKey, ulong fileSize)
    {
        string errorMessage = "None";
        if (ctx.Event.Status is Status.Failed failedStatus) { errorMessage = failedStatus.ToString(); }
        Debug.LogFormat("Reducer Event: ConfirmMediaUploadComplete, Status: {0}, Error: {1}", ctx.Event.Status, errorMessage);
        HandleReducerResultBase(ctx, "ConfirmMediaUploadComplete"); 
        OnConfirmMediaUploadCompleteResultReceived?.Invoke(ctx.Event.Status is Status.Committed);
    }
    #endregion

    #region Data Access / Cache
    public IEnumerable<MediaMetadataData> GetCachedMediaMetadataEntries()
    {
        if (!AssertConnected() || _connection?.Db == null || _connection.Identity == null) return Enumerable.Empty<MediaMetadataData>();

        return _connection.Db.MediaMetadata.Iter()
            .Select(MapToMediaMetadataData)
            .ToList();
    }

    public MediaMetadataData GetCachedMediaMetadata(string objectKey)
    {
        if (!AssertConnected() || _connection?.Db == null) return null;
        var spdbMetadata = _connection.Db.MediaMetadata.Iter().FirstOrDefault(m => m.ObjectKey == objectKey);
        return spdbMetadata == null ? null : MapToMediaMetadataData(spdbMetadata);
    }

    public MediaMetadataData GetCachedMediaMetadataById(ulong mediaId)
    {
        if (!AssertConnected() || _connection?.Db == null) return null;
        var spdbMetadata = _connection.Db.MediaMetadata.Iter().FirstOrDefault(m => m.MediaId == mediaId);
        return spdbMetadata == null ? null : MapToMediaMetadataData(spdbMetadata);
    }
    #endregion

    #region Mappers
    private MediaMetadataData MapToMediaMetadataData(SpacetimeDB.Types.MediaMetadata spdbMetadata)
    {
        if (spdbMetadata == null) return null;
        return new MediaMetadataData
        {
            MediaId = spdbMetadata.MediaId,
            ObjectKey = spdbMetadata.ObjectKey,
            OwnerIdentity = spdbMetadata.OwnerIdentity.ToString(),
            OriginalFilename = spdbMetadata.OriginalFilename,
            ContentType = spdbMetadata.ContentType,
            FileSize = spdbMetadata.FileSize,
            Status = MapSpacetimeDBUploadStatus(spdbMetadata.UploadStatus),
            CreatedAtUtc = TimestampToDateTime(spdbMetadata.CreatedAt),
            UploadCompletedAtUtc = spdbMetadata.UploadCompletedAt.HasValue 
                ? TimestampToDateTime(spdbMetadata.UploadCompletedAt.Value) 
                : (DateTime?)null
        };
    }

    private global::UploadStatus MapSpacetimeDBUploadStatus(SpacetimeDB.Types.UploadStatus spdbStatus)
    {
        switch (spdbStatus)
        {
            case SpacetimeDB.Types.UploadStatus.PendingUpload:
                return global::UploadStatus.PendingUpload;
            case SpacetimeDB.Types.UploadStatus.Available:
                return global::UploadStatus.Available;
            case SpacetimeDB.Types.UploadStatus.Failed:
                return global::UploadStatus.Failed;
            default:
                Debug.LogError($"Unknown SpacetimeDB.Types.UploadStatus: {spdbStatus}");
                return global::UploadStatus.Failed;
        }
    }
    #endregion

    #region Table Update Handlers
    private void HandleMediaMetadataInsert(EventContext ctx, SpacetimeDB.Types.MediaMetadata insertedValue)
    {
        Debug.Log("MediaMetadata inserted.");
        OnMediaMetadataUpdate?.Invoke();
    }

    private void HandleMediaMetadataUpdate(EventContext ctx, SpacetimeDB.Types.MediaMetadata oldValue, SpacetimeDB.Types.MediaMetadata newValue)
    {
        Debug.Log("MediaMetadata updated.");
        OnMediaMetadataUpdate?.Invoke();
    }
    
    private void HandleMediaMetadataDelete(EventContext ctx, SpacetimeDB.Types.MediaMetadata deletedValue)
    {
        Debug.Log("MediaMetadata deleted.");
        OnMediaMetadataUpdate?.Invoke();
    }
    #endregion
} 