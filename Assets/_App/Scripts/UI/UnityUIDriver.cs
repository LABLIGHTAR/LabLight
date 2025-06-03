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
    [SerializeField] private UserSelectionPanelViewController userSelectionPanel;
    [SerializeField] private UIDocument userSelectionToolkitPanel;
    [SerializeField] private UIDocument userRegistrationToolkitPanel;
    [SerializeField] private UIDocument userLoginToolkitPanel;
    [SerializeField] private UIDocument returningUserLoginToolkitPanel;
    [SerializeField] private UIDocument dashboardMenuToolkitPanel;
    [SerializeField] private ProtocolPanelViewController protocolPanel;
    [SerializeField] private ChecklistPanelViewController checklistPanel;
    [SerializeField] private ProtocolMenuViewController protocolMenuPanel;
    [SerializeField] private TimerViewController timerPanel;
    [SerializeField] private LLMChatPanelViewController chatPanel;
    #endregion

    #region Private Fields
    private IAuthProvider _authProvider;
    private ILLMChatProvider _llmChatProvider;
    private IUICallbackHandler _uiCallbackHandler;
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
            if (chatPanel != null) _llmChatProvider.OnResponse.AddListener(chatPanel.DisplayResponse);
            else Debug.LogError("UnityUIDriver: ChatPanel is null, cannot subscribe to LLMChatProvider.OnResponse.");
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
    }

    void OnDestroy()
    {
        if (_authProvider != null)
        {
            _authProvider.OnSignInSuccess -= HandleSignInSuccess;
            _authProvider.OnSignOutSuccess -= HandleSignOutSuccess;
        }
        if (_llmChatProvider != null && chatPanel != null)
        {
            _llmChatProvider.OnResponse.RemoveListener(chatPanel.DisplayResponse);
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
        if (checklistPanel == null || protocolMenuPanel == null) 
        {
            Debug.LogError("UnityUIDriver: ChecklistPanel or ProtocolMenuPanel is not assigned.");
            return;
        }

        if (protocol == null)
        {
            checklistPanel.gameObject.SetActive(false);
            protocolMenuPanel.gameObject.SetActive(true);
        }
        else
        {
            protocolMenuPanel.gameObject.SetActive(false);
            checklistPanel.gameObject.SetActive(true);
        }
    }

    public void OnStepChange(ProtocolState.StepState stepState)
    {
        if (protocolPanel != null) protocolPanel.UpdateContentItems();
        if (checklistPanel != null) StartCoroutine(checklistPanel.LoadChecklist());
    }

    public void OnCheckItemChange(List<ProtocolState.CheckItemState> checkItemStates)
    {
        return;
    }

    public void OnChatMessageReceived(string message)
    {
        if (chatPanel != null && chatPanel.gameObject.activeInHierarchy)
        {
            chatPanel.DisplayResponse(message);
        }
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
        if (userSelectionPanel != null && userSelectionPanel.gameObject != null) 
        {
            userSelectionPanel.gameObject.SetActive(true);
        } 
        else if (userSelectionToolkitPanel != null)
        {
             Debug.LogWarning("DisplayUserSelection called, but legacy userSelectionPanel not found or active. Defaulting to userSelectionToolkitPanel. Consider using DisplayUserSelectionMenu directly.");
            userSelectionToolkitPanel.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError("UnityUIDriver: Neither userSelectionPanel nor userSelectionToolkitPanel is assigned for DisplayUserSelection.");
        }
    }

    public void DisplayUserSelectionMenu()
    {
        HideAllPanels();
        if (userSelectionToolkitPanel != null)
        {
            userSelectionToolkitPanel.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError("UnityUIDriver: userSelectionToolkitPanel (UIDocument) is not assigned for DisplayUserSelectionMenu.");
        }
    }

    public void DisplayReturningUserLogin(LocalUserProfileData userProfile)
    {
        HideAllPanels();
        if (returningUserLoginToolkitPanel != null)
        {
            returningUserLoginToolkitPanel.gameObject.SetActive(true);
            var controller = returningUserLoginToolkitPanel.GetComponent<ReturningUserLoginMenuController>();
            if (controller != null)
            {
                controller.SetUserProfile(userProfile);
            }
            else
            {
                Debug.LogError("UnityUIDriver: ReturningUserLoginMenuController not found on returningUserLoginToolkitPanel.");
            }
        }
        else
        {
            Debug.LogError("UnityUIDriver: returningUserLoginToolkitPanel (UIDocument) is not assigned.");
        }
    }

    public void DisplayUserRegistration()
    {
        HideAllPanels();
        if (userRegistrationToolkitPanel != null)
        {
            userRegistrationToolkitPanel.gameObject.SetActive(true);
            var controller = userRegistrationToolkitPanel.GetComponent<UserRegistrationMenuController>();
            controller?.ClearForm(); 
        }
        else
        {
            Debug.LogError("UnityUIDriver: userRegistrationToolkitPanel (UIDocument) is not assigned.");
        }
    }

    public void DisplayUserLogin()
    {
        HideAllPanels();
        if (userLoginToolkitPanel != null)
        {
            userLoginToolkitPanel.gameObject.SetActive(true);
            var controller = userLoginToolkitPanel.GetComponent<UserLoginMenuController>();
            controller?.ClearForm();
        }
        else
        {
            Debug.LogError("UnityUIDriver: userLoginToolkitPanel (UIDocument) is not assigned.");
        }
    }

    public void DisplayDashboard()
    {
        HideAllPanels();
        if (dashboardMenuToolkitPanel != null)
        {
            dashboardMenuToolkitPanel.gameObject.SetActive(true);
            var controller = dashboardMenuToolkitPanel.GetComponent<DashboardMenuController>();
            controller?.OnDisplay();
        }
        else
        {
            Debug.LogError("UnityUIDriver: dashboardMenuToolkitPanel (UIDocument) is not assigned.");
        }
    }

    public void DisplayProtocolMenu()
    {
        if (protocolMenuPanel == null) { Debug.LogError("UnityUIDriver: ProtocolMenuPanel is null."); return; }
        Debug.Log("UnityUIDriver: Displaying protocol menu");
        HideAllPanels();
        protocolMenuPanel.gameObject.SetActive(true);
    }

    public void DisplayTimer(int seconds)
    {
        if (timerPanel == null) { Debug.LogError("UnityUIDriver: TimerPanel is null."); return; }
        timerPanel.gameObject.SetActive(true);
        timerPanel.SetTimer(seconds);
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
        if (chatPanel == null) { Debug.LogError("UnityUIDriver: ChatPanel is null."); return; }
        chatPanel.gameObject.SetActive(true);
    }

    public void DisplayVideoPlayer(string url)
    {
        Debug.LogWarning($"UnityUIDriver: DisplayVideoPlayer for {url} is not yet implemented.");
    }

    public void DisplayPDFReader(string url)
    {
        Debug.LogWarning($"UnityUIDriver: DisplayPDFReader for {url} is not yet implemented.");
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
            Debug.Log($"UnityUIDriver: User {userID} selected via handler. Displaying protocol menu.");
            DisplayProtocolMenu();
            if (userSelectionPanel != null) userSelectionPanel.gameObject.SetActive(false);
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

        if (checklistPanel != null) checklistPanel.gameObject.SetActive(false);
        Debug.Log("UnityUIDriver: CloseProtocolCallback - UI specific actions post-handler.");
        
        SceneLoader.Instance.LoadSceneClean("ProtocolMenu"); 
        if (protocolMenuPanel != null) protocolMenuPanel.gameObject.SetActive(true);
    }

    public void ChatMessageCallback(string message)
    {
        if (_uiCallbackHandler == null) { Debug.LogError("UnityUIDriver: _uiCallbackHandler is null in ChatMessageCallback."); return; }
        _uiCallbackHandler.HandleChatMessage(message);
    }

    public async void LoginCallback(string username, string password)
    {
        if (_uiCallbackHandler == null) 
        {
             Debug.LogError("UnityUIDriver: _uiCallbackHandler is null in LoginCallback."); 
             var controller = userLoginToolkitPanel?.GetComponent<UserLoginMenuController>();
             controller?.DisplayLoginError("Internal error: UI Callback Handler not found.");
             return;
        }
        try
        {
            await _uiCallbackHandler.HandleLogin(username, password);
            // Successful login is handled by AuthStateChanged -> HandleSignInSuccess -> DisplayProtocolMenu
        }
        catch (Exception ex)
        {
            Debug.LogError($"UnityUIDriver: Login attempt via handler failed for {username}. Error: {ex.Message}");
            var controller = userLoginToolkitPanel?.GetComponent<UserLoginMenuController>();
            controller?.DisplayLoginError(ex.Message);
        }
    }

    public async void CreateUserCallback(string userName)
    {
        if (_uiCallbackHandler == null)
        {
            Debug.LogError("UnityUIDriver: IUICallbackHandler is not available for CreateUserCallback.");
            return;
        }
        Debug.Log($"UnityUIDriver: CreateUserCallback received for {userName}. Attempting to handle.");
        try
        {
            List<LocalUserProfileData> updatedProfiles = await _uiCallbackHandler.HandleCreateUser(userName);
            // Assuming userSelectionToolkitPanel is the correct panel to update.
            // It might be better to have a more generic way to signal UI refresh for user lists if multiple panels show them.
            var userSelectionController = userSelectionToolkitPanel?.GetComponent<UserSelectionMenuController>();
            if (userSelectionController != null)
            {
                userSelectionController.UpdateUserList(updatedProfiles);
                DisplayUserSelection(); // Or potentially stay on a screen that shows the new user
            }
            else
            {
                Debug.LogWarning("UnityUIDriver: UserSelectionMenuController not found after creating user. UI might not reflect the new user immediately.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"UnityUIDriver: Error during CreateUserCallback for {userName}: {ex.Message}");
            // Optionally, show an error message on the UI
            var userSelectionController = userSelectionToolkitPanel?.GetComponent<UserSelectionMenuController>();
            userSelectionController?.DisplayError($"Failed to create user: {ex.Message}");
        }
    }

    public async void AuthRegistrationCallback(string displayName, string email, string password)
    {
        if (_uiCallbackHandler == null)
        {
            Debug.LogError("UnityUIDriver: IUICallbackHandler is not available for AuthRegistrationCallback.");
            return;
        }
        Debug.Log($"UnityUIDriver: AuthRegistrationCallback received for {email}. Attempting to handle.");
        try
        {
            await _uiCallbackHandler.HandleAuthRegistration(displayName, email, password);
            // After HandleAuthRegistration initiates Firebase sign-up, OnSignInSuccess (handled by HandleSignInSuccess in this class)
            // will eventually be triggered if Firebase sign-up & sign-in are successful. HandleSignInSuccess will then call DisplayDashboard.
            // If HandleAuthRegistration throws an exception (e.g., Firebase error), it will be caught below.
            Debug.Log($"UnityUIDriver: AuthRegistrationCallback for {email} handled by UICallbackHandler. Waiting for sign-in confirmation.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"UnityUIDriver: Error during AuthRegistrationCallback for {email}: {ex.Message}");
            var registrationController = userRegistrationToolkitPanel?.GetComponent<UserRegistrationMenuController>();
            registrationController?.DisplayRegistrationError($"Registration failed: {ex.Message}");
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
        if (userSelectionPanel != null && userSelectionPanel.gameObject != null) userSelectionPanel.gameObject.SetActive(false);
        if (userSelectionToolkitPanel != null) userSelectionToolkitPanel.gameObject.SetActive(false);
        if (userRegistrationToolkitPanel != null) userRegistrationToolkitPanel.gameObject.SetActive(false);
        if (userLoginToolkitPanel != null) userLoginToolkitPanel.gameObject.SetActive(false);
        if (returningUserLoginToolkitPanel != null) returningUserLoginToolkitPanel.gameObject.SetActive(false);
        if (dashboardMenuToolkitPanel != null) dashboardMenuToolkitPanel.gameObject.SetActive(false);
        if (protocolPanel != null && protocolPanel.gameObject != null) protocolPanel.gameObject.SetActive(false);
        if (checklistPanel != null && checklistPanel.gameObject != null) checklistPanel.gameObject.SetActive(false);
        if (protocolMenuPanel != null && protocolMenuPanel.gameObject != null) protocolMenuPanel.gameObject.SetActive(false);
        if (timerPanel != null && timerPanel.gameObject != null) timerPanel.gameObject.SetActive(false);
        if (chatPanel != null && chatPanel.gameObject != null) chatPanel.gameObject.SetActive(false);
    }
    #endregion
}
