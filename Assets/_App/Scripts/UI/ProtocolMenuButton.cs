using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.SceneManagement;
using TMPro;
using UniRx;
using MoreMountains.Feedbacks;
using Newtonsoft.Json;

/// <summary>
/// Represents a button in the protocol menu.
/// </summary>
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
public class ProtocolMenuButton : MonoBehaviour
{
    private ProtocolDefinition protocolDefinition;
    // Modal is now spawned via UnityUIDriver.ShowCheckpointModal

    private ICheckpointDataProvider _checkpointProvider;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;

    private XRSimpleInteractable interactable;
    private Renderer buttonRenderer;
    private Material defaultMaterial;
    [SerializeField] private Material progressFillMaterial;
    private float fillProgress = -0.09f;
    [SerializeField] private MMF_Player animationPlayer;

    void Awake()
    {
        _checkpointProvider = ServiceRegistry.GetService<ICheckpointDataProvider>();
        interactable = GetComponent<XRSimpleInteractable>();
        buttonRenderer = GetComponent<Renderer>();
        defaultMaterial = buttonRenderer.material;
        animationPlayer = GetComponent<MMF_Player>();
    }

    void OnEnable()
    {
        animationPlayer.PlayFeedbacks();
    }

    public void Initialize(ProtocolDefinition protocolDefinition)
    {
        this.protocolDefinition = protocolDefinition;
        titleText.text = protocolDefinition.title;
        descriptionText.text = protocolDefinition.description.Length > 100 
            ? protocolDefinition.description.Substring(0, 97) + "..." 
            : protocolDefinition.description;

        interactable.selectEntered.AddListener(_ => OnButtonPressed());
    }

    private IEnumerator ChangeMaterialAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        buttonRenderer.material = progressFillMaterial;
        fillProgress = -0.09f;
        buttonRenderer.material.SetFloat("_FillRate", fillProgress);
    }

    private void IncrementProgressFill()
    {
        buttonRenderer.material.SetFloat("_FillRate", fillProgress += 0.0075f);
        if(fillProgress >= 0.09f)
        {
            interactable.selectExited.RemoveAllListeners();
            var localFileProvider = new LocalFileDataProvider();
            localFileProvider.DeleteProtocolDefinition(titleText.text);
            Destroy(gameObject);
        }
    }

    private void OnButtonPressed()
    {
        // Open checkpoint modal immediately – all further logic handled there
        var driver = ServiceRegistry.GetService<IUIDriver>() as UnityUIDriver;
        if (driver == null)
        {
            Debug.LogError("[ProtocolMenuButton] UnityUIDriver service not found – cannot open checkpoint modal");
            return;
        }

        driver.ShowCheckpointModal(protocolDefinition);
    }
    // Old SelectionFlow & FinishSelectionDirect removed – modal now controls start/resume
  }