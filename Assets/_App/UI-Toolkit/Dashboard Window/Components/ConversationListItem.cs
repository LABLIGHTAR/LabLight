using UnityEngine.UIElements;
using System;
using System.Linq;
using System.Collections.Generic;

public class ConversationListItem : VisualElement
{
    private readonly Label _conversationName;
    private readonly Label _lastMessage;
    private readonly Label _timestamp;
    private readonly Label _unreadCount;
    private readonly VisualElement _unreadBadge;

    private ConversationData _conversation;
    private IDatabase _database;
    private readonly List<string> _missingParticipantIds = new List<string>();

    public ConversationListItem(
        VisualTreeAsset listItemAsset,
        IDatabase database)
    {
        _database = database;
        
        listItemAsset.CloneTree(this);
        
        _conversationName = this.Q<Label>("conversation-name");
        _lastMessage = this.Q<Label>("last-message");
        _timestamp = this.Q<Label>("timestamp");
        _unreadCount = this.Q<Label>("unread-count");
        _unreadBadge = this.Q<VisualElement>("unread-badge");
        
        this.AddManipulator(new Clickable(() => {}));

        _database.OnUserProfileUpdated += HandleUserProfileUpdated;
    }
    
    public void Cleanup()
    {
        if (_database != null)
        {
            _database.OnUserProfileUpdated -= HandleUserProfileUpdated;
        }
        _database = null;
    }

    private void HandleUserProfileUpdated(UserData updatedProfile)
    {
        if (updatedProfile == null || string.IsNullOrEmpty(updatedProfile.SpacetimeId)) return;

        if (_missingParticipantIds.Contains(updatedProfile.SpacetimeId))
        {
            Bind(_conversation);
        }
    }

    public void Bind(ConversationData conversation)
    {
        _conversation = conversation;
        _missingParticipantIds.Clear();

        _conversationName.text = GetConversationName(conversation);

        var lastMsg = conversation.Messages?.LastOrDefault();
        if (lastMsg != null)
        {
            string senderName;
            if (lastMsg.SenderIdentity == _database.CurrentIdentity)
            {
                senderName = "You";
            }
            else
            {
                var senderProfile = _database.GetCachedUserProfile(lastMsg.SenderIdentity);
                if (senderProfile == null)
                {
                    _missingParticipantIds.Add(lastMsg.SenderIdentity);
                }
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
    
    private string GetConversationName(ConversationData conversation)
    {
        if (!string.IsNullOrEmpty(conversation.Name))
        {
            return conversation.Name;
        }

        var otherParticipants = conversation.Participants
            .Where(p => p.ParticipantIdentity != _database.CurrentIdentity)
            .Select(p =>
            {
                var user = _database.GetCachedUserProfile(p.ParticipantIdentity);
                if (user == null)
                {
                    _missingParticipantIds.Add(p.ParticipantIdentity);
                    return "Unknown";
                }
                return user.Name;
            })
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