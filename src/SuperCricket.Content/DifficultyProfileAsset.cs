using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuperCricket.Content;

public enum DifficultySide
{
    Human,
    Cpu
}

/// <summary>Rules and decision tuning for one side at one limited-overs difficulty.</summary>
public sealed class DifficultyProfileAsset
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    public int Version { get; set; } = 1;
    public string Format { get; set; } = "limited";
    public string Level { get; set; } = "standard";
    public DifficultySide Side { get; set; } = DifficultySide.Human;
    public DifficultyTimingProfile Timing { get; set; } = new();
    public float UmpiresCallProbability { get; set; } = 0.35f;
    public float BowlingTargetRadiusMinMeters { get; set; } = 0.12f;
    public float BowlingTargetRadiusMaxMeters { get; set; } = 1.1f;
    public int ScatterSteps { get; set; } = 4;
    public float MeterFillSeconds { get; set; } = 0.7f;
    public CpuDecisionProfile? Cpu { get; set; }

    public static DifficultyProfileAsset Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var profile = JsonSerializer.Deserialize<DifficultyProfileAsset>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"Difficulty profile '{path}' was empty.");
        var errors = profile.Validate();
        if (errors.Count > 0)
            throw new InvalidDataException($"Invalid difficulty profile '{path}': {string.Join(" ", errors)}");
        return profile;
    }

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (Version != 1) errors.Add("Difficulty profile version must be 1.");
        if (!string.Equals(Format, "limited", StringComparison.OrdinalIgnoreCase))
            errors.Add("Difficulty profile format must be 'limited'.");
        if (string.IsNullOrWhiteSpace(Level) || !DifficultyProfileCatalog.LevelNames.Contains(Level, StringComparer.OrdinalIgnoreCase))
            errors.Add($"Difficulty level '{Level}' is not one of the five supported levels.");
        if (!Enum.IsDefined(Side)) errors.Add("Difficulty profile side must be human or cpu.");
        if (Timing is null)
            errors.Add("Difficulty profile timing settings are required.");
        else
            errors.AddRange(Timing.Validate());
        if (!float.IsFinite(UmpiresCallProbability) || UmpiresCallProbability is < 0f or > 1f)
            errors.Add("Umpire's-call probability must be between 0 and 1.");
        if (!float.IsFinite(BowlingTargetRadiusMinMeters) || !float.IsFinite(BowlingTargetRadiusMaxMeters) ||
            BowlingTargetRadiusMinMeters < 0f || BowlingTargetRadiusMaxMeters < BowlingTargetRadiusMinMeters ||
            BowlingTargetRadiusMaxMeters > 5f)
            errors.Add("Bowling target radius bounds must be ordered between 0 and 5 m.");
        if (ScatterSteps is < 0 or > 12) errors.Add("Bowling scatter steps must be between 0 and 12.");
        if (!float.IsFinite(MeterFillSeconds) || MeterFillSeconds is < 0.1f or > 2f)
            errors.Add("Pace meter fill time must be between 0.1 and 2 seconds.");
        if (Side == DifficultySide.Cpu)
        {
            if (Cpu is null)
                errors.Add("CPU difficulty profile settings are required for the cpu side.");
            else
                errors.AddRange(Cpu.Validate());
        }
        else if (Cpu is not null)
            errors.Add("CPU decision settings belong only in a cpu-side profile.");
        return errors;
    }
}

