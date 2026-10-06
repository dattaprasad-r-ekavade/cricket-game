using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Simulation;

public enum FieldingTactic
{
    Balanced,
    ProtectBoundary,
    AttackWickets
}

public readonly record struct FieldPlacementDecision(
    FieldingTactic Tactic,
    IReadOnlyList<Vector3> StartingPositions);

/// <summary>Adjusts a saved field to the current batter and chase situation.</summary>
public static class FieldPlacementModel
{
    public const float DeepFielderRadiusMeters = 20.5f;
    public const float BoundarySafetyMarginMeters = 1.5f;

    public static FieldPlacementDecision Choose(
        FieldPreset preset,
        BowlingSituation situation,
        int strikerPower)
    {
        ArgumentNullException.ThrowIfNull(preset);
        var presetErrors = preset.Validate();
        if (presetErrors.Count > 0)
            throw new ArgumentException($"Invalid field preset: {string.Join(" ", presetErrors)}", nameof(preset));

        var tactic = ChooseTactic(situation, strikerPower);

        var positions = new Vector3[preset.Players.Count];
        var maximumRadius = preset.BoundaryRadiusMeters - BoundarySafetyMarginMeters;
        for (var index = 0; index < preset.Players.Count; index++)
        {
            var player = preset.Players[index];
            var position = player.Position.ToVector3();
            var radius = MathF.Sqrt(position.X * position.X + position.Z * position.Z);
            if (player.IsWicketkeeper || radius <= 0.0001f)
            {
                positions[index] = position;
                continue;
            }

            var adjustedRadius = tactic switch
            {
                FieldingTactic.ProtectBoundary when radius >= DeepFielderRadiusMeters =>
                    MathF.Min(maximumRadius, radius + 2.5f),
                FieldingTactic.AttackWickets when radius < DeepFielderRadiusMeters =>
                    MathF.Max(3.5f, radius - 1.5f),
                _ => radius
            };
            var scale = adjustedRadius / radius;
            positions[index] = new Vector3(position.X * scale, position.Y, position.Z * scale);
        }

        return new FieldPlacementDecision(tactic, Array.AsReadOnly(positions));
    }

    public static FieldingTactic ChooseTactic(BowlingSituation situation, int strikerPower)
    {
        if (strikerPower is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(strikerPower), "Batting power must be between 0 and 100.");
        var situationError = ValidateSituation(situation);
        if (situationError is not null)
            throw new ArgumentOutOfRangeException(nameof(situation), situationError);

        var pressure = GetPressure(situation);
        return strikerPower >= 75 || pressure >= 0.75f
            ? FieldingTactic.ProtectBoundary
            : situation.Wickets >= 7 && pressure <= 0.30f
                ? FieldingTactic.AttackWickets
                : FieldingTactic.Balanced;
    }

    private static float GetPressure(BowlingSituation situation)
    {
        if (situation.Target is not { } target)
            return 0f;
        var ballsRemaining = Math.Max(1, situation.OversPerInnings * OverScoreboard.BallsPerOver - situation.LegalBalls);
        return Math.Clamp((target - situation.Runs) / (float)ballsRemaining, 0f, 1f);
    }

    private static string? ValidateSituation(BowlingSituation situation)
    {
        if (situation.OversPerInnings is < 1 or > 10)
            return "Overs per innings must be between 1 and 10.";
        if (situation.LegalBalls < 0 || situation.LegalBalls >= situation.OversPerInnings * OverScoreboard.BallsPerOver)
            return "Legal balls must be within an unfinished innings.";
        if (situation.Runs < 0 || situation.Wickets is < 0 or >= OverScoreboard.MaximumWickets)
            return "Runs and wickets must describe an unfinished innings.";
        if (situation.Target is <= 0)
            return "A chase target must be positive when present.";
        return null;
    }
}
