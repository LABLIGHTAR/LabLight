using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NewChatComponent : VisualElement
{
    public event Action OnCancel;
    public event Action OnMessageSent;

    private readonly IDatabase _database;
    private readonly IFileManager _fileManager;
    private readonly IAudioService _audioService;

    private readonly TextField _recipientSearchField;
    private readonly TextField _messageTextField;
    private readonly ScrollView _searchResultsScrollView;
    private readonly ScrollView _selectedRecipientsScrollView;
    private readonly Button _sendButton;
    private readonly Button _cancelButton;

    private List<UserData> _allUsers;
    private readonly List<UserData> _selectedRecipients = new List<UserData>();

    public NewChatComponent(VisualTreeAsset componentAsset, IDatabase database, IFileManager fileManager, IAudioService audioService, List<UserData> potentialRecipients)
    {
        componentAsset.CloneTree(this);

        _database = database;
        _fileManager = fileManager;
        _audioService = audioService;
        
        _allUsers = potentialRecipients;

        Debug.Log($"[NewChatComponent] Initialized with {_allUsers.Count} potential recipients:");
        foreach (var user in _allUsers)
        {
            Debug.Log($" - User: {user.Name}, ID: {user.Id}, SpacetimeId: {user.SpacetimeId}");
        }

        _recipientSearchField = this.Q<TextField>("recipient-search-field");
        _messageTextField = this.Q<TextField>("chat-message-field");
        _searchResultsScrollView = this.Q<ScrollView>("search-results-scroll-view");
        _selectedRecipientsScrollView = this.Q<ScrollView>("selected-recipients-scroll-view");
        _sendButton = this.Q<Button>("send-chat-button");
        _cancelButton = this.Q<Button>("back-button");

        _recipientSearchField.RegisterValueChangedCallback(evt => PopulateSearchResults(evt.newValue));
        _sendButton.RegisterCallback<ClickEvent>(OnSendClicked);
        _cancelButton.RegisterCallback<ClickEvent>(OnCancelClicked);

        _searchResultsScrollView.style.display = DisplayStyle.None;
    }

    private void PopulateSearchResults(string searchText)
    {
        _searchResultsScrollView.Clear();

        if (string.IsNullOrWhiteSpace(searchText))
        {
            _searchResultsScrollView.style.display = DisplayStyle.None;
            return;
        }

        var filteredUsers = _allUsers
            .Where(user => user.Name.ToLower().Contains(searchText.ToLower()) && !_selectedRecipients.Contains(user))
            .ToList();
        
        foreach (var user in filteredUsers)
        {
            var label = new Label(user.Name);
            label.AddToClassList("user-search-result-item");
            label.userData = user;
            label.RegisterCallback<ClickEvent>(OnSearchResultClicked);
            _searchResultsScrollView.Add(label);
        }

        _searchResultsScrollView.style.display = filteredUsers.Any() ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void OnSearchResultClicked(ClickEvent evt)
    {
        if ((evt.currentTarget as VisualElement)?.userData is UserData selectedUser)
        {
            _selectedRecipients.Add(selectedUser);
            RefreshSelectedRecipients();

            _recipientSearchField.SetValueWithoutNotify("");
            _searchResultsScrollView.Clear();
            _searchResultsScrollView.style.display = DisplayStyle.None;
        }
    }

    private void RefreshSelectedRecipients()
    {
        _selectedRecipientsScrollView.Clear();
        foreach (var user in _selectedRecipients)
        {
            var badge = new VisualElement();
            badge.AddToClassList("recipient-badge");

            var nameLabel = new Label(user.Name);
            nameLabel.AddToClassList("recipient-badge-label");
            
            var removeButton = new Button
            {
                userData = user
            };
            removeButton.AddToClassList("recipient-badge-remove-button");
            removeButton.AddToClassList("icon-close-small");
            removeButton.RegisterCallback<ClickEvent>(OnRemoveRecipientClicked);

            badge.Add(nameLabel);
            badge.Add(removeButton);
            _selectedRecipientsScrollView.Add(badge);
        }
    }

    private void OnRemoveRecipientClicked(ClickEvent evt)
    {
        if ((evt.currentTarget as VisualElement)?.userData is UserData userToRemove)
        {
            _selectedRecipients.Remove(userToRemove);
            RefreshSelectedRecipients();
        }
    }

    private void OnSendClicked(ClickEvent evt)
    {
        _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center);

        if (!_selectedRecipients.Any())
        {
            // TODO: Show an error to the user
            Debug.LogWarning("Cannot send message: No recipients selected.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_messageTextField.value))
        {
            // TODO: Show an error to the user
            Debug.LogWarning("Cannot send message: Message content is empty.");
            return;
        }

        var recipientIdentities = _selectedRecipients.Select(r => r.SpacetimeId).ToList();
        _database.SendDirectMessage(recipientIdentities, _messageTextField.value);
        
        OnMessageSent?.Invoke();
    }

    private void OnCancelClicked(ClickEvent evt)
    {
        _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center);
        OnCancel?.Invoke();
    }
} 