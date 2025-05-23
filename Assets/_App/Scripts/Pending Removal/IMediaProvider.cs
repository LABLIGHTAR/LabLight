// using System;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.Video;

// /// <summary>
// /// Interface for media services
// /// </summary>
// public interface IMediaProvider
// {
//     /// <summary>
//     /// Retrieves image from given mediaPath
//     /// </summary>
//     /// <param name="mediaPath"></param>
//     /// <returns></returns>
//     IObservable<Texture2D> GetImage(string mediaPath);

//     /// <summary>
//     /// Retrieves sprite from given mediaPath
//     /// </summary>
//     /// <param name="mediaPath"></param>
//     /// <returns></returns>
//     IObservable<Sprite> GetSprite(string mediaPath);

//     /// <summary>
//     /// Retrieves sound from given mediaPath
//     /// </summary>
//     /// <param name="mediaPath"></param>
//     IObservable<AudioClip> GetSound(string mediaPath);

//     /// <summary>
//     /// Retrieves video from given mediaPath
//     /// </summary>
//     /// <param name="mediaPath"></param>
//     IObservable<VideoClip> GetVideo(string mediaPath);

//     /// <summary>
//     /// Retrieves prefab from given mediaPath
//     /// </summary>
//     /// <param name="mediaPath"></param>
//     IObservable<GameObject> GetPrefab(string mediaPath);
// }