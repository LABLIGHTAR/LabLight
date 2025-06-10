using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;


public class PopupPanelViewController : MonoBehaviour
{
    //events are passed through a scriptable object to decrease coupling between classes
    //different popup panels can use different scriptable objects to communicate with other classes
    //listening classes simply need to subscribe to the events on the associated scriptable object
    [SerializeField] SettingsManagerScriptableObject settingsManagerSO;
    [SerializeField] PopupEventSO popupEventSO;

    [SerializeField] TextMeshProUGUI popupHeaderText;
    [SerializeField] TextMeshProUGUI popupText;


    [SerializeField] XRSimpleInteractable yesButton;
    [SerializeField] XRSimpleInteractable noButton;

    [SerializeField] AudioSource audioSource;
    
    // Start is called before the first frame update
    void Start()
    {
        yesButton.selectEntered.AddListener(_ => 
        {
            transform.GetChild(0).transform.gameObject.SetActive(false);
            popupEventSO.Yes();
        });
        
        noButton.selectEntered.AddListener(_ => 
        {
            transform.GetChild(0).transform.gameObject.SetActive(false);
            popupEventSO.No();
        });
    }

    public void DisplayPopup(PopupEventSO newSO)
    {
        popupEventSO = newSO;

        if(settingsManagerSO.GetSettingValue(LablightSettings.Popups) == false)
        {
            yesButton.selectEntered.Invoke(null);
            yesButton.selectExited.Invoke(null);
            return;
        }

        popupHeaderText.text = newSO.popupType.ToString();
        popupText.text = newSO.popupText;

        transform.GetChild(0).transform.gameObject.SetActive(true);
        audioSource.Play();
    }
}
