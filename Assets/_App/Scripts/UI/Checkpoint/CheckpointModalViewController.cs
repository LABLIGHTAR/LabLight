using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;

/// <summary>
/// Runtime modal listing unfinished checkpoint files for the selected protocol / user.
/// The panel is expected to live under a canvas with a ScrollView that has
/// a VerticalLayoutGroup or ContentSizeFitter.
/// </summary>
public sealed class CheckpointModalViewController : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private Transform rowsParent;             // Content container inside the scroll view
    [SerializeField] private GameObject rowPrefab;             // Prefab with CheckpointRowView script
    [SerializeField] private XRSimpleInteractable newRunInteractable;
    [SerializeField] private XRSimpleInteractable closeInteractable;
    [SerializeField] private TextMeshProUGUI headerText;

    private ICheckpointDataProvider _provider;
    private ProtocolDefinition _protocol;
    private string _userID;

    private readonly List<CheckpointRowView> _spawnedRows = new();

    public async void Init(ProtocolDefinition protocol, string userID)
    {
        _provider = ServiceRegistry.GetService<ICheckpointDataProvider>();
        _protocol = protocol;
        _userID   = userID;

        headerText.text = $"Resume {_protocol.title}";
        Debug.Log($"[CHECKPOINT_UI] ts={DateTime.UtcNow:o} action=OpenModal");

        await RefreshAsync();

        newRunInteractable.selectEntered.AddListener(_ => HandleNewRun());
        closeInteractable.selectEntered.AddListener(_ => Close());
    }

    private async Task RefreshAsync()
    {
        // Clear old rows
        foreach (var r in _spawnedRows) Destroy(r.gameObject);
        _spawnedRows.Clear();

        var states = await _provider.LoadStatesAsync(_protocol.title, _userID);
        Debug.Log($"[CHECKPOINT_UI] ts={DateTime.UtcNow:o} action=Populate count={states.Count}");

        foreach (var st in states)
        {
            var go = Instantiate(rowPrefab, rowsParent);
            var row = go.GetComponent<CheckpointRowView>();
            row.Bind(st,
                onResume: () => HandleResume(st),
                onDelete: () => HandleDelete(st));
            _spawnedRows.Add(row);
        }
    }

    private async void HandleResume(CheckpointState state)
    {
        Debug.Log($"[CHECKPOINT_UI] ts={DateTime.UtcNow:o} action=Resume sessionID={state.SessionID}");

        // 1. Ensure the matching protocol definition is active so hydration succeeds
        if (ProtocolState.Instance.ActiveProtocol.Value == null ||
            !string.Equals(ProtocolState.Instance.ActiveProtocol.Value.title, _protocol.title, StringComparison.OrdinalIgnoreCase))
        {
            ProtocolState.Instance.SetProtocolDefinition(_protocol);
        }

        // 2. Install the saved checkpoint so further incremental saves target the same file
        CheckpointManager.Instance?.LoadExistingState(state);

        // 3. Re-hydrate runtime progress (steps, checklist, locks…)
        ProtocolState.Instance.HydrateFromCheckpoint(state);

        // Ensure UI reflects resumed protocol (checklist shown, menu hidden)
        if (ServiceRegistry.GetService<IUIDriver>() is UnityUIDriver driver)
        {
            driver.OnProtocolChange(_protocol);
        }

        Close();
    }

    private async void HandleDelete(CheckpointState state)
    {
        await _provider.DeleteStateAsync(state.SessionID);
        Debug.Log($"[CHECKPOINT_UI] ts={DateTime.UtcNow:o} action=Delete sessionID={state.SessionID}");
        await RefreshAsync();
    }

    private void HandleNewRun()
    {
        Debug.Log($"[CHECKPOINT_UI] ts={DateTime.UtcNow:o} action=NewRun protocol={_protocol.title}");

        // Serialize protocol and start a fresh run
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(_protocol);
        ServiceRegistry.GetService<IUIDriver>()?.ProtocolSelectionCallback(json);

        Close();
    }

    private void Close()
    {
        Destroy(gameObject);
    }
}