using System.Numerics;
using SuperCricket.Content;
using SuperCricket.Content.Bowling;

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

public readonly record struct FieldChoiceSituation(
    int LegalBalls,
    int OversPerInnings,
    int Runs,
    int Wickets,
    int? Target,
    int? LastWicketLegalBall,
    int LastOverBoundaryCount);

public readonly record struct AuthoredFieldPlacement(
    string Name,
    FieldingIntent Intent,
    FieldingTactic Tactic,
    IReadOnlyList<Vector3> StartingPositions,
    Vector3 BowlerPosition);

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
        var deepFielderRadius = DeepFielderRadiusMeters * (preset.BoundaryRadiusMeters / 42f);
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
                FieldingTactic.ProtectBoundary when radius >= deepFielderRadius =>
                    MathF.Min(maximumRadius, radius + 2.5f),
                FieldingTactic.AttackWickets when radius < deepFielderRadius =>
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

    public static FieldingIntent ChooseIntent(FieldChoiceSituation situation)
    {
        ValidateChoiceSituation(situation);
        var recentWicket = situation.LastWicketLegalBall is { } wicketBall &&
            situation.LegalBalls >= wicketBall && situation.LegalBalls - wicketBall <= 12;
        if (recentWicket)
            return FieldingIntent.Attack;

        var ballsRemaining = Math.Max(1,
            situation.OversPerInnings * OverScoreboard.BallsPerOver - situation.LegalBalls);
        var requiredRate = situation.Target is { } target
            ? Math.Max(0, target - situation.Runs) * OverScoreboard.BallsPerOver / (float)ballsRemaining
            : 0f;
        return requiredRate >= 10f || situation.LastOverBoundaryCount >= 2
            ? FieldingIntent.Defend
            : FieldingIntent.Standard;
    }

    public static AuthoredFieldPlacement Resolve(
        FieldFormationCatalog catalog,
        BowlerStyle style,
        FieldingIntent intent,
        BatterHandedness handedness,
        bool powerplayActive = false)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var errors = catalog.Validate(limitedOversRestrictionsEnabled: true, powerplayActive: powerplayActive);
        if (errors.Count > 0)
            throw new ArgumentException($"Invalid field formations: {string.Join(" ", errors)}", nameof(catalog));
        var formation = catalog.Get(style, intent);
        var positions = formation.Positions
            .Select(name => catalog.Positions.Resolve(name, handedness))
            .ToArray();
        var bowlerPosition = catalog.Positions.Resolve(formation.BowlerPosition, handedness);
        return new AuthoredFieldPlacement(
            formation.Name,
            intent,
            intent switch
            {
                FieldingIntent.Attack => FieldingTactic.AttackWickets,
                FieldingIntent.Defend => FieldingTactic.ProtectBoundary,
                _ => FieldingTactic.Balanced
            },
            Array.AsReadOnly(positions),
            bowlerPosition);
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
        if (situation.OversPerInnings is < 1 or > 20)
            return "Overs per innings must be between 1 and 20.";
        if (situation.LegalBalls < 0 || situation.LegalBalls >= situation.OversPerInnings * OverScoreboard.BallsPerOver)
            return "Legal balls must be within an unfinished innings.";
        if (situation.Runs < 0 || situation.Wickets is < 0 or >= OverScoreboard.MaximumWickets)
            return "Runs and wickets must describe an unfinished innings.";
        if (situation.Target is <= 0)
            return "A chase target must be positive when present.";
        return null;
    }

    private static void ValidateChoiceSituation(FieldChoiceSituation situation)
    {
        if (situation.OversPerInnings is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(situation), "Overs per innings must be between 1 and 20.");
        var maximumBalls = situation.OversPerInnings * OverScoreboard.BallsPerOver;
        if (situation.LegalBalls < 0 || situation.LegalBalls > maximumBalls)
            throw new ArgumentOutOfRangeException(nameof(situation), "Legal balls must be within the innings.");
        if (situation.Runs < 0 || situation.Wickets is < 0 or > OverScoreboard.MaximumWickets)
            throw new ArgumentOutOfRangeException(nameof(situation), "Runs and wickets must describe an innings.");
        if (situation.Target is <= 0)
            throw new ArgumentOutOfRangeException(nameof(situation), "A chase target must be positive when present.");
        if (situation.LastWicketLegalBall is { } wicketBall &&
            (wicketBall < 0 || wicketBall > situation.LegalBalls))
            throw new ArgumentOutOfRangeException(nameof(situation), "The last wicket ball cannot be after the current ball count.");
        if (situation.LastOverBoundaryCount < 0)
            throw new ArgumentOutOfRangeException(nameof(situation), "The last over boundary count cannot be negative.");
    }
}
