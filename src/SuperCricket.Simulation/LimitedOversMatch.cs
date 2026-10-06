using SuperCricket.Content;

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
    private readonly TeamRosterAsset _firstTeam;
    private readonly TeamRosterAsset _secondTeam;
    private MatchState _innings = null!;
    private MatchInningsResult? _firstInnings;
    private MatchInningsResult? _secondInnings;

    public LimitedOversMatch(
        string firstTeamName = "Coastal XI",
        string secondTeamName = "Highland XI",
        int oversPerInnings = 1)
    {
        _firstTeam = TeamRosterAsset.CreatePlaceholder(firstTeamName);
        _secondTeam = TeamRosterAsset.CreatePlaceholder(secondTeamName);
        ValidateTeams(_firstTeam, _secondTeam);
        Reset(oversPerInnings);
    }

    public LimitedOversMatch(TeamRosterAsset firstTeam, TeamRosterAsset secondTeam, int oversPerInnings = 1)
    {
        ArgumentNullException.ThrowIfNull(firstTeam);
        ArgumentNullException.ThrowIfNull(secondTeam);
        ValidateTeams(firstTeam, secondTeam);

        _firstTeam = firstTeam;
        _secondTeam = secondTeam;
        Reset(oversPerInnings);
    }

    public static IReadOnlyList<int> SupportedOversPerInnings { get; } = Array.AsReadOnly(new[] { 1, 2, 5, 10 });

    public TeamRosterAsset FirstTeam => _firstTeam;
    public TeamRosterAsset SecondTeam => _secondTeam;
    public string FirstTeamName => _firstTeam.Name;
    public string SecondTeamName => _secondTeam.Name;
    public int OversPerInnings { get; private set; }
    public int InningsNumber { get; private set; }
    public TeamRosterAsset BattingTeam => InningsNumber == 1 ? _firstTeam : _secondTeam;
    public TeamRosterAsset FieldingTeam => InningsNumber == 1 ? _secondTeam : _firstTeam;
    public string BattingTeamName => BattingTeam.Name;
    public MatchState CurrentInnings => _innings;
    public DeliverySession? CurrentDelivery => _innings.CurrentDelivery;
    public int Runs => _innings.Runs;
    public int Wickets => _innings.Wickets;
    public int LegalBalls => _innings.LegalBalls;
    public int Striker => _innings.Striker;
    public int NonStriker => _innings.NonStriker;
    public TeamPlayerData StrikerPlayer => BattingTeam.GetPlayerAtBattingOrder(Striker);
    public TeamPlayerData NonStrikerPlayer => BattingTeam.GetPlayerAtBattingOrder(NonStriker);
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

    private static void ValidateTeams(TeamRosterAsset firstTeam, TeamRosterAsset secondTeam)
    {
        var firstErrors = firstTeam.Validate();
        if (firstErrors.Count > 0)
            throw new ArgumentException($"The first team roster is invalid: {string.Join(" ", firstErrors)}", nameof(firstTeam));
        var secondErrors = secondTeam.Validate();
        if (secondErrors.Count > 0)
            throw new ArgumentException($"The second team roster is invalid: {string.Join(" ", secondErrors)}", nameof(secondTeam));
        if (string.Equals(firstTeam.Name, secondTeam.Name, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The two teams must have different names.", nameof(secondTeam));
    }
}
