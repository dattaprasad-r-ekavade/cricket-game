using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SuperCricket.Simulation;
using SuperCricket.Simulation.Events;

namespace SuperCricket.Game;

public partial class Game1
{
    private const float UmpireSignalDisplaySeconds = 1.25f;

    private void SubscribeToMatchFeedbackEvents()
    {
        var events = _match.Events;
        _matchFeedbackSubscriptions.Add(events.Subscribe<BatContact>(HandleTimingFeedbackEvent));
        _matchFeedbackSubscriptions.Add(events.Subscribe<BallBeatBat>(HandleTimingFeedbackEvent));
        _matchFeedbackSubscriptions.Add(events.Subscribe<BodyHit>(HandleBodyHit));
        _matchFeedbackSubscriptions.Add(events.Subscribe<Boundary>(HandleBoundarySignal));
        _matchFeedbackSubscriptions.Add(events.Subscribe<DeliveryExtraSignalled>(HandleExtraSignal));
        _matchFeedbackSubscriptions.Add(events.Subscribe<Dismissal>(HandleDismissalSignal));
        _matchFeedbackSubscriptions.Add(events.Subscribe<WicketBroken>(HandleWicketSignal));
        _matchFeedbackSubscriptions.Add(events.Subscribe<UmpireDecision>(HandleUmpireDecisionSignal));
        _matchFeedbackSubscriptions.Add(events.Subscribe<Appeal>(HandleAppealSignal));
        _matchFeedbackSubscriptions.Add(events.Subscribe<NearRunOut>(HandleNearRunOut));
    }

    private void HandleTimingFeedbackEvent(MatchEvent matchEvent)
    {
        if (IsReviewRun)
            return;

        if (MatchHudPresenter.GetTimingAudioCue(matchEvent) is { } cue)
            PlayAudio(cue);
        StartGamePadFeedback(MatchHudPresenter.GetGamePadFeedbackCue(matchEvent));
    }

    private void HandleBodyHit(BodyHit bodyHit)
    {
        _bodyHitThisDelivery = true;
        HandleTimingFeedbackEvent(bodyHit);
    }

    private void HandleBoundarySignal(Boundary boundary)
    {
        if (boundary.Runs == 6)
            QueueReplayEvent("six");
        if (IsReviewRun)
            return;
        PlayAudio(CricketAudioCue.Boundary);
        ShowUmpireSignal(MatchHudPresenter.GetUmpireSignalLabel(boundary));
    }

    private void HandleExtraSignal(DeliveryExtraSignalled extra)
    {
        if (IsReviewRun)
            return;
        PlayAudio(CricketAudioCue.Extra);
        ShowUmpireSignal(MatchHudPresenter.GetUmpireSignalLabel(extra));
    }

    private void HandleDismissalSignal(Dismissal dismissal)
    {
        var replayType = dismissal.Kind.ToLowerInvariant() switch
        {
            "bowled" => "bowled",
            "caught" => "caught",
            "lbw" => "lbw",
            "runout" or "run-out" => "run-out",
            _ => null
        };
        if (replayType is not null)
            QueueReplayEvent(replayType, caption: BuildFallOfWicketsCaption(dismissal.Kind));
        if (dismissal.Kind is "RunOut" or "Lbw")
            _camera.SelectPreset("umpire-close-up");
        if (!IsReviewRun)
            ShowUmpireSignal(MatchHudPresenter.GetUmpireSignalLabel(dismissal));
    }

    private void HandleWicketSignal(WicketBroken wicket)
    {
        if (wicket.DismissalType is "Bowled" or "RunOut")
            _camera.SelectPreset("umpire-close-up");
        if (!IsReviewRun)
            ShowUmpireSignal(MatchHudPresenter.GetUmpireSignalLabel(wicket));
    }

