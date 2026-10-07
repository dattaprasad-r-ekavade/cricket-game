using SuperCricket.Content;
using SuperCricket.Game;
using SuperCricket.Simulation;

namespace SuperCricket.Game.Tests;

public sealed class MatchControllerTests
{
    [Fact]
    public void CompletingBothInningsMarksTheFirstMatchAndRestartKeepsThatHistory()
    {
        var controller = new MatchController(new LimitedOversMatch(oversPerInnings: 1));
        controller.StartNewMatch(oversPerInnings: 1, seed: 491);

        Assert.True(controller.IsFirstMatch);
        for (var ball = 0; ball < OverScoreboard.BallsPerOver; ball++)
            CompleteBoundary(controller, clearedInTheAir: false);

        Assert.True(controller.Match.IsInningsComplete);
        Assert.False(controller.Match.IsMatchComplete);
        Assert.True(controller.IsFirstMatch);

        controller.StartNextInnings();
        Assert.Equal(2, controller.Match.InningsNumber);
        for (var ball = 0; ball < 5; ball++)
            CompleteBoundary(controller, clearedInTheAir: true);

        Assert.True(controller.Match.IsMatchComplete);
        Assert.False(controller.IsFirstMatch);

        controller.StartNewMatch(oversPerInnings: 1, seed: 491);

        Assert.Equal(1, controller.Match.InningsNumber);
        Assert.Equal(0, controller.Match.LegalBalls);
        Assert.False(controller.IsFirstMatch);
    }

    [Fact]
    public void BowlingDecisionSeedIsRepeatableAndChangesWithMatchProgress()
    {
        var controller = new MatchController(new LimitedOversMatch());
        controller.StartNewMatch(oversPerInnings: 1, seed: 1729);
        var initialSeed = controller.CreateBowlingDecisionSeed();

        Assert.Equal(initialSeed, controller.CreateBowlingDecisionSeed());

        CompleteBoundary(controller, clearedInTheAir: true);

        Assert.NotEqual(initialSeed, controller.CreateBowlingDecisionSeed());
    }

    [Fact]
    public void NextInningsRequiresTheFirstInningsToBeComplete()
    {
        var controller = new MatchController(new LimitedOversMatch());

        Assert.Throws<InvalidOperationException>(() => controller.StartNextInnings());
    }

