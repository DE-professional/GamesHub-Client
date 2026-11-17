# GamesHub Unity Client — Networking & Auth Reference

Nov 17, 2026 · @D

## Overview

The client's networking layer has three parts:

- **HTTP.** `ApiClient` sends JSON requests to the GamesHub backend and returns every response as an `ApiResult<T>`; it never throws.
- **Login.** `AuthService` logs in and out, saves the token between launches, and holds the current `Session`.
- **Scores.** `ScoreService` sends a logged-in player's scores to the server and keeps a guest's scores on the device.

`Services` builds all of them before the first scene loads and exposes them to the rest of the game.

| File | Types | Namespace |
| --- | --- | --- |
| `ApiClient.cs` | `ApiClient`, `ApiResult<T>`, `ApiOutcome` | global |
| `AuthService.cs` | `AuthService`, `Session`, `User`, `LoginResult`, `RestoreResult` | global |
| `Services.cs` | `Services` | `Util` |
| `GameIds.cs` | `GameId`, `GameIds` | global |
| `ScoreFormulas.cs` | `ScoreFormulas` | global |
| `ScoreStore.cs` | `IScoreStore`, `ScoreRecord`, `SubmitResult`, `PlayedStatus`, `HistoryOutcome`, `HistoryResult` | global |
| `ApiScoreStore.cs` | `ApiScoreStore` | global |
| `LocalScoreStore.cs` | `LocalScoreStore` | global |
| `ScoreService.cs` | `ScoreService` | global |
| `DailyPuzzle.cs` | `DailyPuzzle` | global |

The intended order of events at runtime:

1. Before the first scene loads, `Services.init()` creates one `ApiClient` for `http://localhost:3900`, an `AuthService` around it, and a `ScoreService` built on both.
2. The `AuthService` constructor subscribes `Logout` to `ApiClient.Unauthorized`. The `ScoreService` constructor subscribes to `AuthService.SessionChanged`.
3. When the main menu starts, `MainMenu` calls `Services.Auth.RestoreAsync()`. If a token was saved by an earlier launch, it is checked with `GET /auth/me`.
4. If there is no saved session, the login screen calls `Services.Auth.LoginAsync(email, password)`, which sends `POST /auth/login`.
5. A successful restore or login stores a `Session`, saves the token, and raises `SessionChanged`. `ScoreService` reacts by sending any scores queued while the player was offline.
6. Games call `Services.Scores.MarkAttemptStarted` when play begins and `Services.Scores.SubmitAsync` when it ends.
7. Any later request that carries the current token and gets HTTP 401 raises `Unauthorized`, which runs `Logout`.

## ApiClient

`ApiClient` sends one HTTP request per call to `baseUrl + path` and reports the result as an `ApiResult<T>`. HTTP errors, network failures and bad JSON never throw; callers branch on `Outcome`, and on `ErrorCode` for HTTP errors.

| Member | Signature | Behavior |
| --- | --- | --- |
| Constructor | `ApiClient(string baseUrl)` | Stores `baseUrl` with any trailing `/` removed. Paths passed to the request methods must start with `/`. |
| `AuthToken` | `string { get; set; }` | When non-empty, sent as `Authorization: Bearer <token>` on every request that has `authenticate` set to true. |
| `Unauthorized` | `event Action` | Raised when a request that carried the *current* token gets HTTP 401. |
| `Get<T>` | `Awaitable<ApiResult<T>> Get<T>(string path, bool authenticate = true)` | GET, no body. |
| `Post<T>` | `Awaitable<ApiResult<T>> Post<T>(string path, object body, bool authenticate = true)` | POST with `body` serialized to JSON. A null `body` sends no body. |
| `Put<T>` | `Awaitable<ApiResult<T>> Put<T>(string path, object body, bool authenticate = true)` | PUT with `body` serialized to JSON. |
| `Delete<T>` | `Awaitable<ApiResult<T>> Delete<T>(string path, bool authenticate = true)` | DELETE, no body. |

Pass `authenticate: false` for endpoints that must never see the current token, such as login or registration. Such a request can never raise `Unauthorized`.

