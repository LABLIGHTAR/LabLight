using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using MoreMountains.Feedbacks;
using TMPro;
using UniRx;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
public class SettingsMenuButton : MonoBehaviour
{

    private LablightSettings setting;
    public TextMeshProUGUI settingName;

    public SettingsManagerScriptableObject settingsManagerSO;

    XRSimpleInteractable interactable;

    [SerializeField]
    private GameObject _offIndicator = null;
    
    [SerializeField]
    private GameObject _onIndicator = null;

    [SerializeField] MMF_Player animationPlayer;
    
    // Start is called before the first frame update
    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        if (_offIndicator == null || _onIndicator == null)
        {
            throw new MissingReferenceException("OffIndicator and OnIndicator fields in CalibrationStatusBehavior class cannot be null.");
        }
    }

    void OnEnable()
    {
        animationPlayer.PlayFeedbacks();
    }

    void OnDisable()
    {
        interactable.selectExited.RemoveListener(_ => {
            settingsManagerSO.SetSetting(setting, !settingsManagerSO.GetSettingValue(setting));
        });
        settingsManagerSO.settingChanged.RemoveListener(value => UpdateVisualState(value.Item1));
    }

    // Update is called once per frame
    public void Initialize(LablightSettings setting)
    {
        this.setting = setting;
        settingName.text = setting.ToString().Replace("_", " ");
        _offIndicator.SetActive(!settingsManagerSO.GetSettingValue(setting));
        _onIndicator.SetActive(settingsManagerSO.GetSettingValue(setting));

        settingsManagerSO.settingChanged.AddListener(value => UpdateVisualState(value.Item1));

        interactable.selectExited.AddListener(_ => {
            settingsManagerSO.SetSetting(setting, !settingsManagerSO.GetSettingValue(setting));
        });
    }

    private void UpdateVisualState(LablightSettings setting)
    {
        if(this.setting == setting && _offIndicator != null && _onIndicator != null)
        {
            _offIndicator.SetActive(!settingsManagerSO.GetSettingValue(setting));
            _onIndicator.SetActive(settingsManagerSO.GetSettingValue(setting));
        }
    }
}
