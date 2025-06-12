using UnityEngine;

public interface IAudioService
{
    void PlayButtonPress(Vector3? position = null);
    void PlayAlarm(Vector3? position = null);
    void PlayBeep(Vector3? position = null);
} 