### Request pipeline

All four public methods call the private `Send<T>(method, path, body, authenticate)`, which:

1. If `authenticate` is true and `AuthToken` is non-empty, remembers that token as `sentToken`.
2. Builds the request in `CreateRequest`: a `UnityWebRequest` with a `DownloadHandlerBuffer` and a 15-second timeout. If `body` is not null, it is serialized with `JsonUtility.ToJson`, encoded as UTF-8 and sent with `Content-Type: application/json`. If there is a `sentToken`, it goes in the `Authorization` header.
3. Awaits the request, then maps the result using the table below.
4. On an HTTP error, reads `error.code` from the server's error body into `ErrorCode`.
5. On success with a non-empty body, parses it with `JsonUtility.FromJson<T>`. An `ArgumentException` from the parser is logged as a warning and returned as `ParseError`.

No exception escapes except `OperationCanceledException`, for example when Play mode ends mid-request. An exception while building the request is a client bug, such as a body `JsonUtility` can't serialize or a header with invalid characters. An exception from `SendWebRequest` usually means the request was blocked before it went out, such as Unity's "Insecure connection not allowed" for `http://` URLs. Both are logged with `Debug.LogError` and returned as `NetworkError`.

| Condition | Outcome | Status | Data | ErrorCode |
| --- | --- | --- | --- | --- |
| `ConnectionError` (no connection, DNS failure, timeout) | `NetworkError` | 0 | default | null |
| Exception while building or sending (logged as an error) | `NetworkError` | 0 | default | null |
| Any other failed result (HTTP 4xx/5xx, data-processing error) | `HttpError` | response code | default | `error.code`, or null |
| Success, empty body | `Success` | response code | `default(T)` | null |
| Success, body parses | `Success` | response code | parsed `T` | null |
| Success, body is not valid JSON | `ParseError` | response code | default | null |

The 401 check runs before the `HttpError` return. It raises `Unauthorized` only when the request carried a token *and* that token still equals `AuthToken`. A late 401 for a token that a newer login has replaced is returned to the caller but does not log the new session out. Either way the caller gets `HttpError` with status 401.

### Server error codes

Every backend error body has the form `{ "error": { "code": "...", "message": "..." } }`. `ErrorCode` holds `code`; branch on it, never on `message`, which may be reworded. `ErrorCode` is null for an empty body, for non-JSON such as a proxy's HTML page, and for JSON without the field.

| Status | Code | When |
| --- | --- | --- |
| 400 | `invalid_json`, `invalid_body` | Malformed or wrongly shaped body |
| 401 | `authentication_required` | No `Authorization` header on a protected endpoint |
| 401 | `invalid_token` | Token malformed, unknown, expired or logged out |
| 401 | `invalid_credentials` | Wrong email or password at login |
| 404 | `route_not_found` | No such endpoint |
| 413 | `payload_too_large` | Body over 16 KB |
| 415 | `unsupported_media_type` | Body is not JSON |
| 500 | `internal_error` | Server bug |

Endpoint-specific codes are listed with each endpoint below. The backend's `README.md` (copied to `Docs/`) is the full reference.

### ApiResult\<T>

A `readonly struct` holding one response. Its constructor is private; instances come only from the factory methods.

| Member | Description |
| --- | --- |
| `Outcome` | Which `ApiOutcome` occurred. |
| `Status` | HTTP response code; 0 for `NetworkError`. |
| `Data` | Parsed body. Only meaningful when `Ok` is true; otherwise `default(T)`. |
| `ErrorCode` | The server's `error.code` on `HttpError`; null otherwise. |
| `Ok` | `true` when `Outcome == ApiOutcome.Success`. |
| `Success(status, data)` | Builds a `Success` result. |
| `Fail(status, errorCode)` | Builds an `HttpError` result. |
| `ParseError(status)` | Builds a `ParseError` result. |
| `Network()` | Builds a `NetworkError` result with status 0. |

### ApiOutcome

