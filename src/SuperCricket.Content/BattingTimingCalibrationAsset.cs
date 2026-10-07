using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuperCricket.Content;

/// <summary>Measured ideal shot-input delays for the authored batter clips and delivery presets.</summary>
public sealed class BattingTimingCalibrationAsset
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public float OnTimeWindowSeconds { get; set; } = 0.075f;
    public List<BattingTimingDeliveryProfile> DeliveryProfiles { get; set; } = [];

    public static BattingTimingCalibrationAsset Load(string path)
    {
        var calibration = JsonSerializer.Deserialize<BattingTimingCalibrationAsset>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"Batting timing calibration '{path}' was empty.");
        var errors = calibration.Validate();
        if (errors.Count > 0)
            throw new InvalidDataException($"Invalid batting timing calibration '{path}': {string.Join(" ", errors)}");
        return calibration;
    }

    public float? FindIdealInputDelaySeconds(string deliveryName, string shotName)
    {
        var profile = DeliveryProfiles.Find(candidate =>
            string.Equals(candidate.DeliveryName, deliveryName, StringComparison.OrdinalIgnoreCase));
        return profile?.FindIdealInputDelaySeconds(shotName);
    }

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (!float.IsFinite(OnTimeWindowSeconds) || OnTimeWindowSeconds is < 0.025f or > 0.25f)
            errors.Add("On-time window must be between 25 and 250 milliseconds.");
        if (DeliveryProfiles is null || DeliveryProfiles.Count == 0)
        {
            errors.Add("At least one delivery timing profile is required.");
            return errors;
        }

        var deliveryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in DeliveryProfiles)
        {
            if (profile is null)
            {
                errors.Add("A delivery timing profile is missing.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(profile.DeliveryName) || !deliveryNames.Add(profile.DeliveryName))
                errors.Add("Delivery profile names must be unique and non-empty.");
            if (profile.Shots is null || profile.Shots.Count == 0)
            {
                errors.Add($"Delivery profile '{profile.DeliveryName}' needs at least one shot calibration.");
                continue;
            }

            var shotNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var shot in profile.Shots)
            {
                if (shot is null)
                {
                    errors.Add($"Delivery profile '{profile.DeliveryName}' contains a missing shot calibration.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(shot.ShotName) || !shotNames.Add(shot.ShotName))
                    errors.Add($"Shot calibration names in '{profile.DeliveryName}' must be unique and non-empty.");
                if (!float.IsFinite(shot.IdealInputDelaySeconds) ||
                    shot.IdealInputDelaySeconds is < -0.5f or > 2.5f)
                    errors.Add($"Ideal input delay for '{profile.DeliveryName}/{shot.ShotName}' is outside -0.5 to 2.5 seconds.");
            }
        }

        return errors;
    }
}

public sealed class BattingTimingDeliveryProfile
{
    public string DeliveryName { get; set; } = string.Empty;
    public List<BattingShotTimingCalibration> Shots { get; set; } = [];

    public float? FindIdealInputDelaySeconds(string shotName) => Shots.Find(candidate =>
        string.Equals(candidate.ShotName, shotName, StringComparison.OrdinalIgnoreCase))?.IdealInputDelaySeconds;
}

public sealed class BattingShotTimingCalibration
{
    public string ShotName { get; set; } = string.Empty;
    public float IdealInputDelaySeconds { get; set; }
}
