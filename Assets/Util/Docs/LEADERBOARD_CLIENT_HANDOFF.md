# Handoff: leaderboards in the Unity client

For a Claude instance working in the GamesHub **Unity** repository. The GamesHub server (a separate
repository) now has daily and all-time leaderboards per game. Your job is to build the client side:
calling the two endpoints and showing the boards. This file is self-contained: everything you need
from the server repository is copied here.

Before writing code, read the Unity repository's existing API code: the class that calls the server,
how the session token is stored and sent, how errors (`{ "error": { "code", "message" } }`) are
handled, and especially how score history (`GET /games/{slug}/scores/me`) is requested and parsed.
Follow those patterns; don't build a second HTTP layer. `CLIENT_TODO.md` has a "Leaderboards"
checklist that summarizes the rules below.

## 1. API reference (copied from the server README)

Two boards per game, both readable without login:

- `GET /games/{slug}/leaderboard/daily?puzzleDate=YYYY-MM-DD`: every player's score for one day.
- `GET /games/{slug}/leaderboard/all-time`: every player's **best single day**.

Slugs: `minesweeper`, `sudoku`, `price-is-nice`, `blackjack`, `runner`.

Query parameters:

- `puzzleDate` (daily only, required): the **UTC** date of the puzzle. Any date up to today's UTC
  date. For "today's board", use the same UTC date the game uses to pick and submit the puzzle.
- `limit` (optional): an integer from 1 to 100; default 50. Entries per page.
- `offset` (optional): an integer from 0 to 1,000,000; default 0. Pass the previous page's
  `nextOffset` here to get the next page.

Login is optional. Without an `Authorization` header, `me` is `null`. With a valid
`Authorization: Bearer <token>`, `me` is the player's own entry, wherever it is on the board (or `null`
if they have no score on it). A token that is present but invalid or expired gets `401`, like any
protected endpoint.

Response `200` (daily; all-time is the same without the top-level `puzzleDate`):

```json
{
  "game": "sudoku",
  "formulaVersion": 1,
  "puzzleDate": "2026-11-24",
  "entries": [
    {
      "rank": 1,
      "player": { "displayName": "Dan", "discriminator": 1234 },
      "score": 700,
      "puzzleDate": "2026-11-24",
      "submittedAt": "2026-11-24T20:32:14.643Z"
    },
    {
      "rank": 2,
      "player": { "displayName": "Alice", "discriminator": 5678 },
      "score": 500,
      "puzzleDate": "2026-11-24",
      "submittedAt": "2026-11-24T20:22:14.643Z"
    },
    {
      "rank": 2,
      "player": { "displayName": "Bob", "discriminator": 9012 },
      "score": 500,
      "puzzleDate": "2026-11-24",
      "submittedAt": "2026-11-24T20:42:14.643Z"
    }
  ],
  "nextOffset": 3,
  "me": {
    "rank": 2,
    "player": { "displayName": "Alice", "discriminator": 5678 },
    "score": 500,
    "puzzleDate": "2026-11-24",
    "submittedAt": "2026-11-24T20:22:14.643Z"
  }
}
```

- **Ranks:** higher scores rank first. Equal scores share a rank and the next rank is skipped
  (1, 2, 2, 4). Within a tie, the earlier submission is listed first. Show `rank` as sent; never
  compute ranks from list positions.
- **Formula versions:** only the game's current formula version (`formulaVersion` in the response) is
  ranked. Scores from other versions are left out of both boards.
- **All-time:** one entry per player, their highest daily score. `puzzleDate` and `submittedAt` are
  that day's.
- **Losses** (score 0) are included and rank last.
- **Each player appears at most once per board**, so the tag (`displayName#discriminator`) identifies
  an entry.
- **Paging:** `nextOffset` is `null` on the last page. New scores can arrive while a player pages
  through a board, which shifts later entries down. A player can then appear on two pages: skip
  entries whose tag is already shown.
- **`Cache-Control: no-store`:** re-requesting right after submitting a score shows the new score.
- Every field is always present: `entries` may be `[]`, and `nextOffset` and `me` may be `null`.

Errors:

