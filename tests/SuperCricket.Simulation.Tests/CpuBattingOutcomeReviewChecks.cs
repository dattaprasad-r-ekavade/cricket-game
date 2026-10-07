using SuperCricket.Content;

namespace SuperCricket.Simulation;

public static class CpuBattingOutcomeReviewChecks
{
    public static void Run()
    {
        var striker = TeamRosterAsset.CreatePlaceholder("Review Batting XI").Players[0];
        var fieldingTeam = TeamRosterAsset.CreatePlaceholder("Review Fielding XI");
        var bowler = fieldingTeam.Players[7];
        var wicketkeeper = fieldingTeam.Players.Single(player =>
            string.Equals(player.Role, "Wicketkeeper", StringComparison.OrdinalIgnoreCase));
        var fielders = fieldingTeam.Players
            .Where(player => !ReferenceEquals(player, bowler) && !ReferenceEquals(player, wicketkeeper))
            .Append(wicketkeeper)
            .ToArray();
        var situation = new BowlingSituation(0, 2, 0, 0, Target: 30);
        var balanced = CpuBattingOutcomeModel.Choose(
            striker, bowler, fielders, situation, FieldingTactic.Balanced);
        var replay = CpuBattingOutcomeModel.Choose(
            striker, bowler, fielders, situation, FieldingTactic.Balanced);
        var rookie = CpuBattingOutcomeModel.Choose(
            striker, bowler, fielders, situation, FieldingTactic.Balanced, CpuDifficulty.Rookie);
        var pro = CpuBattingOutcomeModel.Choose(
            striker, bowler, fielders, situation, FieldingTactic.Balanced, CpuDifficulty.Pro);
        Require(balanced == replay,
            "identical CPU batting inputs produced different strategies");
        Require(rookie.Aggression < balanced.Aggression && balanced.Aggression < pro.Aggression &&
            rookie.BoundaryChance < balanced.BoundaryChance && balanced.BoundaryChance < pro.BoundaryChance &&
            rookie.OneRunChance < balanced.OneRunChance && balanced.OneRunChance < pro.OneRunChance,
            "CPU batting difficulty did not tune aggression, boundary, and running chances in the expected direction");
        Require(balanced.Shot == CpuShotChoice.Loft && balanced.Aggression > 0.64f,
            "a high-power batter did not choose an aggressive shot under a high chase rate");

        var defensiveBatter = TeamRosterAsset.CreatePlaceholder("Defensive XI").Players[0];
        defensiveBatter.Power = 25;
        defensiveBatter.Timing = 88;
        var lateWicketsSituation = new BowlingSituation(0, 2, 0, 8, Target: null);
        var defensive = CpuBattingOutcomeModel.Choose(
            defensiveBatter, bowler, fielders, lateWicketsSituation, FieldingTactic.AttackWickets);
        var rookieDefensive = CpuBattingOutcomeModel.Choose(
            defensiveBatter, bowler, fielders, lateWicketsSituation, FieldingTactic.AttackWickets, CpuDifficulty.Rookie);
        var proDefensive = CpuBattingOutcomeModel.Choose(
            defensiveBatter, bowler, fielders, lateWicketsSituation, FieldingTactic.AttackWickets, CpuDifficulty.Pro);
        Require(defensive.Shot == CpuShotChoice.Defence && defensive.Aggression < balanced.Aggression,
            "a low-power team protecting late wickets did not choose a controlled shot");
        Require(rookieDefensive.Shot == CpuShotChoice.Defence && defensive.Shot == proDefensive.Shot &&
            rookieDefensive.WicketChance > defensive.WicketChance && defensive.WicketChance > proDefensive.WicketChance,
            "CPU difficulty did not tune dismissal risk while preserving a defensive shot");

        bowler.Bowling = 100;
        var strongBowling = CpuBattingOutcomeModel.Choose(
            striker, bowler, fielders, situation, FieldingTactic.Balanced);
        bowler.Bowling = 0;
        var weakBowling = CpuBattingOutcomeModel.Choose(
            striker, bowler, fielders, situation, FieldingTactic.Balanced);
        Require(strongBowling.WicketChance > weakBowling.WicketChance &&
            strongBowling.BoundaryChance < weakBowling.BoundaryChance,
            "bowling skill did not change the simulated batting outcome chances");

        bowler.Bowling = 50;
        foreach (var fielder in fielders)
            fielder.Fielding = 100;
        var strongFielding = CpuBattingOutcomeModel.Choose(
            striker, bowler, fielders, situation, FieldingTactic.ProtectBoundary);
        foreach (var fielder in fielders)
            fielder.Fielding = 0;
        var weakFielding = CpuBattingOutcomeModel.Choose(
            striker, bowler, fielders, situation, FieldingTactic.Balanced);
        Require(strongFielding.BoundaryChance < weakFielding.BoundaryChance &&
            strongFielding.OneRunChance < weakFielding.OneRunChance &&
            strongFielding.RunOutChance > weakFielding.RunOutChance,
            "fielding skill or boundary protection did not affect CPU batting outcomes");

        foreach (var chance in new[]
                 {
                     balanced.WicketChance, balanced.BoundaryChance, balanced.OneRunChance,
                     balanced.TwoRunChance, balanced.ThreeRunChance, balanced.RunOutChance,
                     balanced.BoundaryClearedChance
                 })
            Require(float.IsFinite(chance) && chance is >= 0f and <= 1f,
                "the CPU outcome model produced a probability outside [0, 1]");

        var invalidStriker = TeamRosterAsset.CreatePlaceholder("Invalid Batter XI").Players[0];
        invalidStriker.Power = 101;
        RequireThrows(() => CpuBattingOutcomeModel.Choose(
                invalidStriker, bowler, fielders, situation, FieldingTactic.Balanced),
            "an invalid striker rating was accepted");
        bowler.Bowling = -1;
        RequireThrows(() => CpuBattingOutcomeModel.Choose(
                striker, bowler, fielders, situation, FieldingTactic.Balanced),
            "an invalid bowler rating was accepted");
        bowler.Bowling = 50;
        fielders[0].Fielding = 101;
        RequireThrows(() => CpuBattingOutcomeModel.Choose(
                striker, bowler, fielders, situation, FieldingTactic.Balanced),
            "an invalid fielder rating was accepted");

        Console.WriteLine("PASS: CPU batting intent and outcome rates respond to batting, bowling, and fielding ratings.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"CPU batting check failed: {message}");
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
        throw new InvalidOperationException($"CPU batting check failed: {message}");
    }
}
