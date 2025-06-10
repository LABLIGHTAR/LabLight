using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "SettingsManager", menuName = "ScriptableObjects/SettingsManager", order = 3)]
public class SettingsManagerScriptableObject : ScriptableObject
{
    public bool defaultSettingValue = false;
    //private Dictionary<LablightSettings, bool> availableSettings = new Dictionary<LablightSettings, bool>();

    public UnityEvent<Tuple<LablightSettings, bool>> settingChanged = new UnityEvent<Tuple<LablightSettings, bool>>();
    //set up so settings manager holds all settings, scriptable objects cannot hold settings
    void Awake()
    {
        if(settingChanged == null)
        {
            settingChanged = new UnityEvent<Tuple<LablightSettings, bool>>();
        }
    }

    public void SetSetting(LablightSettings setting, bool value)
    {
        settingChanged.Invoke(new Tuple<LablightSettings, bool>(setting, value));
    }

    public void SetAllSettings(bool value)
    {
        foreach(LablightSettings setting in LablightSettings.GetValues(typeof(LablightSettings)))
        {
            settingChanged.Invoke(new Tuple<LablightSettings, bool>(setting, value));
        }
    }

    public bool GetSettingValue(LablightSettings setting)
    {
        return SettingsManager.instance.getSettingValue(setting);
    }

    // public void SetSetting(LablightSettings setting, bool value)
    // {
    //     Debug.Log("Updating settings: " + setting.ToString() + " to " + value.ToString());
    //     if (settingsDictionary.ContainsKey(setting))
    //     {
    //         settingChanged.Invoke(new Tuple<LablightSettings, bool>(setting, value));
    //         settingsDictionary[setting] = value;
    //     }
    //     else
    //     {
    //         settingsDictionary.Add(setting, value);
    //     }
    // }

    // public void SetAllSettings(bool value)
    // {
    //     foreach (var setting in settingsDictionary)
    //     {
    //         settingChanged.Invoke(new Tuple<LablightSettings, bool>(setting.Key, value));
    //         settingsDictionary[setting.Key] = value;
    //     }
    // }

    // public bool GetSettingValue(LablightSettings setting)
    // {
    //     if (settingsDictionary.ContainsKey(setting))
    //     {
    //         return settingsDictionary[setting];
    //     }
    //     else
    //     {
    //         Debug.Log("Setting not found: " + setting.ToString());
    //         return false;
    //     }
    // }
}
