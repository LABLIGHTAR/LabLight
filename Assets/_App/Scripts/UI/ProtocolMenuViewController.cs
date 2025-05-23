using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Threading.Tasks;
using UniRx;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;

public class ProtocolMenuViewController : LLBasePanel
{
    [SerializeField]
    TextMeshProUGUI headerText;

    [Header("UI Buttons")]
    [SerializeField] GridLayoutGroup buttonGrid;
    [SerializeField] GameObject buttonPrefab;
    [SerializeField] GameObject previousButton;
    [SerializeField] GameObject nextButton;
    [SerializeField] XRSimpleInteractable closeAppButton;

    [Header("Popups")]
    [SerializeField] PopupEventSO closeAppPopup;
    PopupPanelViewController popupPanelViewController;
    
    private int currentPage = 0;
    private int maxPage = 0;
    List<ProtocolData> protocols;
    List<ProtocolMenuButton> buttons = new List<ProtocolMenuButton>();
    private IFileManager fileManager;

    protected override void Awake()
    {
        base.Awake();
        fileManager = ServiceRegistry.GetService<IFileManager>();
        if (fileManager == null)
        {
            Debug.LogError("ProtocolMenuViewController: IFileManager service not found!");
        }
    }

    /// <summary>
    /// Called when the script instance is being loaded.
    /// </summary>
    private void Start()
    {
        popupPanelViewController = GameObject.FindFirstObjectByType<PopupPanelViewController>(FindObjectsInactive.Include);
        closeAppButton.selectEntered.AddListener(_ => popupPanelViewController.DisplayPopup(closeAppPopup));
        closeAppPopup.OnYesButtonPressed.AddListener(() => Application.Quit());
    }

    void OnEnable()
    {
        headerText.text = "Hello " + SessionState.currentUserProfile.Name + ", Select a Protocol";
        LoadProtocols();
    }

    /// <summary>
    /// Called when the behaviour becomes disabled or inactive.
    /// </summary>
    private void OnDisable()
    {
        foreach (var button in buttons)
        {
           if (button != null)
           {
               Destroy(button.gameObject);
           }
        }
        buttons.Clear();
    }

    /// <summary>
    /// Called when the MonoBehaviour will be destroyed.
    /// </summary>
    private void OnDestroy() 
    {
        Debug.Log("ProtocolMenuViewController destroyed");
    }

    /// <summary>
    /// Moves to the next page of protocols.
    /// </summary>
    public void NextPage()
    {
        if (currentPage < maxPage - 1)
        {
            currentPage++;
            Build(currentPage);
        }
    }

    /// <summary>
    /// Moves to the previous page of protocols.
    /// </summary>
    public void PreviousPage()
    {
        if (currentPage > 0)
        {
            currentPage--;
            Build(currentPage);
        }
    }

    /// <summary>
    /// Builds the specified page of protocols.
    /// </summary>
    /// <param name="pageNum">The page number to build.</param>
    void Build(int pageNum)
    {
        // Destroy current page
        for (int i = 0; i < buttonGrid.transform.childCount; i++)
        {
            buttonGrid.transform.GetChild(i).gameObject.SetActive(false);
            Destroy(buttonGrid.transform.GetChild(i).gameObject);
        }
        buttons.Clear(); // Clear the list of button scripts as their GameObjects are destroyed

        // Build the requested page
        if (protocols == null) // Guard against null protocols list
        {
            Debug.LogWarning("ProtocolMenuViewController.Build: Protocols list is null.");
            return;
        }

        for (int i = currentPage * 8; i < Math.Min((currentPage + 1) * 8, protocols.Count); i++)
        {
            var protocolDataEntry = protocols[i]; // This is ProtocolData
            if (protocolDataEntry == null || string.IsNullOrEmpty(protocolDataEntry.Content))
            {
                Debug.LogWarning($"ProtocolMenuViewController.Build: ProtocolData at index {i} is null or has no content. Skipping.");
                continue;
            }

            try
            {
                ProtocolDefinition protocolDefinition = Parsers.ParseProtocol(protocolDataEntry.Content);
                if (protocolDefinition != null)
                {
                    var button = Instantiate(buttonPrefab, buttonGrid.transform);
                    ProtocolMenuButton buttonScript = button.GetComponent<ProtocolMenuButton>();
                    buttons.Add(buttonScript); // Add the new button script to the list
                    buttonScript.Initialize(protocolDefinition); // Initialize with ProtocolDefinition
                }
                else
                {
                    Debug.LogError($"ProtocolMenuViewController.Build: Failed to parse protocol content for ID {protocolDataEntry.Id}. Content: {protocolDataEntry.Content}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"ProtocolMenuViewController.Build: Exception parsing protocol content for ID {protocolDataEntry.Id}. Error: {ex.Message}. Content: {protocolDataEntry.Content}");
            }
        }
    }

    /// <summary>
    /// Loads the list of protocols asynchronously.
    /// </summary>
    async void LoadProtocols()
    {
        if (fileManager == null)
        {
            Debug.LogWarning("Cannot load protocols, IFileManager service is NULL");
            protocols = new List<ProtocolData>();
            Build(currentPage);
            return;
        }

        Result<List<ProtocolData>> result = await fileManager.GetAvailableProtocolsAsync();

        if (result.Success && result.Data != null)
        {
            protocols = result.Data;
        }
        else
        {
            Debug.LogWarning($"Cannot load protocols: {result.Error?.Code} - {result.Error?.Message}. Displaying empty list.");
            protocols = new List<ProtocolData>();
        }
        
        maxPage = (int)Math.Ceiling((float)protocols.Count / 8);
        currentPage = 0; 
        Build(currentPage);
    }
}