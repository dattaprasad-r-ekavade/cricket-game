using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SuperCricket.Content;
using SuperCricket.Simulation;

return Run(args);

static int Run(string[] arguments)
{
    if (arguments.Length == 0 ||
        arguments[0] is "help" or "--help" or "-h" ||
        arguments.Length < 2 && arguments[0] is not ("verify-match" or "verify-match-batch" or "verify-fielding" or "verify-footwork"))
    {
        PrintUsage();
        return arguments.Length < 2 ? 2 : 0;
    }

    try
    {
        if (arguments[0] == "validate-player")
        {
            var player = PlayerAsset.Load(arguments[1]);
            Console.WriteLine($"Valid player asset: {player.Name}");
            Console.WriteLine($"Rig: {player.Bones.Count} bones, {player.Meshes.Count} meshes, {player.Animations.Count} animation clips.");
            foreach (var animation in player.Animations)
            {
                Console.WriteLine($"  {animation.Name}: {animation.DurationSeconds:0.00} s, {animation.Samples.Count} sampled poses.");
                var rootMotionStart = animation.Samples[0].RootMotion.ToVector3();
                var rootMotionEnd = animation.Samples[^1].RootMotion.ToVector3();
                var rootMotionDelta = rootMotionEnd - rootMotionStart;
                if (rootMotionDelta.LengthSquared() > 0.000001f)
                    Console.WriteLine($"    root motion {rootMotionDelta.Length():0.00} m ({rootMotionDelta.X:0.00}, {rootMotionDelta.Y:0.00}, {rootMotionDelta.Z:0.00})");
                foreach (var animationEvent in animation.Events)
                    Console.WriteLine($"    event {animationEvent.Name} at {animationEvent.TimeSeconds:0.000} s");
            }
            return 0;
        }

        if (arguments[0] == "validate-shots")
        {
            var shotSet = BattingShotSet.Load(arguments[1]);
            Console.WriteLine($"Valid batting shot set: {shotSet.Shots.Count} shots.");
            foreach (var shot in shotSet.Shots)
                Console.WriteLine($"  {shot.Name}: {shot.AnimationClip}, {shot.LaunchAngleDegrees:0.#}° launch, {shot.SpeedTransfer:0.##} speed transfer");
            return 0;
        }

        if (arguments[0] == "analyze-batting")
        {
            var outputPath = arguments.Length > 2
                ? arguments[2]
                : Path.Combine("artifacts", "batting-impact-grid.csv");
            AnalyzeBatting(arguments[1], outputPath);
            return 0;
        }

        if (arguments[0] == "analyze-batting-practice")
        {
            if (arguments.Length is < 5 or > 7)
                throw new ArgumentException("Usage: analyze-batting-practice <batter.scplayer.json> <bowler.scplayer.json> <shots.json> <delivery.json> [results.csv] [input-step-seconds]");
            var outputPath = arguments.Length > 5
                ? arguments[5]
                : Path.Combine("artifacts", "batting-practice.csv");
            var delayStep = 0.025f;
            if (arguments.Length > 6 &&
                (!float.TryParse(arguments[6], NumberStyles.Float, CultureInfo.InvariantCulture, out delayStep) || !float.IsFinite(delayStep)))
                throw new ArgumentException("Input-step-seconds must be a finite number between 0.01 and 0.25.");
            AnalyzeBattingPractice(arguments[1], arguments[2], arguments[3], arguments[4], outputPath, delayStep);
            return 0;
        }

        if (arguments[0] == "verify-batting-practice")
        {
            if (arguments.Length != 5)
                throw new ArgumentException("Usage: verify-batting-practice <batter.scplayer.json> <bowler.scplayer.json> <shots.json> <delivery.json>");
            VerifyBattingPractice(arguments[1], arguments[2], arguments[3], arguments[4]);
            return 0;
        }

        if (arguments[0] == "verify-batting")
        {
            VerifyBatting(arguments[1]);
            return 0;
        }

        if (arguments[0] == "validate-field")
        {
            var field = FieldPreset.Load(arguments[1]);
            Console.WriteLine($"Valid field preset: {field.Name}");
            Console.WriteLine($"Boundary: {field.BoundaryRadiusMeters:0.##} m; {field.Players.Count} fielders.");
            foreach (var player in field.Players)
            {
                var position = player.Position.ToVector3();
                Console.WriteLine($"  {player.Name}{(player.IsWicketkeeper ? " (wicketkeeper)" : string.Empty)}: ({position.X:0.##}, {position.Y:0.##}, {position.Z:0.##}) m");
            }
            return 0;
        }

        if (arguments[0] == "analyze-field")
        {
            var outputPath = arguments.Length > 2
                ? arguments[2]
                : Path.Combine("artifacts", "practice-attack-coverage.csv");
            var gridSpacing = arguments.Length > 3
                ? float.Parse(arguments[3], CultureInfo.InvariantCulture)
                : 2f;
            AnalyzeField(arguments[1], outputPath, gridSpacing);
            return 0;
        }

        if (arguments[0] == "simulate-over")
        {
            SimulateOver(arguments[1]);
            return 0;
        }

        if (arguments[0] == "verify-cpu-batting")
        {
            if (arguments.Length != 9)
                throw new ArgumentException("Usage: verify-cpu-batting <batter.scplayer.json> <bowler.scplayer.json> <shots.json> <standard-delivery.json> <wide-delivery.json> <batting-team.json> <fielding-team.json> <field.json>");
            return RunSimulationTests(
                "FullyQualifiedName~CpuLiveBattingPlanReviewCheckTests",
                arguments.Skip(1).ToArray());
        }

        if (arguments[0] == "validate-team")
        {
            var team = TeamRosterAsset.Load(arguments[1]);
            Console.WriteLine($"Valid team roster: {team.Name} ({team.ShortName}), {team.Players.Count} players; kit {team.PrimaryKitColorHex}/{team.AccentKitColorHex}.");
            foreach (var player in team.Players.OrderBy(player => player.BattingOrder))
                Console.WriteLine($"  {player.BattingOrder,2}. {player.Name} ({player.Role}) — timing {player.Timing}, power {player.Power}, bowling {player.Bowling}, fielding {player.Fielding}");
            return 0;
        }

        if (arguments[0] == "verify-match")
        {
            if (arguments.Length != 1)
                throw new ArgumentException("Usage: verify-match");
            return RunSimulationTests("FullyQualifiedName~CoreMatchReviewCheckTests");
        }

        if (arguments[0] == "verify-match-batch")
        {
            if (arguments.Length != 1)
                throw new ArgumentException("Usage: verify-match-batch");
            return RunSimulationTests("FullyQualifiedName~AutomatedMatchBatchReviewCheckTests");
        }

        if (arguments[0] == "simulate-match-batch")
        {
            if (arguments.Length is < 6 or > 7)
                throw new ArgumentException("Usage: simulate-match-batch <first-team.json> <second-team.json> <count> <overs> <seed> [results.csv]");
            if (!int.TryParse(arguments[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var matchCount) ||
                !int.TryParse(arguments[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var oversPerInnings) ||
                !int.TryParse(arguments[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed))
                throw new ArgumentException("Match count, overs, and seed must be integers.");
            var firstTeam = TeamRosterAsset.Load(arguments[1]);
            var secondTeam = TeamRosterAsset.Load(arguments[2]);
            var outputPath = arguments.Length == 7 ? arguments[6] : Path.Combine("artifacts", "automatic-match-batch.csv");
            SimulateMatchBatch(firstTeam, secondTeam, matchCount, oversPerInnings, seed, outputPath);
            return 0;
        }

        if (arguments[0] == "simulate-physics-match-batch")
        {
            if (arguments.Length is < 13 or > 14)
                throw new ArgumentException("Usage: simulate-physics-match-batch <first-team.json> <second-team.json> <field.json> <batter.scplayer.json> <bowler.scplayer.json> <shots.json> <standard-delivery.json> <wide-delivery.json> <no-ball-delivery.json> <count> <overs> <seed> [results.csv]");
            if (!int.TryParse(arguments[10], NumberStyles.Integer, CultureInfo.InvariantCulture, out var matchCount) ||
                !int.TryParse(arguments[11], NumberStyles.Integer, CultureInfo.InvariantCulture, out var oversPerInnings) ||
                !int.TryParse(arguments[12], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed))
                throw new ArgumentException("Match count, overs, and seed must be integers.");
            var outputPath = arguments.Length == 14 ? arguments[13] : Path.Combine("artifacts", "physics-match-batch.csv");
            SimulatePhysicsMatchBatch(
                TeamRosterAsset.Load(arguments[1]),
                TeamRosterAsset.Load(arguments[2]),
                FieldPreset.Load(arguments[3]),
                PlayerAsset.Load(arguments[4]),
                PlayerAsset.Load(arguments[5]),
                BattingShotSet.Load(arguments[6]),
                DeliveryPreset.Load(arguments[7]),
                DeliveryPreset.Load(arguments[8]),
                DeliveryPreset.Load(arguments[9]),
                matchCount,
                oversPerInnings,
                seed,
                outputPath);
            return 0;
        }

        if (arguments[0] == "verify-fielding")
        {
            if (arguments.Length != 1)
                throw new ArgumentException("Usage: verify-fielding");
            return RunSimulationTests("FullyQualifiedName~FieldingReviewCheckTests");
        }

        if (arguments[0] == "verify-footwork")
        {
            if (arguments.Length != 5)
                throw new ArgumentException("Usage: verify-footwork <batter.scplayer.json> <bowler.scplayer.json> <shots.json> <wide-delivery.json>");
            VerifyFootwork(arguments[1], arguments[2], arguments[3], arguments[4]);
            return 0;
        }

        var preset = DeliveryPreset.Load(arguments[1]);
        switch (arguments[0])
        {
            case "validate":
                Console.WriteLine($"Valid delivery preset: {preset.Name}");
                Console.WriteLine($"Release speed: {preset.StartVelocity.Length():0.0} m/s ({preset.StartVelocity.Length() * 3.6f:0} km/h)");
                return 0;

            case "simulate":
                var outputPath = arguments.Length > 2
                    ? arguments[2]
                    : Path.Combine("artifacts", "standard-pace-trajectory.csv");
                Simulate(preset, outputPath);
                return 0;

            default:
                Console.Error.WriteLine($"Unknown command '{arguments[0]}'.");
                PrintUsage();
                return 2;
        }
    }
    catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or FormatException or JsonException)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

static int RunSimulationTests(string testFilter, string[]? cpuBattingInputs = null)
{
    var repositoryRoot = FindRepositoryRoot();
    var projectPath = Path.Combine(repositoryRoot, "tests", "SuperCricket.Simulation.Tests", "SuperCricket.Simulation.Tests.csproj");
    var startInfo = new ProcessStartInfo("dotnet")
    {
        UseShellExecute = false,
        WorkingDirectory = repositoryRoot
    };
    startInfo.ArgumentList.Add("test");
    startInfo.ArgumentList.Add(projectPath);
    startInfo.ArgumentList.Add("-c");
    startInfo.ArgumentList.Add("Release");
    startInfo.ArgumentList.Add("--filter");
    startInfo.ArgumentList.Add(testFilter);
    if (cpuBattingInputs is not null)
        startInfo.Environment["SUPERCRICKET_CPU_BATTING_INPUTS"] = JsonSerializer.Serialize(cpuBattingInputs);

    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("Could not start the xUnit test runner.");
    process.WaitForExit();
    return process.ExitCode;
}

static string FindRepositoryRoot()
{
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "SuperCricket.sln")))
            return directory.FullName;
    }

    throw new DirectoryNotFoundException("Could not locate SuperCricket.sln for the simulation test runner.");
}

