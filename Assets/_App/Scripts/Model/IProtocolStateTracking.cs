using System;
using System.Threading.Tasks;
using UniRx;
using ProtocolState;

public interface IProtocolStateTracking
{
    // Observable-based methods to stream protocol state changes.
    // If protocolName is null, return all state data for the given userID.
    IObservable<ProtocolState> GetProtocolStateData(string protocolName, string userID);
    IObservable<ProtocolState> GetAllProtocolStateData();

    // File-based methods for direct protocol state persistence.

    //Task<ProtocolStateData> GetProtocolState(string userID, string protocolTitle);

    Task<List<ProtocolStateData>> GetProtocolStatesByUser(string userID);
    Task<List<ProtocolStateData>> GetProtocolStatesByTitle(string protocolTitle);

    Task SaveProtocolStateData(ProtocolStateData protocolStateData);
    
    Task DeleteProtocolState(string userID, string protocolTitle);
    Task DeleteAllProtocolStateData();
    Task DeleteAllProtocolStateDataForUser(string userID);

    // Incremental update methods for modifying portions of a protocol's state.
    Task UpdateResumeState(string userID, string protocolTitle, int currentStep, int currentCheckItem);
    Task UpdateCheckItemCompletion(string userID, string protocolTitle, int stepIndex, int checkIndex, bool isChecked, string completionTime);
    Task SignOffStep(string userID, string protocolTitle, int stepIndex, string signOffTime);
    Task ResetStepData(string userID, string protocolTitle, int stepIndex);
}