    [Fact]
    public void PreparingHumanBattingDeliveryAppliesFirstMatchPaceWithoutMutatingSource()
    {
        var controller = new MatchController(new LimitedOversMatch());
        var source = CreateDelivery();

        var prepared = controller.PrepareHumanBattingDelivery(
            source,
            CpuDifficulty.Standard,
            humanBattingControlled: true,
            developerMode: false,
            verificationRun: false);

        Assert.NotSame(source, prepared);
        Assert.Equal(34f, source.ReleaseVelocity.ToVector3().Length());
        Assert.Equal(34f * DeliveryPaceModel.RookieOrFirstMatchSpeedMultiplier,
            prepared.ReleaseVelocity.ToVector3().Length(), precision: 4);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public void PreparingIneligibleDeliveryPreservesOriginalReferenceAndSpeed(
        bool humanBattingControlled,
        bool developerMode,
        bool verificationRun)
    {
        var controller = new MatchController(new LimitedOversMatch());
        var source = CreateDelivery();

        var prepared = controller.PrepareHumanBattingDelivery(
            source,
            CpuDifficulty.Rookie,
            humanBattingControlled,
            developerMode,
            verificationRun);

        Assert.Same(source, prepared);
        Assert.Equal(34f, prepared.ReleaseVelocity.ToVector3().Length());
    }

    [Fact]
    public void PreparingLaterStandardHumanDeliveryKeepsAuthoredPace()
    {
        var controller = new MatchController(new LimitedOversMatch());
        controller.StartNewMatch(oversPerInnings: 1, seed: 491);
        for (var ball = 0; ball < OverScoreboard.BallsPerOver; ball++)
            CompleteBoundary(controller, clearedInTheAir: false);
        controller.StartNextInnings();
        for (var ball = 0; ball < 5; ball++)
            CompleteBoundary(controller, clearedInTheAir: true);

        var source = CreateDelivery();
        var prepared = controller.PrepareHumanBattingDelivery(
            source,
            CpuDifficulty.Standard,
            humanBattingControlled: true,
            developerMode: false,
            verificationRun: false);

        Assert.NotSame(source, prepared);
        Assert.Equal(34f, prepared.ReleaseVelocity.ToVector3().Length());
    }

    [Fact]
    public void ChoosingCpuBowlingDeliveryIsRepeatableAndPreservesStockPreset()
    {
        var controller = new MatchController(new LimitedOversMatch());
        controller.StartNewMatch(oversPerInnings: 1, seed: 1729);
        var stock = CreateDelivery();

        var first = controller.ChooseCpuBowlingDelivery(stock, CpuDifficulty.Standard);
        var second = controller.ChooseCpuBowlingDelivery(stock, CpuDifficulty.Standard);

        Assert.NotSame(stock, first);
        Assert.Equal(first.Name, second.Name);
        Assert.Equal(first.ReleaseVelocity.X, second.ReleaseVelocity.X);
        Assert.Equal(first.ReleaseVelocity.Y, second.ReleaseVelocity.Y);
        Assert.Equal(first.ReleaseVelocity.Z, second.ReleaseVelocity.Z);
        Assert.Equal("Controller pace test", stock.Name);
        Assert.Equal(0f, stock.ReleaseVelocity.X);
        Assert.Equal(-34f, stock.ReleaseVelocity.Z);
        Assert.Equal(0f, stock.LateralAccelerationMetersPerSecondSquared);
    }

    [Fact]
    public void BowlingSituationAndFieldPlacementFollowCurrentMatchState()
    {
        var controller = new MatchController(new LimitedOversMatch());
        controller.StartNewMatch(oversPerInnings: 1, seed: 1729);
        CompleteBoundary(controller, clearedInTheAir: true);

        var situation = controller.CurrentBowlingSituation;
        var expectedSituation = new BowlingSituation(1, 1, 6, 0, null);
        var fieldPreset = CreateFieldPreset();
        var expectedPlacement = FieldPlacementModel.Choose(
            fieldPreset,
            expectedSituation,
            controller.Match.StrikerPlayer.Power);
        var placement = controller.ChooseFieldPlacement(fieldPreset);

        Assert.Equal(expectedSituation, situation);
        Assert.Equal(expectedPlacement.Tactic, placement.Tactic);
        Assert.Equal(expectedPlacement.StartingPositions, placement.StartingPositions);
    }

    [Fact]
    public void ConfiguringFieldingSideAppliesPlacementAndCurrentFieldingRatings()
    {
        var controller = new MatchController(new LimitedOversMatch());
        controller.StartNewMatch(oversPerInnings: 1, seed: 1729);
        var fieldPreset = CreateFieldPreset();
        var fieldingSide = new FieldingSide();

        var placement = controller.ConfigureFieldingSide(fieldingSide, fieldPreset);
        var target = new System.Numerics.Vector3(0f, -0.08f, 0f);
        fieldingSide.Step(0f, target);

        var expectedChaserIndex = Enumerable.Range(0, controller.Match.FieldingPlayers.Count)
            .OrderBy(index => FieldingSide.EstimateReachTime(
                placement.StartingPositions[index],
                target,
                controller.Match.FieldingPlayers[index].Fielding))
            .First();
        Assert.Equal(placement.StartingPositions, fieldingSide.Positions);
        Assert.Equal(expectedChaserIndex, fieldingSide.ActiveChaserIndex);
    }

    [Fact]
    public void PreparingDeliveryKeepsCpuAimAheadOfVerificationBypass()
    {
        var controller = new MatchController(new LimitedOversMatch());
        controller.StartNewMatch(oversPerInnings: 1, seed: 1729);
        var selected = CreateDelivery();
        var expected = BowlingAimModel.AimForPitchTarget(selected, 0.35f, -0.2f);

        var actual = controller.PrepareDeliveryForCurrentMatch(
            selected,
            activePresetIndex: 0,
            cpuBattingControlled: true,
            bowlingAimOffsetX: 0.35f,
            bowlingAimOffsetZ: -0.2f,
            developerMode: false,
            verificationRun: true,
            difficulty: CpuDifficulty.Standard);

        Assert.Equal(expected.ReleaseVelocity.X, actual.ReleaseVelocity.X);
        Assert.Equal(expected.ReleaseVelocity.Y, actual.ReleaseVelocity.Y);
        Assert.Equal(expected.ReleaseVelocity.Z, actual.ReleaseVelocity.Z);
        Assert.Equal(34f, selected.ReleaseVelocity.ToVector3().Length());
    }

    [Fact]
    public void PreparingFirstHumanDeliveryUsesSeededBowlingAndFirstMatchPace()
    {
        var controller = new MatchController(new LimitedOversMatch());
        controller.StartNewMatch(oversPerInnings: 1, seed: 1729);
        var selected = CreateDelivery();
        var expectedCpuDelivery = controller.ChooseCpuBowlingDelivery(selected, CpuDifficulty.Standard);
        var expected = controller.PrepareHumanBattingDelivery(
            expectedCpuDelivery,
            CpuDifficulty.Standard,
            humanBattingControlled: true,
            developerMode: false,
            verificationRun: false);

        var actual = controller.PrepareDeliveryForCurrentMatch(
            selected,
            activePresetIndex: 0,
            cpuBattingControlled: false,
            bowlingAimOffsetX: 0f,
            bowlingAimOffsetZ: 0f,
            developerMode: false,
            verificationRun: false,
            difficulty: CpuDifficulty.Standard);

        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.ReleaseVelocity.X, actual.ReleaseVelocity.X);
        Assert.Equal(expected.ReleaseVelocity.Y, actual.ReleaseVelocity.Y);
        Assert.Equal(expected.ReleaseVelocity.Z, actual.ReleaseVelocity.Z);
        Assert.Equal(34f, selected.ReleaseVelocity.ToVector3().Length());
    }

