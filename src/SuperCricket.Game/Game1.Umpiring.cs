using SuperCricket.Content;
using SuperCricket.Simulation;
using SuperCricket.Simulation.Events;
using SuperCricket.Simulation.Umpiring;

namespace SuperCricket.Game;

public partial class Game1
{
    private const float HumanAppealWindowSeconds = 0.9f;

    private void HandleLbwPadContact(LbwContact contact, float contactTimeSeconds)
    {
        var seed = _matchController.CreateBowlingDecisionSeed() ^ unchecked((int)0x54b61d3f);
        var classification = LbwModel.Evaluate(contact, 0f, seed);
        if (classification.Band is not (LbwBand.Hitting or LbwBand.UmpiresCall))
        {
            if (!HasHumanBowlingControls)
                _shotOutcome = "PAD HIT: ball was missing the stumps or going over";
            return;
        }

        if (!HasHumanBowlingControls)
        {
            ResolveLbwAppeal(contact, contactTimeSeconds, humanAppeal: false);
            return;
        }

        if (_appealRequestedThisDelivery)
        {
            ResolveLbwAppeal(contact, contactTimeSeconds, humanAppeal: true);
            return;
        }

        _pendingLbwContact = contact;
        _pendingLbwContactTimeSeconds = contactTimeSeconds;
        _lbwAppealWindowRemainingSeconds = HumanAppealWindowSeconds;
        ShowUmpireSignal("APPEAL? PRESS A/X");
    }

    private void ResolveLbwAppeal(LbwContact contact, float contactTimeSeconds, bool humanAppeal)
    {
        if (HasHumanBowlingControls && !humanAppeal)
            return;
        var side = HasHumanBowlingControls ? DifficultySide.Human : DifficultySide.Cpu;
        var profile = CpuDifficultyModel.GetProfile(_cpuDifficulty, side);
        var seed = _matchController.CreateBowlingDecisionSeed() ^ unchecked((int)0x54b61d3f);
        var decision = LbwModel.Evaluate(contact, profile, seed);
        if (decision.Band is not (LbwBand.Hitting or LbwBand.UmpiresCall))
        {
            _pendingLbwContact = null;
            return;
        }

        _match.Events.Publish(new Appeal(contactTimeSeconds, DismissalKind.Lbw.ToString()));
        _match.Events.Publish(new UmpireDecision(
            contactTimeSeconds,
            decision.IsOut,
            DismissalKind.Lbw.ToString(),
            float.IsFinite(decision.ProjectedOffsetX) ? decision.ProjectedOffsetX : null,
            float.IsFinite(decision.ProjectedHeightMeters) ? decision.ProjectedHeightMeters : null,
            decision.Band.ToString()));
        _pendingLbwContact = null;
        _lbwAppealWindowRemainingSeconds = 0f;

        if (decision.IsOut)
        {
            _camera.SelectPreset("umpire-close-up");
            CurrentDelivery.ResolveLbw();
            _shotOutcome = $"OUT: LBW ({FormatLbwBand(decision.Band)})";
            _ballFlight.StopAtContact(contact.Position);
            FinishDelivery();
            return;
        }

        _shotOutcome = $"NOT OUT: LBW appeal ({FormatLbwBand(decision.Band)})";
        ShowUmpireSignal("NOT OUT");
    }

    private static string FormatLbwBand(LbwBand band) => band switch
    {
        LbwBand.Hitting => "hitting",
        LbwBand.UmpiresCall => "umpire's call",
        _ => band.ToString().ToLowerInvariant()
    };
}
