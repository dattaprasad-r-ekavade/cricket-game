using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Numerics;

namespace SuperCricket.Content;

/// <summary>Named field positions authored as right-handed polar coordinates around the pitch.</summary>
public sealed class FieldPositionCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public int Version { get; set; } = 1;
    public float BoundaryRadiusMeters { get; set; } = 62.5f;
    public List<FieldPositionDefinition> Positions { get; set; } = [];

    public static FieldPositionCatalog Load(string path)
    {
        var catalog = JsonSerializer.Deserialize<FieldPositionCatalog>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"Field position catalogue '{path}' was empty.");
        var errors = catalog.Validate();
        if (errors.Count > 0)
            throw new InvalidDataException($"Invalid field position catalogue '{path}': {string.Join(" ", errors)}");
        return catalog;
    }

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (Version != 1) errors.Add($"Unsupported field position catalogue version {Version}; expected 1.");
        if (!float.IsFinite(BoundaryRadiusMeters) || BoundaryRadiusMeters is < 20f or > 100f)
            errors.Add("Field position boundary radius must be between 20 and 100 m.");
        if (Positions is null || Positions.Count < 11)
        {
            errors.Add("The position catalogue must contain at least eleven named positions.");
            return errors;
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var position in Positions)
        {
            if (position is null || string.IsNullOrWhiteSpace(position.Name) || !names.Add(position.Name))
            {
                errors.Add("Field position names must be unique and non-empty.");
                continue;
            }
            if (!float.IsFinite(position.AngleDegrees) || position.AngleDegrees is < -180f or > 180f)
                errors.Add($"Field position '{position.Name}' angle must be between -180 and 180 degrees.");
            if (!float.IsFinite(position.RadiusMeters) || position.RadiusMeters < 0f ||
                position.RadiusMeters > BoundaryRadiusMeters)
                errors.Add($"Field position '{position.Name}' radius must be inside the field boundary.");
        }

        foreach (var required in new[] { "Wicketkeeper", "Bowler" })
        {
            if (!names.Contains(required))
                errors.Add($"The position catalogue is missing required position '{required}'.");
        }
        return errors;
    }

    public FieldPositionDefinition Get(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Positions.SingleOrDefault(position =>
                   string.Equals(position.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Unknown field position '{name}'.");
    }

    public Vector3 Resolve(string name, BatterHandedness handedness)
    {
        var position = Get(name);
        return position.ToVector3(handedness);
    }
}

public sealed class FieldPositionDefinition
{
    public string Name { get; set; } = string.Empty;
    /// <summary>Zero points toward the bowler; positive angles turn toward the right-hander's leg side.</summary>
    public float AngleDegrees { get; set; }
    public float RadiusMeters { get; set; }

    public Vector3 ToVector3(BatterHandedness handedness)
    {
        if (!Enum.IsDefined(handedness))
            throw new ArgumentOutOfRangeException(nameof(handedness));
        var angle = AngleDegrees * (MathF.PI / 180f);
        var mirror = handedness == BatterHandedness.Left ? -1f : 1f;
        return new Vector3(
            MathF.Sin(angle) * RadiusMeters * mirror,
            -0.08f,
            -MathF.Cos(angle) * RadiusMeters);
    }
}
