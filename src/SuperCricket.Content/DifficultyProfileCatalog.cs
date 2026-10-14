using System.IO;

namespace SuperCricket.Content;

/// <summary>Loads and indexes the five human/CPU profiles for limited-overs cricket.</summary>
public sealed class DifficultyProfileCatalog
{
    public static IReadOnlyList<string> LevelNames { get; } = Array.AsReadOnly(
        new[] { "rookie", "amateur", "standard", "pro", "legend" });

    private readonly Dictionary<(string Format, string Level, DifficultySide Side), DifficultyProfileAsset> _profiles;

    private DifficultyProfileCatalog(IEnumerable<DifficultyProfileAsset> profiles)
    {
        _profiles = new Dictionary<(string Format, string Level, DifficultySide Side), DifficultyProfileAsset>();
        foreach (var profile in profiles)
        {
            var key = (Format: (profile.Format ?? string.Empty).ToLowerInvariant(),
                Level: (profile.Level ?? string.Empty).ToLowerInvariant(), Side: profile.Side);
            if (!_profiles.TryAdd(key, profile))
                throw new InvalidDataException($"Duplicate difficulty profile for {key.Format}/{key.Level}/{key.Side}.");
        }
    }

    public IReadOnlyCollection<DifficultyProfileAsset> Profiles => _profiles.Values;

    public static DifficultyProfileCatalog LoadDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Difficulty profile directory '{directory}' does not exist.");
        var files = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
        if (files.Length == 0)
            throw new InvalidDataException($"Difficulty profile directory '{directory}' contains no JSON files.");

        var profiles = files.Select(DifficultyProfileAsset.Load).ToArray();
        var errors = ValidateProfiles(profiles);
        if (errors.Count > 0)
            throw new InvalidDataException($"Invalid difficulty profile catalog: {string.Join(" ", errors)}");
        return new DifficultyProfileCatalog(profiles);
    }

    public DifficultyProfileAsset GetProfile(string level, DifficultySide side, string format = "limited")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(level);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        return _profiles.TryGetValue((format.ToLowerInvariant(), level.ToLowerInvariant(), side), out var profile)
            ? profile
            : throw new KeyNotFoundException($"No {format}/{level}/{side} difficulty profile is loaded.");
    }

    public static List<string> ValidateProfiles(IEnumerable<DifficultyProfileAsset> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        var errors = new List<string>();
        var seen = new HashSet<(string Format, string Level, DifficultySide Side)>();
        foreach (var profile in profiles)
        {
            if (profile is null)
            {
                errors.Add("A difficulty profile is missing.");
                continue;
            }
            errors.AddRange(profile.Validate());
            var key = (Format: (profile.Format ?? string.Empty).ToLowerInvariant(),
                Level: (profile.Level ?? string.Empty).ToLowerInvariant(), Side: profile.Side);
            if (!seen.Add(key)) errors.Add($"Difficulty profile {key.Format}/{key.Level}/{key.Side} is duplicated.");
        }

        foreach (var level in LevelNames)
        foreach (var side in new[] { DifficultySide.Human, DifficultySide.Cpu })
        {
            if (!seen.Contains(("limited", level, side)))
                errors.Add($"Missing limited/{level}/{side} difficulty profile.");
        }
        foreach (var key in seen)
        {
            if (!string.Equals(key.Format, "limited", StringComparison.OrdinalIgnoreCase))
                errors.Add($"Unsupported difficulty profile format '{key.Format}'.");
        }
        return errors;
    }
}
