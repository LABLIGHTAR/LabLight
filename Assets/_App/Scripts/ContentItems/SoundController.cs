using System;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Threading.Tasks;

/// <summary>
/// Sound content item
/// </summary>
public class SoundController : ContentController<ContentItem>
{
    public AudioSource audioSource;
    public GameObject playerUI;
    public GameObject loadingIndicator;
    public IDisposable downloadSubscription;
    public TextMeshProUGUI Text;
    public Slider progressIndicator;

    private IFileManager fileManager;

    public override ContentItem ContentItem
    {
        get => base.ContentItem;
        set
        {
            base.ContentItem = value;
            UpdateView();
        }
    }

    private void OnDisable()
    {
        downloadSubscription?.Dispose();
        downloadSubscription = null;
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    private async void UpdateView()
    {
        if (ContentItem == null || !ContentItem.properties.TryGetValue("url", out object urlValue))
        {
            Debug.LogError("SoundController: No URL/objectKey found in properties");
            loadingIndicator.SetActive(false);
            playerUI.SetActive(false);
            Text.text = "Error: No URL";
            return;
        }

        var objectKey = urlValue.ToString();
        if (string.IsNullOrEmpty(objectKey))
        {
            Debug.LogError("SoundController: URL/objectKey is null or empty.");
            loadingIndicator.SetActive(false);
            playerUI.SetActive(false);
            Text.text = "Error: Empty URL";
            return;
        }

        downloadSubscription?.Dispose();
        downloadSubscription = null;
        
        loadingIndicator.SetActive(true);
        playerUI.SetActive(false);
        Text.text = "Loading...";

        if (fileManager == null)
        {
            fileManager = ServiceRegistry.GetService<IFileManager>();
            if (fileManager == null)
            {
                Debug.LogError("SoundController: IFileManager service not found!");
                loadingIndicator.SetActive(false);
                Text.text = "Error: FileMan Service Missing";
                return;
            }
        }

        downloadSubscription = fileManager.GetMediaFileAsync(objectKey)
            .ToObservable()
            .ObserveOnMainThread()
            .SelectMany(result => 
            {
                if (result.Success && result.Data != null && result.Data.Length > 0)
                {
                    string base64Data = Convert.ToBase64String(result.Data);
                    string dataUri = $"data:audio/ogg;base64,{base64Data}"; 
                    UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(dataUri, AudioType.OGGVORBIS);
                    return www.SendWebRequest().AsAsyncOperationObservable().Select(_ => www);
                }
                else
                {
                    Debug.LogError($"SoundController: Could not load sound data for '{objectKey}' from FileManager. Error: {result.Error?.Code} - {result.Error?.Message}");
                    return Observable.Throw<UnityWebRequest>(new Exception($"Failed to load sound data: {result.Error?.Code} - {result.Error?.Message}"));
                }
            })
            .Subscribe(www => 
            {
                loadingIndicator.SetActive(false);
                if (www.result == UnityWebRequest.Result.Success)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                    if (clip != null)
                    {
                        Text.text = objectKey;
                        audioSource.clip = clip;
                        audioSource.Play();
                        progressIndicator.minValue = 0;
                        progressIndicator.maxValue = audioSource.clip.length;
                        playerUI.SetActive(true);
                    }
                    else
                    {   
                        Debug.LogError($"SoundController: Failed to get AudioClip from downloaded data for '{objectKey}'. UnityWebRequest error: {www.error}");
                        Text.text = "Error: Load failed";
                    }
                }
                else
                {
                    Debug.LogError($"SoundController: UnityWebRequest failed for '{objectKey}'. Error: {www.error}");
                    Text.text = "Error: Request failed";
                }
                www.Dispose();
            }, ex => 
            {
                loadingIndicator.SetActive(false);
                Debug.LogError($"SoundController: Exception while loading sound '{objectKey}'. {ex.ToString()}");
                Text.text = "Error: Exception";
            })
            .AddTo(this);
    }


    private void Update()
    {
        progressIndicator.value = audioSource.time;
    }
}