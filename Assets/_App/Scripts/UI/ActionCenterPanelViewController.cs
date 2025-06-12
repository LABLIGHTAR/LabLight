using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.UI;

/// <summary>
/// Controls the behavior of the action center panel in the scene.
/// </summary>
public class ActionCenterPanelViewController : MonoBehaviour
{
    [SerializeField] private HeadPlacementEventChannel headPlacementEventChannel;
    [SerializeField] XRSimpleInteractable recordingButton;
    bool isRecording = false;
    [SerializeField] XRSimpleInteractable replayButton;
    bool isReplaying = false;
    [SerializeField] XRSimpleInteractable internetBrowserButton;
    [SerializeField] XRSimpleInteractable chatButton;
    [SerializeField] XRSimpleInteractable timerButton;
    [SerializeField] XRSimpleInteractable calculatorButton;
    [SerializeField] XRSimpleInteractable closeAppButton;
   
    bool isSpeechRecognitionEnabled = true;
    [SerializeField] XRSimpleInteractable speechRecognitionButton;
    [SerializeField] Image speechRecognitionButtonImage;
    [SerializeField] Sprite speechRecognitionButtonImageEnabled;
    [SerializeField] Sprite speechRecognitionButtonImageDisabled;

    void Start()
    {
        recordingButton.selectExited.AddListener(_ => ToggleLighthouseRecording());
        replayButton.selectExited.AddListener(_ => ToggleLighthouseReplay());
        internetBrowserButton.selectExited.AddListener(_ => ServiceRegistry.GetService<IUIDriver>().DisplayWebPage(""));
        calculatorButton.selectExited.AddListener(_ => ServiceRegistry.GetService<IUIDriver>().DisplayCalculator());
        chatButton.selectExited.AddListener(_ => ServiceRegistry.GetService<IUIDriver>().DisplayLLMChat());
        timerButton.selectExited.AddListener(_ => ServiceRegistry.GetService<IUIDriver>().DisplayTimer(30));
        closeAppButton.selectExited.AddListener(_ => Application.Quit());
        speechRecognitionButton.selectExited.AddListener(_ => ToggleSpeechRecognition());
    }

    /// <summary>
    /// Starts the calibration scene additively and deactivates the action center panel.
    /// </summary>
    public void StartCalibration()
    {
        SceneLoader.Instance.LoadSceneAdditive("Calibration");
    }

    public void OpenSettings()
    {
        SceneLoader.Instance.LoadSceneAdditive("Settings");
    }

    public void OpenSpatialNotesEditor()
    {
        this.gameObject.SetActive(false);
        SceneLoader.Instance.LoadSceneAdditive("SpatialNotesEditor");
    }    

    /// <summary>
    /// Toggles the visibility of generic visualizations for AR objects.
    /// </summary>
    public void ToggleTestARObjects()
    {
        SessionState.enableGenericVisualizations.Value = !SessionState.enableGenericVisualizations.Value;
    }

    void ToggleLighthouseRecording()
    {
        if (isRecording)
        {
            ServiceRegistry.GetService<ILighthouseControl>().StopRecordingVideo();
        }
        else
        {
            ServiceRegistry.GetService<ILighthouseControl>().StartRecordingVideo();
        }
        isRecording = !isRecording;
    }

    void ToggleLighthouseReplay()
    {
        if (isReplaying)
        {
            ServiceRegistry.GetService<ILighthouseControl>().StopPlayingVideo();
        }
        else
        {
            ServiceRegistry.GetService<ILighthouseControl>().StartPlayingVideo();
        }
        isReplaying = !isReplaying;
    }

    void ToggleSpeechRecognition()
    {
        isSpeechRecognitionEnabled = !isSpeechRecognitionEnabled;
        speechRecognitionButtonImage.sprite = isSpeechRecognitionEnabled ? speechRecognitionButtonImageDisabled : speechRecognitionButtonImageEnabled;
        if (isSpeechRecognitionEnabled)
        {
            SpeechRecognizer.Instance.EnableSpeechRecognition();
        }
        else
        {
            SpeechRecognizer.Instance.DisableSpeechRecognition();
        }
    }
}
