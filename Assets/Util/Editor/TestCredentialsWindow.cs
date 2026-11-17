using UnityEditor;
using UnityEngine;

/// <summary>Sets the smoke tests' test account password in EditorPrefs (<see cref="TestCredentials"/>).</summary>
class TestCredentialsWindow : EditorWindow
{
    string password;

    [MenuItem("GamesHub/Test Account Password…")]
    static void Open()
    {
        var window = GetWindow<TestCredentialsWindow>(true, "Test Account Password");
        window.minSize = window.maxSize = new Vector2(380, 130);
        window.password = TestCredentials.Password;
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Used by AuthSmokeTest and ScoreSmokeTest. Saved in EditorPrefs on this machine only, " +
            "not in any scene or file in the repo. Use a local test account (@example.com).",
            MessageType.Info);

        password = EditorGUILayout.PasswordField("Password", password);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save"))
        {
            EditorPrefs.SetString(TestCredentials.PasswordKey, password ?? "");
            Close();
        }
        if (GUILayout.Button("Clear"))
        {
            EditorPrefs.DeleteKey(TestCredentials.PasswordKey);
            password = "";
        }
        EditorGUILayout.EndHorizontal();
    }
}
