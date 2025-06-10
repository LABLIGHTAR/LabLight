using System;
using System.Text;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;

public class ChecklistPanelViewController : LLBasePanel
{
    UnityUIDriver UIDriver;
    [SerializeField] GameObject noChecklistText;
    [SerializeField] AudioSource audioPlayer;

    [Header("Checkitem Views")]
    [SerializeField] List<Transform> checkItemSlots;
    CheckItemPool checkItemPool;

    private bool checkOnCooldown = false;

    [SerializeField] TextMeshProUGUI stepText;

    [Header("Signoff Icons")]
    [SerializeField] GameObject unlockedIcon;
    [SerializeField] GameObject lockedIcon;

    [Header("Protocol Navigation Buttons")]
    [SerializeField] XRSimpleInteractable closeProtocolButton;
    [SerializeField] XRSimpleInteractable checkItemButton;
    [SerializeField] XRSimpleInteractable unCheckItemButton;
    [SerializeField] XRSimpleInteractable signOffButton;
    [SerializeField] XRSimpleInteractable nextStepButton;
    [SerializeField] XRSimpleInteractable previousStepButton;

    [Header("Popups")]
    [SerializeField] PopupEventSO signOffPopupEventSO;
    [SerializeField] PopupEventSO checklistIncompletePopupEventSO;
    [SerializeField] PopupEventSO closeProtocolPopupEventSO;
    PopupPanelViewController popupPanelViewController;

    [Header("HUD Event SO")]
    [SerializeField] HudEventSO hudEventSO;

    // Add new fields for optimization
    private readonly WaitForSeconds standardDelay = new WaitForSeconds(0.2f);
    private readonly WaitForSeconds buttonDelay = new WaitForSeconds(0.5f);
    private const float COOLDOWN_DURATION = 1f;

    protected override void Awake()
    {
        base.Awake();
        if (!TryGetComponent(out checkItemPool))
        {
            Debug.LogError($"[{nameof(ChecklistPanelViewController)}] Missing CheckItemPool component!");
        }
    }

    void OnEnable()
    {
        SetupButtonEvents();
        SetupPopupEvents();
        SetupVoiceCommands();
    }

    void OnDisable()
    {
        RemoveButtonEvents();
        RemovePopupEvents();
        DisposeVoice?.Invoke();
        
        // Clean up all slots
        foreach(var slot in checkItemSlots)
        {
            CleanupSlot(slot);
        }
    }

    void Start()
    {
        checkItemPool?.CreatePooledObjects();
        UIDriver = ServiceRegistry.GetService<IUIDriver>() as UnityUIDriver;
        
        if (UIDriver == null)
        {
            Debug.LogError($"[{nameof(ChecklistPanelViewController)}] Failed to get UIDriver!");
            return;
        }

        StartCoroutine(LoadChecklist());

        popupPanelViewController = FindFirstObjectByType<PopupPanelViewController>(FindObjectsInactive.Include);
        if (popupPanelViewController == null)
        {
            Debug.LogWarning($"[{nameof(ChecklistPanelViewController)}] PopupPanelViewController not found!");
        }
    }

    /// <summary>
    /// Checks the next unchecked item if possible
    /// </summary>
    public void CheckItem()
    {
        if (!ValidateChecklistOperation("check"))
            return;

        var checklist = ProtocolState.Instance.CurrentStepState.Value.Checklist;
        var firstUncheckedItem = checklist?.FirstOrDefault(item => !item.IsChecked.Value);

        if (firstUncheckedItem == null)
        {
            hudEventSO.DisplayHudWarning("No item to check.");
            return;
        }

        int itemIndex = checklist.IndexOf(firstUncheckedItem);
        
        // Scroll handling with bounds checking
        if (ProtocolState.Instance.CurrentCheckNum < checklist.Count - 1)
        {
            if (checklist.Count > 5 && 
                ProtocolState.Instance.CurrentCheckNum > 0 && 
                ProtocolState.Instance.CurrentCheckNum + 4 < checklist.Count)
            {
                StartCoroutine(ScrollChecklistUp());
            }
        }

        UIDriver.CheckItemCallback(itemIndex);
        StartCooldown();
    }

