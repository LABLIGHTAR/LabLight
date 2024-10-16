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
using UnityEngine.Video;

/// <summary>
/// Originally known as Api 
/// 
/// Loads data from Resources folder (builtin readonly Unity folder)
/// 
/// Implements IDataProvider interface for accessing available procedures and updating runtime state
/// Implements IMediaProvider interface for accessing images, sound, videos and prefabs
/// </summary>
public class ResourceFileDataProvider : IProcedureDataProvider, IMediaProvider
{
    public Task<List<ProcedureDescriptor>> GetProcedureList()
    {
        return LoadTextAsset("Procedure/index").Select(jsonString =>
        {
            try
            {
                return Parsers.ParseProcedures(jsonString);
            }
            catch (Exception e)
            {
                ServiceRegistry.Logger.LogError("Could not create procedures " + e.ToString());
                throw;
            }
        }).ToTask();
    }

    public IObservable<ProcedureDefinition> GetOrCreateProcedureDefinition(string procedureName)
    {
        var basePath = "Procedure/" + procedureName;
        var systemIoPath = @"Assets/Resources/" + basePath;

        return LoadTextAsset(basePath + "/index").Select(jsonString =>
        {
            try
            {
                var procedure = Parsers.ParseProcedure(jsonString);

                if (procedure.version < 9)
                {
                    UpdateProcedureVersion(procedure);
                }

                // Set basepath for media to the same path
                procedure.mediaBasePath = basePath;

                return procedure;
            }
            catch (Exception e)
            {
                ServiceRegistry.Logger.LogError("Parsing protocol definition " + e.ToString());
                throw;
            }
        });
    }

    // public void DeleteProcedureDefinition(string procedureName)
    // {
    //     string indexPath;
    //     #if UNITY_EDITOR
    //         indexPath = Application.dataPath + "/Resources/Procedure/index.json";
    //     #else
    //         indexPath = Application.persistentDataPath + "/Resources/Procedure/index.json";
    //     #endif

    //     string jsonString = File.ReadAllText(indexPath);
    //     Debug.Log(jsonString);
    //     var procedures = JsonConvert.DeserializeObject<List<ProcedureDescriptor>>(jsonString);
    //     var procedureToDelete = procedures.Find(p => p.title == procedureName);
    //     procedures.Remove(procedureToDelete);
    //     string updatedIndex = JsonConvert.SerializeObject(procedures, Formatting.Indented);
    //     Debug.Log(updatedIndex);
    //     File.WriteAllText(indexPath, updatedIndex);
    // }

    /// <summary>
    /// Version 1 switched to automatic serialization/deserialization withouth manual parsing
    /// Version 2 introduces Containers with ContentItems, SlideArdDefinitions and LabelArDefinitions are obsolete and need to be replaced with containers and content 
    /// Version 3 introduces SoundItems that replace the SoundArDefinitions
    /// Version 4 replaces font size with enumeration for header or block 
    /// Version 5 renamed action to arDefinitionType, added globalArDefinitions array, removed frame from ArDefinition; everything is now assumed in Charuco frame, but behaviour depends on targetId (eg. container without target goes to slide frame)
    /// Version 6 model, prefab name is now renamed to url to have the same structure as content items
    /// Version 7 propertyitem, new contentitem that shows a trackedobject property as string
    /// Version 8 removed target from ArDefinition and replaced with condition to handle more flexible visualization conditions
    /// Version 9 all ArDefinitions are now global at the procedureDef level, removed ArDefinitions from steps and checkItems, seperated ArDefintions and ContentItems
    /// 
    /// LabelARDefitions are converted to containers with TextItem
    /// SlideARDefinitions are converted to containers with TextItems, Images and Videos where applicable
    /// </summary>
    /// <param name="procedure"></param>
    private static void UpdateProcedureVersion(ProcedureDefinition procedure)
    {
        // Convert to version 9 content
        procedure.version = 9;

        Debug.Log("Updating '" + procedure.title + "'  to file version " + procedure.version);

        var newList = new List<ArDefinition>();
        UpdateArDefinitions(procedure.globalArElements, newList);
        procedure.globalArElements = newList;
    }