static void SimulateOver(string scenarioPath)
{
    var json = File.ReadAllText(scenarioPath);
    var scenario = JsonSerializer.Deserialize<OverScenarioDocument>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    }) ?? throw new InvalidDataException($"Over scenario '{scenarioPath}' was empty.");
    if (string.IsNullOrWhiteSpace(scenario.Name) || scenario.Deliveries.Count == 0)
        throw new InvalidDataException("An over scenario needs a name and at least one delivery result.");

    var score = new OverScoreboard();
    Console.WriteLine($"Scenario: {scenario.Name}");
    for (var index = 0; index < scenario.Deliveries.Count; index++)
    {
        if (score.IsOverComplete)
            throw new InvalidDataException($"Scenario contains a result after the over completed at entry {index + 1}.");

        var result = scenario.Deliveries[index];
        score.RecordDelivery(result);
        var delivery = result.Dismissal == DismissalKind.None
            ? $"{result.BatterRuns} batter, {result.ExtraRuns} extra"
            : $"{result.Dismissal} ({result.DismissedEnd.ToString().ToLowerInvariant()})";
        var legality = result.IsLegal ? "legal" : result.Extra.ToString();
        Console.WriteLine($"  {index + 1}. {legality}, {delivery}: {score.Runs}/{score.Wickets} after {score.OversText}");
    }

    if (!score.IsOverComplete)
        throw new InvalidDataException($"Scenario ended at {score.OversText}; six legal deliveries are required to complete the over.");
    Console.WriteLine($"Completed over: {score.Runs}/{score.Wickets} in {score.OversText} overs.");
    Console.WriteLine($"Striker {score.Striker}, non-striker {score.NonStriker}.");
}

