using System;
using UnityEngine;
using UnityEngine.UIElements;

public class TimerWindowController : BaseWindowController
{
    private Label _displayLabel;
    private Button _startStopButton;
    private Button _clockButton;

    private IAudioService _audioService;

    // State
    private IVisualElementScheduledItem _timerUpdate;
    private IVisualElementScheduledItem _clockUpdate;
    private IVisualElementScheduledItem _alertFlasher;
    private IVisualElementScheduledItem _longPressTracker;
    private bool _longPressHandled = false;
    private bool _isTimerRunning = false;
    private bool _isClockMode = false;
    private TimeSpan _currentTime = TimeSpan.Zero;
    private readonly TimeSpan[] _presets = new TimeSpan[4];
    private const float LongPressDuration = 1.0f;
    private const string AlertClassName = "timer-alert";

    protected override void OnEnable()
    {
        base.OnEnable();
        _audioService = ServiceRegistry.GetService<IAudioService>();

        _displayLabel = rootVisualElement.Q<Label>("display-label");
        _startStopButton = rootVisualElement.Q<Button>("start-stop-button");
        _clockButton = rootVisualElement.Q<Button>("clock-button");

        RegisterButtonCallbacks();
        
        _timerUpdate = rootVisualElement.schedule.Execute(UpdateTimer).Every(100);
        _timerUpdate.Pause();

        _clockUpdate = rootVisualElement.schedule.Execute(UpdateClock).Every(1000);
        _clockUpdate.Pause();
        
        _alertFlasher = rootVisualElement.schedule.Execute(ToggleAlertFlash).Every(700);
        _alertFlasher.Pause();
        
        UpdateDisplay();
    }

    private void RegisterButtonCallbacks()
    {
        // Time setting
        rootVisualElement.Q<Button>("h-button")?.RegisterCallback<ClickEvent>(evt => AddTime(TimeSpan.FromHours(1), evt));
        rootVisualElement.Q<Button>("m-button")?.RegisterCallback<ClickEvent>(evt => AddTime(TimeSpan.FromMinutes(1), evt));
        rootVisualElement.Q<Button>("s-button")?.RegisterCallback<ClickEvent>(evt => AddTime(TimeSpan.FromSeconds(1), evt));
        rootVisualElement.Q<Button>("c-button")?.RegisterCallback<ClickEvent>(ClearTime);

        // Controls
        _startStopButton.text = "START";
        _clockButton.text = "CLOCK";
        _startStopButton?.RegisterCallback<ClickEvent>(ToggleTimer);
        _clockButton?.RegisterCallback<ClickEvent>(ToggleClockMode);

        // Presets
        for (int i = 0; i < 4; i++)
        {
            int index = i; // Local copy for the closure
            var button = rootVisualElement.Q<Button>($"t{i + 1}-button");
            
            button?.RegisterCallback<PointerDownEvent>(evt => OnPresetPointerDown(index, evt, button), TrickleDown.TrickleDown);
            button?.RegisterCallback<PointerUpEvent>(evt => OnPresetPointerUp(index, evt), TrickleDown.TrickleDown);
            button?.RegisterCallback<PointerLeaveEvent>(evt => OnPresetPointerLeave(evt));
        }
    }
    
    private void OnPresetPointerDown(int index, PointerDownEvent evt, VisualElement button)
    {
        if (evt.button != 0) return; // Only process left-clicks
        
        _longPressHandled = false; // Reset on new press.
        
        Vector3 buttonPosition = button.worldBound.center;

        _longPressTracker = rootVisualElement.schedule.Execute(() => {
            _longPressHandled = true;
            // Long press: Save preset
            _presets[index] = _currentTime;
            _audioService?.PlayBeep(buttonPosition);
        }).StartingIn((long)(LongPressDuration * 1000));
    }

    private void OnPresetPointerUp(int index, PointerUpEvent evt)
    {
        if (evt.button != 0) return; // Only process left-clicks
        
        _longPressTracker?.Pause();

        if (_longPressHandled)
        {
            return; // Long press was handled, do nothing more.
        }
        
        // This was a short press.
        StopAlert();
        PlayClickSound(evt);
        _currentTime = _presets[index];
        StopEverything();
        UpdateDisplay();
    }
    