| Value | Meaning |
| --- | --- |
| `Success` | The server answered 2xx and the body, if any, parsed. |
| `HttpError` | The server answered with an error code, or Unity could not process the response. |
| `NetworkError` | No response arrived: offline, DNS failure, refused connection, timeout, or the request couldn't be built or sent (logged as an error). |
| `ParseError` | The server answered 2xx but the body was not valid JSON. |

## AuthService

`AuthService` logs in against `POST /auth/login`, restores a saved session with `GET /auth/me`, logs out with `POST /auth/logout`, holds the current `Session`, and logs out locally when the API rejects the token.

| Member | Signature | Behavior |
| --- | --- | --- |
| Constructor | `AuthService(ApiClient api)` | Keeps `api` and subscribes `Logout` to `api.Unauthorized`. |
| `TokenKey` | `const string` = `"GamesHub.AuthToken"` | PlayerPrefs key holding the token between launches. |
| `Current` | `Session { get; private set; }` | The logged-in session, or `null` when logged out. |
| `IsLoggedIn` | `bool` | `Current != null`. |
| `SessionChanged` | `event Action<Session>` | Raised with the new session after a login or restore, and with `null` when a session ends. Not raised when there was no session to end. |
| `LoginAsync` | `Awaitable<LoginResult> LoginAsync(string email, string password)` | Sends credentials and maps the response to a `LoginResult`. |
| `RestoreAsync` | `Awaitable<RestoreResult> RestoreAsync()` | Checks the saved token with the server. Call once at startup. |
| `LogoutAsync` | `Awaitable LogoutAsync()` | Ends the session on the server, then locally. Use for a player-initiated logout. |
| `Logout` | `void Logout()` | Ends the session locally only: clears `Current`, `api.AuthToken` and the saved token. Safe to call repeatedly. |

The token lives in two places, always set together by the private `SetToken`: `api.AuthToken` in memory, and PlayerPrefs under `TokenKey` on disk. On WebGL, PlayerPrefs is backed by IndexedDB.

### LoginAsync

Request body, sent with `authenticate: false`:

```json
{ "email": "player@example.com", "password": "at least twelve chars" }
```

Expected 2xx response body (`expiresAt` is ignored by the client):

```json
{
  "token": "43 characters",
  "expiresAt": "2026-12-03T17:00:00.000Z",
  "user": { "displayName": "Bloon", "discriminator": 4821, "emailVerified": true }
}
```

| `ApiOutcome` | Status | Returns | Side effects |
| --- | --- | --- | --- |
| `Success` | 2xx, with a token and a user that has a `displayName` | `Success` | Sets and saves the token, sets `Current`, then raises `SessionChanged`. |
| `Success` | 2xx, token or user missing | `ServerError` | Warning logged. Nothing else changes. |
| `HttpError` | 401 | `InvalidCredentials` | None; an existing session stays logged in. |
| `HttpError` | anything else | `ServerError` | None. |
| `ParseError` | 2xx | `ServerError` | Warning logged by `ApiClient`. |
| `NetworkError` | 0 | `NetworkError` | None. |

The token is set before `SessionChanged` fires, so subscribers can make authenticated requests immediately. A successful login replaces an existing session locally but doesn't end it on the server, so the UI should log out first.

### RestoreAsync

| Situation | Returns | Side effects |
| --- | --- | --- |
| Already logged in | `Restored` | None; no request. |
| No saved token | `NoSavedSession` | None. Show the login screen. |
| `GET /auth/me` succeeds with a valid user | `Restored` | Sets `Current` and raises `SessionChanged`. |
| Server answers 401 | `Expired` | `Unauthorized` runs `Logout`, which deletes the saved token. No `SessionChanged`, because there was no session yet. |
| A login or logout happened while the request was in flight | `Superseded` | None; the newer state stands. |
| Server unreachable | `NetworkError` | The in-memory token is cleared; the saved copy is kept. Call again to retry. |
| Any other error, or a 2xx without a user | `ServerError` | Same as `NetworkError`. |

While a saved token exists but hasn't been restored (`IsLoggedIn` is false), `ScoreService` treats the player as having an account, not as a guest. See [ScoreService](#scoreservice).

