using UnityEditor;
using UnityEditor.SceneManagement;

public static class SceneMenu
{
    [MenuItem("Scenes/ProtocolMenu")]
    public static void OpenProtocolMenu()
    {
        OpenScene("ProtocolMenu");
    }

    [MenuItem("Scenes/Protocol")]
    public static void OpenProtocol()
    {
        OpenScene("Protocol");
    }

    [MenuItem("Scenes/Calibration")]
    public static void OpenCalibration()
    {
        OpenScene("Calibration");
    }

    [MenuItem("Scenes/Settings")]
    public static void OpenSettings()
    {
        OpenScene("Settings");
    }

    [MenuItem("Scenes/WorkspaceAnchorEditor")]
    public static void OpenWorkspaceAnchorEditor()
    {
        OpenScene("WorkspaceAnchorEditor");
    }

    private static void OpenScene(string sceneName)
    {
        EditorSceneManager.OpenScene("Assets/_App/Scenes/Persistent.unity", OpenSceneMode.Single);
        EditorSceneManager.OpenScene("Assets/_App/Scenes/" + sceneName + ".unity", OpenSceneMode.Additive);
    }
}