    private void OnPresetPointerLeave(PointerLeaveEvent evt)
    {
        // If the pointer leaves the button, cancel the long press timer.
        _longPressTracker?.Pause();
    }

    private void AddTime(TimeSpan amount, ClickEvent evt)
    {
        StopAlert();
        PlayClickSound(evt);
        StopEverything();
        _currentTime += amount;
        if (_currentTime.TotalHours >= 100)
        {
            _currentTime = new TimeSpan(99, 59, 59);
        }
        UpdateDisplay();
    }

    private void ClearTime(ClickEvent evt)
    {
        StopAlert();
        PlayClickSound(evt);
        StopEverything();
        _currentTime = TimeSpan.Zero;
        UpdateDisplay();
    }

    private void ToggleTimer(ClickEvent evt)
    {
        StopAlert();
        PlayClickSound(evt);
        if (_isClockMode)
        {
            _isClockMode = false;
            _clockUpdate.Pause();
            _clockButton.text = "CLOCK";
        }

        if (_currentTime <= TimeSpan.Zero) return;

        _isTimerRunning = !_isTimerRunning;
        if (_isTimerRunning)
        {
            _timerUpdate.Resume();
            _startStopButton.AddToClassList("running");
            _startStopButton.text = "STOP";
        }
        else
        {
            _timerUpdate.Pause();
            _startStopButton.RemoveFromClassList("running");
            _startStopButton.text = "START";
        }
    }
    
    private void ToggleClockMode(ClickEvent evt)
    {
        StopAlert();
        PlayClickSound(evt);
        _isClockMode = !_isClockMode;
        
        if (_isClockMode)
        {
            _clockUpdate.Resume();
            UpdateClock();
            _clockButton.text = "TIMER";
        }
        else
        {
            _clockUpdate.Pause();
            UpdateDisplay(); // Revert to showing the timer time
            _clockButton.text = "CLOCK";
        }
    }
    
    private void StopEverything()
    {
        _isTimerRunning = false;
        _isClockMode = false;
        _timerUpdate.Pause();
        _clockUpdate.Pause();
        _startStopButton.RemoveFromClassList("running");
        _startStopButton.text = "START";
        _clockButton.text = "CLOCK";
    }

    private void UpdateTimer()
    {
        _currentTime -= TimeSpan.FromMilliseconds(100);

        if (_currentTime <= TimeSpan.Zero)
        {
            _currentTime = TimeSpan.Zero;
            _isTimerRunning = false;
            _timerUpdate.Pause();
            _startStopButton.RemoveFromClassList("running");
            _startStopButton.text = "START";
            
            if (_isClockMode)
            {
                _isClockMode = false;
                _clockUpdate.Pause();
                _clockButton.text = "CLOCK";
            }
            
            _alertFlasher.Resume();
        }
        UpdateDisplay();
    }

    private void UpdateClock()
    {
        DateTime now = DateTime.Now;
        _displayLabel.text = $"{now.Hour:D2}:{now.Minute:D2}:{now.Second:D2}";
    }

    private void UpdateDisplay()
    {
        if (!_isClockMode)
        {
             _displayLabel.text = $"{(int)_currentTime.TotalHours}:{_currentTime.Minutes:D2}:{_currentTime.Seconds:D2}";
        }
    }

    private void PlayClickSound(EventBase evt)
    {
        if (evt?.currentTarget is VisualElement element)
        {
            _audioService?.PlayButtonPress(element.worldBound.center);
        }
    }

    private void ToggleAlertFlash()
    {
        _audioService?.PlayAlarm();
        rootVisualElement.Q(className: "timer-container").ToggleInClassList(AlertClassName);
    }

    private void StopAlert()
    {
        _alertFlasher.Pause();
        rootVisualElement.Q(className: "timer-container").RemoveFromClassList(AlertClassName);
    }

    public void SetInitialTime(int seconds)
    {
        StopEverything();
        _currentTime = TimeSpan.FromSeconds(seconds);
        UpdateDisplay();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        _timerUpdate?.Pause();
        _clockUpdate?.Pause();
        _alertFlasher?.Pause();
    }
} 