### LogoutAsync

Does nothing when logged out. Otherwise it sends `POST /auth/logout` with the token, then calls `Logout` whatever the server answered. The skip is if the token changed during the request: a 401 already ran `Logout`, or a new login must not be ended. The local logout waits for the response, which can take up to the 15-second timeout, so disable the logout button while awaiting.

### Session

`public sealed class Session` with one public field, `readonly User user`. It holds no token: `api.AuthToken` is the only in-memory copy.

### User

`[Serializable] public class User`, the `user` object from login and `GET /auth/me`.

| Member | Type | Meaning |
| --- | --- | --- |
| `displayName` | `string` | Name the player chose; not unique. |
| `discriminator` | `int` | Number that makes `displayName` unique, Battle.net style. |
| `emailVerified` | `bool` | Whether the account's email is confirmed. |
| `Tag` | `string` (property) | `displayName#discriminator`, e.g. `Bloon#4821`. Always display this, never the name alone. |

### LoginResult

| Value | Meaning |
| --- | --- |
| `Success` | Logged in; `Current` and the token are set. |
| `InvalidCredentials` | Server answered 401. |
| `NetworkError` | Server unreachable or timed out. |
| `ServerError` | Any other HTTP error, or a 2xx that was unparseable or incomplete. |

### RestoreResult

| Value | Meaning |
| --- | --- |
| `Restored` | Logged in with the saved token, or already logged in. |
| `NoSavedSession` | No token saved. Show the login screen. |
| `Expired` | The server rejected the saved token; it was deleted. |
| `NetworkError` | Server unreachable. The saved token is kept. |
| `ServerError` | Any other error. The saved token is kept. |
| `Superseded` | A login or logout happened during the request; its result was discarded. |

## Scores

### Backend rules the client relies on

- **Endpoints:** `POST /games/:slug/scores` submits one score; `GET /games/:slug/scores/me?limit=&before=` pages through the player's history, newest first. Both require login. There is no leaderboard endpoint yet.
- **Score values:** integers from 0 to 1,000,000, and higher is always better. A loss is submitted as 0, and every completed game must score at least 1 so the two can be told apart.
- **One submission per game per day.** A second one gets `409 already_submitted`, and the first stands. The server can't stop replays, so "one *attempt* per day" is the client's job.
- **Puzzle dates:** UTC `yyyy-MM-dd`, sent by the client. The server accepts today's date, or yesterday's during the first hour after midnight UTC; any other date gets `400 puzzle_date_closed`. Never convert a puzzle date to local time.
- **Formula versions:** each score carries the version of the formula that produced it. Scores from different versions are never compared.

### GameId and GameIds

`GameId` is a `readonly struct` with `Slug` and `FormulaVersion`. `GameIds` holds one for each game, plus `GameIds.All` and `GameIds.TryFind(slug, out GameId)`:

| Field | Slug | Formula version |
| --- | --- | --- |
| `GameIds.Minesweeper` | `minesweeper` | 1 |
| `GameIds.Sudoku` | `sudoku` | 1 |
| `GameIds.PriceIsNice` | `price-is-nice` | 1 |
| `GameIds.Blackjack` | `blackjack` | 1 |
| `GameIds.Runner` | `runner` | 1 |

Use `GameIds.X.Slug` for `DailyPuzzle.Seed` as well as for submission, so the puzzle and the score can never use different slugs. Slugs are permanent on the server.

### ScoreFormulas

Every game's formula, in one place. When a formula changes, raise that game's `FormulaVersion` in `GameIds` in the same commit.

| Method | Formula | Examples |
| --- | --- | --- |
| `Blackjack(chipsLeft)` | Chips left after the day's hands, 0..1,000,000 | Out of chips = 0 (loss) |
| `PriceIsNice(triesRemaining, maxTries, seconds)` | 5000 × (1 + tries/maxTries) × 30 / (30 + seconds) | First try in 0 s = 10000 |
| `Minesweeper(seconds)` | 10000 × 120 / (120 + seconds) | 2 min = 5000, 6 min = 2500 |
| `Sudoku(seconds, mistakes)` | 10000 × 300 / (300 + seconds + 30 × mistakes) | 5 min, no mistakes = 5000 |
| `Runner(distance)` | The distance shown on screen, 1..1,000,000 | Every run counts as completed |

