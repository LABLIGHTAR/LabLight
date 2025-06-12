using System;

[Serializable]
public class MediaMetadataData
{
    public ulong MediaId { get; set; }
    public string ObjectKey { get; set; }
    public string OwnerIdentity { get; set; } // Stored as string for client-side use
    public string OriginalFilename { get; set; }
    public string ContentType { get; set; }
    public ulong? FileSize { get; set; }
    public UploadStatus Status { get; set; } // Uses the client-side UploadStatus enum
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UploadCompletedAtUtc { get; set; }
}