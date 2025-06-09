#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DashboardWindowController))]
public class DashboardWindowControllerEditor : Editor
{
    private string _protocolName = "New Protocol Name";
    private string _protocolContent = "{\"initial\": \"content\"}"; // Default to basic JSON structure
    private bool _isPublic = false;
    private uint _organizationId = 0; // Default to 0 (user-owned)

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        DashboardWindowController controller = (DashboardWindowController)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Editor Utilities", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Use the button below to create a new protocol. It is recommended to be in Play Mode so that all services (like IFileManager) are properly initialized.", MessageType.Info);

        _protocolName = EditorGUILayout.TextField("Protocol Name", _protocolName);
        EditorGUILayout.LabelField("Protocol Content (JSON)");
        _protocolContent = EditorGUILayout.TextArea(_protocolContent, GUILayout.Height(100));
        _isPublic = EditorGUILayout.Toggle("Is Public", _isPublic);
        _organizationId = (uint)EditorGUILayout.IntField("Organization ID", (int)_organizationId);
        if (_organizationId < 0) _organizationId = 0; // Ensure non-negative

        if (GUILayout.Button("Create New Protocol"))
        {
            if (string.IsNullOrWhiteSpace(_protocolName))
            {
                EditorUtility.DisplayDialog("Error", "Protocol Name cannot be empty.", "OK");
            }
            else
            {
                if (EditorUtility.DisplayDialog("Confirm Protocol Creation", 
                    $"Create protocol with:\nName: {_protocolName}\nPublic: {_isPublic}\nOrg ID: {_organizationId}", 
                    "Create", "Cancel"))
                {
                    controller.Editor_CreateNewProtocol(_protocolName, _protocolContent, _isPublic, _organizationId);
                }
            }
        }
    }
}
#endif 