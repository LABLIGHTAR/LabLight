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
    [SerializeField] GameObject noChecklistText;
    [SerializeField] AudioSource audioPlayer;

    private List<ProtocolState.CheckItemState> prevChecklist;

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

    private void Awake()
    {
        base.Awake();
        checkItemPool = GetComponent<CheckItemPool>();
        ProtocolState.stepStream.Subscribe(_ => StartCoroutine(LoadChecklist())).AddTo(this);
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
    }

    void Start()
    {
        checkItemPool.CreatePooledObjects();
        StartCoroutine(LoadChecklist());

        popupPanelViewController = GameObject.FindFirstObjectByType<PopupPanelViewController>(FindObjectsInactive.Include);
    }

    /// <summary>
    /// Checks the next unchecked item if possible
    /// </summary>
    public void CheckItem()
    {
        if(checkOnCooldown == true)
        {
            Debug.LogWarning("Check on cooldown! Pressing too fast!");
            return;
        }

        if (ProtocolState.Steps[ProtocolState.Step].Checklist == null || ProtocolState.Steps[ProtocolState.Step].SignedOff)
        {
            Debug.LogWarning("Already signed off or no item to check");
            hudEventSO.DisplayHudWarning("Checklist already signed off.");
            return;
        }

        var firstUncheckedItem = (from item in ProtocolState.Steps[ProtocolState.Step].Checklist
                                  where !item.IsChecked.Value
                                  select item).FirstOrDefault();

        if (firstUncheckedItem == null || ProtocolState.LockingTriggered.Value)
        {
            Debug.LogWarning("No item to check or locking triggered");
            hudEventSO.DisplayHudWarning("No item to check.");
            return;
        }

        // Check the item and log timestamp
        firstUncheckedItem.IsChecked.Value = true;
        firstUncheckedItem.CompletionTime = DateTime.Now;

        // Increment checkItem if this is not the last check item
        if (ProtocolState.CheckItem < ProtocolState.Steps[ProtocolState.Step].Checklist.Count - 1)
        {
            //if there are more items to display scroll the checklist up
            if(ProtocolState.Steps[ProtocolState.Step].Checklist.Count > 5 && ProtocolState.CheckItem > 0 && ProtocolState.CheckItem + 4 < ProtocolState.Steps[ProtocolState.Step].Checklist.Count)
            {
                StartCoroutine(ScrollChecklistUp());
            }
            
            ProtocolState.SetCheckItem(ProtocolState.CheckItem + 1);
        }
        else
        {
            ProtocolState.SetCheckItem(ProtocolState.CheckItem);
        }

        Invoke("ResetCooldown", 1f);
        checkOnCooldown = true;
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

        if (ProtocolState.Steps[ProtocolState.Step].Checklist == null || ProtocolState.Steps[ProtocolState.Step].SignedOff)
        {
            Debug.LogWarning("Already signed off or no item to uncheck");
            hudEventSO.DisplayHudWarning("Checklist already signed off.");
            return;
        }

        var lastCheckedItem = (from item in ProtocolState.Steps[ProtocolState.Step].Checklist
                               where item.IsChecked.Value
                               select item).LastOrDefault();

        if (lastCheckedItem == null || ProtocolState.LockingTriggered.Value)
        {
            Debug.LogWarning("No item to uncheck or locking triggered");
            hudEventSO.DisplayHudWarning("No item to uncheck.");
            return;
        }

        // Uncheck the item
        lastCheckedItem.IsChecked.Value = false;

        if(ProtocolState.CheckItem > 1 && ProtocolState.Steps[ProtocolState.Step].Checklist.Count > 5 && ProtocolState.CheckItem + 3 < ProtocolState.Steps[ProtocolState.Step].Checklist.Count)
        {
            StartCoroutine(ScrollChecklistDown());
        }

        ProtocolState.SetCheckItem(ProtocolState.Steps[ProtocolState.Step].Checklist.IndexOf(lastCheckedItem));

        Invoke("ResetCooldown", 1f);
        checkOnCooldown = true;
    }

    /// <summary>
    /// Locks the checklist from further change if all items are complete
    /// </summary>
    public void SignOff()
    {
        if (ProtocolState.Steps[ProtocolState.Step].Checklist == null || ProtocolState.Steps[ProtocolState.Step].SignedOff)
        {
            Debug.LogWarning("Already signed off or no checklist for this step");
            hudEventSO.DisplayHudWarning("Checklist already signed off.");
            return;
        }

        var uncheckedItemsCount = (from item in ProtocolState.Steps[ProtocolState.Step].Checklist
                                   where !item.IsChecked.Value
                                   select item).Count();

        if (uncheckedItemsCount != 0)
        {
            Debug.LogWarning("Not all items are checked");
            hudEventSO.DisplayHudWarning("Cannot sign off, not all items are checked.");
            return;
        }

        audioPlayer.Play();
        //update protocol state
        ProtocolState.SignOff();
        //lock sign off indicator in UI
        lockedIcon.SetActive(true);
        unlockedIcon.SetActive(false);
        //write checklist for this step to CSV
        WriteChecklistToCSV(ProtocolState.Steps[ProtocolState.Step].Checklist);
    }

    /// <summary>
    /// Navigates to the previous step if possible
    /// </summary>
    public void PreviousStep()
    {
        if (ProtocolState.LockingTriggered.Value)
        {
            Debug.LogWarning("cannot navigate to previous step: locking in progress");
            return;
        }
        if(ProtocolState.Step == 0)
        {
            return;
        }
        ProtocolState.SetStep(ProtocolState.Step - 1);
    }

    /// <summary>
    /// Navigates to the next step if possible, displays error message if checklist is incomplete
    /// </summary>
    public void NextStep()
    {
        // If there is no checklist or the checklist is already signed off, proceed to the next step
        if (ProtocolState.Steps[ProtocolState.Step].Checklist == null || ProtocolState.Steps[ProtocolState.Step].SignedOff)
        {
            if (ProtocolState.LockingTriggered.Value)
            {
                Debug.LogWarning("cannot navigate to next step: locking in progress");
                return;
            }
            ProtocolState.SetStep(ProtocolState.Step + 1);
            return;
        }

        // If all items are checked but the checklist is not signed off, show sign off confirmation panel
        if (ProtocolState.CheckItem == ProtocolState.Steps[ProtocolState.Step].Checklist.Count - 1 &&
            ProtocolState.Steps[ProtocolState.Step].Checklist[ProtocolState.CheckItem].IsChecked.Value)
        {
            // Update confirmation panel UI and button controls
            Debug.LogWarning("trying to go to next step without signing off");
            popupPanelViewController.DisplayPopup(signOffPopupEventSO);
            return;
        }

        // If not all items are checked, show checklist incomplete confirmation panel
        Debug.LogWarning("trying to go to the next step without checking all items");
        popupPanelViewController.DisplayPopup(checklistIncompletePopupEventSO);
    }

    void LoadStepText()
    {
        stepText.text = (ProtocolState.Step + 1) + " / " + ProtocolState.Steps.Count;
    }
    
    /// <summary>
    /// Loads the checklist for the current step, displays 5 most relevant items, updates the signoff icon
    /// </summary>
    IEnumerator LoadChecklist()
    {
        LoadStepText();

        foreach(var item in checkItemPool.pooledObjects)
        {
            item.SetActive(false);
        }

        //display proper signoff icon
        if (ProtocolState.Steps[ProtocolState.Step].SignedOff)
        {
            lockedIcon.SetActive(true);
            unlockedIcon.SetActive(false);
        }
        else
        {
            lockedIcon.SetActive(false);
            unlockedIcon.SetActive(true);
        }

        if(ProtocolState.Steps[ProtocolState.Step].Checklist != null)
        {
            noChecklistText.SetActive(false);
            
            List<ProtocolState.CheckItemState> relevantCheckItems = new List<ProtocolState.CheckItemState>();

            //get relevant check items
            //case for loading checklist from the start
            if(ProtocolState.CheckItem == 0)
            {
                for(int i = 0; i < 5; i++)
                {
                    if(i >= ProtocolState.Steps[ProtocolState.Step].Checklist.Count)
                    {
                        break;
                    }
                    relevantCheckItems.Add(ProtocolState.Steps[ProtocolState.Step].Checklist[i]);
                }
            }
            //case for loading checklist from the middle
            else
            {
                for(int i = ProtocolState.CheckItem - 1; i < ProtocolState.CheckItem + 4; i++)
                {
                    if(i >= ProtocolState.Steps[ProtocolState.Step].Checklist.Count)
                    {
                        break;
                    }
                    relevantCheckItems.Add(ProtocolState.Steps[ProtocolState.Step].Checklist[i]);
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
        //grab a view from the pool and initalize it with the proper data
        var checkItemView = checkItemPool.GetPooledObject().GetComponent<CheckitemView>();
        checkItemView.InitalizeCheckItem(checkItemData);

        //child the view to the proper slot and reset its position and rotation
        checkItemView.transform.SetParent(checkItemSlots[slotIndex]);
        checkItemView.transform.localPosition = Vector3.zero;
        checkItemView.transform.localRotation = Quaternion.identity;

        //activate the view and play the scale up animation
        checkItemView.gameObject.SetActive(true);
        checkItemView.PlayScaleUpAnimation();
    }

    /// <summary>
    /// Scrolls the checklist up by one item
    /// </summary>
    IEnumerator ScrollChecklistUp()
    {
        //scale down the first item
        checkItemSlots[0].GetChild(0).GetComponent<CheckitemView>().PlayScaleDownAnimation();

        yield return new WaitForSeconds(0.2f);

        for(int i = 1; i < 5; i++)
        {
            //move the items up
            checkItemSlots[i].GetChild(0).GetComponent<CheckitemView>().PlayMoveUpAnimation(checkItemSlots[i].position, checkItemSlots[i - 1].position);
            checkItemSlots[i].GetChild(0).SetParent(checkItemSlots[i - 1]);
            checkItemSlots[i-1].GetChild(0).localPosition = Vector3.zero;
            checkItemSlots[i-1].GetChild(0).localRotation = Quaternion.identity;

            if(i != 4)
            {
                yield return new WaitForSeconds(0.2f);
            }
        }
        //load the next item
        LoadCheckItem(ProtocolState.Steps[ProtocolState.Step].Checklist[ProtocolState.CheckItem + 3], 4);
    }

    /// <summary>
    /// Scrolls the checklist down by one item
    /// </summary>
    IEnumerator ScrollChecklistDown()
    {
        //scale down the last item
        checkItemSlots[4].GetChild(0).GetComponent<CheckitemView>().PlayScaleDownAnimation();

        yield return new WaitForSeconds(0.2f);

        for(int i = 3; i >= 0; i--)
        {
            //move the items down
            checkItemSlots[i].GetChild(0).GetComponent<CheckitemView>().PlayMoveDownAnimation(checkItemSlots[i].position, checkItemSlots[i + 1].position);
            checkItemSlots[i].GetChild(0).SetParent(checkItemSlots[i + 1]);
            checkItemSlots[i+1].GetChild(0).localPosition = Vector3.zero;
            checkItemSlots[i+1].GetChild(0).localRotation = Quaternion.identity;

            if(i != 0)
            {
                yield return new WaitForSeconds(0.2f);
            }
        }
        
        //load the previous item
        LoadCheckItem(ProtocolState.Steps[ProtocolState.Step].Checklist[ProtocolState.CheckItem - 1], 0);
    }

    /// <summary>
    /// Closes the protocol if all steps are complete and the last checklist is signed off, otherwise opens a confirmation popup
    /// </summary>
    void CloseProtocol()
    {
        var lastStepWithChecklist = ProtocolState.Steps.Where(step => step.Checklist != null).LastOrDefault();
            
        //if we are on the last step and the last checklist has been signed off close the protocol
        if(ProtocolState.Step == (ProtocolState.Steps.Count - 1) && lastStepWithChecklist != null && lastStepWithChecklist.SignedOff)
        {
            SessionState.Instance.activeProtocol = null;
            ProtocolState.procedureDef = null;
            SceneLoader.Instance.LoadSceneClean("ProtocolMenu");   
        }
        //open a popup to confirm closing the protocol
        else
        {
            popupPanelViewController.DisplayPopup(closeProtocolPopupEventSO);
        }
    }

    /// <summary>
    /// Writes the checklist to a CSV file for export
    /// </summary>
    public void WriteChecklistToCSV(List<ProtocolState.CheckItemState> CheckList)
    {
        if (ProtocolState.CsvPath == null)
        {
            Debug.LogWarning("no CSV file initalized");
            return;
        }

        // Initalize text writer to specified file and append
        var tw = new StreamWriter(ProtocolState.CsvPath, true);
        string line;

        // Write checklist items to CSV
        if (CheckList != null)
        {
            Debug.Log("######LABLIGHT Writing checklist to CSV " + ProtocolState.CsvPath);
            foreach (var item in CheckList)
            {
                line = item.Text;
                if (line.Contains(","))
                {
                    line = line.Replace(",", "");
                }

                tw.WriteLine(line + ',' + "Completed, " + item.CompletionTime);
            }
            tw.Close();
        }
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
            ProtocolState.SetStep(ProtocolState.Step + 1);
        });

        closeProtocolPopupEventSO.OnYesButtonPressed.AddListener(() =>
        {
            SessionState.Instance.activeProtocol = null;
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
}
