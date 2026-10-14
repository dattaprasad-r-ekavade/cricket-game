using System.Numerics;
using SuperCricket.Content;
using SuperCricket.Simulation;

namespace SuperCricket.Simulation.Tests;

public sealed class TimingWindowModelTests
{
    [Theory]
    [InlineData(4.01f, TimingZone.TooEarly)]
    [InlineData(4f, TimingZone.Start)]
    [InlineData(3f, TimingZone.Early)]
    [InlineData(2f, TimingZone.Ideal)]
    [InlineData(-2f, TimingZone.Ideal)]
    [InlineData(-3f, TimingZone.Late)]
    [InlineData(-4f, TimingZone.End)]
    [InlineData(-4.01f, TimingZone.TooLate)]
    public void Classify_ExactBoundariesBelongToTheInnerZone(float distance, TimingZone expected)
    {
        var window = TimingWindowModel.Build(0f, 8f, 4f);

        Assert.Equal(expected, TimingWindowModel.Classify(window, distance));
    }

    [Fact]
    public void Build_ShrinkingIdealBandWidensEarlyAndLateBands()
    {
        var wideIdeal = TimingWindowModel.Build(0f, 1.4f, 1f);
        var narrowIdeal = TimingWindowModel.Build(0f, 1.4f, 0.4f);

        Assert.True(narrowIdeal.Early.WidthMeters > wideIdeal.Early.WidthMeters);
        Assert.True(narrowIdeal.Late.WidthMeters > wideIdeal.Late.WidthMeters);
    }

    [Theory]
    [InlineData(TimingZone.Early, TimingZone.Ideal)]
    [InlineData(TimingZone.Late, TimingZone.Ideal)]
    [InlineData(TimingZone.Start, TimingZone.Start)]
    [InlineData(TimingZone.TooEarly, TimingZone.TooEarly)]
    [InlineData(TimingZone.Ideal, TimingZone.Ideal)]
    public void ApplyAssist_PromotesOnlyEarlyAndLate(TimingZone input, TimingZone expected)
    {
        Assert.Equal(expected, TimingWindowModel.ApplyAssist(input, assist: true));
    }

    [Fact]
    public void IdealWidth_UsesTheSettleCurveEndpointsAndKnots()
    {
        var profile = new TimingDifficulty();

        Assert.Equal(0.6f, WidthAt(0));
        Assert.Equal(0.6f, WidthAt(8));
        Assert.Equal(0.75f, WidthAt(20));
        Assert.Equal(1f, WidthAt(36));

        float WidthAt(int ballsFaced) => TimingWindowModel.IdealWidth(
            profile, new BatterTimingState(ballsFaced, confidence: 1f), loft: false, strokeRating: 80f);
    }

    [Fact]
    public void IdealWidth_AppliesLoftConfidenceSkillAndMinimumFactors()
    {
        var profile = new TimingDifficulty { IdealBase = 1f, IdealMinimum = 0.1f, LoftReduction = 0.25f };
        var settled = new BatterTimingState(36, confidence: 1f);
        var unsettled = new BatterTimingState(36, confidence: 0f);

        Assert.Equal(1.1f, TimingWindowModel.IdealWidth(profile, settled, loft: false, strokeRating: 100f), 5);
        Assert.Equal(0.9f, TimingWindowModel.IdealWidth(profile, settled, loft: false, strokeRating: 0f), 5);
        Assert.Equal(0.75f, TimingWindowModel.IdealWidth(profile, settled, loft: true, strokeRating: 80f), 5);
        Assert.Equal(0.8f, TimingWindowModel.IdealWidth(profile, unsettled, loft: false, strokeRating: 80f), 5);

        var narrowProfile = new TimingDifficulty { IdealBase = 0.5f, IdealMinimum = 0.4f, LoftReduction = 0.9f };
        Assert.Equal(0.4f, TimingWindowModel.IdealWidth(narrowProfile, new BatterTimingState(confidence: 0f), loft: true, strokeRating: 80f));
    }

