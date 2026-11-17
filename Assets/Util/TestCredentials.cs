/// <summary>
/// The local test account's password for the smoke tests, kept in EditorPrefs (per machine, never in
/// the repo) instead of in a scene. Set it with <c>GamesHub → Test Account Password…</c>.
/// </summary>
public static class TestCredentials
{
    /// <summary>EditorPrefs key holding the password.</summary>
    public const string PasswordKey = "GamesHub.TestAccountPassword";

    /// <summary>The saved password, or empty when none is set. Always empty outside the Editor.</summary>
    public static string Password
    {
        get
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetString(PasswordKey, "");
#else
            return "";
#endif
        }
    }
}
