using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>The logged-in player's session. Held in memory only; not persisted between launches.</summary>
public sealed class Session
{
    /// <summary>Bearer token returned by <c>POST /auth/login</c>. Same value as <see cref="ApiClient.AuthToken"/> while logged in.</summary>
    public string token;

    /// <summary>The player's display name (without the discriminator).</summary>
    public string displayName;
}

/// <summary>Outcome of <see cref="AuthService.LoginAsync"/>.</summary>
public enum LoginResult
{
    /// <summary>Logged in; <see cref="AuthService.Current"/> and the API token are set.</summary>
    Success,
    /// <summary>The server answered 401: wrong email or password.</summary>
    InvalidCredentials,
    /// <summary>The server could not be reached or the request timed out.</summary>
    NetworkError,
    /// <summary>Any other HTTP error, or a 2xx response whose body was not valid JSON.</summary>
    ServerError
}

/// <summary>Account data returned in the <c>user</c> field of the login response.</summary>
[Serializable] public class User
{
    /// <summary>Name the player chose. Not unique on its own.</summary>
    public string displayName;

    /// <summary>Number that makes <see cref="displayName"/> unique, Battle.net style (e.g. <c>Name#4821</c>).</summary>
    public int discriminator;

    /// <summary>Whether the account's email address has been confirmed.</summary>
    public bool emailVerified;
}

/// <summary>
/// Logs the player in and out and tracks the current <see cref="Session"/>.
/// </summary>
/// <remarks>
/// Subscribes <see cref="Logout"/> to <see cref="ApiClient.Unauthorized"/>, so any request
/// whose token the server rejects ends the session. Access the game-wide instance through
/// <c>Util.Services.Auth</c>.
/// </remarks>
public class AuthService
{
    /// <summary>JSON body for <c>POST /auth/login</c>.</summary>
    [Serializable] struct LoginRequest
    {
        public string email;
        public string password;
    }
    
    /// <summary>Expected JSON body of a successful <c>POST /auth/login</c> response.</summary>
    [Serializable] struct LoginResponse
    {
        public string token; 
        public User user;
    }

    private readonly ApiClient api;
    
    public const string TokenKey = "GamesHub.AuthToken";

    /// <summary>The current session, or null when logged out.</summary>
    public Session Current { get; private set; }
    
    /// <summary>True when <see cref="Current"/> is not null.</summary>
    public bool IsLoggedIn => Current != null;
    
    /// <summary>
    /// Raised after a successful login with the new session, and after <see cref="Logout"/>
    /// with null. Includes logouts triggered by a 401 on any authenticated request.
    /// </summary>
    public event Action<Session> SessionChanged;

    /// <summary>Creates the service and subscribes <see cref="Logout"/> to <paramref name="api"/>'s <see cref="ApiClient.Unauthorized"/> event.</summary>
    /// <param name="api">The client used for all auth requests. Share it with other services so they use the same token.</param>
    public AuthService(ApiClient api)
    {
        this.api = api;
        api.Unauthorized += Logout;
    }

    /// <summary>
    /// Sends the credentials to <c>POST /auth/login</c>. On success, sets <see cref="Current"/>
    /// and <see cref="ApiClient.AuthToken"/>, then raises <see cref="SessionChanged"/>.
    /// </summary>
    /// <param name="email">Account email address.</param>
    /// <param name="password">Plain-text password; sent in the JSON body.</param>
    /// <returns>
    /// <see cref="LoginResult.Success"/>; <see cref="LoginResult.InvalidCredentials"/> on HTTP 401;
    /// <see cref="LoginResult.NetworkError"/> if the server is unreachable;
    /// <see cref="LoginResult.ServerError"/> for any other HTTP error or unparseable JSON.
    /// </returns>
    /// <remarks>
    /// The request is sent without the current token, so a failed login leaves any existing
    /// session untouched. The token is set before <see cref="SessionChanged"/> fires, so
    /// subscribers can make authenticated requests immediately. Await the result exactly once.
    /// </remarks>
    public async Awaitable<LoginResult> LoginAsync(string email, string password)
    {
        var r = await api.Post<LoginResponse>("/auth/login", new LoginRequest { email = email, password = password }, authenticate: false);

        switch (r.Outcome)
        {
            case ApiOutcome.NetworkError:
                return LoginResult.NetworkError;
            case ApiOutcome.ParseError:
                return LoginResult.ServerError;
            case ApiOutcome.HttpError:
                return r.Status == 401 ? LoginResult.InvalidCredentials : LoginResult.ServerError;
        }
        
        Current = new Session { token = r.Data.token, displayName = r.Data.user.displayName };
        SetToken(r.Data.token);
        SessionChanged?.Invoke(Current);
        return LoginResult.Success;
    }

    /// <summary>
    /// Ends the session locally: clears <see cref="Current"/> and <see cref="ApiClient.AuthToken"/>,
    /// then raises <see cref="SessionChanged"/> with null. Does not contact the server.
    /// </summary>
    public void Logout()
    {
        if (Current == null && api.AuthToken == null) return;
        Current = null;
        SetToken(null);
        SessionChanged?.Invoke(null);
    }
    /// <summary>Sets the API token and its persisted copy together. Null clears both.</summary>
    void SetToken(string token)
    {
        api.AuthToken = token;
        if (token == null) PlayerPrefs.DeleteKey(TokenKey);
        else PlayerPrefs.SetString(TokenKey, token);
        PlayerPrefs.Save();
    }
}
