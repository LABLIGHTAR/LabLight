/*
using System;
using UnityEngine;

/// <summary>
/// Interface for access image frame data
/// </summary>
public interface IFrameProvider
{
    /// <summary>
    /// Indicates whether the frame provider is currently available and ready
    /// </summary>
    bool IsAvailable { get; }

    bool IsFlippedHorizontally { get; }

    bool IsFlippedVertically { get; }

    /// <summary>
    /// Current frame dimensions
    /// </summary>
    (int width, int height) FrameSize { get; }

    /// <summary>
    /// Event triggered when a new frame is available
    /// </summary>
    event Action<Texture2D> OnFrameReceived;

    /// <summary>
    /// Request necessary permissions to access the frame provider
    /// </summary>
    void RequestPermissions();

    /// <summary>
    /// Initialize the frame provider
    /// </summary>
    /// <param name="width">Desired frame width</param>
    /// <param name="height">Desired frame height</param>
    /// <returns>True if initialization was successful</returns>
    bool Initialize(int width, int height);

    /// <summary>
    /// Start capturing frames
    /// </summary>
    void Start();

    /// <summary>
    /// Stop capturing frames
    /// </summary>
    void Stop();    

    // update
    //void Update();
}
*/

