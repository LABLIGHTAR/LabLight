using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UniRx;

public class ProtocolState : MonoBehaviour
{
    public static ProtocolState Instance { get; private set; }

    // State data
    public ReactiveProperty<ProtocolDefinition> ActiveProtocol { get; } = new ReactiveProperty<ProtocolDefinition>();
    public ReactiveProperty<string> ProtocolTitle { get; } = new ReactiveProperty<string>();
    public ReactiveProperty<DateTime> StartTime { get; } = new ReactiveProperty<DateTime>();
    public ReactiveCollection<StepState> Steps { get; } = new ReactiveCollection<StepState>();
    public ReactiveProperty<int> CurrentStep { get; } = new ReactiveProperty<int>();
    public ReactiveProperty<string> CsvPath { get; } = new ReactiveProperty<string>();

    // Data streams
    public Subject<ProtocolDefinition> ProtocolStream { get; } = new Subject<ProtocolDefinition>();
    public Subject<StepState> StepStream { get; } = new Subject<StepState>();
    public Subject<List<CheckItemState>> ChecklistStream { get; } = new Subject<List<CheckItemState>>();

    // Locking and alignment bools
    public ReactiveProperty<bool> LockingTriggered { get; } = new ReactiveProperty<bool>();
    public ReactiveProperty<bool> AlignmentTriggered { get; } = new ReactiveProperty<bool>();

    // Properties for easy access
    public ReactiveProperty<StepState> CurrentStepState { get; } = new ReactiveProperty<StepState>();
    public ReactiveProperty<CheckItemState> CurrentCheckItemState { get; } = new ReactiveProperty<CheckItemState>();

    // Computed properties
    public StepDefinition CurrentStepDefinition => ActiveProtocol.Value?.steps[CurrentStep.Value];
    public List<CheckItemDefinition> CurrentChecklist => CurrentStepDefinition?.checklist;
    public CheckItemDefinition CurrentCheckItemDefinition => CurrentChecklist?[CurrentStepState.Value?.CheckNum.Value ?? 0];
    public int CurrentCheckNum => CurrentStepState.Value?.CheckNum.Value ?? 0;

    // Helper methods
    public bool HasCurrentChecklist() => CurrentChecklist != null && CurrentChecklist.Count > 0;
    public bool HasCurrentCheckItem() => CurrentCheckItemDefinition != null;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Multiple ProtocolState instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        ServiceRegistry.GetService<IUIDriver>()?.Initialize();
    }

    public void SetProtocolDefinition(ProtocolDefinition protocolDefinition)
    {
        if (protocolDefinition == null || protocolDefinition.steps.Count == 0)
        {
            Debug.LogError("Invalid protocol definition");
            return;
        }

        Steps.Clear();
        ActiveProtocol.Value = protocolDefinition;
        ProtocolTitle.Value = protocolDefinition.title;

        InitializeSteps(protocolDefinition);
        InitCSV();

        ServiceRegistry.GetService<ILighthouseControl>()?.SetProtocolStatus();
        ProtocolStream.OnNext(protocolDefinition);
        SceneLoader.Instance.LoadSceneClean("Protocol");
    }

    private void InitializeSteps(ProtocolDefinition protocolDefinition)
    {
        var arModels = protocolDefinition.globalArElements
            .Where(x => x.arDefinitionType == ArDefinitionType.Model)
            .Cast<ModelArDefinition>()
            .ToList();

        if (arModels.Count > 0)
        {
            protocolDefinition.steps.Insert(0, CreateLockingStep(arModels));
        }

        foreach (var step in protocolDefinition.steps)
        {
            var stepState = new StepState();
            if (step.checklist != null)
            {
                stepState.Checklist = new ReactiveCollection<CheckItemState>(
                    step.checklist.Select(check => new CheckItemState { Text = check.Text })
                );
            }
            Steps.Add(stepState);
        }
        SetStep(0);
    }

    private StepDefinition CreateLockingStep(List<ModelArDefinition> arModels)
    {
        var checkList = new List<CheckItemDefinition>
        {
            new CheckItemDefinition { Text = "Place the items listed below on your workspace" }
        };

        checkList.AddRange(arModels.Select(arModel => new CheckItemDefinition
        {
            Text = arModel.name,
            operations = new List<ArOperation> { new AnchorArOperation { arDefinition = arModel } }
        }));

        return new StepDefinition
        {
            checklist = checkList
        };
    }

    public void SetStep(int step)
    {
        if (step < 0 || ActiveProtocol.Value == null || Steps == null || step >= Steps.Count)
        {
            return;
        }

        CurrentStep.Value = step;
        CurrentStepState.Value = Steps[step];
        StepStream.OnNext(CurrentStepState.Value);
        UpdateCheckItem();
    }

    public void SetCheckItem(int index)
    {
        var currentStepState = CurrentStepState.Value;
        if (index < 0 || currentStepState.Checklist == null || index >= currentStepState.Checklist.Count)
        {
            return;
        }

        currentStepState.CheckNum.Value = index;
        CurrentCheckItemState.Value = currentStepState.Checklist[index];
        ChecklistStream.OnNext(currentStepState.Checklist.ToList());
        ServiceRegistry.GetService<ILighthouseControl>()?.SetProtocolStatus();
    }

    private void UpdateCheckItem()
    {
        var currentStepState = CurrentStepState.Value;
        if (currentStepState.Checklist != null)
        {
            var firstUncheckedItem = currentStepState.Checklist.FirstOrDefault(item => !item.IsChecked.Value);
            SetCheckItem(firstUncheckedItem != null ? currentStepState.Checklist.IndexOf(firstUncheckedItem) : currentStepState.Checklist.Count - 1);
        }
        else
        {
            CurrentCheckItemState.Value = null;
            ChecklistStream.OnNext(null);
        }
    }

    public void SignOff()
    {
        var currentStep = Steps[CurrentStep.Value];
        if (ActiveProtocol.Value == null || currentStep == null || currentStep.Checklist == null || currentStep.SignedOff.Value)
        {
            return;
        }

        currentStep.SignedOff.Value = true;
        ServiceRegistry.GetService<ILighthouseControl>()?.SetProtocolStatus();
    }

    private void InitCSV()
    {
        string fileName = $"{ProtocolTitle.Value}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";
        string csvPath = Path.Combine(Application.persistentDataPath, fileName);
        if (!File.Exists(csvPath))
        {
            File.WriteAllText(csvPath, "Action,Result,Completion Time\n");
        }
        CsvPath.Value = csvPath;
    }

    public class StepState
    {
        public ReactiveProperty<bool> SignedOff { get; } = new ReactiveProperty<bool>();
        public ReactiveProperty<int> CheckNum { get; } = new ReactiveProperty<int>();
        public ReactiveCollection<CheckItemState> Checklist { get; set; }
    }

    public class CheckItemState
    {   
        public ReactiveProperty<DateTime> CompletionTime { get; } = new ReactiveProperty<DateTime>();
        public ReactiveProperty<bool> IsChecked { get; } = new ReactiveProperty<bool>();
        public string Text { get; set; }
    }
}