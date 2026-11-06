# GamesHub Unity Client — Networking & Auth Reference

Nov 5, 2026 · @D

## Overview

Three files form the client's HTTP and login layer. `ApiClient` sends JSON requests to the GamesHub backend and returns every response as an `ApiResult<T>` instead of throwing. `AuthService` uses it to log in and holds the current `Session`. `Services` builds both before the first scene loads and exposes `AuthService` to the rest of the game.

| File | Types | Namespace |
| --- | --- | --- |
| `ApiClient.cs` | `ApiClient`, `ApiResult<T>`, `ApiOutcome` | global |
| `AuthService.cs` | `AuthService`, `Session`, `User`, `LoginResult` | global |
| `Services.cs` | `Services` | `Util` |

The intended order of events at runtime:

1. Before the first scene loads, `Services.init()` creates an `ApiClient` for `http://localhost:3900` and an `AuthService` around it.
2. The `AuthService` constructor subscribes `Logout` to `ApiClient.Unauthorized`.
3. Game code calls `Services.Auth.LoginAsync(email, password)`, which sends `POST /auth/login`.
4. On success, `AuthService` stores a `Session` and raises `SessionChanged`.
5. Any later request that carries a token and gets HTTP 401 raises `Unauthorized`, which runs `Logout`.

`LoginAsync` sends its request with `authenticate: false`, so a wrong password never carries the old token and never triggers step 5.

## ApiClient

`ApiClient` sends one HTTP request per call to `baseUrl + path` and reports the result as an `ApiResult<T>`. HTTP errors, network failures and bad JSON never throw; callers branch on `Outcome`.

| Member | Signature | Behavior |
| --- | --- | --- |
| Constructor | `ApiClient(string baseUrl)` | Stores `baseUrl` with any trailing `/` removed. Paths passed to the request methods must start with `/`. |
| `AuthToken` | `string { get; set; }` | When non-empty, sent as `Authorization: Bearer <token>` on every request that has `authenticate` set to true. |
| `Unauthorized` | `event Action` | Raised when a request that carried a token gets HTTP 401. |
| `Get<T>` | `Awaitable<ApiResult<T>> Get<T>(string path, bool authenticate = true)` | GET, no body. |
| `Post<T>` | `Awaitable<ApiResult<T>> Post<T>(string path, object body, bool authenticate = true)` | POST with `body` serialized to JSON. |
| `Put<T>` | `Awaitable<ApiResult<T>> Put<T>(string path, object body, bool authenticate = true)` | PUT with `body` serialized to JSON. |
| `Delete<T>` | `Awaitable<ApiResult<T>> Delete<T>(string path, bool authenticate = true)` | DELETE, no body. |

Pass `authenticate: false` for endpoints that must never see the current token, such as login or registration. Such a request can never raise `Unauthorized`.

### Request pipeline

All four public methods call the private `Send<T>(method, path, body)`, which:

1. Creates a `UnityWebRequest` with a `DownloadHandlerBuffer` and a 15-second timeout.
2. If `body` is not null, serializes it with `JsonUtility.ToJson`, encodes it as UTF-8 and sets `Content-Type: application/json`.
3. If `authenticate` is true and `AuthToken` is non-empty, adds the `Authorization` header, and records whether it did (`hadToken`).
4. Awaits the request, then maps the result using the table below.
5. On success with a non-empty body, parses it with `JsonUtility.FromJson<T>`. An `ArgumentException` from the parser is logged as a warning and returned as `ParseError`.

| Condition | Outcome | Status | Data |
| --- | --- | --- | --- |
| `ConnectionError` (no connection, DNS failure, timeout) | `NetworkError` | 0 | default |
| Any other failed result (HTTP 4xx/5xx, data-processing error) | `HttpError` | response code | default |
| Success, empty body | `Success` | response code | `default(T)` |
| Success, body parses | `Success` | response code | parsed `T` |
| Success, body is not valid JSON | `ParseError` | response code | default |

The 401 check runs before the `HttpError` return. A 401 on a request with a token therefore raises `Unauthorized` and still returns `HttpError` with status 401 to the caller.

### ApiResult\<T>

A `readonly struct` holding one response. Its constructor is private; instances come only from the factory methods.

| Member | Description |
| --- | --- |
| `Outcome` | Which `ApiOutcome` occurred. |
| `Status` | HTTP response code; 0 for `NetworkError`. |
| `Data` | Parsed body. Only meaningful when `Ok` is true; otherwise `default(T)`. |
| `Ok` | `true` when `Outcome == ApiOutcome.Success`. |
| `Success(status, data)` | Builds a `Success` result. |
| `Fail(status)` | Builds an `HttpError` result. |
| `ParseError(status)` | Builds a `ParseError` result. |
| `Network()` | Builds a `NetworkError` result with status 0. |

