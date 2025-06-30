using System;
using System.Collections.Generic;
using System.Linq;
using SpacetimeDB;
using SpacetimeDB.Types;
using UnityEngine;

public partial class SpacetimeDBImpl
{
    #region Table Callback Handlers
    private void NotifyConversationUpdate(ulong conversationId)
    {
        var conversation = _connection.Db.Conversation.Iter().FirstOrDefault(c => c.ConversationId == conversationId);
        if (conversation == null) return;
        
        var participants = _connection.Db.ConversationParticipant.Iter()
            .Where(p => p.ConversationId == conversationId).ToList();
        
        if (!participants.Any(p => p.ParticipantIdentity == _spacetimedbIdentity)) return;

        var messageEntries = _connection.Db.Message.Iter()
            .Where(m => m.ConversationId == conversationId).ToList();
        
        var conversationData = MapToConversationData(conversation);
        if (conversationData != null)
        {
            OnConversationUpdated?.Invoke(conversationData);
        }
    }

    private void HandleConversationInsert(EventContext ctx, SpacetimeDB.Types.Conversation insertedRow)
    {
        NotifyConversationUpdate(insertedRow.ConversationId);
    }

    private void HandleConversationUpdate(EventContext ctx, SpacetimeDB.Types.Conversation oldRow, SpacetimeDB.Types.Conversation newRow)
    {
        NotifyConversationUpdate(newRow.ConversationId);
    }

    private void HandleConversationDelete(EventContext ctx, SpacetimeDB.Types.Conversation deletedRow)
    {
        OnConversationRemoved?.Invoke(deletedRow.ConversationId);
    }
    
    private void HandleMessageInsert(EventContext ctx, SpacetimeDB.Types.Message insertedRow)
    {
        NotifyConversationUpdate(insertedRow.ConversationId);
    }

    private void HandleMessageUpdate(EventContext ctx, SpacetimeDB.Types.Message oldRow, SpacetimeDB.Types.Message newRow)
    {
        NotifyConversationUpdate(newRow.ConversationId);
    }
    
    private void HandleConversationParticipantInsert(EventContext ctx, SpacetimeDB.Types.ConversationParticipant newParticipant)
    {
        NotifyConversationUpdate(newParticipant.ConversationId);
    }

    private void HandleConversationParticipantUpdate(EventContext ctx, SpacetimeDB.Types.ConversationParticipant oldParticipant, SpacetimeDB.Types.ConversationParticipant newParticipant)
    {
        NotifyConversationUpdate(newParticipant.ConversationId);
    }

    private void HandleConversationParticipantDelete(EventContext ctx, SpacetimeDB.Types.ConversationParticipant deletedParticipant)
    {
        NotifyConversationUpdate(deletedParticipant.ConversationId);
    }
    #endregion

    #region Reducer Calls
    public void SendDirectMessage(List<string> recipientIdentities, string content)
    {
        if (!AssertConnected("send direct message")) return;
        var recipientSpacetimeDBIdentities = recipientIdentities.Select(hex => new Identity(HexStringToByteArray(hex))).ToList();
        _connection.Reducers.SendDirectMessage(recipientSpacetimeDBIdentities, content);
    }

    public void SendConversationMessage(ulong conversationId, string content)
    {
        if (!AssertConnected("send conversation message")) return;
        _connection.Reducers.SendConversationMessage(conversationId, content);
    }

    public void AddUserToConversation(ulong conversationId, string userIdentityToAdd)
    {
        if (!AssertConnected("add user to conversation")) return;
        _connection.Reducers.AddUserToConversation(conversationId, new Identity(HexStringToByteArray(userIdentityToAdd)));
    }

    public void LeaveConversation(ulong conversationId)
    {
        if (!AssertConnected("leave conversation")) return;
        _connection.Reducers.LeaveConversation(conversationId);
    }

    public void RenameConversation(ulong conversationId, string newName)
    {
        if (!AssertConnected("rename conversation")) return;
        _connection.Reducers.RenameConversation(conversationId, newName);
    }

    public void EditMessage(ulong messageId, string newContent)
    {
        if (!AssertConnected("edit message")) return;
        _connection.Reducers.EditMessage(messageId, newContent);
    }

    public void DeleteMessage(ulong messageId)
    {
        if (!AssertConnected("delete message")) return;
        _connection.Reducers.DeleteMessage(messageId);
    }

    public void MarkConversationAsRead(ulong conversationId)
    {
        if (!AssertConnected("mark conversation as read")) return;
        _connection.Reducers.MarkConversationAsRead(conversationId);
    }
    #endregion

    #region Reducer Event Handlers
    private void OnSendDirectMessageReducerEvent(ReducerEventContext ctx, List<Identity> recipients, string content)
    {
        HandleReducerResultBase(ctx, "send direct message");
    }

    private void OnSendConversationMessageReducerEvent(ReducerEventContext ctx, ulong conversationId, string content)
    {
        HandleReducerResultBase(ctx, $"send message to conversation {conversationId}");
        if (ctx.Event.Status is Status.Failed failed) OnMessagingError?.Invoke($"Failed to send message: {failed}");
    }

    private void OnAddUserToConversationReducerEvent(ReducerEventContext ctx, ulong conversationId, Identity userToAdd)
    {
        HandleReducerResultBase(ctx, $"add user to conversation {conversationId}");
        if (ctx.Event.Status is Status.Failed failed) OnMessagingError?.Invoke($"Failed to add user: {failed}");
    }

    private void OnLeaveConversationReducerEvent(ReducerEventContext ctx, ulong conversationId)
    {
        HandleReducerResultBase(ctx, $"leave conversation {conversationId}");
        if (ctx.Event.Status is Status.Failed failed) OnMessagingError?.Invoke($"Failed to leave conversation: {failed}");
    }

