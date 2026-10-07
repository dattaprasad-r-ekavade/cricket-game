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
            _inputRouter.ResetKeyboardHistory();
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
        var expectedBounce = BowlingAimModel.FindFirstBounce(_deliveryPreset)
            ?? throw new InvalidOperationException("The review delivery has no predicted bounce.");
        Require(_predictedBouncePosition is { } predictedBounce &&
            Vector3.Distance(predictedBounce, ToXna(expectedBounce.Position)) < 0.001f,
            "the projected pitch-point marker did not use the current delivery's simulated bounce");
        Require(_camera.PresetName == "Behind striker" && MathF.Abs(_camera.Distance - 8f) < 0.001f &&
            MathF.Abs(_camera.Target.Z - NearBatterZ) < 0.001f,
            "the batting delivery did not start with the focused behind-striker view");
        Require(GetPrimaryControlHint().Contains("Left / Right: aim", StringComparison.Ordinal) &&
            GetPrimaryControlHint().Contains("Space: ground / defend", StringComparison.Ordinal) &&
            GetPrimaryControlHint().Contains("Shift: loft", StringComparison.Ordinal) &&
            GetPrimaryControlHint().Contains("V: camera", StringComparison.Ordinal) &&
            GetPrimaryControlHint().Contains("PgDn: zoom in / PgUp: out", StringComparison.Ordinal) &&
            !GetPrimaryControlHint().Contains("A/S/D", StringComparison.Ordinal),
            "the normal batting HUD did not show compact shot controls and the camera shortcut");
        _lastInputWasGamePad = true;
        Require(GetPrimaryControlHint().Contains("L3: camera", StringComparison.Ordinal),
            "the normal GamePad HUD did not show the camera shortcut");
        _lastInputWasGamePad = false;
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
        Require(_isRunning && _runRequestedPending &&
            _playerAnimator.CurrentClipName == "between-wickets" &&
            _batterAnimations.NonStriker.CurrentClipName == "between-wickets",
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
        Require(!_isRunning && !_runRequestedPending && _shotOutcome.Contains("unsafe", StringComparison.Ordinal) &&
            _batterAnimations.NonStriker.CurrentClipName == "practice-stance",
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
        Require(!_isRunning || _runners.IsReturning,
            $"holding the run button did not turn the batter back (held {_runHoldElapsed:0.00}s; delivery complete: {_deliveryComplete})");

        Reset();
        _battedBall = true;
        UpdateMatch(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.1)), new KeyboardState(),
            MatchControllerActions.Run, controllerRunHeld: true);
        Require(_isRunning, "the GamePad B button did not start a manual run");
        UpdateMatch(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.5)), new KeyboardState(),
            MatchControllerActions.None, controllerRunHeld: true);
        Require(!_isRunning || _runners.IsReturning, "holding GamePad B did not turn the batter back");
        Reset();
        _battedBall = true;
        StartRun();
        UpdateRun(_runDurationSeconds * 0.6f);
        StartRun();
        var beforeTurn = GetBatterWorlds();
        CancelRun();
        var afterTurn = GetBatterWorlds();
        Require(_isRunning && _runners.IsReturning && !_runRequestedPending &&
            beforeTurn.Striker.Translation == afterTurn.Striker.Translation &&
            beforeTurn.NonStriker.Translation == afterTurn.NonStriker.Translation,
            "turn-back teleported a runner or preserved a queued run");
        UpdateRun(_runDurationSeconds * 0.2f);
        Require(_isRunning && _runners.Progress < 0.6f, "turn-back did not move towards home continuously");
        UpdateRun(_runDurationSeconds);
        Require(!_isRunning && _match.CurrentDelivery?.CompletedRuns == 0 &&
            _playerAnimator.CurrentClipName == "practice-stance" &&
            _batterAnimations.NonStriker.CurrentClipName == "practice-stance",
            "returning home credited an uncompleted run");
        _developerMode = developerModeWasEnabled;
        Console.WriteLine("PASS: directional ground/loft controls, compact prompts, repeat runs, safe Rookie cancels, and keyboard/GamePad switching work outside developer mode.");

        var ballCamera = new CameraDirector();
        Require(ballCamera.SelectPreset("ball-follow") && ballCamera.FollowsBall && ballCamera.PresetName == "Ball follow" &&
            MathF.Abs(ballCamera.Distance - 8f) < 0.001f && MathF.Abs(ballCamera.FieldOfViewDegrees - 43f) < 0.001f,
            "ball-follow camera preset was not selectable at its readable tracking distance");
        ballCamera.FollowBall(new Vector3(2f, 1f, -4f), 0f);
        Require(ballCamera.Target == new Vector3(2f, 1f, -4f),
            "ball-follow camera did not focus the initial ball position");
        ballCamera.FollowBall(new Vector3(6f, 2f, -12f), 0.05f);
        Require(ballCamera.Target.X is > 2f and < 6f && ballCamera.Target.Z is < -4f and > -12f,
            "ball-follow camera did not ease toward the moving ball");
        ballCamera.SelectPreset("broadcast");
        Require(!ballCamera.FollowsBall && MathF.Abs(ballCamera.Distance - 20f) < 0.001f &&
            MathF.Abs(ballCamera.FieldOfViewDegrees - 44f) < 0.001f &&
            ballCamera.Target == new Vector3(0f, 0f, -1f),
            "switching camera presets did not stop ball tracking and restore the broadcast view");
        Require(ballCamera.SelectPreset("behind-striker") && MathF.Abs(ballCamera.Distance - 8f) < 0.001f &&
            MathF.Abs(ballCamera.FieldOfViewDegrees - 45f) < 0.001f && MathF.Abs(ballCamera.Target.Z - NearBatterZ) < 0.001f &&
            MathF.Abs(ballCamera.Target.Y - 0.9f) < 0.001f &&
            MathF.Abs(ballCamera.Yaw - (MathHelper.Pi + 0.22f)) < 0.001f &&
            MathF.Abs(ballCamera.Elevation - 0.30f) < 0.001f && ballCamera.SelectPreset("bowler-end") &&
            MathF.Abs(ballCamera.Distance - 8f) < 0.001f && MathF.Abs(ballCamera.FieldOfViewDegrees - 45f) < 0.001f &&
            MathF.Abs(ballCamera.Yaw - 0.22f) < 0.001f && MathF.Abs(ballCamera.Elevation - 0.30f) < 0.001f &&
            MathF.Abs(ballCamera.Target.Z - FarBatterZ) < 0.001f && MathF.Abs(ballCamera.Target.Y - 0.9f) < 0.001f,
            "batting and bowling cameras did not focus the active player at each crease");
        var bowlerCamera = new CameraDirector();
        bowlerCamera.SelectPreset("bowler-end");
        bowlerCamera.SetTarget(new Vector3(0f, 0.9f, 23f));
        bowlerCamera.TrackTarget(new Vector3(0.5f, 0.9f, 18f), 0.1f);
        Require(bowlerCamera.PresetName == "Bowler end" && MathF.Abs(bowlerCamera.Distance - 8f) < 0.001f &&
            bowlerCamera.Target.Z < 23f && bowlerCamera.Target.Z > 18f,
            "the bowler-end view did not follow the bowler through the run-up while preserving close zoom");
        var readableFeedback = MatchHudPresenter.CalculateFeedbackBannerBounds(1440, 900, 158, 760, 96);
        Require(readableFeedback.X == 340 && readableFeedback.Y == 170 && readableFeedback.Width == 760,
            "live feedback banner was not centered immediately below the scoreboard");
        var longHudFeedback = MatchHudPresenter.CalculateFeedbackBannerBounds(1440, 900, 158, 448, 116);
        Require(longHudFeedback.X == 496 && longHudFeedback.Y == 170 && longHudFeedback.Right == 944,
            "live bowling feedback did not remain centered and readable below the keyboard scoreboard");
        var narrowFeedback = MatchHudPresenter.CalculateFeedbackBannerBounds(1280, 720, 158, 720, 96);
        Require(narrowFeedback.X == 280 && narrowFeedback.Y == 170 && narrowFeedback.Bottom <= 696,
            "live feedback banner was not centered below the scoreboard at a narrower resolution");
        var keyboardZoomDistance = ballCamera.Distance;
        var cameraZoomFrame = new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.5));
        ballCamera.Update(cameraZoomFrame, allowDeveloperControls: false, new KeyboardState(Keys.PageDown));
        Require(ballCamera.Distance < keyboardZoomDistance,
            "Page Down did not zoom the gameplay camera closer");
        ballCamera.Update(cameraZoomFrame, allowDeveloperControls: false, new KeyboardState(Keys.PageUp));
        Require(MathF.Abs(ballCamera.Distance - keyboardZoomDistance) < 0.001f,
            "Page Up did not zoom the gameplay camera back out");
        var defaultBowlingDistance = ballCamera.Distance;
        ballCamera.ZoomBy(-2f);
        Require(MathF.Abs(ballCamera.Distance - (defaultBowlingDistance - 2f)) < 0.001f,
            "manual camera zoom did not move closer by the requested distance");
        ballCamera.ZoomBy(-100f);
        Require(MathF.Abs(ballCamera.Distance - 4.5f) < 0.001f,
            "manual camera zoom did not respect its minimum distance");
        Require(CameraDirector.GetRolePresetName(isHumanBowling: false) == "behind-striker" &&
            CameraDirector.GetRolePresetName(isHumanBowling: true) == "bowler-end",
            "delivery camera selection did not put each human role in its view of the pitch");
        var mapPlot = new Rectangle(100, 200, 120, 100);
        var mapCenter = MapPitchPosition(Vector3.Zero, mapPlot, PracticeGround.PitchLength);
        var bowlerMapPoint = MapPitchPosition(new Vector3(0f, 0f, PracticeGround.WicketOffset),
            mapPlot, PracticeGround.PitchLength);
        var batterMapPoint = MapPitchPosition(new Vector3(0f, 0f, -PracticeGround.WicketOffset),
            mapPlot, PracticeGround.PitchLength);
        var wideMapPoint = MapPitchPosition(new Vector3(20f, 0f, 0f), mapPlot, PracticeGround.PitchLength);
        Require(MathF.Abs(mapCenter.X - mapPlot.Center.X) < 0.001f &&
            bowlerMapPoint.Y < mapCenter.Y && batterMapPoint.Y > mapCenter.Y &&
            MathF.Abs(wideMapPoint.X - mapPlot.Right) < 0.001f,
            "pitch map coordinates did not preserve the bowler-to-batter axis or clamp a wide ball into view");
        var accurateBowlingFeedback = MatchHudPresenter.GetBowlingFeedbackSummary(
            new Vector3(0f, 0f, -4f), new Vector3(0.3f, 0f, -4.5f), NearBatterZ);
        var missedBowlingFeedback = MatchHudPresenter.GetBowlingFeedbackSummary(
            new Vector3(0f, 0f, -4f), new Vector3(1.3f, 0f, -4.5f), NearBatterZ);
        Require(accurateBowlingFeedback.Title == "ON TARGET" &&
            accurateBowlingFeedback.Detail.Contains("good length", StringComparison.Ordinal) &&
            missedBowlingFeedback.Title.EndsWith("M FROM AIM", StringComparison.Ordinal),
            "live bowling feedback did not explain aim accuracy and the landing length");
        var slowTrail = GetBallTrailColor(8f);
        var mediumTrail = GetBallTrailColor(22f);
        var fastTrail = GetBallTrailColor(36f);
        Require(slowTrail.B > slowTrail.R && mediumTrail.R > 200 && mediumTrail.G > 170 &&
            fastTrail.R > fastTrail.B && GetContactFeedbackLabel() == string.Empty,
            "ball trail speed colours or empty contact feedback state were not mapped clearly");
        _contactFeedbackQuality = 0.91f;
        Require(GetContactFeedbackLabel() == "MIDDLE", "high-quality batting feedback did not use a clear middle-contact label");
        _contactFeedbackQuality = 0.68f;
        Require(GetContactFeedbackLabel() == "EDGE CONTACT", "moderate batting feedback did not identify edge contact");
        _contactFeedbackQuality = null;
        _contactFeedbackIsMiss = true;
        Require(GetContactFeedbackLabel() == "NO CONTACT", "a missed shot did not have a clear immediate label");
        _contactFeedbackIsMiss = false;
        Console.WriteLine("PASS: role cameras focus the active player, follow the bowler then ball, and center live feedback below the HUD.");

        RunAdvancedGameplayReviewChecks(Tick, Reset, Require);
    }
}
