using System.Collections.Generic;
using System.Threading.Tasks;
using UniRx;
using UnityEngine;
using System;

public interface IUserProfileDataProvider
{
    IObservable<List<UserProfileData>> GetAllUserProfiles();
    IObservable<UserProfileData> GetOrCreateUserProfile(string userId);
    Task<UserProfileData> LoadUserProfileDataAsync(string userId);
    void SaveUserProfileData(string userId, UserProfileData userProfileData);
    void DeleteUserProfile(string userId);
    void ClearAllUserProfiles();
}
