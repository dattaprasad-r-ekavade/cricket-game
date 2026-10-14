using SuperCricket.Content;
using SuperCricket.Game;
using SuperCricket.Simulation;
using SuperCricket.Simulation.Bowling;

namespace SuperCricket.Game.Tests;

public sealed class MatchControllerBowlingTests
{
    [Fact]
    public void CpuPreparedDeliveryUsesTheSharedBowlingAccuracyModelAndCurrentStamina()
    {
        var first = TeamRosterAsset.CreatePlaceholder("First XI");
        var second = TeamRosterAsset.CreatePlaceholder("Second XI");
        var match = new LimitedOversMatch(first, second, oversPerInnings: 2);
        var controller = new MatchController(match);
        controller.StartNewMatch(2, seed: 612);
        var bowler = match.CurrentBowler;
        var stock = DeliveryPreset.Load(Asset("deliveries", "standard-pace.json"));

        var prepared = controller.PrepareDeliveryForCurrentMatch(
            stock,
            activePresetIndex: 0,
            cpuBattingControlled: false,
            bowlingAimOffsetX: 0f,
            bowlingAimOffsetZ: 0f,
            developerMode: false,
            verificationRun: false,
            CpuDifficulty.Standard);

        Assert.NotNull(controller.LastBowlingAccuracy);
        var currentFigures = match.CurrentBowlingFigures.Single(item => item.Bowler.Id == bowler.Id);
        Assert.Equal(currentFigures.Stamina, controller.LastBowlingAccuracy.Value.Stamina);
        Assert.InRange(controller.LastBowlingAccuracy.Value.TargetRadiusMeters, 0.12f, 2f);
        Assert.Equal(controller.LastBowlingAccuracy.Value.Delivery.Name, prepared.Name);
        Assert.NotNull(BowlingAimModel.FindFirstBounce(prepared));
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
