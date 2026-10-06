using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SuperCricket.Simulation;

namespace SuperCricket.Game;

public partial class Game1
{
    private readonly record struct LiveMatchReviewCase(int FrameRate, CpuDifficulty Difficulty, int Overs, int Seed, bool HumanLeaves = false, int FixtureFirstInningsRuns = 0);
    private readonly record struct LiveMatchReviewResult(string Trace, string Summary, int CpuContacts, int CpuLeaves, int CpuCompletedRuns, int YorkerDeliveries, bool Defended);

    private void RunLiveMatchReviewChecks(string outputPath)
    {
        var developerModeWasEnabled = _developerMode;
        _developerMode = false;
        VerifyNormalBowlingSelectorControls();
        _developerMode = developerModeWasEnabled;

        var cases = new List<LiveMatchReviewCase>();
        foreach (var frameRate in new[] { 30, 60, 120 })
            foreach (var difficulty in Enum.GetValues<CpuDifficulty>())
                cases.Add(new LiveMatchReviewCase(frameRate, difficulty, 1, 7100 + (int)difficulty));
        foreach (var difficulty in Enum.GetValues<CpuDifficulty>())
            cases.Add(new LiveMatchReviewCase(60, difficulty, 2, 8100 + (int)difficulty));
        cases.Add(new LiveMatchReviewCase(60, CpuDifficulty.Standard, 2, 9100, HumanLeaves: true));
        // Prepared first-innings scores complement the complete, input-driven matches with sustained chase pressure.
        foreach (var frameRate in new[] { 30, 60, 120 })
            cases.Add(new LiveMatchReviewCase(frameRate, CpuDifficulty.Standard, 2, 10100, FixtureFirstInningsRuns: 24));
        foreach (var difficulty in Enum.GetValues<CpuDifficulty>())
            cases.Add(new LiveMatchReviewCase(60, difficulty, 2, 8100 + (int)difficulty, FixtureFirstInningsRuns: 6));
        cases.Add(new LiveMatchReviewCase(60, CpuDifficulty.Standard, 2, 11100, FixtureFirstInningsRuns: 72));

        var csv = new StringBuilder("fps,difficulty,overs,seed,human_leaves,fixture_first_runs,innings,delivery,preset,shot,leave,contact,predicted_quality,actual_quality,contact_time_s,planned_runs,batter_runs,extras,completed_runs,dismissal,total_runs,wickets,legal_balls\n");
        var totalContacts = 0;
        var totalLeaves = 0;
        var totalRuns = 0;
        var totalYorkers = 0;
        var defendedTargets = 0;
        foreach (var scenario in cases)
        {
            var first = PlayLiveMatchReview(scenario);
            var repeat = PlayLiveMatchReview(scenario);
            RequireLiveReview(first == repeat, $"seeded live match changed on replay: {scenario}");
            csv.Append(first.Trace);
            totalContacts += first.CpuContacts;
            totalLeaves += first.CpuLeaves;
            totalRuns += first.CpuCompletedRuns;
            totalYorkers += first.YorkerDeliveries;
            if (first.Defended) defendedTargets++;
            Console.WriteLine($"PASS: live CPU {scenario.Difficulty}, {scenario.FrameRate} fps, {scenario.Overs} overs, seed {scenario.Seed}, fixture runs {scenario.FixtureFirstInningsRuns}: {first.Summary}");
        }
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(outputPath, csv.ToString());
        RequireLiveReview(totalContacts > 0, "the CPU never contacted a delivery");
        RequireLiveReview(totalRuns > 0, "the CPU never completed a run");
        RequireLiveReview(totalLeaves > 0, "the suite never exercised a live wide leave");
        RequireLiveReview(totalYorkers > 0, "the CPU batting suite never faced a selected yorker");
        RequireLiveReview(defendedTargets > 0, "the suite never exercised a live defended target");
        Console.WriteLine($"Live-match review passed: {cases.Count} match traces replayed exactly, {totalContacts} CPU contacts, {totalLeaves} leaves, {totalYorkers} yorkers, {totalRuns} completed runs, {defendedTargets} defended targets.");
        Console.WriteLine($"Delivery trace written to {outputPath}");
    }

