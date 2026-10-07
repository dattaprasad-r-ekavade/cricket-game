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

    private void StartShot(string name, float? horizontalAimOverride = null)
    {
        if (_simulationPaused || _shotResolved || _ballFlight.CurrentFrame.Phase == BallMotionPhase.Settled)
            return;

        var authoredShot = _shotSet.Get(name);
        var horizontalAim = horizontalAimOverride ??
            Math.Clamp(authoredShot.HorizontalAim + _humanShotAimOffset, -1f, 1f);
        if (!float.IsFinite(horizontalAim) || horizontalAim is < -1f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(horizontalAimOverride), "Shot direction must be between -1 and 1.");
        _chosenShot = CopyShotWithAim(authoredShot, horizontalAim);
        _shotInputDelaySeconds = _bowlerRunUpElapsed + _bowlerActionElapsed -
            (_bowlerRunUpDurationSeconds + _bowlerReleaseTimeSeconds);
        _battingStepRecoveryActive = false;
        _shotResolved = false;
        _shotOutcome = $"Swinging {_chosenShot.Name}; timing and placement decide contact.";
        _playerAnimator.Play(_chosenShot.AnimationClip, 0.12f);
    }

    private void AdjustHumanShotAim(float adjustment)
    {
        if (!float.IsFinite(adjustment) || adjustment == 0f)
            return;

        _humanShotAimOffset = Math.Clamp(_humanShotAimOffset + adjustment, -2f, 2f);
        if (_chosenShot is not { } chosenShot || _shotResolved || IsCpuBattingControlled)
            return;

        var authoredShot = _shotSet.Get(chosenShot.Name);
        var horizontalAim = Math.Clamp(authoredShot.HorizontalAim + _humanShotAimOffset, -1f, 1f);
        _chosenShot = CopyShotWithAim(authoredShot, horizontalAim);
    }

    private static BattingShotData CopyShotWithAim(BattingShotData shot, float horizontalAim) => new()
    {
        Name = shot.Name,
        AnimationClip = shot.AnimationClip,
        LaunchAngleDegrees = shot.LaunchAngleDegrees,
        HorizontalAim = horizontalAim,
        SpeedTransfer = shot.SpeedTransfer
    };

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
            if (!IsCpuBattingControlled)
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
        _isRunning = true;
        _runElapsed = 0f;
        _runHoldElapsed = 0f;
        _playerAnimator.Play("between-wickets", 0.12f);
    }

    private void CancelRun()
    {
        if (_simulationPaused) return;
        if (_runRequestedPending)
        {
            _runRequestedPending = false;
            if (IsCpuBattingControlled)
                _cpuRunsRemaining = 0;
            return;
        }
        if (!_isRunning)
            return;
        if (IsCpuBattingControlled)
            _cpuRunsRemaining = 0;
        _isRunning = false;
        _runElapsed = 0f;
        _runHoldElapsed = 0f;
        _playerAnimator.Play("practice-stance", 0.12f);
    }

    private void UpdateRun(float deltaTime)
    {
        _runElapsed += deltaTime;
        if (_runElapsed < _runDurationSeconds)
            return;

        CompleteRun();
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

        _ballFlight.StopAtContact(_fielderThrowTarget);
        _fielderThrowActive = false;
        _fielderThrowBallReleased = false;
        _fielderSequencePhase = FielderSequencePhase.None;
        _fielderActionClips[_fielderThrowerIndex] = null;
        _fielderActionHoldAtEnd[_fielderThrowerIndex] = false;
        if (_isRunning)
        {
            CurrentDelivery.ResolveRunOut();
            _shotOutcome = $"OUT: fielder {_fielderThrowerIndex + 1} threw to the wicketkeeper";
        }
        else
        {
            _shotOutcome = $"Wicketkeeper received fielder {_fielderThrowerIndex + 1}'s throw";
        }
        FinishDelivery();
    }

    private void CompleteRun(bool recordScoring = true, bool allowNextRun = true)
    {
        if (recordScoring)
            CurrentDelivery.RecordCompletedRun();
        _liveCompletedRunCrossings++;
        _isRunning = false;
        _runElapsed = 0f;
        _runHoldElapsed = 0f;
        _shotOutcome = $"RUN completed: {_batterRuns} batter run(s)";
        _playerAnimator.Play("practice-stance", 0.12f);
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
    }

    private void ResolveIncomingDelivery(NumericsVector3 wicketLinePosition)
    {
        var contact = ToXna(wicketLinePosition);
        var resolution = CurrentDelivery.ResolveIncoming(
            isWide: CricketDeliveryRuleModel.IsWide(contact.X, _deliveryPreset.PitchWidthMeters),
            hitsWickets: MathF.Abs(contact.X) <= 0.12f + _deliveryPreset.BallRadiusMeters &&
                contact.Y >= 0f && contact.Y <= PracticeGround.WicketHeight + _deliveryPreset.BallRadiusMeters);
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

        if (contact.Kind == FieldingContactKind.Catch)
        {
            _cpuRunsRemaining = 0;
            _runRequestedPending = false;
            StartFielderAction(contact.FielderIndex, "fielder-catch", holdAtEnd: true);
            if (!CurrentDelivery.ResolveCatch())
            {
                _shotOutcome = $"NO-BALL: fielder {contact.FielderIndex + 1} caught it; one penalty run";
                PlayAudio(CricketAudioCue.Extra);
            }
            else
            {
                _shotOutcome = $"OUT: caught by fielder {contact.FielderIndex + 1}";
            }
        }
        else if (_isRunning)
        {
            _fielderThrowActive = true;
            _fielderThrowBallReleased = false;
            _fielderSequencePhase = FielderSequencePhase.Pickup;
            _fielderThrowerIndex = contact.FielderIndex;
            _fielderThrowStart = ToNumerics(contact.Position);
            _fielderThrowTarget = new NumericsVector3(0f, 0.55f, -PracticeGround.WicketOffset);
            StartFielderAction(contact.FielderIndex, "fielder-pickup", holdAtEnd: false);
            _shotOutcome = $"Fielder {contact.FielderIndex + 1} picked up; throw to wicketkeeper";
        }
        else
        {
            StartFielderAction(contact.FielderIndex, "fielder-pickup", holdAtEnd: false);
            _shotOutcome = $"Fielder {contact.FielderIndex + 1} collected the ball";
        }

        _ballFlight.StopAtContact(ToNumerics(contact.Position));
        if (!_fielderThrowActive)
            FinishDelivery();
    }

    private void ResolveSettledBall(BallFlightFrame frame)
    {
        if (_isRunning)
        {
            var runCompleted = _runElapsed / _runDurationSeconds >= 0.72f;
            var runOut = CurrentDelivery.ResolveRunAtStoppage(runCompleted);
            if (runCompleted)
                CompleteRun(recordScoring: false, allowNextRun: false);
            else if (runOut)
            {
                _shotOutcome = "OUT: run out while attempting a run";
            }
            else
            {
                _isRunning = false;
            }
        }

        if (_shotOutcome.StartsWith("Choose", StringComparison.Ordinal))
            _shotOutcome = _battedBall ? "DOT: field held the shot" : "DOT ball";
        FinishDelivery();
    }

    private void ResolveBoundaryCrossing(BoundaryCrossing crossing)
    {
        CurrentDelivery.ResolveBoundary(crossing.ClearedInTheAir, _isRunning && _runElapsed >= _runDurationSeconds * 0.5f);
        _shotOutcome = crossing.ClearedInTheAir ? "SIX: cleared the boundary" : "FOUR: reached the boundary";
        PlayAudio(CricketAudioCue.Boundary);
        _cpuRunsRemaining = 0;
        _runRequestedPending = false;
        _isRunning = false;
        _ballFlight.StopAtContact(crossing.Position);
        FinishDelivery();
    }

    private void FinishDelivery()
    {
        if (_deliveryComplete)
            return;

        _match.CompleteDelivery();
        _isRunning = false;
        _runRequestedPending = false;
        _cpuRunsRemaining = 0;
        _liveCompletedRunCrossings = 0;
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

    private static bool TryCrossPlane(NumericsVector3 previous, NumericsVector3 current, float planeZ, out NumericsVector3 crossing)
    {
        if (previous.Z < planeZ || current.Z > planeZ || MathF.Abs(current.Z - previous.Z) < 0.000001f)
        {
            crossing = default;
            return false;
        }

        var amount = (planeZ - previous.Z) / (current.Z - previous.Z);
        crossing = NumericsVector3.Lerp(previous, current, Math.Clamp(amount, 0f, 1f));
        return true;
    }


}
