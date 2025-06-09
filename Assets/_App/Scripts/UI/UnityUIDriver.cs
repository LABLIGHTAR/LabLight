using System;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine.UIElements;

/// <summary>
/// Unity-specific implementation of the IUIDriver interface.
/// Manages Unity UI panels and delegates callback logic to IUICallbackHandler.
/// </summary>
public class UnityUIDriver : MonoBehaviour, IUIDriver
{
    #region Serialized Fields (UI Panel References)
    [SerializeField] private UIDocument userLoginWindow;
    [SerializeField] private UIDocument dashboardWindow;
    [SerializeField] private UIDocument protocolWindow;
    #endregion

    #region Private Fields
    private IAuthProvider _authProvider;
    private ILLMChatProvider _llmChatProvider;
    private IUICallbackHandler _uiCallbackHandler;
    private UserLoginWindowController _userLoginController;
    private ProtocolWindowController _protocolWindowController;
    #endregion

    #region Unity Lifecycle Methods (Initialization & Cleanup)
    public void Initialize()
    {
        _authProvider = ServiceRegistry.GetService<IAuthProvider>();
        if (_authProvider != null)
        {
            _authProvider.OnSignInSuccess += HandleSignInSuccess;
            _authProvider.OnSignOutSuccess += HandleSignOutSuccess;
        }
        else
        {
            Debug.LogError("UnityUIDriver: IAuthProvider is not available from ServiceRegistry.");
        }

        _llmChatProvider = ServiceRegistry.GetService<ILLMChatProvider>();
        if (_llmChatProvider != null)
        {
            // if (chatPanel != null) _llmChatProvider.OnResponse.AddListener(chatPanel.DisplayResponse);
            // else Debug.LogError("UnityUIDriver: ChatPanel is null, cannot subscribe to LLMChatProvider.OnResponse.");
        }
        else
        {
            Debug.LogError("UnityUIDriver: ILLMChatProvider is not available from ServiceRegistry.");
        }

        _uiCallbackHandler = ServiceRegistry.GetService<IUICallbackHandler>();
        if (_uiCallbackHandler == null)
        {
            Debug.LogError("UnityUIDriver: IUICallbackHandler is not available from ServiceRegistry.");
        }

        if (ProtocolState.Instance != null)
        {
            ProtocolState.Instance.StepStream.Subscribe(OnStepChange).AddTo(this);
            ProtocolState.Instance.ProtocolStream.Subscribe(OnProtocolChange).AddTo(this);
        }
        else
        {
            Debug.LogError("UnityUIDriver: ProtocolState.Instance is null. Cannot subscribe to streams.");
        }

        if (userLoginWindow != null)
        {
            _userLoginController = userLoginWindow.GetComponent<UserLoginWindowController>();
        }
        if (protocolWindow != null)
        {
            _protocolWindowController = protocolWindow.GetComponent<ProtocolWindowController>();
        }
    }

    void OnDestroy()
    {
        if (_authProvider != null)
        {
            _authProvider.OnSignInSuccess -= HandleSignInSuccess;
            _authProvider.OnSignOutSuccess -= HandleSignOutSuccess;
        }
        if (_llmChatProvider != null /*&& chatPanel != null*/)
        {
            // _llmChatProvider.OnResponse.RemoveListener(chatPanel.DisplayResponse);
        }
    }
    #endregion

    #region Auth Event Handlers (Local UI Reactions)
    private void HandleSignInSuccess(string oidcToken)
    {
        DisplayDashboard();
    }

    private void HandleSignOutSuccess()
    {
        DisplayUserSelectionMenu();
    }
    #endregion

    #region UI Update Methods (Implementing IUIDriver)
    public void OnProtocolChange(ProtocolDefinition protocol)
    {
        if (protocol == null)
        {
            DisplayDashboard();
        }
        else
        {
            DisplayProtocolView();
        }
    }

    public void OnStepChange(ProtocolState.StepState stepState)
    {
        if (protocolWindow != null && protocolWindow.gameObject.activeSelf)
        {
            // ProtocolView should be reacting to ProtocolState changes internally.
            // No direct call needed here unless a specific refresh is required.
        }
        return;
    }

    public void OnCheckItemChange(List<ProtocolState.CheckItemState> checkItemStates)
    {
        return;
    }

    public void OnChatMessageReceived(string message)
    {
        Debug.LogWarning("OnChatMessageReceived is not implemented in the new UI.");
    }

    public void SendAuthStatus(bool isAuthenticated)
    {
        return;
    }
    #endregion