    /// <summary>
    /// Unchecks the last checked item if possible
    /// </summary>
    public void UnCheckItem()
    {
        if(checkOnCooldown == true)
        {
            Debug.LogWarning("Uncheck on cooldown! Pressing too fast!");
            return;
        }

        if (!ProtocolState.Instance.HasCurrentChecklist() || ProtocolState.Instance.CurrentStepState.Value.SignedOff.Value)
        {
            Debug.LogWarning("Already signed off or no item to uncheck");
            hudEventSO.DisplayHudWarning("Checklist already signed off.");
            return;
        }

        var lastCheckedItem = ProtocolState.Instance.CurrentStepState.Value.Checklist.LastOrDefault(item => item.IsChecked.Value);

        if (lastCheckedItem == null || ProtocolState.Instance.LockingTriggered.Value)
        {
            Debug.LogWarning("No item to uncheck or locking triggered");
            hudEventSO.DisplayHudWarning("No item to uncheck.");
            return;
        }

        if(ProtocolState.Instance.CurrentCheckNum > 1 && ProtocolState.Instance.CurrentStepState.Value.Checklist.Count > 5 && ProtocolState.Instance.CurrentCheckNum + 3 < ProtocolState.Instance.CurrentStepState.Value.Checklist.Count)
        {
            StartCoroutine(ScrollChecklistDown());
        }

        // Uncheck the item
        UIDriver.UncheckItemCallback(ProtocolState.Instance.CurrentStepState.Value.Checklist.IndexOf(lastCheckedItem));

        Invoke("ResetCooldown", 1f);
        checkOnCooldown = true;
    }

    /// <summary>
    /// Locks the checklist from further change if all items are complete
    /// </summary>
    public void SignOff()
    {
        if (!ProtocolState.Instance.HasCurrentChecklist() || ProtocolState.Instance.CurrentStepState.Value.SignedOff.Value)
        {
            Debug.LogWarning("Already signed off or no checklist for this step");
            hudEventSO.DisplayHudWarning("Checklist already signed off.");
            return;
        }

        var uncheckedItemsCount = ProtocolState.Instance.CurrentStepState.Value.Checklist.Count(item => !item.IsChecked.Value);

        if (uncheckedItemsCount != 0)
        {
            Debug.LogWarning("Not all items are checked");
            hudEventSO.DisplayHudWarning("Cannot sign off, not all items are checked.");
            return;
        }

        audioPlayer.Play();
        UIDriver.SignOffChecklistCallback();
        //UIDriver.WriteChecklistToCSV(ProtocolState.Instance.CurrentStepState.Value.Checklist);
        
        //lock sign off indicator in UI
        lockedIcon.SetActive(true);
        unlockedIcon.SetActive(false);
    }

    /// <summary>
    /// Navigates to the previous step if possible
    /// </summary>
    public void PreviousStep()
    {
        UIDriver.StepNavigationCallback(ProtocolState.Instance.CurrentStep.Value - 1);
    }

    /// <summary>
    /// Navigates to the next step if possible, displays error message if checklist is incomplete
    /// </summary>
    public void NextStep()
    {
        UIDriver.StepNavigationCallback(ProtocolState.Instance.CurrentStep.Value + 1);
    }

    void LoadStepText()
    {
        stepText.text = (ProtocolState.Instance.CurrentStep.Value + 1) + " / " + ProtocolState.Instance.Steps.Count;
    }
    
    /// <summary>
    /// Loads the checklist for the current step, displays 5 most relevant items, updates the signoff icon
    /// </summary>
    public IEnumerator LoadChecklist()
    {
        LoadStepText();

        if(checkItemPool == null)
        {
            TryGetComponent(out checkItemPool);
            checkItemPool.CreatePooledObjects();
        }

        // Clean up all slots before loading new items
        foreach(var slot in checkItemSlots)
        {
            CleanupSlot(slot);
        }

        //display proper signoff icon
        if (ProtocolState.Instance.CurrentStepState.Value.SignedOff.Value)
        {
            lockedIcon.SetActive(true);
            unlockedIcon.SetActive(false);
        }
        else
        {
            lockedIcon.SetActive(false);
            unlockedIcon.SetActive(true);
        }

        if(ProtocolState.Instance.HasCurrentChecklist())
        {
            noChecklistText.SetActive(false);
            
            List<ProtocolState.CheckItemState> relevantCheckItems = new List<ProtocolState.CheckItemState>();

            //get relevant check items
            //case for loading checklist from the start
            if(ProtocolState.Instance.CurrentCheckNum == 0)
            {
                for(int i = 0; i < 5; i++)
                {
                    if(i >= ProtocolState.Instance.CurrentStepState.Value.Checklist.Count)
                    {
                        break;
                    }
                    relevantCheckItems.Add(ProtocolState.Instance.CurrentStepState.Value.Checklist[i]);
                }
            }
            //case for loading checklist from the middle
            else
            {
                for(int i = ProtocolState.Instance.CurrentCheckNum - 1; i < ProtocolState.Instance.CurrentCheckNum + 4; i++)
                {
                    if(i >= ProtocolState.Instance.CurrentStepState.Value.Checklist.Count)
                    {
                        break;
                    }
                    relevantCheckItems.Add(ProtocolState.Instance.CurrentStepState.Value.Checklist[i]);
                }
            }

            //load relevant check items
            for(int i = 0; i < relevantCheckItems.Count; i++)
            {
                LoadCheckItem(relevantCheckItems[i], i);
                yield return new WaitForSeconds(0.2f);
            }
        }
        else
        {
            noChecklistText.SetActive(true);
        }
    }

