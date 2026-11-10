using System;
using UnityEngine;

/// <summary>The logged-in player's session.</summary>
/// <remarks>
/// Holds no token: <see cref="ApiClient.AuthToken"/> is the only in-memory copy, and
/// <see cref="AuthService"/> keeps the persisted copy in PlayerPrefs in sync with it.
/// </remarks>
public sealed class Session
{
    /// <summary>The logged-in player, as returned by login or <c>GET /auth/me</c>.</summary>
    public readonly User user;

    public Session(User user) => this.user = user;
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
    /// <summary>Any other HTTP error, or a 2xx response that was unparseable or incomplete.</summary>
    ServerError
}

/// <summary>Outcome of <see cref="AuthService.RestoreAsync"/>.</summary>
public enum RestoreResult
{
    /// <summary>
    /// Logged in with the saved token; <see cref="AuthService.SessionChanged"/> was raised.
    /// Also returned, without a request, when already logged in.
    /// </summary>
    Restored,
    /// <summary>No token is saved. Show the login screen.</summary>
    NoSavedSession,
    /// <summary>The server rejected the saved token (expired, or logged out elsewhere). It was deleted.</summary>
    Expired,
    /// <summary>The server could not be reached. The saved token is kept; call again to retry.</summary>
    NetworkError,
    /// <summary>Any other error. The saved token is kept.</summary>
    ServerError,
    /// <summary>
    /// A login or logout happened while the request was in flight. Its result was discarded;
    /// the newer state stands.
    /// </summary>
    Superseded
}

/// <summary>Account data, as returned in the <c>user</c> field of login and <c>GET /auth/me</c>.</summary>
[Serializable] public class User
{
    /// <summary>Name the player chose. Not unique on its own.</summary>
    public string displayName;

    /// <summary>Number that makes <see cref="displayName"/> unique, Battle.net style (e.g. <c>Name#4821</c>).</summary>
    public int discriminator;

    /// <summary>Whether the account's email address has been confirmed.</summary>
    public bool emailVerified;

    /// <summary>The full tag, e.g. <c>Name#4821</c>. Always display this, never the name alone.</summary>
    public string Tag => $"{displayName}#{discriminator}";
}

