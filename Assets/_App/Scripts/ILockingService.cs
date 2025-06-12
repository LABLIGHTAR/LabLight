using System;
using UnityEngine;

/// <summary>
/// Interface for object locking services.
/// Provides a unified contract for different locking methods (plane, image tracking, etc.)
/// </summary>
public interface ILockingService
{
    /// <summary>
    /// Event raised when an object is successfully locked
    /// </summary>
    event Action<GameObject> OnObjectLocked;

    /// <summary>
    /// The type of locking this service handles
    /// </summary>
    LockingType LockingType { get; }

    /// <summary>
    /// Initiates the locking process for the specified object
    /// </summary>
    /// <param name="objectToLock">The GameObject to lock</param>
    void BeginLocking(GameObject objectToLock);

    /// <summary>
    /// Cancels any active locking operations
    /// </summary>
    void CancelLocking();

    /// <summary>
    /// Gets whether the service is currently in the process of locking an object
    /// </summary>
    bool IsLocking { get; }
}