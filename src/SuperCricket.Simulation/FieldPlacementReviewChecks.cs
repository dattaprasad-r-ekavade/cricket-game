using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Simulation;

public static class FieldPlacementReviewChecks
{
    public static void Run()
    {
        var preset = CreatePracticePreset();
        var balancedSituation = new BowlingSituation(0, 2, 0, 0, Target: null);
        var balanced = FieldPlacementModel.Choose(preset, balancedSituation, strikerPower: 55);
        Require(balanced.Tactic == FieldingTactic.Balanced && balanced.StartingPositions.Count == FieldingSide.FielderCount,
            "a neutral match did not preserve the saved field formation");

        var powerThreat = FieldPlacementModel.Choose(preset, balancedSituation, strikerPower: 90);
        var chasePressure = FieldPlacementModel.Choose(preset,
            new BowlingSituation(0, 2, 0, 0, Target: 10), strikerPower: 55);
        Require(powerThreat.Tactic == FieldingTactic.ProtectBoundary &&
            chasePressure.Tactic == FieldingTactic.ProtectBoundary,
            "the field did not react to a power hitter or high chase requirement");

        var attackingSituation = new BowlingSituation(0, 2, 0, 8, Target: 1);
        var attacking = FieldPlacementModel.Choose(preset, attackingSituation, strikerPower: 50);
        Require(attacking.Tactic == FieldingTactic.AttackWickets,
            "the field did not close in for a late-innings wicket opportunity");

        var movedDeepFielders = 0;
        var movedCloseFielders = 0;
        for (var index = 0; index < preset.Players.Count; index++)
        {
            var original = preset.Players[index].Position.ToVector3();
            var balancedPosition = balanced.StartingPositions[index];
            var defensivePosition = powerThreat.StartingPositions[index];
            var attackingPosition = attacking.StartingPositions[index];
            Require(balancedPosition == original,
                "the balanced field changed a saved position");
            Require(MathF.Abs(defensivePosition.Y - original.Y) < 0.0001f &&
                MathF.Abs(attackingPosition.Y - original.Y) < 0.0001f,
                "a fielding tactic moved a player off the field surface");
            Require(defensivePosition.LengthSquared() <=
                MathF.Pow(preset.BoundaryRadiusMeters - FieldPlacementModel.BoundarySafetyMarginMeters, 2f) + 0.0001f,
                "a defensive fielder was placed outside the boundary safety margin");

            var originalRadius = HorizontalRadius(original);
            if (preset.Players[index].IsWicketkeeper)
            {
                Require(defensivePosition == original && attackingPosition == original,
                    "a tactical adjustment moved the wicketkeeper from the wicket");
            }
            else if (originalRadius >= FieldPlacementModel.DeepFielderRadiusMeters)
            {
                if (HorizontalRadius(defensivePosition) > originalRadius)
                    movedDeepFielders++;
                Require(attackingPosition == original,
                    "an attacking setup moved a deep boundary fielder");
            }
            else
            {
                Require(defensivePosition == original,
                    "a boundary-protection setup moved a close catcher");
                if (HorizontalRadius(attackingPosition) < originalRadius)
                    movedCloseFielders++;
            }
        }
        Require(movedDeepFielders >= 4 && movedCloseFielders >= 3,
            "the selected tactics did not move the expected deep or close fielders");
        Require(preset.Players[4].Position.ToVector3() == new Vector3(-20f, -0.08f, -7f),
            "field placement mutated the reusable authored preset");
        RequireThrows(() => FieldPlacementModel.Choose(preset, balancedSituation, strikerPower: 101),
            "an out-of-range batter power rating was accepted");
        RequireThrows(() => FieldPlacementModel.Choose(preset,
                balancedSituation with { LegalBalls = 12 }, strikerPower: 55),
            "a field plan was accepted for a completed innings");

        Console.WriteLine("PASS: adaptive field tactics, boundary safety, wicketkeeper role, and saved formation preservation.");
    }

    private static FieldPreset CreatePracticePreset() => new()
    {
        Version = 1,
        Name = "Review attacking ring",
        BoundaryRadiusMeters = 42f,
        Players =
        [
            new() { Name = "First slip", Position = Vector3Data.From(new Vector3(-3.2f, -0.08f, -16f)) },
            new() { Name = "Second slip", Position = Vector3Data.From(new Vector3(3.2f, -0.08f, -17.5f)) },
            new() { Name = "Gully", Position = Vector3Data.From(new Vector3(-10f, -0.08f, -18f)) },
            new() { Name = "Short cover", Position = Vector3Data.From(new Vector3(0f, -0.08f, -20f)) },
            new() { Name = "Deep point", Position = Vector3Data.From(new Vector3(-20f, -0.08f, -7f)) },
            new() { Name = "Cover", Position = Vector3Data.From(new Vector3(2.25f, -0.08f, -24f)) },
            new() { Name = "Long leg", Position = Vector3Data.From(new Vector3(-17f, -0.08f, 16f)) },
            new() { Name = "Deep midwicket", Position = Vector3Data.From(new Vector3(16f, -0.08f, 16f)) },
            new() { Name = "Long off", Position = Vector3Data.From(new Vector3(0f, -0.08f, -31f)) },
            new() { Name = "Wicketkeeper", IsWicketkeeper = true, Position = Vector3Data.From(new Vector3(0f, -0.08f, -11.2f)) }
        ]
    };

    private static float HorizontalRadius(Vector3 position) => MathF.Sqrt(position.X * position.X + position.Z * position.Z);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"Field placement check failed: {message}");
    }

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }
        throw new InvalidOperationException($"Field placement check failed: {message}");
    }
}
