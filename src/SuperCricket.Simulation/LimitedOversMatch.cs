namespace SuperCricket.Simulation;

public readonly record struct MatchInningsResult(
    int InningsNumber,
    string TeamName,
    int Runs,
    int Wickets,
    int LegalBalls,
    int OversPerInnings)
{
    public string OversText => $"{LegalBalls / OverScoreboard.BallsPerOver}.{LegalBalls % OverScoreboard.BallsPerOver}";
}

/// <summary>Runs two innings of a short match using the same delivery and innings scorecard rules as practice.</summary>
public sealed class LimitedOversMatch
{
    private MatchState _innings = null!;
    private MatchInningsResult? _firstInnings;
    private MatchInningsResult? _secondInnings;

    public LimitedOversMatch(
        string firstTeamName = "Coastal XI",
        string secondTeamName = "Highland XI",
        int oversPerInnings = 1)
    {
        if (string.IsNullOrWhiteSpace(firstTeamName))
            throw new ArgumentException("The first team needs a name.", nameof(firstTeamName));
        if (string.IsNullOrWhiteSpace(secondTeamName))
            throw new ArgumentException("The second team needs a name.", nameof(secondTeamName));
        if (string.Equals(firstTeamName, secondTeamName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The two teams must have different names.", nameof(secondTeamName));

        FirstTeamName = firstTeamName;
        SecondTeamName = secondTeamName;
        Reset(oversPerInnings);
    }

    public static IReadOnlyList<int> SupportedOversPerInnings { get; } = Array.AsReadOnly(new[] { 1, 2, 5, 10 });

    public string FirstTeamName { get; }
    public string SecondTeamName { get; }
    public int OversPerInnings { get; private set; }
    public int InningsNumber { get; private set; }
    public string BattingTeamName => InningsNumber == 1 ? FirstTeamName : SecondTeamName;
    public MatchState CurrentInnings => _innings;
    public DeliverySession? CurrentDelivery => _innings.CurrentDelivery;
    public int Runs => _innings.Runs;
    public int Wickets => _innings.Wickets;
    public int LegalBalls => _innings.LegalBalls;
    public int Striker => _innings.Striker;
    public int NonStriker => _innings.NonStriker;
    public string OversText => _innings.OversText;
    public int? Target => _firstInnings is { } first && InningsNumber == 2 ? first.Runs + 1 : null;
    public MatchInningsResult? FirstInnings => _firstInnings;
    public MatchInningsResult? SecondInnings => _secondInnings;
    public bool IsInningsComplete => _innings.IsOverComplete ||
        InningsNumber == 2 && Target is { } target && _innings.Runs >= target;
    public bool IsMatchComplete => _secondInnings is not null;

    // Compatibility for the one-innings gameplay review checks.
    public bool IsOverComplete => IsInningsComplete;

    public string ResultText
    {
        get
        {
            if (_firstInnings is not { } first || _secondInnings is not { } second)
                return string.Empty;
            if (second.Runs > first.Runs)
            {
                var wicketsRemaining = OverScoreboard.MaximumWickets - second.Wickets;
                return $"{second.TeamName} wins by {wicketsRemaining} wicket{(wicketsRemaining == 1 ? string.Empty : "s")}";
            }
            if (first.Runs > second.Runs)
            {
                var runMargin = first.Runs - second.Runs;
                return $"{first.TeamName} wins by {runMargin} run{(runMargin == 1 ? string.Empty : "s")}";
            }
            return "Match tied";
        }
    }

    public void Reset(int? oversPerInnings = null)
    {
        var selectedOvers = oversPerInnings ?? (OversPerInnings == 0 ? 1 : OversPerInnings);
        if (selectedOvers is not (1 or 2 or 5 or 10))
            throw new ArgumentOutOfRangeException(nameof(oversPerInnings), "Choose 1, 2, 5, or 10 overs per innings.");

        OversPerInnings = selectedOvers;
        InningsNumber = 1;
        _firstInnings = null;
        _secondInnings = null;
        _innings = new MatchState(selectedOvers);
    }

    public DeliverySession BeginDelivery(bool isNoBall)
    {
        if (IsInningsComplete)
            throw new InvalidOperationException("The innings is complete; begin the next innings before bowling again.");
        return _innings.BeginDelivery(isNoBall);
    }

    public DeliveryResult CompleteDelivery()
    {
        var result = _innings.CompleteDelivery();
        if (!IsInningsComplete)
            return result;

        var summary = new MatchInningsResult(
            InningsNumber,
            BattingTeamName,
            _innings.Runs,
            _innings.Wickets,
            _innings.LegalBalls,
            OversPerInnings);
        if (InningsNumber == 1)
            _firstInnings = summary;
        else
            _secondInnings = summary;
        return result;
    }

    public void StartNextInnings()
    {
        if (InningsNumber != 1 || _firstInnings is null || !IsInningsComplete)
            throw new InvalidOperationException("The first innings must be complete before the second innings can start.");
        InningsNumber = 2;
        _innings = new MatchState(OversPerInnings);
    }
}
