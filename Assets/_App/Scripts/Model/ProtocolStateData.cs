using UnityEngine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

public class ProtocolStateData
{
    // Completion data: userID and protocolTitle (used for documentation)
    [JsonProperty("userID")]
    public string UserID { get; set; }
    
    [JsonProperty("protocolTitle")]
    public string ProtocolTitle { get; set; }
    
    // Resume state: current step and current check item indexes (used for resuming protocol)
    [JsonProperty("currentStep")]
    public int CurrentStep { get; set; }
    
    [JsonProperty("currentCheckItem")]
    public int CurrentCheckItem { get; set; }
    
    // Completion data: each step with its checklist completion status
    [JsonProperty("steps")]
    public List<StepCompletionData> Steps { get; set; } = new List<StepCompletionData>();

    // A flexible container for additional properties (notes, photos etc.)
    [JsonProperty("additionalData")]
    public Dictionary<string, object> AdditionalData { get; set; } = new Dictionary<string, object>();

    // Field to record last update time (improves ordering without parsing filenames)
    [JsonProperty("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.Now;

    public ProtocolStateData(string userID, string protocolTitle)
    {
        UserID = userID;
        ProtocolTitle = protocolTitle;
        CurrentStep = 0;
        CurrentCheckItem = 0;
    }

    [JsonConstructor]
    public ProtocolStateData() { }

    // Factory method to create a state structure based on a protocol definition.
    // Assumes ProtocolDefinition, StepDefinition and CheckItemDefinition exist.
    public static ProtocolStateData CreateFromProtocolDefinition(string userID, ProtocolDefinition protocol)
    {
        var stateData = new ProtocolStateData(userID, protocol.title);
        foreach (var step in protocol.steps)
        {
            var stepData = new StepCompletionData();
            if (step.checklist != null)
            {
                for (int i = 0; i < step.checklist.Count; i++)
                {
                    stepData.Checklist.Add(new CheckItemCompletionData());
                }
            }
            stateData.Steps.Add(stepData);
        }
        return stateData;
    }

    // Update the resume state (current step & check item)
    public void UpdateResumeState(int currentStep, int currentCheckItem)
    {
        CurrentStep = currentStep;
        CurrentCheckItem = currentCheckItem;
        LastUpdated = DateTime.Now;
    }

    // Mark a check item as checked (or unchecked) and record (or clear) its completion time.
    public void UpdateCheckItem(int stepIndex, int checkIndex, bool isChecked, string completionTime)
    {
        if (stepIndex < 0 || stepIndex >= Steps.Count) return;
        var step = Steps[stepIndex];
        if (checkIndex < 0 || checkIndex >= step.Checklist.Count) return;
        var checkItem = step.Checklist[checkIndex];
        checkItem.IsChecked = isChecked;
        checkItem.CompletionTime = isChecked ? completionTime : string.Empty;
        LastUpdated = DateTime.Now;
    }

    // Mark a step as signed off (locking its completion data) with the corresponding sign off time.
    public void SignOffStep(int stepIndex, string signOffTime)
    {
        if (stepIndex < 0 || stepIndex >= Steps.Count) return;
        var step = Steps[stepIndex];
        step.SignedOff = true;
        step.SignOffTime = signOffTime;
        LastUpdated = DateTime.Now;
    }

    // Emergency reset: wipe all completion data for a step (both step sign off and its check items).
    public void ResetStepData(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= Steps.Count) return;
        var step = Steps[stepIndex];
        step.SignedOff = false;
        step.SignOffTime = string.Empty;
        foreach (var checkItem in step.Checklist)
        {
            checkItem.IsChecked = false;
            checkItem.CompletionTime = string.Empty;
        }
        LastUpdated = DateTime.Now;
    }
}

public class StepCompletionData
{
    // Indicates whether the step is signed off (completion data then becomes locked)
    [JsonProperty("signedOff")]
    public bool SignedOff { get; set; } = false;

    // Time when the step was signed off (empty if not signed off)
    [JsonProperty("signOffTime")]
    public string SignOffTime { get; set; } = string.Empty;

    // Completion data for each check item in order
    [JsonProperty("checklist")]
    public List<CheckItemCompletionData> Checklist { get; set; } = new List<CheckItemCompletionData>();
}

public class CheckItemCompletionData
{
    // Whether the check item is checked
    [JsonProperty("isChecked")]
    public bool IsChecked { get; set; } = false;

    // Time when the check item was completed (empty if unchecked)
    [JsonProperty("completionTime")]
    public string CompletionTime { get; set; } = string.Empty;
}

//outline:
/*
1. Finish building protocolStateData class
2. Finish protocol state interface (implement crud operations)
3. Implement IProtocolStateTracking interface into local data provider
4. Register protocol state data provider in session manager
5. Utilize service provider in protocol state to get or save protocol state data
6. update protocol state to allow for resuming, get backend working
7. implement ui for resuming protocol (create new methods in ui driver)
8. create unity engine ui, ensure to have stubs for unity ui 
9. create swift ui 
*/