using System;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using System.Linq;
using Newtonsoft.Json;

public class UnityUIDriver : MonoBehaviour, IUIDriver
{
    // References to UI panels/views
    [SerializeField] private UserSelectionPanelViewController userSelectionPanel;
    [SerializeField] private ProtocolPanelViewController protocolPanel;
    [SerializeField] private ChecklistPanelViewController checklistPanel;
    [SerializeField] private ProtocolMenuViewController protocolMenuPanel;
    [SerializeField] private TimerViewController timerPanel;
    [SerializeField] private LLMChatPanelViewController chatPanel;

    private IAuthProvider authProvider;
    private ILLMChatProvider llmChatProvider;

    public void Initialize()
    {
        authProvider = ServiceRegistry.GetService<IAuthProvider>();
        if (authProvider != null)
        {
            authProvider.OnSignInSuccess += (_) => DisplayProtocolMenu();
            authProvider.OnSignOutSuccess += () => DisplayUserSelection();
        }
        else
        {
            Debug.LogError("IAuthProvider is not available");
        }

        llmChatProvider = ServiceRegistry.GetService<ILLMChatProvider>();
        if (llmChatProvider != null)
        {   
            llmChatProvider.OnResponse.AddListener(chatPanel.DisplayResponse);
        }
        else
        {
            Debug.LogError("ILLMChatProvider is not available");
        }

        ProtocolState.Instance.StepStream.Subscribe(stepState => OnStepChange(stepState)).AddTo(this);
        ProtocolState.Instance.ProtocolStream.Subscribe(protocol => OnProtocolChange(protocol)).AddTo(this);
    }

    void OnDestroy()
    {
        if (protocolPanel != null) Destroy(protocolPanel.gameObject);
        if (checklistPanel != null) Destroy(checklistPanel.gameObject);
        if (protocolMenuPanel != null) Destroy(protocolMenuPanel.gameObject);
        if (timerPanel != null) Destroy(timerPanel.gameObject);
        if (chatPanel != null) Destroy(chatPanel.gameObject);
    }

