using System;
using UnityEngine;
using TMPro;
using UniRx;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;

public class CheckitemView : MonoBehaviour
{

    ProtocolState.CheckItemState data;
    public bool itemChecked = false;

    [SerializeField] public TextMeshProUGUI text;
    string rawText;
    string strikeText;

    [SerializeField] Material activeMaterial;
    [SerializeField] Material defaultMaterial;
    [SerializeField] MeshRenderer m_backgroundMesh;

    [SerializeField] MMF_Player scaleUpAnimation;
    [SerializeField] MMF_Player scaleDownAnimation;
    [SerializeField] MMF_Player moveUpAnimation;
    [SerializeField] MMF_Player moveDownAnimation;

    [SerializeField] HudEventSO _hudEventSO;

    private IDisposable subscription;

    void Start()
    {
        rawText = text.text;
        strikeText = "<s>" + text.text + "</s>";

        ProtocolState.Instance.ChecklistStream.Subscribe(_ => {
            if(ProtocolState.Instance.CurrentStepState.Value.Checklist == null)
            {
                return;
            }
            if(ProtocolState.Instance.CurrentStepState.Value.Checklist[ProtocolState.Instance.CurrentCheckNum] == data)
            {
                SetAsActiveItem();
            }
            else
            {
                SetAsInactiveItem();
            }
        });
    }

    void OnEnable()
    {
        if(data != null)
        {
            if(ProtocolState.Instance.CurrentStepState.Value.Checklist[ProtocolState.Instance.CurrentCheckNum] == data)
            {
                SetAsActiveItem();
            }
            else
            {
                SetAsInactiveItem();
            }
        }
    }

    public void Check()
    {
        itemChecked = true;
        text.text = strikeText;
    }

    public void Uncheck()
    {
        itemChecked = false;
        text.text = rawText;
    }

    public void SetAsActiveItem()
    {
        if(m_backgroundMesh != null && m_backgroundMesh)
        {
            m_backgroundMesh.material = activeMaterial;
        }
        if(!data.IsChecked.Value){
            _hudEventSO.DisplayHudMessage(data.Text);
        }
    }

    public void SetAsInactiveItem()
    {
        if(m_backgroundMesh != null)
        {
            m_backgroundMesh.material = defaultMaterial;
        }
    }

    public void InitalizeCheckItem(ProtocolState.CheckItemState checkItem)
    {
        data = checkItem;

        //check puntcuation
        if(checkItem.Text != null)
        {
            var checkItemText = checkItem.Text = char.ToUpper(checkItem.Text[0]) + checkItem.Text.Substring(1);

            text.text = checkItemText;
            rawText = checkItemText;
            strikeText = "<s>" + checkItemText + "</s>";
        }

        if(subscription != null)
        {
            subscription.Dispose();
        }

        subscription = checkItem.IsChecked.Subscribe(itemChecked => {
            if(itemChecked)
            {
                Check();
            }
            else
            {
                Uncheck();
            }
        });
    }

    public void PlayScaleUpAnimation()
    {   
        scaleUpAnimation.PlayFeedbacks();
    }

    public void PlayScaleDownAnimation()
    {
        scaleDownAnimation.PlayFeedbacks();
    }

    public void PlayMoveUpAnimation(Vector3 initialPosition, Vector3 destinationPosition)
    {
        moveUpAnimation.GetFeedbackOfType<MMF_Position>().InitialPosition = initialPosition;
        moveUpAnimation.GetFeedbackOfType<MMF_Position>().DestinationPosition = destinationPosition;
        moveUpAnimation.PlayFeedbacks();
    }

    public void PlayMoveDownAnimation(Vector3 initialPosition, Vector3 destinationPosition)
    {
        moveDownAnimation.GetFeedbackOfType<MMF_Position>().InitialPosition = initialPosition;
        moveDownAnimation.GetFeedbackOfType<MMF_Position>().DestinationPosition = destinationPosition;
        moveDownAnimation.PlayFeedbacks();
    }
}