    private void OnRenameConversationReducerEvent(ReducerEventContext ctx, ulong conversationId, string newName)
    {
        HandleReducerResultBase(ctx, $"rename conversation {conversationId}");
        if (ctx.Event.Status is Status.Failed failed) OnMessagingError?.Invoke($"Failed to rename conversation: {failed}");
    }

    private void OnEditMessageReducerEvent(ReducerEventContext ctx, ulong messageId, string newContent)
    {
        HandleReducerResultBase(ctx, $"edit message {messageId}");
        if (ctx.Event.Status is Status.Failed failed) OnMessagingError?.Invoke($"Failed to edit message: {failed}");
    }

    private void OnDeleteMessageReducerEvent(ReducerEventContext ctx, ulong messageId)
    {
        HandleReducerResultBase(ctx, $"delete message {messageId}");
        if (ctx.Event.Status is Status.Failed failed) OnMessagingError?.Invoke($"Failed to delete message: {failed}");
    }

    private void OnMarkConversationAsReadReducerEvent(ReducerEventContext ctx, ulong conversationId)
    {
        HandleReducerResultBase(ctx, $"mark conversation {conversationId} as read");
        if (ctx.Event.Status is Status.Failed failed) OnMessagingError?.Invoke($"Failed to mark as read: {failed}");
    }
    #endregion

    #region Data Accessors
    public IEnumerable<ConversationData> GetAllConversations()
    {
        if (!AssertConnected("get all conversations")) yield break;
        
        var participantEntries = _connection.Db.ConversationParticipant.Iter()
            .Where(p => p.ParticipantIdentity == _spacetimedbIdentity.Value);

        foreach (var participantEntry in participantEntries)
        {
            var conversation = _connection.Db.Conversation.Iter()
                .FirstOrDefault(c => c.ConversationId == participantEntry.ConversationId);
            if (conversation != null)
            {
                yield return MapToConversationData(conversation);
            }
        }
    }
    
    public ConversationData GetConversation(ulong conversationId)
    {
        if (!AssertConnected("get conversation")) return null;
        var dbConv = _connection.Db.Conversation.Iter().FirstOrDefault(c => c.ConversationId == conversationId);
        return MapToConversationData(dbConv);
    }
    
    public IEnumerable<MessageData> GetMessages(ulong conversationId)
    {
        if (!AssertConnected("get messages")) return Enumerable.Empty<MessageData>();
        
        return _connection.Db.Message.Iter().Where(m => m.ConversationId == conversationId)
            .Select(MapToMessageData)
            .OrderBy(m => m.SentAt);
    }
    #endregion

    #region Mapping Functions
    private MessageData MapToMessageData(SpacetimeDB.Types.Message dbMsg)
    {
        if (dbMsg == null) return null;

        return new MessageData
        {
            MessageId = dbMsg.MessageId,
            ConversationId = dbMsg.ConversationId,
            SenderIdentity = dbMsg.SenderIdentity.ToString(),
            Content = dbMsg.Content,
            LastEditedAt = dbMsg.LastEditedAt.HasValue ? TimestampToDateTime(dbMsg.LastEditedAt.Value) : (DateTime?)null,
            MessageType = (ConversationMessageType)Enum.Parse(typeof(ConversationMessageType), dbMsg.MessageType.ToString()),
            IsDeleted = dbMsg.IsDeleted,
            SentAt = TimestampToDateTime(dbMsg.SentAt),
        };
    }

    private ConversationParticipantData MapToConversationParticipantData(SpacetimeDB.Types.ConversationParticipant dbParticipant)
    {
        if (dbParticipant == null) return null;

        return new ConversationParticipantData
        {
            ParticipantId = dbParticipant.ParticipantId,
            ConversationId = dbParticipant.ConversationId,
            ParticipantIdentity = dbParticipant.ParticipantIdentity.ToString(),
            JoinedAt = TimestampToDateTime(dbParticipant.JoinedAt),
            LastViewedAt = TimestampToDateTime(dbParticipant.LastViewedAt),
        };
    }

    private ConversationData MapToConversationData(SpacetimeDB.Types.Conversation dbConv)
    {
        if (dbConv == null) return null;

        var conversationData = new ConversationData
        {
            ConversationId = dbConv.ConversationId,
            LastMessageAt = TimestampToDateTime(dbConv.LastMessageAt),
            ParticipantsHash = dbConv.ParticipantsHash,
            Name = dbConv.Name,
            CreatedAt = TimestampToDateTime(dbConv.CreatedAt),
            CreatedByIdentity = dbConv.CreatedBy.ToString(),
            Status = (ConversationStatus)Enum.Parse(typeof(ConversationStatus), dbConv.Status.ToString()),
        };
        
        conversationData.Participants = _connection.Db.ConversationParticipant.Iter()
            .Where(p => p.ConversationId == dbConv.ConversationId)
            .Select(MapToConversationParticipantData).ToList();
            
        conversationData.Messages = _connection.Db.Message.Iter()
            .Where(m => m.ConversationId == dbConv.ConversationId)
            .Select(MapToMessageData).OrderBy(m => m.SentAt).ToList();

        var currentUserParticipant = conversationData.Participants
            .FirstOrDefault(p => p.ParticipantIdentity == CurrentUserId);

        if (currentUserParticipant != null)
        {
            conversationData.UnreadCount = conversationData.Messages
                .Count(m => m.SentAt > currentUserParticipant.LastViewedAt && m.SenderIdentity != CurrentUserId);
        }

        return conversationData;
    }
    #endregion
} 