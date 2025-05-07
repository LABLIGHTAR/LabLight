using System;
using System.Linq;
using UnityEngine;

public static class CheckpointStateExtensions
{
    /// <summary>Create a shell CheckpointState when a protocol starts.</summary>
    public static CheckpointState ToCheckpointSkeleton(this ProtocolDefinition protocol, string userId)
    {
        var state = new CheckpointState
        {
            UserID          = userId,
            ProtocolName    = protocol.title,
            ProtocolVersion = protocol.version,
            StartTimestamp  = DateTime.UtcNow,
        };

        // Pre-populate step meta
        for (int i = 0; i < protocol.steps.Count; i++)
        {
            state.Steps.Add(new CheckpointState.StepProgress
            {
                StepIndex = i,
                Title     = protocol.steps[i].title,
            });
        }
        return state;
    }

    /// <summary>Mutate the CheckpointState in-place using current runtime progress.</summary>
    public static void SyncFromRuntime(this CheckpointState state)
    {
        var ps = ProtocolState.Instance;
        if (ps == null || ps.ActiveProtocol.Value == null) return;

        // ensure StepProgress list matches runtime length
        var protoSteps = ps.ActiveProtocol.Value.steps;
        while (state.Steps.Count < ps.Steps.Count)
        {
            int idx = state.Steps.Count;
            state.Steps.Add(new CheckpointState.StepProgress
            {
                StepIndex = idx,
                Title     = protoSteps != null && idx < protoSteps.Count ? protoSteps[idx].title : $"Step {idx}"
            });
        }

        // iterate through runtime steps and copy progress
        for (int i = 0; i < ps.Steps.Count; i++)
        {
            var runtimeStep = ps.Steps[i];
            var cpStep      = state.Steps[i];

            // Estimate a step start time from the first completed checklist item
            var firstChecked = runtimeStep.Checklist?
                                        .FirstOrDefault(c => c.IsChecked.Value);
            if (cpStep.StartTime == default && firstChecked != null)
            {
                var ts = firstChecked.CompletionTime.Value;
                cpStep.StartTime = ts != default ? ts : DateTime.UtcNow;
            }

            // Sign-off
            if (runtimeStep.SignedOff.Value && cpStep.SignoffTime == null)
            {
                cpStep.SignoffTime   = DateTime.UtcNow;
                cpStep.SignoffUserID = state.UserID;
            }

            // Checklist items
            if (runtimeStep.Checklist != null)
            {
                cpStep.CheckItems = runtimeStep.Checklist
                    .Select((item, idx) => new CheckpointState.CheckItemProgress
                    {
                        Index         = idx,
                        Text          = item.Text,
                        CompletedTime = item.IsChecked.Value
                                        ? (item.CompletionTime.Value != default
                                            ? item.CompletionTime.Value
                                            : DateTime.UtcNow)
                                        : null,
                        CompletedBy   = item.IsChecked.Value ? state.UserID : null
                    })
                    .ToList();
            }
        }
    }

    /// <summary>Restore ProtocolState progress from a saved checkpoint.</summary>
    public static void HydrateFromCheckpoint(this ProtocolState runtime, CheckpointState saved)
    {
        var proto = runtime.ActiveProtocol.Value;
        if (proto == null ||
            !string.Equals(proto.title, saved.ProtocolName, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError($"[CHECKPOINT] hydrate failed – protocol mismatch.");
            return;
        }

        // Restore step & checklist progress
        for (int s = 0; s < saved.Steps.Count && s < runtime.Steps.Count; s++)
        {
            var savedStep   = saved.Steps[s];
            var runtimeStep = runtime.Steps[s];

            if (savedStep.CheckItems != null && runtimeStep.Checklist != null)
            {
                foreach (var savedItem in savedStep.CheckItems)
                {
                    if (savedItem.Index < runtimeStep.Checklist.Count &&
                        savedItem.CompletedTime != null)
                    {
                        var itemState = runtimeStep.Checklist[savedItem.Index];
                        itemState.IsChecked.Value     = true;
                        itemState.CompletionTime.Value = savedItem.CompletedTime.Value;
                    }
                }
            }

            runtimeStep.SignedOff.Value = savedStep.SignoffTime != null;
        }

        // Jump to first unfinished step (or 0 if all signed off)
        int targetStep = saved.Steps.FindIndex(st => st.SignoffTime == null);
        runtime.SetStep(targetStep >= 0 ? targetStep : 0);
    }
}