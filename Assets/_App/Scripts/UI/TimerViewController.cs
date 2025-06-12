using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UniRx;

/// <summary>
/// Draggable TimerViewController
/// </summary>

public class TimerViewController : LLBasePanel
{
    [SerializeField] Material timerFlashMaterial;
    Material defaultMaterial;
    [SerializeField] MeshRenderer backplateMesh;
    [SerializeField] double StartingTime = 10.0f;
    [SerializeField] TextMeshProUGUI TimeDisplay;
    [SerializeField] GameObject StartButton;
    [SerializeField] GameObject StopButton;
    [SerializeField] GameObject ResetButton;
    [SerializeField] GameObject TimeSetControls;
    [SerializeField] AudioSource audioPlayer;

    private double TimeLeft;
    private bool timerRunning = false;

    protected override void Awake()
    {
        base.Awake();
        
        defaultMaterial = backplateMesh.material;
    }

    // Start is called before the first frame update
    void Start()
    {
        if (ProtocolState.Instance.HasCurrentChecklist())
        {
            var currentCheckItem = ProtocolState.Instance.CurrentCheckItemDefinition;
            // Look for timer action in the checklist item's arActions
            var timerAction = currentCheckItem.arActions?.Find(action => action.actionType == "timer");
            
            if (timerAction != null && timerAction.properties.ContainsKey("duration"))
            {
                // Get duration from properties and convert to double
                if (timerAction.properties["duration"] is long || timerAction.properties["duration"] is int || timerAction.properties["duration"] is double)
                {
                    TimeLeft = Convert.ToDouble(timerAction.properties["duration"]);
                    TimeDisplay.text = GetTimeString();
                }
            }
        }
        else
        {
            TimeLeft = StartingTime;
            TimeDisplay.text = GetTimeString();
        }
    }


    // Update is called once per frame
    void Update()
    {
        if(this.transform.lossyScale != new Vector3(0.5f,0.5f,0.5f))
        {
            transform.localScale = new Vector3(0.5f,0.5f,0.5f);
        }
        //if timer has not finished and is running decrement time
        if(TimeLeft > 0 & timerRunning)
        {
            TimeLeft -= Time.deltaTime;
            TimeDisplay.text = GetTimeString();
        }
        if(TimeLeft < 0 & timerRunning)
        {
            StopTimer();
            InvokeRepeating("FlashTimer", 0.0f, 1f);
        }
    }

    //Event functions

    public void IncrementHour()
    {
        if(StartingTime < 352800f)
        {
            StartingTime += 3600f;
            TimeLeft = StartingTime;
            TimeDisplay.text = GetTimeString();
        }
    }

    public void DecrementHour()
    {
        if(StartingTime > 3600f)
        {
            StartingTime -= 3600f;
            TimeLeft = StartingTime;
            TimeDisplay.text = GetTimeString();
        }
    }

    public void IncrementMinute()
    {
        if (StartingTime < 356340f)
        {
            StartingTime += 60f;
            TimeLeft = StartingTime;
            TimeDisplay.text = GetTimeString();
        }
    }

    public void DecrementMinute()
    {
        if(StartingTime > 60f)
        {
            StartingTime -= 60f;
            TimeLeft = StartingTime;
            TimeDisplay.text = GetTimeString();
        }
    }

    public void IncrementSecond()
    {
        if(StartingTime < 356399f)
        {
            StartingTime += 1f;
            TimeLeft = StartingTime;
            TimeDisplay.text = GetTimeString();
        }
    }

    public void DecrementSecond()
    {
        if(StartingTime > 1f)
        {
            StartingTime -= 1f;
            TimeLeft = StartingTime;
            TimeDisplay.text = GetTimeString();
        }
    }

    public void SetTimer()
    {
        StopTimer();
        StartingTime = TimeLeft;
        TimeSetControls.SetActive(!TimeSetControls.activeSelf);
        StartButton.SetActive(!TimeSetControls.activeSelf);
        StopButton.SetActive(!TimeSetControls.activeSelf);
        ResetButton.SetActive(!TimeSetControls.activeSelf);
        if (!TimeSetControls.activeSelf)
        {
            StopButton.SetActive(false);
        }
    }

    public void SetTimer(int timeSeconds)
    {
        TimeLeft = timeSeconds;
        TimeDisplay.text = GetTimeString();
    }

    public void ResetTimer()
    {
        TimeLeft = StartingTime;
        StopTimer();
        TimeDisplay.text = GetTimeString();
        backplateMesh.material = defaultMaterial;
        CancelInvoke("FlashTimer");
        audioPlayer.Stop();
    }

    public void StartTimer()
    {
        if(StartingTime <= 0 || timerRunning || TimeLeft <= 0)
        {
            return;
        }
        ServiceRegistry.GetService<ILighthouseControl>()?.StartTimer((int)StartingTime);
        timerRunning = true;
        StartButton.SetActive(false);
        StopButton.SetActive(true);
    }

    public void StopTimer()
    {
        timerRunning = false;
        StartButton.SetActive(true);
        StopButton.SetActive(false);
    }

    public void CloseTimer()
    {
        Destroy(gameObject);
    }

    public void RestartTimer()
    {
        ResetTimer();
        StartTimer();
    }

    //util
    private string GetTimeString()
    {
        double roundTime = System.Math.Round(TimeLeft, 1);
        TimeSpan ts = TimeSpan.FromSeconds(roundTime);
        return string.Format("{0:D2}:{1:D2}:{2:D2}",
                    ts.Hours,
                    ts.Minutes,
                    ts.Seconds);
    }

    // private void OnCheckItemChange()
    // {
    //     if(timerRunning)
    //     {
    //         return;
    //     }
    //     if(ProtocolState.Steps[ProtocolState.Step].Checklist != null)
    //     {
    //         var currentCheckItem = ProtocolState.procedureDef.steps[ProtocolState.Step].checklist[ProtocolState.CheckItem];
    //         if(currentCheckItem.activateTimer)
    //         {
    //             int timeSeconds = (currentCheckItem.hours * 60 * 60) + (currentCheckItem.minutes * 60) + currentCheckItem.seconds;
    //             StartingTime = timeSeconds;
    //             ResetTimer();
    //         }
    //     }
    // }

    private void FlashTimer()
    {
        backplateMesh.material = backplateMesh.material == defaultMaterial ? timerFlashMaterial : defaultMaterial;
        audioPlayer.Play();
    }
}
