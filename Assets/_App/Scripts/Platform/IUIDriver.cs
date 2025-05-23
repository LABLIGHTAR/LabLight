using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Defines the contract for a UI Driver, which is responsible for
/// managing platform-specific UI display and forwarding input events (callbacks).
/// </summary>
public interface IUIDriver
{
    void Initialize();

    #region UI Update Methods (Platform-Specific Reactions to State Changes)
    void OnProtocolChange(ProtocolDefinition protocol);
    void OnStepChange(ProtocolState.StepState stepState);
    void OnCheckItemChange(List<ProtocolState.CheckItemState> checkItemStates);
    void OnChatMessageReceived(string message);
    void SendAuthStatus(bool isAuthenticated); // Specific to SwiftUI to update its auth UI
    #endregion

    #region UI Display Methods (Platform-Specific Commands to Show UI)
    void DisplayUserSelection();
    void DisplayProtocolMenu();
    void DisplayTimer(int seconds);
    void DisplayCalculator();
    void DisplayWebPage(string url);
    void DisplayLLMChat();
    void DisplayVideoPlayer(string url);
    void DisplayPDFReader(string url);
    #endregion

    #region Unity Callback Methods (Inputs from UI to be Handled)
    // These methods are called by the platform-specific UI in response to user actions.
    // Their core logic is typically delegated to an IUICallbackHandler.
    void UserSelectionCallback(string userId);
    void StepNavigationCallback(int index);
    void CheckItemCallback(int index);
    void UncheckItemCallback(int index);
    void SignOffChecklistCallback();
    void ProtocolSelectionCallback(string protocolJson);
    void CloseProtocolCallback();
    void ChatMessageCallback(string message);
    void LoginCallback(string username, string password);
    void CreateUserCallback(string userName);
    #endregion
}
