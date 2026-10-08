using System;
using SuperCricket.Simulation;

namespace SuperCricket.Game;

public partial class Game1
{
    private readonly BattingTimingCoordinator _battingTiming = new();
    private BattingTimingRequest? _shotBattingTimingRequest;
    private BattingTimingRequest? _automaticFootworkAppliedRequest;
    private float? _shotIdealInputDelaySeconds;

    private void PrepareBattingTiming()
    {
        CancelBattingTiming();
        _automaticFootworkAppliedRequest = null;
        if (IsCpuBattingControlled)
            return;

        var delivery = _deliveryPreset.DeepCopy();
        var batter = _playerAsset;
        var bowler = _bowlerAsset;
        var shots = _shotSet;
        var footwork = _targetBatterFootworkOffsetX;
        var shouldSelectAutomaticFootwork = !_developerMode && !_verifyGameplay && _captureTarget is null;
        _battingTiming.Prepare(token => shouldSelectAutomaticFootwork
                ? BattingPracticeAnalyzer.CalibrateReachableFootwork(
                    batter, bowler, shots, delivery, cancellationToken: token)
                : new AutomaticFootworkTimingPlan(footwork, BattingPracticeAnalyzer.CalibrateTiming(
                    batter, bowler, shots, delivery, footwork, cancellationToken: token)),
            runSynchronously: _verifyGameplay || _captureTarget is not null);
    }

    private float? FindCurrentIdealInputDelaySeconds(string shotName) =>
        _battingTiming.FindIdealInputDelaySeconds(shotName);

    private void RecordShotTiming(string shotName)
    {
        _shotIdealInputDelaySeconds = _footworkTransitionActive
            ? null
            : FindCurrentIdealInputDelaySeconds(shotName);
        _shotBattingTimingRequest = _footworkTransitionActive ? null : _battingTiming.Current;
    }

    private float? FindShotIdealInputDelaySeconds(string shotName)
    {
        _shotIdealInputDelaySeconds ??= _shotBattingTimingRequest?.FindIdealInputDelaySeconds(shotName);
        return _shotIdealInputDelaySeconds;
    }

    private void CancelBattingTiming() => _battingTiming.Clear();

    private void UpdateAutomaticBatterFootwork()
    {
        if (_developerMode || IsCpuBattingControlled || _battingTiming.Current is not { } request ||
            !request.Completion.IsCompletedSuccessfully || ReferenceEquals(request, _automaticFootworkAppliedRequest))
            return;

        _automaticFootworkAppliedRequest = request;
        var target = request.Completion.Result.FootworkOffsetMeters;
        _targetBatterFootworkOffsetX = target;
        _footworkTransitionActive = MathF.Abs(target - _batterFootworkOffsetX) > 0.0001f;
        if (!_footworkTransitionActive)
            return;

        _automaticFootworkIsSettling = true;
        _playerAnimator.PlayOnce(target > _batterFootworkOffsetX
            ? "batting-step-offside" : "batting-step-legside", 0.08f);
        _battingStepRecoveryActive = true;
    }

    private bool IsAutomaticBatterFootworkReady()
    {
        if (_developerMode || IsCpuBattingControlled)
            return true;
        var request = _battingTiming.Current;
        if (request is null)
            return true;
        UpdateAutomaticBatterFootwork();
        if (!request.Completion.IsCompleted)
            return false;
        if (!request.Completion.IsCompletedSuccessfully)
            return true;
        if (!ReferenceEquals(request, _automaticFootworkAppliedRequest))
            return false;
        if (!_automaticFootworkIsSettling)
            return true;
        if (_footworkTransitionActive || _battingStepRecoveryActive || _playerAnimator.IsTransitioning)
            return false;

        _automaticFootworkIsSettling = false;
        return true;
    }
}
