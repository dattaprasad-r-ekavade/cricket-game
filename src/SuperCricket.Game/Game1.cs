using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SuperCricket.Content;
using SuperCricket.Game.Animation;
using SuperCricket.Game.Rendering;
using SuperCricket.Simulation;
using NumericsVector3 = System.Numerics.Vector3;

namespace SuperCricket.Game;

public class Game1 : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private SpriteFont _debugFont = null!;
    private Texture2D _debugPanel = null!;
    private BasicEffect _worldEffect = null!;
    private readonly OrbitCamera _camera = new();
    private readonly List<VertexPositionColor> _trajectoryVertices = [];
    private readonly List<VertexPositionColor> _fielderDrawVertices = [];
    private DeliveryPreset[] _deliveryPresets = [];
    private int _nextDeliveryPresetIndex;
    private int _activeDeliveryPresetIndex;
    private DeliveryPreset _deliveryPreset = null!;
    private BattingShotSet _shotSet = null!;
    private BallFlightSimulator _ballFlight = null!;
    private PlayerAsset _playerAsset = null!;
    private PlayerAnimator _playerAnimator = null!;
    private SkinnedPlayerRenderer _playerRenderer = null!;
    private FieldPreset _fieldPreset = null!;
    private OverScoreboard _scoreboard = new();
    private readonly FieldingSide _fieldingSide = new();
    private KeyboardState _previousKeyboard;
    private float _simulationAccumulator;
    private bool _simulationPaused;
    private bool _showDebugOverlay;
    private BattingShotData? _chosenShot;
    private bool _shotResolved;
    private bool _deliveryComplete;
    private bool _battedBall;
    private int _batterRuns;
    private int _extraRuns;
    private int _completedRuns;
    private DeliveryExtra _extraType;
    private DismissalKind _dismissal;
    private bool _isRunning;
    private bool _runRequestedPending;
    private float _runElapsed;
    private float _runDurationSeconds = 1.35f;
    private bool _fielderThrowActive;
    private float _fielderThrowElapsed;
    private const float FielderThrowDurationSeconds = 0.3f;
    private NumericsVector3 _fielderThrowStart;
    private NumericsVector3 _fielderThrowTarget;
    private int _fielderThrowerIndex;
    private const float NearBatterZ = -8.72f;
    private const float FarBatterZ = 8.72f;
    private string _shotOutcome = "Choose a shot before the ball reaches the batter.";
    private int _batBoneIndex;
    private Vector3 _batBladeMinimum;
    private Vector3 _batBladeMaximum;
    private VertexPositionColor[] _groundVertices = [];
    private VertexPositionColor[] _ballVertices = [];
    private VertexPositionColor[] _fielderMarkerVertices = [];
    private double _fpsElapsed;
    private int _frameCount;
    private int _framesPerSecond;
    private double _frameTimeMilliseconds;
    private double _updateMilliseconds;
    private double _drawMilliseconds;

    public Game1()
    {
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
        _ballVertices = PracticeGround.CreateBall();
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
            LightingEnabled = false
        };
        _deliveryPresets =
        [
            DeliveryPreset.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Deliveries", "standard-pace.json")),
            DeliveryPreset.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Deliveries", "wide-pace.json")),
            DeliveryPreset.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Deliveries", "no-ball-pace.json"))
        ];
        _fieldPreset = FieldPreset.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Fields", "practice-attack.json"));
        _fieldingSide.ConfigureStartingPositions(
            _fieldPreset.Players.ConvertAll(player => ToNumerics(player.Position.ToVector3())));
        var playerPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Characters", "practice-batter.scplayer.json");
        _playerAsset = PlayerAsset.Load(playerPath);
        _playerAnimator = new PlayerAnimator(_playerAsset);
        _playerRenderer = new SkinnedPlayerRenderer(GraphicsDevice, _playerAsset);
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
        _fielderMarkerVertices = PracticeGround.CreateFielderMarker();
        StartNewOver();
    }

    protected override void Update(GameTime gameTime)
    {
        var updateStart = Stopwatch.GetTimestamp();
        var keyboard = Keyboard.GetState();
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
            keyboard.IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        if (keyboard.IsKeyDown(Keys.R) && !_previousKeyboard.IsKeyDown(Keys.R))
        {
            StartNewOver();
        }
        if (keyboard.IsKeyDown(Keys.N) && !_previousKeyboard.IsKeyDown(Keys.N) &&
            _deliveryComplete && !_scoreboard.IsOverComplete)
        {
            BeginDelivery();
        }
        if (keyboard.IsKeyDown(Keys.D1) && !_previousKeyboard.IsKeyDown(Keys.D1)) SelectNextDelivery(0);
        if (keyboard.IsKeyDown(Keys.D2) && !_previousKeyboard.IsKeyDown(Keys.D2)) SelectNextDelivery(1);
        if (keyboard.IsKeyDown(Keys.D3) && !_previousKeyboard.IsKeyDown(Keys.D3)) SelectNextDelivery(2);
        if (keyboard.IsKeyDown(Keys.V) && !_previousKeyboard.IsKeyDown(Keys.V)) _camera.CyclePreset();
        if (keyboard.IsKeyDown(Keys.F1) && !_previousKeyboard.IsKeyDown(Keys.F1)) _showDebugOverlay = !_showDebugOverlay;
        if (keyboard.IsKeyDown(Keys.X) && !_previousKeyboard.IsKeyDown(Keys.X)) CancelRun();
        if (keyboard.IsKeyDown(Keys.P) && !_previousKeyboard.IsKeyDown(Keys.P))
        {
            _simulationPaused = !_simulationPaused;
        }
        if (keyboard.IsKeyDown(Keys.T) && !_previousKeyboard.IsKeyDown(Keys.T))
        {
            _playerAnimator.PlayNext();
        }
        if (keyboard.IsKeyDown(Keys.A) && !_previousKeyboard.IsKeyDown(Keys.A)) StartShot("defence");
        if (keyboard.IsKeyDown(Keys.S) && !_previousKeyboard.IsKeyDown(Keys.S)) StartShot("drive");
        if (keyboard.IsKeyDown(Keys.D) && !_previousKeyboard.IsKeyDown(Keys.D)) StartShot("loft");
        if (keyboard.IsKeyDown(Keys.Enter) && !_previousKeyboard.IsKeyDown(Keys.Enter)) StartRun();
        _previousKeyboard = keyboard;
        _camera.Update(gameTime);
        var elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _playerAnimator.Update(elapsedSeconds);
        if (_isRunning && (_fielderThrowActive || _ballFlight.CurrentFrame.Phase == BallMotionPhase.Settled))
            UpdateRun(elapsedSeconds);
        if (_fielderThrowActive)
            UpdateFielderThrow(elapsedSeconds);

        if (!_simulationPaused && !_deliveryComplete)
        {
            _simulationAccumulator += (float)Math.Min(gameTime.ElapsedGameTime.TotalSeconds, 0.25);
            while (_simulationAccumulator >= _ballFlight.FixedTimeStepSeconds &&
                   _ballFlight.CurrentFrame.Phase != BallMotionPhase.Settled)
            {
                var previousFrame = _ballFlight.CurrentFrame;
                if (_battedBall)
                    _fieldingSide.Step(_ballFlight.FixedTimeStepSeconds, previousFrame.Position);

                var frame = _ballFlight.Step();
                if (_chosenShot is not null && !_shotResolved &&
                    TryBatContact(previousFrame.Position, frame.Position, out var contactPoint, out var hitQuality))
                {
                    var contactVelocity = CreateShotVelocity(frame.Velocity, _chosenShot, hitQuality);
                    _ballFlight.ApplyBatContact(ToNumerics(contactPoint), ToNumerics(contactVelocity));
                    frame = _ballFlight.CurrentFrame;
                    _shotResolved = true;
                    _battedBall = true;
                    _fieldingSide.Reset();
                    _shotOutcome = $"HIT: {_chosenShot.Name} at {contactVelocity.Length():0.0} m/s";
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
        BuildFielderDrawVertices();

        foreach (var pass in _worldEffect.CurrentTechnique.Passes)
        {
            _worldEffect.World = Matrix.Identity;
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(
                PrimitiveType.TriangleList,
                _groundVertices,
                0,
                _groundVertices.Length / 3);

            if (_fielderDrawVertices.Count > 0)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(
                    PrimitiveType.TriangleList,
                    _fielderDrawVertices.ToArray(),
                    0,
                    _fielderDrawVertices.Count / 3);
            }

            if (_trajectoryVertices.Count >= 2)
            {
                var trajectory = _trajectoryVertices.ToArray();
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(
                    PrimitiveType.LineStrip,
                    trajectory,
                    0,
                    trajectory.Length - 1);
            }

            var ballPosition = _fielderThrowActive
                ? NumericsVector3.Lerp(_fielderThrowStart, _fielderThrowTarget,
                    Math.Clamp(_fielderThrowElapsed / FielderThrowDurationSeconds, 0f, 1f))
                : _ballFlight.CurrentFrame.Position;
            _worldEffect.World = Matrix.CreateScale(_deliveryPreset.BallRadiusMeters)
                * Matrix.CreateTranslation(ToXna(ballPosition));
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _ballVertices, 0, _ballVertices.Length / 3);
            _worldEffect.World = Matrix.Identity;
        }

        var skinMatrices = _playerAnimator.GetSkinMatrices();
        var (strikerWorld, nonStrikerWorld) = GetBatterWorlds();
        _playerRenderer.Draw(strikerWorld, _worldEffect.View, _worldEffect.Projection, skinMatrices);
        _playerRenderer.Draw(nonStrikerWorld, _worldEffect.View, _worldEffect.Projection, skinMatrices);
        DrawDebugOverlay();
        base.Draw(gameTime);
        _drawMilliseconds = Stopwatch.GetElapsedTime(drawStart).TotalMilliseconds;
    }

    protected override void UnloadContent()
    {
        _playerRenderer?.Dispose();
        _worldEffect?.Dispose();
        _debugPanel?.Dispose();
        _spriteBatch?.Dispose();
        base.UnloadContent();
    }

    private void DrawDebugOverlay()
    {
        if (!_showDebugOverlay)
        {
            var eventText = _shotOutcome.Length > 72 ? _shotOutcome[..69] + "..." : _shotOutcome;
            var matchLines = new[]
            {
                "SUPER CRICKET  /  ONE-OVER MATCH",
                $"{_scoreboard.Runs}/{_scoreboard.Wickets}    {_scoreboard.OversText} overs    legal balls {_scoreboard.LegalBalls}/6",
                eventText,
                "F1: debug    V: camera view    N: next ball"
            };
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            _spriteBatch.Draw(_debugPanel, new Rectangle(20, 20, 660, 116), Color.White);
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
            "SUPER CRICKET  /  ONE-OVER MATCH",
            $"Pitch {PracticeGround.PitchLength:0.00} m x {PracticeGround.PitchWidth:0.00} m    Stumps {PracticeGround.WicketHeight:0.00} m",
            $"Over {_scoreboard.OversText}    {_scoreboard.Runs}/{_scoreboard.Wickets}    Striker {_scoreboard.Striker}    legal balls {_scoreboard.LegalBalls}/6",
            $"Preset: {_deliveryPreset.Name}    next {_deliveryPresets[_nextDeliveryPresetIndex].Name}    release ({_deliveryPreset.ReleasePosition.X:0.00}, {_deliveryPreset.ReleasePosition.Y:0.00}, {_deliveryPreset.ReleasePosition.Z:0.00}) m",
            $"Ball {(_simulationPaused ? "Paused" : ball.Phase.ToString())}    speed {ball.Velocity.Length():0.0} m/s    bounces {ball.BounceCount}    position ({ball.Position.X:0.0}, {ball.Position.Y:0.0}, {ball.Position.Z:0.0}) m",
            $"Player: {_playerAsset.Name}    animation {_playerAnimator.CurrentClipName}{(_playerAnimator.IsTransitioning ? " (crossfade)" : string.Empty)}",
            $"Delivery: {(_deliveryComplete ? "complete" : "live")}    {_fieldPreset.Name} ({_fieldingSide.Positions.Count} fielders)    run {(_isRunning ? $"{MathHelper.Clamp(_runElapsed / _runDurationSeconds, 0f, 1f):P0}" : "ready")}",
            $"Event: {_shotOutcome}",
            $"View {_camera.PresetName}    distance {_camera.Distance:0.0} m    elevation {MathHelper.ToDegrees(_camera.Elevation):0}°    FPS {_framesPerSecond}    frame {_frameTimeMilliseconds:0.0} ms    CPU update/draw {_updateMilliseconds:0.00}/{_drawMilliseconds:0.00} ms",
            $"Scene vertices {_groundVertices.Length:N0}    fielder vertices {_fielderDrawVertices.Count:N0}    rendered players 2",
            "Arrows orbit    PgUp/PgDn height    wheel zoom    V camera    Home broadcast    F1 hide debug",
            "A defend    S drive    D loft    Enter run    X cancel    1-3 bowl    N next    R over    P pause    T clips    Esc quit"
        };
        var panel = new Rectangle(16, 16, GraphicsDevice.Viewport.Width - 32, 296);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(_debugPanel, panel, Color.White);
        for (var index = 0; index < lines.Length; index++)
        {
            var color = index == 0 ? new Color(242, 206, 116) : Color.White;
            _spriteBatch.DrawString(_debugFont, lines[index], new Vector2(30, 17 + index * 23), color);
        }
        _spriteBatch.End();
    }

    private void StartNewOver()
    {
        _scoreboard.Reset();
        _nextDeliveryPresetIndex = 0;
        BeginDelivery();
    }

    private void BeginDelivery()
    {
        if (_scoreboard.IsOverComplete)
            return;

        _groundVertices = PracticeGround.CreateField();
        _activeDeliveryPresetIndex = _nextDeliveryPresetIndex;
        _deliveryPreset = _deliveryPresets[_activeDeliveryPresetIndex];
        _ballFlight = new BallFlightSimulator(_deliveryPreset);
        _simulationAccumulator = 0f;
        _simulationPaused = false;
        _chosenShot = null;
        _shotResolved = false;
        _deliveryComplete = false;
        _battedBall = false;
        _batterRuns = 0;
        _extraRuns = 0;
        _completedRuns = 0;
        _extraType = DeliveryExtra.None;
        _dismissal = DismissalKind.None;
        _isRunning = false;
        _runRequestedPending = false;
        _runElapsed = 0f;
        _fielderThrowActive = false;
        _fielderThrowElapsed = 0f;
        _fieldingSide.Reset();
        _shotOutcome = "Choose a shot before the ball reaches the batter.";
        _playerAnimator.Play("practice-stance", 0.12f);
        _trajectoryVertices.Clear();
        _trajectoryVertices.Add(new VertexPositionColor(
            ToXna(_ballFlight.CurrentFrame.Position) + new Vector3(0f, 0.01f, 0f),
            new Color(248, 181, 82)));
    }

    private void SelectNextDelivery(int index)
    {
        if (_deliveryPresets.Length == 0)
            return;
        _nextDeliveryPresetIndex = Math.Clamp(index, 0, _deliveryPresets.Length - 1);
    }

    private void StartShot(string name)
    {
        if (_shotResolved || _ballFlight.CurrentFrame.Phase == BallMotionPhase.Settled)
            return;

        _chosenShot = _shotSet.Get(name);
        _shotResolved = false;
        _shotOutcome = $"Swinging {_chosenShot.Name}; timing and placement decide contact.";
        _playerAnimator.Play(_chosenShot.AnimationClip, 0.12f);
    }

    private void StartRun()
    {
        if (_deliveryComplete || _isRunning)
            return;
        if (!_battedBall)
        {
            _runRequestedPending = _chosenShot is not null && !_shotResolved;
            return;
        }

        _runRequestedPending = false;
        _isRunning = true;
        _runElapsed = 0f;
        _playerAnimator.Play("between-wickets", 0.12f);
    }

    private void CancelRun()
    {
        if (_runRequestedPending)
        {
            _runRequestedPending = false;
            return;
        }
        if (!_isRunning)
            return;
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

    private void UpdateFielderThrow(float deltaTime)
    {
        _fielderThrowElapsed += deltaTime;
        if (_fielderThrowElapsed < FielderThrowDurationSeconds)
            return;

        _fielderThrowActive = false;
        if (_isRunning)
        {
            _dismissal = DismissalKind.RunOut;
            _shotOutcome = $"OUT: fielder {_fielderThrowerIndex + 1} threw to the wicketkeeper";
        }
        else
        {
            _shotOutcome = $"Wicketkeeper received fielder {_fielderThrowerIndex + 1}'s throw";
        }
        FinishDelivery();
    }

    private void CompleteRun()
    {
        _batterRuns++;
        _completedRuns++;
        _isRunning = false;
        _runElapsed = 0f;
        _shotOutcome = $"RUN completed: {_batterRuns} batter run(s)";
        _playerAnimator.Play("practice-stance", 0.12f);
    }

    private void ResolveIncomingDelivery(NumericsVector3 wicketLinePosition)
    {
        var contact = ToXna(wicketLinePosition);
        if (_deliveryPreset.IsNoBall)
        {
            _extraRuns = 1;
            _extraType = DeliveryExtra.NoBall;
            _shotOutcome = "NO-BALL: one penalty run, delivery not counted";
            FinishDelivery();
        }
        else if (MathF.Abs(contact.X) > _deliveryPreset.PitchWidthMeters / 2f + 0.55f)
        {
            _extraRuns = 1;
            _extraType = DeliveryExtra.Wide;
            _shotOutcome = "WIDE: one extra run, delivery not counted";
            FinishDelivery();
        }
        else if (MathF.Abs(contact.X) <= 0.12f + _deliveryPreset.BallRadiusMeters &&
                 contact.Y >= 0f && contact.Y <= PracticeGround.WicketHeight + _deliveryPreset.BallRadiusMeters)
        {
            _dismissal = DismissalKind.Bowled;
            _shotOutcome = "OUT: bowled";
            FinishDelivery();
        }
        else
        {
            if (_chosenShot is null)
                _shotOutcome = "DOT: missed the stumps";
            FinishDelivery();
        }

        _ballFlight.StopAtContact(wicketLinePosition);
    }

    private void ResolveFieldingContact(FieldingContact contact)
    {
        if (contact.Kind == FieldingContactKind.Catch)
        {
            if (_deliveryPreset.IsNoBall)
            {
                _extraRuns = 1;
                _extraType = DeliveryExtra.NoBall;
                _shotOutcome = $"NO-BALL: fielder {contact.FielderIndex + 1} caught it; one penalty run";
            }
            else
            {
                _dismissal = DismissalKind.Caught;
                _shotOutcome = $"OUT: caught by fielder {contact.FielderIndex + 1}";
            }
        }
        else if (_isRunning)
        {
            _fielderThrowActive = true;
            _fielderThrowElapsed = 0f;
            _fielderThrowerIndex = contact.FielderIndex;
            _fielderThrowStart = ToNumerics(contact.Position);
            _fielderThrowTarget = new NumericsVector3(0f, 0.4f, -PracticeGround.WicketOffset);
            _shotOutcome = $"Fielder {contact.FielderIndex + 1} picked up; throw to wicketkeeper";
        }
        else
        {
            _shotOutcome = $"Fielder {contact.FielderIndex + 1} collected the ball";
        }

        if (_deliveryPreset.IsNoBall && _extraType == DeliveryExtra.None)
        {
            _extraRuns = 1;
            _extraType = DeliveryExtra.NoBall;
        }
        _ballFlight.StopAtContact(ToNumerics(contact.Position));
        if (!_fielderThrowActive)
            FinishDelivery();
    }

    private void ResolveSettledBall(BallFlightFrame frame)
    {
        var horizontalDistance = MathF.Sqrt(frame.Position.X * frame.Position.X + frame.Position.Z * frame.Position.Z);
        if (_battedBall && horizontalDistance >= _deliveryPreset.FieldBoundaryRadiusMeters - 0.001f)
        {
            var isSix = frame.BounceCount == 0 && frame.Position.Y > _deliveryPreset.FieldSurfaceHeightMeters + 1f;
            _batterRuns += isSix ? 6 : 4;
            _shotOutcome = isSix ? "SIX: cleared the boundary" : "FOUR: reached the boundary";
            _isRunning = false;
        }
        else if (_isRunning)
        {
            if (_runElapsed / _runDurationSeconds >= 0.72f)
                CompleteRun();
            else if (!_deliveryPreset.IsNoBall)
            {
                _dismissal = DismissalKind.RunOut;
                _shotOutcome = "OUT: run out while attempting a run";
            }
            else
            {
                _isRunning = false;
            }
        }

        if (_deliveryPreset.IsNoBall && _extraType == DeliveryExtra.None)
        {
            _extraRuns = 1;
            _extraType = DeliveryExtra.NoBall;
        }
        if (_shotOutcome.StartsWith("Choose", StringComparison.Ordinal))
            _shotOutcome = _battedBall ? "DOT: field held the shot" : "DOT ball";
        FinishDelivery();
    }

    private void FinishDelivery()
    {
        if (_deliveryComplete)
            return;

        var isLegal = _extraType is not (DeliveryExtra.Wide or DeliveryExtra.NoBall);
        var result = new DeliveryResult(
            _batterRuns,
            _extraRuns,
            _completedRuns,
            isLegal,
            _extraType,
            _dismissal,
            DismissedEnd.Striker);
        _scoreboard.RecordDelivery(result);
        _deliveryComplete = true;
        _isRunning = false;
        _runRequestedPending = false;
        _fielderThrowActive = false;
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

    private void BuildFielderDrawVertices()
    {
        _fielderDrawVertices.Clear();
        foreach (var position in _fieldingSide.Positions)
        {
            var offset = ToXna(position);
            foreach (var marker in _fielderMarkerVertices)
                _fielderDrawVertices.Add(new VertexPositionColor(marker.Position + offset, marker.Color));
        }
    }

    private (Matrix Striker, Matrix NonStriker) GetBatterWorlds()
    {
        if (_isRunning)
        {
            var progress = MathHelper.Clamp(_runElapsed / _runDurationSeconds, 0f, 1f);
            return (
                BatterWorld(MathHelper.Lerp(NearBatterZ, FarBatterZ, progress), true),
                BatterWorld(MathHelper.Lerp(FarBatterZ, NearBatterZ, progress), false));
        }

        return (BatterWorld(NearBatterZ, true), BatterWorld(FarBatterZ, false));
    }

    private static Matrix BatterWorld(float z, bool atNearEnd) =>
        Matrix.CreateRotationY(atNearEnd ? 0f : MathHelper.Pi) *
        Matrix.CreateTranslation(new Vector3(-0.48f, -0.025f, z));

    private bool TryBatContact(NumericsVector3 previousBall, NumericsVector3 currentBall, out Vector3 contactPoint, out float hitQuality)
    {
        contactPoint = default;
        hitQuality = 0f;
        var skinMatrices = _playerAnimator.GetSkinMatrices();
        var batWorld = skinMatrices[_batBoneIndex] * BatterWorld(NearBatterZ, true);
        var worldToBat = Matrix.Invert(batWorld);
        var localStart = Vector3.Transform(ToXna(previousBall), worldToBat);
        var localEnd = Vector3.Transform(ToXna(currentBall), worldToBat);
        var expansion = _deliveryPreset.BallRadiusMeters + _shotSet.ContactPaddingMeters;
        var padding = new Vector3(expansion);
        var minimum = _batBladeMinimum - padding;
        var maximum = _batBladeMaximum + padding;

        if (!SegmentIntersectsBox(localStart, localEnd, minimum, maximum, out var hitFraction))
            return false;

        contactPoint = Vector3.Lerp(ToXna(previousBall), ToXna(currentBall), hitFraction);
        var localContact = Vector3.Lerp(localStart, localEnd, hitFraction);
        var halfWidth = MathF.Max((_batBladeMaximum.X - _batBladeMinimum.X) / 2f, 0.001f);
        var lateralError = MathF.Abs(localContact.X - (_batBladeMinimum.X + _batBladeMaximum.X) / 2f) / (halfWidth + expansion);
        hitQuality = MathHelper.Clamp(1f - lateralError * 0.2f, 0.7f, 1f);
        return true;
    }

    private static Vector3 CreateShotVelocity(NumericsVector3 incomingVelocity, BattingShotData shot, float hitQuality)
    {
        var launchAngle = MathHelper.ToRadians(shot.LaunchAngleDegrees);
        var horizontalDirection = Vector3.Normalize(new Vector3(shot.HorizontalAim, 0f, -1f));
        var direction = new Vector3(
            horizontalDirection.X * MathF.Cos(launchAngle),
            MathF.Sin(launchAngle),
            horizontalDirection.Z * MathF.Cos(launchAngle));
        var outgoingSpeed = new Vector3(incomingVelocity.X, incomingVelocity.Y, incomingVelocity.Z).Length()
            * shot.SpeedTransfer * hitQuality;
        return direction * outgoingSpeed;
    }

    private static bool SegmentIntersectsBox(Vector3 start, Vector3 end, Vector3 minimum, Vector3 maximum, out float entry)
    {
        var direction = end - start;
        var lower = 0f;
        var upper = 1f;
        if (!ClipAxis(start.X, direction.X, minimum.X, maximum.X, ref lower, ref upper) ||
            !ClipAxis(start.Y, direction.Y, minimum.Y, maximum.Y, ref lower, ref upper) ||
            !ClipAxis(start.Z, direction.Z, minimum.Z, maximum.Z, ref lower, ref upper))
        {
            entry = 0f;
            return false;
        }

        entry = lower;
        return true;
    }

    private static bool ClipAxis(float origin, float direction, float minimum, float maximum, ref float lower, ref float upper)
    {
        if (MathF.Abs(direction) < 0.000001f)
            return origin >= minimum && origin <= maximum;

        var inverseDirection = 1f / direction;
        var first = (minimum - origin) * inverseDirection;
        var second = (maximum - origin) * inverseDirection;
        if (first > second)
            (first, second) = (second, first);
        lower = MathF.Max(lower, first);
        upper = MathF.Min(upper, second);
        return lower <= upper;
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

    private static Vector3 ToXna(NumericsVector3 value) => new(value.X, value.Y, value.Z);
    private static NumericsVector3 ToNumerics(Vector3 value) => new(value.X, value.Y, value.Z);
}
