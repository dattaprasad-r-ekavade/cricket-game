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
    if (arguments.Length < 2 || arguments[0] is "help" or "--help" or "-h")
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
                Console.WriteLine($"  {animation.Name}: {animation.DurationSeconds:0.00} s, {animation.Samples.Count} sampled poses.");
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

static string CsvValue(string value) => value.Contains(',') || value.Contains('"')
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

static string F(float value) => value.ToString("0.000000", CultureInfo.InvariantCulture);

static void PrintUsage()
{
    Console.WriteLine("Super Cricket tools");
    Console.WriteLine("  validate-player <player.scplayer.json>");
    Console.WriteLine("  validate-shots <shots.json>");
    Console.WriteLine("  validate-field <field.json>");
    Console.WriteLine("  analyze-field <field.json> [coverage.csv] [grid-spacing-meters]");
    Console.WriteLine("  validate <preset.json>");
    Console.WriteLine("  simulate <preset.json> [trajectory.csv]");
    Console.WriteLine("  simulate-over <over-scenario.json>");
}

internal sealed class OverScenarioDocument
{
    public string Name { get; set; } = string.Empty;
    public List<DeliveryResult> Deliveries { get; set; } = [];
}
