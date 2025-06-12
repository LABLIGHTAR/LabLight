using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class FootPedalController : MonoBehaviour
{
    public ChecklistPanelViewController checklist;

    private float footPedalInterval = 0;
    private float lastFootPedalActionTime = 0;

    private static float PEDALDELAY;

    // private InputAction nextProcedureAction;
    // private InputAction previousProcedureAction;

    void Start()
    {
        if(Application.isEditor)
        {
            PEDALDELAY = 0.5f;
        }
        else
        {
            PEDALDELAY = 1.5f;
        }

        // nextProcedureAction = InputSystem.actions.FindAction("Progress Forward");
        // previousProcedureAction = InputSystem.actions.FindAction("Progress Backward");
    }

    void OnEnable()
    {
        InputSystem.actions.FindAction("Progress Forward").performed += NextProtocolAction;
        InputSystem.actions.FindAction("Progress Backward").performed += PreviousProtocolAction;
    }

    void OnDisable()
    {
        InputSystem.actions.FindAction("Progress Forward").performed -= NextProtocolAction;
        InputSystem.actions.FindAction("Progress Backward").performed -= PreviousProtocolAction;
    }

    // Airturn Duo 500 acts as a keyboard
    // https://manuals.plus/airturn/duo-500-bluetooth-pedal-controller-manual#ixzz87lTHS9j6
    //          Switch 1	    Switch 2	    Switch 3	    Switch 4	    Switch 5
    // Mode 1	AirDirect
    // Mode 2	Up Arrow        Left Arrow      Down Arrow      Right Arrow     Enter
    // Mode 3	Page Up         Left Arrow      Page Down       Right Arrow     Enter
    // Mode 4	Volume Up       Previous Track  Volume Down     Next Track      Play/Pause
    // Mode 5	Space           Left Click      Enter           Right Click     Home
    // Mode 6	3	            p               m               Shift +R        Space
    // Mode 7	Damper          Portamento      Sostenuto       Soft Pedal      Legato
    
    // void Update()
    // {
    //     footPedalInterval = Time.time;
    //     if(nextProcedureAction.IsPressed())
    //     {
    //         if(lastFootPedalActionTime == 0 || footPedalInterval - lastFootPedalActionTime > PEDALDELAY)
    //         {
    //             footPedalInterval = Time.time;
    //             NextProcedureAction();
    //         }else
    //         {
    //             Debug.Log("Foot pedal delay");
    //         }
    //     }

    //     if(previousProcedureAction.IsPressed())
    //     {
    //         if(lastFootPedalActionTime == 0 || footPedalInterval - lastFootPedalActionTime > PEDALDELAY)
    //         {
    //             footPedalInterval = Time.time;
    //             PreviousProcedureAction();
    //         }else
    //         {
    //             Debug.Log("Foot pedal delay");
    //         }
    //     }
    // }

    public void NextProtocolAction(InputAction.CallbackContext context)
    {
        if(context.performed)
        {
            Debug.Log("Next protocol action");
            footPedalInterval = Time.time;
            // if(ProtocolState.LockingTriggered.Value)
            // {
            //     lockingDisplay.NextAction();
            // }
            // else if(ProtocolState.AlignmentTriggered.Value)
            // {
            //     ProtocolState.AlignmentTriggered.Value = false;
            //     AlignmentController.ResetModel(lockingDisplay.GetSpecificArViews().Where(arview => arview.Key.arDefinitionType == ArDefinitionType.Model).Select(x => x.Value).ToList());
            // }
            if(ProtocolState.Instance.HasCurrentChecklist())
            {
                if(ProtocolState.Instance.CurrentStepState.Value.SignedOff.Value)
                {
                    checklist.NextStep();
                }else if(ProtocolState.Instance.CurrentCheckNum == ProtocolState.Instance.CurrentChecklist.Count - 1 && ProtocolState.Instance.CurrentCheckItemState.Value.IsChecked.Value && !ProtocolState.Instance.CurrentStepState.Value.SignedOff.Value)
                {
                    checklist.SignOff();
                }else
                {
                    checklist.CheckItem();
                }
            }else 
            {
                checklist.NextStep();
            }
        }
    }

    public void PreviousProtocolAction(InputAction.CallbackContext context)
    {
        if(context.performed)
        {
            Debug.Log("Previous Procedure Action");
            if(ProtocolState.Instance.CurrentCheckNum == 0 || ProtocolState.Instance.CurrentStepState.Value.SignedOff.Value)
            {
                checklist.PreviousStep();
            }else
            {
                checklist.UnCheckItem();
            }
        }
    }
}