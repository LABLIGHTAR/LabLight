using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;
using UnityEngine.XR.Interaction.Toolkit;
using System.Linq;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.IO;

public class ProtocolPanelViewController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI procedureTitle;
    [SerializeField] private Transform contentFrame;
    [SerializeField] private XRSimpleInteractable openPDFButton;
    [SerializeField] private TextController textPrefab;
    [SerializeField] private ImageController imagePrefab;
    [SerializeField] private VideoController videoPrefab;
    [SerializeField] private SoundController soundPrefab;

    private UnityUIDriver uiDriver;
    private List<ContentItem> currentContentItems = new List<ContentItem>();
    private List<MonoBehaviour> contentItemInstances = new List<MonoBehaviour>();

    void Start()
    {
        uiDriver = (UnityUIDriver)ServiceRegistry.GetService<IUIDriver>();
        procedureTitle.text = ProtocolState.Instance.ActiveProtocol.Value.title;
        UpdateContentItems();
        openPDFButton.selectExited.AddListener(_ => OnOpenPDFButtonClicked());
    }

    private void CreateContentItem(ContentItem contentItem, LayoutGroup container)
    {
        MonoBehaviour controller = null;

        switch (contentItem.contentType.ToLower())
        {
            case "text":
                var textController = Instantiate(textPrefab, container.transform);
                textController.ContentItem = contentItem;
                controller = textController;
                break;

            case "image":
                var imageController = Instantiate(imagePrefab, container.transform);
                imageController.ContentItem = contentItem;
                controller = imageController;
                break;

            case "sound":
                var soundController = Instantiate(soundPrefab, container.transform);
                soundController.ContentItem = contentItem;
                controller = soundController;
                break;

            case "weburl":
                if (contentItem.properties.TryGetValue("url", out object webURL))
                {
                    uiDriver.DisplayWebPage(webURL.ToString());
                }
                break;
            case "video":
                if(contentItem.properties.TryGetValue("url", out object videoURL))
                {
                    uiDriver.DisplayVideoPlayer(videoURL.ToString());
                }
                break;
            case "informationpanel":
                /* based on these properties 
                 "Properties": 
                    "arObjectID": "wellplate_01",
                    "Text": "Pipette into Well A1",
                    "ImageURL": "wellplate.png",
                    "Volume": "5 uL"*/
                if(contentItem.properties.TryGetValue("arObjectID", out object arObjectID))
                {
                    Debug.Log("arObjectID: " + arObjectID);
                }
                if(contentItem.properties.TryGetValue("Text", out object text))
                {
                    Debug.Log("Text: " + text);
                }
                if(contentItem.properties.TryGetValue("ImageURL", out object imageURL))
                {
                    Debug.Log("ImageURL: " + imageURL);
                }
                if(contentItem.properties.TryGetValue("Volume", out object volume))
                {
                    Debug.Log("Volume: " + volume);
                }
                break;
        }

        if (controller != null)
        {
            contentItemInstances.Add(controller);
        }
    }

    private void ClearContentItems()
    {
        foreach (var contentItem in contentItemInstances)
        {
            Destroy(contentItem.gameObject);
        }
        contentItemInstances.Clear();
    }

    public void UpdateContentItems()
    {
        var newContentItems = new List<ContentItem>();
        
        // Check if current step has content items
        if (ProtocolState.Instance.CurrentStepDefinition.contentItems?.Any() == true)
        {
            newContentItems.AddRange(ProtocolState.Instance.CurrentStepDefinition.contentItems);
        }

        // If there's a checklist, add its content items too
        if (ProtocolState.Instance.HasCurrentChecklist() && 
            ProtocolState.Instance.CurrentCheckItemDefinition.contentItems?.Any() == true)
        {
            newContentItems.AddRange(ProtocolState.Instance.CurrentCheckItemDefinition.contentItems);
        }

        if(newContentItems.Count == 0)
        {
            foreach(Transform child in transform)
            {
                child.gameObject.SetActive(false);
            }
        }
        else if(currentContentItems != null && currentContentItems == newContentItems)
        {
            currentContentItems = newContentItems;
        }
        else
        {
            ClearContentItems();
            foreach (var contentItem in newContentItems)
            {
                CreateContentItem(contentItem, contentFrame.GetComponent<LayoutGroup>());
            }
            currentContentItems = newContentItems;
            
            foreach(Transform child in transform)
            {
                child.gameObject.SetActive(true);
            }
        }
    }

    private void OnOpenPDFButtonClicked()
    {
        var protocol = ProtocolState.Instance.ActiveProtocol.Value;
        if (protocol.protocolPDFNames?.Count > 0)
        {
            string pdfName = protocol.protocolPDFNames[0]; // Use first PDF for now
            Debug.Log($"displaying PDF: {pdfName}");
            uiDriver.DisplayPDFReader(pdfName);
        }
        else
        {
            Debug.Log("no PDFs available");
        }
    }
}
