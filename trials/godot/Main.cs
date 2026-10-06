using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using SuperCricket.Content;
using SuperCricket.Simulation;

public partial class Main : Node3D
{
    private const float ShotInputDelaySeconds = 0.25f;
    private const float BallDisplayRadiusMeters = 0.11f;
    private const string ShotName = "drive";
    private const string ShotAnimation = "front-foot-drive";

    private BallFlightSimulator _flight = null!;
    private DeliveryPreset _preset = null!;
    private BattingPracticeTrajectory _trajectory;
    private MeshInstance3D _ball = null!;
    private MeshInstance3D _trail = null!;
    private ImmediateMesh _trailMesh = null!;
    private StandardMaterial3D _trailMaterial = null!;
    private AnimationPlayer? _batterAnimation;
    private Camera3D _playerCamera = null!;
    private Camera3D _broadcastCamera = null!;
    private Camera3D _shotCamera = null!;
    private PanelContainer _feedbackPanel = null!;
    private Label _feedbackTitle = null!;
    private Label _feedbackDetail = null!;
    private Label _phaseLabel = null!;
    private Label _cameraNameLabel = null!;
    private readonly List<Vector3> _trailPoints = [];
    private readonly List<BallFlightFrame> _outgoingFrames = [];
    private string _capturePhase = string.Empty;
    private string _repositoryRoot = string.Empty;
    private double _accumulator;
    private double _postResultElapsed;
    private float _captureDelay = -1f;
    private int _outgoingIndex;
    private bool _shotInputStarted;
    private bool _contactSeen;
    private bool _bounceSeen;
    private bool _sequenceComplete;
    private bool _broadcastView;
    private bool _shotCameraActive;
    private bool _manualCameraOverride;

    public override void _Ready()
    {
        var projectRoot = ProjectSettings.GlobalizePath("res://");
        _repositoryRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", ".."));
        _capturePhase = ReadCapturePhase();

        _preset = DeliveryPreset.Load(Path.Combine(_repositoryRoot, "assets", "deliveries", "standard-pace.json"));
        _flight = new BallFlightSimulator(_preset);
        var shotSet = BattingShotSet.Load(Path.Combine(_repositoryRoot, "assets", "batting", "shots.json"));
        var batter = PlayerAsset.Load(Path.Combine(_repositoryRoot, "assets", "characters", "practice-batter.scplayer.json"));
        var bowler = PlayerAsset.Load(Path.Combine(_repositoryRoot, "assets", "characters", "practice-bowler.scplayer.json"));
        _trajectory = BattingPracticeAnalyzer.AnalyzeShotTrajectory(
            batter,
            bowler,
            shotSet,
            ShotName,
            _preset,
            ShotInputDelaySeconds,
            footworkOffsetMeters: 0f);

        if (_trajectory.ContactPosition is not { } contact || _trajectory.OutgoingFrames.Count < 2)
            throw new InvalidDataException("The shared batting simulation could not produce the calibrated front-foot drive.");
        if (_trajectory.Sample.Outcome is not ("Four" or "Six" or "InPlay"))
            throw new InvalidDataException($"The shared trial drive returned an unsupported outcome '{_trajectory.Sample.Outcome}'.");

        GD.Print($"Godot loaded {_preset.Name}; shared batting Simulation found {ShotName} contact at " +
            $"{_trajectory.Sample.ContactTimeSeconds:0.000}s with quality {_trajectory.Sample.ContactQuality:0.00}, " +
            $"then {_trajectory.Sample.Outcome}; contact={_trajectory.ContactPosition}, outgoing={_trajectory.OutgoingVelocity}, " +
            $"final-frame={_trajectory.OutgoingFrames[^1].Position}.");

        TrialStadiumBuilder.Build(this, _preset);
        BuildPlayer();
        BuildBallAndTrail();
        BuildCameras();
        BuildLightingAndPostProcessing();
        BuildFeedbackOverlay();
        ResetDelivery();
    }

