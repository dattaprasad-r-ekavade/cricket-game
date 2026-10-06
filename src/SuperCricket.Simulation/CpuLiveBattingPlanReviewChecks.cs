using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Simulation;

public static class CpuLiveBattingPlanReviewChecks
{
    public static void Run(
        PlayerAsset batterAsset,
        PlayerAsset bowlerAsset,
        BattingShotSet shotSet,
        DeliveryPreset standardDelivery,
        DeliveryPreset wideDelivery,
        TeamRosterAsset battingTeam,
        TeamRosterAsset fieldingTeam,
        FieldPreset fieldPreset)
    {
        ArgumentNullException.ThrowIfNull(batterAsset);
        ArgumentNullException.ThrowIfNull(bowlerAsset);
        ArgumentNullException.ThrowIfNull(shotSet);
        ArgumentNullException.ThrowIfNull(standardDelivery);
        ArgumentNullException.ThrowIfNull(wideDelivery);
        ArgumentNullException.ThrowIfNull(battingTeam);
        ArgumentNullException.ThrowIfNull(fieldingTeam);
        ArgumentNullException.ThrowIfNull(fieldPreset);

        var striker = battingTeam.GetPlayerAtBattingOrder(1);
        var bowler = fieldingTeam.Players
            .Where(player => player.Role.Equals("Bowler", StringComparison.OrdinalIgnoreCase) ||
                player.Role.Equals("AllRounder", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(player => player.Bowling)
            .ThenBy(player => player.BattingOrder)
            .First();
        var wicketkeeper = fieldingTeam.Players.Single(player =>
            player.Role.Equals("Wicketkeeper", StringComparison.OrdinalIgnoreCase));
        var fielders = fieldingTeam.Players
            .Where(player => player.Id != bowler.Id && !ReferenceEquals(player, wicketkeeper))
            .OrderBy(player => player.BattingOrder)
            .Append(wicketkeeper)
            .ToArray();

        var cases = new[]
        {
            (Shot: CpuShotChoice.Defence, Situation: new BowlingSituation(0, 2, 0, 8, Target: null), Power: 25),
            (Shot: CpuShotChoice.Drive, Situation: new BowlingSituation(0, 2, 0, 0, Target: 10), Power: 50),
            (Shot: CpuShotChoice.Loft, Situation: new BowlingSituation(0, 2, 0, 0, Target: 40), Power: 80)
        };
        foreach (var delivery in new[] { standardDelivery, wideDelivery })
        {
            foreach (var testCase in cases)
            {
                var originalPower = striker.Power;
                var originalTiming = striker.Timing;
                striker.Power = testCase.Power;
                striker.Timing = 100;
                try
                {
                    var outcome = CpuBattingOutcomeModel.Choose(
                        striker, bowler, fielders, testCase.Situation, FieldingTactic.Balanced);
                    Require(outcome.Shot == testCase.Shot,
                        $"scenario expected {testCase.Shot} but selected {outcome.Shot}");
                    var shotName = testCase.Shot switch
                    {
                        CpuShotChoice.Defence => "defence",
                        CpuShotChoice.Drive => "drive",
                        _ => "loft"
                    };
                    var samples = BattingPracticeAnalyzer.AnalyzeShot(
                        batterAsset, bowlerAsset, shotSet, shotName, delivery,
                        CpuLiveBattingPlanModel.InputDelayStepSeconds,
                        striker);
                    var plan = CpuLiveBattingPlanModel.ChooseFromSamples(
                        striker, outcome, delivery, samples, seed: 8142);
                    var replay = CpuLiveBattingPlanModel.ChooseFromSamples(
                        striker, outcome, delivery, samples, seed: 8142);
                    Require(plan == replay, "identical CPU live batting inputs did not produce a repeatable plan");

                    var wicketLineTime = BattingPracticeAnalyzer.GetWicketLineTimeSeconds(delivery);
                    Require(float.IsFinite(plan.InputDelaySeconds) && plan.InputDelaySeconds >= 0f &&
                        plan.InputDelaySeconds < wicketLineTime,
                        "CPU selected a shot timing outside the live wicket-line window");
                    Require(float.IsFinite(plan.FootworkOffsetMeters) &&
                        MathF.Abs(plan.FootworkOffsetMeters) <= BatterFootwork.MaximumOffsetMeters + 0.0001f,
                        "CPU selected footwork beyond the supported movement range");

                    var selectedSamples = samples.Where(sample =>
                        MathF.Abs(sample.InputDelaySeconds - plan.InputDelaySeconds) < 0.0001f).ToArray();
                    Require(selectedSamples.Length == 1 &&
                        MathF.Abs(selectedSamples[0].FootworkOffsetMeters - plan.FootworkOffsetMeters) < 0.0001f &&
                        selectedSamples[0].ContactQuality == plan.PredictedContactQuality,
                        "CPU plan did not preserve the analyzer's predicted timing, footwork, and contact quality");
                    if (plan.PredictedContactQuality is { } quality)
                        Require(float.IsFinite(quality) && quality is >= 0f and <= 1f,
                            "CPU plan reported invalid predicted contact quality");

                    if (delivery == standardDelivery && testCase.Shot == CpuShotChoice.Drive)
                    {
                        var formation = FieldPlacementModel.Choose(
                            fieldPreset, testCase.Situation, striker.Power);
                        var placedPlan = CpuLiveBattingPlanModel.Choose(
                            striker,
                            bowler,
                            fielders,
                            testCase.Situation,
                            formation.Tactic,
                            delivery,
                            batterAsset,
                            bowlerAsset,
                            shotSet,
                            seed: 8142,
                            formation.StartingPositions);
                        var placedShot = shotSet.Get("drive");
                        var expectedAim = CpuShotPlacementModel.ChooseHorizontalAim(
                            placedShot,
                            striker.Power,
                            delivery,
                            formation.StartingPositions,
                            fielders.Select(fielder => fielder.Fielding).ToArray());
                        Require(placedPlan.Shot == CpuShotChoice.Drive &&
                            MathF.Abs(placedPlan.HorizontalAim - expectedAim) < 0.0001f,
                            "the live CPU plan did not use the deterministic field-aware shot lane");
                        var authoredGapScore = CpuShotPlacementModel.EvaluateGapScore(
                            placedShot,
                            striker.Power,
                            delivery,
                            formation.StartingPositions,
                            fielders.Select(fielder => fielder.Fielding).ToArray(),
                            placedShot.HorizontalAim);
                        var selectedGapScore = CpuShotPlacementModel.EvaluateGapScore(
                            placedShot,
                            striker.Power,
                            delivery,
                            formation.StartingPositions,
                            fielders.Select(fielder => fielder.Fielding).ToArray(),
                            placedPlan.HorizontalAim);
                        Require(selectedGapScore + 0.0001f >= authoredGapScore,
                            "field-aware shot placement selected a worse lane than the authored aim");

                        var alternateAim = placedShot.HorizontalAim <= 0.5f ? 0.75f : -0.75f;
                        var authoredTrajectory = BattingPracticeAnalyzer.AnalyzeShotTrajectory(
                            batterAsset, bowlerAsset, shotSet, "drive", delivery,
                            placedPlan.InputDelaySeconds, placedPlan.FootworkOffsetMeters, striker);
                        var aimedTrajectory = BattingPracticeAnalyzer.AnalyzeShotTrajectory(
                            batterAsset, bowlerAsset, shotSet, "drive", delivery,
                            placedPlan.InputDelaySeconds, placedPlan.FootworkOffsetMeters, striker, alternateAim);
                        Require(authoredTrajectory.Sample.ContactQuality.HasValue &&
                            aimedTrajectory.Sample.ContactQuality.HasValue &&
                            authoredTrajectory.OutgoingVelocity is { } authoredVelocity &&
                            aimedTrajectory.OutgoingVelocity is { } aimedVelocity &&
                            Vector3.Distance(authoredVelocity, aimedVelocity) > 0.01f,
                            "an explicit batting aim did not alter the actual simulated shot trajectory");

                        striker.Timing = 0;
                        var lowSkillPlan = CpuLiveBattingPlanModel.ChooseFromSamples(
                            striker, outcome, delivery, samples, seed: 8142);
                        Require(lowSkillPlan.Shot == plan.Shot &&
                            (!lowSkillPlan.PredictedContactQuality.HasValue ||
                             plan.PredictedContactQuality is { } highSkillQuality &&
                             lowSkillPlan.PredictedContactQuality.Value <= highSkillQuality + 0.0001f),
                            "a lower timing rating did not reduce or preserve the predicted execution quality");
                    }
                }
                finally
                {
                    striker.Power = originalPower;
                    striker.Timing = originalTiming;
                }
            }
        }

        CpuLiveRunningDecisionReviewChecks.Run(standardDelivery);

        Console.WriteLine("PASS: CPU live batting selects repeatable, rating-aware shots and analyzer-grounded timing and footwork for standard and wide deliveries.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"CPU live batting check failed: {message}");
    }
}
