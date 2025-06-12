using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Globalization;

public class DashboardHomeComponent : VisualElement
{
    private Label _greetingLabel;
    private Label _userNameLabel;
    private Label _timeLabel;

    public DashboardHomeComponent(VisualTreeAsset asset)
    {
        asset.CloneTree(this);

        _greetingLabel = this.Q<Label>("greeting-label");
        _userNameLabel = this.Q<Label>("user-name-label");
        _timeLabel = this.Q<Label>("time-label");

        RegisterCallback<AttachToPanelEvent>(OnAttach);
        RegisterCallback<DetachFromPanelEvent>(OnDetach);

        UpdateAllFields();
    }

    private void OnAttach(AttachToPanelEvent evt)
    {
        // Start any continuous updates, like the time
        schedule.Execute(UpdateTime).Every(1000); 
    }

    private void OnDetach(DetachFromPanelEvent evt)
    {
        // Stop updates when not visible
    }

    public void UpdateAllFields()
    {
        UpdateGreeting();
        UpdateUserName();
        UpdateTime();
    }

    private void UpdateTime()
    {
        if (_timeLabel != null)
        {
            _timeLabel.text = DateTime.Now.ToString("h:mm tt", CultureInfo.InvariantCulture).ToUpper();
        }
    }

    private void UpdateGreeting()
    {
        if (_greetingLabel != null)
        {
            var hour = DateTime.Now.Hour;
            if (hour < 12)
                _greetingLabel.text = "Good morning,";
            else if (hour < 18)
                _greetingLabel.text = "Good afternoon,";
            else
                _greetingLabel.text = "Good evening,";
        }
    }

    public void UpdateUserName(LocalUserProfileData userProfile = null)
    {
        if (_userNameLabel != null)
        {
            var profileToUse = userProfile ?? SessionState.currentUserProfile;
            _userNameLabel.text = profileToUse?.Name ?? "User";
        }
    }
} 