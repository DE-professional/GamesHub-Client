using UnityEngine;
using Util;

/// <summary>Temporary: exercises restore, login, and logout against the local server. Delete after piece 2.</summary>
public class AuthSmokeTest : MonoBehaviour
{
    // Local test account only (@example.com). Don't commit a scene with these filled in.
    [SerializeField] string email = "test1@example.com";
    [SerializeField] string password = "";
    [SerializeField] bool logoutAtEnd;

    async void Start()
    {
        Services.Auth.SessionChanged += s => Debug.Log($"SessionChanged: {(s == null ? "null" : s.user.Tag)}");

        RestoreResult restore = await Services.Auth.RestoreAsync();
        Debug.Log($"Restore: {restore}");

        if (!Services.Auth.IsLoggedIn && restore != RestoreResult.NetworkError)
        {
            LoginResult login = await Services.Auth.LoginAsync(email, password);
            Debug.Log($"Login: {login}");
        }

        if (logoutAtEnd)
        {
            await Services.Auth.LogoutAsync();
            Debug.Log($"After LogoutAsync: IsLoggedIn={Services.Auth.IsLoggedIn}, saved token={PlayerPrefs.HasKey(AuthService.TokenKey)}");
        }
    }
}