/// <summary>
/// Logs the player in and out, restores the session saved by a previous launch, and tracks
/// the current <see cref="Session"/>.
/// </summary>
/// <remarks>
/// Subscribes <see cref="Logout"/> to <see cref="ApiClient.Unauthorized"/>, so any request
/// whose token the server rejects ends the session and deletes the saved token. Access the
/// game-wide instance through <c>Util.Services.Auth</c>.
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

    /// <summary>Expected JSON body of a successful <c>GET /auth/me</c> response.</summary>
    [Serializable] struct MeResponse
    {
        public User user;
    }

    /// <summary>Placeholder response type for endpoints that return no body (<c>204</c>).</summary>
    [Serializable] struct NoContent { }

    /// <summary>PlayerPrefs key holding the session token between launches.</summary>
    public const string TokenKey = "GamesHub.AuthToken";

    readonly ApiClient api;

    /// <summary>The current session, or null when logged out.</summary>
    public Session Current { get; private set; }

    /// <summary>True when <see cref="Current"/> is not null.</summary>
    public bool IsLoggedIn => Current != null;

    /// <summary>
    /// Raised with the new session after a login or restore, and with null when a session
    /// ends, including logouts triggered by a 401 on any authenticated request. Not raised
    /// when there was no session to end.
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
    /// Sends the credentials to <c>POST /auth/login</c>. On success, sets and saves the token,
    /// sets <see cref="Current"/>, then raises <see cref="SessionChanged"/>.
    /// </summary>
    /// <param name="email">Account email address.</param>
    /// <param name="password">Plain-text password; sent in the JSON body.</param>
    /// <returns>
    /// <see cref="LoginResult.Success"/>; <see cref="LoginResult.InvalidCredentials"/> on HTTP 401;
    /// <see cref="LoginResult.NetworkError"/> if the server is unreachable;
    /// <see cref="LoginResult.ServerError"/> for any other HTTP error, or a 2xx response that
    /// was unparseable or lacked the token or user.
    /// </returns>
    /// <remarks>
    /// The request is sent without the current token, so a failed login leaves any existing
    /// session untouched. A successful login replaces an existing session locally but does not
    /// end it on the server; the UI should log out first. The token is set before
    /// <see cref="SessionChanged"/> fires, so subscribers can make authenticated requests
    /// immediately. Await the result exactly once.
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

        // An empty body parses to a default LoginResponse; never accept that as a session.
        if (string.IsNullOrEmpty(r.Data.token) || !IsValidUser(r.Data.user))
        {
            Debug.LogWarning("POST /auth/login succeeded but the response lacks the token or user.");
            return LoginResult.ServerError;
        }

        SetToken(r.Data.token);
        Current = new Session(r.Data.user);
        SessionChanged?.Invoke(Current);
        return LoginResult.Success;
    }

    /// <summary>
    /// Restores the session saved by a previous launch: sends the saved token to
    /// <c>GET /auth/me</c> and, if the server accepts it, sets <see cref="Current"/> and raises
    /// <see cref="SessionChanged"/>. Call once at startup, before showing the login screen.
    /// </summary>
    /// <returns>See <see cref="RestoreResult"/>.</returns>
    /// <remarks>
    /// On a network or server error the saved token is kept, because it may still be valid;
    /// calling again retries. Await the result exactly once.
    /// </remarks>
    public async Awaitable<RestoreResult> RestoreAsync()
    {
        if (IsLoggedIn) return RestoreResult.Restored;

        string token = PlayerPrefs.GetString(TokenKey, "");
        if (string.IsNullOrEmpty(token)) return RestoreResult.NoSavedSession;

        // In memory only: the saved copy is already in PlayerPrefs.
        api.AuthToken = token;
        var r = await api.Get<MeResponse>("/auth/me");

        // 401 with the token still current: ApiClient raised Unauthorized, and Logout cleared
        // the token and deleted the saved copy. (No SessionChanged: there was no session yet.)
        if (r.Outcome == ApiOutcome.HttpError && r.Status == 401 && api.AuthToken == null)
            return RestoreResult.Expired;

        // The token changed while the request was in flight: a login or logout happened, and
        // that newer state wins. Touch nothing.
        if (api.AuthToken != token) return RestoreResult.Superseded;

        if (r.Ok && IsValidUser(r.Data.user))
        {
            Current = new Session(r.Data.user);
            SessionChanged?.Invoke(Current);
            return RestoreResult.Restored;
        }

        // Not logged in. Clear the in-memory token so other services don't send it, but keep
        // the saved copy: it may still be valid once the server is reachable again.
        api.AuthToken = null;
        if (r.Ok) Debug.LogWarning("GET /auth/me succeeded but the response lacks the user.");
        return r.Outcome == ApiOutcome.NetworkError ? RestoreResult.NetworkError : RestoreResult.ServerError;
    }

    /// <summary>
    /// Ends the session on the server with <c>POST /auth/logout</c>, then locally with
    /// <see cref="Logout"/>, whatever the server answered. Does nothing when logged out.
    /// </summary>
    /// <remarks>
    /// The request has to carry the token, so the local logout happens after the response:
    /// up to the 15-second timeout if the server hangs. Disable the logout button while
    /// awaiting. If the request fails, the server-side session expires on its own; the token
    /// is deleted from the client either way. Await the result exactly once.
    /// </remarks>
    public async Awaitable LogoutAsync()
    {
        if (!IsLoggedIn) return;

        string token = api.AuthToken;
        await api.Post<NoContent>("/auth/logout", null);

        // Skip if the token changed meanwhile: a 401 already ran Logout via Unauthorized,
        // or a new login replaced the session and must not be ended.
        if (api.AuthToken == token) Logout();
    }

    /// <summary>
    /// Ends the session locally: clears <see cref="Current"/>, the API token, and the saved
    /// token, then raises <see cref="SessionChanged"/> with null if there was a session.
    /// Does not contact the server; use <see cref="LogoutAsync"/> for a player-initiated logout.
    /// Safe to call repeatedly.
    /// </summary>
    public void Logout()
    {
        bool hadSession = Current != null;
        Current = null;
        SetToken(null);
        if (hadSession) SessionChanged?.Invoke(null);
    }

    /// <summary>Sets the API token and its persisted copy together. Null clears both.</summary>
    void SetToken(string token)
    {
        api.AuthToken = token;
        if (token == null) PlayerPrefs.DeleteKey(TokenKey);
        else PlayerPrefs.SetString(TokenKey, token);
        PlayerPrefs.Save();
    }

    /// <summary>Whether a <c>user</c> object from the server is usable.</summary>
    static bool IsValidUser(User user) => user != null && !string.IsNullOrEmpty(user.displayName);
}
