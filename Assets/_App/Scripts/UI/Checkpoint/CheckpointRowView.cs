using UnityEngine;
using TMPro;
using System;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

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
        dateText.text  = state.StartTimestamp.ToLocalTime().ToString("g");

        resumeInteractable.selectEntered.RemoveAllListeners();
        resumeInteractable.selectEntered.AddListener(_ => onResume?.Invoke());

        deleteInteractable.selectEntered.RemoveAllListeners();
        deleteInteractable.selectEntered.AddListener(_ => onDelete?.Invoke());
    }
}