# A5 human acceptance review — pending

This worksheet captures the human checks left after the automated review on 16 October 2026. No human results have been entered; keep each gate open until someone runs it and records an outcome.

## Session details

- **Tester:**
- **Date/build:**
- **Input device(s):**
- **Fresh player for tutorial checks:**

## Section 6 playtest

| Gate | How to check | Result and notes |
| --- | --- | --- |
| 1. Practice Nets tutorial | Start as a new player, complete the batting lesson, and time it. Confirm the player can explain why a shot went up (for example, “I was late”). Record elapsed time and explanation. | Pending |
| 2. Repeatability | Automated check in Step 132 replays the same delivery category, input timing and seed ten times; all ten resolutions match. For the human session, repeat the same delivery and timing in-game and record the resulting zone and shot class each time. | Automated: 10/10 passed. Human: Pending |
| 3. Difficulty feel | Without practice, record Rookie attacking contacts on good-length deliveries and Legend Ideal contacts. Confirm Rookie contact is at least 60% and Legend Ideal is below 30%. Include numerator and denominator for both. | Pending |
| 4. Bowling feedback | Bowl a delivery, identify its type and where it landed, then cause a no-ball with a late meter press. Record whether the player understands both the delivery feedback and the no-ball reason. | Pending |
| 5. Decision replays | Trigger an out and a not-out LBW, a run-out, and a near miss. While bowling, check that the A/X appeal prompt appears only during its window and that letting it time out resumes play. Record whether each OUT / NOT OUT / SAFE signal, close-up, replay, stump-line or crease overlay explains the result, including the grounded-bat line on a near miss. | Pending |
| 6. Human-profile balance batch | Automated: 50 seeded T20s for each Human profile meet all unchanged RPO, wicket and dismissal-share bands. Rookie averages 305.6 combined runs to Legend's 255.4. Full 1.05 results and CSV links are in [Step 134](../steps/step-134-cpu-stroke-acceptance.md). | PASS (automated) |

## Art-track visual review

| Package | In-game checks | Result and notes |
| --- | --- | --- |
| WP18 clips | Inspect batting attack/defence/loft, footwork, fielding run/pickup/dive/catch/throw, keeper, and umpire actions. Check pose readability, bat contact timing, and return to stance. Previews: `artifacts/wp18/previews/`. | Pending |
| WP19 players | Compare agile, balanced, and power bodies; check body proportions, kit fit/clipping, team-color masks, and transitions among LOD0/LOD1/LOD2. Preview: `artifacts/wp19/character-variants.png`; runtime capture: `artifacts/wp19-runtime-behind-striker.png`. | Pending |
| WP20 props/stadium | Check in-game scale and placement of stumps/bails, bat, helmet, signage, stands, scoreboard, roller, umbrella, and crowd cards. Check readability and clipping. Preview: `artifacts/wp20/stadium-props.png`; field/stadium capture: `artifacts/review-field-overview.png`. | Pending |

## Findings

Record issues, severity, and the related WP here after the session. Leave this section empty until a person has completed the checks.
