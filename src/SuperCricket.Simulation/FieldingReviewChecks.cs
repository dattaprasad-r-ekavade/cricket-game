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

        Console.WriteLine("PASS: low catches, ground pickups, and airborne/rope-skim boundary classification.");
    }

    private static BallFlightFrame Frame(float x, float y, float z, int bounceCount) =>
        new(0f, new Vector3(x, y, z), Vector3.Zero, bounceCount, BallMotionPhase.InFlight);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"Fielding check failed: {message}");
    }
}