    public override void _Process(double delta)
    {
        if (_sequenceComplete)
        {
            _postResultElapsed += delta;
            if (_capturePhase.Length == 0 && _postResultElapsed >= 4.5d)
                ResetDelivery();
        }
        else
        {
            _accumulator += Math.Min(delta, 0.25d);
            while (_accumulator >= _preset.FixedTimeStepSeconds)
            {
                if (_contactSeen)
                    AdvanceOutgoingBall();
                else
                    AdvanceIncomingBall();
                _accumulator -= _preset.FixedTimeStepSeconds;
                if (_sequenceComplete)
                    break;
            }
        }

        AdvanceCapture(delta);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
            return;

        if (keyEvent.Keycode == Key.C)
            ToggleCamera();
        else if (keyEvent.Keycode == Key.R)
            ResetDelivery();
    }

    private void BuildPlayer()
    {
        var playerScene = GD.Load<PackedScene>("res://assets/practice-batter.glb")
            ?? throw new InvalidDataException("The Blender-exported practice batter GLB could not be imported by Godot.");
        var player = playerScene.Instantiate<Node3D>();
        player.Name = "PracticeBatterGLTF";
        player.Position = new Vector3(-0.48f, 0f, BattingPracticeAnalyzer.BatterWicketLineZ);
        AddChild(player);

        _batterAnimation = FindAnimationPlayer(player);
        if (_batterAnimation is null)
            throw new InvalidDataException("The practice batter GLB did not import an AnimationPlayer.");

        var animationNames = _batterAnimation.GetAnimationList().Select(name => name.ToString()).ToArray();
        GD.Print($"Imported Blender batter animations: {string.Join(", ", animationNames)}");
        if (!animationNames.Contains("practice-stance", StringComparer.Ordinal))
            throw new InvalidDataException("The practice batter GLB is missing its practice-stance animation.");
        if (!animationNames.Contains(ShotAnimation, StringComparer.Ordinal))
            throw new InvalidDataException($"The practice batter GLB is missing its '{ShotAnimation}' animation.");
        _batterAnimation.Play("practice-stance");
    }

