using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Numerics;

namespace SuperCricket.Simulation;

/// <summary>Editable, serializable values for one repeatable bowling delivery.</summary>
public sealed class DeliveryPreset
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public string Name { get; set; } = "Standard pace";
    public Vector3Data ReleasePosition { get; set; } = new();
    public Vector3Data ReleaseVelocity { get; set; } = new();
    public float GravityMetersPerSecondSquared { get; set; } = 9.81f;
    public float AirDragPerMeter { get; set; } = 0.00025f;
    public float LateralAccelerationMetersPerSecondSquared { get; set; }
    public float BallRadiusMeters { get; set; } = 0.036f;
    public float PitchSurfaceHeightMeters { get; set; } = -0.025f;
    public float FieldSurfaceHeightMeters { get; set; } = -0.08f;
    public float PitchWidthMeters { get; set; } = 3.05f;
    public float PitchLengthMeters { get; set; } = 20.12f;
    public float PitchBounceRestitution { get; set; } = 0.53f;
    public float GroundBounceRestitution { get; set; } = 0.38f;
    public float TangentialRetention { get; set; } = 0.96f;
    public float RollingDecelerationMetersPerSecondSquared { get; set; } = 1.6f;
    public float FixedTimeStepSeconds { get; set; } = 1f / 120f;
    public float MaximumSimulationSeconds { get; set; } = 3.5f;
    public float FieldBoundaryRadiusMeters { get; set; } = 42f;

    [JsonIgnore]
    public Vector3 StartPosition => ReleasePosition.ToVector3();

    [JsonIgnore]
    public Vector3 StartVelocity => ReleaseVelocity.ToVector3();

    public static DeliveryPreset Load(string path)
    {
        var json = File.ReadAllText(path);
        var preset = JsonSerializer.Deserialize<DeliveryPreset>(json, JsonOptions)
            ?? throw new InvalidDataException($"Delivery preset '{path}' was empty.");
        var errors = preset.Validate();
        if (errors.Count > 0)
        {
            throw new InvalidDataException($"Invalid delivery preset '{path}': {string.Join(" ", errors)}");
        }

        return preset;
    }

    public void Save(string path)
    {
        var errors = Validate();
        if (errors.Count > 0)
        {
            throw new InvalidDataException($"Cannot save invalid delivery preset: {string.Join(" ", errors)}");
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Name)) errors.Add("Name must not be empty.");
        if (ReleasePosition is null || !ReleasePosition.IsFinite()) errors.Add("Release position must contain finite x, y, and z values.");
        if (ReleaseVelocity is null || !ReleaseVelocity.IsFinite()) errors.Add("Release velocity must contain finite x, y, and z values.");
        if (ReleasePosition is not null && ReleasePosition.Y <= PitchSurfaceHeightMeters + BallRadiusMeters)
            errors.Add("Release position must be above the pitch.");
        if (ReleaseVelocity is not null)
        {
            var releaseSpeed = ReleaseVelocity.ToVector3().Length();
            if (!float.IsFinite(releaseSpeed) || releaseSpeed is < 1f or > 80f)
                errors.Add("Release speed must be between 1 and 80 m/s.");
        }
        if (!IsPositive(GravityMetersPerSecondSquared)) errors.Add("Gravity must be positive.");
        if (!IsNonNegative(AirDragPerMeter) || AirDragPerMeter > 0.1f) errors.Add("Air drag must be between 0 and 0.1 per meter.");
        if (!float.IsFinite(LateralAccelerationMetersPerSecondSquared) || MathF.Abs(LateralAccelerationMetersPerSecondSquared) > 100f)
            errors.Add("Lateral acceleration must be between -100 and 100 m/s².");
        if (!IsPositive(BallRadiusMeters) || BallRadiusMeters is < 0.01f or > 0.2f)
            errors.Add("Ball radius must be between 0.01 and 0.2 m.");
        if (!float.IsFinite(PitchSurfaceHeightMeters) || !float.IsFinite(FieldSurfaceHeightMeters))
            errors.Add("Surface heights must be finite.");
        if (!IsPositive(PitchWidthMeters) || !IsPositive(PitchLengthMeters)) errors.Add("Pitch dimensions must be positive.");
        if (!IsUnitInterval(PitchBounceRestitution) || !IsUnitInterval(GroundBounceRestitution))
            errors.Add("Bounce restitution values must be between 0 and 1.");
        if (!IsUnitInterval(TangentialRetention)) errors.Add("Tangential retention must be between 0 and 1.");
        if (!IsPositive(RollingDecelerationMetersPerSecondSquared)) errors.Add("Rolling deceleration must be positive.");
        if (!IsPositive(FixedTimeStepSeconds) || FixedTimeStepSeconds > 0.05f)
            errors.Add("Fixed time step must be greater than zero and at most 0.05 seconds.");
        if (!IsPositive(MaximumSimulationSeconds) || MaximumSimulationSeconds > 30f)
            errors.Add("Maximum simulation time must be greater than zero and at most 30 seconds.");
        if (!IsPositive(FieldBoundaryRadiusMeters)) errors.Add("Field boundary radius must be positive.");
        return errors;
    }

    private static bool IsPositive(float value) => float.IsFinite(value) && value > 0f;
    private static bool IsNonNegative(float value) => float.IsFinite(value) && value >= 0f;
    private static bool IsUnitInterval(float value) => float.IsFinite(value) && value is >= 0f and <= 1f;
}