### ApiOutcome

| Value | Meaning |
| --- | --- |
| `Success` | The server answered 2xx and the body, if any, parsed. |
| `HttpError` | The server answered with an error code, or Unity could not process the response. |
| `NetworkError` | No response arrived: offline, DNS failure, refused connection or timeout. |
| `ParseError` | The server answered 2xx but the body was not valid JSON. |

## AuthService

`AuthService` logs in against `POST /auth/login`, holds the current `Session`, and logs out locally when the API rejects the token.

| Member | Signature | Behavior |
| --- | --- | --- |
| Constructor | `AuthService(ApiClient api)` | Keeps `api` and subscribes `Logout` to `api.Unauthorized`. |
| `Current` | `Session { get; private set; }` | The logged-in session, or `null` when logged out. |
| `IsLoggedIn` | `bool` | `Current != null`. |
| `SessionChanged` | `event Action<Session>` | Raised with the new session after login, and with `null` after logout. |
| `LoginAsync` | `Awaitable<LoginResult> LoginAsync(string email, string password)` | Sends credentials and maps the response to a `LoginResult`. |
| `Logout` | `void Logout()` | Clears `Current` and `api.AuthToken`, then raises `SessionChanged(null)`. Does not contact the server. |

### LoginAsync

Request body:

```json
{ "email": "player@example.com", "password": "hunter2" }
```

Expected 2xx response body:

```json
{
  "token": "<jwt or opaque token>",
  "user": { "displayName": "Bloon", "discriminator": 4821, "emailVerified": true }
}
```

| `ApiOutcome` | Status | Returns | Side effects |
| --- | --- | --- | --- |
| `Success` | 2xx | `Success` | Sets `Current` from `token` and `user.displayName`, sets `api.AuthToken`, then raises `SessionChanged`. |
| `HttpError` | 401 | `InvalidCredentials` | None; an existing session stays logged in. |
| `HttpError` | anything else | `ServerError` | None. |
| `ParseError` | 2xx | `ServerError` | Warning logged by `ApiClient`. |
| `NetworkError` | 0 | `NetworkError` | None. |

The request is sent with `authenticate: false`. The token is set before `SessionChanged` fires, so subscribers can make authenticated requests immediately. The backend must use 401 for a wrong email or password; any other code, including 400, 403 and 429, reaches the player as `ServerError`.

### Session

`public sealed class Session` with two public fields: `token` (the bearer token from login) and `displayName`. It exists only in memory, so the player logs in again on every launch.

### User

`[Serializable] public class User`, the `user` object in the login response.

| Field | Type | Meaning |
| --- | --- | --- |
| `displayName` | `string` | Name the player chose; not unique. |
| `discriminator` | `int` | Number that makes `displayName` unique, Battle.net style (`Name#4821`). |
| `emailVerified` | `bool` | Whether the account's email is confirmed. |

Only `displayName` is copied into `Session`; the other two fields are discarded after login.

### LoginResult

| Value | Meaning |
| --- | --- |
| `Success` | Logged in; `Current` is set. |
| `InvalidCredentials` | Server answered 401. |
| `NetworkError` | Server unreachable or timed out. |
| `ServerError` | Any other HTTP error, or a 2xx with unparseable JSON. |

`LoginRequest` and `LoginResponse` are private nested `[Serializable]` structs that mirror the two JSON bodies above.

## Services

`Util.Services` is a static service locator: it builds the networking objects once and exposes `AuthService` through `Services.Auth`. Callers need `using Util;`.

| Member | Signature | Behavior |
| --- | --- | --- |
| `Auth` | `static AuthService Auth { get; private set; }` | The game-wide `AuthService`. |
| `init` | `static void init()` | Creates `new ApiClient("http://localhost:3900")` and `new AuthService(api)`. |

`init` carries `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]`, so Unity calls it before any `Awake` in the first scene. `Services.Auth` is therefore safe to use from `Awake`, `OnEnable` and `Start`.

The `ApiClient` is a local variable inside `init` and is not exposed. A future score or profile service must be constructed inside `init` with the same instance, so it shares the token and the `Unauthorized` event.

## Usage examples

### Logging in from a UI panel

Handle the `LoginResult` for error messages, and use `SessionChanged` for anything that must react to login or logout, including logouts caused by a 401 elsewhere.