    private void VerifyNormalBowlingSelectorControls()
    {
        var step = TimeSpan.FromSeconds(1d / 120d);
        var time = TimeSpan.Zero;
        void Tick(params Keys[] keys)
        {
            time += step;
            UpdateMatch(new GameTime(time, step), new KeyboardState(keys));
        }

        _selectedOversPerInnings = 1;
        StartNewMatch(19007);
        for (var ball = 0; ball < 6; ball++)
        {
            for (var frame = 0; frame < 1200 && !_deliveryComplete; frame++)
                Tick();
            RequireLiveReview(_deliveryComplete, "control test could not complete the first innings");
            if (ball < 5)
            {
                Tick(Keys.N);
                Tick();
            }
        }
        Tick(Keys.N);
        Tick();
        RequireLiveReview(IsCpuBattingControlled, "control test did not enter the human bowling innings");
        RequireLiveReview(_bowlingTargetMarkerPosition is not null,
            "the human bowling innings did not show the predicted pitch target");
        RequireLiveReview(GetPrimaryControlHint().Contains("Next pitch: arrows aim", StringComparison.Ordinal) &&
            GetPrimaryControlHint().Contains("C: delivery", StringComparison.Ordinal),
            "the human bowling HUD did not keep next-pitch controls concise and phase-specific");

        var expectedNext = (_nextDeliveryPresetIndex + 1) % _deliveryPresets.Length;
        Tick(Keys.C);
        Tick();
        RequireLiveReview(_nextDeliveryPresetIndex == expectedNext,
            "C did not cycle to the next bowling delivery in normal mode");

        var cycleAction = MatchControllerInputModel.ReadPressedActions(
            MatchControllerButtons.LeftShoulder,
            MatchControllerButtons.None,
            isCpuBattingControlled: true,
            isMatchComplete: false,
            developerMode: false);
        expectedNext = (_nextDeliveryPresetIndex + 1) % _deliveryPresets.Length;
        UpdateMatch(new GameTime(time, TimeSpan.Zero), new KeyboardState(), cycleAction);
        RequireLiveReview((cycleAction & MatchControllerActions.CycleDelivery) != 0 &&
            _nextDeliveryPresetIndex == expectedNext,
            "GamePad LB did not cycle the next bowling delivery in normal mode");

        var markerBeforeAim = _bowlingTargetMarkerPosition!.Value;
        Tick(Keys.Left);
        Tick();
        Tick(Keys.Up);
        Tick();
        RequireLiveReview(MathF.Abs(_bowlingAimOffsetX + 0.4f) < 0.001f &&
            MathF.Abs(_bowlingAimOffsetZ + 0.4f) < 0.001f &&
            MathF.Abs(_bowlingTargetMarkerPosition!.Value.X - markerBeforeAim.X + 0.4f) < 0.15f &&
            MathF.Abs(_bowlingTargetMarkerPosition.Value.Z - markerBeforeAim.Z + 0.4f) < 0.15f,
            "arrow keys did not move the visible bowling target across the pitch");

        var longAction = MatchControllerInputModel.ReadPressedActions(
            MatchControllerButtons.DPadUp,
            MatchControllerButtons.None,
            isCpuBattingControlled: true,
            isMatchComplete: false,
            developerMode: false);
        var targetBeforePadInput = _bowlingAimOffsetZ;
        UpdateMatch(new GameTime(time, TimeSpan.Zero), new KeyboardState(), longAction);
        RequireLiveReview((longAction & MatchControllerActions.AimLong) != 0 &&
            MathF.Abs(_bowlingAimOffsetZ - targetBeforePadInput + 0.4f) < 0.001f &&
            GetPrimaryControlHint().Contains("Next pitch: D-pad / left stick aim", StringComparison.Ordinal),
            "GamePad D-pad did not adjust the bowling target length or switch the live control prompt");

        var selectedTarget = _bowlingTargetMarkerPosition!.Value;
        CurrentDelivery.ResolveIncoming(isWide: false, hitsWickets: false);
        FinishDelivery();
        Tick(Keys.N);
        Tick();
        var actualBounce = BowlingAimModel.FindFirstBounce(_deliveryPreset);
        RequireLiveReview(actualBounce is { } bounce &&
            MathF.Abs(ToXna(bounce.Position).X - selectedTarget.X) < 0.15f &&
            MathF.Abs(ToXna(bounce.Position).Z - selectedTarget.Z) < 0.15f,
            $"the next delivery did not pitch at its selected target marker ({(actualBounce is { } actual ? $"{actual.Position.X:0.00},{actual.Position.Z:0.00}" : "no bounce")} vs {selectedTarget.X:0.00},{selectedTarget.Z:0.00}; {_deliveryPreset.Name}; index {_activeDeliveryPresetIndex}; aim {_bowlingAimOffsetX:0.00},{_bowlingAimOffsetZ:0.00})");
        Console.WriteLine("PASS: C/LB select deliveries, keyboard/GamePad direction moves the pitch marker, and the next ball reaches it.");
    }