    #region UI Display Methods (Implementing IUIDriver)
    public void DisplayUserSelection()
    {
        HideAllPanels();
        if (userLoginWindow != null)
        {
             Debug.LogWarning("DisplayUserSelection called, but legacy userSelectionPanel not found. Defaulting to userLoginWindow. Consider using DisplayUserSelectionMenu directly.");
            userLoginWindow.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError("UnityUIDriver: userLoginWindow is not assigned for DisplayUserSelection.");
        }
    }

    public void DisplayUserSelectionMenu()
    {
        HideAllPanels();
        if (userLoginWindow != null)
        {
            userLoginWindow.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError("UnityUIDriver: userLoginToolkitPanel (UIDocument) is not assigned for DisplayUserSelectionMenu.");
        }
    }

    public void DisplayReturningUserLogin(LocalUserProfileData userProfile)
    {
        HideAllPanels();
        if (userLoginWindow != null)
        {
            userLoginWindow.gameObject.SetActive(true);
            // The UserLoginController will handle showing the correct view internally.
            // We just need to ensure the main panel is active.
        }
        else
        {
            Debug.LogError("UnityUIDriver: userLoginToolkitPanel (UIDocument) is not assigned.");
        }
    }

    public void DisplayUserRegistration()
    {
        HideAllPanels();
        if (userLoginWindow != null)
        {
            userLoginWindow.gameObject.SetActive(true);
            // The UserLoginController will handle showing the correct view internally.
        }
        else
        {
            Debug.LogError("UnityUIDriver: userLoginToolkitPanel (UIDocument) is not assigned.");
        }
    }

    public void DisplayUserLogin()
    {
        HideAllPanels();
        if (userLoginWindow != null)
        {
            userLoginWindow.gameObject.SetActive(true);
            // The UserLoginController will handle showing the correct view internally.
        }
        else
        {
            Debug.LogError("UnityUIDriver: userLoginToolkitPanel (UIDocument) is not assigned.");
        }
    }

    public void DisplayDashboard()
    {
        HideAllPanels();
        if (dashboardWindow != null)
        {
            dashboardWindow.gameObject.SetActive(true);
            var controller = dashboardWindow.GetComponent<DashboardWindowController>();
            // The new controller's logic is in OnEnable, so no OnDisplay call is needed.
        }
        else
        {
            Debug.LogError("UnityUIDriver: dashboardWindow (UIDocument) is not assigned.");
        }
    }

    public void DisplayProtocolMenu()
    {
        Debug.Log("UnityUIDriver: Displaying protocol menu by showing dashboard.");
        DisplayDashboard();
    }

    public void DisplayTimer(int seconds)
    {
        Debug.LogWarning("DisplayTimer is not implemented in the new UI.");
    }

    public void DisplayCalculator()
    {
        Debug.LogWarning("UnityUIDriver: DisplayCalculator is not yet implemented.");
    }

    public void DisplayWebPage(string url)
    {
        Debug.LogWarning($"UnityUIDriver: DisplayWebPage for {url} is not yet implemented.");
    }

    public void DisplayLLMChat()
    {
        Debug.LogWarning("DisplayLLMChat is not implemented in the new UI.");
    }

    public void DisplayVideoPlayer(string url)
    {
        Debug.LogWarning($"UnityUIDriver: DisplayVideoPlayer for {url} is not yet implemented.");
    }

    public void DisplayPDFReader(string url)
    {
        Debug.Log("DisplayPDFReader called, but no implementation is available on this platform.");
    }

    public void DisplayProtocolView()
    {
        HideAllPanels();
        if (protocolWindow != null)
        {
            protocolWindow.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError("UnityUIDriver: protocolViewToolkitPanel (UIDocument) is not assigned.");
        }
    }
    #endregion

    #region Callback Methods (Implementing IUIDriver, Delegating to Handler)
    public async void UserSelectionCallback(string userID)
    {
        if (_uiCallbackHandler == null) { Debug.LogError("UnityUIDriver: _uiCallbackHandler is null in UserSelectionCallback."); return; }

        if (string.IsNullOrEmpty(userID))
        {
            Debug.LogWarning("UnityUIDriver: UserSelectionCallback received null or empty userID.");
            await _uiCallbackHandler.HandleUserSelection(userID);
            return;
        }

        try
        {
            await _uiCallbackHandler.HandleUserSelection(userID);
            Debug.Log($"UnityUIDriver: User {userID} selected via handler. Displaying dashboard.");
            DisplayDashboard();
        }
        catch (Exception ex)
        {
            Debug.LogError($"UnityUIDriver: Error during user selection for {userID} via handler: {ex.Message}");
        }
    }

