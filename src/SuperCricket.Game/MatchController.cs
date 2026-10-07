using System;
using SuperCricket.Simulation;

namespace SuperCricket.Game;

/// <summary>Owns match lifecycle transitions and deterministic seeds used by match decisions.</summary>
internal sealed class MatchController
{
    private bool _hasCompletedFirstMatch;

    public MatchController(LimitedOversMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);
        Match = match;
    }

    public LimitedOversMatch Match { get; }
    public int BowlingSeed { get; private set; }
    public bool IsFirstMatch => !_hasCompletedFirstMatch;

    public void StartNewMatch(int oversPerInnings, int? seed = null)
    {
        Match.Reset(oversPerInnings);
        BowlingSeed = seed ?? Random.Shared.Next();
    }

    public void StartNextInnings() => Match.StartNextInnings();

    public DeliverySession BeginDelivery(bool isNoBall) => Match.BeginDelivery(isNoBall);

    public DeliveryResult CompleteDelivery()
    {
        var result = Match.CompleteDelivery();
        if (Match.IsMatchComplete)
            _hasCompletedFirstMatch = true;
        return result;
    }

    public int CreateBowlingDecisionSeed()
    {
        unchecked
        {
            var seed = BowlingSeed;
            seed = seed * 31 + Match.InningsNumber;
            seed = seed * 31 + Match.LegalBalls;
            seed = seed * 31 + Match.Runs;
            seed = seed * 31 + Match.Wickets;
            seed = AddSeedText(seed, Match.CurrentBowler.Id);
            seed = AddSeedText(seed, Match.StrikerPlayer.Id);
            return seed;
        }
    }

    private static int AddSeedText(int seed, string text)
    {
        unchecked
        {
            foreach (var character in text)
                seed = seed * 31 + character;
            return seed;
        }
    }
}