    private static void UpdateArDefinitions(List<ArDefinition> oldList, List<ArDefinition> newList)
    {
        foreach (var ar in oldList)
        {
            ArDefinition updatedItem = ar;

            var containerDef = ar as ContainerArDefinition;
            if (containerDef != null)
            {
                foreach (var content in containerDef.layout.contentItems)
                {
                    var textItem = content as TextItem;
                    if (textItem != null)
                    {
                        textItem.textType = (textItem.fontsize < 10) ? TextType.Block : TextType.Header;
                    }
                }
            }

            newList.Add(updatedItem);
        }
    }

    /// <summary>
    /// Some jumping through hoops to match the HttpDataProvider way of working
    /// Async loading returns an observable immediately, and the listening is triggered when that observable is updated asynchonously
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public IObservable<string> LoadTextAsset(string url)
    {
        return Observable.FromCoroutine<string>((observer, cancellation) => LoadTextAssetCoroutine(url, observer, cancellation));
    }

    /// <summary>
    /// Async loading from resources
    /// </summary>
    /// <param name="url"></param>
    /// <param name="observer"></param>
    /// <param name="cancel"></param>
    /// <returns></returns>
    private static IEnumerator LoadTextAssetCoroutine(string url, IObserver<string> observer, CancellationToken cancel)
    {
        ResourceRequest resourceRequest = Resources.LoadAsync<TextAsset>(url);

        //yield return new WaitForSeconds(5);

        while (!resourceRequest.isDone)
        {
            yield return 0;
        }

        if (cancel.IsCancellationRequested)
        {
            yield break;
        }

        TextAsset textAsset = resourceRequest.asset as TextAsset;

        if (textAsset == null || string.IsNullOrEmpty(textAsset.text))
        {
            observer.OnError(new Exception("Error loading TextAsset from url: " + url));
            yield break;
        }

        observer.OnNext(textAsset.text);
        observer.OnCompleted();
    }

    public IObservable<Texture2D> GetImage(string url)
    {
        Debug.Log("GetImage " + url);
        return Observable.FromCoroutine<Texture2D>((observer, cancellation) => CoGetImage(url, observer, cancellation));
    }

    private static IEnumerator CoGetImage(string url, IObserver<Texture2D> observer, CancellationToken cancel)
    {
        var fileName = Path.ChangeExtension(url, null);
        ResourceRequest resourceRequest = Resources.LoadAsync<Texture2D>(fileName);
        while (!resourceRequest.isDone)
        {
            yield return 0;
        }

        if (cancel.IsCancellationRequested)
        {
            yield break;
        }

        Texture2D textureAsset = resourceRequest.asset as Texture2D;

        if (textureAsset == null)
        {
            observer.OnError(new Exception("Error loading TextureAsset from url: " + url));
            yield break;
        }

        observer.OnNext(textureAsset);
        observer.OnCompleted();
    }

    public IObservable<Sprite> GetSprite(string url)
    {
        return Observable.FromCoroutine<Sprite>((observer, cancellation) => CoGetSprite(url, observer, cancellation));
    }

    private static IEnumerator CoGetSprite(string url, IObserver<Sprite> observer, CancellationToken cancel)
    {
        var fileName = Path.ChangeExtension(url, null);
        ResourceRequest resourceRequest = Resources.LoadAsync<Sprite>(fileName);

        while (!resourceRequest.isDone)
        {
            yield return 0;
        }

        if (cancel.IsCancellationRequested)
        {
            yield break;
        }

        Sprite spriteAsset = resourceRequest.asset as Sprite;

        if (spriteAsset == null)
        {
            observer.OnError(new Exception("Error loading SpriteAsset from url: " + url));
            yield break;
        }

        observer.OnNext(spriteAsset);
        observer.OnCompleted();
    }


    public IObservable<AudioClip> GetSound(string url)
    {
        return Observable.FromCoroutine<AudioClip>((observer, cancellation) => CoGetSound(url, observer, cancellation));
    }

    private static IEnumerator CoGetSound(string url, IObserver<AudioClip> observer, CancellationToken cancel)
    {
        var fileName = Path.ChangeExtension(url, null);
        ResourceRequest resourceRequest = Resources.LoadAsync<AudioClip>(fileName);

        while (!resourceRequest.isDone)
        {
            yield return 0;
        }

        if (cancel.IsCancellationRequested)
        {
            yield break;
        }

        AudioClip audioClipAsset = resourceRequest.asset as AudioClip;

        if (audioClipAsset == null)
        {
            observer.OnError(new Exception("Error loading AudioClip from url: " + url));
            yield break;
        }

        observer.OnNext(audioClipAsset);
        observer.OnCompleted();
    }

