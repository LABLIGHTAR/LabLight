using System;

public class ConversationParticipantData {
    public ulong ParticipantId { get; set; }
    public ulong ConversationId { get; set; }
    public string ParticipantIdentity { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime LastViewedAt { get; set; }
} 