using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuperCricket.Content;

/// <summary>Editable starting positions for the ten fielders apart from the bowler.</summary>
public sealed class FieldPreset
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public int Version { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public float BoundaryRadiusMeters { get; set; } = 42f;
    public List<FieldPositionData> Players { get; set; } = [];

    public static FieldPreset Load(string path)
    {
        var json = File.ReadAllText(path);
        var preset = JsonSerializer.Deserialize<FieldPreset>(json, JsonOptions)
            ?? throw new InvalidDataException($"Field preset '{path}' was empty.");
        var errors = preset.Validate();
        if (errors.Count > 0)
            throw new InvalidDataException($"Invalid field preset '{path}': {string.Join(" ", errors)}");
        return preset;
    }

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (Version != 1) errors.Add($"Unsupported field preset version {Version}; expected 1.");
        if (string.IsNullOrWhiteSpace(Name)) errors.Add("Field preset name must not be empty.");
        if (!float.IsFinite(BoundaryRadiusMeters) || BoundaryRadiusMeters is < 20f or > 100f)
            errors.Add("Boundary radius must be between 20 and 100 m.");

        if (Players is null || Players.Count != 10)
        {
            errors.Add("A field preset must contain ten fielders (the bowler is placed separately).");
            return errors;
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var wicketkeepers = 0;
        for (var index = 0; index < Players.Count; index++)
        {
            var player = Players[index];
            if (player is null)
            {
                errors.Add($"Fielder {index + 1} is missing.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(player.Name) || !names.Add(player.Name))
                errors.Add($"Fielder {index + 1} must have a unique, non-empty position name.");
            if (player.Position is null || !player.Position.IsFinite())
            {
                errors.Add($"Fielder '{player.Name}' must have a finite position.");
                continue;
            }

            if (player.IsWicketkeeper)
                wicketkeepers++;
            var position = player.Position.ToVector3();
            var distanceSquared = position.X * position.X + position.Z * position.Z;
            if (distanceSquared > BoundaryRadiusMeters * BoundaryRadiusMeters)
                errors.Add($"Fielder '{player.Name}' is outside the field boundary.");
            if (MathF.Abs(position.Y + 0.08f) > 1f)
                errors.Add($"Fielder '{player.Name}' must be placed near the field surface at y = -0.08 m.");
        }

        if (wicketkeepers != 1)
            errors.Add("A field preset must mark exactly one wicketkeeper.");
        return errors;
    }
}

public sealed class FieldPositionData
{
    public string Name { get; set; } = string.Empty;
    public bool IsWicketkeeper { get; set; }
    public Vector3Data Position { get; set; } = new();
}
