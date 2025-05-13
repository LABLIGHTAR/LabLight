using UnityEngine;
using TMPro;
using System;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Linq; // Added for Enumerable.LastOrDefault and FindIndex

/// <summary>
/// Binds a single CheckpointState to UI.
/// Expected hierarchy:
///  └─ Text (title)
///  └─ Text (timestamp)
///  └─ Button Resume
///  └─ Button Delete
/// </summary>
public sealed class CheckpointRowView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI dateText;
    [SerializeField] private XRSimpleInteractable resumeInteractable;
    [SerializeField] private XRSimpleInteractable deleteInteractable;

    public void Bind(CheckpointState state, Action onResume, Action onDelete)
    {
        titleText.text = state.ProtocolName;

        // --- Calculate Step Progress ---
        string stepProgressStr;
        int currentStepDisplayIndex = 0; // 0-based for logic
        int totalSteps = state.Steps.Count;

        if (totalSteps > 0)
        {
            int firstUnsignedStepIdx = state.Steps.FindIndex(s => s.SignoffTime == null);
            if (firstUnsignedStepIdx != -1)
            {
                currentStepDisplayIndex = firstUnsignedStepIdx;
            }
            else
            {
                currentStepDisplayIndex = totalSteps - 1; // All steps signed off, show last step
            }
            stepProgressStr = $"Step: {currentStepDisplayIndex + 1}/{totalSteps}";
        }
        else
        {
            stepProgressStr = "Step: N/A";
        }

        // --- Calculate Check Item Progress for the current step ---
        string checkItemProgressStr;
        if (totalSteps > 0 && currentStepDisplayIndex < totalSteps)
        {
            var currentStepProgress = state.Steps[currentStepDisplayIndex];
            int currentCheckItemDisplayIndex = 0; // 0-based for logic
            int totalCheckItemsInStep = currentStepProgress.CheckItems.Count;

            if (totalCheckItemsInStep > 0)
            {
                int firstUncompletedItemIdx = currentStepProgress.CheckItems.FindIndex(ci => ci.CompletedTime == null);
                if (firstUncompletedItemIdx != -1)
                {
                    currentCheckItemDisplayIndex = firstUncompletedItemIdx;
                    checkItemProgressStr = $"Item: {currentCheckItemDisplayIndex + 1}/{totalCheckItemsInStep}";
                }
                else
                {
                    currentCheckItemDisplayIndex = totalCheckItemsInStep - 1; // All items completed, show last item
                    checkItemProgressStr = $"Checklist awaiting sign-off";
                }
            }
            else
            {
                checkItemProgressStr = "No checklist on step";
            }
        }
        else
        {
            checkItemProgressStr = "Item: N/A";
        }

        // --- Determine Last Accessed Time ---
        DateTime latestActivityTimestamp = state.StartTimestamp;
        foreach (var step in state.Steps)
        {
            if (step.SignoffTime.HasValue && step.SignoffTime.Value > latestActivityTimestamp)
            {
                latestActivityTimestamp = step.SignoffTime.Value;
            }
            foreach (var item in step.CheckItems)
            {
                if (item.CompletedTime.HasValue && item.CompletedTime.Value > latestActivityTimestamp)
                {
                    latestActivityTimestamp = item.CompletedTime.Value;
                }
            }
        }
        string lastAccessedStr = $"Last Update: {latestActivityTimestamp.ToLocalTime().ToString("g")}";

        // --- Combine all information ---
        dateText.text = $"{stepProgressStr}  |  {checkItemProgressStr}  |  {lastAccessedStr}";

        resumeInteractable.selectEntered.RemoveAllListeners();
        resumeInteractable.selectEntered.AddListener(_ => onResume?.Invoke());

        deleteInteractable.selectEntered.RemoveAllListeners();
        deleteInteractable.selectEntered.AddListener(_ => onDelete?.Invoke());
    }
}