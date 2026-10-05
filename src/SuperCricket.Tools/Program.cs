using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
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
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or JsonException)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

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
    Console.WriteLine("  validate <preset.json>");
    Console.WriteLine("  simulate <preset.json> [trajectory.csv]");
}
