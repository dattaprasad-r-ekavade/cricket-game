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
}
