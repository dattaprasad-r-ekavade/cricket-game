using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SuperCricket.Game.Rendering;
using SuperCricket.Simulation;
using NumericsVector3 = System.Numerics.Vector3;

namespace SuperCricket.Game;

public partial class Game1
{
    // Runs the actual match update with scripted keyboard state after loading real content.
    private void RunGameplayReviewChecks()
    {
        void Tick(float seconds, params Keys[] keys) =>
            UpdateMatch(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)), new KeyboardState(keys));
        void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"Gameplay review failed: {message}");
        }
        void Reset(int deliveryPresetIndex = 0)
        {
            _previousKeyboard = default;
            _nextDeliveryPresetIndex = deliveryPresetIndex;
            _selectedOversPerInnings = 1;
            _match.Reset(_selectedOversPerInnings);
            BeginDelivery();
        }

        var developerModeWasEnabled = _developerMode;
        _developerMode = false;
        _lastInputWasGamePad = false;
        var standardDelivery = _deliveryPresets[0];
        var standardBounce = BowlingAimModel.FindFirstBounce(standardDelivery);
        var aimedDelivery = BowlingAimModel.AimForPitchTarget(standardDelivery, 0.8f, -0.6f);
        var aimedBounce = BowlingAimModel.FindFirstBounce(aimedDelivery);
        Require(standardBounce is { } baseBounce && aimedBounce is { } targetBounce &&
            MathF.Abs(targetBounce.Position.X - baseBounce.Position.X - 0.8f) <= 0.15f &&
            MathF.Abs(targetBounce.Position.Z - baseBounce.Position.Z + 0.6f) <= 0.15f &&
            MathF.Abs(standardDelivery.ReleaseVelocity.X) < 0.001f,
            "bowling aim did not move the real delivery bounce to its selected line and length");
        Console.WriteLine("PASS: the bowling pitch target changes the real ball-flight bounce without mutating its source preset.");
        Reset();
        Require(GetPrimaryControlHint().Contains("Left / Right: aim", StringComparison.Ordinal) &&
            GetPrimaryControlHint().Contains("Space: ground / defend", StringComparison.Ordinal) &&
            GetPrimaryControlHint().Contains("Shift: loft", StringComparison.Ordinal) &&
            !GetPrimaryControlHint().Contains("A/S/D", StringComparison.Ordinal),
            "the normal batting HUD did not show the compact directional two-shot controls");
        Tick(0f, Keys.Space);
        Require(_chosenShot?.Name == "defence", "a neutral ground shot did not use the defensive clip");

        Reset();
        Tick(0f, Keys.Left);
        Tick(0f);
        Tick(0f, Keys.Space);
        Require(_chosenShot?.Name == "drive" && _chosenShot.HorizontalAim < 0f,
            "left direction plus the ground button did not select a left-directed drive");
        Reset();
        Tick(0f, Keys.LeftShift);
        Require(_chosenShot?.Name == "loft", "the loft button did not select the lofted shot");
        Reset();
        Tick(0f, Keys.A);
        Require(_chosenShot is null, "legacy A/S/D keyboard controls remained active outside developer mode");

        Reset();
        UpdateMatch(
            new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.4)),
            new KeyboardState(),
            MatchControllerActions.Defend,
            controllerAimAxis: -1f);
        Require(_lastInputWasGamePad && _chosenShot?.Name == "drive" && _chosenShot.HorizontalAim < 0f &&
            GetPrimaryControlHint().Contains("Left stick: aim", StringComparison.Ordinal),
            "GamePad direction and shot buttons did not drive the compact, device-specific batting controls");

        Reset();
        _battedBall = true;
        StartRun();
        StartRun();
        Require(_isRunning && _runRequestedPending,
            "pressing the run button during a run did not queue another run");
        CompleteRun();
        Require(_isRunning && !_runRequestedPending && _match.CurrentDelivery?.CompletedRuns == 1,
            "a queued run did not start when the first run completed");

        var savedDifficulty = _cpuDifficulty;
        Reset();
        _battedBall = true;
        _cpuDifficulty = CpuDifficulty.Rookie;
        _fielderBallSecured = true;
        StartRun();
        StartRun();
        CompleteRun();
        Require(!_isRunning && !_runRequestedPending && _shotOutcome.Contains("unsafe", StringComparison.Ordinal),
            "Rookie did not cancel a queued follow-up run when the ball was unsafe");

        Reset();
        _battedBall = true;
        StartRun();
        StartRun();
        ResolveFieldingContact(new FieldingContact(
            0, _fieldingSide.Positions[0], FieldingContactKind.GroundPickup));
        Require(!_runRequestedPending,
            "Rookie did not cancel a queued second run when a fielder reached the ball");
        _cpuDifficulty = savedDifficulty;

        Reset();
        _battedBall = true;
        Tick(0.1f, Keys.Enter);
        Require(_isRunning, "the Enter button did not start a manual run");
        Tick(0.5f, Keys.Enter);
        Require(!_isRunning,
            $"holding the run button did not turn the batter back (held {_runHoldElapsed:0.00}s; delivery complete: {_deliveryComplete})");

        Reset();
        _battedBall = true;
        UpdateMatch(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.1)), new KeyboardState(),
            MatchControllerActions.Run, controllerRunHeld: true);
        Require(_isRunning, "the GamePad B button did not start a manual run");
        UpdateMatch(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.5)), new KeyboardState(),
            MatchControllerActions.None, controllerRunHeld: true);
        Require(!_isRunning, "holding GamePad B did not turn the batter back");
        _developerMode = developerModeWasEnabled;
        Console.WriteLine("PASS: directional ground/loft controls, compact prompts, repeat runs, safe Rookie cancels, and keyboard/GamePad switching work outside developer mode.");

        var ballCamera = new OrbitCamera();
        Require(ballCamera.SelectPreset("ball-follow") && ballCamera.FollowsBall && ballCamera.PresetName == "Ball follow" &&
            MathF.Abs(ballCamera.Distance - 9f) < 0.001f,
            "ball-follow camera preset was not selectable at its readable tracking distance");
        ballCamera.FollowBall(new Vector3(2f, 1f, -4f), 0f);
        Require(ballCamera.Target == new Vector3(2f, 1f, -4f),
            "ball-follow camera did not focus the initial ball position");
        ballCamera.FollowBall(new Vector3(6f, 2f, -12f), 0.05f);
        Require(ballCamera.Target.X is > 2f and < 6f && ballCamera.Target.Z is < -4f and > -12f,
            "ball-follow camera did not ease toward the moving ball");
        ballCamera.SelectPreset("broadcast");
        Require(!ballCamera.FollowsBall && ballCamera.Target == Vector3.Zero,
            "switching camera presets did not stop ball tracking and restore the broadcast view");
        Console.WriteLine("PASS: ball-follow camera tracks and eases across a delivery; broadcast remains a fixed preset.");

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
        _isRunning = true;
        _runElapsed = 0.15f;
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
        _isRunning = true;
        _runElapsed = _runDurationSeconds * 0.6f;
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
