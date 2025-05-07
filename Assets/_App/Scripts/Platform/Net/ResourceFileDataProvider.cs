using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UniRx;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Originally known as Api 
///  
/// Loads data from Resources folder (builtin readonly Unity folder)
/// 
/// Implements IDataProvider interface for accessing available procedures and updating runtime state
/// Implements IMediaProvider interface for accessing images, sound, videos and prefabs
/// </summary>
public class ResourceFileDataProvider : IProtocolDataProvider, IMediaProvider
{
    private const string PROTOCOLS_BASE_PATH = "ProtocolV2/";
    
    // Generic asset loading method to reduce duplication
    private static IEnumerator LoadAssetCoroutine<T>(
        string resourcePath, 
        IObserver<T> observer, 
        CancellationToken cancellationToken) where T : UnityEngine.Object
    {
        string normalizedPath = Path.ChangeExtension(resourcePath, null);
        
        // Try direct loading first
        ResourceRequest loadRequest = Resources.LoadAsync<T>(normalizedPath);
        yield return new WaitUntil(() => loadRequest.isDone);

        if (cancellationToken.IsCancellationRequested)
        {
            yield break;
        }

        T asset = loadRequest.asset as T;

        // Fallback to searching all resources if direct load failed
        if (asset == null)
        {
            string assetName = Path.GetFileName(normalizedPath);
            T[] allAssets = Resources.LoadAll<T>("");

            foreach (var candidateAsset in allAssets)
            {
                if (candidateAsset.name.Equals(assetName, StringComparison.OrdinalIgnoreCase))
                {
                    asset = candidateAsset;
                    break;
                }
            }
        }

        if (asset == null)
        {
            observer.OnError(new Exception($"Failed to load {typeof(T).Name} from path: {resourcePath}"));
            yield break;
        }

        observer.OnNext(asset);
        observer.OnCompleted();
    }

    // Example of how to use the generic loader (repeat for other asset types)
    public IObservable<Texture2D> GetImage(string resourcePath)
    {
        ServiceRegistry.Logger.Log($"Loading image: {resourcePath}");
        return Observable.FromCoroutine<Texture2D>(
            (observer, cancellation) => LoadAssetCoroutine(resourcePath, observer, cancellation));
    }

    // Update GetProtocolList to use consistent logging
    public Task<List<ProtocolDefinition>> GetProtocolList()
    {
        try
        {
            var protocolJsonFiles = new List<string>();
            var resourceFiles = Resources.LoadAll(PROTOCOLS_BASE_PATH, typeof(TextAsset));
            
            foreach (var file in resourceFiles)
            {
                if (file is TextAsset textAsset)
                {
                    Debug.Log($"Found protocol definition: {textAsset.name}");
                    //Debug.Log($"Found protocol definition: {textAsset.name}");
                    protocolJsonFiles.Add(textAsset.text);
                }
            }

            if (protocolJsonFiles.Count == 0)
            {
                Debug.LogError($"No protocol definitions found in Resources/{PROTOCOLS_BASE_PATH}");
                //Debug.LogWarning($"No protocol definitions found in Resources/{PROTOCOLS_BASE_PATH}");
            }

            return Task.FromResult(Parsers.ParseProtocolList(protocolJsonFiles));
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load protocol definitions: {ex}");
            //Debug.LogError($"Failed to load protocol definitions: {ex}");
            throw ex;
        }
    }

    /// <summary>
    /// Some jumping through hoops to match the HttpDataProvider way of working
    /// Async loading returns an observable immediately, and the listening is triggered when that observable is updated asynchonously
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public IObservable<string> LoadTextAsset(string resourcePath)
    {
        Debug.Log($"Loading text asset: {resourcePath}");
        //ServiceRegistry.Logger.Log($"Loading text asset: {resourcePath}");
        return Observable.FromCoroutine<string>((observer, cancellation) => LoadTextAssetCoroutine(resourcePath, observer, cancellation));
    }

    /// <summary>
    /// Async loading from resources
    /// </summary>
    /// <param name="url"></param>
    /// <param name="observer"></param>
    /// <param name="cancel"></param>
    /// <returns></returns>
    private static IEnumerator LoadTextAssetCoroutine(string resourcePath, IObserver<string> observer, CancellationToken cancellationToken)
    {
        var textAssetObserver = new TextAssetToStringObserver(observer);
        yield return LoadAssetCoroutine(resourcePath, textAssetObserver, cancellationToken);
    }

    // Helper class to convert TextAsset to string
    private class TextAssetToStringObserver : IObserver<TextAsset>
    {
        private readonly IObserver<string> _stringObserver;

        public TextAssetToStringObserver(IObserver<string> stringObserver)
        {
            _stringObserver = stringObserver;
        }

        public void OnCompleted() => _stringObserver.OnCompleted();
        public void OnError(Exception error) => _stringObserver.OnError(error);
        public void OnNext(TextAsset value)
        {
            if (string.IsNullOrEmpty(value?.text))
            {
                _stringObserver.OnError(new Exception("TextAsset is null or empty"));
                return;
            }
            _stringObserver.OnNext(value.text);
        }
    }

    public IObservable<Sprite> GetSprite(string resourcePath)
    {
        Debug.Log($"Loading sprite: {resourcePath}");
        //ServiceRegistry.Logger.Log($"Loading sprite: {resourcePath}");
        return Observable.FromCoroutine<Sprite>(
            (observer, cancellation) => LoadAssetCoroutine(resourcePath, observer, cancellation));
    }

    public IObservable<AudioClip> GetSound(string resourcePath)
    {
        Debug.Log($"Loading audio: {resourcePath}");
        //ServiceRegistry.Logger.Log($"Loading audio: {resourcePath}");
        return Observable.FromCoroutine<AudioClip>(
            (observer, cancellation) => LoadAssetCoroutine(resourcePath, observer, cancellation));
    }

    public IObservable<VideoClip> GetVideo(string resourcePath)
    {
        Debug.Log($"Loading video: {resourcePath}");
        //ServiceRegistry.Logger.Log($"Loading video: {resourcePath}");
        return Observable.FromCoroutine<VideoClip>(
            (observer, cancellation) => LoadAssetCoroutine(resourcePath, observer, cancellation));
    }

    public IObservable<GameObject> GetPrefab(string resourcePath)
    {
        Debug.Log($"Loading prefab: {resourcePath}");
        //ServiceRegistry.Logger.Log($"Loading prefab: {resourcePath}");
        return Observable.FromCoroutine<GameObject>(
            (observer, cancellation) => LoadAssetCoroutine(resourcePath, observer, cancellation));
    }
}