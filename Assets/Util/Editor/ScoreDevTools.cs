using UnityEditor;
using UnityEngine;

/// <summary>Editor-only development tools for the score layer. Not included in builds.</summary>
static class ScoreDevTools
{
    [MenuItem("GamesHub/Reset Played State")]
    static void ResetPlayedState()
    {
        bool ok = EditorUtility.DisplayDialog(
            "Reset Played State",
            "Forget every game's played state on this machine?\n\n" +
            "Clears the attempt marks, queued scores (they are deleted, not sent) and guest scores. " +
            "Your login is kept.\n\n" +
            "Scores already on the server still count. Delete those in the database.",
            "Reset", "Cancel");
        if (!ok) return;

        ScoreService.ResetLocalPlayedState();

        // In Play mode, update the menu's greyed-out slots right away.
        if (EditorApplication.isPlaying)
        {
            MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
            if (menu != null) menu.RefreshPlayedSlots();
        }

        Debug.Log("Played state reset on this machine. To also remove today's server scores for a test account:\n" +
                  "DELETE s FROM scores s JOIN users u ON u.id = s.user_id JOIN games g ON g.id = s.game_id\n" +
                  "WHERE u.email = 'test1@example.com' AND s.puzzle_date = UTC_DATE();  -- add AND g.slug = '...' for one game");
    }
}