    public void StepNavigationCallback(int index)
    {
        if (_uiCallbackHandler == null) { Debug.LogError("UnityUIDriver: _uiCallbackHandler is null in StepNavigationCallback."); return; }
        _uiCallbackHandler.HandleStepNavigation(index);
    }

    public void CheckItemCallback(int index)
    {
        if (_uiCallbackHandler == null) { Debug.LogError("UnityUIDriver: _uiCallbackHandler is null in CheckItemCallback."); return; }
        _uiCallbackHandler.HandleCheckItem(index);
    }

    public void UncheckItemCallback(int index)
    {
        if (_uiCallbackHandler == null) { Debug.LogError("UnityUIDriver: _uiCallbackHandler is null in UncheckItemCallback."); return; }
        _uiCallbackHandler.HandleUncheckItem(index);
    }

    public void SignOffChecklistCallback()
    {
        if (_uiCallbackHandler == null) { Debug.LogError("UnityUIDriver: _uiCallbackHandler is null in SignOffChecklistCallback."); return; }
        _uiCallbackHandler.HandleSignOffChecklist();
    }

    public void ProtocolSelectionCallback(string protocolDefinitionJson)
    {
        if (_uiCallbackHandler == null) { Debug.LogError("UnityUIDriver: _uiCallbackHandler is null in ProtocolSelectionCallback."); return; }
        _uiCallbackHandler.HandleProtocolSelection(protocolDefinitionJson);
    }

    public void CloseProtocolCallback()
    {
        if (_uiCallbackHandler == null) { Debug.LogError("UnityUIDriver: _uiCallbackHandler is null in CloseProtocolCallback."); return; }
        _uiCallbackHandler.HandleCloseProtocol();

        Debug.Log("UnityUIDriver: CloseProtocolCallback - UI specific actions post-handler. Returning to dashboard.");
        
        DisplayDashboard();
    }

    public void ChatMessageCallback(string message)
    {
        if (_uiCallbackHandler == null) { Debug.LogError("UnityUIDriver: _uiCallbackHandler is null in ChatMessageCallback."); return; }
        _uiCallbackHandler.HandleChatMessage(message);
    }

    public async void LoginCallback(string username, string password)
    {
        try
        {
            await _uiCallbackHandler.HandleLogin(username, password);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Login failed: {ex.Message}");
            if (_userLoginController != null)
            {
                _userLoginController.DisplayLoginError(ex.Message);
            }
        }
    }

    public async void CreateUserCallback(string userName)
    {
        // This method seems to be related to the legacy user selection panel.
        // It might need to be adapted or removed depending on the new flow.
        Debug.LogWarning("CreateUserCallback is not fully integrated with the new UI flow yet.");
        try
        {
            var updatedProfiles = await _uiCallbackHandler.HandleCreateUser(userName);
            if (_userLoginController != null)
            {
                // This part needs a method on UserLoginController to update its UserSelectionView
                // For now, we assume such a method exists or will be added.
                // _userLoginController.UpdateUserProfiles(updatedProfiles);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Create user failed: {ex.Message}");
            if (_userLoginController != null)
            {
                // A method like `_userLoginController.DisplayError(ex.Message)` would be ideal here,
                // routed to the currently active view.
                 _userLoginController.DisplayLoginError(ex.Message); // Re-using login error display for now
            }
        }
    }

    public async void AuthRegistrationCallback(string displayName, string email, string password)
    {
        try
        {
            await _uiCallbackHandler.HandleAuthRegistration(displayName, email, password);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Registration failed: {ex.Message}");
            if (_userLoginController != null)
            {
                _userLoginController.DisplayRegistrationError(ex.Message);
            }
        }
    }

    public void RequestSignOut() // Implementation of RequestSignOut
    {
        Debug.Log("UnityUIDriver: RequestSignOut called. Attempting to sign out via SessionManager.");
        SessionManager.instance?.SignOut(); 
    }
    #endregion

    #region Helper Methods
    private void HideAllPanels()
    {
        if (userLoginWindow != null) userLoginWindow.gameObject.SetActive(false);
        if (dashboardWindow != null) dashboardWindow.gameObject.SetActive(false);
        if (protocolWindow != null) protocolWindow.gameObject.SetActive(false);
    }
    #endregion
}