    [Fact]
    public void IdealWidth_UsesFootSideAndLoftSpecificPlayerRatings()
    {
        var profile = new TimingDifficulty { IdealBase = 1f, IdealMinimum = 0.1f };
        var batter = new TeamPlayerData
        {
            BattingRatings = new BattingRatingsData
            {
                FrontFoot = 98,
                BackFoot = 42,
                OffSide = 90,
                LegSide = 58,
                Lofted = 94,
                Concentration = 70
            }
        };
        var state = new BatterTimingState(36, confidence: 1f);

        var frontFootOffSide = TimingWindowModel.IdealWidth(
            profile, state, batter, StrokeFoot.Front, ShotDirection8.OffDrive, loft: false);
        var backFootLegSideLoft = TimingWindowModel.IdealWidth(
            profile, state, batter, StrokeFoot.Back, ShotDirection8.MidWicket, loft: true);

        Assert.True(frontFootOffSide > backFootLegSideLoft);
        Assert.Equal(94f, PlayerBattingRatingModel.GetStrokeRating(
            batter, StrokeFoot.Front, ShotDirection8.OffDrive, loft: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerBattingRatingModel.GetStrokeRating(
            batter, StrokeFoot.Auto, ShotDirection8.Straight, loft: false));
    }

    [Fact]
    public void DifficultyProfileControlsTimingWindowAndAssist()
    {
        var profile = new DifficultyProfileAsset
        {
            Side = DifficultySide.Human,
            Timing = new DifficultyTimingProfile
            {
                CentreMeters = 0.25f,
                WindowMeters = 7f,
                IdealBase = 1.1f,
                IdealMinimum = 0.35f,
                AssistEnabled = false
            }
        };

        var window = TimingWindowModel.Build(profile);

        Assert.Equal(0.25f, window.CentreMeters);
        Assert.Equal(7f, window.WindowMeters);
        Assert.False(TimingWindowModel.ApplyAssist(TimingZone.Early, profile) == TimingZone.Ideal);
    }

    [Fact]
    public void BatterTimingState_UpdatesAndClampsConfidence()
    {
        var state = new BatterTimingState();
        Assert.Equal(0.6f, state.Confidence);

        state.RegisterBall(TimingZone.Ideal, bodyHit: false, beaten: false);
        Assert.Equal(0.65f, state.Confidence, 5);
        state.RegisterBall(TimingZone.Early, bodyHit: false, beaten: true);
        Assert.Equal(0.55f, state.Confidence, 5);
        state.RegisterBall(TimingZone.Late, bodyHit: true, beaten: false);
        Assert.Equal(0.4f, state.Confidence, 5);
        Assert.Equal(3, state.BallsFaced);

        var clamped = new BatterTimingState(confidence: 0.99f);
        for (var count = 0; count < 10; count++)
            clamped.RegisterBall(TimingZone.Ideal, bodyHit: false, beaten: false);
        Assert.Equal(1f, clamped.Confidence);
        for (var count = 0; count < 20; count++)
            clamped.RegisterBall(TimingZone.TooLate, bodyHit: true, beaten: true);
        Assert.Equal(0f, clamped.Confidence);
    }

    [Fact]
    public void Classify_IsMonotonicInDistance()
    {
        var window = TimingWindowModel.Build(0.5f, 1.4f, 0.6f);
        var distanceOrder = new[]
        {
            TimingZone.TooLate,
            TimingZone.End,
            TimingZone.Late,
            TimingZone.Ideal,
            TimingZone.Early,
            TimingZone.Start,
            TimingZone.TooEarly
        };
        var previousIndex = 0;

        for (var distance = -1f; distance <= 2.0001f; distance += 0.01f)
        {
            var current = TimingWindowModel.Classify(window, distance);
            var currentIndex = Array.IndexOf(distanceOrder, current);
            Assert.True(currentIndex >= previousIndex, $"Zone moved backward at d={distance:0.00}.");
            previousIndex = currentIndex;
        }
    }

    [Fact]
    public void DistanceToContactPlane_MeasuresAlongVelocityAndIsPaceIndependentForStraightBalls()
    {
        var angled = new BallFlightFrame(0f, new Vector3(1f, 2f, 5f), new Vector3(3f, -4f, -12f), 0, BallMotionPhase.InFlight);
        Assert.Equal(13f, TimingWindowModel.DistanceToContactPlane(angled, -7f), 5);

        var normalPace = new BallFlightFrame(0f, new Vector3(0f, 1f, 1f), new Vector3(0f, 0f, -25f), 0, BallMotionPhase.InFlight);
        var fastPace = normalPace with { Velocity = new Vector3(0f, 0f, -40f) };
        var normalDistance = TimingWindowModel.DistanceToContactPlane(normalPace, 0f);
        var fastDistance = TimingWindowModel.DistanceToContactPlane(fastPace, 0f);
        var window = TimingWindowModel.Build(0.8f, 1.4f, 0.6f);
        Assert.Equal(normalDistance, fastDistance);
        Assert.Equal(TimingWindowModel.Classify(window, normalDistance), TimingWindowModel.Classify(window, fastDistance));
    }

    [Fact]
    public void BuildAndIdealWidth_RejectInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TimingWindowModel.Build(float.NaN, 1f, 0.5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => TimingWindowModel.Build(0f, 0f, 0.5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => TimingWindowModel.Build(0f, 1f, 1.1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => TimingWindowModel.IdealWidth(new TimingDifficulty(), new BatterTimingState(), false, 101f));
        Assert.Throws<ArgumentException>(() => TimingWindowModel.IdealWidth(
            new TimingDifficulty { IdealMinimum = 1.5f }, new BatterTimingState(), false, 80f));
    }
}
