## Leaderboards (server decisions 51–61, November 24, 2026)

Endpoints and response shapes: README.md in the server repo, "Leaderboards".

- [ ] **Two boards per game.** Daily: `GET /games/{slug}/leaderboard/daily?puzzleDate=YYYY-MM-DD`,
      with today's **UTC** date (the same date used to pick and submit the puzzle). All-time:
      `GET /games/{slug}/leaderboard/all-time`. A date after today's UTC date gets
      `400 puzzle_date_in_future`.
- [ ] **Send the token when logged in** (`Authorization: Bearer ...`), so the response includes `me`:
      the player's own entry, with their real rank even when they're not on the loaded pages. Show it
      pinned (for example at the bottom of the board). `me` is `null` for guests and for a player with
      no score on that board ("You haven't played today" / "No score yet").
- [ ] **On `401` from a leaderboard request, treat it like any other 401:** discard the stored
      token and show login. A leaderboard request without a token never gets a 401, so guests can
      always read the boards.
- [ ] **Paging:** request with `limit` (1–100, default 50) and `offset` (start at 0). Load the next
      page by passing the response's `nextOffset` as `offset`; stop when it is `null`.
- [ ] **Skip duplicate players while paging.** New scores can arrive between page requests and push
      an entry onto the next page. Each player appears at most once per board, so drop any entry whose
      tag (`displayName#discriminator`) is already shown.
- [ ] **Show ranks from the server, don't compute them.** Equal scores share a rank and the next rank
      is skipped (1, 2, 2, 4). The list is already in display order (within a tie, earlier submission first).
- [ ] **All-time shows each player's best single day.** Each entry's `puzzleDate` is the day of that
      score; consider showing it ("900, Nov 21").
- [ ] **Losses (score 0) are on the boards** and rank last. This is another reason a completed game
      must always score ≥ 1.
- [ ] **Show the full tag** (`Name#1234`) for every entry, as everywhere else. No player ids or emails
      are in the response.
- [ ] **Refresh after submitting.** Responses are `Cache-Control: no-store`, so re-requesting the board
      right after `POST /games/{slug}/scores` shows the new score.
- [ ] **Formula versions:** boards rank only the game's current formula version (`formulaVersion` in
      the response). When a game's score formula changes, bump `formulaVersion` in Unity **and**
      `currentFormulaVersion` for that game in the server's `prisma/seed.ts` at the same time (then run
      `npm run db:seed`). Until both match, that game's boards won't show the new scores. Optionally,
      compare the response's `formulaVersion` with the build's version and show "Leaderboard
      unavailable for this version" if they differ.
- [ ] **C# classes:** an entry is `{ rank, player { displayName, discriminator }, score, puzzleDate,
      submittedAt }`, the same shape on both boards and in `me`. The board is `{ game, formulaVersion,
      puzzleDate (daily only), entries[], nextOffset (int or null), me (entry or null) }`.
