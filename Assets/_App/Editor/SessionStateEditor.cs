using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SessionState))]
public class SessionStateEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); // Draws the default inspector elements

        // SessionState sessionStateInstance = (SessionState)target; // Not strictly needed if calling a static method

        EditorGUILayout.Space(10); // Add some space

        EditorGUILayout.LabelField("Data Management (Editor Only)", EditorStyles.boldLabel);
        if (GUILayout.Button("Clear Local User Profiles & PlayerPrefs"))
        {
            if (EditorUtility.DisplayDialog(
                "Confirm Clear Data",
                "Are you sure you want to delete local user profile files (identified by prefix) from Application.persistentDataPath, clear all PlayerPrefs, and reset SessionState static fields? This action cannot be undone.",
                "Yes, Clear Data",
                "Cancel"))
            {
                SessionState.ClearLocalUserProfilesAndPlayerPrefs_EditorOnly();
                EditorUtility.DisplayDialog(
                    "Data Cleared",
                    "Local user profiles (matching prefix) and PlayerPrefs have been cleared. Static SessionState fields have been reset. You may need to refresh views or re-select assets if data was cached by the editor.",
                    "OK");
            }
        }
        EditorGUILayout.HelpBox("Clears local user profile files (identified by prefix directly from persistentDataPath), all PlayerPrefs, and resets static SessionState fields. Can be used outside of Play Mode.", MessageType.Info);
    }
} 