    /// <summary>
    /// Loads a check item into the checklist panel at the specified slot index
    /// </summary>
    void LoadCheckItem(ProtocolState.CheckItemState checkItemData, int slotIndex)
    {
        if (checkItemData == null || slotIndex < 0 || slotIndex >= checkItemSlots.Count)
        {
            Debug.LogError($"Invalid LoadCheckItem parameters: data={checkItemData != null}, slot={slotIndex}");
            return;
        }

        // Clean the target slot first
        CleanupSlot(checkItemSlots[slotIndex]);

        var checkItemView = checkItemPool.GetPooledObject()?.GetComponent<CheckitemView>();
        if (checkItemView == null)
        {
            Debug.LogError("Failed to get pooled CheckitemView!");
            return;
        }

        checkItemView.InitalizeCheckItem(checkItemData);
        checkItemView.transform.SetParent(checkItemSlots[slotIndex], false);
        checkItemView.transform.localPosition = Vector3.zero;
        checkItemView.transform.localRotation = Quaternion.identity;
        checkItemView.gameObject.SetActive(true);
        checkItemView.PlayScaleUpAnimation();
    }

    /// <summary>
    /// Scrolls the checklist up by one item
    /// </summary>
    IEnumerator ScrollChecklistUp()
    {
        if (checkItemSlots == null || checkItemSlots.Count < 5)
        {
            Debug.LogError("Invalid checkItemSlots configuration!");
            yield break;
        }

        // Clean up first slot and play animation for its primary item
        if (checkItemSlots[0].childCount > 0)
        {
            var firstItem = checkItemSlots[0].GetChild(0)?.GetComponent<CheckitemView>();
            if (firstItem != null)
            {
                firstItem.PlayScaleDownAnimation();
                yield return standardDelay;
            }
            CleanupSlot(checkItemSlots[0]);
        }

        // Move items up
        for(int i = 1; i < 5; i++)
        {
            if (checkItemSlots[i].childCount > 0)
            {
                var itemTransform = checkItemSlots[i].GetChild(0);
                var itemView = itemTransform.GetComponent<CheckitemView>();
                
                // Clean target slot before moving
                CleanupSlot(checkItemSlots[i - 1]);
                
                itemView.PlayMoveUpAnimation(checkItemSlots[i].position, checkItemSlots[i - 1].position);
                itemTransform.SetParent(checkItemSlots[i - 1]);
                itemTransform.localPosition = Vector3.zero;
                itemTransform.localRotation = Quaternion.identity;
            }

            if(i != 4)
            {
                yield return standardDelay;
            }
        }

        // Clean last slot before loading new item
        CleanupSlot(checkItemSlots[4]);
        LoadCheckItem(ProtocolState.Instance.CurrentStepState.Value.Checklist[ProtocolState.Instance.CurrentCheckNum + 3], 4);
    }

    /// <summary>
    /// Scrolls the checklist down by one item
    /// </summary>
    IEnumerator ScrollChecklistDown()
    {
        // Clean up last slot and play animation for its primary item
        if (checkItemSlots[4].childCount > 0)
        {
            var lastItem = checkItemSlots[4].GetChild(0)?.GetComponent<CheckitemView>();
            if (lastItem != null)
            {
                lastItem.PlayScaleDownAnimation();
                yield return standardDelay;
            }
            CleanupSlot(checkItemSlots[4]);
        }

        for(int i = 3; i >= 0; i--)
        {
            if (checkItemSlots[i].childCount > 0)
            {
                var itemTransform = checkItemSlots[i].GetChild(0);
                var itemView = itemTransform.GetComponent<CheckitemView>();
                
                // Clean target slot before moving
                CleanupSlot(checkItemSlots[i + 1]);
                
                itemView.PlayMoveDownAnimation(checkItemSlots[i].position, checkItemSlots[i + 1].position);
                itemTransform.SetParent(checkItemSlots[i + 1]);
                itemTransform.localPosition = Vector3.zero;
                itemTransform.localRotation = Quaternion.identity;
            }

            if(i != 0)
            {
                yield return standardDelay;
            }
        }

        // Clean first slot before loading new item
        CleanupSlot(checkItemSlots[0]);
        LoadCheckItem(ProtocolState.Instance.CurrentStepState.Value.Checklist[ProtocolState.Instance.CurrentCheckNum - 1], 0);
    }

