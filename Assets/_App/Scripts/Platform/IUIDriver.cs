using UnityEngine;
using System;
using System.Collections.Generic;
public interface IUIDriver
{
    void Initialize();

    //UI Update methods
    void OnProtocolChange(ProtocolDefinition protocol);
    void OnStepChange(ProtocolState.StepState stepState);
    void OnCheckItemChange(List<ProtocolState.CheckItemState> checkItemStates);
    void OnChatMessageReceived(string message);
    void SendAuthStatus(bool isAuthenticated);

    //UI Display methods 
    void DisplayUserSelection();
    void DisplayProtocolMenu();
    void DisplayTimer(int seconds);
    void DisplayCalculator();
    void DisplayWebPage(string url);
    void DisplayLLMChat();
    void DisplayVideoPlayer(string url);
    void DisplayPDFReader(string url);

    //Unity Callback Methods
    void UserSelectionCallback(string username);
    void StepNavigationCallback(int index);
    void CheckItemCallback(int index);
    void UncheckItemCallback(int index);
    void SignOffChecklistCallback();
    void ProtocolSelectionCallback(string protocolJson);
    void CloseProtocolCallback();
    void ChatMessageCallback(string message);
    void LoginCallback(string username, string password);
}
