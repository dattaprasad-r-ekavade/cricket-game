# Team roster asset contract

Team files are editable UTF-8 JSON documents at `assets/teams/<team>.json`. The game and local tools load the same versioned format through `TeamRosterAsset`.

Version 1 contains a team `name`, a short scoreboard name, `primaryKitColorHex` and `accentKitColorHex` colors in `#RRGGBB` format, and exactly 11 `players`. Each player has a stable team-local `id`, display `name`, unique `battingOrder` from 1 through 11, one of the roles `Batter`, `AllRounder`, `Bowler`, or `Wicketkeeper`, and integer `timing`, `power`, `bowling`, and `fielding` ratings from 0 through 100. Exactly one player must be the wicketkeeper, and each team must have at least one bowler or all-rounder. Unknown JSON fields are rejected so a misspelled property cannot silently fall back to a default.

The active striker and incoming batter are resolved from the batting order in the match scorecard. Timing adjusts contact quality for off-centre hits; power scales outgoing shot speed. A rating of 50 on both attributes preserves the current neutral batting response. The strongest available bowler or all-rounder starts each innings' rotation; the match advances through the sorted bowling order one over at a time. The ten fielders are the rest of the team, with the wicketkeeper assigned to the wicketkeeper position. Fielding skill adjusts reaction time, movement speed, and pickup radius; a rating of 50 preserves the original fielding baseline. Bowling skill controls delivery pace, line control, and swing execution; the CPU selects a line/movement plan using striker power and chase pressure. Seeded headless checks cover repeatability and skill response. Balance through live play remains open. The renderer maps the starter player mesh's `Shirt`/`Forearm` groups to the primary color and `Jersey Collar`/`Sleeve Band`/crest groups to the accent color; batting and fielding teams swap those palettes between innings. Runtime player animation still uses the shared practice batter model for every roster entry.

Validate either team with:

```powershell
dotnet run --project src/SuperCricket.Tools -- validate-team assets/teams/coastal-xi.json
dotnet run --project src/SuperCricket.Tools -- validate-team assets/teams/highland-xi.json
```