    private void HandleUmpireDecisionSignal(UmpireDecision decision)
    {
        if (decision.IsOut && string.Equals(decision.DecisionType, "Lbw", StringComparison.OrdinalIgnoreCase))
            QueueReplayEvent("lbw", decision.ProjectedImpactX, decision.ProjectedImpactHeightMeters);
        if (string.Equals(decision.DecisionType, "Lbw", StringComparison.OrdinalIgnoreCase))
            _camera.SelectPreset("umpire-close-up");
        if (!IsReviewRun)
            ShowUmpireSignal(MatchHudPresenter.GetUmpireSignalLabel(decision));
    }

    private void HandleAppealSignal(Appeal appeal)
    {
        if (!IsReviewRun)
            ShowUmpireSignal(MatchHudPresenter.GetUmpireSignalLabel(appeal));
    }

    private void HandleNearRunOut(NearRunOut nearRunOut)
    {
        _camera.SelectPreset("umpire-close-up");
        QueueReplayEvent("near-run-out", creaseDistanceMeters: nearRunOut.ClosestCreaseDistanceMeters);
        if (!IsReviewRun)
            ShowUmpireSignal("SAFE");
    }

    private void ShowUmpireSignal(string? signal)
    {
        if (string.IsNullOrWhiteSpace(signal))
            return;
        _umpireSignalText = signal;
        _umpireSignalRemainingSeconds = UmpireSignalDisplaySeconds;
    }

    private void StartGamePadFeedback(GamePadFeedbackCue cue)
    {
        var profile = MatchHudPresenter.GetGamePadFeedbackProfile(cue);
        if (profile is not { } feedback || IsCpuBattingControlled || !_lastInputWasGamePad || IsReviewRun)
            return;

        try
        {
            if (!GamePad.GetState(PlayerIndex.One).IsConnected)
                return;
            _activeGamePadFeedbackCue = cue;
            _gamePadFeedbackElapsedSeconds = 0f;
            _gamePadFeedbackRemainingSeconds = feedback.DurationSeconds;
            ApplyGamePadFeedback();
        }
        catch (PlatformNotSupportedException)
        {
            StopGamePadFeedback();
        }
    }

    private void UpdateGamePadFeedback(float elapsedSeconds)
    {
        _umpireSignalRemainingSeconds = MathF.Max(0f, _umpireSignalRemainingSeconds - elapsedSeconds);
        _timingZoneFeedbackRemainingSeconds = MathF.Max(0f, _timingZoneFeedbackRemainingSeconds - elapsedSeconds);
        if (_activeGamePadFeedbackCue == GamePadFeedbackCue.None)
            return;

        _gamePadFeedbackElapsedSeconds += elapsedSeconds;
        _gamePadFeedbackRemainingSeconds = MathF.Max(0f, _gamePadFeedbackRemainingSeconds - elapsedSeconds);
        if (_gamePadFeedbackRemainingSeconds <= 0f)
        {
            StopGamePadFeedback();
            return;
        }

        try
        {
            if (!GamePad.GetState(PlayerIndex.One).IsConnected)
            {
                StopGamePadFeedback();
                return;
            }
            ApplyGamePadFeedback();
        }
        catch (PlatformNotSupportedException)
        {
            StopGamePadFeedback();
        }
    }

    private void ApplyGamePadFeedback()
    {
        var profile = MatchHudPresenter.GetGamePadFeedbackProfile(_activeGamePadFeedbackCue);
        if (profile is not { } feedback)
            return;
        var pulseIsOff = feedback.PulseSeconds > 0f &&
            MathF.Floor(_gamePadFeedbackElapsedSeconds / feedback.PulseSeconds) % 2f >= 1f;
        var left = pulseIsOff ? 0.04f : feedback.LeftMotor;
        var right = pulseIsOff ? 0.04f : feedback.RightMotor;
        GamePad.SetVibration(PlayerIndex.One, left, right);
    }

    private void StopGamePadFeedback()
    {
        _activeGamePadFeedbackCue = GamePadFeedbackCue.None;
        _gamePadFeedbackElapsedSeconds = 0f;
        _gamePadFeedbackRemainingSeconds = 0f;
        try
        {
            GamePad.SetVibration(PlayerIndex.One, 0f, 0f);
        }
        catch (PlatformNotSupportedException)
        {
            // Rumble is optional on platforms that do not expose it.
        }
    }
}
