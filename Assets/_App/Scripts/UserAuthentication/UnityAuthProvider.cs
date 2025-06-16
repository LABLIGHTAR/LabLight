using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.Core;
using UnityEngine;

public class UnityAuthProvider : MonoBehaviour, IAuthProvider
{
    public event Action<string> OnSignInSuccess;
    public event Action OnSignOutSuccess;
    public event Action<string> OnAuthError;
    public event Action<bool> OnInitialAuthChecked;
    public event Action<AuthStatus, string> OnAuthStatusChanged;

    public AuthStatus CurrentAuthStatus { get; private set; } = AuthStatus.Idle;

    public bool IsSignedIn => AuthenticationService.Instance.IsSignedIn;
    public string CurrentUserId => IsSignedIn ? AuthenticationService.Instance.PlayerId : string.Empty;
    
    /// <summary>
    /// Gets the current user's email. Note: Unity Authentication service does not expose user email after sign-in for security reasons.
    /// This property will only hold a value if a user has just signed in or signed up during the current session.
    /// It will be empty if the user was already signed-in when the application started.
    /// </summary>
    public string CurrentUserEmail { get; private set; }
    
    /// <summary>
    /// Gets the current user's photo URL. Unity Authentication does not support this feature.
    /// </summary>
    public Uri CurrentUserPhotoUrl => null;

    private async void Awake()
    {
        try
        {
            await UnityServices.InitializeAsync();
            
            // Subscribe to authentication events
            AuthenticationService.Instance.SignedIn += HandleSignedIn;
            AuthenticationService.Instance.SignedOut += HandleSignedOut;
            AuthenticationService.Instance.SignInFailed += HandleSignInFailed;
            AuthenticationService.Instance.Expired += HandleSessionExpired;
            
            OnInitialAuthChecked?.Invoke(IsSignedIn);
            if (IsSignedIn)
            {
                SetAuthStatus(AuthStatus.SignedIn);
            }
        }
        catch (Exception e)
        {
            SetAuthStatus(AuthStatus.Error, e.Message);
            OnAuthError?.Invoke(e.Message);
            OnInitialAuthChecked?.Invoke(false);
        }
    }
    
    private void OnDestroy()
    {
        if (AuthenticationService.Instance == null) return;
        
        // Unsubscribe from authentication events
        AuthenticationService.Instance.SignedIn -= HandleSignedIn;
        AuthenticationService.Instance.SignedOut -= HandleSignedOut;
        AuthenticationService.Instance.SignInFailed -= HandleSignInFailed;
        AuthenticationService.Instance.Expired -= HandleSessionExpired;
    }

    public async Task SignUp(string email, string password)
    {
        SetAuthStatus(AuthStatus.Authenticating);
        try
        {
            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(email, password);
            CurrentUserEmail = email; // Cache email on sign-up
            // The HandleSignedIn event will be triggered by the service, which will then call OnSignInSuccess.
        }
        catch (AuthenticationException e)
        {
            HandleSignInFailed(e);
        }
        catch (RequestFailedException e)
        {
            HandleSignInFailed(e);
        }
    }

    public async Task SignIn(string email, string password)
    {
        SetAuthStatus(AuthStatus.Authenticating);
        try
        {
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(email, password);
            CurrentUserEmail = email; // Cache email on sign-in
            // The HandleSignedIn event will be triggered by the service, which will then call OnSignInSuccess.
        }
        catch (AuthenticationException e)
        {
            HandleSignInFailed(e);
        }
        catch (RequestFailedException e)
        {
            HandleSignInFailed(e);
        }
    }

    public Task SignOut()
    {
        if (!IsSignedIn)
        {
            return Task.CompletedTask;
        }
        
        AuthenticationService.Instance.SignOut();
        // The HandleSignedOut event will be triggered by the service, which will then call OnSignOutSuccess.
        return Task.CompletedTask;
    }

    public Task<string> GetIdTokenAsync(bool forceRefresh)
    {
        if (!IsSignedIn)
        {
            return Task.FromResult<string>(null);
        }
        // Unity's AccessToken is automatically managed and refreshed by the SDK.
        // 'forceRefresh' is not directly applicable.
        return Task.FromResult(AuthenticationService.Instance.AccessToken);
    }
    
    private void HandleSignedIn()
    {
        SetAuthStatus(AuthStatus.SignedIn);
        OnSignInSuccess?.Invoke(AuthenticationService.Instance.AccessToken);
    }

    private void HandleSignedOut()
    {
        CurrentUserEmail = string.Empty;
        SetAuthStatus(AuthStatus.Idle);
        OnSignOutSuccess?.Invoke();
    }

    private void HandleSignInFailed(Exception e)
    {
        SetAuthStatus(AuthStatus.Error, e.Message);
        OnAuthError?.Invoke(e.Message);
    }

    private void HandleSessionExpired()
    {
        // This is like a sign-out, but initiated by the service.
        CurrentUserEmail = string.Empty;
        SetAuthStatus(AuthStatus.Error, "Your session has expired. Please sign in again.");
        OnAuthError?.Invoke("Session Expired");
        // Depending on desired UX, you might want to trigger a sign-out flow here.
    }

    private void SetAuthStatus(AuthStatus status, string message = "")
    {
        CurrentAuthStatus = status;
        OnAuthStatusChanged?.Invoke(status, message);
    }

    public async Task SignInWithGoogle(string idToken)
    {
        SetAuthStatus(AuthStatus.Authenticating);
        try
        {
            await AuthenticationService.Instance.SignInWithGoogleAsync(idToken);
            // The HandleSignedIn event will be triggered by the service.
        }
        catch (AuthenticationException e)
        {
            HandleSignInFailed(e);
        }
        catch (RequestFailedException e)
        {
            HandleSignInFailed(e);
        }
    }

    public async Task SignInWithApple(string idToken)
    {
        SetAuthStatus(AuthStatus.Authenticating);
        try
        {
            await AuthenticationService.Instance.SignInWithAppleAsync(idToken);
            // The HandleSignedIn event will be triggered by the service.
        }
        catch (AuthenticationException e)
        {
            HandleSignInFailed(e);
        }
        catch (RequestFailedException e)
        {
            HandleSignInFailed(e);
        }
    }
} 