using SuperCricket.Content;

namespace SuperCricket.Content.Tests;

public sealed class DifficultyProfileAssetTests
{
    private static string DifficultyDirectory => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "../../../../../assets/difficulty"));

    [Fact]
    public void LoadDirectory_ContainsFiveLevelsForBothSides()
    {
        var catalog = DifficultyProfileCatalog.LoadDirectory(DifficultyDirectory);

        Assert.Equal(10, catalog.Profiles.Count);
        Assert.Equal(new[] { "rookie", "amateur", "standard", "pro", "legend" }, DifficultyProfileCatalog.LevelNames);
        Assert.Empty(DifficultyProfileCatalog.ValidateProfiles(catalog.Profiles));
        foreach (var level in DifficultyProfileCatalog.LevelNames)
        foreach (var side in new[] { DifficultySide.Human, DifficultySide.Cpu })
            Assert.Equal(level, catalog.GetProfile(level, side).Level);
    }

    [Fact]
    public void CpuProfilesStrengthenAsDifficultyIncreases()
    {
        var catalog = DifficultyProfileCatalog.LoadDirectory(DifficultyDirectory);
        var rookie = catalog.GetProfile("rookie", DifficultySide.Cpu).Cpu!;
        var standard = catalog.GetProfile("standard", DifficultySide.Cpu).Cpu!;
        var legend = catalog.GetProfile("legend", DifficultySide.Cpu).Cpu!;

        Assert.True(legend.StrokeWeights.Drive > rookie.StrokeWeights.Drive);
        Assert.True(legend.StrokeWeights.Loft > rookie.StrokeWeights.Loft);
        Assert.True(legend.CommitSigmaSeconds < rookie.CommitSigmaSeconds);
        Assert.True(legend.ScoringWeights.Fit > rookie.ScoringWeights.Fit);
        Assert.True(legend.ScoringWeights.Random < rookie.ScoringWeights.Random);
        Assert.Equal(-0.1f, standard.PacingAggressionOffset, 6);
        Assert.Equal(1f, rookie.StrokeWeights.Defence + rookie.StrokeWeights.Drive + rookie.StrokeWeights.Loft, 5);
        Assert.Equal(1f, legend.StrokeWeights.Defence + legend.StrokeWeights.Drive + legend.StrokeWeights.Loft, 5);
        Assert.Equal(1f, rookie.ScoringWeights.Fit + rookie.ScoringWeights.Intent + rookie.ScoringWeights.Reward + rookie.ScoringWeights.Random, 5);
        Assert.Equal(1f, legend.ScoringWeights.Fit + legend.ScoringWeights.Intent + legend.ScoringWeights.Reward + legend.ScoringWeights.Random, 5);
    }

    [Fact]
    public void Validate_RejectsInvalidTimingBandsAndMissingCpuDecisions()
    {
        var invalidTiming = new DifficultyProfileAsset { Timing = new DifficultyTimingProfile { WindowMeters = 0f } };
        Assert.Contains(invalidTiming.Validate(), message => message.Contains("Timing window", StringComparison.Ordinal));

        var missingCpu = new DifficultyProfileAsset { Side = DifficultySide.Cpu, Cpu = null };
        Assert.Contains(missingCpu.Validate(), message => message.Contains("CPU difficulty profile settings", StringComparison.Ordinal));

        var badWeights = new DifficultyProfileAsset
        {
            Side = DifficultySide.Cpu,
            Cpu = new CpuDecisionProfile
            {
                StrokeWeights = new CpuStrokeWeights { Defence = -1f, Drive = 0f, Loft = 0f },
                ScoringWeights = new CpuStrokeScoringWeights { Fit = 0f, Intent = 0f, Reward = 0f, Random = 0f }
            }
        };
        Assert.Contains(badWeights.Validate(), message => message.Contains("weights must be finite", StringComparison.Ordinal));

        var badPacing = new DifficultyProfileAsset
        {
            Side = DifficultySide.Cpu,
            Cpu = new CpuDecisionProfile { PacingAggressionOffset = 1.1f }
        };
        Assert.Contains(badPacing.Validate(), message => message.Contains("pacing aggression offset", StringComparison.Ordinal));
    }
}
