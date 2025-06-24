using System;
using System.Collections.Generic;

public class ConversationData {
    public ulong ConversationId { get; set; }
    public DateTime LastMessageAt { get; set; }
    public string ParticipantsHash { get; set; }
    public string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedByIdentity { get; set; }
    public ConversationStatus Status { get; set; }
    public List<ConversationParticipantData> Participants { get; set; } = new List<ConversationParticipantData>();
    public List<MessageData> Messages { get; set; } = new List<MessageData>();
    public int UnreadCount { get; set; }
} 