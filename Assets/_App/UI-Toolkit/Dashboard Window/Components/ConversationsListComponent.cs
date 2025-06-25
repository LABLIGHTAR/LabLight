using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using System.Linq;

public class ConversationsListComponent : VisualElement
{
    public event Action OnNewChatRequested;
    public event Action<ConversationData> OnConversationSelected;

    private readonly VisualTreeAsset _listItemAsset;
    private readonly IDatabase _database;
    private readonly IAudioService _audioService;
    private readonly Func<string, LocalUserProfileData> _userProfileResolver;

    private readonly ScrollView _scrollView;
    private readonly Button _newChatButton;

    public ConversationsListComponent(
        VisualTreeAsset componentAsset, 
        VisualTreeAsset listItemAsset, 
        IDatabase database, 
        IAudioService audioService,
        Func<string, LocalUserProfileData> userProfileResolver)
    {
        if (componentAsset == null)
        {
            Debug.LogError("ConversationsListComponent: The provided component asset is null. Please assign the 'ConversationsListComponent.uxml' asset to the 'Conversations List Component Asset' field on the DashboardWindowController in the Unity Inspector.");
            return;
        }
        
        componentAsset.CloneTree(this);

        _listItemAsset = listItemAsset;
        _database = database;
        _audioService = audioService;
        _userProfileResolver = userProfileResolver;

        _newChatButton = this.Q<Button>("new-chat-button");
        _scrollView = this.Q<ScrollView>("conversations-scroll-view");

        _newChatButton.RegisterCallback<ClickEvent>(evt => {
            _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center);
            OnNewChatRequested?.Invoke();
        });

        SubscribeToDbEvents();
        RefreshConversations();
    }
    
    private void SubscribeToDbEvents()
    {
        if (_database == null) return;
        _database.OnConversationAdded += HandleConversationChange;
        _database.OnConversationUpdated += HandleConversationChange;
        _database.OnConversationRemoved += HandleConversationRemoved;
        _database.OnMessageReceived += HandleMessageReceived;
    }

    public void UnsubscribeFromDbEvents()
    {
        if (_database == null) return;
        _database.OnConversationAdded -= HandleConversationChange;
        _database.OnConversationUpdated -= HandleConversationChange;
        _database.OnConversationRemoved -= HandleConversationRemoved;
        _database.OnMessageReceived -= HandleMessageReceived;
    }

    private void RefreshConversations()
    {
        _scrollView.Clear();
        if (_database == null)
        {
            _scrollView.Add(new Label("Database service not available."));
            return;
        }

        var conversations = _database.GetAllConversations().OrderByDescending(c => c.LastMessageAt).ToList();

        foreach (var conversation in conversations)
        {
            var listItem = new ConversationListItem(_listItemAsset, conversation, _userProfileResolver);
            listItem.RegisterCallback<ClickEvent>(evt => OnListItemClicked(listItem.Conversation, evt));
            _scrollView.Add(listItem);
        }
    }

    private void OnListItemClicked(ConversationData conversation, ClickEvent evt)
    {
        _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center);
        OnConversationSelected?.Invoke(conversation);
    }

    private void HandleConversationChange(ConversationData conversation)
    {
        RefreshConversations();
    }

    private void HandleConversationRemoved(ulong conversationId)
    {
        RefreshConversations();
    }
    
    private void HandleMessageReceived(MessageData message)
    {
        RefreshConversations();
    }
} 