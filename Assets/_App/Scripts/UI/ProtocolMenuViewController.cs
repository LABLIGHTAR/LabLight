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
    [SerializeField] GameObject downloadButton;
    [SerializeField] XRSimpleInteractable closeAppButton;

    [Header("Popups")]
    [SerializeField] PopupEventSO closeAppPopup;
    PopupPanelViewController popupPanelViewController;
    
    private int currentPage = 0;
    private int maxPage = 0;
    List<ProtocolDefinition> protocols;
    List<ProtocolMenuButton> buttons = new List<ProtocolMenuButton>();

    protected override void Awake()
    {
        base.Awake();

        SessionState.JsonFileDownloadable.Subscribe(jsonFileInfo =>
        {
            if (string.IsNullOrEmpty(jsonFileInfo))
            {
                downloadButton.GetComponent<XRSimpleInteractable>().enabled = false;
            }
            else
            {
                downloadButton.GetComponent<XRSimpleInteractable>().enabled = true;
                downloadButton.GetComponent<MeshRenderer>().material.color = Color.green;
            }
        });

        downloadButton.GetComponent<XRSimpleInteractable>().selectEntered.AddListener(async _ =>
        {
            await DownloadJsonProtocolAsync();
            LoadProtocols();
            Build(currentPage);
        });
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
        headerText.text = "Hello " + SessionState.currentUserProfile.GetName() + ", Select a Protocol";
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

        // Build the requested page
        for (int i = currentPage * 8; i < Math.Min((currentPage + 1) * 8, protocols.Count); i++)
        {
            var protocol = protocols[i];
            var button = Instantiate(buttonPrefab, buttonGrid.transform);
            ProtocolMenuButton buttonScript = button.GetComponent<ProtocolMenuButton>();
            buttons.Add(buttonScript);
            buttonScript.Initialize(protocol);
        }
    }

    /// <summary>
    /// Loads the list of protocols asynchronously.
    /// </summary>
    async void LoadProtocols()
    {
        // Load protocol list
        if(ServiceRegistry.GetService<IProtocolDataProvider>() != null)
        {
            protocols = await ServiceRegistry.GetService<IProtocolDataProvider>()?.GetProtocolList();
            protocols.AddRange(await ((LocalFileDataProvider)ServiceRegistry.GetService<ITextDataProvider>())?.GetProtocolList());
            maxPage = (int)Math.Ceiling((float)protocols.Count / 8);
            currentPage = 0;

            // Build page 1
            Build(currentPage);
        }
        else
        {
            Debug.LogWarning("Cannot load protocols, protocol data provider service NULL");
        }
    }

    public async Task<string> DownloadJsonProtocolAsync()
    {
        string fileServerUri = ServiceRegistry.GetService<ILighthouseControl>()?.GetFileServerUri();

        if (!string.IsNullOrEmpty(fileServerUri))
        {
            string uri;

            bool filenameKnown = !string.IsNullOrEmpty(SessionState.JsonFileDownloadable.Value);
            if (filenameKnown)
            {
                uri = fileServerUri + "/GetFile?Filename=" + SessionState.JsonFileDownloadable.Value;
            }
            else
            {
                uri = fileServerUri + "/GetProtocolJson";
            }

            Debug.Log("Downloading from " + uri);

            UnityWebRequest request = UnityWebRequest.Get(uri);
            request.SendWebRequest();

            while (!request.isDone)
            {
                await Task.Yield();
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                var fileName = filenameKnown ? SessionState.JsonFileDownloadable.Value : request.GetResponseHeader("File-Name");

                if (!string.IsNullOrEmpty(fileName))
                {
                    var protocolName = Path.GetDirectoryName(fileName);

                    var lfdp = new LocalFileDataProvider();
                    
                    lfdp.SaveTextFile(protocolName + ".json", request.downloadHandler.text);
                }
                else
                {
                    Debug.LogError("There is no 'File-Name' in the response header.");
                }

                SessionState.JsonFileDownloadable.Value = string.Empty;
            }
            else
            {
                Debug.LogError(request.error);
            }
        }
        else
        {
            Debug.LogError("Could not retrieve FileServerUri from LightHouse");
        }

        return null;
    }
}