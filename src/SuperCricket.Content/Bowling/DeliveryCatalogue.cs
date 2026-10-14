using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuperCricket.Content.Bowling;

public enum BowlerStyle
{
    Pace,
    OffSpin,
    LegSpin
}

public enum PitchType
{
    Green,
    Normal,
    Hard,
    Dusty,
    Damp,
    Soft
}

/// <summary>Authored delivery families and their pace and movement ranges.</summary>
public sealed class DeliveryCatalogue
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public int Version { get; set; } = 1;
    public List<DeliveryDefinition> Deliveries { get; set; } = [];

    public static DeliveryCatalogue Load(string path)
    {
        var catalogue = JsonSerializer.Deserialize<DeliveryCatalogue>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"Delivery catalogue '{path}' was empty.");
        var errors = catalogue.Validate();
        if (errors.Count > 0)
            throw new InvalidDataException($"Invalid delivery catalogue '{path}': {string.Join(" ", errors)}");
        return catalogue;
    }

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (Version != 1) errors.Add("Delivery catalogue version must be 1.");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var delivery in Deliveries ?? [])
        {
            if (delivery is null) { errors.Add("A delivery definition is missing."); continue; }
            if (string.IsNullOrWhiteSpace(delivery.Id) || !ids.Add(delivery.Id))
                errors.Add("Delivery IDs must be unique and non-empty.");
            if (!Enum.IsDefined(delivery.Style)) errors.Add($"Delivery '{delivery.Id}' has an unknown bowler style.");
            if (string.IsNullOrWhiteSpace(delivery.Name)) errors.Add($"Delivery '{delivery.Id}' needs a name.");
            if (!float.IsFinite(delivery.MinSpeedKph) || !float.IsFinite(delivery.MaxSpeedKph) ||
                delivery.MinSpeedKph < 30f || delivery.MaxSpeedKph > 160f || delivery.MinSpeedKph > delivery.MaxSpeedKph)
                errors.Add($"Delivery '{delivery.Id}' speed range must be ordered and between 30 and 160 km/h.");
            if (!float.IsFinite(delivery.SwingAcceleration) || MathF.Abs(delivery.SwingAcceleration) > 8f ||
                !float.IsFinite(delivery.SeamAcceleration) || MathF.Abs(delivery.SeamAcceleration) > 8f)
                errors.Add($"Delivery '{delivery.Id}' swing and seam values must be between -8 and 8 m/s².");
            if (!float.IsFinite(delivery.SideSpin) || MathF.Abs(delivery.SideSpin) > 2f ||
                !float.IsFinite(delivery.TopSpin) || MathF.Abs(delivery.TopSpin) > 2f)
                errors.Add($"Delivery '{delivery.Id}' spin values must be between -2 and 2.");
            if (delivery.Style == BowlerStyle.Pace && delivery.PitchSpinMultipliers is { Count: > 0 })
                errors.Add($"Pace delivery '{delivery.Id}' must not define pitch spin multipliers.");
            if (delivery.Style != BowlerStyle.Pace)
            {
                foreach (var pitchType in Enum.GetValues<PitchType>())
                {
                    if (delivery.PitchSpinMultipliers is null || !delivery.PitchSpinMultipliers.TryGetValue(pitchType, out var multiplier) ||
                        !float.IsFinite(multiplier) || multiplier is < 0f or > 4f)
                        errors.Add($"Delivery '{delivery.Id}' needs a spin multiplier from 0 to 4 for {pitchType} pitches.");
                }
                if (delivery.SideSpin == 0f && delivery.TopSpin == 0f)
                    errors.Add($"Spin delivery '{delivery.Id}' needs side-spin or top-spin movement.");
            }
        }

        foreach (var style in Enum.GetValues<BowlerStyle>())
        {
            if (!(Deliveries ?? []).Any(delivery => delivery is not null && delivery.Style == style))
                errors.Add($"The catalogue must contain at least one {style} delivery.");
        }
        return errors;
    }
}

public sealed class DeliveryDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public BowlerStyle Style { get; set; }
    public float MinSpeedKph { get; set; }
    public float MaxSpeedKph { get; set; }
    public float SwingAcceleration { get; set; }
    public float SeamAcceleration { get; set; }
    public float SideSpin { get; set; }
    public float TopSpin { get; set; }
    public Dictionary<PitchType, float> PitchSpinMultipliers { get; set; } = [];
}