static void AnalyzeField(string presetPath, string outputPath, float gridSpacingMeters)
{
    if (!float.IsFinite(gridSpacingMeters) || gridSpacingMeters is < 0.5f or > 10f)
        throw new ArgumentOutOfRangeException(nameof(gridSpacingMeters), "Grid spacing must be between 0.5 and 10 m.");

    var preset = FieldPreset.Load(presetPath);
    var csv = new StringBuilder("x_m,z_m,nearest_fielder,estimated_reach_s\n");
    var reachTimes = new List<float>();
    var fastestFielders = new int[preset.Players.Count];
    var radius = preset.BoundaryRadiusMeters;
    for (var z = -radius; z <= radius + 0.0001f; z += gridSpacingMeters)
    {
        for (var x = -radius; x <= radius + 0.0001f; x += gridSpacingMeters)
        {
            if (x * x + z * z > radius * radius)
                continue;

            var target = new Vector3(x, -0.08f, z);
            var fastestIndex = -1;
            var fastestTime = float.PositiveInfinity;
            for (var playerIndex = 0; playerIndex < preset.Players.Count; playerIndex++)
            {
                var time = FieldingSide.EstimateReachTime(preset.Players[playerIndex].Position.ToVector3(), target);
                if (time >= fastestTime)
                    continue;
                fastestIndex = playerIndex;
                fastestTime = time;
            }

            reachTimes.Add(fastestTime);
            fastestFielders[fastestIndex]++;
            csv.Append(F(x)).Append(',')
                .Append(F(z)).Append(',')
                .Append(CsvValue(preset.Players[fastestIndex].Name)).Append(',')
                .AppendLine(F(fastestTime));
        }
    }

    if (reachTimes.Count == 0)
        throw new InvalidDataException("Field analysis produced no in-boundary grid points.");

    var sortedTimes = reachTimes.Order().ToArray();
    var p95Index = Math.Clamp((int)MathF.Ceiling(sortedTimes.Length * 0.95f) - 1, 0, sortedTimes.Length - 1);
    var withinOneSecond = reachTimes.Count(time => time <= 1f);
    var withinTwoSeconds = reachTimes.Count(time => time <= 2f);
    var fullOutputPath = Path.GetFullPath(outputPath);
    var outputDirectory = Path.GetDirectoryName(fullOutputPath);
    if (!string.IsNullOrEmpty(outputDirectory))
        Directory.CreateDirectory(outputDirectory);
    File.WriteAllText(fullOutputPath, csv.ToString());

    Console.WriteLine($"Field: {preset.Name} ({preset.Players.Count} fielders, {radius:0.##} m boundary)");
    Console.WriteLine($"Coverage grid: {reachTimes.Count} points at {gridSpacingMeters:0.##} m spacing.");
    Console.WriteLine($"Estimated reach within 1 s: {withinOneSecond}/{reachTimes.Count} ({withinOneSecond * 100f / reachTimes.Count:0.0}%).");
    Console.WriteLine($"Estimated reach within 2 s: {withinTwoSeconds}/{reachTimes.Count} ({withinTwoSeconds * 100f / reachTimes.Count:0.0}%).");
    Console.WriteLine($"Reach time P95/max: {sortedTimes[p95Index]:0.000}/{sortedTimes[^1]:0.000} s.");
    for (var index = 0; index < preset.Players.Count; index++)
        Console.WriteLine($"  {preset.Players[index].Name}: fastest for {fastestFielders[index]} points.");
    Console.WriteLine($"Coverage CSV written to {fullOutputPath}");
}

