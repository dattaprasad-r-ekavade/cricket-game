using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SuperCricket.Content;
using SuperCricket.Game.Animation;
using SuperCricket.Game.Rendering;
using SuperCricket.Simulation;
using NumericsVector3 = System.Numerics.Vector3;

namespace SuperCricket.Game;

public partial class Game1
{
    protected override void Update(GameTime gameTime)
    {
        var controllerInput = _inputRouter.ReadController(
            GamePad.GetState(PlayerIndex.One),
            IsCpuBattingControlled,
            _match.IsMatchComplete,
            _simulationPaused,
            _developerMode);
        UpdateMatch(gameTime, Keyboard.GetState(), controllerInput.Actions, controllerInput.AimAxis,
            controllerInput.AimLengthAxis, controllerInput.RunHeld);
    }

    private void UpdateMatch(GameTime gameTime, KeyboardState keyboard) =>
        UpdateMatch(gameTime, keyboard, MatchControllerActions.None);

    private void UpdateMatch(
        GameTime gameTime,
        KeyboardState keyboard,
        MatchControllerActions controllerActions,
        float controllerAimAxis = 0f,
        float controllerAimLengthAxis = 0f,
        bool controllerRunHeld = false)
    {
        var updateStart = Stopwatch.GetTimestamp();
        var elapsedSeconds = MathF.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
        _inputRouter.BeginKeyboardFrame(keyboard);
        bool ControllerPressed(MatchControllerActions action) => (controllerActions & action) != 0;
        bool KeyPressed(Keys key) => _inputRouter.WasKeyPressed(key);
        var pressedKeys = keyboard.GetPressedKeys();
        if (pressedKeys.Length > 0)
            _lastInputWasGamePad = false;
        if (controllerActions != MatchControllerActions.None || MathF.Abs(controllerAimAxis) > 0.2f ||
            MathF.Abs(controllerAimLengthAxis) > 0.2f)
            _lastInputWasGamePad = true;
        if (ControllerPressed(MatchControllerActions.Exit) ||
            keyboard.IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        if ((KeyPressed(Keys.R) ||
             ControllerPressed(MatchControllerActions.RestartMatch)) && !_simulationPaused)
        {
            StartNewMatch();
        }
        if ((KeyPressed(Keys.N) ||
             ControllerPressed(MatchControllerActions.NextBall)) &&
            !_simulationPaused && _deliveryComplete && !_match.IsMatchComplete)
        {
            if (_match.IsInningsComplete)
                StartNextInnings();
            else
                BeginDelivery();
        }
        if ((KeyPressed(Keys.O) ||
             ControllerPressed(MatchControllerActions.CycleOvers)) && _match.IsMatchComplete && !_simulationPaused)
        {
            var currentIndex = Array.IndexOf(OversChoices, _selectedOversPerInnings);
            _selectedOversPerInnings = OversChoices[(currentIndex + 1) % OversChoices.Length];
            _gameSettings.OversPerInnings = _selectedOversPerInnings;
            SaveGameSettings();
            StartNewMatch();
        }
        if ((KeyPressed(Keys.D) ||
             ControllerPressed(MatchControllerActions.CycleDifficulty)) && _match.IsMatchComplete && !_simulationPaused)
        {
            _cpuDifficulty = CpuDifficultyModel.Next(_cpuDifficulty);
            _gameSettings.Difficulty = _cpuDifficulty;
            SaveGameSettings();
            StartNewMatch();
        }
        if (_developerMode && !_simulationPaused && (KeyPressed(Keys.D1) ||
            ControllerPressed(MatchControllerActions.SelectStandardDelivery))) SelectNextDelivery(0);
        if (_developerMode && !_simulationPaused && ((KeyPressed(Keys.D2)) ||
            ControllerPressed(MatchControllerActions.SelectWideDelivery))) SelectNextDelivery(1);
        if (_developerMode && !_simulationPaused && ((KeyPressed(Keys.D3)) ||
            ControllerPressed(MatchControllerActions.SelectNoBallDelivery))) SelectNextDelivery(2);
        if (_developerMode && !_simulationPaused && ((KeyPressed(Keys.D4)) ||
            ControllerPressed(MatchControllerActions.SelectYorkerDelivery))) SelectNextDelivery(3);
        if (IsCpuBattingControlled && !_developerMode && !_simulationPaused &&
            (KeyPressed(Keys.C) || ControllerPressed(MatchControllerActions.CycleDelivery)))
            CycleNextDelivery();
        if (KeyPressed(Keys.V) || ControllerPressed(MatchControllerActions.CycleCamera)) _camera.CyclePreset();
        if (_developerMode && KeyPressed(Keys.F1)) _showDebugOverlay = !_showDebugOverlay;
        if (_developerMode && !IsCpuBattingControlled && !_simulationPaused &&
            (KeyPressed(Keys.X) ||
             ControllerPressed(MatchControllerActions.CancelRun))) CancelRun();
        if (KeyPressed(Keys.P) ||
            ControllerPressed(MatchControllerActions.Pause))
        {
            _simulationPaused = !_simulationPaused;
        }
        if (_simulationPaused &&
            (KeyPressed(Keys.H) ||
             ControllerPressed(MatchControllerActions.ToggleHighContrast)))
        {
            _gameSettings.HighContrast = !_gameSettings.HighContrast;
            SaveGameSettings();
        }
        if (_simulationPaused &&
            (KeyPressed(Keys.T) ||
             ControllerPressed(MatchControllerActions.ToggleLargeText)))
        {
            _gameSettings.LargeText = !_gameSettings.LargeText;
            SaveGameSettings();
        }
        if (_simulationPaused &&
            (KeyPressed(Keys.OemMinus) ||
             ControllerPressed(MatchControllerActions.DecreaseEffectsVolume)))
            AdjustEffectsVolume(-0.1f);
        if (_simulationPaused &&
            (KeyPressed(Keys.OemPlus) ||
             ControllerPressed(MatchControllerActions.IncreaseEffectsVolume)))
            AdjustEffectsVolume(0.1f);
        if (!IsCpuBattingControlled && !_simulationPaused && !_deliveryComplete &&
            !_battedBall && !_shotResolved && !_isRunning)
        {
            var aimStep = _developerMode ? 0.12f : 0.24f;
            if (KeyPressed(_developerMode ? Keys.J : Keys.Left) ||
                ControllerPressed(MatchControllerActions.AimOffSide))
                AdjustHumanShotAim(-aimStep);
            if (KeyPressed(_developerMode ? Keys.L : Keys.Right) ||
                ControllerPressed(MatchControllerActions.AimLegSide))
                AdjustHumanShotAim(aimStep);

            var stickIntent = MatchInputRouter.NormalizeAimAxis(controllerAimAxis);
            if (stickIntent != 0f)
                AdjustHumanShotAim(stickIntent * 1.25f * elapsedSeconds);
        }
        else if (IsCpuBattingControlled && !_developerMode && !_simulationPaused && !_match.IsInningsComplete)
        {
            if (KeyPressed(Keys.Left) || ControllerPressed(MatchControllerActions.AimOffSide))
                AdjustHumanBowlingAim(-0.4f, 0f);
            if (KeyPressed(Keys.Right) || ControllerPressed(MatchControllerActions.AimLegSide))
                AdjustHumanBowlingAim(0.4f, 0f);
            if (KeyPressed(Keys.Up) || ControllerPressed(MatchControllerActions.AimLong))
                AdjustHumanBowlingAim(0f, -0.4f);
            if (KeyPressed(Keys.Down) || ControllerPressed(MatchControllerActions.AimShort))
                AdjustHumanBowlingAim(0f, 0.4f);

            var lineIntent = MatchInputRouter.NormalizeAimAxis(controllerAimAxis);
            var lengthIntent = MatchInputRouter.NormalizeAimAxis(controllerAimLengthAxis);
            if (lineIntent != 0f || lengthIntent != 0f)
            {
                AdjustHumanBowlingAim(lineIntent * 5f * elapsedSeconds, -lengthIntent * 5f * elapsedSeconds);
            }
        }
        if (_developerMode && !IsCpuBattingControlled && !_simulationPaused && !_deliveryComplete && !_battedBall && !_isRunning)
        {
            if (KeyPressed(Keys.Q))
                StepBatterFootwork(1f);
            if (KeyPressed(Keys.E))
                StepBatterFootwork(-1f);
            if (ControllerPressed(MatchControllerActions.StepOffSide))
                StepBatterFootwork(1f);
            if (ControllerPressed(MatchControllerActions.StepLegSide))
                StepBatterFootwork(-1f);
        }
        if (_developerMode && !IsCpuBattingControlled && !_simulationPaused && KeyPressed(Keys.T))
        {
            _playerAnimator.PlayNext();
        }
        if (!IsCpuBattingControlled && !_simulationPaused)
        {
            var groundShotPressed = _developerMode
                ? KeyPressed(Keys.A) || ControllerPressed(MatchControllerActions.Defend)
                : KeyPressed(Keys.Space) || ControllerPressed(MatchControllerActions.Defend);
            var loftShotPressed = _developerMode
                ? KeyPressed(Keys.D) || ControllerPressed(MatchControllerActions.Loft)
                : KeyPressed(Keys.LeftShift) || KeyPressed(Keys.RightShift) || ControllerPressed(MatchControllerActions.Loft);
            if (groundShotPressed)
                StartShot(MathF.Abs(_humanShotAimOffset) < 0.08f ? "defence" : "drive");
            if (_developerMode && (KeyPressed(Keys.S) || ControllerPressed(MatchControllerActions.Drive)))
                StartShot("drive");
            if (loftShotPressed) StartShot("loft");
            if (KeyPressed(Keys.Enter) || ControllerPressed(MatchControllerActions.Run)) StartRun();
        }
        if (!_simulationPaused && !IsCpuBattingControlled && _isRunning &&
            (keyboard.IsKeyDown(Keys.Enter) || controllerRunHeld))
        {
            _runHoldElapsed += elapsedSeconds;
            if (_runHoldElapsed >= 0.45f)
                CancelRun();
        }
        else
        {
            _runHoldElapsed = 0f;
        }
        _inputRouter.EndKeyboardFrame();
        _camera.Update(gameTime, _developerMode, keyboard);
        var flightElapsed = _simulationPaused ? 0f : UpdateBowler(elapsedSeconds);
        if (!_simulationPaused)
            UpdateCpuBatting(flightElapsed);
        var runningElapsedDuringFielding = elapsedSeconds;
        if (_fielderSequencePhase == FielderSequencePhase.Throw)
            runningElapsedDuringFielding = MathF.Min(elapsedSeconds,
                MathF.Max(0f, _fielderThrowDurationSeconds - _fielderAnimators[_fielderThrowerIndex].CurrentTimeSeconds));
        var freezeBatterAnimations = _captureTarget is not null || _simulationPaused;
        if (!freezeBatterAnimations)
            _previousBatWorld = _currentBatWorld;
        _batterAnimations.Update(elapsedSeconds, frozen: freezeBatterAnimations);
        if (!freezeBatterAnimations)
        {
            UpdateBatterFootwork(elapsedSeconds);
            _currentBatWorld = GetBatWorldTransform();
            UpdateFielderAnimations(elapsedSeconds);
        }
        if (!_simulationPaused && _isRunning && _fielderThrowActive)
            UpdateRun(runningElapsedDuringFielding);
        if (!_simulationPaused && _fielderThrowActive)
            UpdateFielderThrow();

        if (!_simulationPaused && !_deliveryComplete && _bowlerReleased)
        {
            var accumulatorBeforeFrame = _simulationAccumulator;
            var physicsElapsedThisFrame = 0f;
            _simulationAccumulator += MathF.Min(flightElapsed, 0.25f);
            while (_simulationAccumulator >= _ballFlight.FixedTimeStepSeconds &&
                   BattedBallFieldingModel.CanAdvance(_battedBall, _deliveryComplete,
                       _fielderThrowActive, _ballFlight.CurrentFrame.Phase))
            {
                var previousFrame = _ballFlight.CurrentFrame;
                var fieldingPreviousFrame = previousFrame;
                var fieldingElapsed = _ballFlight.FixedTimeStepSeconds;
                if (_isRunning)
                    UpdateRun(_ballFlight.FixedTimeStepSeconds);

                var frame = _ballFlight.Step(enforceSimulationLimit: !_battedBall);
                if (_firstBouncePosition is null && previousFrame.BounceCount == 0 && frame.BounceCount > 0)
                {
                    _firstBouncePosition = ToXna(frame.Position);
                    _bounceSpotFeedbackRemainingSeconds = BounceSpotFeedbackDurationSeconds;
                    _liveFeedbackBannerRemainingSeconds = LiveFeedbackBannerDurationSeconds;
                }
                if (_chosenShot is not null && !_shotResolved &&
                    TryBatContact(
                        previousFrame.Position,
                        frame.Position,
                        InterpolateBatWorld(GetBatPoseFraction(physicsElapsedThisFrame, accumulatorBeforeFrame, elapsedSeconds, flightElapsed)),
                        InterpolateBatWorld(GetBatPoseFraction(physicsElapsedThisFrame + _ballFlight.FixedTimeStepSeconds, accumulatorBeforeFrame, elapsedSeconds, flightElapsed)),
                        GetBatPoseDeltaSeconds(physicsElapsedThisFrame, accumulatorBeforeFrame, elapsedSeconds, flightElapsed),
                        out var battingContact))
                {
                    _contactMarkerPosition = battingContact.Position;
                    _sweetSpotMarkerPosition = battingContact.SweetSpotPosition;
                    _contactTimeSeconds = previousFrame.TimeSeconds +
                        _ballFlight.FixedTimeStepSeconds * battingContact.HitFraction;
                    _contactSweetSpotOffset = battingContact.NormalizedSweetSpotOffset;
                    var impact = BattingImpactModel.Calculate(
                        ToNumerics(frame.Velocity),
                        ToNumerics(battingContact.BatPointVelocity),
                        new System.Numerics.Vector2(battingContact.NormalizedSweetSpotOffset.X, battingContact.NormalizedSweetSpotOffset.Y),
                        _chosenShot,
                        _match.StrikerPlayer);
                    _contactQuality = impact.ContactQuality;
                    _contactFeedbackQuality = impact.ContactQuality;
                    _contactFeedbackIsMiss = false;
                    _contactFeedbackRemainingSeconds = ContactFeedbackDurationSeconds;
                    _liveFeedbackBannerRemainingSeconds = LiveFeedbackBannerDurationSeconds;
                    _ballFlight.ApplyBatContact(ToNumerics(battingContact.Position), impact.OutgoingVelocity);
                    frame = _ballFlight.CurrentFrame;
                    fieldingPreviousFrame = frame;
                    fieldingElapsed *= 1f - battingContact.HitFraction;
                    _shotResolved = true;
                    _battedBall = true;
                    _camera.SelectPreset("ball-follow");
                    _fieldingSide.Reset();
                    _shotOutcome = $"HIT: {_chosenShot.Name}, {impact.ContactQuality:0.00} quality at {impact.OutgoingVelocity.Length():0.0} m/s";
                    PlayAudio(CricketAudioCue.BatContact);
                    if (IsCpuBattingControlled && _cpuBattingPlan is { } cpuPlan)
                    {
                        var fieldingPlayers = _match.FieldingPlayers;
                        var fieldingRatings = fieldingPlayers.Select(player => player.Fielding).ToArray();
                        _cpuRunsRemaining = CpuLiveRunningDecisionModel.Choose(
                            cpuPlan.AttemptRun,
                            _deliveryPreset,
                            ToNumerics(battingContact.Position),
                            impact.OutgoingVelocity,
                            _fieldingSide.Positions,
                            fieldingRatings,
                            _runDurationSeconds,
                            _fielderPickupDurationSeconds,
                            _fielderThrowDurationSeconds,
                            catchAnimationDurationSeconds: _fielderCatchDurationSeconds).PlannedRuns;
                        _runRequestedPending = _cpuRunsRemaining > 0;
                    }
                    if (_runRequestedPending)
                    {
                        StartRun();
                        if (_isRunning)
                            UpdateRun(_ballFlight.FixedTimeStepSeconds * (1f - battingContact.HitFraction));
                    }
                }
                else if (_chosenShot is not null && !_shotResolved && frame.Position.Z < NearBatterZ - 0.45f && frame.Velocity.Z < 0f)
                {
                    _shotResolved = true;
                    _contactFeedbackQuality = null;
                    _contactFeedbackIsMiss = true;
                    _contactFeedbackRemainingSeconds = ContactFeedbackDurationSeconds;
                    _liveFeedbackBannerRemainingSeconds = LiveFeedbackBannerDurationSeconds;
                    _shotOutcome = $"MISS: {_chosenShot.Name} swung outside contact";
                }

                if (!_deliveryComplete && !_battedBall && TryCrossPlane(previousFrame.Position, frame.Position, -PracticeGround.WicketOffset, out var wicketLinePosition))
                {
                    ResolveIncomingDelivery(wicketLinePosition);
                    frame = _ballFlight.CurrentFrame;
                }

                if (!_deliveryComplete && _battedBall &&
                    BattedBallFieldingModel.TryAdvance(_fieldingSide, fieldingPreviousFrame, frame,
                        fieldingElapsed, out var fieldingContact))
                {
                    ResolveFieldingContact(fieldingContact);
                    frame = _ballFlight.CurrentFrame;
                }

                if (!_deliveryComplete && _battedBall &&
                    BoundaryResolver.TryFindCrossing(
                        previousFrame,
                        frame,
                        _deliveryPreset.FieldBoundaryRadiusMeters,
                        _deliveryPreset.FieldSurfaceHeightMeters,
                        _deliveryPreset.BallRadiusMeters,
                        out var boundaryCrossing))
                {
                    ResolveBoundaryCrossing(boundaryCrossing);
                    frame = _ballFlight.CurrentFrame;
                }

                if (!_deliveryComplete && !_battedBall && !_fielderThrowActive && frame.Phase == BallMotionPhase.Settled)
                {
                    ResolveSettledBall(frame);
                }

                if (frame.Position != previousFrame.Position)
                    _trajectoryVertices.Add(new VertexPositionColor(
                        ToXna(frame.Position) + new Vector3(0f, 0.01f, 0f),
                        GetBallTrailColor(frame.Velocity.Length())));
                _simulationAccumulator -= _ballFlight.FixedTimeStepSeconds;
                physicsElapsedThisFrame += _ballFlight.FixedTimeStepSeconds;
            }
        }
        if (_deliveryComplete || _fielderThrowActive ||
            (_ballFlight.CurrentFrame.Phase == BallMotionPhase.Settled && !_battedBall))
        {
            _simulationAccumulator = 0f;
        }

        var cameraBallPosition = _fielderThrowActive
            ? ToXna(GetFielderThrowBallPosition())
            : ToXna(_ballFlight.CurrentFrame.Position);
        if (!_captureCameraPresetSpecified && IsHumanBowling && !_bowlerReleased && !_deliveryComplete &&
            _camera.PresetName == "Bowler end")
        {
            _camera.TrackTarget(GetBowlerWorld().Translation + new Vector3(0f, 0.9f, 0f), elapsedSeconds);
        }
        _camera.FollowBall(cameraBallPosition, elapsedSeconds);
        if (!_simulationPaused)
        {
            _bounceSpotFeedbackRemainingSeconds = MathF.Max(0f, _bounceSpotFeedbackRemainingSeconds - elapsedSeconds);
            _contactFeedbackRemainingSeconds = MathF.Max(0f, _contactFeedbackRemainingSeconds - elapsedSeconds);
            _liveFeedbackBannerRemainingSeconds = MathF.Max(0f, _liveFeedbackBannerRemainingSeconds - elapsedSeconds);
        }

        _frameTimeMilliseconds = gameTime.ElapsedGameTime.TotalMilliseconds;
        _fpsElapsed += gameTime.ElapsedGameTime.TotalSeconds;
        _frameCount++;
        if (_fpsElapsed >= 1.0)
        {
            _framesPerSecond = (int)Math.Round(_frameCount / _fpsElapsed);
            _frameCount = 0;
            _fpsElapsed = 0;
        }

        base.Update(gameTime);
        _updateMilliseconds = Stopwatch.GetElapsedTime(updateStart).TotalMilliseconds;
    }


}
