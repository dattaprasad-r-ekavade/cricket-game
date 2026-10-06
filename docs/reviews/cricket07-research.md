# Cricket 07 gameplay research

Research for the Super Cricket camera and feedback pass, 7 October 2026. This combines the installed PC copy's readable support material with a producer diary and contemporary PC reviews. The local game reached its title screen but did not advance past “Click to continue” in this Windows 11 session, despite mouse and keyboard input; the in-match menus and camera switches therefore could not be observed directly.

## Installed copy

The installation at `C:\Program Files (x86)\EA SPORTS\EA SPORTS(TM) Cricket 07` includes `Support\en-uk\readme.txt`, `Support\config.xml`, `Data\controltypes.csv`, the game executable, and packed game data. The support folder has no readable gameplay manual or separate camera guide. `controltypes.csv` covers controller types rather than the full gameplay key map.

The readme says Esc opens the pause menu and points to **My Cricket > System Settings > Display Options** for display setup. It recommends practice nets to learn shots and timing, the run-assist function to judge running, and supported analog controllers for batting. It also warns that pressing multiple keys can cause loss of control on some keyboards. Its listed support target is Windows 2000/XP, so the stalled title prompt on this newer Windows system may be a compatibility issue; that explanation is an inference, not a confirmed diagnosis.

## Batting and onboarding

EA described Century Stick as an attempt to reduce the inputs needed for a basic batting action while retaining a deeper mode. In the default scheme, the right stick determines the stroke, direction, and timing in one gesture; how far it moves also controls shot power. The advanced scheme uses the left stick for crease movement and manual front/back-foot selection, while the right stick remains responsible for the shot. Triggers add loft and advancing down the pitch. Easier difficulty uses wider timing windows; higher difficulty and less confident/lower-order batters make timing and placement harder. [Producer diary #1](https://worthplaying.com/article/2006/11/2/news/37478-cricket-07-ps2pc-developer-diary-1/)

A PC review describes the same left-stick placement / right-stick shot-and-power split, notes the optional batting timing gauge, and explains that the sweet spot varies with batter skill. It also characterizes running as a single-button action. The installed readme's emphasis on nets and run assist supports teaching these skills in short practice loops before expecting a player to use them in a full match. [PC review at GameFAQs](https://gamefaqs.gamespot.com/pc/935545-cricket-07/reviews/110832)

## Cameras and in-game information

The producer said the default batting view was widened to keep the bowler's approach visible and build anticipation before release. Two reverse-perspective options face the approaching bowler. For the closer behind-batsman camera, EA made the batter and wicketkeeper semi-transparent because they otherwise hid the pitch-point marker. A separate Flip Cam changes sides for left- and right-handed batters. The fielding view was widened again so the player could read more of the outfield and make shot/run decisions. [Producer diary #3](https://worthplaying.com/article/2006/11/13/news/37744-cricket-07-ps2pc-developer-diary-3/)

The same diary describes the Picture-in-Picture panel as gameplay information: it helps judge whether a run is available, shows the bowler side-on during the approach, and shows the batter's shot in real time. A field radar lets the user adjust placement without pausing. The optional timing gauge is off by default and can be enabled in pause settings. This makes the important pattern a camera plus a small amount of contextual information, rather than relying on a single wide view or a dense instruction block.

## Bowling and running

Contemporary PC coverage describes bowling as choosing a delivery, watching a pace meter during the run-up, and stopping it within the permitted range; exceeding the cap produces a no-ball. The player also guides a pitch-point marker to set line and length. The same review criticizes the target cursor as too sensitive and easy to misplace. The useful design idea is visible pace/line/length intent with a clear outcome; the exact cursor sensitivity and timing pressure should not be copied. [PC review at GameFAQs](https://gamefaqs.gamespot.com/pc/935545-cricket-07/reviews/110832)

EA's diary and the PC review both describe the field radar and simple running prompt. They also show how cricket information can stay phase-specific: batting direction and shot, bowling selection and target, then whether a run is safe after contact. The review notes that Cricket 07 did not provide a return action after a mistimed running call, so our safer one-button repeat/turn-back behaviour should remain an intentional improvement.

## Application to Super Cricket

Step 51 applies the strongest visual lessons to the current build:

- The batting default is now a closer broadcast angle (28 m instead of 54 m), while behind-striker, bowler-end, and square-leg views are also closer. The role selects a batting or bowling camera at each new delivery, and ball-follow takes over after contact.
- A completed delivery keeps a result card visible until the next ball. It reports release speed, measured pitch line and length, shot/contact quality, runs or wicket, and—when the player bowled—the intended target and actual landing error.
- Release captures cover both batting and bowling card layouts. Human keyboard retest is still required to confirm the framing and feedback are legible during play; controller feedback remains untested.

The next Cricket 07-inspired gaps are the batting timing aid and guided nets, clearer pitch-point visualization from the close reverse camera, and a fielding/run-availability cue. Bowling should gain an understandable pace/accuracy decision without bringing back a twitchy target control. The result card presently reports contact quality, but does not claim an early/perfect/late timing label until timing has been calibrated against actual swings.
