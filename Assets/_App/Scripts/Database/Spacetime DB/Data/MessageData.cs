using System;

public class MessageData {
    public ulong MessageId { get; set; }
    public ulong ConversationId { get; set; }
    public string SenderIdentity { get; set; }
    public string Content { get; set; }
    public DateTime? LastEditedAt { get; set; }
    public ConversationMessageType MessageType { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime SentAt { get; set; }
} 