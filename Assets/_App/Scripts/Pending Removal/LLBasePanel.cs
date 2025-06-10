using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(LazyFollow))]
[RequireComponent(typeof(XRGrabInteractable))]
public class LLBasePanel : MonoBehaviour
{
    [SerializeField] GameObject _pinButton;
    XRSimpleInteractable _pinButtonInteractable;
    MeshRenderer _pinButtonMeshRenderer;

    XRGrabInteractable _grabInteractable;

    [Header("Materials")]
    [SerializeField] Material _pinnedMaterial;
    [SerializeField] Material _unpinnedMaterial;

    LazyFollow _lazyFollow;

    // Start is called before the first frame update
    protected virtual void Awake()
    {
        _lazyFollow = GetComponent<LazyFollow>();
        _pinButtonInteractable = _pinButton.GetComponent<XRSimpleInteractable>();
        _pinButtonMeshRenderer = _pinButton.GetComponent<MeshRenderer>();
        _pinButtonInteractable.selectEntered.AddListener(_ => OnPinButtonPressed());
        
        _grabInteractable = GetComponent<XRGrabInteractable>();
        _grabInteractable.selectEntered.AddListener(_ => OnGrabbed());
    }

    void OnPinButtonPressed()
    {
        if(_lazyFollow.positionFollowMode == LazyFollow.PositionFollowMode.Follow)
        {
            Pin();
        }
        else
        {
            Unpin();
        }
    }

    void OnGrabbed()
    {
        if(_lazyFollow.positionFollowMode == LazyFollow.PositionFollowMode.Follow)
        {
            Pin();
        }
    }

    public void Pin()
    {
        _lazyFollow.positionFollowMode = LazyFollow.PositionFollowMode.None;
        _pinButtonMeshRenderer.material = _pinnedMaterial;
    }

    public void Unpin()
    {
        _lazyFollow.positionFollowMode = LazyFollow.PositionFollowMode.Follow;
        _pinButtonMeshRenderer.material = _unpinnedMaterial;
    }
}
