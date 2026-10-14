using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SuperCricket.Simulation;

namespace SuperCricket.Content;

/// <summary>Authored per-stroke outcomes, keyed by timing zone.</summary>
public sealed class StrokeTableAsset
{
    private const string DefaultAnimationContractPath = "assets/characters/practice-batter.animation-contract.json";
    private const string WarningPrefix = "Warning:";
    private static readonly TimingZone[] RequiredZones =
    [
        TimingZone.Start,
        TimingZone.Early,
        TimingZone.Ideal,
        TimingZone.Late,
        TimingZone.End
    ];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private string? _animationContractPath;

    public int Version { get; set; } = 1;
    public List<StrokeDefinition> Strokes { get; set; } = [];

    public static StrokeTableAsset Load(string path)
    {
        var json = File.ReadAllText(path);
        var table = JsonSerializer.Deserialize<StrokeTableAsset>(json, JsonOptions)
            ?? throw new InvalidDataException($"Stroke table '{path}' was empty.");
        table._animationContractPath = ResolveAnimationContractPath(path);
        var errors = table.Validate();
        var failures = errors.Where(error => !error.StartsWith(WarningPrefix, StringComparison.Ordinal)).ToArray();
        if (failures.Length > 0)
            throw new InvalidDataException($"Invalid stroke table '{path}': {string.Join(" ", failures)}");
        return table;
    }

    public List<string> Validate() => Validate(_animationContractPath ?? DefaultAnimationContractPath);

