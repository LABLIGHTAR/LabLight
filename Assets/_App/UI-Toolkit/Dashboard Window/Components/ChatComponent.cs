using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using System.Linq;

public class ChatComponent : VisualElement
{
    public event Action OnBack;
    
    public ulong ConversationId => _conversation.ConversationId;
    
    private ConversationData _conversation;
    private readonly IDatabase _database;
    private readonly IAudioService _audioService;
    private readonly List<string> _missingParticipantIds = new List<string>();

    private readonly Button _backButton;
    private readonly Label _conversationTitle;
    private readonly ScrollView _messagesScrollView;
    private readonly TextField _messageInput;
    private readonly Button _sendButton;
    private readonly VisualTreeAsset _chatMessageListItemAsset;

    public ChatComponent(
        VisualTreeAsset componentAsset,
        VisualTreeAsset chatMessageListItemAsset,
        ConversationData conversation,
        IDatabase database,
        IAudioService audioService)
    {
        componentAsset.CloneTree(this);

        _chatMessageListItemAsset = chatMessageListItemAsset;
        _conversation = conversation;
        _database = database;
        _audioService = audioService;

        _backButton = this.Q<Button>("back-button");
        _conversationTitle = this.Q<Label>("conversation-title");
        _messagesScrollView = this.Q<ScrollView>("messages-scroll-view");
        _messageInput = this.Q<TextField>("message-input");
        _sendButton = this.Q<Button>("send-button");
        
        _backButton.RegisterCallback<ClickEvent>(evt => OnBack?.Invoke());
        _sendButton.RegisterCallback<ClickEvent>(OnSendClicked);

        SetTitle();
        PopulateMessages();
        SubscribeToDbEvents();
    }

    public void UnsubscribeFromDbEvents()
    {
        if (_database == null) return;
        _database.OnMessageReceived -= HandleMessageReceived;
        _database.OnMessageUpdated -= HandleMessageUpdated;
        _database.OnUserProfileUpdated -= HandleUserProfileUpdated;
    }

    private void HandleUserProfileUpdated(UserData updatedProfile)
    {
        if (updatedProfile == null || string.IsNullOrEmpty(updatedProfile.SpacetimeId)) return;

        if (_missingParticipantIds.Contains(updatedProfile.SpacetimeId))
        {
            SetTitle();
            // We could refresh all messages, but for now we'll just update the title
            // as re-populating could be expensive.
        }
    }

    private void SetTitle()
    {
        _conversationTitle.text = GetConversationName();
    }

    private void PopulateMessages()
    {
        _messagesScrollView.Clear();
        _missingParticipantIds.Clear();
        foreach (var message in _conversation.Messages)
        {
            AddMessage(message);
        }
        ScrollToBottom();
    }

    private void HandleMessageReceived(MessageData message)
    {
        if (message.ConversationId == _conversation.ConversationId)
        {
            AddMessage(message);
            ScrollToBottom();
            _conversation.Messages.Add(message); // Keep local data in sync
        }
    }
    
    private void AddMessage(MessageData message)
    {
        var listItem = _chatMessageListItemAsset.Instantiate();
        var controller = listItem.Q<ChatMessageListItemController>();

        var isSentByUser = message.SenderIdentity == _database.CurrentIdentity;
        var senderProfile = isSentByUser ? null : _database.GetCachedUserProfile(message.SenderIdentity);
        var isGroupChat = _conversation.Participants.Count > 2;
        
        if (!isSentByUser && senderProfile == null)
        {
            _missingParticipantIds.Add(message.SenderIdentity);
        }

        controller.SetMessageData(message, senderProfile, isSentByUser, isGroupChat);
        _messagesScrollView.Add(listItem);
    }

    private void HandleMessageUpdated(MessageData updatedMessage)
    {
        if (updatedMessage.ConversationId != _conversation.ConversationId) return;

        var elementToUpdate = _messagesScrollView.Children()
            .FirstOrDefault(c => c.userData is MessageData md && md.MessageId == updatedMessage.MessageId);

        var controller = elementToUpdate?.Q<ChatMessageListItemController>();
        controller?.UpdateMessageContent(updatedMessage);
    }

    private void OnSendClicked(ClickEvent evt)
    {
        var messageText = _messageInput.value;
        if (string.IsNullOrWhiteSpace(messageText)) return;
        
        _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center);
        _database.SendConversationMessage(_conversation.ConversationId, messageText);
        _messageInput.SetValueWithoutNotify("");
    }

    private string GetConversationName()
    {
        if (!string.IsNullOrEmpty(_conversation.Name)) return _conversation.Name;
        
        var otherParticipants = _conversation.Participants
            .Where(p => p.ParticipantIdentity != _database.CurrentIdentity)
            .Select(p =>
            {
                var user = _database.GetCachedUserProfile(p.ParticipantIdentity);
                if (user == null)
                {
                    if (!_missingParticipantIds.Contains(p.ParticipantIdentity))
                    {
                        _missingParticipantIds.Add(p.ParticipantIdentity);
                    }
                    return "Unknown";
                }
                return user.Name;
            })
            .ToList();

        return !otherParticipants.Any() ? "Personal Notes" : string.Join(", ", otherParticipants);
    }
    
    private void SubscribeToDbEvents()
    {
        if (_database == null) return;
        _database.OnMessageReceived += HandleMessageReceived;
        _database.OnMessageUpdated += HandleMessageUpdated;
        _database.OnUserProfileUpdated += HandleUserProfileUpdated;
    }

    private void ScrollToBottom()
    {
        _messagesScrollView.schedule.Execute(() => _messagesScrollView.verticalScroller.value = _messagesScrollView.verticalScroller.highValue).ExecuteLater(10);
    }
} 