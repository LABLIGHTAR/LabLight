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
    private void HandleUserProfileInsert(EventContext ctx, SpacetimeDB.Types.UserProfile spdbProfile)
    {
        // Only update the CurrentUserProfile if the incoming profile matches the currently connected identity
        if (_spacetimedbIdentity.HasValue && spdbProfile.Identity == _spacetimedbIdentity.Value) {
            CurrentUserProfile = MapToUserData(spdbProfile); // Assumes CurrentUserProfile setter is accessible or it's directly setting a field
            Debug.Log($"Local UserProfile created/updated via Insert callback for CURRENT user: {CurrentUserProfile?.Name}");
            OnUserProfileUpdated?.Invoke(CurrentUserProfile);
        } else {
             Debug.Log($"Ignoring UserProfile Insert event for non-matching identity: {spdbProfile.Identity}");
        }
    }

    private void HandleUserProfileUpdate(EventContext ctx, SpacetimeDB.Types.UserProfile oldSpdbProfile, SpacetimeDB.Types.UserProfile newSpdbProfile)
    {
        // Only update the CurrentUserProfile if the incoming profile matches the currently connected identity
         if (_spacetimedbIdentity.HasValue && newSpdbProfile.Identity == _spacetimedbIdentity.Value) {
            CurrentUserProfile = MapToUserData(newSpdbProfile); // Assumes CurrentUserProfile setter is accessible or it's directly setting a field
            Debug.Log($"Local UserProfile updated via Update callback for CURRENT user: {CurrentUserProfile?.Name}");
            OnUserProfileUpdated?.Invoke(CurrentUserProfile);
         } else {
             Debug.Log($"Ignoring UserProfile Update event for non-matching identity: {newSpdbProfile.Identity}");
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
    public UserData GetCachedUserProfile(string userId)
    {
        if (!AssertConnected("get cached user profile")) return null;
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogWarning("GetCachedUserProfile: Input userId string is null or empty.");
            return null;
        }

        var userProfileHandle = _connection?.Db?.UserProfile;
        if (userProfileHandle == null) {
             Debug.LogWarning("GetCachedUserProfile: UserProfile table handle is null.");
             return null;
        }

        SpacetimeDB.Types.UserProfile foundProfile = null; 
        foreach (var row in userProfileHandle.Iter())
        {
            if (row.Identity.ToString() == userId)
            {
                foundProfile = row;
                break;
            }
        }
        
        return MapToUserData(foundProfile); 
    }

    public IEnumerable<UserData> GetAllCachedUserProfiles()
    {
        if (!AssertConnected("get all cached user profiles"))
        {
            yield break;
        }

        var userProfileHandle = _connection?.Db?.UserProfile;
        if (userProfileHandle == null)
        {
            Debug.LogWarning("GetAllCachedUserProfiles: UserProfile table handle is null.");
            yield break;
        }

        foreach (var row in userProfileHandle.Iter())
        {
            yield return MapToUserData(row);
        }
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