using System.Text.Json;
using SuperCricket.Content;
using SuperCricket.Simulation;

namespace SuperCricket.Simulation.Tests;

public sealed class CoreMatchReviewCheckTests
{
    [Fact] public void MatchState() => MatchStateReviewChecks.Run();
    [Fact] public void LimitedOversMatch() => LimitedOversMatchReviewChecks.Run();
    [Fact] public void TeamRoster() => TeamRosterReviewChecks.Run();
    [Fact] public void BowlingDecision() => BowlingDecisionReviewChecks.Run();
    [Fact] public void FieldPlacement() => FieldPlacementReviewChecks.Run();
    [Fact] public void CpuBattingOutcome() => CpuBattingOutcomeReviewChecks.Run();
    [Fact] public void ControllerInput() => MatchControllerInputReviewChecks.Run();
    [Fact] public void GameSettings() => GameSettingsReviewChecks.Run();
    [Fact] public void DeliveryRules() => CricketDeliveryRuleReviewChecks.Run();
    [Fact] public void ProceduralAudio() => ProceduralCricketAudioReviewChecks.Run();
}

public sealed class AutomatedMatchBatchReviewCheckTests
{
    [Fact]
    public void DeterministicMatchBatch() => AutomatedMatchBatchReviewChecks.Run();
}

public sealed class FieldingReviewCheckTests
{
    [Fact]
    public void FieldingScenarios() => FieldingReviewChecks.Run();
}

public sealed class CpuLiveBattingPlanReviewCheckTests
{
    [Fact]
    public void CpuBattingPlanAndRunningScenarios()
    {
        var paths = TestAssets.CpuBattingInputs();
        CpuLiveBattingPlanReviewChecks.Run(
            PlayerAsset.Load(paths[0]),
            PlayerAsset.Load(paths[1]),
            BattingShotSet.Load(paths[2]),
            DeliveryPreset.Load(paths[3]),
            DeliveryPreset.Load(paths[4]),
            TeamRosterAsset.Load(paths[5]),
            TeamRosterAsset.Load(paths[6]),
            FieldPreset.Load(paths[7]));
    }
}

public sealed class CpuLiveRunningDecisionReviewCheckTests
{
    [Fact]
    public void RunningDecisionScenarios() =>
        CpuLiveRunningDecisionReviewChecks.Run(DeliveryPreset.Load(TestAssets.Asset("deliveries", "standard-pace.json")));
}

internal static class TestAssets
{
    private const string CpuBattingInputsEnvironmentVariable = "SUPERCRICKET_CPU_BATTING_INPUTS";

    public static string Asset(params string[] segments) => Path.Combine(FindRepositoryRoot(), "assets", Path.Combine(segments));

    public static string[] CpuBattingInputs()
    {
        var configuredInputs = Environment.GetEnvironmentVariable(CpuBattingInputsEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredInputs))
        {
            var paths = JsonSerializer.Deserialize<string[]>(configuredInputs);
            if (paths is { Length: 8 })
                return paths.Select(ResolvePath).ToArray();

            throw new InvalidOperationException($"{CpuBattingInputsEnvironmentVariable} must contain 8 asset paths.");
        }

        return
        [
            Asset("characters", "practice-batter-humanoid.glb"),
            Asset("characters", "practice-bowler-humanoid.glb"),
            Asset("batting", "shots.json"),
            Asset("deliveries", "standard-pace.json"),
            Asset("deliveries", "wide-pace.json"),
            Asset("teams", "coastal-xi.json"),
            Asset("teams", "highland-xi.json"),
            Asset("fields", "practice-attack.json")
        ];
    }

    public static string ResolvePath(string path) =>
        Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(FindRepositoryRoot(), path));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SuperCricket.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate SuperCricket.sln for the simulation test assets.");
    }
}
