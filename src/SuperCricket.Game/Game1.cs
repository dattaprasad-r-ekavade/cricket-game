using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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
    private BallFlightSimulator _ballFlight = null!;
    private KeyboardState _previousKeyboard;
    private float _simulationAccumulator;
    private bool _simulationPaused;
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
        }
        if (keyboard.IsKeyDown(Keys.Space) && !_previousKeyboard.IsKeyDown(Keys.Space))
        {
            _simulationPaused = !_simulationPaused;
        }
        _previousKeyboard = keyboard;
        _camera.Update(gameTime);

        if (!_simulationPaused)
        {
            _simulationAccumulator += (float)Math.Min(gameTime.ElapsedGameTime.TotalSeconds, 0.25);
            while (_simulationAccumulator >= _ballFlight.FixedTimeStepSeconds &&
                   _ballFlight.CurrentFrame.Phase != BallMotionPhase.Settled)
            {
                var frame = _ballFlight.Step();
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

        DrawDebugOverlay();
        base.Draw(gameTime);
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
            $"Camera distance {_camera.Distance:0.0} m    elevation {MathHelper.ToDegrees(_camera.Elevation):0}°    FPS {_framesPerSecond}    {_frameTimeMilliseconds:0.0} ms",
            "Arrows: orbit    PgUp/PgDn: elevation    Mouse wheel: zoom    Home: reset    Space: pause    R: replay    Esc: quit"
        };
        var panel = new Rectangle(16, 16, 900, 164);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(_debugPanel, panel, Color.White);
        for (var index = 0; index < lines.Length; index++)
        {
            var color = index == 0 ? new Color(242, 206, 116) : Color.White;
            _spriteBatch.DrawString(_debugFont, lines[index], new Vector2(30, 23 + index * 23), color);
        }
        _spriteBatch.End();
    }

    private void RestartDelivery()
    {
        _ballFlight = new BallFlightSimulator(_deliveryPreset);
        _simulationAccumulator = 0f;
        _simulationPaused = false;
        _trajectoryVertices.Clear();
        _trajectoryVertices.Add(new VertexPositionColor(
            ToXna(_ballFlight.CurrentFrame.Position) + new Vector3(0f, 0.01f, 0f),
            new Color(248, 181, 82)));
    }

    private static Vector3 ToXna(NumericsVector3 value) => new(value.X, value.Y, value.Z);
}