static string CsvValue(string value) => value.Contains(',') || value.Contains('"') || value.Contains('\r') || value.Contains('\n')
    ? $"\"{value.Replace("\"", "\"\"")}\""
    : value;

static void Simulate(DeliveryPreset preset, string outputPath)
{
    var simulator = new BallFlightSimulator(preset);
    var csv = new StringBuilder();
    csv.AppendLine("time_s,x_m,y_m,z_m,vx_mps,vy_mps,vz_mps,speed_mps,bounces,phase");
    AppendFrame(csv, simulator.CurrentFrame);

    var maximumSteps = (int)MathF.Ceiling(preset.MaximumSimulationSeconds / preset.FixedTimeStepSeconds) + 1;
    for (var step = 0; step < maximumSteps && simulator.CurrentFrame.Phase != BallMotionPhase.Settled; step++)
    {
        AppendFrame(csv, simulator.Step());
    }

    var fullOutputPath = Path.GetFullPath(outputPath);
    var outputDirectory = Path.GetDirectoryName(fullOutputPath);
    if (!string.IsNullOrEmpty(outputDirectory))
    {
        Directory.CreateDirectory(outputDirectory);
    }
    File.WriteAllText(fullOutputPath, csv.ToString());

    var final = simulator.CurrentFrame;
    Console.WriteLine($"Simulated '{preset.Name}' to {final.TimeSeconds:0.000}s: {final.BounceCount} bounces, {final.Phase.ToString().ToLowerInvariant()}.");
    Console.WriteLine($"Final position: ({final.Position.X:0.00}, {final.Position.Y:0.00}, {final.Position.Z:0.00}) m.");
    Console.WriteLine($"Trajectory written to {fullOutputPath}");
}

static void AppendFrame(StringBuilder csv, BallFlightFrame frame)
{
    var position = frame.Position;
    var velocity = frame.Velocity;
    var speed = velocity.Length();
    csv.AppendLine(string.Join(',',
        F(frame.TimeSeconds),
        F(position.X), F(position.Y), F(position.Z),
        F(velocity.X), F(velocity.Y), F(velocity.Z),
        F(speed),
        frame.BounceCount.ToString(CultureInfo.InvariantCulture),
        frame.Phase.ToString().ToLowerInvariant()));
}

static void AnalyzeBatting(string shotSetPath, string outputPath)
{
    var shotSet = BattingShotSet.Load(shotSetPath);
    var offsets = new[]
    {
        new Vector2(-1f, -1f), new Vector2(0f, -1f), new Vector2(1f, -1f),
        new Vector2(-1f, 0f), new Vector2(0f, 0f), new Vector2(1f, 0f),
        new Vector2(-1f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f)
    };
    var swingSpeeds = new[] { 0f, 4f, 8f };
    var incomingVelocity = new Vector3(0f, 0f, -30f);
    var csv = new StringBuilder();
    csv.AppendLine("shot,offset_x,offset_y,swing_speed_mps,incoming_speed_mps,quality,launch_angle_degrees,out_x_mps,out_y_mps,out_z_mps,out_speed_mps");

    foreach (var shot in shotSet.Shots)
    {
        foreach (var offset in offsets)
        {
            foreach (var swingSpeed in swingSpeeds)
            {
                var swingVelocity = new Vector3(0f, 0f, -swingSpeed);
                var impact = BattingImpactModel.Calculate(incomingVelocity, swingVelocity, offset, shot);
                var outgoing = impact.OutgoingVelocity;
                csv.Append(CsvValue(shot.Name)).Append(',')
                    .Append(F(offset.X)).Append(',').Append(F(offset.Y)).Append(',')
                    .Append(F(swingSpeed)).Append(',').Append(F(incomingVelocity.Length())).Append(',')
                    .Append(F(impact.ContactQuality)).Append(',').Append(F(impact.LaunchAngleDegrees)).Append(',')
                    .Append(F(outgoing.X)).Append(',').Append(F(outgoing.Y)).Append(',').Append(F(outgoing.Z)).Append(',')
                    .AppendLine(F(outgoing.Length()));
            }
        }
    }

    var fullOutputPath = Path.GetFullPath(outputPath);
    var outputDirectory = Path.GetDirectoryName(fullOutputPath);
    if (!string.IsNullOrEmpty(outputDirectory))
        Directory.CreateDirectory(outputDirectory);
    File.WriteAllText(fullOutputPath, csv.ToString());
    Console.WriteLine($"Analyzed {shotSet.Shots.Count} batting intents across {offsets.Length} contact points and {swingSpeeds.Length} swing speeds.");
    Console.WriteLine($"Impact grid written to {fullOutputPath}");
}

