using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UniRx;

/// <summary>
/// Listens to ProtocolState changes and writes a crash-safe JSON checkpoint
/// after every relevant mutation.
/// </summary>
[DefaultExecutionOrder(-150)] // ensure it starts before most runtime code
public sealed class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }

    private CheckpointState _current;
    private ICheckpointDataProvider _provider;

    private readonly CompositeDisposable _subs = new();

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _provider = ServiceRegistry.GetService<ICheckpointDataProvider>();
        if (_provider == null)
        {
            Debug.LogError("[CHECKPOINT] provider not registered – persistence disabled");
            enabled = false;
            return;
        }

        // Observe protocol lifecycle
        ProtocolState.Instance.ProtocolStream
            .Subscribe(protocol =>
            {
                if (protocol == null)
                {
                    // protocol closed – clear state
                    _current = null;
                    _subs.Clear();
                    return;
                }
                StartNewCheckpoint(protocol);
            })
            .AddTo(this);
    }

    private void StartNewCheckpoint(ProtocolDefinition protocol)
    {
        _subs.Clear();

        _current = protocol.ToCheckpointSkeleton(SessionState.currentUserProfile?.GetUserId() ?? "anonymous");

        // Persist immediately so temp file exists early
        _ = SafeSaveAsync();

        // Step & checklist updates
        ProtocolState.Instance.StepStream
            .Subscribe(_ => OnStepChanged())
            .AddTo(_subs);

        ProtocolState.Instance.ChecklistStream
            .Subscribe(_ => OnChecklistChanged())
            .AddTo(_subs);

        // Final completion watcher
        ProtocolState.Instance.Steps.ObserveEveryValueChanged(_ => ProtocolState.Instance.Steps.All(s => s.SignedOff.Value))
            .Where(allDone => allDone)
            .Take(1)
            .Subscribe(_ => OnProtocolCompleted())
            .AddTo(_subs);
    }

    private void OnStepChanged()
    {
        if (_current == null) return;
        _current.SyncFromRuntime();       // extension updates contents
        _ = SafeSaveAsync();
    }

    private void OnChecklistChanged()
    {
        if (_current == null) return;
        _current.SyncFromRuntime();
        _ = SafeSaveAsync();
    }

    private void OnProtocolCompleted()
    {
        if (_current == null) return;
        _current.CompletionTimestamp = DateTime.UtcNow;
        _ = SafeSaveAsync();
    }

    private async Task SafeSaveAsync()
    {
        try
        {
            await _provider.UpdateStateAsync(_current).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CHECKPOINT] ts={DateTime.UtcNow:o} action=UpdateState fatal err={ex}");
        }
    }

    private void OnDestroy()
    {
        _subs.Dispose();
        if (Instance == this) Instance = null;
    }

    // --------------- Helpers -------------------------------------------------
    /// <summary>
    /// Called by UI when the user resumes a previously-saved run.
    /// It installs the supplied state as the current working
    /// checkpoint so that further incremental updates append to
    /// the same JSON file.
    /// </summary>
    /// <remarks>MUST be invoked before <see cref="ProtocolState.HydrateFromCheckpoint"/>.</remarks>
    public void LoadExistingState(CheckpointState existing)
    {
        if (existing == null) throw new ArgumentNullException(nameof(existing));

        // Replace the in-memory state and ensure we are listening for changes.
        _current = existing;

        // Guard against duplicate subscriptions by clearing and
        // then re-registering the usual observers.
        _subs.Clear();

        ProtocolState.Instance.StepStream
            .Subscribe(_ => OnStepChanged())
            .AddTo(_subs);

        ProtocolState.Instance.ChecklistStream
            .Subscribe(_ => OnChecklistChanged())
            .AddTo(_subs);

        // Completion watcher
        ProtocolState.Instance.Steps.ObserveEveryValueChanged(_ => ProtocolState.Instance.Steps.All(s => s.SignedOff.Value))
            .Where(allDone => allDone)
            .Take(1)
            .Subscribe(_ => OnProtocolCompleted())
            .AddTo(_subs);

        Debug.Log($"[CHECKPOINT] ts={DateTime.UtcNow:o} action=ResumeLoaded sessionID={existing.SessionID}");
    }
}