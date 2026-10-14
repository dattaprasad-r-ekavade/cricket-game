namespace SuperCricket.Simulation;

/// <summary>Difficulty-controlled parameters used to size the batter's ideal timing band.</summary>
public sealed class TimingDifficulty
{
    public float IdealBase { get; set; } = 1f;
    public float IdealMinimum { get; set; } = 0.3f;
    public float LoftReduction { get; set; } = 0.25f;
    public List<TimingSettlePoint> SettleCurve { get; set; } =
    [
        new(0, 0.6f),
        new(8, 0.6f),
        new(20, 0.75f),
        new(36, 1f)
    ];

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (!float.IsFinite(IdealBase) || IdealBase <= 0f)
            errors.Add("Ideal base must be finite and positive.");
        if (!float.IsFinite(IdealMinimum) || IdealMinimum < 0f || IdealMinimum > IdealBase)
            errors.Add("Ideal minimum must be between zero and ideal base.");
        if (!float.IsFinite(LoftReduction) || LoftReduction is < 0f or > 1f)
            errors.Add("Loft reduction must be between zero and one.");
        if (SettleCurve is null || SettleCurve.Count < 2)
        {
            errors.Add("Settle curve must contain at least two points.");
            return errors;
        }
        if (SettleCurve[0].BallsFaced != 0)
            errors.Add("Settle curve must start at zero balls faced.");

        for (var index = 0; index < SettleCurve.Count; index++)
        {
            var point = SettleCurve[index];
            if (point.BallsFaced < 0 || index > 0 && point.BallsFaced <= SettleCurve[index - 1].BallsFaced)
                errors.Add("Settle curve ball counts must increase strictly.");
            if (!float.IsFinite(point.Fraction) || point.Fraction is < 0f or > 1f)
                errors.Add("Settle curve fractions must be between zero and one.");
            if (index > 0 && point.Fraction < SettleCurve[index - 1].Fraction)
                errors.Add("Settle curve fractions cannot decrease.");
        }

        return errors;
    }
}

public readonly record struct TimingSettlePoint(int BallsFaced, float Fraction);