static void VerifyBatting(string shotSetPath)
{
    var shotSet = BattingShotSet.Load(shotSetPath);
    var incomingVelocity = new Vector3(0f, 0f, -30f);
    var bladeMinimum = new Vector3(-0.2f, -0.5f, -0.05f);
    var bladeMaximum = new Vector3(0.2f, 0.5f, 0.05f);
    var straightDirection = BattingImpactModel.GetHorizontalShotDirection(0f);
    if (MathF.Abs(straightDirection.X) > 0.0001f || MathF.Abs(straightDirection.Z - 1f) > 0.0001f)
        throw new InvalidDataException("A straight drive did not point from the striker's wicket toward the bowler's end.");
    if (!SweptBattingContactResolver.TryResolve(
            new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, 1f),
            Matrix4x4.Identity, Matrix4x4.Identity,
            bladeMinimum, bladeMaximum, 0.03f, 1f / 120f, out var stationaryContact) ||
        stationaryContact.NormalizedSweetSpotOffset.Length() > 0.001f ||
        stationaryContact.SweetSpotPosition.Length() > 0.001f)
        throw new InvalidDataException("The swept bat resolver missed a centered ball against a stationary bat.");

    if (SweptBattingContactResolver.TryResolve(
            new Vector3(0f, 1f, -1f), new Vector3(0f, 1f, 1f),
            Matrix4x4.Identity, Matrix4x4.Identity,
            bladeMinimum, bladeMaximum, 0.03f, 1f / 120f, out _))
        throw new InvalidDataException("The swept bat resolver accepted a ball outside the blade bounds.");

    if (!SweptBattingContactResolver.TryResolve(
            Vector3.Zero, Vector3.Zero,
            Matrix4x4.CreateTranslation(-1f, 0f, 0f), Matrix4x4.CreateTranslation(1f, 0f, 0f),
            bladeMinimum, bladeMaximum, 0.03f, 0.1f, out var movingBatContact) ||
        movingBatContact.BatPointVelocity.X < 19.9f ||
        MathF.Abs(movingBatContact.SweetSpotPosition.X) > 0.24f)
        throw new InvalidDataException("The swept bat resolver missed a moving bat or reported an incorrect sweet-spot position or point velocity.");

    foreach (var shot in shotSet.Shots)
    {
        var center = BattingImpactModel.Calculate(incomingVelocity, Vector3.Zero, Vector2.Zero, shot);
        var edge = BattingImpactModel.Calculate(incomingVelocity, Vector3.Zero, Vector2.One, shot);
        var highContact = BattingImpactModel.Calculate(incomingVelocity, Vector3.Zero, new Vector2(0f, 1f), shot);
        var lowContact = BattingImpactModel.Calculate(incomingVelocity, Vector3.Zero, new Vector2(0f, -1f), shot);
        var forwardSwing = BattingImpactModel.Calculate(incomingVelocity, new Vector3(0f, 0f, 8f), Vector2.Zero, shot);
        var upwardSwing = BattingImpactModel.Calculate(incomingVelocity, new Vector3(0f, 8f, 0f), Vector2.Zero, shot);
        var actualAngle = MathF.Atan2(upwardSwing.OutgoingVelocity.Y,
            new Vector2(upwardSwing.OutgoingVelocity.X, upwardSwing.OutgoingVelocity.Z).Length()) * 180f / MathF.PI;
        if (MathF.Abs(upwardSwing.LaunchAngleDegrees - actualAngle) > 0.001f)
            throw new InvalidDataException($"Reported launch angle differs from outgoing velocity for '{shot.Name}'.");

        if (center.ContactQuality <= edge.ContactQuality ||
            center.OutgoingVelocity.Z <= 0f ||
            highContact.LaunchAngleDegrees <= lowContact.LaunchAngleDegrees ||
            forwardSwing.OutgoingVelocity.Length() <= center.OutgoingVelocity.Length())
            throw new InvalidDataException($"Batting impact response failed a forward-axis, sweet-spot, vertical-offset, or swing-speed check for '{shot.Name}'.");

        Console.WriteLine($"{shot.Name}: sweet spot {center.ContactQuality:0.00}, edge {edge.ContactQuality:0.00}, swing gain {forwardSwing.OutgoingVelocity.Length() - center.OutgoingVelocity.Length():0.00} m/s");
    }

    Console.WriteLine("Swept collision and batting impact checks passed.");
}

static void AnalyzeBattingPractice(
    string batterPath,
    string bowlerPath,
    string shotSetPath,
    string deliveryPath,
    string outputPath,
    float delayStepSeconds)
{
    var batter = PlayerAsset.Load(batterPath);
    var bowler = PlayerAsset.Load(bowlerPath);
    var shotSet = BattingShotSet.Load(shotSetPath);
    var delivery = DeliveryPreset.Load(deliveryPath);
    var results = BattingPracticeAnalyzer.Analyze(batter, bowler, shotSet, delivery, delayStepSeconds);
    var csv = new StringBuilder();
    csv.AppendLine("shot,delivery,input_delay_s,outcome,contact_time_s,contact_quality,launch_angle_degrees,outgoing_speed_mps,bat_point_speed_mps,sweet_spot_x,sweet_spot_y,footwork_offset_m");
    foreach (var sample in results)
    {
        csv.Append(CsvValue(sample.ShotName)).Append(',')
            .Append(CsvValue(sample.DeliveryName)).Append(',')
            .Append(F(sample.InputDelaySeconds)).Append(',')
            .Append(sample.Outcome).Append(',')
            .Append(Optional(sample.ContactTimeSeconds)).Append(',')
            .Append(Optional(sample.ContactQuality)).Append(',')
            .Append(Optional(sample.LaunchAngleDegrees)).Append(',')
            .Append(Optional(sample.OutgoingSpeedMetersPerSecond)).Append(',')
            .Append(Optional(sample.BatPointSpeedMetersPerSecond)).Append(',')
            .Append(Optional(sample.SweetSpotOffsetX)).Append(',')
            .Append(Optional(sample.SweetSpotOffsetY)).Append(',')
            .AppendLine(F(sample.FootworkOffsetMeters));
    }

    var fullOutputPath = Path.GetFullPath(outputPath);
    var outputDirectory = Path.GetDirectoryName(fullOutputPath);
    if (!string.IsNullOrEmpty(outputDirectory))
        Directory.CreateDirectory(outputDirectory);
    File.WriteAllText(fullOutputPath, csv.ToString());

    Console.WriteLine($"Analyzed {results.Count} input timings for {delivery.Name} with the exported batter and bowler clips.");
    foreach (var shotGroup in results.GroupBy(result => result.ShotName))
    {
        var contacts = shotGroup.Where(result => result.ContactQuality.HasValue).ToArray();
        if (contacts.Length == 0)
        {
            Console.WriteLine($"  {shotGroup.Key}: no contacts in the tested input window.");
            continue;
        }

        var best = contacts.OrderByDescending(result => result.ContactQuality).First();
        Console.WriteLine($"  {shotGroup.Key}: {contacts.Length}/{shotGroup.Count()} timings contact; best {best.ContactQuality:0.00} at {best.InputDelaySeconds:+0.000;-0.000;0.000} s, step {best.FootworkOffsetMeters:+0.00;-0.00;0.00} m, result {best.Outcome}.");
    }
    Console.WriteLine($"Batting-practice results written to {fullOutputPath}");
}

