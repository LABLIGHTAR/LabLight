using System;
using System.Collections.Generic;
using UnityEngine;
using UniRx;

public class ProtocolManager : MonoBehaviour
{
    // private Dictionary<ArDefinition, ArElementViewController> specificArViews = new Dictionary<ArDefinition, ArElementViewController>();

    private Dictionary<ArObject, ArObjectViewController> specificArObjectViews = new Dictionary<ArObject, ArObjectViewController>();

    [SerializeField] GameObject timerPrefab;

    private CheckItemDefinition previousCheckItem;

    private void Awake()
    {
        ProtocolState.Instance.LockingTriggered.Value = false;
        ProtocolState.Instance.AlignmentTriggered.Value = false;
        //ProtocolState.Instance.ChecklistStream.Subscribe(_ => OnCheckItemChange()).AddTo(this);
    }

    private void OnEnable()
    {
        ProtocolState.Instance.StartTime.Value = DateTime.Now;
    }

    private void OnDisable()
    {
        ProtocolState.Instance.AlignmentTriggered.Value = false;
    }

    private void OnCheckItemChange()
    {
        if (!ProtocolState.Instance.HasCurrentChecklist()) return;

        var currentCheckItem = ProtocolState.Instance.CurrentCheckItemDefinition;
        if (currentCheckItem == null || currentCheckItem == previousCheckItem) return;

        // Check for timer actions in the current checkitem
        foreach (var action in currentCheckItem.arActions)
        {
            if (action.actionType == "timer")
            {
                var timer = Instantiate(timerPrefab, transform);
                // You might want to configure the timer here based on action parameters
                // timer.GetComponent<TimerController>().SetDuration(action.duration);
                break;
            }
        }

        previousCheckItem = currentCheckItem;
    }
}
