namespace SuperCricket.Simulation;

/// <summary>Scoring, strike, and over progression for one limited-overs innings.</summary>
public sealed class OverScoreboard
{
    public const int BallsPerOver = 6;
    public const int MaximumWickets = 10;

    private readonly int _oversPerInnings;

    public OverScoreboard(int oversPerInnings = 1)
    {
        if (oversPerInnings is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(oversPerInnings), "An innings must have between one and ten overs.");
        _oversPerInnings = oversPerInnings;
    }

    public int OversPerInnings => _oversPerInnings;
    public int Runs { get; private set; }
    public int Wickets { get; private set; }
    public int LegalBalls { get; private set; }
    public int Striker { get; private set; } = 1;
    public int NonStriker { get; private set; } = 2;
    public int NextBatter { get; private set; } = 3;
    public bool IsOverComplete => LegalBalls >= BallsPerOver * _oversPerInnings || Wickets >= MaximumWickets;
    public int CompletedOvers => LegalBalls / BallsPerOver;
    public int BallsInCurrentOver => LegalBalls % BallsPerOver;
    public string OversText => $"{CompletedOvers}.{BallsInCurrentOver}";

    public void Reset()
    {
        Runs = 0;
        Wickets = 0;
        LegalBalls = 0;
        Striker = 1;
        NonStriker = 2;
        NextBatter = 3;
    }

    public void RecordDelivery(DeliveryResult result)
    {
        if (IsOverComplete)
            throw new InvalidOperationException("The innings is complete; start another innings before recording a delivery.");

        var error = result.Validate();
        if (error is not null)
            throw new ArgumentException(error, nameof(result));

        Runs += result.BatterRuns + result.ExtraRuns;
        if (result.CompletedRuns % 2 != 0)
            SwapBatterEnds();

        if (result.Dismissal != DismissalKind.None)
        {
            Wickets++;
            if (result.DismissedEnd == DismissedEnd.Striker)
                Striker = NextBatter++;
            else
                NonStriker = NextBatter++;
        }

        if (result.IsLegal)
        {
            LegalBalls++;
            if (LegalBalls % BallsPerOver == 0)
                SwapBatterEnds();
        }
    }

    private void SwapBatterEnds() => (Striker, NonStriker) = (NonStriker, Striker);
}
