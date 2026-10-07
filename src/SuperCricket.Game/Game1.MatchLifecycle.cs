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
    private void StartNewMatch(int? seed = null)
    {
        _matchController.StartNewMatch(_selectedOversPerInnings, seed);
        _nextDeliveryPresetIndex = 0;
        _bowlingAimOffsetX = 0f;
        _bowlingAimOffsetZ = 0f;
        BeginDelivery();
    }

    private void StartNextInnings()
    {
        _matchController.StartNextInnings();
        _nextDeliveryPresetIndex = 0;
        _bowlingAimOffsetX = 0f;
        _bowlingAimOffsetZ = 0f;
        BeginDelivery();
    }

    private void PrepareBowlingTargetCapture()
    {
        for (var ball = 0; ball < _match.OversPerInnings * OverScoreboard.BallsPerOver; ball++)
        {
            CurrentDelivery.ResolveBoundary(clearedInTheAir: false, currentRunCrossed: false);
            _matchController.CompleteDelivery();
            if (!_match.IsInningsComplete)
                BeginDelivery();
        }

        StartNextInnings();
    }

    private void BeginDelivery()
    {
        if (_match.IsInningsComplete || _match.IsMatchComplete)
            return;

        _groundVertices = PracticeGround.CreateField();
        _activeDeliveryPresetIndex = _nextDeliveryPresetIndex;
        var selectedPreset = _deliveryPresets[_activeDeliveryPresetIndex];
        var situation = _matchController.CurrentBowlingSituation;
        var fieldPlacement = _matchController.ConfigureFieldingSide(_fieldingSide, _fieldPreset);
        _activeFieldingTactic = fieldPlacement.Tactic;
        _deliveryPreset = _matchController.PrepareDeliveryForCurrentMatch(
            selectedPreset,
            _activeDeliveryPresetIndex,
            cpuBattingControlled: IsCpuBattingControlled,
            bowlingAimOffsetX: _bowlingAimOffsetX,
            bowlingAimOffsetZ: _bowlingAimOffsetZ,
            developerMode: _developerMode,
            verificationRun: _verifyGameplay,
            difficulty: _cpuDifficulty);
        _matchController.BeginDelivery(_deliveryPreset.IsNoBall);
        _ballFlight = new BallFlightSimulator(_deliveryPreset);
        _fieldingSide.ConfigureBoundaryRadius(_deliveryPreset.FieldBoundaryRadiusMeters);
        _predictedBouncePosition = BowlingAimModel.FindFirstBounce(_deliveryPreset) is { } predictedBounce
            ? ToXna(predictedBounce.Position)
            : null;
        _simulationAccumulator = 0f;
        _simulationPaused = false;
        _chosenShot = null;
        _humanShotAimOffset = 0f;
        _battingStepRecoveryActive = false;
        _shotResolved = false;
        _battedBall = false;
        _batterFootworkOffsetX = 0f;
        _targetBatterFootworkOffsetX = 0f;
        _footworkTransitionActive = false;
        _runners.Reset();
        _runRequestedPending = false;
        _cpuRunsRemaining = 0;
        _cpuBattingPlan = null;
        _cpuShotStarted = false;
        _runHoldElapsed = 0f;
        _fielderThrowActive = false;
        _fielderThrowBallReleased = false;
        _fielderBallSecured = false;
        _fielderSequencePhase = FielderSequencePhase.None;
        Array.Clear(_fielderActionClips);
        Array.Clear(_fielderActionHoldAtEnd);
        _fieldingSide.Reset();
        _shotOutcome = "Choose a shot before the ball reaches the batter.";
        _releaseMarkerPosition = null;
        _contactMarkerPosition = null;
        _sweetSpotMarkerPosition = null;
        _contactTimeSeconds = null;
        _contactQuality = null;
        _shotInputDelaySeconds = null;
        _shotIdealInputDelaySeconds = null;
        _shotBattingTimingRequest = null;
        _contactSweetSpotOffset = null;
        _batterAnimations.ResetDelivery();
        _currentBatWorld = GetBatWorldTransform();
        _previousBatWorld = _currentBatWorld;
        _bowlerRunUpElapsed = 0f;
        _bowlerActionElapsed = 0f;
        _bowlerActionStarted = false;
        _bowlerActionFinished = false;
        _bowlerReleased = false;
        _bowlerAnimator.Play("bowling-run-up", 0.08f);
        _trajectoryVertices.Clear();
        UpdateBowlingTargetPreview();
        _deliverySpeedKilometersPerHour = _deliveryPreset.ReleaseVelocity.ToVector3().Length() * 3.6f;
        _firstBouncePosition = null;
        _bounceSpotFeedbackRemainingSeconds = 0f;
        _contactFeedbackRemainingSeconds = 0f;
        _liveFeedbackBannerRemainingSeconds = 0f;
        _contactFeedbackQuality = null;
        _contactFeedbackIsMiss = false;
        _activeBowlingTargetPosition = IsCpuBattingControlled && !_developerMode
            ? _bowlingTargetMarkerPosition
            : null;
        if (!_captureCameraPresetSpecified)
        {
            _camera.SelectRolePreset(IsCpuBattingControlled);
            if (IsHumanBowling)
                _camera.SetTarget(GetBowlerWorld().Translation + new Vector3(0f, 0.9f, 0f));
        }

        if (IsCpuBattingControlled)
        {
            _cpuBattingPlan = CpuLiveBattingPlanModel.Choose(
                _match.StrikerPlayer,
                _match.CurrentBowler,
                _match.FieldingPlayers,
                situation,
                _activeFieldingTactic,
                _deliveryPreset,
                _playerAsset,
                _bowlerAsset,
                _shotSet,
                _matchController.CreateBowlingDecisionSeed() ^ unchecked((int)0x6d2b79f5),
                _fieldingSide.Positions,
                _cpuDifficulty);
            _targetBatterFootworkOffsetX = _cpuBattingPlan.Value.FootworkOffsetMeters;
            _footworkTransitionActive = MathF.Abs(_targetBatterFootworkOffsetX) > 0.0001f;
            _runRequestedPending = _cpuBattingPlan.Value.AttemptRun;
        }
        PrepareBattingTiming();
    }

    private void UpdateCpuBatting(float flightElapsed)
    {
        if (!IsCpuBattingControlled || _cpuBattingPlan is not { } plan || _cpuShotStarted || _deliveryComplete || !_bowlerReleased)
            return;

        var upcomingSimulationTime = _ballFlight.CurrentFrame.TimeSeconds + _simulationAccumulator + flightElapsed;
        if (upcomingSimulationTime < plan.InputDelaySeconds)
            return;

        if (plan.Leave)
        {
            _shotOutcome = "CPU leaves the wide delivery.";
            _cpuShotStarted = true;
            return;
        }

        StartShot(plan.Shot switch
        {
            CpuShotChoice.Defence => "defence",
            CpuShotChoice.Drive => "drive",
            _ => "loft"
        }, plan.HorizontalAim);
        _cpuShotStarted = true;
    }

    private float UpdateBowler(float elapsedSeconds)
    {
        if (_bowlerActionFinished)
        {
            _bowlerAnimator.Update(elapsedSeconds);
            return _bowlerReleased ? elapsedSeconds : 0f;
        }

        var remaining = elapsedSeconds;
        var flightElapsed = 0f;

        if (!_bowlerActionStarted)
        {
            var runUpRemaining = MathF.Max(0f, _bowlerRunUpDurationSeconds - _bowlerRunUpElapsed);
            var runUpStep = MathF.Min(remaining, runUpRemaining);
            _bowlerRunUpElapsed += runUpStep;
            _bowlerAnimator.Update(runUpStep);
            remaining -= runUpStep;
            if (_bowlerRunUpElapsed >= _bowlerRunUpDurationSeconds)
            {
                _bowlerActionStarted = true;
                _bowlerActionElapsed = 0f;
                _bowlerAnimator.Play("overarm-delivery", 0.08f);
            }
        }

        if (remaining <= 0f)
            return 0f;

        var actionDuration = GetAnimationDuration(_bowlerAsset, "overarm-delivery");
        if (_bowlerActionElapsed < actionDuration)
        {
            var actionStep = MathF.Min(remaining, actionDuration - _bowlerActionElapsed);
            var previousActionTime = _bowlerActionElapsed;
            _bowlerAnimator.Update(actionStep);
            _bowlerActionElapsed += actionStep;
            remaining -= actionStep;

            if (!_bowlerReleased && _bowlerActionElapsed >= _bowlerReleaseTimeSeconds)
            {
                _bowlerReleased = true;
                if (!_captureCameraPresetSpecified && IsHumanBowling && _camera.PresetName == "Bowler end")
                    _camera.SelectPreset("ball-follow");
                _releaseMarkerPosition = ToXna(_ballFlight.CurrentFrame.Position);
                _trajectoryVertices.Add(new VertexPositionColor(
                    ToXna(_ballFlight.CurrentFrame.Position) + new Vector3(0f, 0.01f, 0f),
                    GetBallTrailColor(_ballFlight.CurrentFrame.Velocity.Length())));
            }

            if (_bowlerReleased)
                flightElapsed = previousActionTime >= _bowlerReleaseTimeSeconds
                    ? actionStep
                    : MathF.Max(0f, _bowlerActionElapsed - _bowlerReleaseTimeSeconds);
        }

        if (_bowlerActionElapsed >= actionDuration)
        {
            _bowlerActionFinished = true;
            _bowlerAnimator.Play("practice-stance", 0.16f);
            if (remaining > 0f)
            {
                _bowlerAnimator.Update(remaining);
                flightElapsed += remaining;
            }
        }

        return flightElapsed;
    }

    private void SetBowlerCaptureReleasePose()
    {
        _bowlerRunUpElapsed = _bowlerRunUpDurationSeconds;
        _bowlerActionStarted = true;
        _bowlerActionFinished = false;
        _bowlerActionElapsed = _bowlerReleaseTimeSeconds;
        _bowlerReleased = true;
        _releaseMarkerPosition = ToXna(_ballFlight.CurrentFrame.Position);
        _bowlerAnimator.Play("overarm-delivery", 0.001f);
        _bowlerAnimator.Update(_bowlerReleaseTimeSeconds);
        _trajectoryVertices.Clear();
        _trajectoryVertices.Add(new VertexPositionColor(
            ToXna(_ballFlight.CurrentFrame.Position) + new Vector3(0f, 0.01f, 0f),
            GetBallTrailColor(_ballFlight.CurrentFrame.Velocity.Length())));
    }

    private void SetFielderActionCapturePose(string clipName, float timeSeconds)
    {
        if (clipName is not ("fielder-catch" or "fielder-pickup" or "fielder-throw"))
            throw new ArgumentException("Fielder action capture must name fielder-catch, fielder-pickup, or fielder-throw.", nameof(clipName));

        var duration = GetAnimationDuration(_bowlerAsset, clipName);
        if (timeSeconds > duration)
            throw new ArgumentOutOfRangeException(nameof(timeSeconds), timeSeconds,
                $"Fielder action capture time must be between 0 and {duration:0.###} seconds.");

        var fielderIndex = 0;
        _fielderActionClips[fielderIndex] = clipName;
        _fielderActionHoldAtEnd[fielderIndex] = true;
        _fielderAnimators[fielderIndex].PlayOnce(clipName, 0.001f);
        _fielderAnimators[fielderIndex].Update(MathF.Max(0.001f, timeSeconds));
        var fielderWorld = ToXna(_fieldingSide.Positions[fielderIndex]);
        _camera.Focus(fielderWorld + new Vector3(0f, 0.85f, 0f), 4.5f, 0f, 0.22f, "Fielder action");
    }

    private void SetBatterFootworkCapturePose(string clipName, float timeSeconds)
    {
        if (clipName is not ("batting-step-offside" or "batting-step-legside"))
            throw new ArgumentException("Batter-footwork capture must name batting-step-offside or batting-step-legside.", nameof(clipName));

        var duration = GetAnimationDuration(_playerAsset, clipName);
        if (!float.IsFinite(timeSeconds) || timeSeconds < 0f || timeSeconds > duration)
            throw new ArgumentOutOfRangeException(nameof(timeSeconds), timeSeconds,
                $"Batter-footwork capture time must be between 0 and {duration:0.###} seconds.");

        _playerAnimator.PlayOnce(clipName, 0.001f);
        _playerAnimator.Update(timeSeconds);
        _batterFootworkOffsetX = clipName == "batting-step-offside"
            ? BatterFootwork.StepDistanceMeters
            : -BatterFootwork.StepDistanceMeters;
        _targetBatterFootworkOffsetX = _batterFootworkOffsetX;
        _currentBatWorld = GetBatWorldTransform();
        _previousBatWorld = _currentBatWorld;
        var striker = BatterWorld(NearBatterZ, true, _batterFootworkOffsetX);
        _camera.Focus(striker.Translation + new Vector3(0f, 0.9f, 0.5f), 4.5f, 0f, 0.2f, "Batter footwork");
    }

    private void SetBowlerDeliveryCapturePose(float timeSeconds)
    {
        var deliveryDuration = GetAnimationDuration(_bowlerAsset, "overarm-delivery");
        _bowlerRunUpElapsed = _bowlerRunUpDurationSeconds;
        _bowlerActionElapsed = timeSeconds;
        _bowlerActionStarted = true;
        _bowlerActionFinished = false;
        _bowlerReleased = false;
        _bowlerAnimator.Play("overarm-delivery", 0.001f);
        var previewTime = MathF.Min(MathF.Max(timeSeconds, 0.001f), MathF.Max(0f, deliveryDuration - 0.001f));
        _bowlerAnimator.Update(previewTime);
        _trajectoryVertices.Clear();
    }

    private void SetBowlerRunUpCapturePose(float timeSeconds)
    {
        _bowlerRunUpElapsed = timeSeconds;
        _bowlerActionElapsed = 0f;
        _bowlerActionStarted = false;
        _bowlerActionFinished = false;
        _bowlerReleased = false;
        _bowlerAnimator.Play("bowling-run-up", 0.001f);
        _bowlerAnimator.Update(timeSeconds);
        _trajectoryVertices.Clear();
    }

    private static float GetAnimationDuration(PlayerAsset asset, string clipName)
    {
        var animation = asset.Animations.Find(clip =>
            string.Equals(clip.Name, clipName, StringComparison.OrdinalIgnoreCase));
        return animation?.DurationSeconds
            ?? throw new InvalidDataException($"Player asset '{asset.Name}' is missing animation clip '{clipName}'.");
    }

    private static float GetAnimationEventTime(PlayerAsset asset, string clipName, string eventName)
    {
        var animation = asset.Animations.Find(clip =>
            string.Equals(clip.Name, clipName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"Player asset '{asset.Name}' is missing animation clip '{clipName}'.");
        var animationEvent = animation.Events.Find(candidate =>
            string.Equals(candidate.Name, eventName, StringComparison.OrdinalIgnoreCase));
        return animationEvent?.TimeSeconds
            ?? throw new InvalidDataException($"Player clip '{clipName}' is missing required event '{eventName}'.");
    }

    private static void RequireAnimation(PlayerAsset asset, string clipName) =>
        _ = GetAnimationDuration(asset, clipName);


}