`ScoreFormulas.Loss` (0) is the score for a lost game, and `ScoreFormulas.MaxScore` is the server's 1,000,000 limit. All win formulas round half away from zero and are clamped to 1..`MaxScore`.

### IScoreStore

The interface for the Strategy pattern. Game code doesn't use the stores directly; it uses `Services.Scores`, which implements the same interface.

| Method | Behavior |
| --- | --- |
| `Awaitable<SubmitResult> SubmitAsync(GameId game, string puzzleDate, int score)` | Saves the score for that game and day. |
| `Awaitable<PlayedStatus> GetPlayedStatusAsync(GameId game, string puzzleDate)` | Whether that day's puzzle was already played. |
| `Awaitable<HistoryResult> GetHistoryAsync(GameId game)` | All of the player's scores for the game's current formula version, newest first. |

`ScoreRecord` (`[Serializable]`) has the server's score shape: `game`, `puzzleDate`, `score`, `formulaVersion`, `submittedAt` (an ISO 8601 UTC timestamp). Server responses and guest scores both use it.

### SubmitResult

| Value | Meaning | From `ScoreService`? |
| --- | --- | --- |
| `Accepted` | Stored: on the server for a player, on the device for a guest. | yes |
| `Queued` | Saved on the device; it will be sent when the player is logged in again. | yes |
| `AlreadySubmitted` | A score for this game and day already exists. | yes |
| `DateClosed` | The server no longer accepts that date. | yes |
| `Rejected` | The server rejected the request as invalid. A client bug; logged as an error. | yes |
| `NotLoggedIn`, `NetworkError`, `ServerError` | Returned by `ApiScoreStore` only. `ScoreService` turns them into `Queued`. | no |

`PlayedStatus` is `NotPlayed`, `Played` or `Unknown`. `Unknown` means the server couldn't be asked, or a saved login hasn't been restored yet. `HistoryResult` has `Outcome` (`Success`, `NotLoggedIn`, `NetworkError` or `ServerError`), `Ok`, and `Scores`, which is never null and is empty unless `Ok`.

### ApiScoreStore

`ApiScoreStore(ApiClient api)` must get the same `ApiClient` as `AuthService`, so it sends the current token and a 401 logs the player out.

`SubmitAsync` sends `{ "puzzleDate", "score", "formulaVersion" }` and maps the result:

| Response | `SubmitResult` |
| --- | --- |
| 201 | `Accepted` |
| `409 already_submitted` | `AlreadySubmitted` |
| `400 puzzle_date_closed` | `DateClosed` |
| 401 | `NotLoggedIn` |
| Any other 4xx (`invalid_*`, `game_not_found`, ...) | `Rejected`, logged as an error |
| 5xx, or a 2xx with unparseable JSON | `ServerError` |
| No response | `NetworkError` |

`GetPlayedStatusAsync` requests `?limit=1` and compares the newest score's `puzzleDate` with the given date; any failure gives `Unknown`. `GetHistoryAsync` follows `nextBefore` in pages of 100 until it is null, keeps only scores whose `formulaVersion` is the current one, and stops after 50 pages or if `nextBefore` doesn't advance.

### LocalScoreStore

Guest scores, kept in PlayerPrefs under `GamesHub.GuestScores.<slug>` as one JSON list per game. It follows the server's one-score-per-day rule and keeps at most the newest 366 scores per game, because WebGL PlayerPrefs is limited to 1 MB in total. It doesn't check the date window. Guest scores are never uploaded. `DiscardAll()` deletes them, and should be called when a guest registers; the registration screen should warn about this.

### ScoreService

The game-wide implementation, `Services.Scores`. Constructor: `ScoreService(AuthService auth, IScoreStore remote, LocalScoreStore local)`.

**Which store is used:**

