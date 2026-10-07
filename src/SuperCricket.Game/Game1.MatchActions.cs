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
    private void SelectNextDelivery(int index)
    {
        if (_deliveryPresets.Length == 0)
            return;
        _nextDeliveryPresetIndex = Math.Clamp(index, 0, _deliveryPresets.Length - 1);
        UpdateBowlingTargetPreview();
    }

    private void CycleNextDelivery()
    {
        if (_deliveryPresets.Length == 0)
            return;
        SelectNextDelivery((_nextDeliveryPresetIndex + 1) % _deliveryPresets.Length);
    }

    private void AdjustHumanBowlingAim(float lineAdjustmentMeters, float lengthAdjustmentMeters)
    {
        if (!float.IsFinite(lineAdjustmentMeters) || !float.IsFinite(lengthAdjustmentMeters))
            return;
        var nextLine = Math.Clamp(
            _bowlingAimOffsetX + lineAdjustmentMeters,
            -BowlingAimModel.MaximumLineOffsetMeters,
            BowlingAimModel.MaximumLineOffsetMeters);
        var nextLength = Math.Clamp(
            _bowlingAimOffsetZ + lengthAdjustmentMeters,
            -BowlingAimModel.MaximumLengthOffsetMeters,
            BowlingAimModel.MaximumLengthOffsetMeters);
        if (MathF.Abs(nextLine - _bowlingAimOffsetX) < 0.0001f &&
            MathF.Abs(nextLength - _bowlingAimOffsetZ) < 0.0001f)
            return;
        _bowlingAimOffsetX = nextLine;
        _bowlingAimOffsetZ = nextLength;
        UpdateBowlingTargetPreview();
    }

    private void UpdateBowlingTargetPreview()
    {
        if (_developerMode || !IsCpuBattingControlled || _deliveryPresets.Length == 0)
        {
            _bowlingTargetMarkerPosition = null;
            return;
        }

        var previewPreset = BowlingAimModel.AimForPitchTarget(
            _deliveryPresets[_nextDeliveryPresetIndex],
            _bowlingAimOffsetX,
            _bowlingAimOffsetZ);
        if (BowlingAimModel.FindFirstBounce(previewPreset) is not { } bounce)
        {
            _bowlingTargetMarkerPosition = null;
            return;
        }
        _bowlingTargetMarkerPosition = ToXna(bounce.Position) + Vector3.UnitY * 0.045f;
    }

    private void StartShot(
        string name,
        float? horizontalAimOverride = null,
        float? forwardAimOverride = null,
        string? animationClipOverride = null,
        string? controlLabelOverride = null)
    {
        if (_simulationPaused || _shotResolved || _ballFlight.CurrentFrame.Phase == BallMotionPhase.Settled)
            return;

        var authoredShot = _shotSet.Get(name);
        var horizontalAim = horizontalAimOverride ?? Math.Clamp(
            IsCpuBattingControlled
                ? authoredShot.HorizontalAim + _humanShotAimOffset
                : _humanShotAimOffset,
            -1f,
            1f);
        var forwardAim = forwardAimOverride ?? (IsCpuBattingControlled
            ? authoredShot.ForwardAim
            : _humanForwardShotAim);
        if (!float.IsFinite(horizontalAim) || horizontalAim is < -1f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(horizontalAimOverride), "Shot direction must be between -1 and 1.");
        if (!float.IsFinite(forwardAim) || forwardAim is < -1f or > 1f ||
            (MathF.Abs(horizontalAim) < 0.001f && MathF.Abs(forwardAim) < 0.001f))
            throw new ArgumentOutOfRangeException(nameof(forwardAimOverride), "Shot direction must be a finite, non-zero vector between -1 and 1.");
        _chosenShot = CopyShotWithAim(authoredShot, horizontalAim, forwardAim, animationClipOverride);
        RecordShotTiming(name);
        _shotInputDelaySeconds = _bowlerRunUpElapsed + _bowlerActionElapsed -
            (_bowlerRunUpDurationSeconds + _bowlerReleaseTimeSeconds);
        _shotControlLabel = IsCpuBattingControlled
            ? null
            : controlLabelOverride ?? MatchHudPresenter.GetShotControlLabel(name, _lastInputWasGamePad);
        _battingStepRecoveryActive = false;
        _shotResolved = false;
        _shotOutcome = $"Swinging {_chosenShot.Name}; timing and placement decide contact.";
        _batterAnimations.PlayShot(_chosenShot.AnimationClip);
    }

    private void AdjustHumanShotAim(float adjustment)
    {
        if (!float.IsFinite(adjustment) || adjustment == 0f)
            return;

        _humanShotAimOffset = Math.Clamp(_humanShotAimOffset + adjustment, -2f, 2f);
        if (_chosenShot is not { } chosenShot || _shotResolved || IsCpuBattingControlled)
            return;

        var authoredShot = _shotSet.Get(chosenShot.Name);
        var horizontalAim = Math.Clamp(_humanShotAimOffset, -1f, 1f);
        _chosenShot = CopyShotWithAim(authoredShot, horizontalAim, chosenShot.ForwardAim, chosenShot.AnimationClip);
    }

    private static BattingShotData CopyShotWithAim(
        BattingShotData shot,
        float horizontalAim,
        float forwardAim,
        string? animationClipOverride = null) => new()
    {
        Name = shot.Name,
        AnimationClip = animationClipOverride ?? shot.AnimationClip,
        LaunchAngleDegrees = shot.LaunchAngleDegrees,
        HorizontalAim = horizontalAim,
        ForwardAim = forwardAim,
        SpeedTransfer = shot.SpeedTransfer
    };

    private void SetHumanForwardShotAim(float aim)
    {
        if (!float.IsFinite(aim) || aim == 0f)
            return;
        _humanForwardShotAim = Math.Clamp(aim, -1f, 1f);
        _humanForwardShotAimSelected = true;
        if (_chosenShot is not { } chosenShot || _shotResolved || IsCpuBattingControlled)
            return;

        var authoredShot = _shotSet.Get(chosenShot.Name);
        _chosenShot = CopyShotWithAim(
            authoredShot,
            Math.Clamp(_humanShotAimOffset, -1f, 1f),
            _humanForwardShotAim,
            chosenShot.AnimationClip);
    }

    private static string FormatKeyboardShotControlLabel(string recordedKeys, float horizontalAim, float forwardAim)
    {
        var direction = new List<string>(2);
        if (horizontalAim < -0.12f) direction.Add("LEFT");
        else if (horizontalAim > 0.12f) direction.Add("RIGHT");
        if (forwardAim < -0.12f) direction.Add("BEHIND");
        else if (forwardAim > 0.12f) direction.Add("DOWNFIELD");
        return direction.Count == 0 ? recordedKeys : $"{string.Join(" + ", direction)} | {recordedKeys}";
    }

    private string HumanShotAimStatus
    {
        get
        {
            var hasSelectedShot = _chosenShot is not null && !IsCpuBattingControlled;
            var aim = hasSelectedShot ? _chosenShot!.HorizontalAim : _humanShotAimOffset;
            var markerAim = hasSelectedShot ? aim : aim * 0.5f;
            var marker = Math.Clamp((int)MathF.Round((markerAim + 1f) * 5f), 0, 10);
            var track = $"[{new string('-', marker)}#{new string('-', 10 - marker)}]";
            var position = MathF.Abs(aim) < 0.005f ? "center" : aim > 0f ? $"+{aim:0.00}" : $"{aim:0.00}";
            return $"{(hasSelectedShot ? "Shot lane" : "Aim shift")}: {track} {position}";
        }
    }

    private void StepBatterFootwork(float direction)
    {
        _targetBatterFootworkOffsetX = BatterFootwork.AddStep(_targetBatterFootworkOffsetX, direction);
        PrepareBattingTiming();
        _footworkTransitionActive = MathF.Abs(_targetBatterFootworkOffsetX - _batterFootworkOffsetX) > 0.0001f;
        if (_footworkTransitionActive && _chosenShot is null)
        {
            _playerAnimator.PlayOnce(
                direction > 0f ? "batting-step-offside" : "batting-step-legside",
                0.08f);
            _battingStepRecoveryActive = true;
        }
    }

    private void UpdateBatterFootwork(float deltaTime)
    {
        _batterFootworkOffsetX = BatterFootwork.Advance(
            _batterFootworkOffsetX,
            _targetBatterFootworkOffsetX,
            deltaTime);
        if (_footworkTransitionActive)
        {
            if (MathF.Abs(_targetBatterFootworkOffsetX - _batterFootworkOffsetX) > 0.0001f)
                return;
            _footworkTransitionActive = false;
        }

        if (!_battingStepRecoveryActive)
            return;
        if (_chosenShot is not null || _isRunning ||
            _playerAnimator.CurrentClipName is not ("batting-step-offside" or "batting-step-legside"))
        {
            _battingStepRecoveryActive = false;
            return;
        }
        if (!_playerAnimator.IsOneShotComplete)
            return;

        _battingStepRecoveryActive = false;
        _playerAnimator.Play("practice-stance", 0.12f);
    }

    private void StartRun()
    {
        if (_simulationPaused || _deliveryComplete)
            return;
        if (_isRunning)
        {
            if (!IsCpuBattingControlled && !_runners.IsReturning)
            {
                _runRequestedPending = true;
                _shotOutcome = "Another run requested.";
            }
            return;
        }
        if (!_battedBall)
        {
            _runRequestedPending = _chosenShot is not null && !_shotResolved;
            return;
        }

        _runRequestedPending = false;
        if (IsCpuBattingControlled)
        {
            if (_cpuRunsRemaining <= 0)
                return;
            _cpuRunsRemaining--;
        }
        _targetBatterFootworkOffsetX = 0f;
        _footworkTransitionActive = MathF.Abs(_batterFootworkOffsetX) > 0.0001f;
        _runners.StartRun();
        _runHoldElapsed = 0f;
        _batterAnimations.SetRunning(true);
    }

    private void CancelRun()
    {
        if (_simulationPaused) return;
        _runRequestedPending = false;
        _cpuRunsRemaining = 0;
        if (!_isRunning)
            return;
        _runners.TurnBack();
        _runHoldElapsed = 0f;
        _shotOutcome = "Turning back; reach the crease before the return throw.";
    }

    private void UpdateRun(float deltaTime)
    {
        do
        {
            var result = _runners.Advance(deltaTime, _runDurationSeconds, out var unusedSeconds);
            if (result == RunMovementResult.CompletedRun)
                CompleteRun(updateRunnerState: false);
            else if (result == RunMovementResult.ReturnedHome)
            {
                _runHoldElapsed = 0f;
                _shotOutcome = "Returned safely; no additional run.";
                _batterAnimations.SetRunning(false);
            }
            else
                break;
            deltaTime = unusedSeconds;
        } while (_isRunning && deltaTime > 0f);
    }

    private void UpdateFielderAnimations(float deltaTime)
    {
        var chaserIndex = _battedBall && !_deliveryComplete && !_fielderThrowActive
            ? _fieldingSide.ActiveChaserIndex
            : -1;
        for (var fielderIndex = 0; fielderIndex < _fielderAnimators.Length; fielderIndex++)
        {
            var animation = _fielderAnimators[fielderIndex];
            var actionClip = _fielderActionClips[fielderIndex];
            if (actionClip is not null)
            {
                if (!string.Equals(animation.CurrentClipName, actionClip, StringComparison.OrdinalIgnoreCase))
                    animation.PlayOnce(actionClip, 0.12f);
                if (!animation.IsOneShotComplete)
                    animation.Update(deltaTime);

                var ballSecuredTime = actionClip switch
                {
                    "fielder-catch" => _fielderCatchBallSecuredTimeSeconds,
                    "fielder-pickup" => _fielderPickupBallSecuredTimeSeconds,
                    _ => float.PositiveInfinity
                };
                if (!_fielderBallSecured && animation.CurrentTimeSeconds >= ballSecuredTime)
                {
                    _fielderBallSecured = true;
                    _ballFlight.StopAtContact(_fielderHeldBallPosition);
                }

                if (_fielderSequencePhase == FielderSequencePhase.Pickup &&
                    fielderIndex == _fielderThrowerIndex && animation.IsOneShotComplete)
                {
                    _fielderSequencePhase = FielderSequencePhase.Throw;
                    StartFielderAction(fielderIndex, "fielder-throw", holdAtEnd: true);
                }
                else if (animation.IsOneShotComplete && !_fielderActionHoldAtEnd[fielderIndex])
                {
                    _fielderActionClips[fielderIndex] = null;
                }
                continue;
            }

            var clipName = fielderIndex == chaserIndex ? "between-wickets" : "practice-stance";
            if (!string.Equals(animation.CurrentClipName, clipName, StringComparison.OrdinalIgnoreCase))
                animation.Play(clipName, 0.16f);
            animation.Update(deltaTime);
        }
    }

    private void UpdateFielderThrow()
    {
        if (_fielderSequencePhase != FielderSequencePhase.Throw)
            return;

        var animation = _fielderAnimators[_fielderThrowerIndex];
        _fielderThrowBallReleased = animation.CurrentTimeSeconds >= _fielderThrowReleaseTimeSeconds;
        if (!animation.IsOneShotComplete)
            return;

        var wicketBroken = WicketContactModel.IsBroken(_fielderThrowTarget, -PracticeGround.WicketOffset,
            _deliveryPreset.BallRadiusMeters, _fielderBallSecured, _fielderThrowBallReleased);
        _groundVertices = PracticeGround.CreateField(nearWicketBroken: wicketBroken);
        _ballFlight.StopAtContact(_fielderThrowTarget);
        _fielderThrowActive = false;
        _fielderThrowBallReleased = false;
        _fielderSequencePhase = FielderSequencePhase.None;
        _fielderActionClips[_fielderThrowerIndex] = null;
        _fielderActionHoldAtEnd[_fielderThrowerIndex] = false;
        if (_runners.TryResolveRunOut(WicketEnd.Near, wicketBroken, out var runOut))
        {
            CurrentDelivery.ResolveRunOut(runOut.DismissedEnd, runOut.SwapEnds);
            _shotOutcome = $"OUT: fielder {_fielderThrowerIndex + 1} threw to the wicketkeeper";
        }
        else
        {
            _shotOutcome = $"Wicketkeeper received fielder {_fielderThrowerIndex + 1}'s throw";
        }
        FinishDelivery();
    }

    private void CompleteRun(bool recordScoring = true, bool allowNextRun = true, bool updateRunnerState = true)
    {
        if (recordScoring)
            CurrentDelivery.RecordCompletedRun();
        if (updateRunnerState)
            _runners.CompleteRun();
        _runHoldElapsed = 0f;
        _shotOutcome = $"RUN completed: {_batterRuns} batter run(s)";
        if (allowNextRun && IsCpuBattingControlled && _cpuRunsRemaining > 0 && !_deliveryComplete)
            StartRun();
        else if (allowNextRun && !IsCpuBattingControlled && _runRequestedPending && !_deliveryComplete)
        {
            _runRequestedPending = false;
            if (_cpuDifficulty != CpuDifficulty.Rookie || (!_fielderBallSecured && !_fielderThrowActive))
                StartRun();
            else
                _shotOutcome = "RUN completed; the next run was unsafe and cancelled.";
        }
        _batterAnimations.SetRunning(_isRunning);
    }

    private void ResolveIncomingDelivery(IncomingDeliveryResolution resolution, NumericsVector3 wicketLinePosition)
    {
        switch (resolution)
        {
            case IncomingDeliveryResolution.NoBall:
                _shotOutcome = "NO-BALL: one penalty run, delivery not counted";
                PlayAudio(CricketAudioCue.Extra);
                FinishDelivery();
                break;
            case IncomingDeliveryResolution.Wide:
                _shotOutcome = "WIDE: one extra run, delivery not counted";
                PlayAudio(CricketAudioCue.Extra);
                FinishDelivery();
                break;
            case IncomingDeliveryResolution.Bowled:
                _shotOutcome = "OUT: bowled";
                FinishDelivery();
                break;
            default:
                if (_chosenShot is null)
                    _shotOutcome = "DOT: missed the stumps";
                FinishDelivery();
                break;
        }

        _ballFlight.StopAtContact(wicketLinePosition);
    }

    private void ResolveFieldingContact(FieldingContact contact)
    {
        var fielderPosition = _fieldingSide.Positions[contact.FielderIndex];
        _fielderHeldBallPosition = new NumericsVector3(
            fielderPosition.X,
            fielderPosition.Y + 1.02f,
            fielderPosition.Z);
        _fielderBallSecured = false;
        if (!IsCpuBattingControlled && _cpuDifficulty == CpuDifficulty.Rookie && _runRequestedPending)
            _runRequestedPending = false;

        var collectionAction = BattedBallFieldingModel.ResolveCollection(CurrentDelivery, contact.Kind, _isRunning);
        if (contact.Kind == FieldingContactKind.Catch && CurrentDelivery.IsNoBall)
            PlayAudio(CricketAudioCue.Extra);
        if (collectionAction == FieldingCollectionAction.CaughtDismissal)
        {
            _cpuRunsRemaining = 0;
            _runRequestedPending = false;
            StartFielderAction(contact.FielderIndex, "fielder-catch", holdAtEnd: true);
            _shotOutcome = $"OUT: caught by fielder {contact.FielderIndex + 1}";
        }
        else if (collectionAction == FieldingCollectionAction.ReturnThrow)
        {
            _fielderThrowActive = true;
            _fielderThrowBallReleased = false;
            _fielderSequencePhase = FielderSequencePhase.Pickup;
            _fielderThrowerIndex = contact.FielderIndex;
            _fielderThrowStart = ToNumerics(contact.Position);
            _fielderThrowTarget = new NumericsVector3(0f, 0.55f, -PracticeGround.WicketOffset);
            var collectionClip = contact.Kind == FieldingContactKind.Catch ? "fielder-catch" : "fielder-pickup";
            StartFielderAction(contact.FielderIndex, collectionClip, holdAtEnd: false);
            _shotOutcome = contact.Kind == FieldingContactKind.Catch
                ? $"NO-BALL: fielder {contact.FielderIndex + 1} held it; return to wicketkeeper"
                : $"Fielder {contact.FielderIndex + 1} picked up; throw to wicketkeeper";
        }
        else
        {
            StartFielderAction(contact.FielderIndex,
                contact.Kind == FieldingContactKind.Catch ? "fielder-catch" : "fielder-pickup", holdAtEnd: false);
            _shotOutcome = contact.Kind == FieldingContactKind.Catch
                ? $"NO-BALL: fielder {contact.FielderIndex + 1} held it; one penalty run"
                : $"Fielder {contact.FielderIndex + 1} collected the ball";
        }

        _ballFlight.StopAtContact(ToNumerics(contact.Position));
        if (!_fielderThrowActive)
            FinishDelivery();
    }

    private void ResolveSettledBall(BallFlightFrame frame)
    {
        if (_battedBall)
            return;
        if (_isRunning)
        {
            var crossed = _runners.CurrentRunCrossed;
            CurrentDelivery.ResolveDeadBall(crossed);
            if (crossed)
                CompleteRun(recordScoring: false, allowNextRun: false);
            else
            {
                _runners.Stop();
                _shotOutcome = _batterRuns > 0
                    ? $"RUN: {_batterRuns} batter run(s); ball dead before the next crossing"
                    : "DOT: ball dead before the batters crossed";
            }
        }

        if (_shotOutcome.StartsWith("Choose", StringComparison.Ordinal))
            _shotOutcome = _battedBall ? "DOT: field held the shot" : "DOT ball";
        FinishDelivery();
    }

    private void ResolveBoundaryCrossing(BoundaryCrossing crossing)
    {
        CurrentDelivery.ResolveBoundary(crossing.ClearedInTheAir, _isRunning && _runners.CurrentRunCrossed);
        _shotOutcome = crossing.ClearedInTheAir ? "SIX: cleared the boundary" : "FOUR: reached the boundary";
        PlayAudio(CricketAudioCue.Boundary);
        _cpuRunsRemaining = 0;
        _runRequestedPending = false;
        _runners.Stop();
        _ballFlight.StopAtContact(crossing.Position);
        FinishDelivery();
    }

    private void FinishDelivery()
    {
        if (_deliveryComplete)
            return;

        _matchController.CompleteDelivery();
        _runners.Stop();
        _batterAnimations.SetRunning(false);
        _runRequestedPending = false;
        _cpuRunsRemaining = 0;
        _fielderThrowActive = false;
        _fielderSequencePhase = FielderSequencePhase.None;
        if (_dismissal != DismissalKind.None)
        {
            PlayAudio(CricketAudioCue.Wicket);
            if (_dismissal is DismissalKind.Bowled or DismissalKind.RunOut)
                _groundVertices = PracticeGround.CreateField(nearWicketBroken: true);
            _playerAnimator.Play("practice-stance", 0.12f);
        }
    }


}