static void VerifyBattingPractice(string batterPath, string bowlerPath, string shotSetPath, string deliveryPath)
{
    var batter = PlayerAsset.Load(batterPath);
    var bowler = PlayerAsset.Load(bowlerPath);
    var shotSet = BattingShotSet.Load(shotSetPath);
    var delivery = DeliveryPreset.Load(deliveryPath);
    var results = BattingPracticeAnalyzer.Analyze(batter, bowler, shotSet, delivery);
    var bestDrive = results
        .Where(sample => string.Equals(sample.ShotName, "drive", StringComparison.OrdinalIgnoreCase) && sample.ContactQuality.HasValue)
        .OrderByDescending(sample => sample.ContactQuality)
        .FirstOrDefault();
    var driveTrajectory = BattingPracticeAnalyzer.AnalyzeShotTrajectory(
        batter,
        bowler,
        shotSet,
        "drive",
        delivery,
        bestDrive.InputDelaySeconds,
        bestDrive.FootworkOffsetMeters);
    if (driveTrajectory.ContactPosition is not { } driveContact ||
        driveTrajectory.OutgoingVelocity is not { } driveVelocity ||
        MathF.Abs(driveContact.Z - BattingPracticeAnalyzer.BatterWicketLineZ) > 0.5f ||
        driveVelocity.Z <= 0f)
        throw new InvalidDataException("The real-asset front-foot drive did not travel from the striker's wicket toward the bowler's end.");
    Console.WriteLine($"PASS: real-asset straight drive travels toward positive Z from contact at {driveContact.Z:0.00} m.");

    // A broad blade guarantees overlap at release, including while still in stance.
    // It exposes collisions incorrectly accepted before a positive input delay.
    var overlapBatter = PlayerAsset.Load(batterPath);
    var blade = overlapBatter.Meshes.Find(mesh => mesh.Name == "Bat Blade")!;
    for (var index = 0; index < blade.Positions.Length; index++)
        blade.Positions[index] = index < 3 ? -100f : 100f;
    var overlapResults = BattingPracticeAnalyzer.Analyze(overlapBatter, bowler, shotSet, delivery, 0.019f);
    if (!overlapResults.Any(sample => sample.InputDelaySeconds > 0f && sample.ContactQuality.HasValue))
        throw new InvalidDataException("Pre-input overlap fixture did not exercise delayed contact.");
    foreach (var sample in overlapResults.Concat(results))
    {
        if (sample.ContactQuality.HasValue &&
            (!sample.ContactTimeSeconds.HasValue || sample.ContactTimeSeconds.Value < MathF.Max(0f, sample.InputDelaySeconds) - 0.000001f))
            throw new InvalidDataException("Batting practice accepted contact before the shot input.");
        if (!sample.ContactQuality.HasValue && sample.ContactTimeSeconds.HasValue)
            throw new InvalidDataException("A missed shot must not report a contact time.");
    }

    foreach (var shot in shotSet.Shots)
    {
        var contacts = results
            .Where(result => string.Equals(result.ShotName, shot.Name, StringComparison.OrdinalIgnoreCase) && result.ContactQuality.HasValue)
            .ToArray();
        if (contacts.Length == 0)
            throw new InvalidDataException($"Shot '{shot.Name}' has no contact timing against '{delivery.Name}'.");

        var best = contacts.OrderByDescending(result => result.ContactQuality).First();
        if (!float.IsFinite(best.ContactQuality!.Value) || best.ContactQuality.Value is < 0.48f or > 1f ||
            !float.IsFinite(best.OutgoingSpeedMetersPerSecond!.Value) || best.OutgoingSpeedMetersPerSecond.Value <= 0f)
            throw new InvalidDataException($"Shot '{shot.Name}' produced an invalid best impact against '{delivery.Name}'.");

        Console.WriteLine($"{shot.Name}: best quality {best.ContactQuality:0.00} at {best.InputDelaySeconds:+0.000;-0.000;0.000} s; {best.Outcome}, {best.OutgoingSpeedMetersPerSecond:0.0} m/s");
    }

    Console.WriteLine($"Real-asset batting practice passed for {delivery.Name}.");
}

static void SimulateMatchBatch(
    TeamRosterAsset firstTeam,
    TeamRosterAsset secondTeam,
    int matchCount,
    int oversPerInnings,
    int seed,
    string outputPath)
{
    var results = AutomatedMatchBatchSimulator.RunBatch(firstTeam, secondTeam, matchCount, oversPerInnings, seed);
    WriteMatchBatchCsv(firstTeam, secondTeam, results, outputPath);

    var firstTeamWins = results.Count(result => result.FirstInnings.Runs > result.SecondInnings.Runs);
    var secondTeamWins = results.Count(result => result.SecondInnings.Runs > result.FirstInnings.Runs);
    var ties = results.Count - firstTeamWins - secondTeamWins;
    var averageRuns = results.Average(result => result.FirstInnings.Runs + result.SecondInnings.Runs);
    Console.WriteLine($"Completed {results.Count} seeded synthetic matches at {oversPerInnings} overs per innings (seed {seed} onward).");
    Console.WriteLine($"{firstTeam.Name} wins {firstTeamWins}; {secondTeam.Name} wins {secondTeamWins}; ties {ties}; average combined score {averageRuns:0.0} runs.");
    Console.WriteLine($"Match results written to {Path.GetFullPath(outputPath)}");
}

