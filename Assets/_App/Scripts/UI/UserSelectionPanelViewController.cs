using UnityEngine;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using UniRx;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class UserSelectionPanelViewController : LLBasePanel
{
    IUserProfileDataProvider userProfileDataProvider;
    IUIDriver uiDriver;

    List<UserProfileData> userProfiles = new List<UserProfileData>();

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
    XRSimpleInteractable createUserButton;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        userProfileDataProvider = ServiceRegistry.GetService<IUserProfileDataProvider>();
        uiDriver = ServiceRegistry.GetService<IUIDriver>();

        userProfileDataProvider.GetAllUserProfiles()
            .ObserveOnMainThread()
            .Subscribe(profiles =>
            {
                userProfiles = profiles;
                BuildUserList();
            })
            .AddTo(this);

        addUserButton.selectEntered.AddListener(_ => DisplayCreateUser());
        cancelButton.selectEntered.AddListener(_ => DisplayUserSelection());
        createUserButton.selectEntered.AddListener(_ => OnCreateUserButtonPressed());
    }
    
    void BuildUserList()
    {
        ClearUserList();
        userProfileDataProvider.GetAllUserProfiles();
        foreach (var userProfile in userProfiles)
        {
            Debug.Log(userProfile.GetName());
            var userProfileButton = Instantiate(userProfileButtonPrefab, userProfileButtonGrid);
            userProfileButton.transform.GetChild(0).GetChild(0).GetComponent<TextMeshProUGUI>().text = userProfile.GetName();
            userProfileButton.GetComponent<XRSimpleInteractable>().selectEntered.AddListener(_ => {
                uiDriver.UserSelectionCallback(userProfile.GetUserId());
                gameObject.SetActive(false);
            });
        }
    }

    void OnCreateUserButtonPressed()
    {
        if(createUserNameInputField.text.Length > 0)
        {
            var userProfileDataProvider = ServiceRegistry.GetService<IUserProfileDataProvider>();
            userProfileDataProvider.SaveUserProfileData(createUserNameInputField.text, new UserProfileData(createUserNameInputField.text));
            DisplayUserSelection();
            createUserNameInputField.text = "";
        }
    }

    void DisplayUserSelection()
    {
        panelTitle.text = "Select User";
        selectUserView.SetActive(true);
        createUserView.SetActive(false);
        userProfileDataProvider.GetAllUserProfiles().ObserveOnMainThread().Subscribe(profiles => {
            userProfiles = profiles;
            BuildUserList();
        });
    }

    void DisplayCreateUser()
    {
        panelTitle.text = "Create User";
        selectUserView.SetActive(false);
        createUserView.SetActive(true);
    }

    void ClearUserList()
    {
        foreach (Transform child in userProfileButtonGrid)
        {
            Destroy(child.gameObject);
        }
    }
}