| State | `SubmitAsync` | `GetPlayedStatusAsync` | `GetHistoryAsync` |
| --- | --- | --- | --- |
| Logged in | Server; on failure, queued | Server | Server |
| Saved token, not restored yet (e.g. offline at launch) | Queued | `Unknown` | `NotLoggedIn` |
| Guest (no saved token) | Device | Device | Device |

`GetPlayedStatusAsync` first checks two local sources: the attempt mark (below) and the queue. Either one returns `Played` without asking a store.

**Queue.** When a logged-in submit returns `NetworkError`, `ServerError` or `NotLoggedIn`, or a player whose saved login hasn't been restored submits, the score is saved under `GamesHub.PendingScores` and `Queued` is returned. Each queued score stores the account's tag, or an empty tag if it was earned before the saved login was restored. If the same game, date and tag are already queued, the new score is ignored: the first one stands, as on the server.

`FlushPendingAsync()` sends queued scores and runs automatically:

- whenever `SessionChanged` reports a new session (login or restore), and
- after every submit that reached the server.

It first drops scores whose date the server no longer accepts (older than the UTC date one hour ago). It then sends, in order, every score whose tag matches the current account or is empty. It stops at the first network or server error and keeps the rest for the next attempt. Scores that come back as anything else are removed from the queue, with a warning for `DateClosed` or `Rejected`. If the session changes during a send, it stops. Only one flush runs at a time.

**One attempt per day.** `MarkAttemptStarted(GameId, puzzleDate)` writes the date to `GamesHub.Attempt.<slug>`. Call it when play actually begins, not when the scene loads, so quitting partway still uses up the day. The mark is per device, not per account.

**Other members:** `DiscardGuestScores()` calls `LocalScoreStore.DiscardAll()`.

**Main menu.** `MainMenu` refreshes each game slot's status when it starts (after `RestoreAsync`), when the games panel opens, and on every `SessionChanged`. A slot whose status is `Played` gets `interactable = false`, which `RetroButton` draws greyed out and sunk. `Unknown` stays playable. That status only happens when this device hasn't started today's puzzle and the server can't be asked; the server still refuses a second score for the day, and the queue drops it. Slots map to games through `MainMenu.gameSlugs`, which follows the order of `gameSceneNames`. A blank or unknown slug is logged once and never greyed out.

### PlayerPrefs keys

| Key | Written by | Contents |
| --- | --- | --- |
| `GamesHub.AuthToken` | `AuthService` | The session token |
| `GamesHub.PendingScores` | `ScoreService` | Queued scores, `{ "items": [...] }` |
| `GamesHub.Attempt.<slug>` | `ScoreService` | The puzzle date of the last attempt started |
| `GamesHub.GuestScores.<slug>` | `LocalScoreStore` | Guest scores for one game, `{ "items": [...] }` |

To make games playable again during development, use *GamesHub → Reset Played State* in the Editor (`Editor/ScoreDevTools.cs`). It calls `ScoreService.ResetLocalPlayedState()`, which deletes the attempt marks, queued scores and guest scores but keeps `GamesHub.AuthToken`. In Play mode it also refreshes the main menu. A score already on the server still counts as played; delete it in the local database (the menu logs the SQL).

### DailyPuzzle

`DailyPuzzle.TodayUtc()` returns today's UTC date as `yyyy-MM-dd` (InvariantCulture). `DailyPuzzle.Seed(slug, puzzleDate)` returns a non-negative seed that is the same on every device. Take the date **once** when a puzzle starts, and use that same string for the seed, `MarkAttemptStarted` and `SubmitAsync`. A game started just before midnight UTC then submits the date of the puzzle that was actually played, which the server accepts during its grace hour.

## Services

`Util.Services` is a static service locator: it builds the networking objects once and exposes them. Callers need `using Util;`.

| Member | Signature | Behavior |
| --- | --- | --- |
| `Auth` | `static AuthService Auth { get; private set; }` | The game-wide `AuthService`. |
| `Scores` | `static ScoreService Scores { get; private set; }` | The game-wide `ScoreService`. |
| `init` | `static void init()` | Creates one `ApiClient("http://localhost:3900")`, then `Auth`, then `Scores` with an `ApiScoreStore` on the same client and a new `LocalScoreStore`. |