static void SimulatePhysicsMatchBatch(
    TeamRosterAsset firstTeam,
    TeamRosterAsset secondTeam,
    FieldPreset fieldPreset,
    PlayerAsset batterAsset,
    PlayerAsset bowlerAsset,
    BattingShotSet shotSet,
    DeliveryPreset stockDelivery,
    DeliveryPreset wideDelivery,
    DeliveryPreset noBallDelivery,
    int matchCount,
    int oversPerInnings,
    int seed,
    string outputPath)
{
    var results = PhysicsAutomatedMatchBatchSimulator.RunBatch(
        firstTeam, secondTeam, fieldPreset, batterAsset, bowlerAsset, shotSet,
        stockDelivery, wideDelivery, noBallDelivery, matchCount, oversPerInnings, seed);
    WriteMatchBatchCsv(firstTeam, secondTeam, results, outputPath);

    var firstTeamWins = results.Count(result => result.FirstInnings.Runs > result.SecondInnings.Runs);
    var secondTeamWins = results.Count(result => result.SecondInnings.Runs > result.FirstInnings.Runs);
    var ties = results.Count - firstTeamWins - secondTeamWins;
    var averageRuns = results.Average(result => result.FirstInnings.Runs + result.SecondInnings.Runs);
    var physicsMetrics = results.Where(result => result.PhysicsMetrics.HasValue)
        .Select(result => result.PhysicsMetrics!.Value)
        .ToArray();
    Console.WriteLine($"Completed {results.Count} physics-grounded matches at {oversPerInnings} overs per innings (seed {seed} onward).");
    Console.WriteLine($"{firstTeam.Name} wins {firstTeamWins}; {secondTeam.Name} wins {secondTeamWins}; ties {ties}; average combined score {averageRuns:0.0} runs.");
    if (physicsMetrics.Length > 0)
    {
        Console.WriteLine($"Physics events: {physicsMetrics.Sum(item => item.Contacts)} contacts, {physicsMetrics.Sum(item => item.Misses)} misses, {physicsMetrics.Sum(item => item.Leaves)} leaves, {physicsMetrics.Sum(item => item.Catches)} catches, {physicsMetrics.Sum(item => item.GroundPickups)} pickups, {physicsMetrics.Sum(item => item.Boundaries)} boundaries.");
        Console.WriteLine($"Running: {physicsMetrics.Sum(item => item.RunIntents)} intents, {physicsMetrics.Sum(item => item.SafeRunAttempts)} safe runs, {physicsMetrics.Sum(item => item.TwoRunPlans)} double plans, {physicsMetrics.Sum(item => item.TwoRunScores)} doubles scored, {physicsMetrics.Sum(item => item.RunOuts)} run-outs.");
    }
    Console.WriteLine($"Match results written to {Path.GetFullPath(outputPath)}");
}

