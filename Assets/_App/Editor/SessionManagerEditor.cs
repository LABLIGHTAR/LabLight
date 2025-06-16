using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SessionManager))]
public class SessionManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SessionManager sessionManager = (SessionManager)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Authentication Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        AuthProviderType newProviderType = (AuthProviderType)EditorGUILayout.EnumPopup("Auth Provider", sessionManager.SelectedAuthProvider);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(sessionManager, "Change Auth Provider");
            sessionManager.SelectedAuthProvider = newProviderType;
            
            UpdateAuthProviderComponent(sessionManager);

            EditorUtility.SetDirty(sessionManager);
        }
    }

    private void UpdateAuthProviderComponent(SessionManager sessionManager)
    {
        GameObject go = sessionManager.gameObject;

        // Remove any existing IAuthProvider implementations to avoid conflicts
        IAuthProvider[] existingProviders = go.GetComponents<IAuthProvider>();
        foreach (var provider in existingProviders)
        {
            if (provider is MonoBehaviour component)
            {
                Undo.DestroyObjectImmediate(component);
            }
        }
        
        // Add the newly selected provider component
        switch (sessionManager.SelectedAuthProvider)
        {
            case AuthProviderType.Firebase:
                Undo.AddComponent<FirebaseAuthProvider>(go);
                Debug.Log("SessionManager configured to use Firebase Auth Provider.");
                break;
            case AuthProviderType.Unity:
                Undo.AddComponent<UnityAuthProvider>(go);
                Debug.Log("SessionManager configured to use Unity Auth Provider.");
                break;
        }
    }
} 