    private LiveMatchReviewResult PlayLiveMatchReview(LiveMatchReviewCase scenario)
    {
        var trace = new StringBuilder();
        var elapsed = 1f / scenario.FrameRate;
        var totalTime = TimeSpan.Zero;
        void Tick(params Keys[] keys)
        {
            var delta = TimeSpan.FromSeconds(elapsed);
            totalTime += delta;
            UpdateMatch(new GameTime(totalTime, delta), new KeyboardState(keys));
        }
        void Press(Keys key)
        {
            Tick(key);
            Tick();
        }

        _previousKeyboard = default;
        _cpuDifficulty = scenario.Difficulty;
        _selectedOversPerInnings = scenario.Overs;
        StartNewMatch(scenario.Seed);
        if (scenario.FixtureFirstInningsRuns > 0)
        {
            var remainingRuns = scenario.FixtureFirstInningsRuns;
            for (var ball = 0; ball < scenario.Overs * 6; ball++)
            {
                if (remainingRuns >= 4)
                {
                    var six = remainingRuns >= 6;
                    CurrentDelivery.ResolveBoundary(clearedInTheAir: six, currentRunCrossed: false);
                    remainingRuns -= six ? 6 : 4;
                }
                else
                {
                    while (remainingRuns > 0)
                    {
                        CurrentDelivery.RecordCompletedRun();
                        remainingRuns--;
                    }
                }
                _match.CompleteDelivery();
                if (!_match.IsInningsComplete) BeginDelivery();
            }
            Press(Keys.N);
            RequireLiveReview(_match.InningsNumber == 2 && IsCpuBattingControlled &&
                _match.Target == scenario.FixtureFirstInningsRuns + 1, "prepared target did not enter the live CPU chase");
        }
        var cpuContacts = 0;
        var cpuLeaves = 0;
        var cpuCompletedRuns = 0;
        var cpuYorkers = 0;
        var cpuDeliveries = 0;
        var completedDeliveries = 0;
        var inningsDelivery = 0;
        var lastInnings = 1;
        while (!_match.IsMatchComplete && completedDeliveries < scenario.Overs * 20 + 20)
        {
            if (_match.InningsNumber != lastInnings)
            {
                lastInnings = _match.InningsNumber;
                inningsDelivery = 0;
            }
            inningsDelivery++;
            var cpu = IsCpuBattingControlled;
            RequireLiveReview(cpu == (_match.InningsNumber == 2), "CPU control was disabled in the actual chase");
            var plan = _cpuBattingPlan;
            RequireLiveReview(!cpu || plan.HasValue, "chase delivery has no CPU batting plan");
            var enteredRuns = _match.Runs;
            var enteredWickets = _match.Wickets;
            var enteredLegalBalls = _match.LegalBalls;
            var delivery = CurrentDelivery;
            var bowledPreset = _activeDeliveryPresetIndex;
            BattingPracticeSample? humanSample = null;
            var humanShotName = inningsDelivery % 3 == 0 ? "loft" : "drive";
            if (!cpu && !scenario.HumanLeaves)
            {
                humanSample = BattingPracticeAnalyzer.AnalyzeShot(_playerAsset, _bowlerAsset, _shotSet,
                    humanShotName, _deliveryPreset, battingRatings: _match.StrikerPlayer)
                    .Where(sample => sample.InputDelaySeconds >= 0f && sample.ContactQuality.HasValue)
                    .OrderByDescending(sample => sample.ContactQuality)
                    .ThenBy(sample => MathF.Abs(sample.FootworkOffsetMeters))
                    .Cast<BattingPracticeSample?>().FirstOrDefault();
                if (humanSample.HasValue)
                {
                    var footwork = humanSample.Value.FootworkOffsetMeters;
                    var steps = (int)MathF.Round(MathF.Abs(footwork) / BatterFootwork.StepDistanceMeters);
                    for (var step = 0; step < steps; step++) Press(footwork > 0f ? Keys.Q : Keys.E);
                }
            }
            var humanShotPlayed = false;
            var plannedRuns = 0;
            var pauseChecked = false;
            for (var frame = 0; frame < scenario.FrameRate * 12 && !_deliveryComplete; frame++)
            {
                if (cpu && !pauseChecked && _bowlerReleased)
                {
                    Press(Keys.P);
                    var frozenBall = _ballFlight.CurrentFrame;
                    var frozenTime = _playerAnimator.CurrentTimeSeconds;
                    var frozenShot = _cpuShotStarted;
                    var frozenRun = _runElapsed;
                    for (var pausedFrame = 0; pausedFrame < scenario.FrameRate / 2; pausedFrame++) Tick();
                    RequireLiveReview(_simulationPaused && frozenBall == _ballFlight.CurrentFrame &&
                        frozenTime == _playerAnimator.CurrentTimeSeconds && frozenShot == _cpuShotStarted && frozenRun == _runElapsed,
                        "paused live CPU advanced batting, flight, or running");
                    Press(Keys.P);
                    pauseChecked = true;
                }
                if (!cpu && !scenario.HumanLeaves && !humanShotPlayed && _bowlerReleased &&
                    _ballFlight.CurrentFrame.TimeSeconds >= (humanSample?.InputDelaySeconds ?? 0.25f))
                {
                    Tick(humanShotName == "loft" ? Keys.D : Keys.S);
                    humanShotPlayed = true;
                }
                else
                {
                    Tick();
                }
                if (cpu) plannedRuns = Math.Max(plannedRuns, _cpuRunsRemaining + (_isRunning ? 1 : 0));
            }
            RequireLiveReview(_deliveryComplete && delivery.Result.HasValue,
                $"delivery stalled: {scenario}, innings {lastInnings}, ball {inningsDelivery}, {_shotOutcome}");
            var result = delivery.Result!.Value;
            RequireLiveReview(result.Validate() is null && _match.Runs == enteredRuns + result.BatterRuns + result.ExtraRuns &&
                _match.Wickets == enteredWickets + (result.Dismissal == DismissalKind.None ? 0 : 1) &&
                _match.LegalBalls == enteredLegalBalls + (result.IsLegal ? 1 : 0), "delivery/scorecard totals diverged");
            RequireLiveReview(_match.Wickets <= 10 && _match.LegalBalls <= scenario.Overs * 6, "innings limits were exceeded");
            // HUD identities must remain valid even after the tenth wicket.
            _ = _match.StrikerPlayer;
            _ = _match.NonStrikerPlayer;
            if (cpu)
            {
                cpuDeliveries++;
                RequireLiveReview(_cpuShotStarted || _dismissal == DismissalKind.Bowled,
                    "CPU never executed its input plan before the delivery ended");
                if (plan!.Value.Leave)
                {
                    cpuLeaves++;
                    RequireLiveReview(_chosenShot is null && !_battedBall && result.Extra == DeliveryExtra.Wide,
                        "CPU wide leave swung or failed to resolve a wide");
                }
                if (_battedBall) cpuContacts++;
                if (bowledPreset == 3) cpuYorkers++;
                cpuCompletedRuns += result.CompletedRuns;
                RequireLiveReview(plannedRuns <= 2, "CPU planned more than two consecutive runs");
            }
            trace.AppendLine(string.Join(',', scenario.FrameRate, scenario.Difficulty, scenario.Overs, scenario.Seed,
                scenario.HumanLeaves, scenario.FixtureFirstInningsRuns, lastInnings, inningsDelivery, bowledPreset,
                _chosenShot?.Name ?? "none", plan?.Leave ?? false, _battedBall,
                OptionalLiveReview(plan?.PredictedContactQuality), OptionalLiveReview(_contactQuality), OptionalLiveReview(_contactTimeSeconds),
                plannedRuns, result.BatterRuns, result.ExtraRuns, result.CompletedRuns, result.Dismissal,
                _match.Runs, _match.Wickets, _match.LegalBalls));
            completedDeliveries++;
            if (_match.IsMatchComplete) break;

            if (cpu)
            {
                // Exercise wide/no-ball extras and a yorker against the production CPU batter.
                Press(cpuDeliveries switch { 1 => Keys.D2, 2 => Keys.D3, 3 => Keys.D4, _ => Keys.D1 });
            }
            Press(Keys.N);
        }
        RequireLiveReview(_match.IsMatchComplete && _match.FirstInnings.HasValue && _match.SecondInnings.HasValue &&
            !string.IsNullOrWhiteSpace(_match.ResultText), $"full live match failed to finish: {scenario}");
        if (scenario.HumanLeaves)
            RequireLiveReview(_match.FirstInnings!.Value.Wickets == 10, "all-out case did not exercise ten wickets");
        var firstInnings = _match.FirstInnings!.Value;
        var secondInnings = _match.SecondInnings!.Value;
        var summary = $"{firstInnings.Runs}/{firstInnings.Wickets} vs {secondInnings.Runs}/{secondInnings.Wickets}; {_match.ResultText}";
        // Restart through normal input, preserving difficulty and match length.
        Press(Keys.R);
        RequireLiveReview(!_match.IsMatchComplete && _match.InningsNumber == 1 && _match.Runs == 0 &&
            _match.OversPerInnings == scenario.Overs && _cpuDifficulty == scenario.Difficulty,
            "restart failed to restore the configured match");
        return new LiveMatchReviewResult(trace.ToString(), summary, cpuContacts, cpuLeaves, cpuCompletedRuns,
            cpuYorkers, firstInnings.Runs > secondInnings.Runs);
    }

    private static string OptionalLiveReview(float? value) => value?.ToString("0.000000", CultureInfo.InvariantCulture) ?? string.Empty;

    private static void RequireLiveReview(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Live-match review failed: {message}");
    }
}
