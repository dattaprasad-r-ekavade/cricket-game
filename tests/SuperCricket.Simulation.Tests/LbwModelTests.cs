using System.Numerics;
using SuperCricket.Simulation.Umpiring;

namespace SuperCricket.Simulation.Tests;

public sealed class LbwModelTests
{
    private static LbwContact Contact(float x = 0f, float height = 0.5f, float velocityX = 0f,
        float velocityY = 0f, bool hasPitched = true, bool pitchedOutsideLeg = false,
        bool impactOutsideOffStump = false, bool shotOffered = true) => new(
            new Vector3(x, height, CricketPitchGeometry.NearWicketZ + 2f),
            new Vector3(velocityX, velocityY, -20f), HasPitched: hasPitched,
            PitchedOutsideLeg: pitchedOutsideLeg, ImpactOutsideOffStump: impactOutsideOffStump,
            ShotOffered: shotOffered);

    [Fact]
    public void Evaluate_ClassifiesHittingUmpiresCallMissingAndGoingOver()
    {
        Assert.Equal(LbwBand.Hitting, LbwModel.Evaluate(Contact(), 0.35f, 1).Band);
        Assert.Equal(LbwBand.UmpiresCall, LbwModel.Evaluate(Contact(velocityX: 1.2f), 0f, 1).Band);
        Assert.Equal(LbwBand.Missing, LbwModel.Evaluate(Contact(velocityX: 1.8f), 1f, 1).Band);
        Assert.Equal(LbwBand.GoingOver, LbwModel.Evaluate(Contact(height: 0.8f), 1f, 1).Band);
        Assert.True(LbwModel.Evaluate(Contact(), 0.35f, 1).IsOut);
        Assert.False(LbwModel.Evaluate(Contact(velocityX: 1.2f), 0f, 1).IsOut);
    }

    [Fact]
    public void Evaluate_AppliesOutsideLegAndOfferedShotRules()
    {
        Assert.False(LbwModel.Evaluate(Contact(pitchedOutsideLeg: true), 1f, 4).IsOut);
        Assert.False(LbwModel.Evaluate(Contact(impactOutsideOffStump: true, shotOffered: true), 1f, 4).IsOut);
        Assert.True(LbwModel.Evaluate(Contact(impactOutsideOffStump: true, shotOffered: false), 0f, 4).IsOut);
    }

    [Fact]
    public void Evaluate_UsesSeededUmpiresCallWithinFivePercent()
    {
        const float probability = 0.35f;
        var outCount = Enumerable.Range(0, 1000)
            .Count(seed => LbwModel.Evaluate(Contact(velocityX: 1.2f), probability, seed).IsOut);

        Assert.InRange(outCount / 1000f, probability - 0.05f, probability + 0.05f);
    }

    [Fact]
    public void TryProjectToWickets_AppliesOneBounceWhenTheBallHasNotPitched()
    {
        var contact = Contact(height: 0.3f, velocityY: -5f, hasPitched: false);

        Assert.True(LbwModel.TryProjectToWickets(contact, out var x, out var height));
        Assert.Equal(0f, x);
        Assert.True(height > 0f);
    }

    [Fact]
    public void TryProjectToWickets_RejectsBallMovingAwayFromTheStumps()
    {
        var contact = Contact() with { PreImpactVelocity = new Vector3(0, 0, 20f) };
        Assert.False(LbwModel.TryProjectToWickets(contact, out _, out _));
    }

    [Fact]
    public void DeliverySession_RecordsLbwAndRejectsIllegalDeliveryDismissal()
    {
        var session = new DeliverySession(isNoBall: false);
        session.ResolveLbw();
        Assert.Equal(DismissalKind.Lbw, session.Complete().Dismissal);

        var noBall = new DeliverySession(isNoBall: true);
        Assert.Throws<InvalidOperationException>(noBall.ResolveLbw);
        Assert.Contains("wide or no-ball", new DeliveryResult(0, 1, 0, IsLegal: false,
            Extra: DeliveryExtra.NoBall, Dismissal: DismissalKind.Lbw).Validate());
    }
}
