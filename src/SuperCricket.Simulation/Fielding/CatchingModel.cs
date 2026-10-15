namespace SuperCricket.Simulation.Fielding;

/// <summary>Resolves a seeded catch attempt using the fielder's catching rating.</summary>
public static class CatchingModel
{
    public static float GetHoldChance(int catchingRating)
    {
        if (catchingRating is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(catchingRating), "Catching ratings must be between 0 and 100.");
        return 0.5f + catchingRating * 0.005f;
    }

    public static bool IsHeld(int catchingRating, int seed) => new Random(seed).NextDouble() < GetHoldChance(catchingRating);

    public static float GetHoldChance(int catchingRating, float reactionDistanceMeters, float ballHeightAboveGroundMeters)
    {
        if (catchingRating is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(catchingRating), "Catching ratings must be between 0 and 100.");
        if (!float.IsFinite(reactionDistanceMeters) || reactionDistanceMeters < 0f ||
            !float.IsFinite(ballHeightAboveGroundMeters) || ballHeightAboveGroundMeters < 0f)
            throw new ArgumentOutOfRangeException(nameof(reactionDistanceMeters), "Catch distance and ball height must be finite and non-negative.");

        var skillChance = 0.5f + catchingRating * 0.004f;
        var reactionChance = Math.Clamp(1f - reactionDistanceMeters * 0.012f, 0.45f, 1f);
        var heightChance = ballHeightAboveGroundMeters switch
        {
            < 1.05f => Math.Clamp(0.3333333f +
                (ballHeightAboveGroundMeters - 0.72f) * (0.6666667f / 0.33f), 0.3333333f, 1f),
            <= 1.8f => 1f,
            _ => Math.Clamp(1f - (ballHeightAboveGroundMeters - 1.8f) * 0.55f, 0.55f, 1f)
        };
        return Math.Clamp(skillChance * reactionChance * heightChance, 0f, 1f);
    }

    public static bool IsHeld(int catchingRating, float reactionDistanceMeters, float ballHeightAboveGroundMeters, int seed) =>
        new Random(seed).NextDouble() < GetHoldChance(catchingRating, reactionDistanceMeters, ballHeightAboveGroundMeters);
}
