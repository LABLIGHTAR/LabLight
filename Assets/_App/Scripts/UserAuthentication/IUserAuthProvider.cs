using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public interface IUserAuthProvider
{
    public Task<bool> TryAuthenticateUser(string username, string password);
    public bool IsAuthenticated();
    public string TryGetIdToken();
    public string TryGetAccessToken();
    public string TryGetRefreshToken();
}