// Assets/Editor/GamesHubMenu.cs
using UnityEditor;
using UnityEngine;

static class GamesHubMenu
{
    [MenuItem("GamesHub/Clear Saved Session")]
    static void ClearSavedSession()
    {
        PlayerPrefs.DeleteKey(AuthService.TokenKey);
        PlayerPrefs.Save();
        Debug.Log("Cleared the saved session token.");
    }
}