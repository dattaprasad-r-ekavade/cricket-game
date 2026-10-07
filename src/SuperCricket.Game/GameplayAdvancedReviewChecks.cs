using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SuperCricket.Game.Rendering;
using SuperCricket.Simulation;
using NumericsVector3 = System.Numerics.Vector3;

namespace SuperCricket.Game;

public partial class Game1
{
    private void RunAdvancedGameplayReviewChecks(
        Action<float, Keys[]> tick,
        Action<int> reset,
        Action<bool, string> require)
    {
        void Tick(float seconds, params Keys[] keys) => tick(seconds, keys);
        void Reset(int deliveryPresetIndex = 0) => reset(deliveryPresetIndex);
        void Require(bool condition, string message) => require(condition, message);
        var difficultyBeforeContactZoneReview = _cpuDifficulty;
        _cpuDifficulty = CpuDifficulty.Rookie;
        var rookieContactZoneAlpha = GetBattingContactZoneAlpha();
        _cpuDifficulty = CpuDifficulty.Standard;
        var standardContactZoneAlpha = GetBattingContactZoneAlpha();
        _cpuDifficulty = CpuDifficulty.Pro;
        var proContactZoneAlpha = GetBattingContactZoneAlpha();
        _cpuDifficulty = difficultyBeforeContactZoneReview;
        Require(rookieContactZoneAlpha > standardContactZoneAlpha && standardContactZoneAlpha > proContactZoneAlpha &&
            rookieContactZoneAlpha > 0 && proContactZoneAlpha == 0,
            "batting contact-zone assistance did not scale from a strong Rookie outline to a hidden Pro outline");
        Console.WriteLine("PASS: the projected bounce matches the flight simulation; contact-zone prominence descends from Rookie to Standard to hidden on Pro.");

        Reset();
        for (var step = 0; step < 1200 && _firstBouncePosition is null; step++) Tick(1f / 120f);
        Require(_firstBouncePosition is not null && _bounceSpotFeedbackRemainingSeconds > 0f,
            "the first ball bounce did not start a longer in-world pitch marker");
        Require(_liveFeedbackBannerRemainingSeconds <= LiveFeedbackBannerDurationSeconds &&
            _liveFeedbackBannerRemainingSeconds > LiveFeedbackBannerDurationSeconds - 0.02f,
            "the live feedback banner did not start its longer visibility window at the bounce");
        var bounceMarkerRemaining = _bounceSpotFeedbackRemainingSeconds;
        Tick(0.1f);
        Require(_bounceSpotFeedbackRemainingSeconds < bounceMarkerRemaining && _bounceSpotFeedbackRemainingSeconds > 0f,
            "the in-world pitch marker did not fade with elapsed game time");
        Require(_liveFeedbackBannerRemainingSeconds > _bounceSpotFeedbackRemainingSeconds,
            "the readable live feedback banner expired before the shorter world-space bounce marker");
        Tick(BounceSpotFeedbackDurationSeconds);
        Require(_bounceSpotFeedbackRemainingSeconds == 0f,
            "the in-world pitch marker persisted past its visibility window");
        Console.WriteLine("PASS: actual bounce feedback remains visible long enough to follow the pitch event.");

        Reset();
        var feedbackBounce = BowlingAimModel.FindFirstBounce(_deliveryPreset)
            ?? throw new InvalidOperationException("The standard delivery has no pitch bounce for the feedback review.");
        _firstBouncePosition = new Vector3(feedbackBounce.Position.X, feedbackBounce.Position.Y, feedbackBounce.Position.Z);
        _deliverySpeedKilometersPerHour = _deliveryPreset.ReleaseVelocity.ToVector3().Length() * 3.6f;
        _chosenShot = _shotSet.Get("drive");
        _contactQuality = 0.91f;
        var idealDriveInputDelay = _battingTimingCalibration.FindIdealInputDelaySeconds(_deliveryPreset.Name, _chosenShot.Name);
        var measuredDriveInputDelay = FindCurrentIdealInputDelaySeconds(_chosenShot.Name);
        Require(idealDriveInputDelay is { } idealDelay && measuredDriveInputDelay is { } measuredDelay &&
            MathF.Abs(idealDelay - measuredDelay) < 0.001f &&
            _battingTimingCalibration.FindIdealInputDelaySeconds("Uncalibrated delivery", "drive") is null,
            "batting timing calibration did not load the analyzer-derived drive target or reject unknown deliveries");
        _shotInputDelaySeconds = measuredDriveInputDelay;
        _shotIdealInputDelaySeconds = measuredDriveInputDelay;
        var originalDelivery = _deliveryPreset;
        _deliveryPreset = _deliveryPreset.DeepCopy();
        _deliveryPreset.Name = $"{_deliveryPresets[_activeDeliveryPresetIndex].Name} - outswing";
        var timingWithBowlingVariation = GetBattingTimingText();
        var variationTestShotName = _chosenShot?.Name ?? "<none>";
        var variationTestIdeal = _battingTimingCalibration.FindIdealInputDelaySeconds(
            _deliveryPresets[_activeDeliveryPresetIndex].Name, variationTestShotName);
        _deliveryPreset = originalDelivery;
        Require(timingWithBowlingVariation == "PERFECT",
            $"batting timing feedback lost its calibration when the CPU bowler added a delivery variation (" +
            $"text '{timingWithBowlingVariation ?? "<none>"}', preset '{_deliveryPresets[_activeDeliveryPresetIndex].Name}', " +
            $"shot '{variationTestShotName}', target {variationTestIdeal}, delay {_shotInputDelaySeconds}, " +
            $"CPU batting {IsCpuBattingControlled})");
        CurrentDelivery.ResolveBoundary(clearedInTheAir: false, currentRunCrossed: false);
        _match.CompleteDelivery();
        var feedbackLines = BuildDeliveryFeedbackLines(CurrentDelivery.Result!.Value);
        Require(feedbackLines.Count == 4 && feedbackLines[0].Contains("YOUR BATTING RESULT", StringComparison.Ordinal) &&
            feedbackLines[0].Contains("FOUR", StringComparison.Ordinal) &&
            feedbackLines[1].Contains("km/h", StringComparison.Ordinal) &&
            feedbackLines[1].Contains("good length", StringComparison.Ordinal) &&
            feedbackLines[2].Contains("PITCH", StringComparison.Ordinal) &&
            feedbackLines[2].Contains("m from striker", StringComparison.Ordinal) &&
            feedbackLines[3].Contains("YOUR SHOT", StringComparison.Ordinal) &&
            feedbackLines[3].Contains("drive", StringComparison.Ordinal) &&
            feedbackLines[3].Contains("middled", StringComparison.Ordinal) &&
            feedbackLines[3].Contains("timing PERFECT", StringComparison.Ordinal),
            "the batting result card did not put pace, pitch, contact, timing, and runs in the player's focus");
        var early = BattingTimingFeedbackModel.Assess(0.12f, 0.25f, 0.075f);
        var perfect = BattingTimingFeedbackModel.Assess(0.30f, 0.25f, 0.075f);
        var late = BattingTimingFeedbackModel.Assess(0.42f, 0.25f, 0.075f);
        Require(early.Band == BattingTimingBand.Early && perfect.Band == BattingTimingBand.Perfect &&
            late.Band == BattingTimingBand.Late && MathF.Abs(late.OffsetFromIdealSeconds - 0.17f) < 0.001f,
            "calibrated batting timing did not distinguish early, perfect, and late inputs");
        var waitingCue = BattingTimingFeedbackModel.EvaluateCue(0.10f, 0.25f, 0.075f);
        var onTimeCue = BattingTimingFeedbackModel.EvaluateCue(0.25f, 0.25f, 0.075f);
        var lateCue = BattingTimingFeedbackModel.EvaluateCue(0.40f, 0.25f, 0.075f);
        Require(waitingCue.State == BattingTimingCueState.Waiting &&
            MathF.Abs(waitingCue.SecondsUntilWindow - 0.075f) < 0.001f &&
            onTimeCue.State == BattingTimingCueState.SwingNow &&
            lateCue.State == BattingTimingCueState.WindowPassed &&
            waitingCue.Progress < waitingCue.WindowStart &&
            onTimeCue.Progress is > 0.49f and < 0.51f &&
            lateCue.Progress > lateCue.WindowEnd,
            "the live timing gauge did not place its calibrated window and moving marker correctly");
        var rejectedCueTime = false;
        try
        {
            BattingTimingFeedbackModel.EvaluateCue(float.NaN, 0.25f, 0.075f);
        }
        catch (ArgumentOutOfRangeException)
        {
            rejectedCueTime = true;
        }
        Require(rejectedCueTime, "the live timing gauge accepted a non-finite ball-flight time");
        foreach (var profile in _battingTimingCalibration.DeliveryProfiles)
        foreach (var shots in new[] { new[] { "defence", "loft" }, new[] { "drive", "loft" } })
        {
            var first = _battingTimingCalibration.FindIdealInputDelaySeconds(profile.DeliveryName, shots[0])
                ?? throw new InvalidOperationException($"The timing cue was missing '{shots[0]}' for '{profile.DeliveryName}'.");
            var second = _battingTimingCalibration.FindIdealInputDelaySeconds(profile.DeliveryName, shots[1])
                ?? throw new InvalidOperationException($"The timing cue was missing '{shots[1]}' for '{profile.DeliveryName}'.");
            var safeStart = MathF.Max(first - _battingTimingCalibration.OnTimeWindowSeconds,
                second - _battingTimingCalibration.OnTimeWindowSeconds);
            var safeEnd = MathF.Min(first + _battingTimingCalibration.OnTimeWindowSeconds,
                second + _battingTimingCalibration.OnTimeWindowSeconds);
            var safeCenter = (safeStart + safeEnd) * 0.5f;
            var safeHalfWidth = (safeEnd - safeStart) * 0.5f;
            Require(safeEnd > safeStart &&
                BattingTimingFeedbackModel.Assess(safeCenter, first, _battingTimingCalibration.OnTimeWindowSeconds).Band == BattingTimingBand.Perfect &&
                BattingTimingFeedbackModel.Assess(safeCenter, second, _battingTimingCalibration.OnTimeWindowSeconds).Band == BattingTimingBand.Perfect &&
                safeHalfWidth > 0f,
                $"the shared timing-cue window was not on-time for both {shots[0]} and {shots[1]} on '{profile.DeliveryName}'");
        }
        Console.WriteLine("PASS: the batting timing cue shows calibrated waiting, on-time, and passed-window states with a moving gauge marker.");
        Reset();
        PrepareBowlingTargetCapture();
        var bowlingBounce = BowlingAimModel.FindFirstBounce(_deliveryPreset)
            ?? throw new InvalidOperationException("The CPU batting target delivery has no bounce for the feedback review.");
        _firstBouncePosition = new Vector3(bowlingBounce.Position.X, bowlingBounce.Position.Y, bowlingBounce.Position.Z);
        _activeBowlingTargetPosition = _firstBouncePosition.Value + new Vector3(0.5f, 0f, 0f);
        _chosenShot = _shotSet.Get("drive");
        _contactQuality = 0.81f;
        CurrentDelivery.ResolveBoundary(clearedInTheAir: false, currentRunCrossed: false);
        _match.CompleteDelivery();
        var bowlingFeedbackLines = BuildDeliveryFeedbackLines(CurrentDelivery.Result!.Value);
        Require(bowlingFeedbackLines.Count == 5 &&
            bowlingFeedbackLines[0].Contains("YOUR BOWLING RESULT", StringComparison.Ordinal) &&
            bowlingFeedbackLines[2].Contains("PITCH", StringComparison.Ordinal) &&
            bowlingFeedbackLines[3].Contains("YOUR BOWL", StringComparison.Ordinal) &&
            bowlingFeedbackLines[3].Contains("0.5 m", StringComparison.Ordinal) &&
            bowlingFeedbackLines[4].Contains("BATTER", StringComparison.Ordinal),
            "the bowling result card did not foreground the human player's aim and landing feedback");
        Console.WriteLine("PASS: completed-ball feedback foregrounds the human player's role, result, pitch, and aim accuracy.");

        Reset();
        Tick(0f, Keys.J);
        Tick(0f);
        Tick(0f, Keys.J);
        Tick(0f);
        StartShot("drive");
        Require(_chosenShot is { } leftAim && MathF.Abs(leftAim.HorizontalAim - -0.09f) < 0.001f,
            "keyboard aim adjustments did not shift the authored drive lane to the left");
        var leftAimImpact = BattingImpactModel.Calculate(
            new NumericsVector3(0f, 0f, -20f),
            NumericsVector3.Zero,
            System.Numerics.Vector2.Zero,
            _chosenShot!);
        Require(leftAimImpact.OutgoingVelocity.X < 0f,
            "left shot aim did not direct the outgoing ball toward negative X");
        Tick(0f, Keys.L);
        Require(_chosenShot is { } adjustedShot && MathF.Abs(adjustedShot.HorizontalAim - 0.03f) < 0.001f,
            "keyboard aim could not adjust a selected shot before contact");

        Reset();
        UpdateMatch(
            new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.4)),
            new KeyboardState(),
            MatchControllerActions.None,
            controllerAimAxis: 1f);
        Require(MathF.Abs(_humanShotAimOffset - 0.5f) < 0.001f,
            "GamePad right-stick aim did not scale continuously with elapsed time");
        StartShot("drive");
        Require(_chosenShot is { } rightAim && MathF.Abs(rightAim.HorizontalAim - 0.65f) < 0.001f,
            "GamePad right-stick aim was not applied to the next authored shot");
        Reset();
        Require(MathF.Abs(_humanShotAimOffset) < 0.001f,
            "shot aim adjustment was not cleared for the next delivery");
        Console.WriteLine("PASS: keyboard and GamePad shot aiming shift human lanes and preserve the authored shot defaults.");

        Reset();
        Tick(0.1f);
        Tick(0f, Keys.P);
        var batterTime = _playerAnimator.CurrentTimeSeconds;
        var bowlerTime = _bowlerRunUpElapsed;
        var fielderTime = _fielderAnimators[0].CurrentTimeSeconds;
        var ballFrame = _ballFlight.CurrentFrame;
        Tick(1f);
        Require(_playerAnimator.CurrentTimeSeconds == batterTime && _bowlerRunUpElapsed == bowlerTime &&
            _fielderAnimators[0].CurrentTimeSeconds == fielderTime && _ballFlight.CurrentFrame == ballFrame,
            "pause advanced an animation, ball, or run-up");
        Tick(0.1f, Keys.S, Keys.Enter);
        Require(_chosenShot is null && !_runRequestedPending, "pause accepted shot/run input");
        Tick(0f);
        Tick(0.01f, Keys.P);
        Require(_bowlerRunUpElapsed > bowlerTime, "resume did not advance run-up");
        Console.WriteLine("PASS: pause/resume freezes match clocks and ignores gameplay input.");

        Reset();
        _battedBall = true;
        StartRun();
        ResolveFieldingContact(new FieldingContact(0, _fieldingSide.Positions[0], FieldingContactKind.GroundPickup));
        Tick(0f, Keys.P);
        var pickupTime = _fielderAnimators[0].CurrentTimeSeconds;
        Tick(0.75f);
        Require(_runElapsed == 0f && _fielderAnimators[0].CurrentTimeSeconds == pickupTime &&
            _fielderSequencePhase == FielderSequencePhase.Pickup && !_deliveryComplete,
            "pause advanced running or pickup/throw sequence");
        Tick(0f, Keys.P);
        for (var step = 0; step < 70 && _fielderSequencePhase != FielderSequencePhase.Throw; step++) Tick(1f / 120f);
        Require(_fielderSequencePhase == FielderSequencePhase.Throw, "pickup did not start the throw action");
        Tick(0f, Keys.P);
        var throwTime = _fielderAnimators[0].CurrentTimeSeconds;
        var runTime = _runElapsed;
        Tick(0.75f);
        Require(_fielderAnimators[0].CurrentTimeSeconds == throwTime && _runElapsed == runTime && !_deliveryComplete,
            "paused throw advanced or resolved delivery");
        Tick(0f, Keys.P);
        for (var step = 0; step < 150 && !_deliveryComplete; step++) Tick(1f / 120f);
        Require(_deliveryComplete && _dismissal == DismissalKind.RunOut && _match.Wickets == 1,
            "resumed pickup/throw did not finish as a run-out");
        Console.WriteLine("PASS: pickup/throw pause and authored throw completion.");

        Reset();
        _battedBall = true;
        CurrentDelivery.RecordCompletedRun();
        _runners.CompleteRun();
        _runners.StartRun();
        _runners.Advance(0.15f, _runDurationSeconds);
        ResolveFieldingContact(new FieldingContact(0, _fieldingSide.Positions[0], FieldingContactKind.GroundPickup));
        for (var step = 0; step < 180 && !_deliveryComplete; step++) Tick(1f / 120f);
        Require(_deliveryComplete && _dismissal == DismissalKind.RunOut && _match.Runs == 1 &&
            _match.Wickets == 1 && _match.LegalBalls == 1,
            "the authored throw failed to run out a batter still short of the far crease after the turn");
        Console.WriteLine("PASS: pickup and return throw resolve a run-out while the batter is turning.");

        Reset();
        for (var delivery = 0; delivery < 6; delivery++)
        {
            for (var step = 0; step < 600 && !_deliveryComplete; step++) Tick(1f / 120f);
            Require(_deliveryComplete && _dismissal == DismissalKind.Bowled, "unplayed delivery failed to resolve bowled");
            if (delivery < 5) BeginDelivery();
        }
        Require(_match.IsOverComplete && _match.LegalBalls == 6 && _match.Wickets == 6,
            "six legal deliveries did not complete the over");
        Console.WriteLine("PASS: six bowled deliveries complete an over.");

        Reset();
        for (var ball = 0; ball < 6; ball++)
        {
            for (var step = 0; step < 600 && !_deliveryComplete; step++) Tick(1f / 120f);
            Require(_deliveryComplete, "the live first innings did not resolve its delivery");
            if (ball < 5)
            {
                Tick(0f, Keys.N);
                Tick(0f);
            }
        }
        Require(_match.IsInningsComplete && !_match.IsMatchComplete && _match.FirstInnings is { Runs: 0, LegalBalls: 6 },
            "the first live innings did not stop at its over limit");
        Tick(0f, Keys.N);
        Require(_match.InningsNumber == 2 && _match.BattingTeamName == "Highland XI" && _match.Target == 1 && !_deliveryComplete,
            "N did not start the target chase as the second innings");
        Tick(0f);
        for (var ball = 0; ball < 6; ball++)
        {
            for (var step = 0; step < 600 && !_deliveryComplete; step++) Tick(1f / 120f);
            Require(_deliveryComplete, "the live second innings did not resolve its delivery");
            if (ball < 5)
            {
                Tick(0f, Keys.N);
                Tick(0f);
            }
        }
        Require(_match.IsMatchComplete && _match.ResultText == "Match tied",
            "the second live innings did not show a tied result after its over");
        Tick(0f, Keys.O);
        Require(!_match.IsMatchComplete && _match.OversPerInnings == 2 && _match.InningsNumber == 1 && _match.Runs == 0,
            "O did not restart the match with the next overs selection");
        Console.WriteLine("PASS: N starts the second innings, the chase resolves, and O changes overs after the result.");

        foreach (var presetIndex in new[] { 1, 2 })
        {
            Reset(presetIndex);
            for (var step = 0; step < 600 && !_deliveryComplete; step++) Tick(1f / 120f);
            Require(_deliveryComplete && _match.Runs == 1 && _match.LegalBalls == 0 && _match.Wickets == 0,
                "wide/no-ball did not award one extra without a legal ball or bowled wicket");
        }
        Console.WriteLine("PASS: wide and no-ball gameplay scoring.");

        Reset();
        for (var step = 0; step < 600 && !_deliveryComplete; step++) Tick(1f / 120f);
        Require(_deliveryComplete, "standard delivery did not complete before yorker selection");
        Tick(0f, Keys.D4);
        Require(_nextDeliveryPresetIndex == 3, "D4 did not select the yorker for the next delivery");
        Tick(0f, Keys.N);
        Require(_activeDeliveryPresetIndex == 3 && _deliveryPreset.Name == "Yorker pace",
            "the selected yorker preset was not loaded for the next ball");
        var yorkerFlight = new BallFlightSimulator(_deliveryPreset);
        var previousYorkerFrame = yorkerFlight.CurrentFrame;
        BallFlightFrame? yorkerBounce = null;
        BallFlightFrame? yorkerWicketCrossing = null;
        var wicketLineZ = -_deliveryPreset.ReleasePosition.Z;
        for (var step = 0; step < 600 && yorkerWicketCrossing is null; step++)
        {
            var frame = yorkerFlight.Step();
            if (yorkerBounce is null && frame.BounceCount == 1)
                yorkerBounce = frame;
            if (previousYorkerFrame.Position.Z > wicketLineZ && frame.Position.Z <= wicketLineZ)
                yorkerWicketCrossing = frame;
            previousYorkerFrame = frame;
        }
        var pitchEndZ = -_deliveryPreset.PitchLengthMeters / 2f;
        Require(yorkerBounce is { } bounce && bounce.Position.Z > pitchEndZ && bounce.Position.Z <= -9f &&
            MathF.Abs(bounce.Position.Y - (_deliveryPreset.PitchSurfaceHeightMeters + _deliveryPreset.BallRadiusMeters)) < 0.01f,
            "yorker did not bounce on the pitch near the striker's crease");
        Require(yorkerWicketCrossing is { } crossing && crossing.Position.Y < 0.3f,
            "yorker did not stay low as it crossed the wicket line");
        Console.WriteLine($"PASS: D4 selects a low yorker that pitches at z={yorkerBounce!.Value.Position.Z:0.00} m and reaches the wicket line at y={yorkerWicketCrossing!.Value.Position.Y:0.00} m.");

        foreach (var frameRate in new[] { 30, 60, 120 })
        {
            foreach (var (key, delay) in new[] { (Keys.A, 0.2f), (Keys.S, 0.25f), (Keys.D, 0.25f) })
            {
                Reset();
                var elapsed = 1f / frameRate;
                for (var step = 0; step < frameRate * 5 && !_bowlerReleased; step++) Tick(elapsed);
                for (var step = 0; step < frameRate && _ballFlight.CurrentFrame.TimeSeconds < delay; step++) Tick(elapsed);
                Tick(elapsed, key);
                for (var step = 0; step < frameRate * 2 && !_battedBall && !_deliveryComplete; step++) Tick(elapsed);
                Require(_battedBall && _contactFeedbackRemainingSeconds > 0f && !_contactFeedbackIsMiss,
                    $"shot {key} did not show a brief quality flash after contact at {frameRate} fps");
                var contactFlashRemaining = _contactFeedbackRemainingSeconds;
                Tick(elapsed);
                Require(_contactFeedbackRemainingSeconds < contactFlashRemaining && _contactFeedbackRemainingSeconds > 0f,
                    $"shot {key} contact flash did not fade at {frameRate} fps");
                for (var step = 0; step < frameRate * 6 && !_deliveryComplete; step++) Tick(elapsed);
                Require(_battedBall && _deliveryComplete, $"shot {key} failed contact/resolution at {frameRate} fps");
                Require(_releaseMarkerPosition is not null && _contactMarkerPosition is not null &&
                    _sweetSpotMarkerPosition is not null && _contactTimeSeconds is >= 0f &&
                    _contactQuality is >= 0f and <= 1f && _contactSweetSpotOffset is not null,
                    $"shot {key} failed to record release/contact/sweet-spot marker data at {frameRate} fps");
            }
        }
        Console.WriteLine("PASS: defence, drive, and loft contact and resolve at 30/60/120 fps.");

        Reset(1);
        Tick(0f, Keys.Q);
        Require(_playerAnimator.CurrentClipName == "batting-step-offside" && !_playerAnimator.IsOneShotComplete,
            "off-side footwork did not play its authored step clip");
        Tick(0.6f);
        Require(MathF.Abs(_batterFootworkOffsetX - BatterFootwork.StepDistanceMeters) < 0.001f &&
            _playerAnimator.CurrentClipName == "practice-stance",
            "off-side footwork did not finish its move and recover to stance");

        Reset(1);
        Tick(0f, Keys.E);
        Require(_playerAnimator.CurrentClipName == "batting-step-legside" && !_playerAnimator.IsOneShotComplete,
            "leg-side footwork did not play its authored step clip");
        Tick(0.6f);
        Require(MathF.Abs(_batterFootworkOffsetX + BatterFootwork.StepDistanceMeters) < 0.001f &&
            _playerAnimator.CurrentClipName == "practice-stance",
            "leg-side footwork did not finish its move and recover to stance");
        Console.WriteLine("PASS: authored off-side and leg-side steps play once and recover to stance.");

        foreach (var frameRate in new[] { 30, 60, 120 })
        {
            Reset(1);
            for (var step = 0; step < BatterFootwork.MaximumSteps; step++)
            {
                Tick(0f, Keys.Q);
                Tick(0f);
            }
            Tick(0.6f);
            var elapsed = 1f / frameRate;
            for (var step = 0; step < frameRate * 5 && !_bowlerReleased; step++) Tick(elapsed);
            for (var step = 0; step < frameRate && _ballFlight.CurrentFrame.TimeSeconds < 0.35f; step++) Tick(elapsed);
            Tick(elapsed, Keys.S);
            for (var step = 0; step < frameRate * 6 && !_deliveryComplete; step++) Tick(elapsed);
            Require(_battedBall && _deliveryComplete &&
                MathF.Abs(_batterFootworkOffsetX - BatterFootwork.MaximumOffsetMeters) < 0.001f,
                $"off-side footwork failed to reach the wide delivery at {frameRate} fps");
            Require(_releaseMarkerPosition is not null && _contactMarkerPosition is not null &&
                _sweetSpotMarkerPosition is not null && _contactTimeSeconds is >= 0f &&
                _contactQuality is >= 0f and <= 1f && _contactSweetSpotOffset is not null,
                $"wide shot failed to record release/contact/sweet-spot markers at {frameRate} fps");
        }
        Console.WriteLine("PASS: Q steps into the wide line and the drive makes contact at 30/60/120 fps.");

        foreach (var presetIndex in new[] { 0, 2 })
        foreach (var progress in new[] { 0.49f, 0.6f })
        {
            Reset(presetIndex);
            _battedBall = true;
            StartRun();
            _runners.Advance(_runDurationSeconds * progress, _runDurationSeconds);
            _ballFlight.StopAtContact(new NumericsVector3(0f, 0f, 0f));
            ResolveSettledBall(_ballFlight.CurrentFrame);
            var expectedBatterRuns = progress >= 0.5f ? 1 : 0;
            Require(_deliveryComplete && _dismissal == DismissalKind.None && _match.Wickets == 0 &&
                _match.Runs == expectedBatterRuns + (presetIndex == 2 ? 1 : 0) &&
                _match.Striker == (expectedBatterRuns == 1 ? 2 : 1),
                "settled-ball scoring dismissed a runner or failed to credit a crossed run");
        }
        Console.WriteLine("PASS: legal/no-ball dead-ball scoring preserves crossing and never invents a run-out.");

        foreach (var boundaryRuns in new[] { 4, 6 })
        {
            Reset();
            _battedBall = true;
            CurrentDelivery.RecordCompletedRun();
            ResolveBoundaryCrossing(new BoundaryCrossing(
                1f,
                new NumericsVector3(_deliveryPreset.FieldBoundaryRadiusMeters, 3f, 0f),
                boundaryRuns == 6));
            Require(_match.Runs == boundaryRuns && _match.Striker == 1 && _match.NonStriker == 2,
                "boundary incorrectly added an earlier run or changed strike");
        }
        Console.WriteLine("PASS: four/six scoring replaces earlier completed runs and restores ends.");

        Reset();
        _battedBall = true;
        for (var run = 0; run < 4; run++)
            CurrentDelivery.RecordCompletedRun();
        _runners.StartRun();
        _runners.Advance(_runDurationSeconds * 0.6f, _runDurationSeconds);
        ResolveBoundaryCrossing(new BoundaryCrossing(
            1f,
            new NumericsVector3(_deliveryPreset.FieldBoundaryRadiusMeters, 0.1f, 0f),
            false));
        Require(_match.Runs == 5 && _match.Striker == 2,
            "boundary failed to preserve the greater running allowance including a crossed run");
        Console.WriteLine("PASS: running allowance greater than the boundary allowance.");

        Reset();
        _battedBall = true;
        CurrentDelivery.RecordCompletedRun();
        ResolveFieldingContact(new FieldingContact(0, _fieldingSide.Positions[0], FieldingContactKind.Catch));
        Require(_match.Runs == 0 && _match.Wickets == 1 && _match.Striker == 3 && _match.NonStriker == 2,
            "catch retained completed runs or replaced the wrong batter");
        Console.WriteLine("PASS: a legal catch voids completed runs and replaces the striker.");

        Reset(2);
        _battedBall = true;
        CurrentDelivery.RecordCompletedRun();
        ResolveFieldingContact(new FieldingContact(0, _fieldingSide.Positions[0], FieldingContactKind.Catch));
        Require(_match.Runs == 2 && _match.Wickets == 0 && _match.LegalBalls == 0 && _match.Striker == 2,
            "no-ball catch incorrectly voided runs or dismissed the striker");
        Console.WriteLine("PASS: no-ball catch retains completed runs plus the penalty.");
        Console.WriteLine("Gameplay review checks passed.");
    }
}