    public IObservable<VideoClip> GetVideo(string mediaPath)
    {
        return Observable.FromCoroutine<VideoClip>((observer, cancellation) => CoGetVideo(mediaPath, observer, cancellation));
    }

    private static IEnumerator CoGetVideo(string url, IObserver<VideoClip> observer, CancellationToken cancel)
    {
        var fileName = Path.ChangeExtension(url, null);
        ResourceRequest resourceRequest = Resources.LoadAsync<VideoClip>(fileName);

        while (!resourceRequest.isDone)
        {
            yield return 0;
        }

        if (cancel.IsCancellationRequested)
        {
            yield break;
        }

        VideoClip videoClipAsset = resourceRequest.asset as VideoClip;

        if (videoClipAsset == null)
        {
            observer.OnError(new Exception("Error loading VideoClip from url: " + url));
            yield break;
        }

        observer.OnNext(videoClipAsset);
        observer.OnCompleted();
    }

    public IObservable<GameObject> GetPrefab(string mediaPath)
    {
        return Observable.FromCoroutine<GameObject>((observer, cancellation) => CoGetPrefab(mediaPath, observer, cancellation));
    }

    private static IEnumerator CoGetPrefab(string url, IObserver<GameObject> observer, CancellationToken cancel)
    {
        var fileName = Path.ChangeExtension(url, null);
        ResourceRequest resourceRequest = Resources.LoadAsync<GameObject>(fileName);
        while (!resourceRequest.isDone)
        {
            yield return 0;
        }

        if (cancel.IsCancellationRequested)
        {
            yield break;
        }

        GameObject prefabAsset = resourceRequest.asset as GameObject;

        if (prefabAsset == null)
        {
            observer.OnError(new Exception("Error loading Prefab from url: " + url));
            yield break;
        }

        observer.OnNext(prefabAsset);
        observer.OnCompleted();
    }

    public IObservable<List<MediaDescriptor>> GetMediaList(string mediaBasePath)
    {
        List<MediaDescriptor> mediaItems = new List<MediaDescriptor>();
        string path = @"Assets/Resources/" + mediaBasePath;

        if (Directory.Exists(path))
        {
            var fileInfo = Directory.GetFiles(path);

            foreach (var filePath in fileInfo)
            {
                string filename = Path.GetFileName(filePath).ToLowerInvariant();
                if (!filename.EndsWith(".meta"))
                {
                    if (filename.EndsWith(".jpg") || filename.EndsWith(".png"))
                    {
                        mediaItems.Add(new MediaDescriptor()
                        {
                            type = MediaDescriptorType.Image,
                            path = filename
                        });
                    }
                    else if (filename.EndsWith(".mp4") || filename.EndsWith(".mov"))
                    {
                        mediaItems.Add(new MediaDescriptor()
                        {
                            type = MediaDescriptorType.Video,
                            path = filename
                        });
                    }
                    else if (filename.EndsWith(".mp3") || filename.EndsWith(".ogg"))
                    {
                        mediaItems.Add(new MediaDescriptor()
                        {
                            type = MediaDescriptorType.Sound,
                            path = filename
                        });
                    }
                    else if (filename.EndsWith(".prefab"))
                    {
                        mediaItems.Add(new MediaDescriptor()
                        {
                            type = MediaDescriptorType.Prefab,
                            path = filename
                        });
                    }
                }
            }
        }

        return Observable.Return<List<MediaDescriptor>>(mediaItems);
    }

    /// <summary>
    /// Save procedure to resources folder (can only be done inside Unity editor)
    /// </summary>
    /// <param name="procedureName"></param>
    /// <param name="procedure"></param>
    public void SaveProcedureDefinition(string procedureName, ProcedureDefinition procedure)
    {
#if UNITY_EDITOR
        string path = "Assets/Resources/Procedure/" + procedureName;
        string filePath = path + "/index.json";

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        StreamWriter writer = new StreamWriter(filePath, false);
        var output = JsonConvert.SerializeObject(procedure, Formatting.Indented, Parsers.serializerSettings);
        writer.WriteLine(output);
        writer.Close();

        AssetDatabase.ImportAsset(filePath);
#endif
    }
}