    // UI Update methods
    public void OnProtocolChange(ProtocolDefinition protocol)
    {
        if (protocol == null)
        {
            checklistPanel.gameObject.SetActive(false);
            protocolMenuPanel.gameObject.SetActive(true);
            return;
        }
        protocolMenuPanel.gameObject.SetActive(false);
        checklistPanel.gameObject.SetActive(true);
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

    public void DisplayUserSelection()
    {
        userSelectionPanel.gameObject.SetActive(true);
    }

    // UI Display methods
    public void DisplayProtocolMenu()
    {
        if (protocolMenuPanel != null)
        {
            Debug.Log("Displaying protocol menu");
            protocolMenuPanel.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError("Protocol menu panel is null");
        }
    }

    public void DisplayTimer(int seconds)
    {
        timerPanel.gameObject.SetActive(true);
        timerPanel.SetTimer(seconds);
    }

    //TODO: implement Unity calculator
    public void DisplayCalculator()
    {
        return;
        //calculatorPanel.gameObject.SetActive(true);
    }

    //TODO: implement Unity web browser
    public void DisplayWebPage(string url)
    {
        Debug.Log("Displaying web page at " + url);
        return;
        //webBrowserPanel.gameObject.SetActive(true);
        //webBrowserPanel.LoadUrl(url);
    }

    public void DisplayLLMChat()
    {
        chatPanel.gameObject.SetActive(true);
    }

    //TODO: implement Unity video player
    public void DisplayVideoPlayer(string url)
    {
        Debug.Log("Displaying video at " + url);
        return;
        //videoPlayerPanel.gameObject.SetActive(true);
        //videoPlayerPanel.LoadVideo(url);
    }

    //TODO: implement Unity PDF reader
    public void DisplayPDFReader(string url)
    {
        return;
        //pdfReaderPanel.gameObject.SetActive(true);
        //pdfReaderPanel.LoadPDF(url);
    }

    // Unity Callback Methods
    public async void UserSelectionCallback(string userID)
    {
        if (string.IsNullOrEmpty(userID))
        {
            Debug.LogWarning("UnityUIDriver: UserSelectionCallback received null or empty userID. Possible cancel action.");
            // Optionally, navigate to a main menu or specific view if selection is cancelled.
            // sessionManager?.SignOut(); // Or handle cancellation as appropriate
            // DisplayUserSelection(); // Or another appropriate view
            return;
        }

        if (SessionManager.instance == null)
        {
            Debug.LogError("UnityUIDriver: SessionManager instance is null. Cannot process user selection.");
            return;
        }

        bool selectionSuccess = await SessionManager.instance.SelectLocalUserAsync(userID);

        if (selectionSuccess)
        {
            // Successfully set the local user context in SessionManager.
            // Now, determine the next step. Usually, this means displaying the protocol menu or user view.
            Debug.Log($"UnityUIDriver: User {userID} selected. Displaying protocol menu.");
            DisplayProtocolMenu(); 
            // If this selection should also trigger an online sign-in and DB connection:
            // This is where you might call SessionManager.instance.AttemptSignIn(email, password) if you have credentials,
            // or SessionManager.instance.ConnectToDatabaseIfAuthenticated() (a new method you might add to SessionManager
            // that checks if _firebaseUserId is set and tries to get an OIDC token and connect).
        }
        else
        {    
            Debug.LogError($"UnityUIDriver: Failed to select user {userID} via SessionManager.");
            // Optionally, show an error to the user or return to user selection.
            // userSelectionPanel.gameObject.SetActive(true); // Re-display if selection failed
        }
    }

    public void StepNavigationCallback(int index)
    {
        if(index < 0 || index >= ProtocolState.Instance.Steps.Count)
        {
            return;
        }
        Debug.Log("Navigating to step " + index);
        ProtocolState.Instance.SetStep(index);
    }

    public void CheckItemCallback(int index)
    {
        var currentStepState = ProtocolState.Instance.CurrentStepState.Value;
        if (currentStepState == null || currentStepState.Checklist == null)
        {
            return;
        }

        if (index < 0 || index >= currentStepState.Checklist.Count)
        {
            return;
        }

        currentStepState.Checklist[index].IsChecked.Value = true;
        currentStepState.Checklist[index].CompletionTime.Value = DateTime.Now;
        
        // Find next unchecked item
        var nextUncheckedItem = currentStepState.Checklist
            .Skip(index + 1)
            .FirstOrDefault(item => !item.IsChecked.Value);

        if (nextUncheckedItem != null)
        {
            ProtocolState.Instance.SetCheckItem(currentStepState.Checklist.IndexOf(nextUncheckedItem));
        }
        else
        {
            // If no more unchecked items, stay on the last checked item
            ProtocolState.Instance.SetCheckItem(currentStepState.Checklist.Count - 1);
        }
    }

    public void UncheckItemCallback(int index)
    {
        ProtocolState.Instance.CurrentStepState.Value.Checklist[index].IsChecked.Value = false;
        if(index - 1 >= 0)
        {
            ProtocolState.Instance.SetCheckItem(index - 1);
        }
        else
        {
            ProtocolState.Instance.SetCheckItem(index);
        }
    }

    public void SignOffChecklistCallback()
    {
        ProtocolState.Instance.SignOff();
    }

    public void ProtocolSelectionCallback(string protocolDefinitionJson)
    {
        var protocolDefinition = JsonConvert.DeserializeObject<ProtocolDefinition>(protocolDefinitionJson);
        ProtocolState.Instance.SetProtocolDefinition(protocolDefinition);
    }

    public void ChecklistSignOffCallback(bool isSignedOff)
    {
        if (ProtocolState.Instance.CurrentStepState.Value != null)
        {
            ProtocolState.Instance.CurrentStepState.Value.SignedOff.Value = isSignedOff;
        }
    }

    public void CloseProtocolCallback()
    {
        checklistPanel.gameObject.SetActive(false);
        Debug.Log("######LABLIGHT UNITYUIDRIVER CloseProtocolCallback");

        SpeechRecognizer.Instance.ClearAllKeywords();

        ProtocolState.Instance.ActiveProtocol.Value = null;
        SceneLoader.Instance.LoadSceneClean("ProtocolMenu");
        protocolMenuPanel.gameObject.SetActive(true);  
    }

    public void ChatMessageCallback(string message)
    {
        if (llmChatProvider != null)
        {
            llmChatProvider.QueryAsync(message);
        }
        else
        {
            Debug.LogError("ILLMChatProvider is not available");
        }
    }

    public void LoginCallback(string username, string password)
    {
        if (authProvider != null)
        {
            authProvider.SignIn(username, password);
        }
        else
        {
            Debug.LogError("IAuthProvider is not available");
        }
    }

    // Helper methods
    private void HideAllPanels()
    {
        if (protocolPanel != null) protocolPanel.gameObject.SetActive(false);
        if (checklistPanel != null) checklistPanel.gameObject.SetActive(false);
        if (protocolMenuPanel != null) protocolMenuPanel.gameObject.SetActive(false);
        if (timerPanel != null) timerPanel.gameObject.SetActive(false);
        //if (calculatorPanel != null) calculatorPanel.gameObject.SetActive(false);
        //if (webBrowserPanel != null) webBrowserPanel.gameObject.SetActive(false);
        if (chatPanel != null) chatPanel.gameObject.SetActive(false);
        //if (videoPlayerPanel != null) videoPlayerPanel.gameObject.SetActive(false);
        //if (pdfReaderPanel != null) pdfReaderPanel.gameObject.SetActive(false);
    }
}