static void WriteMatchBatchCsv(
    TeamRosterAsset firstTeam,
    TeamRosterAsset secondTeam,
    IReadOnlyList<AutomatedMatchResult> results,
    string outputPath)
{
    var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
    if (!string.IsNullOrEmpty(outputDirectory))
        Directory.CreateDirectory(outputDirectory);

    using (var writer = new StreamWriter(outputPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
    {
        writer.WriteLine("seed,first_team,first_runs,first_wickets,first_legal_balls,second_team,second_runs,second_wickets,second_legal_balls,result,total_deliveries,shot_plans,contacts,misses,catches,ground_pickups,boundaries,run_intents,safe_run_attempts,two_run_plans,two_run_scores,completed_runs,run_outs,bowled_dismissals,wide_deliveries,no_ball_deliveries,leaves");
        foreach (var result in results)
        {
            var physics = result.PhysicsMetrics ?? default;
            writer.WriteLine(string.Join(',',
                result.Seed.ToString(CultureInfo.InvariantCulture),
                CsvValue(firstTeam.Name),
                result.FirstInnings.Runs.ToString(CultureInfo.InvariantCulture),
                result.FirstInnings.Wickets.ToString(CultureInfo.InvariantCulture),
                result.FirstInnings.LegalBalls.ToString(CultureInfo.InvariantCulture),
                CsvValue(secondTeam.Name),
                result.SecondInnings.Runs.ToString(CultureInfo.InvariantCulture),
                result.SecondInnings.Wickets.ToString(CultureInfo.InvariantCulture),
                result.SecondInnings.LegalBalls.ToString(CultureInfo.InvariantCulture),
                CsvValue(result.ResultText),
                result.TotalDeliveries.ToString(CultureInfo.InvariantCulture),
                physics.ShotPlans.ToString(CultureInfo.InvariantCulture),
                physics.Contacts.ToString(CultureInfo.InvariantCulture),
                physics.Misses.ToString(CultureInfo.InvariantCulture),
                physics.Catches.ToString(CultureInfo.InvariantCulture),
                physics.GroundPickups.ToString(CultureInfo.InvariantCulture),
                physics.Boundaries.ToString(CultureInfo.InvariantCulture),
                physics.RunIntents.ToString(CultureInfo.InvariantCulture),
                physics.SafeRunAttempts.ToString(CultureInfo.InvariantCulture),
                physics.TwoRunPlans.ToString(CultureInfo.InvariantCulture),
                physics.TwoRunScores.ToString(CultureInfo.InvariantCulture),
                physics.CompletedRuns.ToString(CultureInfo.InvariantCulture),
                physics.RunOuts.ToString(CultureInfo.InvariantCulture),
                physics.BowledDismissals.ToString(CultureInfo.InvariantCulture),
                physics.WideDeliveries.ToString(CultureInfo.InvariantCulture),
                physics.NoBallDeliveries.ToString(CultureInfo.InvariantCulture),
                physics.Leaves.ToString(CultureInfo.InvariantCulture)));
        }
    }

}

static void VerifyFootwork(string batterPath, string bowlerPath, string shotSetPath, string wideDeliveryPath)
{
    var targetOffset = 0f;
    for (var step = 0; step < BatterFootwork.MaximumSteps + 2; step++)
        targetOffset = BatterFootwork.AddStep(targetOffset, 1f);
    var halfwayOffset = BatterFootwork.Advance(0f, targetOffset, 0.25f);
    var completedOffset = BatterFootwork.Advance(halfwayOffset, targetOffset, 1f);
    if (MathF.Abs(targetOffset - BatterFootwork.MaximumOffsetMeters) > 0.0001f ||
        MathF.Abs(halfwayOffset - 1.125f) > 0.0001f ||
        MathF.Abs(completedOffset - targetOffset) > 0.0001f ||
        MathF.Abs(BatterFootwork.AddStep(0f, -1f) + BatterFootwork.StepDistanceMeters) > 0.0001f)
        throw new InvalidDataException("Batter footwork did not respect its step size, movement speed, or lateral limits.");

    var batter = PlayerAsset.Load(batterPath);
    var bowler = PlayerAsset.Load(bowlerPath);
    var shotSet = BattingShotSet.Load(shotSetPath);
    var wideDelivery = DeliveryPreset.Load(wideDeliveryPath);
    var samples = BattingPracticeAnalyzer.Analyze(batter, bowler, shotSet, wideDelivery);

    foreach (var shot in shotSet.Shots)
    {
        var contacts = samples.Where(sample =>
            string.Equals(sample.ShotName, shot.Name, StringComparison.OrdinalIgnoreCase) &&
            sample.ContactQuality.HasValue && MathF.Abs(sample.FootworkOffsetMeters) > 0.0001f).ToArray();
        if (contacts.Length == 0)
            throw new InvalidDataException($"Shot '{shot.Name}' found no contact against '{wideDelivery.Name}' using a batter step.");
        if (contacts.Any(sample => MathF.Abs(sample.FootworkOffsetMeters) > BatterFootwork.MaximumOffsetMeters + 0.0001f ||
            !sample.ContactTimeSeconds.HasValue ||
            sample.ContactTimeSeconds.Value < MathF.Max(0f, sample.InputDelaySeconds) - 0.000001f))
            throw new InvalidDataException($"Shot '{shot.Name}' used invalid footwork or contacted before input.");

        var best = contacts.OrderByDescending(sample => sample.ContactQuality).First();
        Console.WriteLine($"{shot.Name}: {contacts.Length} wide contacts with a {best.FootworkOffsetMeters:+0.00;-0.00;0.00} m step; best quality {best.ContactQuality:0.00} at {best.InputDelaySeconds:+0.000;-0.000;0.000} s.");
    }

    Console.WriteLine($"Wide-ball footwork checks passed for {wideDelivery.Name}.");
}

static string F(float value) => value.ToString("0.000000", CultureInfo.InvariantCulture);
static string Optional(float? value) => value is { } number ? F(number) : string.Empty;

static void PrintUsage()
{
    Console.WriteLine("Super Cricket tools");
    Console.WriteLine("  validate-player <player.scplayer.json>");
    Console.WriteLine("  validate-team <team.json>");
    Console.WriteLine("  validate-shots <shots.json>");
    Console.WriteLine("  analyze-batting <shots.json> [impact-grid.csv]");
    Console.WriteLine("  analyze-batting-practice <batter.scplayer.json> <bowler.scplayer.json> <shots.json> <delivery.json> [results.csv] [input-step-seconds]");
    Console.WriteLine("  verify-batting-practice <batter.scplayer.json> <bowler.scplayer.json> <shots.json> <delivery.json>");
    Console.WriteLine("  verify-cpu-batting <batter.scplayer.json> <bowler.scplayer.json> <shots.json> <standard-delivery.json> <wide-delivery.json> <batting-team.json> <fielding-team.json> <field.json>");
    Console.WriteLine("  verify-batting <shots.json>");
    Console.WriteLine("  validate-field <field.json>");
    Console.WriteLine("  analyze-field <field.json> [coverage.csv] [grid-spacing-meters]");
    Console.WriteLine("  validate <preset.json>");
    Console.WriteLine("  simulate <preset.json> [trajectory.csv]");
    Console.WriteLine("  simulate-over <over-scenario.json>");
    Console.WriteLine("  simulate-match-batch <first-team.json> <second-team.json> <count> <overs> <seed> [results.csv]");
    Console.WriteLine("  simulate-physics-match-batch <first-team.json> <second-team.json> <field.json> <batter.scplayer.json> <bowler.scplayer.json> <shots.json> <standard-delivery.json> <wide-delivery.json> <no-ball-delivery.json> <count> <overs> <seed> [results.csv]");
    Console.WriteLine("  verify-match");
    Console.WriteLine("  verify-match-batch");
    Console.WriteLine("  verify-fielding");
    Console.WriteLine("  verify-footwork <batter.scplayer.json> <bowler.scplayer.json> <shots.json> <wide-delivery.json>");
}

internal sealed class OverScenarioDocument
{
    public string Name { get; set; } = string.Empty;
    public List<DeliveryResult> Deliveries { get; set; } = [];
}
