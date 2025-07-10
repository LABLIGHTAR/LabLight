using UnityEngine.UIElements;
using System;

public class ChatMessageListItemController : VisualElement
{
    public new class UxmlFactory : UxmlFactory<ChatMessageListItemController, UxmlTraits> { }
    public new class UxmlTraits : VisualElement.UxmlTraits { }

    private Label _senderNameLabel;
    private Label _messageContentLabel;
    private Label _messageTimestampLabel;
    private VisualElement _messageBubble;

    public ChatMessageListItemController()
    {
        RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
    }

    private void OnAttachToPanel(AttachToPanelEvent evt)
    {
        _senderNameLabel = this.Q<Label>("sender-name-label");
        _messageContentLabel = this.Q<Label>("message-content");
        _messageTimestampLabel = this.Q<Label>("message-timestamp");
        _messageBubble = this.Q<VisualElement>("message-bubble");
    }

    public void SetMessageData(MessageData message, UserData sender, bool isSentByUser, bool isGroupChat)
    {
        // It's possible SetMessageData is called before OnAttachToPanel.
        // Ensure the elements are queried if they haven't been already.
        if (_messageContentLabel == null)
        {
            OnAttachToPanel(null); // Manually call to query elements
        }

        this.userData = message;
        _messageContentLabel.text = message.IsDeleted ? "Message deleted" : message.Content;
        _messageTimestampLabel.text = message.SentAt.ToLocalTime().ToString("h:mm tt");

        // Add 'sent' or 'received' classes to the row and bubble for styling
        var statusClass = isSentByUser ? "sent" : "received";
        this.AddToClassList(statusClass);
        _messageBubble.AddToClassList(statusClass);
        
        // Show sender name only on received messages in a group chat
        if (!isSentByUser && isGroupChat)
        {
            _senderNameLabel.text = sender?.Name ?? "Unknown";
            _senderNameLabel.style.display = DisplayStyle.Flex;
        }
        else
        {
            _senderNameLabel.style.display = DisplayStyle.None;
        }
    }

    public void UpdateMessageContent(MessageData message)
    {
        _messageContentLabel.text = message.IsDeleted ? "Message deleted" : message.Content;
    }
} 