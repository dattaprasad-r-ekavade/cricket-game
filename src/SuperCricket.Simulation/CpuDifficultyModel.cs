using SuperCricket.Content;

namespace SuperCricket.Simulation;

public enum CpuDifficulty
{
    Rookie,
    Amateur,
    Standard,
    Pro,
    Legend
}

public readonly record struct CpuDifficultyTuning(
    float BattingTimingErrorMultiplier,
    float ShotPlacementErrorMultiplier,
    float BowlingAccuracySpreadMultiplier,
    float BattingAggressionOffset,
    float BattingWicketChanceMultiplier,
    float BattingBoundaryChanceMultiplier,
    float BattingRunChanceMultiplier,
    float BattingRunOutChanceMultiplier);

/// <summary>Applies difficulty through opponent decision quality while leaving ball and scoring rules fixed.</summary>
public static class CpuDifficultyModel
{
    private static readonly CpuDifficulty[] OrderedDifficulties =
    [
        CpuDifficulty.Rookie,
        CpuDifficulty.Amateur,
        CpuDifficulty.Standard,
        CpuDifficulty.Pro,
        CpuDifficulty.Legend
    ];
    private static readonly object ProfilesLock = new();
    private static DifficultyProfileCatalog? _profiles;

    public static IReadOnlyList<CpuDifficulty> Difficulties => Array.AsReadOnly(OrderedDifficulties);

    public static DifficultyProfileAsset GetProfile(CpuDifficulty difficulty, DifficultySide side = DifficultySide.Cpu)
    {
        if (!Enum.IsDefined(difficulty))
            throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Unknown CPU difficulty.");
        if (!Enum.IsDefined(side))
            throw new ArgumentOutOfRangeException(nameof(side));
        return GetProfiles().GetProfile(difficulty.ToString(), side);
    }

    public static CpuDifficultyTuning GetTuning(CpuDifficulty difficulty)
    {
        var cpu = GetProfile(difficulty).Cpu
            ?? throw new InvalidOperationException($"The {difficulty} CPU profile has no CPU decision settings.");
        return new CpuDifficultyTuning(
            cpu.BattingTimingErrorMultiplier,
            cpu.ShotPlacementErrorMultiplier,
            cpu.BowlingAccuracySpreadMultiplier,
            cpu.BattingAggressionOffset,
            cpu.BattingWicketChanceMultiplier,
            cpu.BattingBoundaryChanceMultiplier,
            cpu.BattingRunChanceMultiplier,
            cpu.BattingRunOutChanceMultiplier);
    }

    public static void ConfigureProfiles(DifficultyProfileCatalog profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        lock (ProfilesLock)
            _profiles = profiles;
    }

    public static CpuDifficulty Next(CpuDifficulty difficulty)
    {
        var index = Array.IndexOf(OrderedDifficulties, difficulty);
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Unknown CPU difficulty.");
        return OrderedDifficulties[(index + 1) % OrderedDifficulties.Length];
    }

    private static DifficultyProfileCatalog GetProfiles()
    {
        if (_profiles is not null) return _profiles;
        lock (ProfilesLock)
        {
            if (_profiles is not null) return _profiles;
            var roots = new[] { AppContext.BaseDirectory, Environment.CurrentDirectory }
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var root in roots)
            {
                var current = new DirectoryInfo(root);
                while (current is not null)
                {
                    foreach (var directoryName in new[] { "Assets/Difficulty", "assets/difficulty" })
                    {
                        var candidate = Path.Combine(current.FullName, directoryName);
                        if (Directory.Exists(candidate))
                            return _profiles = DifficultyProfileCatalog.LoadDirectory(candidate);
                    }
                    current = current.Parent;
                }
            }
        }
        throw new DirectoryNotFoundException("Could not locate assets/difficulty. Load and configure a DifficultyProfileCatalog before using CPU difficulty tuning.");
    }
}
