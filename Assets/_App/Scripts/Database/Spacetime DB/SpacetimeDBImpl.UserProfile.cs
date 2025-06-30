using UnityEngine;
using System;
using System.Linq;
using SpacetimeDB;
using SpacetimeDB.Types;
using System.Collections.Generic;

public partial class SpacetimeDBImpl
{
    // Event for UserProfile updates (declaration moved here from main file)
    // Note: The actual CurrentUserProfile property remains in the main SpacetimeDBImpl file
    // as it's tightly coupled with connection state.
    // public event Action<UserData> OnUserProfileUpdated; // This line was identified to be moved, but public events can't be in partial if not all partials define it.
                                                        // It's better to keep the event declaration in the main SpacetimeDBImpl.cs file
                                                        // where other public interface events are declared.
                                                        // Partial methods can still invoke it.

    #region Profile Management (Interface Method + Internal Logic)
    private void HandleUserProfileInsert(EventContext ctx, UserProfile userProfile)
    {
        var userData = MapToUserData(userProfile);
        if (userData == null) return;
        
        Debug.Log($"Received new user profile from DB: {userData.Name} ({userData.SpacetimeId})");
        OnUserProfileUpdated?.Invoke(userData);

        if (_spacetimedbIdentity.HasValue && userProfile.Identity == _spacetimedbIdentity.Value) {
            CurrentUserProfile = userData; 
            Debug.Log($"Local UserProfile created/updated via Insert callback for CURRENT user: {CurrentUserProfile?.Name}");
        }
    }

    private void HandleUserProfileUpdate(EventContext ctx, SpacetimeDB.Types.UserProfile oldSpdbProfile, SpacetimeDB.Types.UserProfile newSpdbProfile)
    {
        var userData = MapToUserData(newSpdbProfile);
        if (userData == null) return;

        Debug.Log($"Received updated user profile from DB: {userData.Name} ({userData.SpacetimeId})");
        OnUserProfileUpdated?.Invoke(userData);

        if (_spacetimedbIdentity.HasValue && newSpdbProfile.Identity == _spacetimedbIdentity.Value) {
            CurrentUserProfile = userData; 
            Debug.Log($"Local UserProfile updated via Update callback for CURRENT user: {CurrentUserProfile?.Name}");
         }
    }

    public void RegisterProfile(string displayName)
    {
        if (!AssertConnected("register profile")) return;
        if (string.IsNullOrWhiteSpace(displayName)) { LogErrorAndInvoke("Display name cannot be empty."); return; }
        if (CurrentUserProfile != null) { LogErrorAndInvoke("Profile already exists for this user."); return; }

        Debug.Log($"SpacetimeDB: Requesting profile registration: {displayName}");
        _connection.Reducers.RegisterProfile(displayName);
    }

    private void OnRegisterProfileResult(ReducerEventContext ctx, string name)
    {
        if (ctx.Event.CallerIdentity == _spacetimedbIdentity) {
            if (ctx.Event.Status is Status.Committed) {
                Debug.Log($"SpacetimeDB: Successfully registered profile '{name}'. Update event will follow.");
            } else if (ctx.Event.Status is Status.Failed failedStatus) {
                LogErrorAndInvoke($"Failed to register profile: {failedStatus.ToString()}");
            } else {
                 LogErrorAndInvoke($"Failed to register profile: Non-committed status {ctx.Event.Status}");
            }
        }
    }
    #endregion

    #region Reducer Event Handlers (UserProfile)
    internal void OnUpdateUserProfileNameResult(ReducerEventContext ctx, string newName)
    {
        if (ctx.Event.CallerIdentity == _spacetimedbIdentity) {
            if (ctx.Event.Status is Status.Committed) {
                Debug.Log($"SpacetimeDB: Successfully updated user profile name to '{newName}'. UserProfile table update will reflect changes.");
            } else if (ctx.Event.Status is Status.Failed failedStatus) {
                LogErrorAndInvoke($"Failed to update user profile name to '{newName}': {failedStatus.ToString()}");
            } else {
                 LogErrorAndInvoke($"Failed to update user profile name to '{newName}': Non-committed status {ctx.Event.Status}");
            }
        }
    }
    #endregion

    #region Data Access (UserProfile)
    public UserData GetCachedUserProfile(string spacetimeId)
    {
        if (string.IsNullOrEmpty(spacetimeId) || _connection?.Db == null)
        {
            return null;
        }

        try
        {
            var identityToFind = new Identity(HexStringToByteArray(spacetimeId));
            var dbUser = _connection.Db.UserProfile.Iter().FirstOrDefault(p => p.Identity.Equals(identityToFind));
            return MapToUserData(dbUser);
        }
        catch (FormatException e)
        {
            Debug.LogError($"Failed to parse spacetimeId '{spacetimeId}': {e.Message}");
            return null;
        }
    }

    public IEnumerable<UserData> GetAllCachedUserProfiles()
    {
        if (_connection?.Db == null)
        {
            return Enumerable.Empty<UserData>();
        }
        return _connection.Db.UserProfile.Iter().Select(MapToUserData);
    }
    #endregion

    #region Mapping Functions (UserProfile)
     private UserData MapToUserData(SpacetimeDB.Types.UserProfile spdbProfile)
     {
         if (spdbProfile == null) return null;
         return new UserData {
             SpacetimeId = spdbProfile.Identity.ToString(),
             Name = spdbProfile.Name,
             IsOnline = spdbProfile.Online,
             CreatedAtUtc = TimestampToDateTime(spdbProfile.CreatedAt),
             LastOnlineUtc = TimestampToDateTime(spdbProfile.LastOnline)
         };
     }
     #endregion
} 