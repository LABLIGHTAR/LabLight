using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;
using System.Threading.Tasks;
using UniRx;

public class LLMChatPanelViewController : LLBasePanel
{
    [Header("Login UI")]
    [SerializeField] GameObject loginCanvas;
    [SerializeField] TMP_InputField emailField;
    [SerializeField] TMP_InputField passwordField;
    [SerializeField] XRSimpleInteractable loginButton;
    [SerializeField] TextMeshProUGUI loginErrorText;

    [Header("Chat Input UI")]
    [SerializeField] GameObject inputPanel;
    [SerializeField] TMP_InputField inputField;
    [SerializeField] XRSimpleInteractable recordButton;
    [SerializeField] XRSimpleInteractable testButton;
    [SerializeField] XRSimpleInteractable submitButton;

    [Header("Chat Output UI")]
    [SerializeField] GameObject chatCanvas;
    [SerializeField] TextMeshProUGUI panelText;

    private TouchScreenKeyboard keyboard;

    protected override void Awake()
    {
        base.Awake();
    }

    // Start is called before the first frame update
    void Start()
    {
        recordButton.selectEntered.AddListener(_ => Record());
        testButton.selectEntered.AddListener(_ => Test());
        submitButton.selectEntered.AddListener(_ => Submit());
        inputField.onSubmit.AddListener(_ => Submit());
        loginButton.selectExited.AddListener(_ => LoginAsync());
        UpdateUIBasedOnAuthStatus();
    }

    void Update()
    {
        if(TouchScreenKeyboard.visible)
        {
            inputField.ActivateInputField();
            inputField.text = keyboard.text;
            inputField.MoveTextEnd(true);
        }
    }

    public void Record()
    {
        SpeechRecognizer.Instance.RecognizedTextHandler = HandleRecognizedText;
    }

    public void HandleRecognizedText(string recognizedText)
    {
        inputField.text = recognizedText;
        SpeechRecognizer.Instance.RecognizedTextHandler = null;
    }

    public void DisplayResponse(string response)
    {
        panelText.text = panelText.text + "<color=white>" + response + "\n\n";
    }

    void Test()
    {
        HandleRecognizedText("Hello, Claude!");
    }

    void Submit()
    {
        string query = inputField.text;
        inputField.text = "";
        panelText.text = panelText.text + "<color=blue>" + query + "\n\n";
        ServiceRegistry.GetService<ILLMChatProvider>().QueryAsync(query);
    }

    private void UpdateUIBasedOnAuthStatus()
    {
        bool isAuthenticated = ServiceRegistry.GetService<IUserAuthProvider>().IsAuthenticated();
        inputPanel.SetActive(isAuthenticated);
        loginCanvas.SetActive(!isAuthenticated);
        chatCanvas.SetActive(isAuthenticated);
    }

    private async void LoginAsync()
    {
        loginButton.gameObject.SetActive(false);

        string email = emailField.text;
        string password = passwordField.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            Debug.LogError("Email or password is empty");
            loginButton.gameObject.SetActive(true);
            return;
        }

        bool isAuthenticated = await ServiceRegistry.GetService<IUserAuthProvider>().TryAuthenticateUser(email, password);

        UpdateUIBasedOnAuthStatus();

        if (isAuthenticated)
        {
            // Login successful
            Debug.Log("Login successful");
            // You might want to clear the login fields here
            emailField.text = "";
            passwordField.text = "";
            loginErrorText.gameObject.SetActive(false);
        }
        else
        {
            // Login failed
            Debug.LogError("Login failed");
            loginErrorText.gameObject.SetActive(true);
        }

        loginButton.gameObject.SetActive(true);
    }
}
