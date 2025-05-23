using System.Threading.Tasks;
using System.Collections.Generic;

/// <summary>
/// Defines the contract for a UI callback handler, centralizing actions
/// triggered by UI interactions across different platforms.
/// </summary>
public interface IUICallbackHandler
{
    #region User Session & Profile Callbacks
    Task HandleUserSelection(string userId);
    Task HandleLogin(string username, string password);
    Task<List<LocalUserProfileData>> HandleCreateUser(string userName);
    #endregion

    #region Protocol Navigation & Interaction Callbacks
    void HandleStepNavigation(int stepIndex);
    void HandleCheckItem(int itemIndex);
    void HandleUncheckItem(int itemIndex);
    void HandleSignOffChecklist();
    void HandleProtocolSelection(string protocolJson);
    void HandleCloseProtocol();
    #endregion

    #region Communication Callbacks
    void HandleChatMessage(string message);
    #endregion
} 