namespace SuperCricket.Simulation;

public enum DeliveryExtra
{
    None,
    Wide,
    NoBall,
    Bye,
    LegBye
}

public enum DismissalKind
{
    None,
    Bowled,
    Caught,
    RunOut
}

public enum DismissedEnd
{
    Striker,
    NonStriker
}

/// <summary>Cricket scoring and legality facts resolved for one delivery.</summary>
public readonly record struct DeliveryResult(
    int BatterRuns,
    int ExtraRuns,
    int CompletedRuns,
    bool IsLegal = true,
    DeliveryExtra Extra = DeliveryExtra.None,
    DismissalKind Dismissal = DismissalKind.None,
    DismissedEnd DismissedEnd = DismissedEnd.Striker,
    bool SwapEndsOnRunOut = false)
{
    public string? Validate()
    {
        if (BatterRuns is < 0 or > 12) return "Batter runs must be between zero and twelve.";
        if (ExtraRuns < 0) return "Extra runs cannot be negative.";
        if (CompletedRuns < 0) return "Completed runs cannot be negative.";
        if (SwapEndsOnRunOut && Dismissal != DismissalKind.RunOut)
            return "An uncompleted crossing may change ends only on a run-out.";
        if (!Enum.IsDefined(Extra) || !Enum.IsDefined(Dismissal) || !Enum.IsDefined(DismissedEnd))
            return "Delivery contains an unknown scoring or dismissal type.";
        if (CompletedRuns > BatterRuns + ExtraRuns)
            return "Completed runs cannot exceed the total runs scored from the delivery.";
        if (Extra is DeliveryExtra.Wide or DeliveryExtra.NoBall)
        {
            if (IsLegal) return "A wide or no-ball cannot count as a legal delivery.";
            if (ExtraRuns < 1) return "A wide or no-ball must include its one-run penalty.";
        }
        else if (!IsLegal)
        {
            return "Only a wide or no-ball may be an illegal delivery.";
        }
        if (Extra == DeliveryExtra.None && ExtraRuns != 0)
            return "Extra runs require an extra type.";
        if (Extra is DeliveryExtra.Bye or DeliveryExtra.LegBye && BatterRuns != 0)
            return "Byes and leg-byes cannot be credited as batter runs.";
        if (Extra == DeliveryExtra.Wide && BatterRuns != 0)
            return "Wide runs cannot be credited as batter runs.";
        if (Dismissal is DismissalKind.Bowled or DismissalKind.Caught && !IsLegal)
            return "A batter cannot be bowled or caught from a wide or no-ball.";
        if (Dismissal == DismissalKind.Bowled && (BatterRuns != 0 || ExtraRuns != 0))
            return "A bowled dismissal cannot score runs.";
        return null;
    }
}
