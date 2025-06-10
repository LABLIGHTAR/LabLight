using UnityEngine;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using UniRx;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class UserSelectionPanelViewController : LLBasePanel
{
    IFileManager fileManager;
    IUIDriver uiDriver;
    SessionManager sessionManager;

    List<LocalUserProfileData> userProfiles = new List<LocalUserProfileData>();
    private CompositeDisposable _disposables = new CompositeDisposable();

    [SerializeField]
    TextMeshProUGUI panelTitle;

    [SerializeField]
    GameObject selectUserView;

    [SerializeField]
    GameObject userProfileButtonPrefab;

    [SerializeField]
    Transform userProfileButtonGrid;

    [SerializeField]
    XRSimpleInteractable addUserButton;

    [SerializeField]
    XRSimpleInteractable cancelButton;

    [SerializeField]
    GameObject createUserView;

    [SerializeField]
    TMP_InputField createUserNameInputField;
    [SerializeField]
    TMP_InputField createUserEmailInputField;
    [SerializeField]
    TMP_InputField createUserPasswordInputField;

    [SerializeField]
    XRSimpleInteractable createUserButton;
    [SerializeField]
    XRSimpleInteractable backToUserSelectionButton;

    void Awake()
    {
        fileManager = ServiceRegistry.GetService<IFileManager>();
        uiDriver = ServiceRegistry.GetService<IUIDriver>();
        sessionManager = SessionManager.instance;

        if (fileManager == null)
        {
            Debug.LogError("UserSelectionPanelViewController: Awake: FileManager service not found!");
        }
        if (uiDriver == null)
        {
            Debug.LogError("UserSelectionPanelViewController: Awake: UIDriver service not found!");
        }
        if (sessionManager == null)
        {
            Debug.LogError("UserSelectionPanelViewController: Awake: SessionManager instance is null!");
        }
    }

    void Start()
    {
        if (fileManager == null) fileManager = ServiceRegistry.GetService<IFileManager>();
        if (uiDriver == null) uiDriver = ServiceRegistry.GetService<IUIDriver>();
        if (sessionManager == null) sessionManager = SessionManager.instance;

        addUserButton.selectEntered.AddListener(_ => DisplayCreateUser());
        cancelButton.selectEntered.AddListener(_ => uiDriver?.UserSelectionCallback(null));
        createUserButton.selectEntered.AddListener(_ => OnCreateUserButtonPressed());
        if(backToUserSelectionButton != null) backToUserSelectionButton.selectEntered.AddListener(_ => DisplayUserSelection());
    }

    void OnEnable()
    {
        RefreshUserProfiles();
        DisplayUserSelection();
    }

    void OnDisable()
    {
        _disposables.Clear();
    }

    void OnDestroy()
    {
        _disposables.Dispose();
    }

    void RefreshUserProfiles()
    {
        if (fileManager == null) 
        {
            Debug.LogError("UserSelectionPanelViewController: FileManager is null, cannot refresh profiles.");
            BuildUserList();
            return;
        }

        fileManager.GetLocalUserProfilesAsync()
            .ToObservable()
            .ObserveOnMainThread()
            .Subscribe(result =>
            {
                if (result.Success && result.Data != null)
                {
                    this.userProfiles = result.Data;
                }
                else
                {
                    Debug.LogError($"Error fetching local user profiles: {result.Error?.Code} - {result.Error?.Message}");
                    this.userProfiles = new List<LocalUserProfileData>(); 
                }
                BuildUserList();
            })
            .AddTo(_disposables);
    }
    
    void BuildUserList()
    {
        ClearUserList();
        foreach (var userProfile in userProfiles)
        {
            Debug.Log(userProfile.Name);
            var userProfileButton = Instantiate(userProfileButtonPrefab, userProfileButtonGrid);
            userProfileButton.transform.GetChild(0).GetChild(0).GetComponent<TextMeshProUGUI>().text = userProfile.Name;
            userProfileButton.GetComponent<XRSimpleInteractable>().selectEntered.AddListener(_ => {
                uiDriver.UserSelectionCallback(userProfile.Id);
                gameObject.SetActive(false);
            });
        }
    }

    void OnCreateUserButtonPressed()
    {
        string userName = createUserNameInputField.text;
        string email = createUserEmailInputField.text;
        string password = createUserPasswordInputField.text;

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            Debug.LogError("Username, email, and password cannot be empty.");
            //uiDriver?.ShowError("Username, email, and password cannot be empty.");
            return;
        }

        if (sessionManager != null)
        {
            sessionManager.AttemptSignUp(email, password, userName);
        }
        else
        {
            Debug.LogError("Cannot create user, SessionManager is not available.");
            //uiDriver?.ShowError("User creation service not available.");
        }
    }

    void DisplayUserSelection()
    {
        panelTitle.text = "Select User";
        selectUserView.SetActive(true);
        createUserView.SetActive(false);
        RefreshUserProfiles(); 
    }

    void DisplayCreateUser()
    {
        panelTitle.text = "Create User";
        selectUserView.SetActive(false);
        createUserView.SetActive(true);
        createUserNameInputField.text = "";
        createUserEmailInputField.text = "";
        createUserPasswordInputField.text = "";
    }

    void ClearUserList()
    {
        foreach (Transform child in userProfileButtonGrid)
        {
            Destroy(child.gameObject);
        }
    }
}
