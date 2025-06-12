using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System.Linq;

/// <summary>
/// A singleton class for loading and unloading scenes in Unity.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance;
    public UnityEvent OnLoadBegin = new UnityEvent();
    public UnityEvent OnLoadEnd = new UnityEvent();

    private bool isLoading = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        SceneManager.sceneLoaded += SetActiveScene;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= SetActiveScene;
    }

    /// <summary>
    /// Loads a new scene by name.
    /// </summary>
    /// <param name="sceneName">The name of the scene to load.</param>
    public void LoadNewScene(string sceneName)
    {
        if(!isLoading)
        {
            StartCoroutine(LoadScene(sceneName));
        }
    }

    /// <summary>
    /// Loads a scene additively by name.
    /// </summary>
    /// <param name="sceneName">The name of the scene to load additively.</param>
    public void LoadSceneAdditive(string sceneName)
    {
        if(!isLoading)
        {
            StartCoroutine(LoadNew(sceneName));
        }
    }


    /// <summary>
    /// Unloads a scene by name if it is loaded and sets the first loaded scene that is not named "Persistent" as the active scene.
    /// </summary>
    /// <param name="sceneName">The name of the scene to unload.</param>
    public void UnloadScene(string sceneName)
    {
        if (SceneManager.GetSceneByName(sceneName).isLoaded)
        {
            SceneManager.UnloadSceneAsync(sceneName);

            // Set the first loaded scene that is not named "Persistent" as the active scene
            for(int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).name != "Persistent")
                {
                    SceneManager.SetActiveScene(SceneManager.GetSceneAt(i));
                    break;
                }
            }
        }
        else
        {
            Debug.LogWarning("Tried to unload scene " + sceneName + " but it has not been loaded.");
        }
    }

    /// <summary>
    /// Unloads the currently active scene.
    /// </summary>
    public void UnloadActiveScene()
    {
        if(!isLoading && SceneManager.GetActiveScene().name != "Persistent")
        {
            StartCoroutine(UnloadCurrent());
        }
    }

    public void LoadSceneClean(string sceneName)
    {
        for(int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).name != "Persistent" && !SceneManager.GetSceneAt(i).name.Contains("Simulated"))
            {
                SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            }
        }

        //load new scene
        StartCoroutine(LoadNew(sceneName));
    }

    private IEnumerator LoadScene(string sceneName)
    {
        isLoading = true;
        OnLoadBegin?.Invoke();

        yield return StartCoroutine(UnloadCurrent());
        yield return StartCoroutine(LoadNew(sceneName));

        isLoading = false;
        OnLoadEnd?.Invoke();
    }

    private IEnumerator UnloadCurrent()
    {
        AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(SceneManager.GetActiveScene());

        while(!unloadOperation.isDone)
        {
            yield return null;
        }
    }

    private IEnumerator LoadNew(string sceneName)
    {
        //unload all scenes but persistent
        for(int i = 0; i < SceneManager.sceneCount; i++)
        {
            if(SceneManager.GetSceneAt(i).name == sceneName)
            {
                Debug.LogWarning("Scene " + sceneName + " is already loaded.");
                yield break;
            }
        }

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

        while(!loadOperation.isDone)
        {
            yield return null;
        }
    }

    private void SetActiveScene(Scene scene, LoadSceneMode mode)
    {
        Debug.Log("Setting active scene: " + scene.name); 
        SceneManager.SetActiveScene(scene);
    }
}