using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System;

public class ConversationsListComponent : VisualElement
{
    public event Action OnNewChatRequested;
    public event Action<ConversationData> OnConversationSelected;

    private readonly VisualTreeAsset _conversationListItemAsset;
    private readonly ScrollView _scrollView;
    private readonly Button _newChatButton;
    
    private readonly IDatabase _database;
    private readonly IAudioService _audioService;

    public ConversationsListComponent(
        VisualTreeAsset componentAsset,
        VisualTreeAsset conversationListItemAsset,
        IDatabase database,
        IAudioService audioService)
    {
        componentAsset.CloneTree(this);

        _conversationListItemAsset = conversationListItemAsset;
        _database = database;
        _audioService = audioService;

        _scrollView = this.Q<ScrollView>("conversations-scroll-view");
        if (_scrollView == null)
        {
            Debug.LogError("[ConversationsListComponent] Could not find a ScrollView named 'conversations-scroll-view' in the UXML. The component will not function correctly.");
        }
        
        _newChatButton = this.Q<Button>("new-chat-button");
        _newChatButton.clicked += () => OnNewChatRequested?.Invoke();
    }

    public void RefreshConversations(List<ConversationData> conversations)
    {
        if (_scrollView == null) return;
        
        ClearAllListItems();

        if (conversations == null || conversations.Count == 0)
        {
            _scrollView.Add(new Label("No conversations yet."));
            return;
        }

        foreach (var conversation in conversations.OrderByDescending(c => (c.Messages != null && c.Messages.Any()) ? c.Messages.Last().SentAt : c.CreatedAt))
        {
            var listItem = new ConversationListItem(_conversationListItemAsset, _database);
            listItem.Bind(conversation);
            listItem.RegisterCallback<ClickEvent>(evt => OnListItemClicked(conversation, evt));
            _scrollView.Add(listItem);
        }
    }

    private void OnListItemClicked(ConversationData conversation, ClickEvent evt)
    {
        _audioService?.PlayButtonPress((evt.target as VisualElement).worldBound.center);
        OnConversationSelected?.Invoke(conversation);
    }

    private void ClearAllListItems()
    {
        // We must iterate over a copy since we are modifying the collection
        foreach (var item in _scrollView.Children().ToList())
        {
            if (item is ConversationListItem listItem)
            {
                listItem.Cleanup();
            }
        }
        _scrollView.Clear();
    }
} 