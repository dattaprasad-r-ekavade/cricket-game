using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuperCricket.Content;

/// <summary>Editable batting intents shared by the game and local tooling.</summary>
public sealed class BattingShotSet
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public float ContactPaddingMeters { get; set; } = 0.03f;
    public List<BattingShotData> Shots { get; set; } = [];

    public static BattingShotSet Load(string path)
    {
        var json = File.ReadAllText(path);
        var set = JsonSerializer.Deserialize<BattingShotSet>(json, JsonOptions)
            ?? throw new InvalidDataException($"Batting shot set '{path}' was empty.");
        var errors = set.Validate();
        if (errors.Count > 0)
            throw new InvalidDataException($"Invalid batting shot set '{path}': {string.Join(" ", errors)}");
        return set;
    }

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (!float.IsFinite(ContactPaddingMeters) || ContactPaddingMeters is < 0f or > 0.25f)
            errors.Add("Contact padding must be between 0 and 0.25 m.");
        if (Shots is null || Shots.Count < 3)
        {
            errors.Add("At least three batting shots are required.");
            return errors;
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var shot in Shots)
        {
            if (shot is null)
            {
                errors.Add("A batting shot is missing.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(shot.Name) || !names.Add(shot.Name))
                errors.Add("Shot names must be unique and non-empty.");
            if (string.IsNullOrWhiteSpace(shot.AnimationClip))
                errors.Add($"Shot '{shot.Name}' must name an animation clip.");
            if (!float.IsFinite(shot.LaunchAngleDegrees) || shot.LaunchAngleDegrees is < -5f or > 70f)
                errors.Add($"Shot '{shot.Name}' launch angle must be between -5 and 70 degrees.");
            if (!float.IsFinite(shot.HorizontalAim) || shot.HorizontalAim is < -1f or > 1f)
                errors.Add($"Shot '{shot.Name}' horizontal aim must be between -1 and 1.");
            if (!float.IsFinite(shot.SpeedTransfer) || shot.SpeedTransfer is <= 0f or > 1.5f)
                errors.Add($"Shot '{shot.Name}' speed transfer must be greater than 0 and at most 1.5.");
        }

        return errors;
    }

    public BattingShotData Get(string name) => Shots.First(shot => string.Equals(shot.Name, name, StringComparison.OrdinalIgnoreCase));
}

public sealed class BattingShotData
{
    public string Name { get; set; } = string.Empty;
    public string AnimationClip { get; set; } = string.Empty;
    public float LaunchAngleDegrees { get; set; }
    public float HorizontalAim { get; set; }
    public float SpeedTransfer { get; set; }
}