`init` carries `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]`, so Unity calls it before any `Awake` in the first scene, and `Services.Auth` and `Services.Scores` are safe to use from `Awake` onward. The `ApiClient` is not exposed. Any future service must be built inside `init` with the same instance, so it shares the token and the `Unauthorized` event.

## Usage examples

### Startup and login

```csharp
using UnityEngine;
using Util;

public class LoginPanel : MonoBehaviour
{
    void OnEnable()  => Services.Auth.SessionChanged += OnSessionChanged;
    void OnDisable() => Services.Auth.SessionChanged -= OnSessionChanged;

    async void Start()
    {
        RestoreResult r = await Services.Auth.RestoreAsync();
        if (this == null) return;
        if (r == RestoreResult.NetworkError) ShowError("Can't reach the server. Playing offline.");
        // Restored: OnSessionChanged already ran. Anything else: show the login form.
    }

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
        else { /* greet session.user.Tag */ }
    }

    void ShowError(string message) => Debug.Log(message);
}
```

Use `SessionChanged` for anything that must react to login or logout, including logouts caused by a 401 elsewhere.

### A game submitting its score

The pattern every game follows:

```csharp
using UnityEngine;
using Util;

public class ExampleGame : MonoBehaviour
{
    string date;

    void Start()
    {
        date = DailyPuzzle.TodayUtc(); // once; used for the seed and the score
        var random = new System.Random(DailyPuzzle.Seed(GameIds.Sudoku.Slug, date));
    }

    void OnPlayBegins() => Services.Scores.MarkAttemptStarted(GameIds.Sudoku, date);

    void OnWin(float seconds, int mistakes) => SubmitScore(ScoreFormulas.Sudoku(seconds, mistakes));
    void OnLoss()                           => SubmitScore(ScoreFormulas.Loss);

    async void SubmitScore(int score)
    {
        SubmitResult result = await Services.Scores.SubmitAsync(GameIds.Sudoku, date, score);
        if (this == null) return;
        // Accepted / Queued: saved. AlreadySubmitted: today's first score stands.
        // DateClosed: played too long after midnight UTC. Rejected: a bug, already logged.
    }
}
```

### Checking whether today's puzzle was played

```csharp
PlayedStatus status = await Services.Scores.GetPlayedStatusAsync(GameIds.Sudoku, DailyPuzzle.TodayUtc());
// Played: show the result screen instead of the puzzle.
// Unknown: offline or not yet restored. Each game decides whether to allow play.
```

### Calling a new endpoint with ApiClient

Request and response types must be `[Serializable]` with public fields whose names match the JSON keys exactly.

```csharp
[System.Serializable] class GameInfo { public string slug; public string name; }

ApiResult<GameInfo> r = await api.Get<GameInfo>("/games/sudoku", authenticate: false);

if (r.Ok)                                       Debug.Log(r.Data.name);
else if (r.Outcome == ApiOutcome.NetworkError)  Debug.Log("Offline");
else if (r.ErrorCode == "game_not_found")       Debug.Log("No such game");
else                                            Debug.Log($"Failed: {r.Outcome} {r.Status} {r.ErrorCode}");
```

Check `Ok` before reading `Data`: on every non-success outcome `Data` is `default`, which is `null` for classes.

## Smoke tests

Temporary scripts for checking the layer against a local server. Add one to an empty scene and press Play. Use `@example.com` test accounts, and never commit a scene with a password filled in.

| Script | What it checks |
| --- | --- |
| `ApiClientSmokeTest` | Error codes, and that `Unauthorized` fires for a rejected token but not for a stale one. |
| `AuthSmokeTest` | Restore, login and optional logout. |
| `ScoreSmokeTest` | Four modes (`Online`, `QueueSubmit`, `QueueFlush`, `Guest`): formulas, submit, duplicates, closed dates, played status, history, the offline queue and guest scores. Logs PASS or FAIL per check. Submits a real score for today, so it uses up that game's daily attempt for the test account. |

