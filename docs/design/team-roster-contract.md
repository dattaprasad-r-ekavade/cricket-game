# Team roster asset contract

Team files are editable UTF-8 JSON documents at `assets/teams/<team>.json`. The game and local tools load the same versioned format through `TeamRosterAsset`.

Version 1 contains a team `name`, a short scoreboard name, and exactly 11 `players`. Each player has a stable team-local `id`, display `name`, unique `battingOrder` from 1 through 11, one of the roles `Batter`, `AllRounder`, `Bowler`, or `Wicketkeeper`, and integer `timing` and `power` ratings from 0 through 100. Exactly one player must be the wicketkeeper. Unknown JSON fields are rejected so a misspelled rating cannot silently fall back to a default.

The active striker and incoming batter are resolved from the batting order in the match scorecard. Timing adjusts contact quality for off-centre hits; power scales outgoing shot speed. A rating of 50 on both attributes preserves the current neutral batting response. Runtime player animation still uses the shared practice batter model for every roster entry; individual player art, bowling attributes, and fielding attributes remain future work.

Validate either team with:

```powershell
dotnet run --project src/SuperCricket.Tools -- validate-team assets/teams/coastal-xi.json
dotnet run --project src/SuperCricket.Tools -- validate-team assets/teams/highland-xi.json
```
