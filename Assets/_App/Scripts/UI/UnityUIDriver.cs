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
        DisplayProtocolMenu();
    }

    private void HandleSignOutSuccess()
    {
        DisplayUserSelection();
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
        if (userSelectionToolkitPanel != null)
        {
            userSelectionToolkitPanel.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError("UnityUIDriver: userSelectionToolkitPanel (UIDocument) is not assigned.");
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
        if (_uiCallbackHandler == null) { Debug.LogError("UnityUIDriver: _uiCallbackHandler is null in CreateUserCallback."); return; }
        if (string.IsNullOrWhiteSpace(userName)) { Debug.LogWarning("UnityUIDriver: CreateUserCallback received null or empty userName."); return; }

        Debug.Log($"UnityUIDriver: Attempting to create user: {userName}");
        try
        {
            List<LocalUserProfileData> updatedProfiles = await _uiCallbackHandler.HandleCreateUser(userName);
            Debug.Log($"UnityUIDriver: User {userName} created successfully. {updatedProfiles.Count} total profiles.");
            if (userSelectionPanel != null && userSelectionPanel.gameObject.activeInHierarchy)
            {
                Debug.Log("UnityUIDriver: User creation successful, UserSelectionPanel might need refresh.");
            }
            if (userSelectionToolkitPanel != null) userSelectionToolkitPanel.gameObject.SetActive(false);
            if (userRegistrationToolkitPanel != null) userRegistrationToolkitPanel.gameObject.SetActive(false);
            if (userLoginToolkitPanel != null) userLoginToolkitPanel.gameObject.SetActive(false);
            if (protocolPanel != null) protocolPanel.gameObject.SetActive(false);
        }
        catch (Exception ex)
        {
            Debug.LogError($"UnityUIDriver: Error creating user {userName} via handler: {ex.Message}");
        }
    }

    public async void AuthRegistrationCallback(string displayName, string email, string password)
    {
        if (_uiCallbackHandler == null) 
        { 
            Debug.LogError("UnityUIDriver: _uiCallbackHandler is null in AuthRegistrationCallback."); 
            // Optionally, provide feedback to the registration screen
            var controller = userRegistrationToolkitPanel?.GetComponent<UserRegistrationMenuController>();
            controller?.DisplayRegistrationError("Internal error: UI Callback Handler not found.");
            return; 
        }
        try
        {
            await _uiCallbackHandler.HandleAuthRegistration(displayName, email, password);
            // If HandleAuthRegistration is successful, AuthStateChanged in FirebaseAuthProvider
            // should trigger OnSignInSuccess, which in turn calls HandleSignInSuccess here.
            // HandleSignInSuccess already calls DisplayProtocolMenu.
            // So, no explicit navigation here is needed if all goes well.
            Debug.Log($"UnityUIDriver: AuthRegistrationCallback for {email} handled by UICallbackHandler. Waiting for sign-in confirmation.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"UnityUIDriver: Error during auth registration for {email} via handler: {ex.Message}");
            // Display the error on the registration screen
            var controller = userRegistrationToolkitPanel?.GetComponent<UserRegistrationMenuController>();
            controller?.DisplayRegistrationError(ex.Message); 
        }
    }
    #endregion

    #region Helper Methods
    private void HideAllPanels()
    {
        if (userSelectionPanel != null) userSelectionPanel.gameObject.SetActive(false);
        if (userSelectionToolkitPanel != null) userSelectionToolkitPanel.gameObject.SetActive(false);
        if (userRegistrationToolkitPanel != null) userRegistrationToolkitPanel.gameObject.SetActive(false);
        if (userLoginToolkitPanel != null) userLoginToolkitPanel.gameObject.SetActive(false);
        if (protocolPanel != null) protocolPanel.gameObject.SetActive(false);
        if (checklistPanel != null) checklistPanel.gameObject.SetActive(false);
        if (protocolMenuPanel != null) protocolMenuPanel.gameObject.SetActive(false);
    }
    #endregion
}
