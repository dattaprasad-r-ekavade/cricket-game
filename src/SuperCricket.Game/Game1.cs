using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SuperCricket.Content;
using SuperCricket.Game.Animation;
using SuperCricket.Game.Rendering;
using SuperCricket.Simulation;
using NumericsVector3 = System.Numerics.Vector3;

namespace SuperCricket.Game;

public partial class Game1 : Microsoft.Xna.Framework.Game
{
    private DeliverySession CurrentDelivery => _match.CurrentDelivery
        ?? throw new InvalidOperationException("No delivery is active.");

    private enum FielderSequencePhase
    {
        None,
        Pickup,
        Throw
    }

    private readonly record struct FrameTiming(double FrameIntervalMilliseconds, double UpdateCpuMilliseconds, double DrawCpuMilliseconds);

    private readonly record struct BattingContact(
        Vector3 Position,
        Vector3 SweetSpotPosition,
        Vector2 NormalizedSweetSpotOffset,
        Vector3 BatPointVelocity,
        float HitFraction);

    private readonly GraphicsDeviceManager _graphics;
    private readonly string? _capturePath;
    private readonly bool _verifyGameplay;
    private readonly int _profileFrameTarget;
    private readonly float? _captureRunUpTimeSeconds;
    private readonly float? _captureDeliveryTimeSeconds;
    private readonly string? _captureFielderActionClip;
    private readonly float? _captureFielderActionTimeSeconds;
    private SpriteBatch _spriteBatch = null!;
    private SpriteFont _debugFont = null!;
    private Texture2D _debugPanel = null!;
    private BasicEffect _worldEffect = null!;
    private BasicEffect _crowdEffect = null!;
    private BasicEffect _surfaceEffect = null!;
    private BasicEffect _lineEffect = null!;
    private Texture2D _outfieldTexture = null!;
    private Texture2D _pitchTexture = null!;
    private VertexBuffer? _crowdVertexBuffer;
    private RenderTarget2D? _captureTarget;
    private readonly OrbitCamera _camera = new();
    private readonly List<VertexPositionColor> _trajectoryVertices = [];
    private readonly List<VertexPositionColor> _shadowVertices = [];
    private readonly VertexPositionColor[] _debugMarkerVertices = new VertexPositionColor[18];
    private readonly List<FrameTiming> _profileTimings = [];
    private DeliveryPreset[] _deliveryPresets = [];
    private int _nextDeliveryPresetIndex;
    private int _activeDeliveryPresetIndex;
    private int _matchBowlingSeed;
    private CpuDifficulty _cpuDifficulty = CpuDifficulty.Standard;
    private DeliveryPreset _deliveryPreset = null!;
    private BattingShotSet _shotSet = null!;
    private BallFlightSimulator _ballFlight = null!;
    private PlayerAsset _playerAsset = null!;
    private PlayerAnimator _playerAnimator = null!;
    private SkinnedPlayerRenderer _playerRenderer = null!;
    private PlayerAsset _bowlerAsset = null!;
    private PlayerAnimator _bowlerAnimator = null!;
    private SkinnedPlayerRenderer _bowlerRenderer = null!;
    private PlayerAnimator[] _fielderAnimators = [];
    private readonly string?[] _fielderActionClips = new string?[FieldingSide.FielderCount];
    private readonly bool[] _fielderActionHoldAtEnd = new bool[FieldingSide.FielderCount];
    private FieldPreset _fieldPreset = null!;
    private static readonly int[] OversChoices = [1, 2, 5, 10];
    private LimitedOversMatch _match = null!;
    private readonly FieldingSide _fieldingSide = new();
    private KeyboardState _previousKeyboard;
    private MatchControllerButtons _previousControllerButtons;
    private float _simulationAccumulator;
    private bool _simulationPaused;
    private float _bowlerRunUpDurationSeconds;
    private float _bowlerRunUpElapsed;
    private float _bowlerActionElapsed;
    private float _bowlerReleaseTimeSeconds;
    private int _selectedOversPerInnings = 1;
    private float _batterFootworkOffsetX;
    private float _targetBatterFootworkOffsetX;
    private bool _footworkTransitionActive;
    private bool _bowlerActionStarted;
    private bool _bowlerActionFinished;
    private bool _bowlerReleased;
    private bool _showDebugOverlay;
    private FieldingTactic _activeFieldingTactic = FieldingTactic.Balanced;
    private Vector3? _releaseMarkerPosition;
    private Vector3? _contactMarkerPosition;
    private Vector3? _sweetSpotMarkerPosition;
    private float? _contactTimeSeconds;
    private float? _contactQuality;
    private Vector2? _contactSweetSpotOffset;
    private BattingShotData? _chosenShot;
    private bool _shotResolved;
    private bool _deliveryComplete => _match.CurrentDelivery?.IsComplete == true;
    private bool IsCpuBattingControlled => _match.InningsNumber == 2 && !_verifyGameplay;
    private bool _battedBall;
    private int _batterRuns => _match.CurrentDelivery?.BatterRuns ?? 0;
    private int _extraRuns => _match.CurrentDelivery?.ExtraRuns ?? 0;
    private int _completedRuns => _match.CurrentDelivery?.CompletedRuns ?? 0;
    private DeliveryExtra _extraType => _match.CurrentDelivery?.Extra ?? DeliveryExtra.None;
    private DismissalKind _dismissal => _match.CurrentDelivery?.Dismissal ?? DismissalKind.None;
    private string ScoreStatusText => _match.IsMatchComplete
        ? _match.ResultText
        : $"Innings {_match.InningsNumber}/2    {_match.BattingTeamName} {_match.Runs}/{_match.Wickets}    {_match.OversText}/{_match.OversPerInnings} overs{(_match.Target is { } target ? $"    target {target}" : string.Empty)}    bowler {_match.CurrentBowler.Name}";
    private bool _isRunning;
    private bool _runRequestedPending;
    private int _cpuRunsRemaining;
    private int _liveCompletedRunCrossings;
    private CpuLiveBattingPlan? _cpuBattingPlan;
    private bool _cpuShotStarted;
    private float _runElapsed;
    private float _runDurationSeconds = CpuLiveRunningDecisionModel.DefaultRunDurationSeconds;
    private bool _fielderThrowActive;
    private bool _fielderThrowBallReleased;
    private bool _fielderBallSecured;
    private FielderSequencePhase _fielderSequencePhase;
    private NumericsVector3 _fielderHeldBallPosition;
    private float _fielderPickupBallSecuredTimeSeconds;
    private float _fielderCatchBallSecuredTimeSeconds;
    private float _fielderPickupDurationSeconds;
    private float _fielderThrowReleaseTimeSeconds;
    private float _fielderThrowDurationSeconds;
    private NumericsVector3 _fielderThrowStart;
    private NumericsVector3 _fielderThrowTarget;
    private int _fielderThrowerIndex;
    private const float NearBatterZ = BattingPracticeAnalyzer.BatterWicketLineZ;
    private const float FarBatterZ = 8.72f;
    private const float BowlerReleaseHandOffsetXMeters = 0.197f;
    private const float BowlerHandForwardMeters = 0.39f;
    private string _shotOutcome = "Choose a shot before the ball reaches the batter.";
    private int _batBoneIndex;
    private Vector3 _batBladeMinimum;
    private Vector3 _batBladeMaximum;
    private Matrix _previousBatWorld = Matrix.Identity;
    private Matrix _currentBatWorld = Matrix.Identity;
    private VertexPositionColorNormal[] _groundVertices = [];
    private VertexPositionColorNormalTexture[] _outfieldVertices = [];
    private VertexPositionColorNormalTexture[] _pitchVertices = [];
    private VertexPositionColorNormal[] _ballVertices = [];
    private int _crowdPrimitiveCount;
    private double _fpsElapsed;
    private int _frameCount;
    private int _framesPerSecond;
    private double _frameTimeMilliseconds;
    private double _updateMilliseconds;
    private double _drawMilliseconds;
    private readonly int _profileWarmupFrameCount;
    private int _profileWarmupRemaining;

    public Game1(
        string? capturePath = null,
        string? captureCameraPreset = null,
        float? captureRunUpTimeSeconds = null,
        float? captureDeliveryTimeSeconds = null,
        string? captureFielderActionClip = null,
        float? captureFielderActionTimeSeconds = null,
        bool verifyGameplay = false,
        bool captureDebugOverlay = false,
        int profileFrameCount = 0)
    {
        if ((captureRunUpTimeSeconds is not null && captureDeliveryTimeSeconds is not null) ||
            (captureFielderActionClip is null) != (captureFielderActionTimeSeconds is null) ||
            captureFielderActionClip is not null && (captureRunUpTimeSeconds is not null || captureDeliveryTimeSeconds is not null) ||
            profileFrameCount is < 0 or > 36000)
            throw new ArgumentException("Choose one bowler preview time or a fielder action and its preview time.");
        _capturePath = capturePath;
        _verifyGameplay = verifyGameplay;
        _profileFrameTarget = profileFrameCount;
        _profileWarmupFrameCount = Math.Min(60, profileFrameCount / 4);
        _profileWarmupRemaining = _profileWarmupFrameCount;
        _showDebugOverlay = captureDebugOverlay;
        _captureRunUpTimeSeconds = captureRunUpTimeSeconds;
        _captureDeliveryTimeSeconds = captureDeliveryTimeSeconds;
        _captureFielderActionClip = captureFielderActionClip;
        _captureFielderActionTimeSeconds = captureFielderActionTimeSeconds;
        if (captureCameraPreset is not null && !_camera.SelectPreset(captureCameraPreset))
            throw new ArgumentException($"Unknown capture camera '{captureCameraPreset}'. Use broadcast, behind-striker, bowler-end, or square-leg.", nameof(captureCameraPreset));
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "Super Cricket — One Over";
        _graphics.PreferredBackBufferWidth = 1440;
        _graphics.PreferredBackBufferHeight = 900;
        _graphics.SynchronizeWithVerticalRetrace = true;
        IsFixedTimeStep = false;
    }