| Status | Code | Meaning | Client action |
|---|---|---|---|
| 400 | `invalid_slug` | Bad slug | Bug |
| 400 | `invalid_puzzle_date` | Daily: `puzzleDate` missing or not `YYYY-MM-DD` | Bug |
| 400 | `puzzle_date_in_future` | Daily: date after today's UTC date | Bug (probably a local date instead of UTC) |
| 400 | `invalid_limit` / `invalid_offset` | Out of range | Bug |
| 401 | `invalid_token` | Token sent but invalid/expired | Discard the token, show login (the app's existing 401 handling) |
| 404 | `game_not_found` | Unknown slug | Bug |

Network failures and 5xx: show an error state with a retry button.

## 2. JSON parsing: watch for `null`

Check which JSON library the client uses for API responses.

**If it uses Newtonsoft (Json.NET):** use `int?` for `nextOffset` and a nullable class reference for
`me`. Nothing else to do.

**If it uses Unity's `JsonUtility`:** it has no nullable types, so the `null`s are silently lost:

- `"nextOffset": null` deserializes to `0`. Taken literally, "next page at offset 0" reloads the
  first page forever.
- `"me": null` deserializes to an entry object with `rank` 0 and an empty `displayName`, not `null`.

The server never sends `0` for either (`nextOffset` is always ≥ 1 when present, and ranks start at 1),
so these checks are safe: **`nextOffset == 0` means there are no more pages**, and
**`me == null || me.rank == 0` means no entry**. Put those checks in one place (for example,
`HasMorePages` and `HasMe` properties on the response class) with a comment explaining why.

Suggested classes (`[Serializable]`, public fields, for `JsonUtility`):

```csharp
[Serializable] public class LeaderboardPlayer { public string displayName; public int discriminator; }
[Serializable] public class LeaderboardEntry {
    public int rank; public LeaderboardPlayer player; public int score;
    public string puzzleDate;   // "YYYY-MM-DD", a UTC date: don't convert it to local time
    public string submittedAt;  // ISO 8601 timestamp
}
[Serializable] public class LeaderboardResponse {
    public string game; public int formulaVersion;
    public string puzzleDate;   // daily only; empty on the all-time board
    public LeaderboardEntry[] entries;
    public int nextOffset;      // JsonUtility: 0 means null, i.e. no more pages
    public LeaderboardEntry me; // JsonUtility: rank 0 means null, i.e. no entry
}
```

`puzzleDate` is a calendar date, not a point in time: display it as is. Parsing it as a
`DateTime` and converting it to local time shows the previous day for players west of UTC.

## 3. Screens: suggested defaults

Not decided by the developer yet. Ask about anything you'd do differently; otherwise use these:

- **Entry points:** a "Leaderboard" button on each game's menu, and on the results screen after a
  score is submitted. Opening it from the results screen shows the daily board.
- **One screen per game with two tabs:** "Today" (daily board, today's UTC date) and "All time".
  Each tab loads on first open; switching back doesn't reload unless the player refreshes.
- **Rows:** rank, full tag `Name#1234` (always the full tag, never the name alone), and score. On
  All time, also the date of that best score.
- **Ties:** tied players show the same rank number.
- **Your row:** when logged in and `me` is present, highlight the player's row if it's in the loaded
  list, and also pin a copy at the bottom of the screen ("You: #42, 500") so it's visible without
  scrolling.
  - Logged in, but no entry: "You haven't played today" on Today, "No score yet" on All time.
  - Guest: "Log in to see your rank" (button to the login screen). Guests' local scores are not on
    the server boards.
- **Paging:** load 50; a "Load more" button at the end of the list while there are more pages.
  Drop entries whose tag is already shown.
- **States:** loading indicator; empty board ("No scores yet today. Be the first!"); error with retry.
- **Refresh:** a refresh button reloads the current tab from offset 0.

## 4. Things the client must not do

- Don't send a local date: `puzzleDate` must be the UTC date, like score submission.
- Don't re-rank, re-sort, or filter by `formulaVersion` on the client: the server already did.
- Don't cache board responses across screen visits; they change with every submission.
- Don't omit the token when logged in: without it, `me` is always `null`.

## 5. Testing against a local server

The developer runs the server locally (`npm run dev`, `http://localhost:3900`). To get several
players on a board:

- Register and log in a few accounts in the Editor and submit scores for today, or
- ask the developer to load the bulk test data from the server repository's `DEV_COMMANDS.md`
  ("Leaderboard test data": 500 players with scores for the last 60 days, about 150,000 rows).
  Those players have no usable passwords; they only fill the boards. That data covers the past 60
  days, not today: check the daily board with yesterday's date to see a full board (`puzzleDate` is
  accepted for any past date).

Check:

- Ties show shared ranks (1, 2, 2, 4).
- Paging loads every page and then stops. In particular, with `JsonUtility`, the last page doesn't
  restart from the top.
- `me`: logged in with a score (highlighted and pinned), logged in without a score (message), and
  guest (login prompt).
- An expired token on the leaderboard screen leads to the login screen.
- Submitting a score, then opening the board, shows the new score.
- In a timezone west of UTC (or by setting the system clock), the daily board and its dates still
  show the UTC date.
