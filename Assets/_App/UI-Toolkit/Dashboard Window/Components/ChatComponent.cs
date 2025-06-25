using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using System.Linq;

public class ChatComponent : VisualElement
{
    public event Action OnBack;
    
    private readonly ConversationData _conversation;
    private readonly IDatabase _database;
    private readonly IAudioService _audioService;
    private readonly Func<string, LocalUserProfileData> _userProfileResolver;

    private readonly Button _backButton;
    private readonly Label _conversationTitle;
    private readonly ScrollView _messagesScrollView;
    private readonly TextField _messageInput;
    private readonly Button _sendButton;
    
    public ChatComponent(
        VisualTreeAsset componentAsset,
        ConversationData conversation,
        IDatabase database,
        IAudioService audioService,
        Func<string, LocalUserProfileData> userProfileResolver)
    {
        componentAsset.CloneTree(this);

        _conversation = conversation;
        _database = database;
        _audioService = audioService;
        _userProfileResolver = userProfileResolver;

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
    }

    private void SetTitle()
    {
        _conversationTitle.text = GetConversationName();
    }

    private void PopulateMessages()
    {
        _messagesScrollView.Clear();
        foreach (var message in _conversation.Messages)
        {
            var messageElement = CreateMessageVisualElement(message);
            _messagesScrollView.Add(messageElement);
        }
        ScrollToBottom();
    }

    private void HandleMessageReceived(MessageData message)
    {
        if (message.ConversationId == _conversation.ConversationId)
        {
            var messageElement = CreateMessageVisualElement(message);
            _messagesScrollView.Add(messageElement);
            ScrollToBottom();
            _conversation.Messages.Add(message); // Keep local data in sync
        }
    }

    private void HandleMessageUpdated(MessageData updatedMessage)
    {
        if (updatedMessage.ConversationId != _conversation.ConversationId) return;

        var elementToUpdate = _messagesScrollView.Children()
            .FirstOrDefault(c => c.userData is MessageData md && md.MessageId == updatedMessage.MessageId);

        if (elementToUpdate != null)
        {
            var contentLabel = elementToUpdate.Q<Label>("message-content");
            contentLabel.text = updatedMessage.IsDeleted ? "Message deleted" : updatedMessage.Content;
        }
    }

    private void OnSendClicked(ClickEvent evt)
    {
        var messageText = _messageInput.value;
        if (string.IsNullOrWhiteSpace(messageText)) return;
        
        _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center);
        _database.SendConversationMessage(_conversation.ConversationId, messageText);
        _messageInput.SetValueWithoutNotify("");
    }

    private VisualElement CreateMessageVisualElement(MessageData message)
    {
        var isSentByUser = message.SenderIdentity == SessionState.currentUserProfile.Id;
        
        var row = new VisualElement();
        row.AddToClassList("message-row");
        row.AddToClassList(isSentByUser ? "sent" : "received");
        row.userData = message;

        var bubble = new VisualElement();
        bubble.AddToClassList("message-bubble");
        bubble.AddToClassList(isSentByUser ? "sent" : "received");
        
        if (!isSentByUser && _conversation.Participants.Count > 2)
        {
            var senderName = _userProfileResolver(message.SenderIdentity)?.Name ?? "Unknown";
            var senderLabel = new Label(senderName);
            senderLabel.AddToClassList("sender-name-label");
            bubble.Add(senderLabel);
        }

        var content = new Label(message.IsDeleted ? "Message deleted" : message.Content);
        content.AddToClassList("message-content");
        content.name = "message-content"; // For querying in update
        
        var timestamp = new Label(message.SentAt.ToLocalTime().ToString("h:mm tt"));
        timestamp.AddToClassList("message-timestamp");

        bubble.Add(content);
        bubble.Add(timestamp);
        row.Add(bubble);

        return row;
    }

    private string GetConversationName()
    {
        if (!string.IsNullOrEmpty(_conversation.Name)) return _conversation.Name;
        
        var otherParticipants = _conversation.Participants
            .Where(p => p.ParticipantIdentity != SessionState.currentUserProfile.Id)
            .Select(p => _userProfileResolver(p.ParticipantIdentity)?.Name ?? "Unknown")
            .ToList();

        return !otherParticipants.Any() ? "Personal Notes" : string.Join(", ", otherParticipants);
    }
    
    private void SubscribeToDbEvents()
    {
        if (_database == null) return;
        _database.OnMessageReceived += HandleMessageReceived;
        _database.OnMessageUpdated += HandleMessageUpdated;
    }

    private void ScrollToBottom()
    {
        _messagesScrollView.schedule.Execute(() => _messagesScrollView.verticalScroller.value = _messagesScrollView.verticalScroller.highValue).ExecuteLater(10);
    }
} 