    [Fact]
    public void PreparingHumanDeliveryDuringVerificationLeavesPresetUnchanged()
    {
        var controller = new MatchController(new LimitedOversMatch());
        controller.StartNewMatch(oversPerInnings: 1, seed: 1729);
        var selected = CreateDelivery();

        var actual = controller.PrepareDeliveryForCurrentMatch(
            selected,
            activePresetIndex: 0,
            cpuBattingControlled: false,
            bowlingAimOffsetX: 0f,
            bowlingAimOffsetZ: 0f,
            developerMode: false,
            verificationRun: true,
            difficulty: CpuDifficulty.Standard);

        Assert.Same(selected, actual);
        Assert.Equal(34f, actual.ReleaseVelocity.ToVector3().Length());
    }

    private static DeliveryPreset CreateDelivery() => new()
    {
        Name = "Controller pace test",
        ReleasePosition = new Vector3Data { X = 0f, Y = 1f, Z = 20f },
        ReleaseVelocity = new Vector3Data { X = 0f, Y = 0f, Z = -34f }
    };

    private static FieldPreset CreateFieldPreset() => new()
    {
        Name = "Controller placement test",
        Players = Enumerable.Range(0, 10)
            .Select(index => new FieldPositionData
            {
                Name = $"Fielder {index + 1}",
                IsWicketkeeper = index == 0,
                Position = new Vector3Data { X = index, Y = -0.08f, Z = index + 1 }
            })
            .ToList()
    };

    private static void CompleteBoundary(MatchController controller, bool clearedInTheAir)
    {
        controller.BeginDelivery(isNoBall: false)
            .ResolveBoundary(clearedInTheAir, currentRunCrossed: false);
        controller.CompleteDelivery();
    }
}
