using System;
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
    private DeliveryPreset _deliveryPreset = null!;
    private BattingShotSet _shotSet = null!;
    private BallFlightSimulator _ballFlight = null!;
    private PlayerAsset _playerAsset = null!;
    private PlayerAnimator _playerAnimator = null!;
    private SkinnedPlayerRenderer _playerRenderer = null!;
    private KeyboardState _previousKeyboard;
    private float _simulationAccumulator;
    private bool _simulationPaused;
    private BattingShotData? _chosenShot;
    private bool _shotResolved;
    private string _shotOutcome = "Choose a shot before the ball reaches the batter.";
    private int _batBoneIndex;
    private Vector3 _batBladeMinimum;
    private Vector3 _batBladeMaximum;
    private readonly Matrix _playerWorld = Matrix.CreateTranslation(new Vector3(-0.48f, -0.025f, -8.72f));
    private VertexPositionColor[] _groundVertices = [];
    private VertexPositionColor[] _ballVertices = [];
    private double _fpsElapsed;
    private int _frameCount;
    private int _framesPerSecond;
    private double _frameTimeMilliseconds;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "Super Cricket — Practice Ground";
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
        var presetPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Deliveries", "standard-pace.json");
        _deliveryPreset = DeliveryPreset.Load(presetPath);
        RestartDelivery();
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
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
            keyboard.IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        if (keyboard.IsKeyDown(Keys.R) && !_previousKeyboard.IsKeyDown(Keys.R))
        {
            RestartDelivery();
            _playerAnimator.Play("practice-stance", 0.12f);
        }
        if (keyboard.IsKeyDown(Keys.Space) && !_previousKeyboard.IsKeyDown(Keys.Space))
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
        _previousKeyboard = keyboard;
        _camera.Update(gameTime);
        _playerAnimator.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

        if (!_simulationPaused)
        {
            _simulationAccumulator += (float)Math.Min(gameTime.ElapsedGameTime.TotalSeconds, 0.25);
            while (_simulationAccumulator >= _ballFlight.FixedTimeStepSeconds &&
                   _ballFlight.CurrentFrame.Phase != BallMotionPhase.Settled)
            {
                var previousFrame = _ballFlight.CurrentFrame;
                var frame = _ballFlight.Step();
                if (_chosenShot is not null && !_shotResolved &&
                    TryBatContact(previousFrame.Position, frame.Position, out var contactPoint, out var hitQuality))
                {
                    var contactVelocity = CreateShotVelocity(frame.Velocity, _chosenShot, hitQuality);
                    _ballFlight.ApplyBatContact(ToNumerics(contactPoint), ToNumerics(contactVelocity));
                    frame = _ballFlight.CurrentFrame;
                    _shotResolved = true;
                    _shotOutcome = $"HIT: {_chosenShot.Name} at {contactVelocity.Length():0.0} m/s";
                }
                else if (_chosenShot is not null && !_shotResolved && frame.Position.Z < _playerWorld.Translation.Z - 0.45f && frame.Velocity.Z < 0f)
                {
                    _shotResolved = true;
                    _shotOutcome = $"MISS: {_chosenShot.Name} swung outside contact";
                }

                _trajectoryVertices.Add(new VertexPositionColor(
                    ToXna(frame.Position) + new Vector3(0f, 0.01f, 0f),
                    new Color(248, 181, 82)));
                _simulationAccumulator -= _ballFlight.FixedTimeStepSeconds;
            }
        }
        if (_ballFlight.CurrentFrame.Phase == BallMotionPhase.Settled)
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
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(116, 161, 195));
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        GraphicsDevice.BlendState = BlendState.Opaque;

        _worldEffect.World = Matrix.Identity;
        _worldEffect.View = Matrix.CreateLookAt(_camera.Position, new Vector3(0f, 0f, 0f), Vector3.Up);
        _worldEffect.Projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(48f),
            GraphicsDevice.Viewport.AspectRatio,
            0.05f,
            250f);

        foreach (var pass in _worldEffect.CurrentTechnique.Passes)
        {
            _worldEffect.World = Matrix.Identity;
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(
                PrimitiveType.TriangleList,
                _groundVertices,
                0,
                _groundVertices.Length / 3);

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

            var ball = _ballFlight.CurrentFrame;
            _worldEffect.World = Matrix.CreateScale(_deliveryPreset.BallRadiusMeters)
                * Matrix.CreateTranslation(ToXna(ball.Position));
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _ballVertices, 0, _ballVertices.Length / 3);
            _worldEffect.World = Matrix.Identity;
        }

        _playerRenderer.Draw(_playerWorld, _worldEffect.View, _worldEffect.Projection, _playerAnimator.GetSkinMatrices());
        DrawDebugOverlay();
        base.Draw(gameTime);
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
        var ball = _ballFlight.CurrentFrame;
        var lines = new[]
        {
            "SUPER CRICKET  /  PRACTICE GROUND",
            $"Pitch {PracticeGround.PitchLength:0.00} m x {PracticeGround.PitchWidth:0.00} m    Stumps {PracticeGround.WicketHeight:0.00} m",
            $"Preset: {_deliveryPreset.Name}    release ({_deliveryPreset.ReleasePosition.X:0.00}, {_deliveryPreset.ReleasePosition.Y:0.00}, {_deliveryPreset.ReleasePosition.Z:0.00}) m",
            $"Ball {(_simulationPaused ? "Paused" : ball.Phase.ToString())}    speed {ball.Velocity.Length():0.0} m/s    bounces {ball.BounceCount}    position ({ball.Position.X:0.0}, {ball.Position.Y:0.0}, {ball.Position.Z:0.0}) m",
            $"Player: {_playerAsset.Name}    animation {_playerAnimator.CurrentClipName}{(_playerAnimator.IsTransitioning ? " (crossfade)" : string.Empty)}",
            $"Shot: {_shotOutcome}",
            $"Camera distance {_camera.Distance:0.0} m    elevation {MathHelper.ToDegrees(_camera.Elevation):0}°    FPS {_framesPerSecond}    {_frameTimeMilliseconds:0.0} ms",
            "A: defend    S: drive    D: loft    Arrows: orbit    PgUp/PgDn: elevation    wheel: zoom    Home: reset    Space: pause    R: replay    T: clips    Esc: quit"
        };
        var panel = new Rectangle(16, 16, 1100, 210);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(_debugPanel, panel, Color.White);
        for (var index = 0; index < lines.Length; index++)
        {
            var color = index == 0 ? new Color(242, 206, 116) : Color.White;
            _spriteBatch.DrawString(_debugFont, lines[index], new Vector2(30, 17 + index * 23), color);
        }
        _spriteBatch.End();
    }

    private void RestartDelivery()
    {
        _ballFlight = new BallFlightSimulator(_deliveryPreset);
        _simulationAccumulator = 0f;
        _simulationPaused = false;
        _chosenShot = null;
        _shotResolved = false;
        _shotOutcome = "Choose a shot before the ball reaches the batter.";
        _trajectoryVertices.Clear();
        _trajectoryVertices.Add(new VertexPositionColor(
            ToXna(_ballFlight.CurrentFrame.Position) + new Vector3(0f, 0.01f, 0f),
            new Color(248, 181, 82)));
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

    private bool TryBatContact(NumericsVector3 previousBall, NumericsVector3 currentBall, out Vector3 contactPoint, out float hitQuality)
    {
        contactPoint = default;
        hitQuality = 0f;
        var skinMatrices = _playerAnimator.GetSkinMatrices();
        var batWorld = skinMatrices[_batBoneIndex] * _playerWorld;
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
