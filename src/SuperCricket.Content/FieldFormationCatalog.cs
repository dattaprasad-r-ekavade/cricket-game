using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SuperCricket.Content.Bowling;

namespace SuperCricket.Content;

public enum FieldingIntent
{
    Attack,
    Standard,
    Defend
}

/// <summary>Loads the nine style/intent field formations and their named-position references.</summary>
public sealed class FieldFormationCatalog
{
    public const float InnerRingRadiusMeters = 27.43f;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public FieldPositionCatalog Positions { get; private set; } = new();
    public List<FieldFormationPreset> Formations { get; private set; } = [];

    public static FieldFormationCatalog Load(string positionsPath, string presetsDirectory)
    {
        var catalog = new FieldFormationCatalog
        {
            Positions = FieldPositionCatalog.Load(positionsPath),
            Formations = Directory.EnumerateFiles(presetsDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => JsonSerializer.Deserialize<FieldFormationPreset>(File.ReadAllText(path), JsonOptions)
                    ?? throw new InvalidDataException($"Field formation '{path}' was empty."))
                .ToList()
        };
        var errors = catalog.Validate(limitedOversRestrictionsEnabled: true, powerplayActive: false);
        errors.AddRange(catalog.Validate(limitedOversRestrictionsEnabled: true, powerplayActive: true));
        if (errors.Count > 0)
            throw new InvalidDataException($"Invalid field formation catalogue: {string.Join(" ", errors)}");
        return catalog;
    }

    public List<string> Validate(bool limitedOversRestrictionsEnabled = true, bool powerplayActive = false)
    {
        var errors = Positions.Validate();
        if (Formations is null || Formations.Count != 9)
        {
            errors.Add("The field formation catalogue must contain exactly nine presets.");
            return errors;
        }

        var combinations = new HashSet<(BowlerStyle Style, FieldingIntent Intent)>();
        foreach (var formation in Formations)
        {
            if (formation is null)
            {
                errors.Add("A field formation is missing.");
                continue;
            }
            errors.AddRange(formation.Validate(Positions, limitedOversRestrictionsEnabled, powerplayActive));
            if (!combinations.Add((formation.Style, formation.Intent)))
                errors.Add($"Duplicate {formation.Style}/{formation.Intent} field formation.");
        }

        foreach (var style in Enum.GetValues<BowlerStyle>())
        foreach (var intent in Enum.GetValues<FieldingIntent>())
        {
            if (!combinations.Contains((style, intent)))
                errors.Add($"Missing {style}/{intent} field formation.");
        }
        return errors;
    }

    public FieldFormationPreset Get(BowlerStyle style, FieldingIntent intent)
    {
        return Formations.SingleOrDefault(formation => formation.Style == style && formation.Intent == intent)
            ?? throw new KeyNotFoundException($"No {style}/{intent} field formation exists.");
    }

    public IReadOnlyList<FieldFormationPreset> ForStyle(BowlerStyle style) => Formations
        .Where(formation => formation.Style == style)
        .OrderBy(formation => formation.Intent)
        .ToArray();
}

public sealed class FieldFormationPreset
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public BowlerStyle Style { get; set; }
    public FieldingIntent Intent { get; set; }
    public List<string> Positions { get; set; } = [];
    public string BowlerPosition { get; set; } = "Bowler";

    public List<string> Validate(
        FieldPositionCatalog catalog,
        bool limitedOversRestrictionsEnabled = true,
        bool powerplayActive = false)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var errors = new List<string>();
        if (Version != 1) errors.Add($"Unsupported field formation version {Version}; expected 1.");
        if (string.IsNullOrWhiteSpace(Name)) errors.Add("Field formation name must not be empty.");
        if (!Enum.IsDefined(Style)) errors.Add("Field formation has an unknown bowler style.");
        if (!Enum.IsDefined(Intent)) errors.Add("Field formation has an unknown intent.");
        if (Positions is null || Positions.Count != 10)
        {
            errors.Add("A field formation must place nine fielders and one wicketkeeper.");
            return errors;
        }
        if (string.IsNullOrWhiteSpace(BowlerPosition) ||
            !string.Equals(BowlerPosition, "Bowler", StringComparison.OrdinalIgnoreCase))
            errors.Add("A field formation must reference the Bowler position separately.");

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var legSideCount = 0;
        var outsideRingCount = 0;
        foreach (var name in Positions.Append(BowlerPosition))
        {
            if (string.IsNullOrWhiteSpace(name) || !names.Add(name))
            {
                errors.Add($"Field formation '{Name}' must reference each position once.");
                continue;
            }
            FieldPositionDefinition position;
            try
            {
                position = catalog.Get(name);
            }
            catch (KeyNotFoundException)
            {
                errors.Add($"Field formation '{Name}' references unknown position '{name}'.");
                continue;
            }

            var resolved = position.ToVector3(BatterHandedness.Right);
            if (resolved.X > 0.01f)
                legSideCount++;
            if (position.RadiusMeters > FieldFormationCatalog.InnerRingRadiusMeters)
                outsideRingCount++;
        }

        if (!names.Contains("Wicketkeeper"))
            errors.Add($"Field formation '{Name}' must include the wicketkeeper.");
        if (limitedOversRestrictionsEnabled && legSideCount > 5)
            errors.Add($"Field formation '{Name}' has {legSideCount} players on the leg side; the limit is five.");
        if (limitedOversRestrictionsEnabled && powerplayActive && outsideRingCount > 2)
            errors.Add($"Field formation '{Name}' has {outsideRingCount} players outside the inner ring during the powerplay; the limit is two.");
        return errors;
    }
}
