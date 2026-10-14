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
using SuperCricket.Simulation.Batting;
using SuperCricket.Simulation.Events;
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
            controllerInput.AimLengthAxis, controllerInput.RunHeld, controllerInput.PressedButtons,
            controllerInput.PacePressed);
    }

    private void UpdateMatch(GameTime gameTime, KeyboardState keyboard) =>
        UpdateMatch(gameTime, keyboard, MatchControllerActions.None);

    private void UpdateMatch(
        GameTime gameTime,
        KeyboardState keyboard,
        MatchControllerActions controllerActions,
        float controllerAimAxis = 0f,
        float controllerAimLengthAxis = 0f,
        bool controllerRunHeld = false,
        MatchControllerButtons controllerPressedButtons = MatchControllerButtons.None,
        bool controllerPacePressed = false)
    {
        var updateStart = Stopwatch.GetTimestamp();
        var elapsedSeconds = MathF.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
        UpdateGamePadFeedback(elapsedSeconds);
        _inputRouter.BeginKeyboardFrame(keyboard);
        bool ControllerPressed(MatchControllerActions action) => (controllerActions & action) != 0;
        var controllerFrame = new ControllerInputFrame(
            controllerActions, controllerAimAxis, controllerAimLengthAxis, controllerRunHeld,
            controllerPressedButtons, controllerPacePressed);
        bool KeyPressed(Keys key) => _inputRouter.WasKeyPressed(key);
        var pressedKeys = keyboard.GetPressedKeys();
        var keyboardBattingInput = default(KeyboardBattingInputFrame);
        var hasKeyboardBattingInput = false;
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
            else if (!HasHumanBowlingControls)
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
        if (IsCpuBattingControlled && !_developerMode && !_simulationPaused && !HasHumanBowlingControls &&
            (KeyPressed(Keys.C) || ControllerPressed(MatchControllerActions.CycleDelivery)))
            CycleNextDelivery();
        if (HasHumanBowlingControls && !_simulationPaused && !_match.IsInningsComplete)
        {
            RefreshBowlingOptions();
            var bowlingInput = _inputRouter.ReadBowlingControls(controllerFrame, _bowlingControlPage);
            var pageCount = Math.Max(1, (_bowlingOptions.Length + 3) / 4);
            if (!_humanBowlingStarted && !_bowlerReleased && bowlingInput.PreviousPagePressed)
                _bowlingControlPage = (_bowlingControlPage + pageCount - 1) % pageCount;
            if (!_humanBowlingStarted && !_bowlerReleased && bowlingInput.NextPagePressed)
                _bowlingControlPage = (_bowlingControlPage + 1) % pageCount;
            if (!_humanBowlingStarted && !_bowlerReleased && bowlingInput.DeliveryIndex is { } deliveryIndex)
                SelectHumanBowlingDelivery(deliveryIndex);
            if (bowlingInput.PacePressed && _humanBowlingStarted && !_bowlerReleased)
                ResolveHumanBowlingPaceMeter(_paceMeterElapsedSeconds);
        }
        if (KeyPressed(Keys.V) || ControllerPressed(MatchControllerActions.CycleCamera)) _camera.CyclePreset();
        if (_developerMode && KeyPressed(Keys.F1)) _showDebugOverlay = !_showDebugOverlay;
        if (_developerMode && !IsCpuBattingControlled && !_simulationPaused &&
            (KeyPressed(Keys.X) ||
             ControllerPressed(MatchControllerActions.CancelRun))) CancelRun();
        if (!_developerMode && !IsCpuBattingControlled && !_simulationPaused && _battedBall && KeyPressed(Keys.A))
            CancelRun();
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
        if (_simulationPaused && KeyPressed(Keys.I))
        {
            _gameSettings.BattingInputScheme = _gameSettings.BattingInputScheme == BattingInputScheme.Directional
                ? BattingInputScheme.ClassicChords
                : BattingInputScheme.Directional;
            SaveGameSettings();
        }
        if (_simulationPaused && KeyPressed(Keys.F))
        {
            _gameSettings.BattingFootSelection = _gameSettings.BattingFootSelection == BattingFootSelection.Automatic
                ? BattingFootSelection.Manual
                : BattingFootSelection.Automatic;
            SaveGameSettings();
        }
        if (!IsCpuBattingControlled && !_simulationPaused && !_deliveryComplete &&
            !_battedBall && !_shotResolved && !_isRunning)
        {
            if (_developerMode)
            {
                var aimStep = 0.12f;
                if (KeyPressed(Keys.J) || ControllerPressed(MatchControllerActions.AimOffSide))
                    AdjustHumanShotAim(-aimStep);
                if (KeyPressed(Keys.L) || ControllerPressed(MatchControllerActions.AimLegSide))
                    AdjustHumanShotAim(aimStep);
            }
            else
            {
                keyboardBattingInput = _inputRouter.ReadBattingInput(
                    _gameSettings.BattingInputScheme,
                    _gameSettings.BattingFootSelection);
                hasKeyboardBattingInput = true;
                if (_gameSettings.BattingInputScheme == BattingInputScheme.Directional)
                {
                    _humanShotAimOffset = keyboardBattingInput.HorizontalAim;
                    _humanForwardShotAim = keyboardBattingInput.ForwardAim;
                    _humanForwardShotAimSelected = keyboard.IsKeyDown(Keys.Up) || keyboard.IsKeyDown(Keys.Down);
                }
                else
                {
                    var aimStep = 0.5f;
                    if (KeyPressed(Keys.Left) || ControllerPressed(MatchControllerActions.AimOffSide))
                        AdjustHumanShotAim(-aimStep);
                    if (KeyPressed(Keys.Right) || ControllerPressed(MatchControllerActions.AimLegSide))
                        AdjustHumanShotAim(aimStep);
                    if (KeyPressed(Keys.Up)) SetHumanForwardShotAim(1f);
                    if (KeyPressed(Keys.Down)) SetHumanForwardShotAim(-1f);
                }

                var stickIntent = MatchInputRouter.NormalizeAimAxis(controllerAimAxis);
                var stickDepth = MatchInputRouter.NormalizeAimAxis(controllerAimLengthAxis);
                if (stickIntent != 0f)
                    _humanShotAimOffset = stickIntent;
                if (stickDepth != 0f)
                {
                    _humanForwardShotAim = stickDepth;
                    _humanForwardShotAimSelected = true;
                }
            }
        }
        else if (IsCpuBattingControlled && !_developerMode && !_simulationPaused &&
            !_match.IsInningsComplete && !_humanBowlingStarted)
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
            if (_developerMode)
            {
                if (KeyPressed(Keys.A) || ControllerPressed(MatchControllerActions.Defend))
                    StartShot(MathF.Abs(_humanShotAimOffset) < 0.08f ? "defence" : "drive");
                if (KeyPressed(Keys.S) || ControllerPressed(MatchControllerActions.Drive)) StartShot("drive");
                if (KeyPressed(Keys.D) || ControllerPressed(MatchControllerActions.Loft)) StartShot("loft");
            }
            else
            {
                var controllerIntent = ControllerPressed(MatchControllerActions.Attack)
                    ? StrokeIntent.Attack
                    : ControllerPressed(MatchControllerActions.Defend)
                        ? StrokeIntent.Defend
                        : ControllerPressed(MatchControllerActions.Loft)
                            ? StrokeIntent.Attack
                            : (StrokeIntent?)null;
                if (controllerIntent is { } intent)
                {
                    var loft = ControllerPressed(MatchControllerActions.Loft);
                    var request = new StrokeRequest(
                        StrokeDirectionModel.FromAim(_humanShotAimOffset, _humanForwardShotAim),
                        intent,
                        loft,
                        StrokeFoot.Auto);
                    var name = intent == StrokeIntent.Defend ? "defence" : loft ? "loft" : "drive";
                    StartShot(name, _humanShotAimOffset, _humanForwardShotAim,
                        controlLabelOverride: MatchHudPresenter.GetShotControlLabel(name, isGamePad: true),
                        strokeRequestOverride: request);
                }

                if (ControllerPressed(MatchControllerActions.Leave)) CommitLeave();
                if (ControllerPressed(MatchControllerActions.Advance)) AdvanceBatterDownPitch();

                if (hasKeyboardBattingInput)
                {
                    if (keyboardBattingInput.LeavePressed) CommitLeave();
                    if (keyboardBattingInput.AdvancePressed) AdvanceBatterDownPitch();
                    if (keyboardBattingInput.Stroke is { } stroke && !_deliveryComplete && !_shotResolved)
                    {
                        var name = stroke.Intent == StrokeIntent.Defend ? "defence" : stroke.Loft ? "loft" : "drive";
                        StartShot(name, keyboardBattingInput.HorizontalAim, keyboardBattingInput.ForwardAim,
                            controlLabelOverride: stroke.Intent == StrokeIntent.Defend ? "W: DEFEND" : "S: ATTACK",
                            strokeRequestOverride: stroke);
                    }
                }

                if (_battedBall || _isRunning)
                {
                    _battingInputRecorder.Reset();
                    if (KeyPressed(Keys.D)) StartRun();
                }
                else if (!_deliveryComplete && !_shotResolved &&
                    _gameSettings.BattingInputScheme == BattingInputScheme.ClassicChords)
                {
                    var pressedStyleKeys = pressedKeys.Where(KeyPressed).ToArray();
                    var command = _battingInputRecorder.Update(elapsedSeconds, pressedStyleKeys);
                    if (command is { } stroke)
                    {
                        var isBackFootStroke = stroke.Stroke is
                            RecordedBattingStroke.BackFootDrive or RecordedBattingStroke.BackFootLoft;
                        var forwardAim = isBackFootStroke && !_humanForwardShotAimSelected
                                ? -1f
                                : _humanForwardShotAim;
                        var request = new StrokeRequest(
                            StrokeDirectionModel.FromAim(_humanShotAimOffset, forwardAim),
                            stroke.ShotName == "defence" ? StrokeIntent.Defend : StrokeIntent.Attack,
                            stroke.ShotName == "loft",
                            isBackFootStroke ? StrokeFoot.Back : StrokeFoot.Front);
                        StartShot(stroke.ShotName, _humanShotAimOffset, forwardAim,
                            stroke.AnimationClip,
                            FormatKeyboardShotControlLabel(stroke.ControlLabel, _humanShotAimOffset, forwardAim),
                            strokeRequestOverride: request);
                    }
                }
                else if (_gameSettings.BattingInputScheme == BattingInputScheme.Directional)
                {
                    _battingInputRecorder.Reset();
                }
            }
            if ((_developerMode && KeyPressed(Keys.Enter)) || ControllerPressed(MatchControllerActions.Run)) StartRun();
        }
        if (!_simulationPaused && !IsCpuBattingControlled && _isRunning &&
            ((_developerMode && keyboard.IsKeyDown(Keys.Enter)) || controllerRunHeld))
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
                    _match.Events.Publish(new BallPitched(frame.TimeSeconds, frame.Position, frame.BounceCount));
                    _firstBouncePosition = ToXna(frame.Position);
                    _bounceSpotFeedbackRemainingSeconds = BounceSpotFeedbackDurationSeconds;
                    _liveFeedbackBannerRemainingSeconds = LiveFeedbackBannerDurationSeconds;
                }
                if (_chosenShot is not null && !_shotResolved &&
                    ContactPlaneCrossingModel.TryResolve(
                        previousFrame,
                        frame,
                        BatterContactPlaneZ,
                        out var contactFrame,
                        out var contactFraction))
                {
                    var batStartWorld = InterpolateBatWorld(GetBatPoseFraction(
                        physicsElapsedThisFrame, accumulatorBeforeFrame, elapsedSeconds, flightElapsed));
                    var batEndWorld = InterpolateBatWorld(GetBatPoseFraction(
                        physicsElapsedThisFrame + _ballFlight.FixedTimeStepSeconds,
                        accumulatorBeforeFrame, elapsedSeconds, flightElapsed));
                    var batAtContactWorld = InterpolateBatWorld(GetBatPoseFraction(
                        physicsElapsedThisFrame + _ballFlight.FixedTimeStepSeconds * contactFraction,
                        accumulatorBeforeFrame, elapsedSeconds, flightElapsed));
                    var sweetSpotLocal = (_batBladeMinimum + _batBladeMaximum) * 0.5f;
                    var sweetSpotPosition = Vector3.Transform(sweetSpotLocal, batAtContactWorld);
                    var contactPosition = ToXna(contactFrame.Position);
                    var sweetSpotOffset = Vector2.Zero;
                    if (_developerMode && TryBatContact(
                        previousFrame.Position,
                        frame.Position,
                        batStartWorld,
                        batEndWorld,
                        GetBatPoseDeltaSeconds(physicsElapsedThisFrame, accumulatorBeforeFrame, elapsedSeconds, flightElapsed),
                        out var sweptContact))
                    {
                        sweetSpotOffset = sweptContact.NormalizedSweetSpotOffset;
                    }

                    _contactMarkerPosition = contactPosition;
                    _sweetSpotMarkerPosition = sweetSpotPosition;
                    _contactTimeSeconds = contactFrame.TimeSeconds;
                    _contactSweetSpotOffset = sweetSpotOffset;
                    if (_strokeResolution is not { } strokeOutcome)
                        throw new InvalidOperationException("A committed batting input must have a resolved stroke outcome.");

                    if (strokeOutcome.Missed)
                    {
                        _contactQuality = 0f;
                        _contactFeedbackQuality = null;
                        _contactFeedbackIsMiss = true;
                        _contactFeedbackRemainingSeconds = ContactFeedbackDurationSeconds;
                        _liveFeedbackBannerRemainingSeconds = LiveFeedbackBannerDurationSeconds;
                        _shotResolved = true;
                        _shotOutcome = $"BEATEN: {_chosenShot.Name} was outside the {strokeOutcome.Zone} zone";
                        _match.Events.Publish(new BallBeatBat(_contactTimeSeconds.Value, strokeOutcome.Zone.ToString()));
                    }
                    else
                    {
                        var contactQuality = strokeOutcome.Zone switch
                        {
                            TimingZone.Ideal => 1f,
                            TimingZone.Early or TimingZone.Late => 0.7f,
                            TimingZone.Start or TimingZone.End => 0.45f,
                            _ => 0.3f
                        };
                        var outgoingVelocity = ToXna(strokeOutcome.OutgoingVelocity);
                        var resolvedContactPosition = Vector3.Distance(contactPosition, sweetSpotPosition) <= 0.35f
                            ? sweetSpotPosition
                            : contactPosition;
                        _contactQuality = contactQuality;
                        _contactFeedbackQuality = contactQuality;
                        _contactFeedbackIsMiss = false;
                        _contactFeedbackRemainingSeconds = ContactFeedbackDurationSeconds;
                        _liveFeedbackBannerRemainingSeconds = LiveFeedbackBannerDurationSeconds;
                        _ballFlight.ApplyBatContact(ToNumerics(resolvedContactPosition), strokeOutcome.OutgoingVelocity);
                        _match.Events.Publish(new BatContact(
                            _contactTimeSeconds.Value,
                            ToNumerics(resolvedContactPosition),
                            strokeOutcome.Zone.ToString()));
                        frame = _ballFlight.CurrentFrame;
                        fieldingPreviousFrame = frame;
                        fieldingElapsed *= 1f - contactFraction;
                        _shotResolved = true;
                        _battedBall = true;
                        _camera.SelectPreset("ball-follow");
                        _fieldingSide.Reset();
                        _shotOutcome = $"HIT: {_chosenShot.Name} {strokeOutcome.Zone}, {contactQuality:0.00} quality at {outgoingVelocity.Length():0.0} m/s";
                        if (IsCpuBattingControlled && _cpuBattingPlan is { } cpuPlan)
                        {
                            var fieldingPlayers = _match.FieldingPlayers;
                            var fieldingRatings = fieldingPlayers.Select(player => player.FieldingAttributes.Speed).ToArray();
                            _cpuRunsRemaining = CpuLiveRunningDecisionModel.Choose(
                                cpuPlan.AttemptRun,
                                _deliveryPreset,
                                ToNumerics(resolvedContactPosition),
                                strokeOutcome.OutgoingVelocity,
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
                                UpdateRun(_ballFlight.FixedTimeStepSeconds * (1f - contactFraction));
                        }
                    }
                }
                else if (_chosenShot is not null && !_shotResolved && frame.Position.Z < BatterContactPlaneZ - 0.45f && frame.Velocity.Z < 0f)
                {
                    _shotResolved = true;
                    _contactFeedbackQuality = null;
                    _contactFeedbackIsMiss = true;
                    _contactFeedbackRemainingSeconds = ContactFeedbackDurationSeconds;
                    _liveFeedbackBannerRemainingSeconds = LiveFeedbackBannerDurationSeconds;
                    _shotOutcome = $"MISS: {_chosenShot.Name} swung outside contact";
                    _match.Events.Publish(new BallBeatBat(frame.TimeSeconds, _strokeResolution?.Zone.ToString()));
                }

                if (!_deliveryComplete && !_battedBall && IncomingDeliveryModel.TryResolve(
                    CurrentDelivery, previousFrame.Position, frame.Position, _deliveryPreset,
                    out var wicketLinePosition, out var incomingResolution))
                {
                    ResolveIncomingDelivery(incomingResolution, wicketLinePosition);
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