```csharp
using UnityEngine;
using Util;

public class LoginPanel : MonoBehaviour
{
    void OnEnable()  => Services.Auth.SessionChanged += OnSessionChanged;
    void OnDisable() => Services.Auth.SessionChanged -= OnSessionChanged;

    // Wired to the login button. async void is acceptable for UI event handlers.
    public async void OnLoginClicked(string email, string password)
    {
        LoginResult result = await Services.Auth.LoginAsync(email, password);
        if (this == null) return; // panel destroyed while the request was in flight

        switch (result)
        {
            case LoginResult.Success:            break; // OnSessionChanged updates the UI
            case LoginResult.InvalidCredentials: ShowError("Wrong email or password."); break;
            case LoginResult.NetworkError:       ShowError("Can't reach the server."); break;
            case LoginResult.ServerError:        ShowError("Server error. Try again later."); break;
        }
    }

    void OnSessionChanged(Session session)
    {
        if (session == null) { /* show login form */ }
        else { /* greet session.displayName */ }
    }

    void ShowError(string message) => Debug.Log(message);
}
```

### Calling a new endpoint with ApiClient

Request and response types must be `[Serializable]` with public fields. The endpoint below is illustrative, not one the backend has.

```csharp
[System.Serializable] public class ScoreSubmit { public string gameId; public int score; }
[System.Serializable] public class ScoreAck    { public int rank; }

ApiResult<ScoreAck> r = await api.Post<ScoreAck>("/scores", new ScoreSubmit { gameId = "btd6-wordle", score = 420 });

if (r.Ok)                                       Debug.Log($"Rank {r.Data.rank}");
else if (r.Outcome == ApiOutcome.NetworkError)  Debug.Log("Offline");
else                                            Debug.Log($"Failed: {r.Outcome} {r.Status}");
```

Check `Ok` before reading `Data`: on every non-success outcome `Data` is `default`, which is `null` for classes.

## Requirements and caveats

- **Unity version.** `Awaitable` requires Unity 2023.1 or later (Unity 6).
- **Await each call once.** Unity pools `Awaitable` instances. Await the returned value directly; do not store it, await it twice, or pass it around.
- **Main thread.** Continuations after `await` resume on Unity's main thread, so handlers for `SessionChanged` and code after `await LoginAsync` can touch GameObjects.
- **JsonUtility limits.** Bodies and responses must be `[Serializable]` classes or structs with public (or `[SerializeField]`) fields. Properties, dictionaries and top-level arrays are not supported; wrap arrays as `{ "items": [...] }`.
- **Shape mismatches are silent.** JSON fields missing from the response keep their default values and unknown fields are ignored. A renamed field on the server shows up as `null` or 0, not as `ParseError`.
- **Plain HTTP.** Unity 2022.1 and later block `http://` URLs unless *Player Settings → Other Settings → Allow downloads over HTTP* permits them. The backend needs HTTPS before public deployment regardless.
- **Timeout.** Each request gives up after 15 seconds and returns `NetworkError`.

## Known issues

Nothing below breaks the normal login flow, but items 1 and 2 can crash or throw in edge cases today. Most severe first.

| # | Issue | Effect | Fix |
| --- | --- | --- | --- |
| 1 | A 2xx login response with an empty body gives a `default` `LoginResponse`. | `r.Data.user` is `null`, so `LoginAsync` throws `NullReferenceException`. An empty `token` is also accepted. | Return `ServerError` when `token` is empty or `user` is null. |
| 2 | `Send` catches nothing around `SendWebRequest`. | Exceptions such as Unity's insecure-HTTP block propagate out of `LoginAsync` instead of becoming a result. | Wrap the send in `try/catch` and return `Network()`. |
| 3 | A request sent with an old token can return 401 after a new login succeeds. | The late 401 fires `Unauthorized` and logs out the new session. Rare. | Compare the token the request was sent with against the current `AuthToken` before raising `Unauthorized`. |
| 4 | Every non-401 error maps to `ServerError`. | A rate-limited login (429) or validation error (400) reads to the player as "server down". | Add `LoginResult` values for the codes the backend actually returns. |
| 5 | `DataProcessingError` is reported as `HttpError`. | `Status` can be 200 on an `HttpError`, which is misleading. | Map it to `ParseError` or a new outcome. |
| 6 | `Session` keeps only `displayName`, plus a copy of the token. | `discriminator` and `emailVerified` are lost, so the UI can't show `Name#4821` or prompt for verification. `Session.token` duplicates `api.AuthToken`. | Store the whole `User` in `Session`; drop `Session.token` or make it the single source. |
| 7 | The base URL is hard-coded to `http://localhost:3900`. | Builds can only reach a server on the player's own machine. | Read it from a `ScriptableObject` or set it per build configuration. |
| 8 | Minor style: `Services` is in `Util` while the other types are global; `init` is lowercase; `AuthService.cs` has three unused usings. | Inconsistent with C# conventions. | Put all three files in one namespace; rename to `Init`; remove `System.Collections.Generic`, `System.Text` and `UnityEngine.Networking`. |
