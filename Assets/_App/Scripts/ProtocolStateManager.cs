using System;
using System.Threading.Tasks;
using UniRx;
using UnityEngine;

public class ProtocolStateManager
{
    private readonly IProtocolDataProvider _protocolDataProvider;
    private readonly string _userId;
    private CompositeDisposable _subscriptions = new CompositeDisposable();

    public ProtocolStateManager(string userId, IProtocolDataProvider protocolDataProvider)
    {
        _userId = userId;
        _protocolDataProvider = protocolDataProvider;
    }

    /// <summary>
    /// Loads the protocol definition, optionally resuming from a saved state.
    /// </summary>
    public async Task LoadProtocolDefinition(string userId, ProtocolDefinition protocolDefinition)
    {
        var savedState = await _protocolDataProvider.GetProtocolState(userId, protocolDefinition.title);
        if (savedState != null && await PromptUserForStateLoad(protocolDefinition.title))
        {
            Debug.Log("Resuming saved protocol state.");
            ProtocolState.Instance.SetProtocolDefinition(protocolDefinition, savedState);
        }
        else
        {
            Debug.Log("Starting protocol afresh.");
            ProtocolState.Instance.SetProtocolDefinition(protocolDefinition);
        }
        SubscribeToProtocolStateEvents();
    }

    // Simulated prompt; 
    private async Task<bool> PromptUserForStateLoad(string protocolTitle)
    {
        await Task.Delay(100);
        return true;
    }

    /// <summary>
    /// Subscribes to changes in the runtime protocol state and updates persistence accordingly.
    /// </summary>
    private void SubscribeToProtocolStateEvents()
    {
        _subscriptions.Dispose();
        _subscriptions = new CompositeDisposable();

        if (ProtocolState.Instance == null)
        {
            Debug.LogWarning("ProtocolState instance is null.");
            return;
        }

        Observable.CombineLatest(
            ProtocolState.Instance.CurrentStep,
            ProtocolState.Instance.CurrentStepState.Select(state => state?.CheckNum),
            (step, checkNum) => new { step, checkNum }
        )
        .Throttle(TimeSpan.FromSeconds(1))
        .DistinctUntilChanged()
        .Subscribe(data =>
        {
            _protocolDataProvider.UpdateResumeState(_userId,
                ProtocolState.Instance.ProtocolTitle.Value,
                data.step,
                data.checkNum ?? 0);
        })
        .AddTo(_subscriptions);

        ProtocolState.Instance.CurrentStepState
            .Where(state => state != null)
            .Subscribe(stepState =>
            {
                stepState.SignedOff
                    .Where(s => s)
                    .Subscribe(_ =>
                    {
                        string signOffTime = DateTime.Now.ToString();
                        int stepIndex = ProtocolState.Instance.CurrentStep.Value;
                        _protocolDataProvider.SignOffStep(_userId,
                            ProtocolState.Instance.ProtocolTitle.Value,
                            stepIndex,
                            signOffTime);
                    })
                    .AddTo(_subscriptions);

                if (stepState.Checklist != null)
                {
                    for (int i = 0; i < stepState.Checklist.Count; i++)
                    {
                        int checkIndex = i;
                        stepState.Checklist[i].IsChecked
                            .Subscribe(isChecked =>
                            {
                                string cmpTime = isChecked ? DateTime.Now.ToString() : "";
                                int stepIndex = ProtocolState.Instance.CurrentStep.Value;
                                _protocolDataProvider.UpdateCheckItemCompletion(_userId,
                                    ProtocolState.Instance.ProtocolTitle.Value,
                                    stepIndex,
                                    checkIndex,
                                    isChecked,
                                    cmpTime);
                            })
                            .AddTo(_subscriptions);
                    }
                }
            })
            .AddTo(_subscriptions);
    }
} 