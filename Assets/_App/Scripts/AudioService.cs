using System.Collections.Generic;
using UnityEngine;

public class AudioService : MonoBehaviour, IAudioService
{
    [Header("Settings")]
    [SerializeField] private int poolSize = 10;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip buttonPressClip;

    private List<AudioSource> _audioSources;
    private Camera _mainCamera;
    
    private void Awake()
    {
        _mainCamera = Camera.main;

        _audioSources = new List<AudioSource>();
        var poolParent = new GameObject("AudioSourcePool");
        poolParent.transform.SetParent(transform);

        for (int i = 0; i < poolSize; i++)
        {
            var sourceGo = new GameObject($"AudioSource_{i}");
            sourceGo.transform.SetParent(poolParent.transform);
            var source = sourceGo.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1.0f; // Default to 3D audio
            _audioSources.Add(source);
        }
    }

    public void PlayButtonPress(Vector3? position = null) 
    {
        Play(buttonPressClip, position, 0.7f);
    }
    
    private void Play(AudioClip clip, Vector3? position = null, float volume = 1f)
    {
        if (clip == null) return;

        foreach (var source in _audioSources)
        {
            if (!source.isPlaying)
            {
                source.clip = clip;
                source.volume = volume;

                if (position.HasValue)
                {
                    if (_mainCamera != null)
                    {
                        // We assume the position is a screen coordinate from a UI element.
                        // We must convert it to a world position in front of the camera.
                        Vector3 screenPos = position.Value;
                        // A fixed distance from the camera is good for UI sounds.
                        screenPos.z = 1.0f; 

                        Vector3 worldPosition = _mainCamera.ScreenToWorldPoint(screenPos);
                        source.transform.position = worldPosition;
                        source.spatialBlend = 1.0f; // 3D sound
                    }
                    else
                    {
                        // Fallback if no camera found - play as 2D
                        Debug.LogWarning("AudioService: Main Camera not found. Playing sound as 2D.");
                        source.spatialBlend = 0.0f;
                    }
                }
                else
                {
                    source.spatialBlend = 0.0f; // 2D sound
                }
                
                source.Play();
                return;
            }
        }
        
        Debug.LogWarning("AudioService: No available AudioSource to play sound. Consider increasing the pool size.");
    }
} 