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
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;

    private XRSimpleInteractable interactable;
    private Renderer buttonRenderer;
    private Material defaultMaterial;
    [SerializeField] private MMF_Player animationPlayer;

    void Awake()
    {
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

        interactable.selectEntered.RemoveAllListeners();
        interactable.selectExited.RemoveAllListeners();

        interactable.selectExited.AddListener(_ => {
            string protocolDefinitionJson = JsonConvert.SerializeObject(protocolDefinition);
            ServiceRegistry.GetService<IUIDriver>().ProtocolSelectionCallback(protocolDefinitionJson);
        });
    }
}
