using System.Numerics;

namespace SuperCricket.Simulation;

public static class FieldingReviewChecks
{
    public static void Run()
    {
        var startingPositions = Enumerable.Range(0, FieldingSide.FielderCount)
            .Select(index => new Vector3(24f + index, -0.08f, 24f))
            .ToArray();
        startingPositions[0] = new Vector3(0f, -0.08f, 0f);
        var fielders = new FieldingSide();
        fielders.ConfigureStartingPositions(startingPositions);

        var lowCatchPrevious = Frame(-0.3f, 0.8f, 0f, bounceCount: 0);
        var lowCatchCurrent = Frame(0.3f, 0.8f, 0f, bounceCount: 0);
        Require(fielders.TryFindContact(lowCatchPrevious, lowCatchCurrent, out var lowCatch) &&
            lowCatch.Kind == FieldingContactKind.Catch && MathF.Abs(lowCatch.Position.X) < 0.001f,
            "a low airborne ball within reach was not caught at its swept contact point");

        var groundPickupPrevious = Frame(-0.2f, -0.04f, 0f, bounceCount: 1);
        var groundPickupCurrent = Frame(0.2f, -0.04f, 0f, bounceCount: 1);
        Require(fielders.TryFindContact(groundPickupPrevious, groundPickupCurrent, out var groundPickup) &&
            groundPickup.Kind == FieldingContactKind.GroundPickup,
            "a ball at the ground after a bounce was not resolved as a pickup");

        var target = new Vector3(10f, -0.08f, 0f);
        var defaultReach = FieldingSide.EstimateReachTime(Vector3.Zero, target);
        var averageReach = FieldingSide.EstimateReachTime(Vector3.Zero, target, 50);
        var lowSkillReach = FieldingSide.EstimateReachTime(Vector3.Zero, target, 0);
        var highSkillReach = FieldingSide.EstimateReachTime(Vector3.Zero, target, 100);
        Require(MathF.Abs(defaultReach - averageReach) < 0.0001f && highSkillReach < averageReach && averageReach < lowSkillReach,
            "fielding ratings did not preserve the neutral baseline or improve estimated reach time");

        var lowSkillFielders = new FieldingSide();
        lowSkillFielders.ConfigureStartingPositions(startingPositions);
        lowSkillFielders.ConfigureFieldingRatings(Enumerable.Repeat(0, FieldingSide.FielderCount).ToArray());
        var highSkillRatings = Enumerable.Repeat(0, FieldingSide.FielderCount).ToArray();
        highSkillRatings[0] = 100;
        var highSkillFielders = new FieldingSide();
        highSkillFielders.ConfigureStartingPositions(startingPositions);
        highSkillFielders.ConfigureFieldingRatings(highSkillRatings);
        var closeBall = Frame(0.8f, -0.04f, 0f, bounceCount: 1);
        Require(!lowSkillFielders.TryFindContact(closeBall, closeBall, out _) &&
            highSkillFielders.TryFindContact(closeBall, closeBall, out var skilledPickup) &&
            skilledPickup.Kind == FieldingContactKind.GroundPickup,
            "fielder skill did not change the reachable pickup radius");
        for (var frame = 0; frame < 4; frame++)
        {
            lowSkillFielders.Step(0.1f, new Vector3(6f, -0.08f, 0f));
            highSkillFielders.Step(0.1f, new Vector3(6f, -0.08f, 0f));
        }
        var lowSkillTravel = Vector3.Distance(startingPositions[0], lowSkillFielders.Positions[0]);
        var highSkillTravel = Vector3.Distance(startingPositions[0], highSkillFielders.Positions[0]);
        Require(highSkillTravel > lowSkillTravel,
            "higher fielding skill did not improve movement speed and reaction time");
        var invalidRatings = Enumerable.Repeat(50, FieldingSide.FielderCount).ToArray();
        invalidRatings[3] = 101;
        RequireThrows(() => lowSkillFielders.ConfigureFieldingRatings(invalidRatings),
            "an out-of-range fielding rating was accepted");

        var highPrevious = Frame(-0.3f, 2.6f, 0f, bounceCount: 0);
        var highCurrent = Frame(0.3f, 2.6f, 0f, bounceCount: 0);
        Require(!fielders.TryFindContact(highPrevious, highCurrent, out _),
            "an airborne ball above fielder reach was incorrectly intercepted");

        const float boundaryRadius = 42f;
        const float fieldSurface = -0.08f;
        const float ballRadius = 0.036f;
        var airborneStart = Frame(0f, 3f, boundaryRadius - 0.5f, bounceCount: 0);
        var airborneEnd = Frame(0f, 3f, boundaryRadius + 0.5f, bounceCount: 0);
        Require(BoundaryResolver.TryFindCrossing(airborneStart, airborneEnd, boundaryRadius, fieldSurface, ballRadius,
                out var airborneCrossing) && airborneCrossing.ClearedInTheAir && airborneCrossing.BoundaryRuns == 6 &&
            MathF.Abs(airborneCrossing.Position.Z - boundaryRadius) < 0.001f,
            "an airborne rope crossing did not resolve at the boundary as six");

        var ropeSkimStart = Frame(0f, fieldSurface + ballRadius, boundaryRadius - 0.5f, bounceCount: 1);
        var ropeSkimEnd = Frame(0f, fieldSurface + ballRadius, boundaryRadius + 0.5f, bounceCount: 1);
        Require(BoundaryResolver.TryFindCrossing(ropeSkimStart, ropeSkimEnd, boundaryRadius, fieldSurface, ballRadius,
                out var ropeSkim) && !ropeSkim.ClearedInTheAir && ropeSkim.BoundaryRuns == 4,
            "a rope skim after a bounce did not resolve as four");

        var highAfterBounce = Frame(0f, 3f, boundaryRadius + 0.5f, bounceCount: 1);
        Require(BoundaryResolver.TryFindCrossing(airborneStart, highAfterBounce, boundaryRadius, fieldSurface, ballRadius,
                out var bouncedCrossing) && !bouncedCrossing.ClearedInTheAir && bouncedCrossing.BoundaryRuns == 4,
            "a ball that bounced before crossing the rope was incorrectly scored as six");

        var insideEnd = Frame(0f, 3f, boundaryRadius - 0.1f, bounceCount: 0);
        Require(!BoundaryResolver.TryFindCrossing(airborneStart, insideEnd, boundaryRadius, fieldSurface, ballRadius, out _),
            "a ball that stayed inside the rope produced a boundary crossing");

        Console.WriteLine("PASS: low catches, ground pickups, rating-based fielding, and airborne/rope-skim boundary classification.");
    }

    private static BallFlightFrame Frame(float x, float y, float z, int bounceCount) =>
        new(0f, new Vector3(x, y, z), Vector3.Zero, bounceCount, BallMotionPhase.InFlight);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"Fielding check failed: {message}");
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
        throw new InvalidOperationException($"Fielding check failed: {message}");
    }
}