    public List<string> Validate(string animationContractPath)
    {
        var errors = new List<string>();
        if (Version != 1)
            errors.Add("Stroke table version must be 1.");
        if (Strokes is null || Strokes.Count < 40)
        {
            errors.Add("At least 40 strokes are required.");
            if (Strokes is null)
                return errors;
        }

        var animationNames = LoadAnimationNames(animationContractPath, errors);
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var availableCombinations = new HashSet<(ShotDirection8 Direction, StrokeIntent Intent, StrokeFoot Foot)>();
        foreach (var stroke in Strokes)
        {
            if (stroke is null)
            {
                errors.Add("A stroke definition is missing.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(stroke.Id) || !ids.Add(stroke.Id))
                errors.Add("Stroke IDs must be unique and non-empty.");
            if (!Enum.IsDefined(stroke.Direction))
                errors.Add($"Stroke '{stroke.Id}' has an unknown direction.");
            if (!Enum.IsDefined(stroke.Intent))
                errors.Add($"Stroke '{stroke.Id}' has an unknown intent.");
            if (!Enum.IsDefined(stroke.Foot) || stroke.Foot == StrokeFoot.Auto)
                errors.Add($"Stroke '{stroke.Id}' foot must be Front or Back.");
            if (Enum.IsDefined(stroke.Direction) && Enum.IsDefined(stroke.Intent) &&
                Enum.IsDefined(stroke.Foot) && stroke.Foot != StrokeFoot.Auto)
                availableCombinations.Add((stroke.Direction, stroke.Intent, stroke.Foot));

            if (stroke.Aggression is < 0 or > 2)
                errors.Add($"Stroke '{stroke.Id}' aggression must be 0, 1, or 2.");
            if (!IsUnitInterval(stroke.Difficulty))
                errors.Add($"Stroke '{stroke.Id}' difficulty must be between 0 and 1.");
            if (!IsUnitInterval(stroke.Reward))
                errors.Add($"Stroke '{stroke.Id}' reward must be between 0 and 1.");
            if (string.IsNullOrWhiteSpace(stroke.AnimationClip) || !animationNames.Contains(stroke.AnimationClip))
                errors.Add($"Stroke '{stroke.Id}' animation clip '{stroke.AnimationClip}' is not in the batter animation contract.");

            ValidateSuits(stroke, errors);
            ValidateZones(stroke, errors);
            AddCricketFitWarning(stroke, errors);
        }

        foreach (var direction in Enum.GetValues<ShotDirection8>())
        foreach (var intent in new[] { StrokeIntent.Defend, StrokeIntent.Attack })
        foreach (var foot in new[] { StrokeFoot.Front, StrokeFoot.Back })
        {
            if (!availableCombinations.Contains((direction, intent, foot)))
                errors.Add($"No stroke covers {direction}/{intent}/{foot}.");
        }

        return errors;
    }

    public StrokeTableAsset Mirror()
    {
        return new StrokeTableAsset
        {
            Version = Version,
            _animationContractPath = _animationContractPath,
            Strokes = Strokes.Select(stroke => new StrokeDefinition
            {
                Id = stroke.Id,
                Direction = stroke.Direction,
                Intent = stroke.Intent,
                Foot = stroke.Foot,
                Loft = stroke.Loft,
                AnimationClip = stroke.AnimationClip,
                Aggression = stroke.Aggression,
                Difficulty = stroke.Difficulty,
                Reward = stroke.Reward,
                Suits = new StrokeSuit
                {
                    Lengths = [.. stroke.Suits.Lengths],
                    Lines = [.. stroke.Suits.Lines]
                },
                Zones = stroke.Zones.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value with { YawDegrees = -pair.Value.YawDegrees })
            }).ToList()
        };
    }

    public static bool IsWarning(string message) => message.StartsWith(WarningPrefix, StringComparison.Ordinal);

    private static HashSet<string> LoadAnimationNames(string path, List<string> errors)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("animations", out var animations) ||
                animations.ValueKind != JsonValueKind.Array)
            {
                errors.Add("Batter animation contract must contain an animations array.");
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return animations.EnumerateArray()
                .Where(animation => animation.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                .Select(animation => animation.GetProperty("name").GetString()!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            errors.Add($"Could not read batter animation contract '{path}': {exception.Message}");
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string ResolveAnimationContractPath(string strokeTablePath)
    {
        var tableDirectory = Path.GetDirectoryName(Path.GetFullPath(strokeTablePath)) ?? Directory.GetCurrentDirectory();
        var assetsDirectory = Directory.GetParent(tableDirectory)?.FullName ?? Directory.GetCurrentDirectory();
        return Path.Combine(assetsDirectory, "characters", "practice-batter.animation-contract.json");
    }

    private static void ValidateSuits(StrokeDefinition stroke, List<string> errors)
    {
        if (stroke.Suits is null || stroke.Suits.Lengths is null || stroke.Suits.Lines is null ||
            stroke.Suits.Lengths.Count == 0 || stroke.Suits.Lines.Count == 0)
        {
            errors.Add($"Stroke '{stroke.Id}' must suit at least one length and line.");
            return;
        }

        if (stroke.Suits.Lengths.Any(length => !Enum.IsDefined(length)) ||
            stroke.Suits.Lines.Any(line => !Enum.IsDefined(line)))
            errors.Add($"Stroke '{stroke.Id}' has an unknown ball length or line in Suits.");
    }

    private static void ValidateZones(StrokeDefinition stroke, List<string> errors)
    {
        if (stroke.Zones is null)
        {
            errors.Add($"Stroke '{stroke.Id}' has no timing zones.");
            return;
        }

        foreach (var zone in RequiredZones)
        {
            if (!stroke.Zones.ContainsKey(zone))
                errors.Add($"Stroke '{stroke.Id}' is missing its {zone} zone.");
        }
        if (stroke.Zones.Keys.Any(zone => !RequiredZones.Contains(zone)))
            errors.Add($"Stroke '{stroke.Id}' must leave TooEarly and TooLate to the global beaten result.");

        foreach (var (zone, data) in stroke.Zones)
        {
            if (data is null)
            {
                errors.Add($"Stroke '{stroke.Id}' has no data for {zone}.");
                continue;
            }
            if (!float.IsFinite(data.YawDegrees))
                errors.Add($"Stroke '{stroke.Id}' {zone} yaw must be finite.");
            if (!float.IsFinite(data.YawSpreadDegrees) || data.YawSpreadDegrees is < 0f or > 45f)
                errors.Add($"Stroke '{stroke.Id}' {zone} yaw spread must be between 0 and 45 degrees.");
            if (!float.IsFinite(data.ElevationDegrees) || data.ElevationDegrees is < -10f or > 70f)
                errors.Add($"Stroke '{stroke.Id}' {zone} elevation must be between -10 and 70 degrees.");
            if (!float.IsFinite(data.MinSpeedMetersPerSecond) || !float.IsFinite(data.MaxSpeedMetersPerSecond) ||
                data.MinSpeedMetersPerSecond < 0f || data.MinSpeedMetersPerSecond > data.MaxSpeedMetersPerSecond)
                errors.Add($"Stroke '{stroke.Id}' {zone} speed range must have finite, non-negative minimum and maximum values with min <= max.");
            if (!IsUnitInterval(data.EdgeChance))
                errors.Add($"Stroke '{stroke.Id}' {zone} edge chance must be between 0 and 1.");
        }

        if (stroke.Zones.TryGetValue(TimingZone.Ideal, out var ideal) && ideal is not null && float.IsFinite(ideal.MaxSpeedMetersPerSecond))
        {
            foreach (var (zone, data) in stroke.Zones)
            {
                if (zone != TimingZone.Ideal && data is not null && float.IsFinite(data.MaxSpeedMetersPerSecond) &&
                    ideal.MaxSpeedMetersPerSecond < data.MaxSpeedMetersPerSecond)
                    errors.Add($"Stroke '{stroke.Id}' Ideal max speed must be at least the {zone} max speed.");
            }
        }
    }

    private static void AddCricketFitWarning(StrokeDefinition stroke, List<string> errors)
    {
        if (stroke.Suits?.Lines is null)
            return;

        var offSide = stroke.Direction is ShotDirection8.OffDrive or ShotDirection8.Cover or ShotDirection8.Square or ShotDirection8.Fine;
        var legSide = stroke.Direction is ShotDirection8.LegFine or ShotDirection8.LegSquare or ShotDirection8.MidWicket;
        if (offSide && stroke.Suits.Lines.Any(line => line is BallLine.Leg or BallLine.WideLeg))
            errors.Add($"{WarningPrefix} Stroke '{stroke.Id}' pairs an off-side direction with a leg-side line.");
        if (legSide && stroke.Suits.Lines.Contains(BallLine.WideOutsideOff))
            errors.Add($"{WarningPrefix} Stroke '{stroke.Id}' pairs a leg-side direction with a wide outside-off line.");
    }

    private static bool IsUnitInterval(float value) => float.IsFinite(value) && value is >= 0f and <= 1f;
}

public sealed class StrokeDefinition
{
    public string Id { get; set; } = string.Empty;
    public ShotDirection8 Direction { get; set; }
    public StrokeIntent Intent { get; set; }
    public StrokeFoot Foot { get; set; }
    public bool Loft { get; set; }
    public string AnimationClip { get; set; } = string.Empty;
    public int Aggression { get; set; }
    public float Difficulty { get; set; }
    public float Reward { get; set; }
    public StrokeSuit Suits { get; set; } = new();
    public Dictionary<TimingZone, ZoneData> Zones { get; set; } = [];
}

public sealed class StrokeSuit
{
    public List<BallLength> Lengths { get; set; } = [];
    public List<BallLine> Lines { get; set; } = [];
}

public sealed record ZoneData(
    float YawDegrees,
    float YawSpreadDegrees,
    float ElevationDegrees,
    float MinSpeedMetersPerSecond,
    float MaxSpeedMetersPerSecond,
    float EdgeChance,
    bool BeatenOrMissed = false);
