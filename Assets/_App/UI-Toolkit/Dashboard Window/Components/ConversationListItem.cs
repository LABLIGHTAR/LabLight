using UnityEngine.UIElements;
using System;
using System.Linq;

public class ConversationListItem : VisualElement
{
    private readonly Label _conversationName;
    private readonly Label _lastMessage;
    private readonly Label _timestamp;
    private readonly Label _unreadCount;
    private readonly VisualElement _unreadBadge;

    public ConversationData Conversation { get; private set; }

    public ConversationListItem(
        VisualTreeAsset listItemAsset,
        ConversationData conversation, 
        Func<string, LocalUserProfileData> userProfileResolver)
    {
        listItemAsset.CloneTree(this);
        
        _conversationName = this.Q<Label>("conversation-name");
        _lastMessage = this.Q<Label>("last-message");
        _timestamp = this.Q<Label>("timestamp");
        _unreadCount = this.Q<Label>("unread-count");
        _unreadBadge = this.Q<VisualElement>("unread-badge");
        
        this.AddManipulator(new Clickable(() => {}));

        Bind(conversation, userProfileResolver);
    }

    private void Bind(ConversationData conversation, Func<string, LocalUserProfileData> userProfileResolver)
    {
        Conversation = conversation;

        _conversationName.text = GetConversationName(conversation, userProfileResolver);

        var lastMsg = conversation.Messages?.LastOrDefault();
        if (lastMsg != null)
        {
            string senderName;
            if (lastMsg.SenderIdentity == SessionState.currentUserProfile.Id)
            {
                senderName = "You";
            }
            else
            {
                var senderProfile = userProfileResolver(lastMsg.SenderIdentity);
                senderName = senderProfile?.Name ?? "Unknown";
            }
            
            _lastMessage.text = $"{senderName}: {lastMsg.Content}";
            _timestamp.text = FormatTimestamp(lastMsg.SentAt);
        }
        else
        {
            _lastMessage.text = "No messages yet.";
            _timestamp.text = FormatTimestamp(conversation.CreatedAt);
        }

        if (conversation.UnreadCount > 0)
        {
            _unreadCount.text = conversation.UnreadCount.ToString();
            _unreadBadge.RemoveFromClassList("hidden");
        }
        else
        {
            _unreadBadge.AddToClassList("hidden");
        }
    }
    
    private string GetConversationName(ConversationData conversation, Func<string, LocalUserProfileData> userProfileResolver)
    {
        if (!string.IsNullOrEmpty(conversation.Name))
        {
            return conversation.Name;
        }

        var otherParticipants = conversation.Participants
            .Where(p => p.ParticipantIdentity != SessionState.currentUserProfile.Id)
            .Select(p => userProfileResolver(p.ParticipantIdentity)?.Name ?? "Unknown")
            .ToList();

        if (!otherParticipants.Any())
        {
            return "Personal Notes";
        }

        return string.Join(", ", otherParticipants);
    }
    
    private string FormatTimestamp(DateTime timestamp)
    {
        var localTime = timestamp.ToLocalTime();
        if (localTime.Date == DateTime.Today)
        {
            return localTime.ToString("h:mm tt");
        }
        if (localTime.Date == DateTime.Today.AddDays(-1))
        {
            return "Yesterday";
        }
        return localTime.ToString("M/d/yy");
    }
} 