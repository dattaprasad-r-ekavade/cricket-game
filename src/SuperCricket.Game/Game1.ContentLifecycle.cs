using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SuperCricket.Content;
using SuperCricket.Game.Animation;
using SuperCricket.Game.Rendering;
using SuperCricket.Simulation;
using NumericsVector3 = System.Numerics.Vector3;

namespace SuperCricket.Game;

public partial class Game1
{
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
        _feedbackMapPixel = new Texture2D(GraphicsDevice, 1, 1);
        _feedbackMapPixel.SetData([Color.White]);
        _feedbackMapDot = CreateCircularMarkerTexture(GraphicsDevice, 32, innerRadiusFraction: 0f);
        _feedbackMapRing = CreateCircularMarkerTexture(GraphicsDevice, 32, innerRadiusFraction: 0.58f);
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
            DeliveryPreset.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Deliveries", "no-ball-pace.json")),
            DeliveryPreset.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Deliveries", "yorker-pace.json"))
        ];
        _fieldPreset = FieldPreset.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Fields", "practice-attack.json"));
        _match = new LimitedOversMatch(
            TeamRosterAsset.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Teams", "coastal-xi.json")),
            TeamRosterAsset.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Teams", "highland-xi.json")),
            _selectedOversPerInnings);
        var playerPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Characters", "practice-batter-humanoid.glb");
        _playerAsset = PlayerAsset.Load(playerPath);
        _playerAnimator = new PlayerAnimator(_playerAsset);
        _playerRenderer = new SkinnedPlayerRenderer(GraphicsDevice, _playerAsset);
        var bowlerPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Characters", "practice-bowler-humanoid.glb");
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
        _battingTimingCalibration = BattingTimingCalibrationAsset.Load(
            Path.Combine(AppContext.BaseDirectory, "Assets", "Batting", "timing-calibration.json"));
        foreach (var shot in _shotSet.Shots)
        {
            if (!_playerAsset.Animations.Exists(clip => string.Equals(clip.Name, shot.AnimationClip, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException($"Shot '{shot.Name}' refers to missing player clip '{shot.AnimationClip}'.");
        }
        RequireAnimation(_playerAsset, "batting-step-offside");
        RequireAnimation(_playerAsset, "batting-step-legside");
        _batBoneIndex = _playerAsset.Bones.FindIndex(bone => string.Equals(bone.Name, "forearm.R", StringComparison.OrdinalIgnoreCase));
        var batMesh = _playerAsset.Meshes.Find(mesh => string.Equals(mesh.Name, "Bat Blade", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("Player asset is missing the Bat Blade mesh.");
        if (_batBoneIndex < 0)
            throw new InvalidDataException("Player asset is missing the forearm.R bat attachment bone.");
        (_batBladeMinimum, _batBladeMaximum) = FindBounds(batMesh.Positions);
        _currentBatWorld = GetBatWorldTransform();
        _previousBatWorld = _currentBatWorld;
        LoadGameSettings();
        if (_captureDifficultyOverride is { } captureDifficulty)
            _cpuDifficulty = captureDifficulty;
        LoadAudioCues();
        StartNewMatch();
        if (_captureTarget is not null)
        {
            if (_captureBowlingTarget)
                PrepareBowlingTargetCapture();

            if (_captureBatterFootworkActionClip is { } footworkClip &&
                _captureBatterFootworkActionTimeSeconds is { } footworkTime)
            {
                SetBatterFootworkCapturePose(footworkClip, footworkTime);
            }
            else if (_captureFielderActionClip is { } fielderActionClip && _captureFielderActionTimeSeconds is { } actionTime)
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
            else if (_captureBallFlightTimeSeconds is { } ballFlightTime)
            {
                if (ballFlightTime > _deliveryPreset.MaximumSimulationSeconds)
                    throw new ArgumentOutOfRangeException("captureBallFlightTimeSeconds", ballFlightTime,
                        $"Ball-flight capture time must be between 0 and {_deliveryPreset.MaximumSimulationSeconds:0.###} seconds.");
                SetBowlerCaptureReleasePose();
                var sampleCount = (int)MathF.Round(ballFlightTime / _ballFlight.FixedTimeStepSeconds);
                for (var step = 0; step < sampleCount && _ballFlight.CurrentFrame.Phase != BallMotionPhase.Settled; step++)
                {
                    var frame = _ballFlight.Step();
                    _trajectoryVertices.Add(new VertexPositionColor(
                        ToXna(frame.Position) + new Vector3(0f, 0.01f, 0f),
                        GetBallTrailColor(frame.Velocity.Length())));
                }
            }
            else
            {
                SetBowlerCaptureReleasePose();
            }
            if (_captureBowlingTarget && !_captureCameraPresetSpecified && IsHumanBowling)
                _camera.SelectPreset("ball-follow");
            if (_camera.FollowsBall)
                _camera.FollowBall(ToXna(_ballFlight.CurrentFrame.Position), 0f);
            _simulationPaused = true;
            if (_captureFeedbackPreview)
                PrepareFeedbackPreviewCapture();
        }
        if (_verifyGameplay)
        {
            RunGameplayReviewChecks();
            Exit();
        }
        if (_liveMatchReviewPath is { } reviewPath)
        {
            RunLiveMatchReviewChecks(reviewPath);
            Exit();
        }
    }


}
