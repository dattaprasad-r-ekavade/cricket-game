using System;
using System.Linq;
using SuperCricket.Content;
using SuperCricket.Content.Bowling;
using SuperCricket.Simulation;
using SuperCricket.Simulation.Bowling;

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
    public BowlingAimResult? LastBowlingAccuracy { get; private set; }
    public bool IsFirstMatch => !_hasCompletedFirstMatch;
    public BowlingSituation CurrentBowlingSituation => new(
        Match.LegalBalls,
        Match.OversPerInnings,
        Match.Runs,
        Match.Wickets,
        Match.Target);

    public void StartNewMatch(int oversPerInnings, int? seed = null)
    {
        Match.Reset(oversPerInnings);
        BowlingSeed = seed ?? Random.Shared.Next();
    }

    public void StartNextInnings() => Match.StartNextInnings();

    public DeliverySession BeginDelivery(bool isNoBall) => Match.BeginDelivery(isNoBall);

    public DeliveryPreset PrepareHumanBattingDelivery(
        DeliveryPreset delivery,
        CpuDifficulty difficulty,
        bool humanBattingControlled,
        bool developerMode,
        bool verificationRun)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        if (!humanBattingControlled || developerMode || verificationRun)
            return delivery;

        return DeliveryPaceModel.ApplyHumanBattingPace(delivery, difficulty, IsFirstMatch);
    }

    public DeliveryPreset ChooseCpuBowlingDelivery(DeliveryPreset stockDelivery, CpuDifficulty difficulty)
    {
        ArgumentNullException.ThrowIfNull(stockDelivery);
        return BowlingDecisionModel.ChooseDelivery(
            stockDelivery,
            Match.CurrentBowler.BowlingAttributes.Accuracy,
            Match.StrikerPlayer.Power,
            CurrentBowlingSituation,
            CreateBowlingDecisionSeed(),
            difficulty).Delivery;
    }

    public FieldPlacementDecision ChooseFieldPlacement(FieldPreset fieldPreset)
    {
        ArgumentNullException.ThrowIfNull(fieldPreset);
        return FieldPlacementModel.Choose(fieldPreset, CurrentBowlingSituation, Match.StrikerPlayer.Power);
    }

    public FieldPlacementDecision ConfigureFieldingSide(FieldingSide fieldingSide, FieldPreset fieldPreset)
    {
        ArgumentNullException.ThrowIfNull(fieldingSide);
        var placement = ChooseFieldPlacement(fieldPreset);
        fieldingSide.ConfigureStartingPositions(placement.StartingPositions);

        var players = Match.FieldingPlayers;
        var ratings = new int[players.Count];
        for (var index = 0; index < players.Count; index++)
            ratings[index] = players[index].FieldingAttributes.Speed;
        fieldingSide.ConfigureFieldingRatings(ratings);
        return placement;
    }

    public AuthoredFieldPlacement ChooseCpuFieldFormation(
        FieldingSide fieldingSide,
        FieldFormationCatalog catalog,
        bool powerplayActive = false)
    {
        ArgumentNullException.ThrowIfNull(fieldingSide);
        ArgumentNullException.ThrowIfNull(catalog);
        var intent = FieldPlacementModel.ChooseIntent(new FieldChoiceSituation(
            Match.LegalBalls,
            Match.OversPerInnings,
            Match.Runs,
            Match.Wickets,
            Match.Target,
            Match.LastWicketLegalBall,
            Match.LastOverBoundaryCount));
        return ConfigureFieldFormation(
            fieldingSide, catalog, intent, Match.StrikerPlayer.Handedness, powerplayActive);
    }

    public AuthoredFieldPlacement ConfigureFieldFormation(
        FieldingSide fieldingSide,
        FieldFormationCatalog catalog,
        FieldingIntent intent,
        BatterHandedness handedness,
        bool powerplayActive = false)
    {
        ArgumentNullException.ThrowIfNull(fieldingSide);
        ArgumentNullException.ThrowIfNull(catalog);
        var placement = FieldPlacementModel.Resolve(
            catalog, Match.CurrentBowler.BowlingAttributes.Style, intent, handedness, powerplayActive);
        fieldingSide.ConfigureStartingPositions(placement.StartingPositions, resetPositions: false);

        var players = Match.FieldingPlayers;
        var ratings = new int[players.Count];
        for (var index = 0; index < players.Count; index++)
            ratings[index] = players[index].FieldingAttributes.Speed;
        fieldingSide.ConfigureFieldingRatings(ratings);
        return placement;
    }

    public DeliveryPreset PrepareDeliveryForCurrentMatch(
        DeliveryPreset selectedPreset,
        int activePresetIndex,
        bool cpuBattingControlled,
        float bowlingAimOffsetX,
        float bowlingAimOffsetZ,
        bool developerMode,
        bool verificationRun,
        CpuDifficulty difficulty)
    {
        ArgumentNullException.ThrowIfNull(selectedPreset);
        var delivery = cpuBattingControlled && !developerMode
            ? BowlingAimModel.AimForPitchTarget(selectedPreset, bowlingAimOffsetX, bowlingAimOffsetZ)
            : activePresetIndex == 0 && !verificationRun
                ? ChooseCpuBowlingDelivery(selectedPreset, difficulty)
                : selectedPreset;

        if (!cpuBattingControlled && !developerMode && !verificationRun)
        {
            var bowler = Match.CurrentBowler;
            var figures = Match.CurrentBowlingFigures.Single(item => item.Bowler.Id == bowler.Id);
            var profile = CpuDifficultyModel.GetProfile(difficulty, DifficultySide.Cpu);
            LastBowlingAccuracy = BowlingAccuracyModel.Aim(
                delivery,
                0f,
                0f,
                bowler,
                Match.LegalBalls % OverScoreboard.BallsPerOver,
                // The CPU decision model already applies its seeded accuracy error; the shared model supplies
                // the target radius and stamina response without stacking a second random scatter on that choice.
                BowlingSkillLevel.Rookie,
                CreateBowlingDecisionSeed() ^ unchecked((int)0x4f1bbcdc),
                profile,
                figures.Stamina);
            delivery = LastBowlingAccuracy.Value.Delivery;
        }

        return PrepareHumanBattingDelivery(
            delivery,
            difficulty,
            humanBattingControlled: !cpuBattingControlled,
            developerMode,
            verificationRun);
    }

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