    private void BuildBallAndTrail()
    {
        var ballMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.78f, 0.035f, 0.06f),
            Roughness = 0.28f,
            EmissionEnabled = true,
            Emission = new Color(0.18f, 0.006f, 0.009f),
            EmissionEnergyMultiplier = 0.9f
        };
        _ball = new MeshInstance3D
        {
            Name = "SimulationBall",
            Mesh = new SphereMesh { Radius = BallDisplayRadiusMeters, Height = BallDisplayRadiusMeters * 2f, RadialSegments = 24, Rings = 16 },
            MaterialOverride = ballMaterial
        };
        AddChild(_ball);

        _trailMesh = new ImmediateMesh();
        _trailMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.94f, 0.97f, 0.79f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            EmissionEnabled = true,
            Emission = new Color(0.52f, 0.67f, 0.24f),
            EmissionEnergyMultiplier = 0.7f
        };
        _trail = new MeshInstance3D { Name = "BallFlightTrail", Mesh = _trailMesh };
        AddChild(_trail);
    }

    private void BuildCameras()
    {
        _playerCamera = new Camera3D
        {
            Name = "BehindStrikerCamera",
            Position = new Vector3(1.2f, 3.6f, -16.2f),
            Current = true,
            Fov = 51f,
            Far = 320f
        };
        AddChild(_playerCamera);
        _playerCamera.LookAt(new Vector3(0f, 0.1f, 0f), Vector3.Up);

        _broadcastCamera = new Camera3D
        {
            Name = "HighBroadcastCamera",
            Position = new Vector3(0f, 96f, 156f),
            Fov = 56f,
            Far = 360f
        };
        AddChild(_broadcastCamera);
        _broadcastCamera.LookAt(new Vector3(0f, 8f, 0f), Vector3.Up);

        _shotCamera = new Camera3D
        {
            Name = "BattedBallFollowCamera",
            Position = new Vector3(24f, 14f, -10f),
            Fov = 55f,
            Far = 320f
        };
        AddChild(_shotCamera);
        _shotCamera.LookAt(new Vector3(0f, 1f, -15f), Vector3.Up);
    }

    private void BuildLightingAndPostProcessing()
    {
        var sky = new Sky
        {
            SkyMaterial = new ProceduralSkyMaterial
            {
                SkyTopColor = new Color(0.07f, 0.16f, 0.30f),
                SkyHorizonColor = new Color(0.48f, 0.62f, 0.73f),
                GroundBottomColor = new Color(0.11f, 0.16f, 0.13f),
                GroundHorizonColor = new Color(0.55f, 0.62f, 0.54f),
                SunAngleMax = 18f,
                UseDebanding = true
            }
        };
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = sky,
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = 0.48f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            GlowEnabled = true,
            GlowIntensity = 0.42f,
            GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Screen,
            FogEnabled = true,
            FogLightColor = new Color(0.62f, 0.70f, 0.70f),
            FogDensity = 0.00055f,
            FogDepthBegin = 70f,
            FogDepthEnd = 220f,
            FogDepthCurve = 1.2f
        };
        environment.SetGlowLevel(1, 0f);
        environment.SetGlowLevel(2, 0.5f);
        environment.SetGlowLevel(3, 0.25f);
        environment.SetGlowLevel(4, 0.08f);
        AddChild(new WorldEnvironment { Name = "BroadcastEnvironment", Environment = environment });
        GetViewport().World3D.Environment = environment;
        GD.Print($"Godot trial environment: {environment.BackgroundMode}, glow={environment.GlowEnabled}, fog={environment.FogEnabled}, " +
            $"viewport-bound={GetViewport().World3D.Environment == environment}.");

        var sun = new DirectionalLight3D
        {
            Name = "LateAfternoonSun",
            RotationDegrees = new Vector3(-43f, -31f, 0f),
            LightColor = new Color(1f, 0.82f, 0.63f),
            LightEnergy = 1.3f,
            ShadowEnabled = true,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits,
            DirectionalShadowMaxDistance = 175f,
            DirectionalShadowFadeStart = 0.82f
        };
        AddChild(sun);
    }

    private void BuildFeedbackOverlay()
    {
        var overlay = new CanvasLayer { Name = "BroadcastHUD" };
        var topLine = new Label
        {
            Name = "TrialTitle",
            AnchorLeft = 0f,
            AnchorRight = 0f,
            AnchorTop = 0f,
            AnchorBottom = 0f,
            OffsetLeft = 26f,
            OffsetTop = 20f,
            OffsetRight = 580f,
            OffsetBottom = 54f,
            Text = "SUPER CRICKET  /  ENGINE TRIAL"
        };
        topLine.AddThemeFontSizeOverride("font_size", 18);
        topLine.AddThemeColorOverride("font_color", new Color(0.88f, 0.95f, 0.91f));
        topLine.AddThemeColorOverride("font_shadow_color", new Color(0.02f, 0.07f, 0.08f, 0.9f));
        topLine.AddThemeConstantOverride("shadow_offset_x", 2);
        topLine.AddThemeConstantOverride("shadow_offset_y", 2);
        overlay.AddChild(topLine);

        _phaseLabel = new Label
        {
            Name = "DeliveryReadout",
            AnchorLeft = 1f,
            AnchorRight = 1f,
            AnchorTop = 0f,
            AnchorBottom = 0f,
            OffsetLeft = -430f,
            OffsetTop = 20f,
            OffsetRight = -26f,
            OffsetBottom = 54f,
            HorizontalAlignment = HorizontalAlignment.Right,
            Text = $"STANDARD PACE  ·  {_preset.StartVelocity.Length() * 3.6f:0} KM/H"
        };
        _phaseLabel.AddThemeFontSizeOverride("font_size", 18);
        _phaseLabel.AddThemeColorOverride("font_color", new Color(0.98f, 0.89f, 0.69f));
        _phaseLabel.AddThemeColorOverride("font_shadow_color", new Color(0.02f, 0.07f, 0.08f, 0.9f));
        _phaseLabel.AddThemeConstantOverride("shadow_offset_x", 2);
        _phaseLabel.AddThemeConstantOverride("shadow_offset_y", 2);
        overlay.AddChild(_phaseLabel);

        _feedbackPanel = new PanelContainer
        {
            Name = "PlayerFeedback",
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.12f,
            AnchorBottom = 0.12f,
            OffsetLeft = -355f,
            OffsetTop = 0f,
            OffsetRight = 355f,
            OffsetBottom = 116f,
            Visible = true
        };
        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.025f, 0.09f, 0.105f, 0.91f),
            BorderColor = new Color(0.44f, 0.82f, 0.48f),
            BorderWidthLeft = 3,
            BorderWidthRight = 3,
            BorderWidthTop = 3,
            BorderWidthBottom = 4,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            ContentMarginLeft = 20f,
            ContentMarginRight = 20f,
            ContentMarginTop = 10f,
            ContentMarginBottom = 10f
        };
        _feedbackPanel.AddThemeStyleboxOverride("panel", panelStyle);

        var feedbackStack = new VBoxContainer { Name = "FeedbackStack", Alignment = BoxContainer.AlignmentMode.Center };
        _feedbackTitle = new Label
        {
            Name = "FeedbackTitle",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Text = "DELIVERY IN PLAY"
        };
        _feedbackTitle.AddThemeFontSizeOverride("font_size", 32);
        _feedbackTitle.AddThemeColorOverride("font_color", Colors.White);
        _feedbackTitle.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.9f));
        _feedbackTitle.AddThemeConstantOverride("shadow_offset_x", 2);
        _feedbackTitle.AddThemeConstantOverride("shadow_offset_y", 2);
        _feedbackDetail = new Label
        {
            Name = "FeedbackDetail",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Text = "Watch the bounce · front-foot drive is timed at +0.25 s"
        };
        _feedbackDetail.AddThemeFontSizeOverride("font_size", 19);
        _feedbackDetail.AddThemeColorOverride("font_color", new Color(0.83f, 0.92f, 0.88f));
        feedbackStack.AddChild(_feedbackTitle);
        feedbackStack.AddChild(_feedbackDetail);
        _feedbackPanel.AddChild(feedbackStack);
        overlay.AddChild(_feedbackPanel);

        var controls = new Label
        {
            Name = "KeyboardControls",
            AnchorLeft = 0f,
            AnchorRight = 0f,
            AnchorTop = 1f,
            AnchorBottom = 1f,
            OffsetLeft = 26f,
            OffsetTop = -50f,
            OffsetRight = 600f,
            OffsetBottom = -20f,
            Text = "C  CAMERA   ·   R  REPLAY"
        };
        controls.AddThemeFontSizeOverride("font_size", 16);
        controls.AddThemeColorOverride("font_color", new Color(0.91f, 0.95f, 0.89f));
        controls.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.9f));
        controls.AddThemeConstantOverride("shadow_offset_x", 2);
        controls.AddThemeConstantOverride("shadow_offset_y", 2);
        overlay.AddChild(controls);

        _cameraNameLabel = new Label
        {
            Name = "CameraName",
            AnchorLeft = 1f,
            AnchorRight = 1f,
            AnchorTop = 1f,
            AnchorBottom = 1f,
            OffsetLeft = -450f,
            OffsetTop = -50f,
            OffsetRight = -26f,
            OffsetBottom = -20f,
            HorizontalAlignment = HorizontalAlignment.Right,
            Text = "BEHIND THE STRIKER"
        };
        _cameraNameLabel.AddThemeFontSizeOverride("font_size", 16);
        _cameraNameLabel.AddThemeColorOverride("font_color", new Color(0.91f, 0.95f, 0.89f));
        overlay.AddChild(_cameraNameLabel);
        AddChild(overlay);
    }

    private void AdvanceIncomingBall()
    {
        var frame = _flight.Step();
        _ball.Position = ToVisualBallPosition(frame.Position);
        AddTrailPoint(_ball.Position);

        if (!_shotInputStarted && frame.TimeSeconds >= ShotInputDelaySeconds)
        {
            _shotInputStarted = true;
            if (_batterAnimation is not null)
                _batterAnimation.Play(ShotAnimation, customBlend: 0.12d);
            SetFeedback("FRONT-FOOT DRIVE", "SHOT INPUT  +0.25 s  ·  SHARED BAT/SWING CONTACT MODEL", new Color(0.55f, 0.88f, 0.62f));
        }

        if (!_bounceSeen && frame.BounceCount > 0)
        {
            _bounceSeen = true;
            SetFeedback("GOOD LENGTH", "BOUNCE POINT  ·  THE BATTER IS SET TO DRIVE", new Color(0.97f, 0.81f, 0.36f));
        }

        if (frame.TimeSeconds >= _trajectory.Sample.ContactTimeSeconds!.Value)
        {
            _contactSeen = true;
            _outgoingFrames.Clear();
            _outgoingFrames.AddRange(_trajectory.OutgoingFrames);
            _outgoingIndex = 0;
            _trailPoints.Clear();
            _trailMesh.ClearSurfaces();
            _ball.Position = ToVisualBallPosition(_trajectory.ContactPosition!.Value);
            SetFeedback("GOOD CONTACT  ·  DRIVE", $"TIMING: ON TIME  ·  SWEET SPOT {(_trajectory.Sample.ContactQuality ?? 0f):0.00}", new Color(0.49f, 0.94f, 0.62f));
            _phaseLabel.Text = "BALL OFF THE BAT  ·  WATCH THE FLIGHT";
        }
        else if (frame.Phase == BallMotionPhase.Settled)
        {
            throw new InvalidDataException("The incoming trial delivery settled before the calibrated bat contact.");
        }
    }

    private void AdvanceOutgoingBall()
    {
        if (_outgoingIndex >= _outgoingFrames.Count)
        {
            CompleteSequence();
            return;
        }

        var frame = _outgoingFrames[_outgoingIndex++];
        _ball.Position = ToVisualBallPosition(frame.Position);
        AddTrailPoint(_ball.Position);
        if (!_shotCameraActive && !_manualCameraOverride && _capturePhase != "contact")
        {
            _shotCameraActive = true;
            _broadcastView = false;
            _shotCamera.MakeCurrent();
            _cameraNameLabel.Text = "BALL FOLLOW";
            GD.Print($"Godot camera changed to {_shotCamera.Name} at ball {_ball.Position}.");
        }
        if (_shotCameraActive)
        {
            var shotDirection = ToGodot(_trajectory.OutgoingVelocity!.Value).Normalized();
            var sideOffset = new Vector3(-shotDirection.Z, 0f, shotDirection.X).Normalized();
            _shotCamera.Position = _ball.Position - shotDirection * 6f + sideOffset * 18f + Vector3.Up * 11f;
            _shotCamera.LookAt(_ball.Position + Vector3.Up * 0.4f, Vector3.Up);
        }
        if (frame.Phase == BallMotionPhase.Settled || _outgoingIndex >= _outgoingFrames.Count)
            CompleteSequence();
    }

    private void CompleteSequence()
    {
        if (_sequenceComplete)
            return;
        _sequenceComplete = true;
        _postResultElapsed = 0d;
        var distance = new Vector2(_trajectory.ContactPosition!.Value.X, _trajectory.ContactPosition.Value.Z)
            .DistanceTo(new Vector2(_ball.Position.X, _ball.Position.Z));
        var result = _trajectory.Sample.Outcome switch
        {
            "Four" => (Title: "FOUR  ·  BOUNDARY", Detail: $"{distance:0} m FROM CONTACT  ·  THE SHARED SIMULATION CLEARED THE ROPE", Accent: new Color(1f, 0.83f, 0.35f), Phase: "RESULT  ·  FOUR"),
            "Six" => (Title: "SIX  ·  OVER THE ROPE", Detail: $"{distance:0} m FROM CONTACT  ·  THE SHARED SIMULATION CLEARED THE ROPE IN THE AIR", Accent: new Color(1f, 0.83f, 0.35f), Phase: "RESULT  ·  SIX"),
            _ => (Title: "IN PLAY  ·  BALL SETTLED", Detail: $"{distance:0} m FROM CONTACT  ·  THE SHARED SIMULATION STOPPED INSIDE THE ROPE", Accent: new Color(0.53f, 0.89f, 0.68f), Phase: "RESULT  ·  BALL IN PLAY")
        };
        SetFeedback(result.Title, result.Detail, result.Accent);
        _phaseLabel.Text = result.Phase;
        GD.Print($"Godot result: {_trajectory.Sample.Outcome}, {distance:0.0} m from bat contact; " +
            $"{_outgoingFrames.Count} shared post-contact frames replayed.");
    }

    private void ResetDelivery()
    {
        _flight = new BallFlightSimulator(_preset);
        _accumulator = 0d;
        _postResultElapsed = 0d;
        _outgoingIndex = 0;
        _shotInputStarted = false;
        _contactSeen = false;
        _bounceSeen = false;
        _sequenceComplete = false;
        _shotCameraActive = false;
        _manualCameraOverride = false;
        _broadcastView = _capturePhase == "wide";
        _outgoingFrames.Clear();
        _trailPoints.Clear();
        _trailMesh?.ClearSurfaces();
        if (_ball is not null)
            _ball.Position = ToVisualBallPosition(_flight.CurrentFrame.Position);
        if (_batterAnimation is not null && _batterAnimation.HasAnimation("practice-stance"))
            _batterAnimation.Play("practice-stance");
        if (_broadcastView)
            _broadcastCamera.MakeCurrent();
        else if (_playerCamera is not null)
            _playerCamera.MakeCurrent();
        if (_cameraNameLabel is not null)
            _cameraNameLabel.Text = _broadcastView ? "HIGH BROADCAST" : "BEHIND THE STRIKER";
        if (_feedbackTitle is not null)
        {
            SetFeedback("DELIVERY IN PLAY", "WATCH THE BOUNCE  ·  FRONT-FOOT DRIVE AT +0.25 s", new Color(0.64f, 0.85f, 0.97f));
            _phaseLabel.Text = $"STANDARD PACE  ·  {_preset.StartVelocity.Length() * 3.6f:0} KM/H";
        }
    }

    private void AddTrailPoint(Vector3 point)
    {
        _trailPoints.Add(point);
        if (_trailPoints.Count > 32)
            _trailPoints.RemoveAt(0);
        if (_trailPoints.Count < 2)
            return;

        _trailMesh.ClearSurfaces();
        _trailMesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip, _trailMaterial);
        foreach (var trailPoint in _trailPoints)
            _trailMesh.SurfaceAddVertex(trailPoint);
        _trailMesh.SurfaceEnd();
    }

    private void ToggleCamera()
    {
        _shotCameraActive = false;
        _manualCameraOverride = true;
        _broadcastView = !_broadcastView;
        if (_broadcastView)
            _broadcastCamera.MakeCurrent();
        else
            _playerCamera.MakeCurrent();

        if (_cameraNameLabel is not null)
            _cameraNameLabel.Text = _broadcastView ? "HIGH BROADCAST" : "BEHIND THE STRIKER";
    }

    private void SetFeedback(string title, string detail, Color accent)
    {
        if (_feedbackTitle is null)
            return;
        _feedbackTitle.Text = title;
        _feedbackDetail.Text = detail;
        var style = _feedbackPanel.GetThemeStylebox("panel") as StyleBoxFlat;
        if (style is not null)
        {
            var updated = (StyleBoxFlat)style.Duplicate();
            updated.BorderColor = accent;
            _feedbackPanel.AddThemeStyleboxOverride("panel", updated);
        }
    }

    private void AdvanceCapture(double delta)
    {
        if (_capturePhase.Length == 0)
            return;
        var targetReached = _capturePhase switch
        {
            "wide" => _broadcastView,
            "contact" => _contactSeen,
            "result" => _sequenceComplete,
            _ => false
        };
        if (!targetReached)
            return;

        if (_captureDelay < 0f)
            _captureDelay = 0.4f;
        _captureDelay -= (float)delta;
        if (_captureDelay > 0f)
            return;

        var path = Path.Combine(_repositoryRoot, "artifacts", $"godot-b3-{_capturePhase}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        GD.Print($"Godot capture state: phase={_capturePhase}, shot-camera={_shotCameraActive}, current={GetViewport().GetCamera3D()?.Name}.");
        var error = GetViewport().GetTexture().GetImage().SavePng(path);
        if (error != Error.Ok)
            throw new IOException($"Could not save Godot B3 capture '{path}': {error}.");
        GD.Print($"Godot B3 screenshot saved: {path}");
        GetTree().Quit();
        _capturePhase = string.Empty;
    }

    private static string ReadCapturePhase()
    {
        foreach (var argument in OS.GetCmdlineUserArgs())
        {
            if (argument.StartsWith("--capture=", StringComparison.Ordinal))
                return argument[10..].ToLowerInvariant();
        }
        return string.Empty;
    }

    private static AnimationPlayer? FindAnimationPlayer(Node node)
    {
        if (node is AnimationPlayer player)
            return player;
        foreach (var child in node.GetChildren())
        {
            if (FindAnimationPlayer(child) is { } found)
                return found;
        }
        return null;
    }

    private Vector3 ToVisualBallPosition(System.Numerics.Vector3 value) =>
        ToGodot(value) + Vector3.Up * (BallDisplayRadiusMeters - _preset.BallRadiusMeters);

    private static Vector3 ToGodot(System.Numerics.Vector3 value) => new(value.X, value.Y, value.Z);
}
