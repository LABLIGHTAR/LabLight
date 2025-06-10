# Audio Service Implementation Plan

This document outlines the steps to create a new, globally accessible `AudioService` for playing sound effects throughout the application.

---

## I. Core Architecture

The service will be a `MonoBehaviour` accessible via a service locator/registry pattern. It will manage a pool of `AudioSource` components to play sound effects efficiently at specified 3D positions.

---

## II. Step-by-Step Implementation

### 1. **Create the `IAudioService` Interface**

- **File:** `Assets/_App/Scripts/IAudioService.cs`
- **Purpose:** Define the public contract for the audio service. This ensures that any class interacting with the service depends on an abstraction, not a concrete implementation.
- **Contents:**

  - A method signature for each distinct sound effect. For now, we can start with a few examples:

    ```csharp
    using UnityEngine;

    public interface IAudioService
    {
        void PlayButtonClick();
        void PlayErrorSound();
        void PlaySoundAt(AudioClip clip, Vector3 position, float volume = 1f);
        // Add more specific sound methods here as needed
    }
    ```

  - The `PlaySoundAt` method will be a more generic method for positional audio, while the others are for UI or non-positional sounds.

### 2. **Create the `AudioService` MonoBehaviour**

- **File:** `Assets/_App/Scripts/AudioService.cs`
- **Purpose:** The concrete implementation of `IAudioService`.
- **Initial Structure:**

  ```csharp
  public class AudioService : MonoBehaviour, IAudioService
  {
      // Fields for AudioClips and pool management will go here

      public void PlayButtonClick()
      {
          // Implementation will call the shared Play method
      }

      public void PlayErrorSound()
      {
          // Implementation will call the shared Play method
      }

      public void PlaySoundAt(AudioClip clip, Vector3 position, float volume = 1f)
      {
          // Implementation will call the shared Play method for positional audio
      }
  }
  ```

### 3. **Implement the `AudioSource` Pool**

- **Location:** Inside `AudioService.cs`.
- **Logic:**
  - Add a field for the pool size (e.g., `private int poolSize = 10;`).
  - Add a `List<AudioSource>` to hold the pooled sources.
  - In the `Awake()` method:
    - Create a parent `GameObject` named `AudioSourcePool` to keep the hierarchy clean.
    - Loop `poolSize` times:
      - Create a new `GameObject` named `AudioSource (i)`.
      - Parent it to the `AudioSourcePool` object.
      - Add an `AudioSource` component to the new GameObject.
      - Configure the `AudioSource` with default 3D settings (`spatialBlend = 1.0f`).
      - Add the new `AudioSource` to the list.

### 4. **Create the Shared Private `Play` Method**

- **Location:** Inside `AudioService.cs`.
- **Purpose:** To centralize the logic of finding an available `AudioSource` and playing a clip. This avoids code duplication in the public methods.
- **Signature:** `private void Play(AudioClip clip, Vector3? position = null, float volume = 1f)`
- **Logic:**
  - Iterate through the `AudioSource` pool to find one that is not currently playing (`!source.isPlaying`).
  - If an available source is found:
    - If a `position` is provided, set `source.transform.position = position.Value`. The source should already be configured for 3D sound.
    - If `position` is null, it's a 2D sound. Set `source.spatialBlend = 0.0f`.
    - Assign the `clip` and `volume`.
    - Call `source.Play()`.
    - Return from the method.
  - If no available source is found (i.e., all are busy), log a warning that the pool size might be too small.

### 5. **Flesh out the Public Methods**

- **Location:** Inside `AudioService.cs`.
- **Purpose:** To provide the simple, clean API defined by the interface.
- **Logic:**

  - Add `[SerializeField]` private fields for each specific `AudioClip` (e.g., `buttonClickClip`, `errorSoundClip`).
  - Implement the interface methods by calling the shared private `Play` method with the correct parameters.

    ```csharp
    public void PlayButtonClick()
    {
        Play(buttonClickClip, position: null, volume: 0.5f); // UI sounds are 2D
    }

    public void PlaySoundAt(AudioClip clip, Vector3 position, float volume = 1f)
    {
        Play(clip, position, volume);
    }
    ```

### 6. **Register and Use the Service**

- Create an empty `GameObject` in your main scene (e.g., in `Persistent.unity`).
- Attach the `AudioService.cs` component to it.
- In the Inspector, drag the required `AudioClip` assets into the fields you created.
- Register the `AudioService` instance with your service locator upon `Awake()`.
- Any script that needs to play audio can now retrieve the service from the registry and call its methods:
  ```csharp
  // Example usage in a UI script
  myButton.RegisterCallback<ClickEvent>(evt =>
  {
      ServiceRegistry.Get<IAudioService>().PlayButtonClick();
  });
  ```

---

This plan provides a clear path from interface to implementation, ensuring a robust, maintainable, and efficient audio system.
