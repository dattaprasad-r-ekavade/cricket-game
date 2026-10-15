using SuperCricket.Content.Bowling;
using SuperCricket.Content;
using SuperCricket.Game;
using SuperCricket.Simulation;
using SuperCricket.Simulation.Batting;
using SuperCricket.Simulation.Bowling;

namespace SuperCricket.Game.Tests;

public sealed class PracticeNetsPresenterTests
{
    [Fact]
    public void TutorialProgressesOnlyAfterThreeIdealHitsForEachPrompt()
    {
        var presenter = new PracticeNetsPresenter();

        RecordThree(presenter, addedDirection: false, lofted: false);
        Assert.Equal(1, presenter.TutorialStep);
        Assert.Contains("add a direction", presenter.TutorialPrompt);

        presenter.RecordBattingBall(TimingZone.Ideal, madeContact: true, addedDirection: false, lofted: false);
        Assert.Equal(0, presenter.TutorialStepHits);
        presenter.RecordBattingBall(TimingZone.Early, madeContact: true, addedDirection: true, lofted: false);
        Assert.Equal(0, presenter.TutorialStepHits);
        RecordThree(presenter, addedDirection: true, lofted: false);
        Assert.Equal(2, presenter.TutorialStep);
        Assert.Contains("hold Shift", presenter.TutorialPrompt);

        RecordThree(presenter, addedDirection: true, lofted: true);
        Assert.True(presenter.TutorialComplete);
        Assert.Equal(string.Empty, presenter.TutorialPrompt);
    }

    [Fact]
    public void RecentZonesAndHistogramRetainOnlyTheLastTenBalls()
    {
        var presenter = new PracticeNetsPresenter();
        for (var index = 0; index < PracticeNetsPresenter.RecentBallCount + 2; index++)
            presenter.RecordBattingBall(index % 2 == 0 ? TimingZone.Ideal : TimingZone.Late,
                madeContact: true, addedDirection: false, lofted: false);

        Assert.Equal(PracticeNetsPresenter.RecentBallCount, presenter.RecentBalls.Count);
        Assert.Equal(3, presenter.RecentBalls[0].Index);
        Assert.Equal(5, presenter.BuildZoneHistogram()[TimingZone.Ideal]);
        Assert.Equal(5, presenter.BuildZoneHistogram()[TimingZone.Late]);
    }

    [Fact]
    public void BowlingDrillCompletesAfterFiveTargetHitsAndKeepsRecentResults()
    {
        var presenter = new PracticeNetsPresenter();
        for (var index = 0; index < 4; index++)
            presenter.RecordBowlingBall(TimingZone.Early, targetHit: true);

        Assert.False(presenter.BowlingTargetComplete);
        presenter.RecordBowlingBall(TimingZone.Ideal, targetHit: true);
        Assert.True(presenter.BowlingTargetComplete);
        Assert.Equal(5, presenter.BowlingTargetHits);
        Assert.Equal(5, presenter.RecentBalls.Count);
    }

    [Fact]
    public void SetupCyclesStyleLineLengthPaceAndDrill()
    {
        var setup = PracticeNetsSetup.Default;

        Assert.Equal(PracticeNetDrill.BowlingTarget, PracticeNetsSetup.Adjust(setup, 0, 1).Drill);
        Assert.Equal(BowlerStyle.OffSpin, PracticeNetsSetup.Adjust(setup, 1, 1).BowlerStyle);
        Assert.Equal(PracticeNetLine.LegSide, PracticeNetsSetup.Adjust(setup, 2, 1).Line);
        Assert.Equal(PracticeNetLength.Full, PracticeNetsSetup.Adjust(setup, 3, 1).Length);
        Assert.Equal(PracticeNetPace.Fast, PracticeNetsSetup.Adjust(setup, 4, 1).Pace);
    }

    [Fact]
    public void SetupBuildsAuthoredDeliveryAtSelectedStyleLineLengthAndPace()
    {
        var catalogue = DeliveryCatalogue.Load(Asset("bowling", "deliveries.json"));
        var stock = DeliveryPreset.Load(Asset("deliveries", "standard-pace.json"));
        var setup = PracticeNetsSetup.Default with
        {
            BowlerStyle = BowlerStyle.OffSpin,
            Line = PracticeNetLine.OutsideOff,
            Length = PracticeNetLength.Yorker,
            Pace = PracticeNetPace.Fast
        };

        var definition = catalogue.Deliveries.First(item => item.Style == setup.BowlerStyle);
        var requestedSpeed = definition.MinSpeedKph +
            (definition.MaxSpeedKph - definition.MinSpeedKph) * setup.SpeedFraction;
        var delivery = PracticeNetsSetup.CreateDelivery(setup, catalogue, stock);
        var bounce = BowlingAimModel.FindFirstBounce(delivery);
        var speed = delivery.ReleaseVelocity.ToVector3().Length() * 3.6f;
        var baseline = PaceMeterModel.CreateDelivery(definition, stock, 0f, 0f, requestedSpeed, PitchType.Normal);
        var baselineBounce = BowlingAimModel.FindFirstBounce(baseline);

        Assert.Equal("Off-break", delivery.Name);
        Assert.NotNull(bounce);
        Assert.NotNull(baselineBounce);
        Assert.InRange(MathF.Abs(bounce!.Value.Position.X - baselineBounce!.Value.Position.X - setup.LineOffsetMeters), 0f, 0.15f);
        Assert.InRange(MathF.Abs(bounce.Value.Position.Z - baselineBounce.Value.Position.Z - setup.LengthOffsetMeters), 0f, 0.15f);
        Assert.InRange(speed, 69f, 91f);
    }

    private static void RecordThree(PracticeNetsPresenter presenter, bool addedDirection, bool lofted)
    {
        for (var hit = 0; hit < PracticeNetsPresenter.IdealHitsPerTutorialStep; hit++)
            presenter.RecordBattingBall(TimingZone.Ideal, madeContact: true, addedDirection, lofted);
    }

    private static string Asset(params string[] segments)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var root = Path.Combine(directory.FullName, "assets");
            var candidate = Path.Combine(root, Path.Combine(segments));
            if (File.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("Could not locate Super Cricket test assets.");
    }
}
