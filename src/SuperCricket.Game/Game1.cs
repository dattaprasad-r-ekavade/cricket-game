using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SuperCricket.Game.Rendering;

namespace SuperCricket.Game;

public class Game1 : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private SpriteFont _debugFont = null!;
    private Texture2D _debugPanel = null!;
    private BasicEffect _worldEffect = null!;
    private readonly OrbitCamera _camera = new();
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
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
            Keyboard.GetState().IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        _camera.Update(gameTime);
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
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(
                PrimitiveType.TriangleList,
                _groundVertices,
                0,
                _groundVertices.Length / 3);

            _worldEffect.World = Matrix.CreateScale(PracticeGround.BallRadius)
                * Matrix.CreateTranslation(PracticeGround.BallStart);
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(
                PrimitiveType.TriangleList,
                _ballVertices,
                0,
                _ballVertices.Length / 3);
            _worldEffect.World = Matrix.Identity;
        }

        DrawDebugOverlay();
        base.Draw(gameTime);
    }

    private void DrawDebugOverlay()
    {
        var lines = new[]
        {
            "SUPER CRICKET  /  PRACTICE GROUND",
            $"Pitch {PracticeGround.PitchLength:0.00} m x {PracticeGround.PitchWidth:0.00} m    Stumps {PracticeGround.WicketHeight:0.00} m",
            $"Ball start ({PracticeGround.BallStart.X:0.00}, {PracticeGround.BallStart.Y:0.00}, {PracticeGround.BallStart.Z:0.00}) m",
            $"Camera distance {_camera.Distance:0.0} m    elevation {MathHelper.ToDegrees(_camera.Elevation):0}°    FPS {_framesPerSecond}    {_frameTimeMilliseconds:0.0} ms",
            "Arrows: orbit    PgUp/PgDn: elevation    Mouse wheel: zoom    Home: reset    Esc: quit"
        };
        var panel = new Rectangle(16, 16, 820, 140);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(_debugPanel, panel, Color.White);
        for (var index = 0; index < lines.Length; index++)
        {
            var color = index == 0 ? new Color(242, 206, 116) : Color.White;
            _spriteBatch.DrawString(_debugFont, lines[index], new Vector2(30, 25 + index * 23), color);
        }
        _spriteBatch.End();
    }
}