## Requirements and caveats

- **Unity version.** `Awaitable` requires Unity 2023.1 or later (Unity 6).
- **Await each call once.** Unity pools `Awaitable` instances. Await the returned value directly; do not store it, await it twice, or pass it around.
- **Main thread.** Continuations after `await` resume on Unity's main thread, so handlers for `SessionChanged` and code after `await` can touch GameObjects. Check `this == null` after an await in a MonoBehaviour.
- **JsonUtility limits.** Bodies and responses must be `[Serializable]` classes or structs with public (or `[SerializeField]`) fields. Properties, dictionaries and top-level arrays are not supported; wrap arrays as `{ "items": [...] }`. A JSON `null` string may read back as `""`, so test with `string.IsNullOrEmpty`.
- **Shape mismatches are silent.** JSON fields missing from the response keep their default values and unknown fields are ignored. A renamed field on the server shows up as `null` or 0, not as `ParseError`.
- **Plain HTTP.** Unity blocks `http://` URLs unless *Player Settings → Other Settings → Allow downloads over HTTP* permits them. WebGL builds can only use an HTTPS API with CORS, which the backend doesn't have yet.
- **Timeout.** Each request gives up after 15 seconds and returns `NetworkError`.
- **Scores are client-reported.** The server trusts them; anything the client computes can be faked.

## Known issues

Most severe first.

| # | Issue | Effect | Fix |
| --- | --- | --- | --- |
| 1 | Only the main menu restores the session and checks for replays. | A game scene opened directly (in the Editor) skips both, so it treats a saved login as not yet restored and lets a played puzzle start again. Builds always start at the menu. | Restore in `Services` at startup, and check the status on each game's title screen as well. |
| 2 | Queued scores with an empty tag (earned before a saved login was restored) go to the first account that logs in. | If a different account logs in on that device, it gets the score. Rare. | Store the saved token's user tag in PlayerPrefs at login and use it for queued scores. |
| 3 | The attempt mark is per device, not per account. | After a guest plays, an account on the same device is told the puzzle was already played, and the reverse. | Key the mark by account tag (`"guest"` for guests) if this matters. |
| 4 | Every non-401 login error maps to `ServerError`. | A rate-limited login (429) or validation error (400) reads to the player as "server down". | Add `LoginResult` values for the codes the backend actually returns. |
| 5 | `DataProcessingError` is reported as `HttpError`. | `Status` can be 200 on an `HttpError`, which is misleading. | Map it to `ParseError` or a new outcome. |
| 6 | The base URL is hard-coded to `http://localhost:3900`. | Builds can only reach a server on the player's own machine. | Read it from a `ScriptableObject` or set it per build configuration. |
| 7 | Queued scores are not included in `GetHistoryAsync`. | A score played offline appears in history only after it is sent. | Merge pending scores into the result, or show them separately as "not yet sent". |
| 8 | Minesweeper's "Daily Best" and Runner's "Best" are kept separately in PlayerPrefs; Minesweeper's uses local time. | They can disagree with the server's history, and Minesweeper's resets at local midnight instead of UTC midnight. | Show the best from `GetHistoryAsync` instead. |
| 9 | A build or send exception reads as `NetworkError`. | A client bug, such as an unserializable body, looks like being offline to the caller. `ScoreService` queues the score and retries it on every flush until its day closes. The error log is the only signal. | Add an `ApiOutcome` for client-side failures if this ever hides a real bug. |
| 10 | Minor style: `Services` is in `Util` while the other types are global; `init` is lowercase. | Inconsistent with C# conventions. | Put all the files in one namespace; rename to `Init`. |

Fixed since Nov 5: an empty login body no longer throws (it returns `ServerError`); a late 401 for a replaced token no longer logs out the new session; `Session` now keeps the whole `User` and no token copy; `AuthService` has no unused usings; sessions persist between launches (`RestoreAsync`); logout reaches the server (`LogoutAsync`); exceptions from building or sending a request become `NetworkError` instead of escaping (Nov 17); `Send`'s doc comment is back above `Send`; and the main menu restores the session and greys out games already played today.
