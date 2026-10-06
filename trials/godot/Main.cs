using System;
using System.IO;
using Godot;
using SuperCricket.Content;
using SuperCricket.Simulation;

public partial class Main : Node3D
{
    private BallFlightSimulator _flight = null!;
    private DeliveryPreset _preset = null!;
    private MeshInstance3D _ball = null!;
    private MeshInstance3D _actualBounceMarker = null!;
    private Label _status = null!;
    private double _accumulator;
    private bool _bounceSeen;

    public override void _Ready()
    {
        var projectRoot = ProjectSettings.GlobalizePath("res://");
        var repositoryRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", ".."));
        var presetPath = Path.Combine(repositoryRoot, "assets", "deliveries", "standard-pace.json");
        _preset = DeliveryPreset.Load(presetPath);
        _flight = new BallFlightSimulator(_preset);
        BuildTrialScene();

        var predictedBounce = BowlingAimModel.FindFirstBounce(_preset)
            ?? throw new InvalidDataException("The shared simulation did not find a delivery bounce.");
        GD.Print($"Godot trial loaded {_preset.Name} from shared Content; shared Simulation predicts first bounce " +
            $"at ({predictedBounce.Position.X:0.00}, {predictedBounce.Position.Y:0.00}, {predictedBounce.Position.Z:0.00}) m.");
    }

    public override void _Process(double delta)
    {
        _accumulator += Math.Min(delta, 0.25);
        while (_accumulator >= _flight.FixedTimeStepSeconds &&
               _flight.CurrentFrame.Phase != BallMotionPhase.Settled)
        {
            var frame = _flight.Step();
            _ball.Position = ToGodot(frame.Position);
            if (!_bounceSeen && frame.BounceCount > 0)
            {
                _bounceSeen = true;
                _actualBounceMarker.Visible = true;
                _status.Text = "FIRST BOUNCE — shared Simulation event received by Godot renderer";
            }
            _accumulator -= _flight.FixedTimeStepSeconds;
        }

        if (!_bounceSeen)
        {
            var frame = _flight.CurrentFrame;
            _status.Text = $"{_preset.Name} | {frame.Velocity.Length() * 3.6f:0} km/h | " +
                $"shared fixed step {_flight.FixedTimeStepSeconds * 1000f:0.00} ms";
        }
    }

    private void BuildTrialScene()
    {
        var field = new MeshInstance3D
        {
            Name = "Outfield",
            Mesh = new PlaneMesh { Size = new Vector2(90f, 90f) },
            Position = new Vector3(0f, _preset.FieldSurfaceHeightMeters, 0f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.13f, 0.36f, 0.18f) }
        };
        AddChild(field);

        var pitch = new MeshInstance3D
        {
            Name = "Pitch",
            Mesh = new PlaneMesh { Size = new Vector2(_preset.PitchWidthMeters, _preset.PitchLengthMeters) },
            Position = new Vector3(0f, _preset.PitchSurfaceHeightMeters, 0f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.54f, 0.36f, 0.23f) }
        };
        AddChild(pitch);

        var predictedBounce = BowlingAimModel.FindFirstBounce(_preset)
            ?? throw new InvalidDataException("The shared simulation did not find a delivery bounce.");
        var predictedSpot = ToGodot(predictedBounce.Position);
        var predictedMarker = new MeshInstance3D
        {
            Name = "PredictedBounce",
            Mesh = new CylinderMesh { TopRadius = 0.32f, BottomRadius = 0.32f, Height = 0.015f },
            Position = new Vector3(predictedSpot.X, _preset.PitchSurfaceHeightMeters + 0.015f, predictedSpot.Z),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.12f, 0.85f, 0.95f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            }
        };
        AddChild(predictedMarker);

        _actualBounceMarker = new MeshInstance3D
        {
            Name = "ActualBounce",
            Mesh = new SphereMesh { Radius = 0.22f, Height = 0.44f, RadialSegments = 16, Rings = 8 },
            Position = new Vector3(predictedSpot.X, _preset.PitchSurfaceHeightMeters + 0.22f, predictedSpot.Z),
            Visible = false,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.88f, 0.18f),
                EmissionEnabled = true,
                Emission = new Color(0.75f, 0.54f, 0.05f)
            }
        };
        AddChild(_actualBounceMarker);

        _ball = new MeshInstance3D
        {
            Name = "CricketBall",
            Mesh = new SphereMesh
            {
                Radius = _preset.BallRadiusMeters,
                Height = _preset.BallRadiusMeters * 2f,
                RadialSegments = 20,
                Rings = 12
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.76f, 0.08f, 0.10f),
                Roughness = 0.42f
            }
        };
        _ball.Position = ToGodot(_flight.CurrentFrame.Position);
        AddChild(_ball);

        AddChild(new DirectionalLight3D
        {
            Name = "Sun",
            RotationDegrees = new Vector3(-47f, -32f, 0f),
            ShadowEnabled = true,
            LightEnergy = 1.1f
        });

        var camera = new Camera3D
        {
            Name = "TrialCamera",
            Position = new Vector3(0f, 15f, 26f),
            Current = true,
            Fov = 47f
        };
        AddChild(camera);
        camera.LookAt(new Vector3(0f, 0f, 0f), Vector3.Up);

        var overlay = new CanvasLayer { Name = "TrialOverlay" };
        _status = new Label
        {
            Name = "SimulationStatus",
            Position = new Vector2(20f, 20f),
            Text = "Loading shared cricket simulation…"
        };
        _status.AddThemeFontSizeOverride("font_size", 22);
        overlay.AddChild(_status);

        var legend = new Label
        {
            Name = "SceneLegend",
            Position = new Vector2(20f, 56f),
            Text = "CYAN: predicted pitch point     YELLOW: actual bounce"
        };
        legend.AddThemeFontSizeOverride("font_size", 16);
        overlay.AddChild(legend);
        AddChild(overlay);
    }

    private static Vector3 ToGodot(System.Numerics.Vector3 value) => new(value.X, value.Y, value.Z);
}