    /// <summary>
    /// Closes the protocol if all steps are complete and the last checklist is signed off, otherwise opens a confirmation popup
    /// </summary>
    void CloseProtocol()
    {
        UIDriver.CloseProtocolCallback();
    }
    void SetupButtonEvents()
    {
        closeProtocolButton.selectEntered.AddListener(_ => CloseProtocol());
        checkItemButton.selectEntered.AddListener(_ => CheckItem());
        unCheckItemButton.selectEntered.AddListener(_ => UnCheckItem());
        signOffButton.selectEntered.AddListener(_ => SignOff());
        nextStepButton.selectEntered.AddListener(_ => NextStep());
        previousStepButton.selectEntered.AddListener(_ => PreviousStep());
    }

    void RemoveButtonEvents()
    {
        closeProtocolButton.selectEntered.RemoveAllListeners();
        checkItemButton.selectEntered.RemoveAllListeners();
        unCheckItemButton.selectEntered.RemoveAllListeners();
        signOffButton.selectEntered.RemoveAllListeners();
        nextStepButton.selectEntered.RemoveAllListeners();
        previousStepButton.selectEntered.RemoveAllListeners();
    }

    void SetupPopupEvents()
    {
        signOffPopupEventSO.OnYesButtonPressed.AddListener(() =>
        {
            SignOff(); 
            NextStep();
        });

        checklistIncompletePopupEventSO.OnYesButtonPressed.AddListener(() =>
        {
            UIDriver.StepNavigationCallback(ProtocolState.Instance.CurrentStep.Value + 1);
        });

        closeProtocolPopupEventSO.OnYesButtonPressed.AddListener(() =>
        {
            SceneLoader.Instance.LoadSceneClean("ProtocolMenu");
        });
    }

    void RemovePopupEvents()
    {
        signOffPopupEventSO.OnYesButtonPressed.RemoveAllListeners();
        checklistIncompletePopupEventSO.OnYesButtonPressed.RemoveAllListeners();
        closeProtocolPopupEventSO.OnYesButtonPressed.RemoveAllListeners();
    }

    Action DisposeVoice;
    
    //voice commands setup to emulate button presses
    void SetupVoiceCommands()
    {
        if(SpeechRecognizer.Instance == null)
        {
            Debug.LogWarning("SpeechRecognizer not found");
            return;
        }
        DisposeVoice = SpeechRecognizer.Instance.Listen(new Dictionary<string, Action>()
        {
            {"check", async () => 
            {
                checkItemButton.selectEntered.Invoke(null);
                await StartCoroutine(Wait());
                checkItemButton.selectExited.Invoke(null);                
            }},
            {"uncheck", async () => 
            {
                unCheckItemButton.selectEntered.Invoke(null);
                await StartCoroutine(Wait());
                unCheckItemButton.selectExited.Invoke(null);                
            }},
            {"sign", async () => 
            {
                signOffButton.selectEntered.Invoke(null);
                await StartCoroutine(Wait());
                signOffButton.selectExited.Invoke(null);                
            }},
            {"next", async () => 
            {
                nextStepButton.selectEntered.Invoke(null);
                await StartCoroutine(Wait());
                nextStepButton.selectExited.Invoke(null);                
            }},
            {"previous", async () => 
            {
                previousStepButton.selectEntered.Invoke(null);
                await StartCoroutine(Wait());
                previousStepButton.selectExited.Invoke(null);                
            }},
        });
    }

    private void ResetCooldown()
    {
        checkOnCooldown = false;
    }

    private IEnumerator Wait()
    {
        yield return new WaitForSeconds(0.5f);
    }

    // Add validation helper
    private bool ValidateChecklistOperation(string operation)
    {
        if (checkOnCooldown)
        {
            Debug.LogWarning($"{operation} on cooldown! Pressing too fast!");
            return false;
        }

        if (!ProtocolState.Instance?.HasCurrentChecklist() ?? true)
        {
            hudEventSO.DisplayHudWarning("No checklist available.");
            return false;
        }

        if (ProtocolState.Instance.CurrentStepState.Value.SignedOff.Value)
        {
            hudEventSO.DisplayHudWarning("Checklist already signed off.");
            return false;
        }

        if (ProtocolState.Instance.LockingTriggered.Value)
        {
            hudEventSO.DisplayHudWarning($"Cannot {operation} - checklist is locked.");
            return false;
        }

        return true;
    }

    // Improved cooldown handling
    private void StartCooldown()
    {
        checkOnCooldown = true;
        CancelInvoke(nameof(ResetCooldown));
        Invoke(nameof(ResetCooldown), COOLDOWN_DURATION);
    }

    // Add this helper method to clean a single slot
    private void CleanupSlot(Transform slot)
    {
        // Deactivate all children in the slot
        for (int i = slot.childCount - 1; i >= 0; i--)
        {
            var child = slot.GetChild(i);
            child.gameObject.SetActive(false);
            // Optionally return to original parent if needed
            child.SetParent(checkItemPool.Content);
        }
    }
}