public sealed class DifficultyTimingProfile
{
    public float CentreMeters { get; set; }
    public float WindowMeters { get; set; } = 8f;
    public float IdealBase { get; set; } = 1f;
    public float IdealMinimum { get; set; } = 0.3f;
    public float LoftReduction { get; set; } = 0.25f;
    public List<DifficultySettlePoint> SettleCurve { get; set; } =
    [new(0, 0.6f), new(8, 0.6f), new(20, 0.75f), new(36, 1f)];
    public bool AssistEnabled { get; set; } = true;

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (!float.IsFinite(CentreMeters)) errors.Add("Timing centre must be finite.");
        if (!float.IsFinite(WindowMeters) || WindowMeters <= 0f) errors.Add("Timing window must be finite and positive.");
        if (!float.IsFinite(IdealBase) || IdealBase <= 0f || IdealBase > WindowMeters)
            errors.Add("Ideal timing base must be positive and no wider than the timing window.");
        if (!float.IsFinite(IdealMinimum) || IdealMinimum < 0f || IdealMinimum > IdealBase)
            errors.Add("Ideal timing minimum must be between zero and ideal base.");
        if (!float.IsFinite(LoftReduction) || LoftReduction is < 0f or > 1f)
            errors.Add("Timing loft reduction must be between zero and one.");
        if (SettleCurve is null || SettleCurve.Count < 2)
        {
            errors.Add("Timing settle curve must contain at least two points.");
            return errors;
        }
        if (SettleCurve[0].BallsFaced != 0) errors.Add("Timing settle curve must start at zero balls faced.");
        for (var index = 0; index < SettleCurve.Count; index++)
        {
            var point = SettleCurve[index];
            if (point.BallsFaced < 0 || index > 0 && point.BallsFaced <= SettleCurve[index - 1].BallsFaced)
                errors.Add("Timing settle curve ball counts must increase strictly.");
            if (!float.IsFinite(point.Fraction) || point.Fraction is < 0f or > 1f)
                errors.Add("Timing settle curve fractions must be between zero and one.");
            if (index > 0 && point.Fraction < SettleCurve[index - 1].Fraction)
                errors.Add("Timing settle curve fractions cannot decrease.");
        }
        return errors;
    }
}

public readonly record struct DifficultySettlePoint(int BallsFaced, float Fraction);

public sealed class CpuDecisionProfile
{
    public CpuStrokeWeights StrokeWeights { get; set; } = new();
    public float CommitSigmaSeconds { get; set; } = 0.1f;
    public float BattingTimingErrorMultiplier { get; set; } = 1f;
    public float ShotPlacementErrorMultiplier { get; set; } = 1f;
    public float BowlingAccuracySpreadMultiplier { get; set; } = 1f;
    public float BattingAggressionOffset { get; set; }
    public float BattingWicketChanceMultiplier { get; set; } = 1f;
    public float BattingBoundaryChanceMultiplier { get; set; } = 1f;
    public float BattingRunChanceMultiplier { get; set; } = 1f;
    public float BattingRunOutChanceMultiplier { get; set; } = 1f;

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (StrokeWeights is null)
            errors.Add("CPU stroke weights are required.");
        else
            errors.AddRange(StrokeWeights.Validate());
        if (!float.IsFinite(CommitSigmaSeconds) || CommitSigmaSeconds is < 0f or > 1f)
            errors.Add("CPU commit sigma must be between 0 and 1 second.");
        CheckRange(errors, BattingTimingErrorMultiplier, 0f, 5f, "CPU batting timing-error multiplier");
        CheckRange(errors, ShotPlacementErrorMultiplier, 0f, 5f, "CPU shot-placement multiplier");
        CheckRange(errors, BowlingAccuracySpreadMultiplier, 0f, 5f, "CPU bowling-accuracy spread multiplier");
        CheckRange(errors, BattingAggressionOffset, -1f, 1f, "CPU batting aggression offset");
        CheckRange(errors, BattingWicketChanceMultiplier, 0f, 5f, "CPU wicket-chance multiplier");
        CheckRange(errors, BattingBoundaryChanceMultiplier, 0f, 5f, "CPU boundary-chance multiplier");
        CheckRange(errors, BattingRunChanceMultiplier, 0f, 5f, "CPU run-chance multiplier");
        CheckRange(errors, BattingRunOutChanceMultiplier, 0f, 5f, "CPU run-out-chance multiplier");
        return errors;
    }

    private static void CheckRange(List<string> errors, float value, float min, float max, string label)
    {
        if (!float.IsFinite(value) || value < min || value > max)
            errors.Add($"{label} must be between {min} and {max}.");
    }
}

public sealed class CpuStrokeWeights
{
    public float Defence { get; set; } = 0.33f;
    public float Drive { get; set; } = 0.45f;
    public float Loft { get; set; } = 0.22f;

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (!float.IsFinite(Defence) || Defence < 0f ||
            !float.IsFinite(Drive) || Drive < 0f ||
            !float.IsFinite(Loft) || Loft < 0f ||
            Defence + Drive + Loft <= 0f)
            errors.Add("CPU defence, drive, and loft weights must be finite, non-negative, and have a positive sum.");
        return errors;
    }
}