    protected override void Initialize()
    {
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        GraphicsDevice.BlendState = BlendState.Opaque;
        _groundVertices = PracticeGround.CreateField();
        _outfieldVertices = PracticeGround.CreateOutfieldSurface();
        _pitchVertices = PracticeGround.CreatePitchSurface();
        _ballVertices = PracticeGround.CreateBall();
        var crowdVertices = PracticeGround.CreateCrowd();
        _crowdPrimitiveCount = crowdVertices.Length / 3;
        _crowdVertexBuffer = new VertexBuffer(
            GraphicsDevice,
            VertexPositionColorNormal.VertexDeclaration,
            crowdVertices.Length,
            BufferUsage.WriteOnly);
        _crowdVertexBuffer.SetData(crowdVertices);
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _debugFont = Content.Load<SpriteFont>("DebugFont");
        _debugPanel = new Texture2D(GraphicsDevice, 1, 1);
        _debugPanel.SetData([new Color(11, 20, 20, 220)]);
        _worldEffect = new BasicEffect(GraphicsDevice)
        {
            VertexColorEnabled = true,
            LightingEnabled = true
        };
        _worldEffect.EnableDefaultLighting();
        _worldEffect.AmbientLightColor = new Vector3(0.48f, 0.50f, 0.46f);
        _worldEffect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-0.45f, -0.82f, 0.34f));
        _worldEffect.DirectionalLight0.DiffuseColor = new Vector3(0.94f, 0.88f, 0.73f);
        _worldEffect.DirectionalLight0.SpecularColor = new Vector3(0.20f, 0.19f, 0.16f);
        _worldEffect.DirectionalLight1.Enabled = false;
        _worldEffect.DirectionalLight2.Enabled = false;
        _crowdEffect = new BasicEffect(GraphicsDevice)
        {
            VertexColorEnabled = true,
            LightingEnabled = false
        };
        _surfaceEffect = new BasicEffect(GraphicsDevice)
        {
            VertexColorEnabled = true,
            TextureEnabled = true,
            LightingEnabled = true
        };
        _surfaceEffect.EnableDefaultLighting();
        _surfaceEffect.AmbientLightColor = _worldEffect.AmbientLightColor;
        _surfaceEffect.DirectionalLight0.Direction = _worldEffect.DirectionalLight0.Direction;
        _surfaceEffect.DirectionalLight0.DiffuseColor = _worldEffect.DirectionalLight0.DiffuseColor;
        _surfaceEffect.DirectionalLight0.SpecularColor = _worldEffect.DirectionalLight0.SpecularColor;
        _surfaceEffect.DirectionalLight1.Enabled = false;
        _surfaceEffect.DirectionalLight2.Enabled = false;
        _outfieldTexture = LoadTexture(Path.Combine(AppContext.BaseDirectory, "Assets", "Textures", "outfield-grass.png"));
        _pitchTexture = LoadTexture(Path.Combine(AppContext.BaseDirectory, "Assets", "Textures", "cricket-pitch-clay.png"));
        _lineEffect = new BasicEffect(GraphicsDevice)
        {
            VertexColorEnabled = true,
            LightingEnabled = false
        };
        if (_capturePath is not null)
        {
            var outputDirectory = Path.GetDirectoryName(_capturePath);
            if (!string.IsNullOrEmpty(outputDirectory))
                Directory.CreateDirectory(outputDirectory);
            _captureTarget = new RenderTarget2D(
                GraphicsDevice,
                GraphicsDevice.PresentationParameters.BackBufferWidth,
                GraphicsDevice.PresentationParameters.BackBufferHeight,
                false,
                SurfaceFormat.Color,
                DepthFormat.Depth24);
        }
        _deliveryPresets =
        [
            DeliveryPreset.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Deliveries", "standard-pace.json")),
            DeliveryPreset.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Deliveries", "wide-pace.json")),
            DeliveryPreset.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Deliveries", "no-ball-pace.json"))
        ];
        _fieldPreset = FieldPreset.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Fields", "practice-attack.json"));
        _match = new LimitedOversMatch(
            TeamRosterAsset.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Teams", "coastal-xi.json")),
            TeamRosterAsset.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Teams", "highland-xi.json")),
            _selectedOversPerInnings);
        var playerPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Characters", "practice-batter.scplayer.json");
        _playerAsset = PlayerAsset.Load(playerPath);
        _playerAnimator = new PlayerAnimator(_playerAsset);
        _playerRenderer = new SkinnedPlayerRenderer(GraphicsDevice, _playerAsset);
        var bowlerPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Characters", "practice-bowler.scplayer.json");
        _bowlerAsset = PlayerAsset.Load(bowlerPath);
        _bowlerAnimator = new PlayerAnimator(_bowlerAsset);
        _bowlerRenderer = new SkinnedPlayerRenderer(GraphicsDevice, _bowlerAsset);
        _fielderAnimators = new PlayerAnimator[FieldingSide.FielderCount];
        for (var fielderIndex = 0; fielderIndex < _fielderAnimators.Length; fielderIndex++)
            _fielderAnimators[fielderIndex] = new PlayerAnimator(_bowlerAsset);
        _bowlerRunUpDurationSeconds = GetAnimationDuration(_bowlerAsset, "bowling-run-up");
        _bowlerReleaseTimeSeconds = GetAnimationEventTime(_bowlerAsset, "overarm-delivery", "ball-release");
        _fielderThrowDurationSeconds = GetAnimationDuration(_bowlerAsset, "fielder-throw");
        _fielderPickupDurationSeconds = GetAnimationDuration(_bowlerAsset, "fielder-pickup");
        _fielderThrowReleaseTimeSeconds = GetAnimationEventTime(_bowlerAsset, "fielder-throw", "ball-release");
        _fielderPickupBallSecuredTimeSeconds = GetAnimationEventTime(_bowlerAsset, "fielder-pickup", "ball-secured");
        _fielderCatchBallSecuredTimeSeconds = GetAnimationEventTime(_bowlerAsset, "fielder-catch", "catch-secured");
        RequireAnimation(_bowlerAsset, "bowling-run-up");
        RequireAnimation(_bowlerAsset, "overarm-delivery");
        RequireAnimation(_bowlerAsset, "fielder-catch");
        RequireAnimation(_bowlerAsset, "fielder-pickup");
        if (_fielderThrowReleaseTimeSeconds >= _fielderThrowDurationSeconds)
            throw new InvalidDataException("Fielder throw release event must occur before the end of its animation.");
        RequireAnimation(_bowlerAsset, "practice-stance");
        var shotSetPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Batting", "shots.json");
        _shotSet = BattingShotSet.Load(shotSetPath);
        foreach (var shot in _shotSet.Shots)
        {
            if (!_playerAsset.Animations.Exists(clip => string.Equals(clip.Name, shot.AnimationClip, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException($"Shot '{shot.Name}' refers to missing player clip '{shot.AnimationClip}'.");
        }
        _batBoneIndex = _playerAsset.Bones.FindIndex(bone => string.Equals(bone.Name, "forearm.R", StringComparison.OrdinalIgnoreCase));
        var batMesh = _playerAsset.Meshes.Find(mesh => string.Equals(mesh.Name, "Bat Blade", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("Player asset is missing the Bat Blade mesh.");
        if (_batBoneIndex < 0)
            throw new InvalidDataException("Player asset is missing the forearm.R bat attachment bone.");
        (_batBladeMinimum, _batBladeMaximum) = FindBounds(batMesh.Positions);
        _currentBatWorld = GetBatWorldTransform();
        _previousBatWorld = _currentBatWorld;
        StartNewMatch();
        if (_captureTarget is not null)
        {
            if (_captureFielderActionClip is { } fielderActionClip && _captureFielderActionTimeSeconds is { } actionTime)
            {
                SetFielderActionCapturePose(fielderActionClip, actionTime);
            }
            else if (_captureRunUpTimeSeconds is { } runUpTime)
            {
                if (runUpTime > _bowlerRunUpDurationSeconds)
                    throw new ArgumentOutOfRangeException("captureRunUpTimeSeconds", runUpTime,
                        $"Run-up capture time must be between 0 and {_bowlerRunUpDurationSeconds:0.###} seconds.");
                SetBowlerRunUpCapturePose(runUpTime);
            }
            else if (_captureDeliveryTimeSeconds is { } deliveryTime)
            {
                var deliveryDuration = GetAnimationDuration(_bowlerAsset, "overarm-delivery");
                if (deliveryTime > deliveryDuration)
                    throw new ArgumentOutOfRangeException("captureDeliveryTimeSeconds", deliveryTime,
                        $"Delivery capture time must be between 0 and {deliveryDuration:0.###} seconds.");
                SetBowlerDeliveryCapturePose(deliveryTime);
            }
            else
            {
                SetBowlerCaptureReleasePose();
            }
            _simulationPaused = true;
        }
        if (_verifyGameplay)
        {
            RunGameplayReviewChecks();
            Exit();
        }
    }

    protected override void Update(GameTime gameTime)
    {
        var controllerButtons = ReadControllerButtons(GamePad.GetState(PlayerIndex.One));
        var controllerActions = MatchControllerInputModel.ReadPressedActions(
            controllerButtons,
            _previousControllerButtons,
            IsCpuBattingControlled,
            _match.IsMatchComplete);
        _previousControllerButtons = controllerButtons;
        UpdateMatch(gameTime, Keyboard.GetState(), controllerActions);
    }

    private void UpdateMatch(GameTime gameTime, KeyboardState keyboard) =>
        UpdateMatch(gameTime, keyboard, MatchControllerActions.None);

    private void UpdateMatch(
        GameTime gameTime,
        KeyboardState keyboard,
        MatchControllerActions controllerActions)
    {
        var updateStart = Stopwatch.GetTimestamp();
        bool ControllerPressed(MatchControllerActions action) => (controllerActions & action) != 0;
        if (ControllerPressed(MatchControllerActions.Exit) ||
            keyboard.IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        if ((keyboard.IsKeyDown(Keys.R) && !_previousKeyboard.IsKeyDown(Keys.R)) ||
            ControllerPressed(MatchControllerActions.RestartMatch))
        {
            StartNewMatch();
        }
        if (((keyboard.IsKeyDown(Keys.N) && !_previousKeyboard.IsKeyDown(Keys.N)) ||
             ControllerPressed(MatchControllerActions.NextBall)) &&
            _deliveryComplete && !_match.IsMatchComplete)
        {
            if (_match.IsInningsComplete)
                StartNextInnings();
            else
                BeginDelivery();
        }
        if (((keyboard.IsKeyDown(Keys.O) && !_previousKeyboard.IsKeyDown(Keys.O)) ||
             ControllerPressed(MatchControllerActions.CycleOvers)) && _match.IsMatchComplete)
        {
            var currentIndex = Array.IndexOf(OversChoices, _selectedOversPerInnings);
            _selectedOversPerInnings = OversChoices[(currentIndex + 1) % OversChoices.Length];
            StartNewMatch();
        }
        if (((keyboard.IsKeyDown(Keys.D) && !_previousKeyboard.IsKeyDown(Keys.D)) ||
             ControllerPressed(MatchControllerActions.CycleDifficulty)) && _match.IsMatchComplete)
        {
            _cpuDifficulty = CpuDifficultyModel.Next(_cpuDifficulty);
            StartNewMatch();
        }
        if ((keyboard.IsKeyDown(Keys.D1) && !_previousKeyboard.IsKeyDown(Keys.D1)) ||
            ControllerPressed(MatchControllerActions.SelectStandardDelivery)) SelectNextDelivery(0);
        if ((keyboard.IsKeyDown(Keys.D2) && !_previousKeyboard.IsKeyDown(Keys.D2)) ||
            ControllerPressed(MatchControllerActions.SelectWideDelivery)) SelectNextDelivery(1);
        if ((keyboard.IsKeyDown(Keys.D3) && !_previousKeyboard.IsKeyDown(Keys.D3)) ||
            ControllerPressed(MatchControllerActions.SelectNoBallDelivery)) SelectNextDelivery(2);
        if (keyboard.IsKeyDown(Keys.V) && !_previousKeyboard.IsKeyDown(Keys.V)) _camera.CyclePreset();
        if (keyboard.IsKeyDown(Keys.F1) && !_previousKeyboard.IsKeyDown(Keys.F1)) _showDebugOverlay = !_showDebugOverlay;
        if (!IsCpuBattingControlled &&
            ((keyboard.IsKeyDown(Keys.X) && !_previousKeyboard.IsKeyDown(Keys.X)) ||
             ControllerPressed(MatchControllerActions.CancelRun))) CancelRun();
        if ((keyboard.IsKeyDown(Keys.P) && !_previousKeyboard.IsKeyDown(Keys.P)) ||
            ControllerPressed(MatchControllerActions.Pause))
        {
            _simulationPaused = !_simulationPaused;
        }
        if (!IsCpuBattingControlled && !_simulationPaused && !_deliveryComplete && !_battedBall && !_isRunning)
        {
            if (keyboard.IsKeyDown(Keys.Q) && !_previousKeyboard.IsKeyDown(Keys.Q))
                StepBatterFootwork(1f);
            if (keyboard.IsKeyDown(Keys.E) && !_previousKeyboard.IsKeyDown(Keys.E))
                StepBatterFootwork(-1f);
            if (ControllerPressed(MatchControllerActions.StepOffSide))
                StepBatterFootwork(1f);
            if (ControllerPressed(MatchControllerActions.StepLegSide))
                StepBatterFootwork(-1f);
        }
        if (!IsCpuBattingControlled && !_simulationPaused && keyboard.IsKeyDown(Keys.T) && !_previousKeyboard.IsKeyDown(Keys.T))
        {
            _playerAnimator.PlayNext();
        }
        if (!IsCpuBattingControlled)
        {
            if ((keyboard.IsKeyDown(Keys.A) && !_previousKeyboard.IsKeyDown(Keys.A)) ||
                ControllerPressed(MatchControllerActions.Defend)) StartShot("defence");
            if ((keyboard.IsKeyDown(Keys.S) && !_previousKeyboard.IsKeyDown(Keys.S)) ||
                ControllerPressed(MatchControllerActions.Drive)) StartShot("drive");
            if ((keyboard.IsKeyDown(Keys.D) && !_previousKeyboard.IsKeyDown(Keys.D)) ||
                ControllerPressed(MatchControllerActions.Loft)) StartShot("loft");
            if ((keyboard.IsKeyDown(Keys.Enter) && !_previousKeyboard.IsKeyDown(Keys.Enter)) ||
                ControllerPressed(MatchControllerActions.Run)) StartRun();
        }
        _previousKeyboard = keyboard;
        _camera.Update(gameTime);
        var elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var flightElapsed = _simulationPaused ? 0f : UpdateBowler(elapsedSeconds);
        if (!_simulationPaused)
            UpdateCpuBatting(flightElapsed);
        if (_captureTarget is null && !_simulationPaused)
        {
            _previousBatWorld = _currentBatWorld;
            _playerAnimator.Update(elapsedSeconds);
            UpdateBatterFootwork(elapsedSeconds);
            _currentBatWorld = GetBatWorldTransform();
            UpdateFielderAnimations(elapsedSeconds);
        }
        if (!_simulationPaused && _isRunning && (_fielderThrowActive || _ballFlight.CurrentFrame.Phase == BallMotionPhase.Settled))
            UpdateRun(elapsedSeconds);
        if (!_simulationPaused && _fielderThrowActive)
            UpdateFielderThrow();

        if (!_simulationPaused && !_deliveryComplete && _bowlerReleased)
        {
            var accumulatorBeforeFrame = _simulationAccumulator;
            var physicsElapsedThisFrame = 0f;
            _simulationAccumulator += MathF.Min(flightElapsed, 0.25f);
            while (_simulationAccumulator >= _ballFlight.FixedTimeStepSeconds &&
                   _ballFlight.CurrentFrame.Phase != BallMotionPhase.Settled)
            {
                var previousFrame = _ballFlight.CurrentFrame;
                if (_battedBall)
                    _fieldingSide.Step(_ballFlight.FixedTimeStepSeconds, previousFrame.Position);

                var frame = _ballFlight.Step();
                if (_chosenShot is not null && !_shotResolved &&
                    TryBatContact(
                        previousFrame.Position,
                        frame.Position,
                        InterpolateBatWorld(GetBatPoseFraction(physicsElapsedThisFrame, accumulatorBeforeFrame, elapsedSeconds, flightElapsed)),
                        InterpolateBatWorld(GetBatPoseFraction(physicsElapsedThisFrame + _ballFlight.FixedTimeStepSeconds, accumulatorBeforeFrame, elapsedSeconds, flightElapsed)),
                        GetBatPoseDeltaSeconds(physicsElapsedThisFrame, accumulatorBeforeFrame, elapsedSeconds, flightElapsed),
                        out var battingContact))
                {
                    _contactMarkerPosition = battingContact.Position;
                    _sweetSpotMarkerPosition = battingContact.SweetSpotPosition;
                    _contactTimeSeconds = previousFrame.TimeSeconds +
                        _ballFlight.FixedTimeStepSeconds * battingContact.HitFraction;
                    _contactSweetSpotOffset = battingContact.NormalizedSweetSpotOffset;
                    var impact = BattingImpactModel.Calculate(
                        ToNumerics(frame.Velocity),
                        ToNumerics(battingContact.BatPointVelocity),
                        new System.Numerics.Vector2(battingContact.NormalizedSweetSpotOffset.X, battingContact.NormalizedSweetSpotOffset.Y),
                        _chosenShot,
                        _match.StrikerPlayer);
                    _contactQuality = impact.ContactQuality;
                    _ballFlight.ApplyBatContact(ToNumerics(battingContact.Position), impact.OutgoingVelocity);
                    frame = _ballFlight.CurrentFrame;
                    _shotResolved = true;
                    _battedBall = true;
                    _fieldingSide.Reset();
                    _shotOutcome = $"HIT: {_chosenShot.Name}, {impact.ContactQuality:0.00} quality at {impact.OutgoingVelocity.Length():0.0} m/s";
                    if (IsCpuBattingControlled && _cpuBattingPlan is { } cpuPlan)
                    {
                        var fieldingPlayers = _match.FieldingPlayers;
                        var fieldingRatings = fieldingPlayers.Select(player => player.Fielding).ToArray();
                        _cpuRunsRemaining = CpuLiveRunningDecisionModel.Choose(
                            cpuPlan.AttemptRun,
                            _deliveryPreset,
                            ToNumerics(battingContact.Position),
                            impact.OutgoingVelocity,
                            _fieldingSide.Positions,
                            fieldingRatings,
                            _runDurationSeconds,
                            _fielderPickupDurationSeconds,
                            _fielderThrowDurationSeconds).PlannedRuns;
                        _runRequestedPending = _cpuRunsRemaining > 0;
                    }
                    if (_runRequestedPending)
                        StartRun();
                }
                else if (_chosenShot is not null && !_shotResolved && frame.Position.Z < NearBatterZ - 0.45f && frame.Velocity.Z < 0f)
                {
                    _shotResolved = true;
                    _shotOutcome = $"MISS: {_chosenShot.Name} swung outside contact";
                }

                if (!_deliveryComplete && !_battedBall && TryCrossPlane(previousFrame.Position, frame.Position, -PracticeGround.WicketOffset, out var wicketLinePosition))
                {
                    ResolveIncomingDelivery(wicketLinePosition);
                    frame = _ballFlight.CurrentFrame;
                }

                if (!_deliveryComplete && _battedBall &&
                    _fieldingSide.TryFindContact(previousFrame, frame, out var fieldingContact))
                {
                    ResolveFieldingContact(fieldingContact);
                    frame = _ballFlight.CurrentFrame;
                }

                if (!_deliveryComplete && _battedBall &&
                    BoundaryResolver.TryFindCrossing(
                        previousFrame,
                        frame,
                        _deliveryPreset.FieldBoundaryRadiusMeters,
                        _deliveryPreset.FieldSurfaceHeightMeters,
                        _deliveryPreset.BallRadiusMeters,
                        out var boundaryCrossing))
                {
                    ResolveBoundaryCrossing(boundaryCrossing);
                    frame = _ballFlight.CurrentFrame;
                }

                if (!_deliveryComplete && !_fielderThrowActive && frame.Phase == BallMotionPhase.Settled)
                {
                    ResolveSettledBall(frame);
                }

                if (_isRunning)
                    UpdateRun(_ballFlight.FixedTimeStepSeconds);

                _trajectoryVertices.Add(new VertexPositionColor(
                    ToXna(frame.Position) + new Vector3(0f, 0.01f, 0f),
                    new Color(248, 181, 82)));
                _simulationAccumulator -= _ballFlight.FixedTimeStepSeconds;
                physicsElapsedThisFrame += _ballFlight.FixedTimeStepSeconds;
            }
        }
        if (_ballFlight.CurrentFrame.Phase == BallMotionPhase.Settled || _deliveryComplete)
        {
            _simulationAccumulator = 0f;
        }

        _frameTimeMilliseconds = gameTime.ElapsedGameTime.TotalMilliseconds;
        _fpsElapsed += gameTime.ElapsedGameTime.TotalSeconds;
        _frameCount++;
        if (_fpsElapsed >= 1.0)
        {
            _framesPerSecond = (int)Math.Round(_frameCount / _fpsElapsed);
            _frameCount = 0;
            _fpsElapsed = 0;
        }

        base.Update(gameTime);
        _updateMilliseconds = Stopwatch.GetElapsedTime(updateStart).TotalMilliseconds;
    }

    protected override void Draw(GameTime gameTime)
    {
        var drawStart = Stopwatch.GetTimestamp();
        if (_captureTarget is not null)
            GraphicsDevice.SetRenderTarget(_captureTarget);
        GraphicsDevice.Clear(new Color(116, 161, 195));
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        GraphicsDevice.BlendState = BlendState.Opaque;

        _worldEffect.World = Matrix.Identity;
        _worldEffect.View = Matrix.CreateLookAt(_camera.Position, _camera.Target, Vector3.Up);
        _worldEffect.Projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(48f),
            GraphicsDevice.Viewport.AspectRatio,
            0.05f,
            250f);
        DrawTexturedSurfaces();
        var (strikerWorld, nonStrikerWorld) = GetBatterWorlds();
        var ballPosition = _fielderThrowActive
            ? GetFielderThrowBallPosition()
            : _ballFlight.CurrentFrame.Position;
        BuildShadowVertices(strikerWorld, nonStrikerWorld, ballPosition);

        foreach (var pass in _worldEffect.CurrentTechnique.Passes)
        {
            _worldEffect.World = Matrix.Identity;
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(
                PrimitiveType.TriangleList,
                _groundVertices,
                0,
                _groundVertices.Length / 3);

        }

        DrawCrowd();
        DrawShadows();

        if (_bowlerReleased)
        {
            _worldEffect.World = Matrix.CreateScale(_deliveryPreset.BallRadiusMeters)
                * Matrix.CreateTranslation(ToXna(ballPosition));
            foreach (var pass in _worldEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _ballVertices, 0, _ballVertices.Length / 3);
            }
            _worldEffect.World = Matrix.Identity;
        }

        _lineEffect.World = Matrix.Identity;
        _lineEffect.View = _worldEffect.View;
        _lineEffect.Projection = _worldEffect.Projection;
        if (_bowlerReleased && _trajectoryVertices.Count >= 2)
        {
            var trajectory = _trajectoryVertices.ToArray();
            foreach (var pass in _lineEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(
                    PrimitiveType.LineStrip,
                    trajectory,
                    0,
                    trajectory.Length - 1);
            }
        }

        var battingPrimaryColor = ToXna(_match.BattingTeam.PrimaryKitColor);
        var battingAccentColor = ToXna(_match.BattingTeam.AccentKitColor);
        var fieldingPrimaryColor = ToXna(_match.FieldingTeam.PrimaryKitColor);
        var fieldingAccentColor = ToXna(_match.FieldingTeam.AccentKitColor);
        for (var fielderIndex = 0; fielderIndex < _fielderAnimators.Length; fielderIndex++)
        {
            _bowlerRenderer.Draw(
                GetFielderWorld(fielderIndex, ballPosition),
                _worldEffect.View,
                _worldEffect.Projection,
                _fielderAnimators[fielderIndex].GetSkinMatrices(),
                fieldingPrimaryColor,
                fieldingAccentColor);
        }

        var skinMatrices = _playerAnimator.GetSkinMatrices();
        _playerRenderer.Draw(strikerWorld, _worldEffect.View, _worldEffect.Projection,
            skinMatrices, battingPrimaryColor, battingAccentColor);
        _playerRenderer.Draw(nonStrikerWorld, _worldEffect.View, _worldEffect.Projection,
            skinMatrices, battingPrimaryColor, battingAccentColor);
        _bowlerRenderer.Draw(GetBowlerWorld(), _worldEffect.View, _worldEffect.Projection,
            _bowlerAnimator.GetSkinMatrices(), fieldingPrimaryColor, fieldingAccentColor);
        if (_showDebugOverlay)
        {
            GraphicsDevice.DepthStencilState = DepthStencilState.None;
            DrawDebugMarkers();
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        }
        DrawDebugOverlay();
        base.Draw(gameTime);
        _drawMilliseconds = Stopwatch.GetElapsedTime(drawStart).TotalMilliseconds;

        if (_profileFrameTarget > 0)
        {
            if (_profileWarmupRemaining > 0)
            {
                _profileWarmupRemaining--;
            }
            else
            {
                _profileTimings.Add(new FrameTiming(
                    _frameTimeMilliseconds,
                    _updateMilliseconds,
                    _drawMilliseconds));
                if (_profileTimings.Count >= _profileFrameTarget)
                {
                    PrintFrameProfile();
                    Exit();
                    return;
                }
            }
        }

        if (_captureTarget is not null)
        {
            var target = _captureTarget;
            GraphicsDevice.SetRenderTarget(null);
            using (var output = File.Create(_capturePath!))
                target.SaveAsPng(output, target.Width, target.Height);
            _captureTarget = null;
            target.Dispose();
            Exit();
        }
    }

    private void DrawDebugMarkers()
    {
        var vertexCount = 0;

        void AddMarker(Vector3? position, Color color)
        {
            if (position is not { } point)
                return;

            const float halfLength = 0.24f;
            _debugMarkerVertices[vertexCount++] = new VertexPositionColor(point - Vector3.UnitX * halfLength, color);
            _debugMarkerVertices[vertexCount++] = new VertexPositionColor(point + Vector3.UnitX * halfLength, color);
            _debugMarkerVertices[vertexCount++] = new VertexPositionColor(point - Vector3.UnitY * halfLength, color);
            _debugMarkerVertices[vertexCount++] = new VertexPositionColor(point + Vector3.UnitY * halfLength, color);
            _debugMarkerVertices[vertexCount++] = new VertexPositionColor(point - Vector3.UnitZ * halfLength, color);
            _debugMarkerVertices[vertexCount++] = new VertexPositionColor(point + Vector3.UnitZ * halfLength, color);
        }

        AddMarker(_releaseMarkerPosition, new Color(248, 195, 82));
        AddMarker(_contactMarkerPosition, new Color(255, 126, 64));
        AddMarker(_sweetSpotMarkerPosition, new Color(81, 224, 218));
        if (vertexCount == 0)
            return;

        foreach (var pass in _lineEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(
                PrimitiveType.LineList,
                _debugMarkerVertices,
                0,
                vertexCount / 2);
        }
    }

    private void PrintFrameProfile()
    {
        static (double Average, double Median, double P95, double Maximum) Summarize(
            IReadOnlyList<FrameTiming> samples,
            Func<FrameTiming, double> valueSelector)
        {
            var values = new double[samples.Count];
            var total = 0d;
            for (var index = 0; index < samples.Count; index++)
            {
                values[index] = valueSelector(samples[index]);
                total += values[index];
            }
            Array.Sort(values);
            var medianIndex = Math.Clamp((int)Math.Ceiling(values.Length * 0.5d) - 1, 0, values.Length - 1);
            var p95Index = Math.Clamp((int)Math.Ceiling(values.Length * 0.95d) - 1, 0, values.Length - 1);
            return (total / values.Length, values[medianIndex], values[p95Index], values[^1]);
        }

        static void WriteSummary(string name, (double Average, double Median, double P95, double Maximum) summary) =>
            Console.WriteLine($"{name}: avg {summary.Average:0.00} ms, p50 {summary.Median:0.00} ms, p95 {summary.P95:0.00} ms, max {summary.Maximum:0.00} ms");

        Console.WriteLine($"Renderer profile: {GraphicsDevice.Viewport.Width}x{GraphicsDevice.Viewport.Height}, VSync enabled");
        Console.WriteLine($"Measured {_profileTimings.Count} rendered frames after {_profileWarmupFrameCount} warm-up frames.");
        WriteSummary("Frame interval", Summarize(_profileTimings, static sample => sample.FrameIntervalMilliseconds));
        WriteSummary("CPU update", Summarize(_profileTimings, static sample => sample.UpdateCpuMilliseconds));
        WriteSummary("CPU draw submission", Summarize(_profileTimings, static sample => sample.DrawCpuMilliseconds));
        Console.WriteLine("GPU execution time is not included in CPU draw submission; use a GPU profiler for that measurement.");
    }

    protected override void UnloadContent()
    {
        _playerRenderer?.Dispose();
        _bowlerRenderer?.Dispose();
        _crowdVertexBuffer?.Dispose();
        _worldEffect?.Dispose();
        _crowdEffect?.Dispose();
        _lineEffect?.Dispose();
        _captureTarget?.Dispose();
        _debugPanel?.Dispose();
        _spriteBatch?.Dispose();
        base.UnloadContent();
    }

    private void DrawDebugOverlay()
    {
        if (!_showDebugOverlay)
        {
            var eventText = _shotOutcome.Length > 72 ? _shotOutcome[..69] + "..." : _shotOutcome;
            var batterText = _match.IsMatchComplete
                ? "Match complete"
                : _match.IsInningsComplete
                    ? "Innings complete; press N to start the chase"
                    : IsCpuBattingControlled
                        ? $"CPU batting: {_match.StrikerPlayer.Name}    Non-striker: {_match.NonStrikerPlayer.Name}"
                        : $"On strike: {_match.StrikerPlayer.Name}    Non-striker: {_match.NonStrikerPlayer.Name}";
            var matchLines = new[]
            {
                $"SUPER CRICKET  /  SHORT MATCH    CPU {_cpuDifficulty}",
                ScoreStatusText,
                batterText,
                _match.IsMatchComplete ? "Match finished" : eventText,
                _match.IsMatchComplete
                    ? "D: next CPU difficulty    O: next overs    R: replay"
                    : IsCpuBattingControlled
                        ? $"CPU batting ({_cpuDifficulty}). Choose a bowling delivery."
                        : "A/S/D or Pad A/X/Y: shot    Q/E or D-pad: step",
                _match.IsMatchComplete
                    ? "Pad A: replay    LB: difficulty    RB: overs"
                    : IsCpuBattingControlled
                        ? "1-3 or D-pad: delivery    N/RB: next    P/Start: pause"
                        : "Enter/B: run    X/LB: cancel    N/RB: next    R: restart    P/Start: pause"
            };
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            _spriteBatch.Draw(_debugPanel, new Rectangle(20, 20, 660, 166), Color.White);
            for (var index = 0; index < matchLines.Length; index++)
            {
                var color = index == 0 ? new Color(242, 206, 116) : Color.White;
                _spriteBatch.DrawString(_debugFont, matchLines[index], new Vector2(34, 24 + index * 25), color);
            }
            _spriteBatch.End();
            return;
        }

        var ball = _ballFlight.CurrentFrame;
        var lines = new[]
        {
            $"SUPER CRICKET  /  SHORT MATCH    CPU {_cpuDifficulty}    seed {_matchBowlingSeed}",
            $"Pitch {PracticeGround.PitchLength:0.00} m x {PracticeGround.PitchWidth:0.00} m    Stumps {PracticeGround.WicketHeight:0.00} m",
            $"{ScoreStatusText}    Striker {_match.StrikerPlayer.Name}    legal balls {_match.LegalBalls}/{_match.OversPerInnings * OverScoreboard.BallsPerOver}",
            $"Preset: {_deliveryPreset.Name}    next {_deliveryPresets[_nextDeliveryPresetIndex].Name}    release ({_deliveryPreset.ReleasePosition.X:0.00}, {_deliveryPreset.ReleasePosition.Y:0.00}, {_deliveryPreset.ReleasePosition.Z:0.00}) m",
            $"Ball {(_bowlerReleased ? (_simulationPaused ? "Paused" : ball.Phase.ToString()) : "Awaiting release")}    {(_bowlerReleased ? $"speed {ball.Velocity.Length():0.0} m/s    bounces {ball.BounceCount}    position ({ball.Position.X:0.0}, {ball.Position.Y:0.0}, {ball.Position.Z:0.0}) m" : "flight simulation starts at the bowler's release")}",
            $"Player: {_playerAsset.Name}    animation {_playerAnimator.CurrentClipName}{(_playerAnimator.IsTransitioning ? " (crossfade)" : string.Empty)}",
            $"Batter footwork: {_batterFootworkOffsetX:+0.00;-0.00;0.00} m lateral",
            $"Bowler: {_bowlerAsset.Name}    {(_bowlerReleased ? "released" : _bowlerActionStarted ? "delivery stride" : "run-up")}    animation {_bowlerAnimator.CurrentClipName}",
            $"Delivery: {(_deliveryComplete ? "complete" : "live")}    {_fieldPreset.Name} ({_activeFieldingTactic}, {_fieldingSide.Positions.Count} fielders)    run {(_isRunning ? $"{MathHelper.Clamp(_runElapsed / _runDurationSeconds, 0f, 1f):P0}" : "ready")}",
            $"Event: {_shotOutcome}",
            $"Release: {(_releaseMarkerPosition is { } release ? $"t=0.000 s @ {FormatPosition(release)} m" : "not yet released")}    Contact: {(_contactMarkerPosition is { } contact ? $"t={_contactTimeSeconds:0.000} s @ {FormatPosition(contact)} m" : "waiting")}",
            $"Sweet spot: {(_sweetSpotMarkerPosition is { } sweetSpot && _contactSweetSpotOffset is { } offset && _contactQuality is { } quality ? $"q={quality:0.00} offset ({offset.X:+0.00;-0.00;0.00}, {offset.Y:+0.00;-0.00;0.00}) @ {FormatPosition(sweetSpot)} m" : "waiting for bat contact")}    Markers: gold / orange / cyan",
            $"View {_camera.PresetName}    distance {_camera.Distance:0.0} m    elevation {MathHelper.ToDegrees(_camera.Elevation):0}°    FPS {_framesPerSecond}    frame {_frameTimeMilliseconds:0.0} ms    CPU update/draw {_updateMilliseconds:0.00}/{_drawMilliseconds:0.00} ms",
            $"Skinned players {_fielderAnimators.Length + 3} ({_fielderAnimators.Length} fielders)    material batches batter/bowler {_playerRenderer.MaterialBatchCount}/{_bowlerRenderer.MaterialBatchCount}",
            "Arrows orbit    PgUp/PgDn height    wheel zoom    V camera    Home broadcast    F1 hide debug",
            "A defend    S drive    D loft    Q/E step off/leg    Enter run    X cancel    1-3 bowl    N ball/innings    R restart    O overs    D difficulty at result    P pause    Esc quit",
            "Pad A/X/Y shots    B run    LB cancel/difficulty    RB next/overs    D-pad delivery/steps    Start pause    Back quit"
        };
        var panel = new Rectangle(16, 16, GraphicsDevice.Viewport.Width - 32, 18 + lines.Length * 23);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(_debugPanel, panel, new Color(255, 255, 255, 190));
        for (var index = 0; index < lines.Length; index++)
        {
            var color = index == 0 ? new Color(242, 206, 116) : Color.White;
            _spriteBatch.DrawString(_debugFont, lines[index], new Vector2(30, 17 + index * 23), color);
        }
        _spriteBatch.End();
    }

    private static MatchControllerButtons ReadControllerButtons(GamePadState state)
    {
        var buttons = MatchControllerButtons.None;
        if (state.IsButtonDown(Buttons.A)) buttons |= MatchControllerButtons.A;
        if (state.IsButtonDown(Buttons.B)) buttons |= MatchControllerButtons.B;
        if (state.IsButtonDown(Buttons.X)) buttons |= MatchControllerButtons.X;
        if (state.IsButtonDown(Buttons.Y)) buttons |= MatchControllerButtons.Y;
        if (state.IsButtonDown(Buttons.Start)) buttons |= MatchControllerButtons.Start;
        if (state.IsButtonDown(Buttons.Back)) buttons |= MatchControllerButtons.Back;
        if (state.IsButtonDown(Buttons.LeftShoulder)) buttons |= MatchControllerButtons.LeftShoulder;
        if (state.IsButtonDown(Buttons.RightShoulder)) buttons |= MatchControllerButtons.RightShoulder;
        if (state.IsButtonDown(Buttons.DPadUp)) buttons |= MatchControllerButtons.DPadUp;
        if (state.IsButtonDown(Buttons.DPadDown)) buttons |= MatchControllerButtons.DPadDown;
        if (state.IsButtonDown(Buttons.DPadLeft)) buttons |= MatchControllerButtons.DPadLeft;
        if (state.IsButtonDown(Buttons.DPadRight)) buttons |= MatchControllerButtons.DPadRight;
        return buttons;
    }

    private void StartNewMatch()
    {
        _match.Reset(_selectedOversPerInnings);
        _matchBowlingSeed = Random.Shared.Next();
        _nextDeliveryPresetIndex = 0;
        BeginDelivery();
    }

    private void StartNextInnings()
    {
        _match.StartNextInnings();
        _nextDeliveryPresetIndex = 0;
        BeginDelivery();
    }

    private void BeginDelivery()
    {
        if (_match.IsInningsComplete || _match.IsMatchComplete)
            return;

        _groundVertices = PracticeGround.CreateField();
        _activeDeliveryPresetIndex = _nextDeliveryPresetIndex;
        var selectedPreset = _deliveryPresets[_activeDeliveryPresetIndex];
        var situation = new BowlingSituation(
            _match.LegalBalls,
            _match.OversPerInnings,
            _match.Runs,
            _match.Wickets,
            _match.Target);
        var fieldPlacement = FieldPlacementModel.Choose(_fieldPreset, situation, _match.StrikerPlayer.Power);
        _activeFieldingTactic = fieldPlacement.Tactic;
        _fieldingSide.ConfigureStartingPositions(fieldPlacement.StartingPositions);
        if (_activeDeliveryPresetIndex == 0 && !_verifyGameplay)
        {
            _deliveryPreset = BowlingDecisionModel.ChooseDelivery(
                selectedPreset,
                _match.CurrentBowler.Bowling,
                _match.StrikerPlayer.Power,
                situation,
                CreateBowlingDecisionSeed(),
                _cpuDifficulty).Delivery;
        }
        else
        {
            _deliveryPreset = selectedPreset;
        }
        ConfigureFieldingRatingsForCurrentSide();
        _match.BeginDelivery(_deliveryPreset.IsNoBall);
        _ballFlight = new BallFlightSimulator(_deliveryPreset);
        _simulationAccumulator = 0f;
        _simulationPaused = false;
        _chosenShot = null;
        _shotResolved = false;
        _battedBall = false;
        _batterFootworkOffsetX = 0f;
        _targetBatterFootworkOffsetX = 0f;
        _footworkTransitionActive = false;
        _isRunning = false;
        _runRequestedPending = false;
        _cpuRunsRemaining = 0;
        _liveCompletedRunCrossings = 0;
        _cpuBattingPlan = null;
        _cpuShotStarted = false;
        _runElapsed = 0f;
        _fielderThrowActive = false;
        _fielderThrowBallReleased = false;
        _fielderBallSecured = false;
        _fielderSequencePhase = FielderSequencePhase.None;
        Array.Clear(_fielderActionClips);
        Array.Clear(_fielderActionHoldAtEnd);
        _fieldingSide.Reset();
        _shotOutcome = "Choose a shot before the ball reaches the batter.";
        _releaseMarkerPosition = null;
        _contactMarkerPosition = null;
        _sweetSpotMarkerPosition = null;
        _contactTimeSeconds = null;
        _contactQuality = null;
        _contactSweetSpotOffset = null;
        _playerAnimator.Play("practice-stance", 0.12f);
        _currentBatWorld = GetBatWorldTransform();
        _previousBatWorld = _currentBatWorld;
        _bowlerRunUpElapsed = 0f;
        _bowlerActionElapsed = 0f;
        _bowlerActionStarted = false;
        _bowlerActionFinished = false;
        _bowlerReleased = false;
        _bowlerAnimator.Play("bowling-run-up", 0.08f);
        _trajectoryVertices.Clear();

        if (IsCpuBattingControlled)
        {
            _cpuBattingPlan = CpuLiveBattingPlanModel.Choose(
                _match.StrikerPlayer,
                _match.CurrentBowler,
                _match.FieldingPlayers,
                situation,
                _activeFieldingTactic,
                _deliveryPreset,
                _playerAsset,
                _bowlerAsset,
                _shotSet,
                CreateBowlingDecisionSeed() ^ unchecked((int)0x6d2b79f5),
                _fieldingSide.Positions,
                _cpuDifficulty);
            _targetBatterFootworkOffsetX = _cpuBattingPlan.Value.FootworkOffsetMeters;
            _footworkTransitionActive = MathF.Abs(_targetBatterFootworkOffsetX) > 0.0001f;
            _runRequestedPending = _cpuBattingPlan.Value.AttemptRun;
        }
    }

    private void UpdateCpuBatting(float flightElapsed)
    {
        if (!IsCpuBattingControlled || _cpuBattingPlan is not { } plan || _cpuShotStarted || _deliveryComplete || !_bowlerReleased)
            return;

        var upcomingSimulationTime = _ballFlight.CurrentFrame.TimeSeconds + _simulationAccumulator + flightElapsed;
        if (upcomingSimulationTime < plan.InputDelaySeconds)
            return;

        StartShot(plan.Shot switch
        {
            CpuShotChoice.Defence => "defence",
            CpuShotChoice.Drive => "drive",
            _ => "loft"
        }, plan.HorizontalAim);
        _cpuShotStarted = true;
    }

    private int CreateBowlingDecisionSeed()
    {
        unchecked
        {
            var seed = _matchBowlingSeed;
            seed = seed * 31 + _match.InningsNumber;
            seed = seed * 31 + _match.LegalBalls;
            seed = seed * 31 + _match.Runs;
            seed = seed * 31 + _match.Wickets;
            seed = AddSeedText(seed, _match.CurrentBowler.Id);
            seed = AddSeedText(seed, _match.StrikerPlayer.Id);
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

    private void ConfigureFieldingRatingsForCurrentSide()
    {
        var players = _match.FieldingPlayers;
        var ratings = new int[players.Count];
        for (var index = 0; index < players.Count; index++)
            ratings[index] = players[index].Fielding;
        _fieldingSide.ConfigureFieldingRatings(ratings);
    }

    private float UpdateBowler(float elapsedSeconds)
    {
        if (_bowlerActionFinished)
        {
            _bowlerAnimator.Update(elapsedSeconds);
            return _bowlerReleased ? elapsedSeconds : 0f;
        }

        var remaining = elapsedSeconds;
        var flightElapsed = 0f;

        if (!_bowlerActionStarted)
        {
            var runUpRemaining = MathF.Max(0f, _bowlerRunUpDurationSeconds - _bowlerRunUpElapsed);
            var runUpStep = MathF.Min(remaining, runUpRemaining);
            _bowlerRunUpElapsed += runUpStep;
            _bowlerAnimator.Update(runUpStep);
            remaining -= runUpStep;
            if (_bowlerRunUpElapsed >= _bowlerRunUpDurationSeconds)
            {
                _bowlerActionStarted = true;
                _bowlerActionElapsed = 0f;
                _bowlerAnimator.Play("overarm-delivery", 0.08f);
            }
        }

        if (remaining <= 0f)
            return 0f;

        var actionDuration = GetAnimationDuration(_bowlerAsset, "overarm-delivery");
        if (_bowlerActionElapsed < actionDuration)
        {
            var actionStep = MathF.Min(remaining, actionDuration - _bowlerActionElapsed);
            var previousActionTime = _bowlerActionElapsed;
            _bowlerAnimator.Update(actionStep);
            _bowlerActionElapsed += actionStep;
            remaining -= actionStep;

            if (!_bowlerReleased && _bowlerActionElapsed >= _bowlerReleaseTimeSeconds)
            {
                _bowlerReleased = true;
                _releaseMarkerPosition = ToXna(_ballFlight.CurrentFrame.Position);
                _trajectoryVertices.Add(new VertexPositionColor(
                    ToXna(_ballFlight.CurrentFrame.Position) + new Vector3(0f, 0.01f, 0f),
                    new Color(248, 181, 82)));
            }

            if (_bowlerReleased)
                flightElapsed = previousActionTime >= _bowlerReleaseTimeSeconds
                    ? actionStep
                    : MathF.Max(0f, _bowlerActionElapsed - _bowlerReleaseTimeSeconds);
        }

        if (_bowlerActionElapsed >= actionDuration)
        {
            _bowlerActionFinished = true;
            _bowlerAnimator.Play("practice-stance", 0.16f);
            if (remaining > 0f)
            {
                _bowlerAnimator.Update(remaining);
                flightElapsed += remaining;
            }
        }

        return flightElapsed;
    }

    private void SetBowlerCaptureReleasePose()
    {
        _bowlerRunUpElapsed = _bowlerRunUpDurationSeconds;
        _bowlerActionStarted = true;
        _bowlerActionFinished = false;
        _bowlerActionElapsed = _bowlerReleaseTimeSeconds;
        _bowlerReleased = true;
        _releaseMarkerPosition = ToXna(_ballFlight.CurrentFrame.Position);
        _bowlerAnimator.Play("overarm-delivery", 0.001f);
        _bowlerAnimator.Update(_bowlerReleaseTimeSeconds);
        _trajectoryVertices.Clear();
        _trajectoryVertices.Add(new VertexPositionColor(
            ToXna(_ballFlight.CurrentFrame.Position) + new Vector3(0f, 0.01f, 0f),
            new Color(248, 181, 82)));
    }

    private void SetFielderActionCapturePose(string clipName, float timeSeconds)
    {
        if (clipName is not ("fielder-catch" or "fielder-pickup" or "fielder-throw"))
            throw new ArgumentException("Fielder action capture must name fielder-catch, fielder-pickup, or fielder-throw.", nameof(clipName));

        var duration = GetAnimationDuration(_bowlerAsset, clipName);
        if (timeSeconds > duration)
            throw new ArgumentOutOfRangeException(nameof(timeSeconds), timeSeconds,
                $"Fielder action capture time must be between 0 and {duration:0.###} seconds.");

        var fielderIndex = 0;
        _fielderActionClips[fielderIndex] = clipName;
        _fielderActionHoldAtEnd[fielderIndex] = true;
        _fielderAnimators[fielderIndex].PlayOnce(clipName, 0.001f);
        _fielderAnimators[fielderIndex].Update(MathF.Max(0.001f, timeSeconds));
        var fielderWorld = ToXna(_fieldingSide.Positions[fielderIndex]);
        _camera.Focus(fielderWorld + new Vector3(0f, 0.85f, 0f), 4.5f, 0f, 0.22f, "Fielder action");
    }

    private void SetBowlerDeliveryCapturePose(float timeSeconds)
    {
        var deliveryDuration = GetAnimationDuration(_bowlerAsset, "overarm-delivery");
        _bowlerRunUpElapsed = _bowlerRunUpDurationSeconds;
        _bowlerActionElapsed = timeSeconds;
        _bowlerActionStarted = true;
        _bowlerActionFinished = false;
        _bowlerReleased = false;
        _bowlerAnimator.Play("overarm-delivery", 0.001f);
        var previewTime = MathF.Min(MathF.Max(timeSeconds, 0.001f), MathF.Max(0f, deliveryDuration - 0.001f));
        _bowlerAnimator.Update(previewTime);
        _trajectoryVertices.Clear();
    }

    private void SetBowlerRunUpCapturePose(float timeSeconds)
    {
        _bowlerRunUpElapsed = timeSeconds;
        _bowlerActionElapsed = 0f;
        _bowlerActionStarted = false;
        _bowlerActionFinished = false;
        _bowlerReleased = false;
        _bowlerAnimator.Play("bowling-run-up", 0.001f);
        _bowlerAnimator.Update(timeSeconds);
        _trajectoryVertices.Clear();
    }

    private static float GetAnimationDuration(PlayerAsset asset, string clipName)
    {
        var animation = asset.Animations.Find(clip =>
            string.Equals(clip.Name, clipName, StringComparison.OrdinalIgnoreCase));
        return animation?.DurationSeconds
            ?? throw new InvalidDataException($"Player asset '{asset.Name}' is missing animation clip '{clipName}'.");
    }

    private static float GetAnimationEventTime(PlayerAsset asset, string clipName, string eventName)
    {
        var animation = asset.Animations.Find(clip =>
            string.Equals(clip.Name, clipName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"Player asset '{asset.Name}' is missing animation clip '{clipName}'.");
        var animationEvent = animation.Events.Find(candidate =>
            string.Equals(candidate.Name, eventName, StringComparison.OrdinalIgnoreCase));
        return animationEvent?.TimeSeconds
            ?? throw new InvalidDataException($"Player clip '{clipName}' is missing required event '{eventName}'.");
    }

    private static void RequireAnimation(PlayerAsset asset, string clipName) =>
        _ = GetAnimationDuration(asset, clipName);

    private void SelectNextDelivery(int index)
    {
        if (_deliveryPresets.Length == 0)
            return;
        _nextDeliveryPresetIndex = Math.Clamp(index, 0, _deliveryPresets.Length - 1);
    }

    private void StartShot(string name, float? horizontalAimOverride = null)
    {
        if (_simulationPaused || _shotResolved || _ballFlight.CurrentFrame.Phase == BallMotionPhase.Settled)
            return;

        var authoredShot = _shotSet.Get(name);
        if (horizontalAimOverride is { } horizontalAim)
        {
            if (!float.IsFinite(horizontalAim) || horizontalAim is < -1f or > 1f)
                throw new ArgumentOutOfRangeException(nameof(horizontalAimOverride), "Shot direction must be between -1 and 1.");
            _chosenShot = new BattingShotData
            {
                Name = authoredShot.Name,
                AnimationClip = authoredShot.AnimationClip,
                LaunchAngleDegrees = authoredShot.LaunchAngleDegrees,
                HorizontalAim = horizontalAim,
                SpeedTransfer = authoredShot.SpeedTransfer
            };
        }
        else
        {
            _chosenShot = authoredShot;
        }
        _shotResolved = false;
        _shotOutcome = $"Swinging {_chosenShot.Name}; timing and placement decide contact.";
        _playerAnimator.Play(_chosenShot.AnimationClip, 0.12f);
    }

    private void StepBatterFootwork(float direction)
    {
        _targetBatterFootworkOffsetX = BatterFootwork.AddStep(_targetBatterFootworkOffsetX, direction);
        _footworkTransitionActive = MathF.Abs(_targetBatterFootworkOffsetX - _batterFootworkOffsetX) > 0.0001f;
        if (_footworkTransitionActive && _chosenShot is null)
            _playerAnimator.Play("between-wickets", 0.08f);
    }

    private void UpdateBatterFootwork(float deltaTime)
    {
        _batterFootworkOffsetX = BatterFootwork.Advance(
            _batterFootworkOffsetX,
            _targetBatterFootworkOffsetX,
            deltaTime);
        if (!_footworkTransitionActive || MathF.Abs(_targetBatterFootworkOffsetX - _batterFootworkOffsetX) > 0.0001f)
            return;

        _footworkTransitionActive = false;
        if (_chosenShot is null && !_isRunning)
            _playerAnimator.Play("practice-stance", 0.12f);
    }

    private void StartRun()
    {
        if (_simulationPaused || _deliveryComplete || _isRunning)
            return;
        if (!_battedBall)
        {
            _runRequestedPending = _chosenShot is not null && !_shotResolved;
            return;
        }

        _runRequestedPending = false;
        if (IsCpuBattingControlled)
        {
            if (_cpuRunsRemaining <= 0)
                return;
            _cpuRunsRemaining--;
        }
        _targetBatterFootworkOffsetX = 0f;
        _footworkTransitionActive = MathF.Abs(_batterFootworkOffsetX) > 0.0001f;
        _isRunning = true;
        _runElapsed = 0f;
        _playerAnimator.Play("between-wickets", 0.12f);
    }

    private void CancelRun()
    {
        if (_simulationPaused) return;
        if (_runRequestedPending)
        {
            _runRequestedPending = false;
            if (IsCpuBattingControlled)
                _cpuRunsRemaining = 0;
            return;
        }
        if (!_isRunning)
            return;
        if (IsCpuBattingControlled)
            _cpuRunsRemaining = 0;
        _isRunning = false;
        _runElapsed = 0f;
        _playerAnimator.Play("practice-stance", 0.12f);
    }

    private void UpdateRun(float deltaTime)
    {
        _runElapsed += deltaTime;
        if (_runElapsed < _runDurationSeconds)
            return;

        CompleteRun();
    }

    private void UpdateFielderAnimations(float deltaTime)
    {
        var chaserIndex = _battedBall && !_deliveryComplete && !_fielderThrowActive
            ? _fieldingSide.ActiveChaserIndex
            : -1;
        for (var fielderIndex = 0; fielderIndex < _fielderAnimators.Length; fielderIndex++)
        {
            var animation = _fielderAnimators[fielderIndex];
            var actionClip = _fielderActionClips[fielderIndex];
            if (actionClip is not null)
            {
                if (!string.Equals(animation.CurrentClipName, actionClip, StringComparison.OrdinalIgnoreCase))
                    animation.PlayOnce(actionClip, 0.12f);
                if (!animation.IsOneShotComplete)
                    animation.Update(deltaTime);

                var ballSecuredTime = actionClip switch
                {
                    "fielder-catch" => _fielderCatchBallSecuredTimeSeconds,
                    "fielder-pickup" => _fielderPickupBallSecuredTimeSeconds,
                    _ => float.PositiveInfinity
                };
                if (!_fielderBallSecured && animation.CurrentTimeSeconds >= ballSecuredTime)
                {
                    _fielderBallSecured = true;
                    _ballFlight.StopAtContact(_fielderHeldBallPosition);
                }

                if (_fielderSequencePhase == FielderSequencePhase.Pickup &&
                    fielderIndex == _fielderThrowerIndex && animation.IsOneShotComplete)
                {
                    _fielderSequencePhase = FielderSequencePhase.Throw;
                    StartFielderAction(fielderIndex, "fielder-throw", holdAtEnd: true);
                }
                else if (animation.IsOneShotComplete && !_fielderActionHoldAtEnd[fielderIndex])
                {
                    _fielderActionClips[fielderIndex] = null;
                }
                continue;
            }

            var clipName = fielderIndex == chaserIndex ? "between-wickets" : "practice-stance";
            if (!string.Equals(animation.CurrentClipName, clipName, StringComparison.OrdinalIgnoreCase))
                animation.Play(clipName, 0.16f);
            animation.Update(deltaTime);
        }
    }

    private void UpdateFielderThrow()
    {
        if (_fielderSequencePhase != FielderSequencePhase.Throw)
            return;

        var animation = _fielderAnimators[_fielderThrowerIndex];
        _fielderThrowBallReleased = animation.CurrentTimeSeconds >= _fielderThrowReleaseTimeSeconds;
        if (!animation.IsOneShotComplete)
            return;

        _ballFlight.StopAtContact(_fielderThrowTarget);
        _fielderThrowActive = false;
        _fielderThrowBallReleased = false;
        _fielderSequencePhase = FielderSequencePhase.None;
        _fielderActionClips[_fielderThrowerIndex] = null;
        _fielderActionHoldAtEnd[_fielderThrowerIndex] = false;
        if (_isRunning)
        {
            CurrentDelivery.ResolveRunOut();
            _shotOutcome = $"OUT: fielder {_fielderThrowerIndex + 1} threw to the wicketkeeper";
        }
        else
        {
            _shotOutcome = $"Wicketkeeper received fielder {_fielderThrowerIndex + 1}'s throw";
        }
        FinishDelivery();
    }

    private void CompleteRun(bool recordScoring = true, bool allowNextRun = true)
    {
        if (recordScoring)
            CurrentDelivery.RecordCompletedRun();
        _liveCompletedRunCrossings++;
        _isRunning = false;
        _runElapsed = 0f;
        _shotOutcome = $"RUN completed: {_batterRuns} batter run(s)";
        _playerAnimator.Play("practice-stance", 0.12f);
        if (allowNextRun && IsCpuBattingControlled && _cpuRunsRemaining > 0 && !_deliveryComplete)
            StartRun();
    }

    private void ResolveIncomingDelivery(NumericsVector3 wicketLinePosition)
    {
        var contact = ToXna(wicketLinePosition);
        var resolution = CurrentDelivery.ResolveIncoming(
            isWide: MathF.Abs(contact.X) > _deliveryPreset.PitchWidthMeters / 2f + 0.55f,
            hitsWickets: MathF.Abs(contact.X) <= 0.12f + _deliveryPreset.BallRadiusMeters &&
                contact.Y >= 0f && contact.Y <= PracticeGround.WicketHeight + _deliveryPreset.BallRadiusMeters);
        switch (resolution)
        {
            case IncomingDeliveryResolution.NoBall:
                _shotOutcome = "NO-BALL: one penalty run, delivery not counted";
                FinishDelivery();
                break;
            case IncomingDeliveryResolution.Wide:
                _shotOutcome = "WIDE: one extra run, delivery not counted";
                FinishDelivery();
                break;
            case IncomingDeliveryResolution.Bowled:
                _shotOutcome = "OUT: bowled";
                FinishDelivery();
                break;
            default:
                if (_chosenShot is null)
                    _shotOutcome = "DOT: missed the stumps";
                FinishDelivery();
                break;
        }

        _ballFlight.StopAtContact(wicketLinePosition);
    }

    private void ResolveFieldingContact(FieldingContact contact)
    {
        var fielderPosition = _fieldingSide.Positions[contact.FielderIndex];
        _fielderHeldBallPosition = new NumericsVector3(
            fielderPosition.X,
            fielderPosition.Y + 1.02f,
            fielderPosition.Z);
        _fielderBallSecured = false;

        if (contact.Kind == FieldingContactKind.Catch)
        {
            _cpuRunsRemaining = 0;
            _runRequestedPending = false;
            StartFielderAction(contact.FielderIndex, "fielder-catch", holdAtEnd: true);
            if (!CurrentDelivery.ResolveCatch())
            {
                _shotOutcome = $"NO-BALL: fielder {contact.FielderIndex + 1} caught it; one penalty run";
            }
            else
            {
                _shotOutcome = $"OUT: caught by fielder {contact.FielderIndex + 1}";
            }
        }
        else if (_isRunning)
        {
            _fielderThrowActive = true;
            _fielderThrowBallReleased = false;
            _fielderSequencePhase = FielderSequencePhase.Pickup;
            _fielderThrowerIndex = contact.FielderIndex;
            _fielderThrowStart = ToNumerics(contact.Position);
            _fielderThrowTarget = new NumericsVector3(0f, 0.55f, -PracticeGround.WicketOffset);
            StartFielderAction(contact.FielderIndex, "fielder-pickup", holdAtEnd: false);
            _shotOutcome = $"Fielder {contact.FielderIndex + 1} picked up; throw to wicketkeeper";
        }
        else
        {
            StartFielderAction(contact.FielderIndex, "fielder-pickup", holdAtEnd: false);
            _shotOutcome = $"Fielder {contact.FielderIndex + 1} collected the ball";
        }

        _ballFlight.StopAtContact(ToNumerics(contact.Position));
        if (!_fielderThrowActive)
            FinishDelivery();
    }

    private void ResolveSettledBall(BallFlightFrame frame)
    {
        if (_isRunning)
        {
            var runCompleted = _runElapsed / _runDurationSeconds >= 0.72f;
            var runOut = CurrentDelivery.ResolveRunAtStoppage(runCompleted);
            if (runCompleted)
                CompleteRun(recordScoring: false, allowNextRun: false);
            else if (runOut)
            {
                _shotOutcome = "OUT: run out while attempting a run";
            }
            else
            {
                _isRunning = false;
            }
        }

        if (_shotOutcome.StartsWith("Choose", StringComparison.Ordinal))
            _shotOutcome = _battedBall ? "DOT: field held the shot" : "DOT ball";
        FinishDelivery();
    }

    private void ResolveBoundaryCrossing(BoundaryCrossing crossing)
    {
        CurrentDelivery.ResolveBoundary(crossing.ClearedInTheAir, _isRunning && _runElapsed >= _runDurationSeconds * 0.5f);
        _shotOutcome = crossing.ClearedInTheAir ? "SIX: cleared the boundary" : "FOUR: reached the boundary";
        _cpuRunsRemaining = 0;
        _runRequestedPending = false;
        _isRunning = false;
        _ballFlight.StopAtContact(crossing.Position);
        FinishDelivery();
    }

    private void FinishDelivery()
    {
        if (_deliveryComplete)
            return;

        _match.CompleteDelivery();
        _isRunning = false;
        _runRequestedPending = false;
        _cpuRunsRemaining = 0;
        _liveCompletedRunCrossings = 0;
        _fielderThrowActive = false;
        _fielderSequencePhase = FielderSequencePhase.None;
        if (_dismissal != DismissalKind.None)
        {
            if (_dismissal is DismissalKind.Bowled or DismissalKind.RunOut)
                _groundVertices = PracticeGround.CreateField(nearWicketBroken: true);
            _playerAnimator.Play("practice-stance", 0.12f);
        }
    }

    private static bool TryCrossPlane(NumericsVector3 previous, NumericsVector3 current, float planeZ, out NumericsVector3 crossing)
    {
        if (previous.Z < planeZ || current.Z > planeZ || MathF.Abs(current.Z - previous.Z) < 0.000001f)
        {
            crossing = default;
            return false;
        }

        var amount = (planeZ - previous.Z) / (current.Z - previous.Z);
        crossing = NumericsVector3.Lerp(previous, current, Math.Clamp(amount, 0f, 1f));
        return true;
    }

    private void BuildShadowVertices(Matrix strikerWorld, Matrix nonStrikerWorld, NumericsVector3 ballPosition)
    {
        _shadowVertices.Clear();
        var striker = strikerWorld.Translation;
        var nonStriker = nonStrikerWorld.Translation;
        PracticeGround.AppendSoftShadow(
            _shadowVertices,
            new Vector3(striker.X, -0.018f, striker.Z),
            0.52f,
            0.82f,
            74);
        PracticeGround.AppendSoftShadow(
            _shadowVertices,
            new Vector3(nonStriker.X, -0.018f, nonStriker.Z),
            0.52f,
            0.82f,
            74);
        var bowler = GetBowlerWorld().Translation;
        PracticeGround.AppendSoftShadow(
            _shadowVertices,
            new Vector3(bowler.X, -0.018f, bowler.Z),
            0.48f,
            0.82f,
            74);

        foreach (var fielderPosition in _fieldingSide.Positions)
        {
            var fielder = ToXna(fielderPosition);
            var overPitch = MathF.Abs(fielder.X) <= PracticeGround.PitchWidth / 2f &&
                MathF.Abs(fielder.Z) <= PracticeGround.WicketOffset;
            PracticeGround.AppendSoftShadow(
                _shadowVertices,
                new Vector3(fielder.X, overPitch ? -0.018f : -0.068f, fielder.Z),
                0.48f,
                0.72f,
                52);
        }

        if (_bowlerReleased)
        {
            var ballOverPitch = MathF.Abs(ballPosition.X) <= PracticeGround.PitchWidth / 2f &&
                MathF.Abs(ballPosition.Z) <= PracticeGround.WicketOffset;
            var groundHeight = ballOverPitch ? -0.025f : -0.075f;
            var ballHeight = MathF.Max(0f, ballPosition.Y - groundHeight);
            var opacity = (byte)Math.Clamp(72f / (1f + ballHeight * 0.55f), 12f, 72f);
            var ballRadiusX = Math.Clamp(0.09f + ballHeight * 0.11f, 0.09f, 1.2f);
            var ballRadiusZ = Math.Clamp(0.12f + ballHeight * 0.13f, 0.12f, 1.5f);
            var ballCenter = ToXna(ballPosition);
            ballCenter.Y = groundHeight + 0.006f;
            PracticeGround.AppendSoftShadow(_shadowVertices, ballCenter, ballRadiusX, ballRadiusZ, opacity);
        }
    }

    private void DrawShadows()
    {
        if (_shadowVertices.Count == 0)
            return;

        GraphicsDevice.BlendState = BlendState.NonPremultiplied;
        GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
        _lineEffect.World = Matrix.Identity;
        _lineEffect.View = _worldEffect.View;
        _lineEffect.Projection = _worldEffect.Projection;
        var shadows = _shadowVertices.ToArray();
        foreach (var pass in _lineEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(
                PrimitiveType.TriangleList,
                shadows,
                0,
                shadows.Length / 3);
        }
        GraphicsDevice.BlendState = BlendState.Opaque;
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
    }

    private void DrawTexturedSurfaces()
    {
        _surfaceEffect.World = Matrix.Identity;
        _surfaceEffect.View = _worldEffect.View;
        _surfaceEffect.Projection = _worldEffect.Projection;
        GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

        _surfaceEffect.Texture = _outfieldTexture;
        foreach (var pass in _surfaceEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(
                PrimitiveType.TriangleList,
                _outfieldVertices,
                0,
                _outfieldVertices.Length / 3);
        }

        _surfaceEffect.Texture = _pitchTexture;
        foreach (var pass in _surfaceEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(
                PrimitiveType.TriangleList,
                _pitchVertices,
                0,
                _pitchVertices.Length / 3);
        }
    }

    private void DrawCrowd()
    {
        if (_crowdVertexBuffer is null || _crowdPrimitiveCount == 0)
            return;

        _crowdEffect.World = Matrix.Identity;
        _crowdEffect.View = _worldEffect.View;
        _crowdEffect.Projection = _worldEffect.Projection;
        foreach (var pass in _crowdEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.SetVertexBuffer(_crowdVertexBuffer);
            GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, _crowdPrimitiveCount);
            GraphicsDevice.SetVertexBuffer(null);
        }
    }

    private Texture2D LoadTexture(string path)
    {
        using var stream = File.OpenRead(path);
        return Texture2D.FromStream(GraphicsDevice, stream);
    }

    private (Matrix Striker, Matrix NonStriker) GetBatterWorlds()
    {
        var strikerStartsNear = _liveCompletedRunCrossings % 2 == 0;
        var strikerStartZ = strikerStartsNear ? NearBatterZ : FarBatterZ;
        var strikerEndZ = strikerStartsNear ? FarBatterZ : NearBatterZ;
        var nonStrikerStartZ = strikerStartsNear ? FarBatterZ : NearBatterZ;
        var nonStrikerEndZ = strikerStartsNear ? NearBatterZ : FarBatterZ;
        if (_isRunning)
        {
            var progress = MathHelper.Clamp(_runElapsed / _runDurationSeconds, 0f, 1f);
            return (
                BatterWorld(MathHelper.Lerp(strikerStartZ, strikerEndZ, progress), strikerStartsNear, _batterFootworkOffsetX),
                BatterWorld(MathHelper.Lerp(nonStrikerStartZ, nonStrikerEndZ, progress), !strikerStartsNear));
        }

        return (
            BatterWorld(strikerStartZ, strikerStartsNear, _batterFootworkOffsetX),
            BatterWorld(nonStrikerStartZ, !strikerStartsNear));
    }

    private static Matrix BatterWorld(float z, bool atNearEnd, float lateralOffsetX = 0f) =>
        Matrix.CreateRotationY(atNearEnd ? 0f : MathHelper.Pi) *
        Matrix.CreateTranslation(new Vector3(-0.48f + lateralOffsetX, -0.025f, z));

    private Matrix GetFielderWorld(int fielderIndex, NumericsVector3 ballPosition)
    {
        var position = _fieldingSide.Positions[fielderIndex];
        var isChasing = fielderIndex == _fieldingSide.ActiveChaserIndex && _battedBall && !_deliveryComplete;
        var isThrowing = fielderIndex == _fielderThrowerIndex && _fielderSequencePhase == FielderSequencePhase.Throw;
        var targetX = isThrowing ? _fielderThrowTarget.X : isChasing ? ballPosition.X : 0f;
        var targetZ = isThrowing ? _fielderThrowTarget.Z : isChasing ? ballPosition.Z : 0f;
        var yaw = MathF.Atan2(targetX - position.X, targetZ - position.Z);
        return Matrix.CreateRotationY(yaw) *
            Matrix.CreateScale(0.94f) *
            Matrix.CreateTranslation(ToXna(position));
    }

    private void StartFielderAction(int fielderIndex, string clipName, bool holdAtEnd)
    {
        _fielderActionClips[fielderIndex] = clipName;
        _fielderActionHoldAtEnd[fielderIndex] = holdAtEnd;
        _fielderAnimators[fielderIndex].PlayOnce(clipName, 0.12f);
    }

    private NumericsVector3 GetFielderThrowBallPosition()
    {
        if (_fielderSequencePhase == FielderSequencePhase.Pickup)
        {
            var pickupTime = _fielderAnimators[_fielderThrowerIndex].CurrentTimeSeconds;
            var securedAmount = Math.Clamp(
                pickupTime / MathF.Max(0.001f, _fielderPickupBallSecuredTimeSeconds),
                0f,
                1f);
            return NumericsVector3.Lerp(_fielderThrowStart, _fielderHeldBallPosition, securedAmount);
        }

        if (_fielderSequencePhase != FielderSequencePhase.Throw || !_fielderThrowBallReleased)
            return _fielderHeldBallPosition;

        if (_fielderThrowDurationSeconds <= _fielderThrowReleaseTimeSeconds)
            return _fielderThrowStart;

        var animationTime = _fielderAnimators[_fielderThrowerIndex].CurrentTimeSeconds;
        var flightDuration = MathF.Max(0.001f, _fielderThrowDurationSeconds - _fielderThrowReleaseTimeSeconds);
        var flightAmount = Math.Clamp((animationTime - _fielderThrowReleaseTimeSeconds) / flightDuration, 0f, 1f);
        var position = NumericsVector3.Lerp(_fielderHeldBallPosition, _fielderThrowTarget, flightAmount);
        position.Y += 3.6f * flightAmount * (1f - flightAmount);
        return position;
    }

    private Matrix GetBowlerWorld()
    {
        var release = _deliveryPreset.ReleasePosition;
        var facing = Matrix.CreateRotationY(MathHelper.Pi);
        var releasePosition = new Vector3(
            release.X + BowlerReleaseHandOffsetXMeters,
            -0.025f,
            release.Z + BowlerHandForwardMeters);
        var position = releasePosition;
        if (_bowlerActionStarted)
        {
            var deliveryMotion = _bowlerActionFinished
                ? _bowlerAnimator.GetRootMotionAtEnd("overarm-delivery")
                : _bowlerAnimator.GetCurrentClipRootMotion();
            position += Vector3.TransformNormal(deliveryMotion, facing);
        }
        else
        {
            var finalRootMotion = _bowlerAnimator.GetRootMotionAtEnd("bowling-run-up");
            var currentRootMotion = _bowlerRunUpElapsed >= _bowlerRunUpDurationSeconds
                ? finalRootMotion
                : _bowlerAnimator.GetRootMotion();
            var startPosition = releasePosition - Vector3.TransformNormal(finalRootMotion, facing);
            position = startPosition + Vector3.TransformNormal(currentRootMotion, facing);
        }

        return facing * Matrix.CreateTranslation(position);
    }

    private bool TryBatContact(
        NumericsVector3 previousBall,
        NumericsVector3 currentBall,
        Matrix batStartWorld,
        Matrix batEndWorld,
        float batPoseDeltaSeconds,
        out BattingContact contact)
    {
        contact = default;
        var expansion = _deliveryPreset.BallRadiusMeters + _shotSet.ContactPaddingMeters;
        if (!SweptBattingContactResolver.TryResolve(
            previousBall,
            currentBall,
            ToNumerics(batStartWorld),
            ToNumerics(batEndWorld),
            ToNumerics(_batBladeMinimum),
            ToNumerics(_batBladeMaximum),
            expansion,
            batPoseDeltaSeconds,
            out var resolved))
            return false;

        contact = new BattingContact(
            ToXna(resolved.Position),
            ToXna(resolved.SweetSpotPosition),
            new Vector2(resolved.NormalizedSweetSpotOffset.X, resolved.NormalizedSweetSpotOffset.Y),
            ToXna(resolved.BatPointVelocity),
            resolved.HitFraction);
        return true;
    }

    private Matrix GetBatWorldTransform()
    {
        var skinMatrices = _playerAnimator.GetSkinMatrices();
        return skinMatrices[_batBoneIndex] * BatterWorld(NearBatterZ, true, _batterFootworkOffsetX);
    }

    private float GetBatPoseFraction(float physicsElapsed, float accumulatorBeforeFrame, float elapsedSeconds, float flightElapsed)
    {
        if (elapsedSeconds <= 0.000001f)
            return 1f;

        var afterReleaseFraction = 1f - Math.Clamp(flightElapsed / elapsedSeconds, 0f, 1f);
        return Math.Clamp(afterReleaseFraction + (physicsElapsed - accumulatorBeforeFrame) / elapsedSeconds, 0f, 1f);
    }

    private float GetBatPoseDeltaSeconds(float physicsElapsed, float accumulatorBeforeFrame, float elapsedSeconds, float flightElapsed)
    {
        if (elapsedSeconds <= 0.000001f)
            return 0f;

        var start = GetBatPoseFraction(physicsElapsed, accumulatorBeforeFrame, elapsedSeconds, flightElapsed);
        var end = GetBatPoseFraction(physicsElapsed + _ballFlight.FixedTimeStepSeconds, accumulatorBeforeFrame, elapsedSeconds, flightElapsed);
        return MathF.Max(0f, end - start) * elapsedSeconds;
    }

    private Matrix InterpolateBatWorld(float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        if (amount <= 0f)
            return _previousBatWorld;
        if (amount >= 1f)
            return _currentBatWorld;

        if (!_previousBatWorld.Decompose(out var previousScale, out var previousRotation, out var previousTranslation) ||
            !_currentBatWorld.Decompose(out var currentScale, out var currentRotation, out var currentTranslation))
            return Matrix.Lerp(_previousBatWorld, _currentBatWorld, amount);

        return Matrix.CreateScale(Vector3.Lerp(previousScale, currentScale, amount)) *
            Matrix.CreateFromQuaternion(Quaternion.Slerp(previousRotation, currentRotation, amount)) *
            Matrix.CreateTranslation(Vector3.Lerp(previousTranslation, currentTranslation, amount));
    }

    private static (Vector3 Minimum, Vector3 Maximum) FindBounds(float[] positions)
    {
        var minimum = new Vector3(float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity);
        for (var offset = 0; offset < positions.Length; offset += 3)
        {
            var point = new Vector3(positions[offset], positions[offset + 1], positions[offset + 2]);
            minimum = Vector3.Min(minimum, point);
            maximum = Vector3.Max(maximum, point);
        }
        return (minimum, maximum);
    }

    private static string FormatPosition(Vector3 value) =>
        $"({value.X:0.00}, {value.Y:0.00}, {value.Z:0.00})";

    private static Vector3 ToXna(NumericsVector3 value) => new(value.X, value.Y, value.Z);
    private static NumericsVector3 ToNumerics(Vector3 value) => new(value.X, value.Y, value.Z);
    private static System.Numerics.Matrix4x4 ToNumerics(Matrix value) => new(
        value.M11, value.M12, value.M13, value.M14,
        value.M21, value.M22, value.M23, value.M24,
        value.M31, value.M32, value.M33, value.M34,
        value.M41, value.M42, value.M43, value.M44);
}
