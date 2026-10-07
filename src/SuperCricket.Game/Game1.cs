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
    private readonly string? _liveMatchReviewPath;
    private bool IsReviewRun => _verifyGameplay || _liveMatchReviewPath is not null;
    private readonly int _profileFrameTarget;
    private readonly float? _captureRunUpTimeSeconds;
    private readonly float? _captureDeliveryTimeSeconds;
    private readonly float? _captureBallFlightTimeSeconds;
    private readonly string? _captureFielderActionClip;
    private readonly float? _captureFielderActionTimeSeconds;
    private readonly string? _captureBatterFootworkActionClip;
    private readonly float? _captureBatterFootworkActionTimeSeconds;
    private readonly bool _captureBowlingTarget;
    private readonly bool _captureFeedbackPreview;
    private readonly CpuDifficulty? _captureDifficultyOverride;
    private readonly bool _captureCameraPresetSpecified;
    private bool _developerMode;
    private SpriteBatch _spriteBatch = null!;
    private SpriteFont _debugFont = null!;
    private Texture2D _debugPanel = null!;
    private Texture2D _feedbackMapPixel = null!;
    private Texture2D _feedbackMapDot = null!;
    private Texture2D _feedbackMapRing = null!;
    private readonly Dictionary<CricketAudioCue, SoundEffect> _audioCues = [];
    private BasicEffect _worldEffect = null!;
    private BasicEffect _crowdEffect = null!;
    private BasicEffect _surfaceEffect = null!;
    private BasicEffect _lineEffect = null!;
    private Texture2D _outfieldTexture = null!;
    private Texture2D _pitchTexture = null!;
    private VertexBuffer? _crowdVertexBuffer;
    private RenderTarget2D? _captureTarget;
    private readonly CameraDirector _camera = new();
    private Rectangle _matchHudBounds;
    private readonly List<VertexPositionColor> _trajectoryVertices = [];
    private readonly List<VertexPositionColor> _shadowVertices = [];
    private readonly VertexPositionColor[] _debugMarkerVertices = new VertexPositionColor[18];
    private readonly VertexPositionColor[] _bowlingTargetMarkerVertices = new VertexPositionColor[28];
    private readonly VertexPositionColor[] _feedbackRingVertices = new VertexPositionColor[64];
    private readonly List<FrameTiming> _profileTimings = [];
    private DeliveryPreset[] _deliveryPresets = [];
    private int _nextDeliveryPresetIndex;
    private int _activeDeliveryPresetIndex;
    private GameSettings _gameSettings = new();
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
    private MatchController _matchController = null!;
    private LimitedOversMatch _match => _matchController.Match;
    private readonly FieldingSide _fieldingSide = new();
    private readonly MatchInputRouter _inputRouter = new();
    private float _simulationAccumulator;
    private bool _simulationPaused;
    private bool _audioUnavailable;
    private float _bowlerRunUpDurationSeconds;
    private float _bowlerRunUpElapsed;
    private float _bowlerActionElapsed;
    private float _bowlerReleaseTimeSeconds;
    private int _selectedOversPerInnings = 1;
    private float _batterFootworkOffsetX;
    private float _targetBatterFootworkOffsetX;
    private float _humanShotAimOffset;
    private bool _battingStepRecoveryActive;
    private bool _footworkTransitionActive;
    private bool _bowlerActionStarted;
    private bool _bowlerActionFinished;
    private bool _bowlerReleased;
    private bool _showDebugOverlay;
    private bool _lastInputWasGamePad;
    private float _runHoldElapsed;
    private float _bowlingAimOffsetX;
    private float _bowlingAimOffsetZ;
    private Vector3? _bowlingTargetMarkerPosition;
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
    private string ScoreStatusText => MatchHudPresenter.GetScoreStatus(new MatchScoreStatusState(
        _match.IsMatchComplete,
        _match.ResultText,
        _match.InningsNumber,
        _match.BattingTeamName,
        _match.Runs,
        _match.Wickets,
        _match.OversText,
        _match.OversPerInnings,
        _match.Target,
        _match.CurrentBowler.Name));
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
    private string? _settingsStatusMessage;
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
        int profileFrameCount = 0,
        string? liveMatchReviewPath = null,
        string? captureBatterFootworkActionClip = null,
        float? captureBatterFootworkActionTimeSeconds = null,
        float? captureBallFlightTimeSeconds = null,
        bool developerMode = false,
        bool captureBowlingTarget = false,
        bool captureFeedbackPreview = false,
        CpuDifficulty? captureDifficultyOverride = null)
    {
        if ((captureRunUpTimeSeconds is not null && captureDeliveryTimeSeconds is not null) ||
            captureBallFlightTimeSeconds is not null &&
                (captureRunUpTimeSeconds is not null || captureDeliveryTimeSeconds is not null ||
                 captureFielderActionClip is not null || captureBatterFootworkActionClip is not null) ||
            (captureFielderActionClip is null) != (captureFielderActionTimeSeconds is null) ||
            (captureBatterFootworkActionClip is null) != (captureBatterFootworkActionTimeSeconds is null) ||
            (captureFielderActionClip is not null && captureBatterFootworkActionClip is not null) ||
            (captureFielderActionClip is not null || captureBatterFootworkActionClip is not null) &&
                (captureRunUpTimeSeconds is not null || captureDeliveryTimeSeconds is not null) ||
            captureBatterFootworkActionClip is not null && captureBatterFootworkActionClip is not
                ("batting-step-offside" or "batting-step-legside") ||
            captureBowlingTarget && (capturePath is null || captureRunUpTimeSeconds is not null ||
                captureDeliveryTimeSeconds is not null || captureBallFlightTimeSeconds is not null ||
                captureFielderActionClip is not null || captureBatterFootworkActionClip is not null) ||
            captureFeedbackPreview && (capturePath is null || captureRunUpTimeSeconds is not null ||
                captureDeliveryTimeSeconds is not null || captureBallFlightTimeSeconds is not null ||
                captureFielderActionClip is not null || captureBatterFootworkActionClip is not null) ||
            captureDifficultyOverride is not null && capturePath is null ||
            profileFrameCount is < 0 or > 36000)
            throw new ArgumentException("Choose one bowler preview time, fielder action, or batter-footwork action and its preview time.");
        _capturePath = capturePath;
        _verifyGameplay = verifyGameplay;
        _liveMatchReviewPath = liveMatchReviewPath;
        _profileFrameTarget = profileFrameCount;
        _profileWarmupFrameCount = Math.Min(60, profileFrameCount / 4);
        _profileWarmupRemaining = _profileWarmupFrameCount;
        _developerMode = developerMode;
        _showDebugOverlay = captureDebugOverlay || developerMode;
        _captureRunUpTimeSeconds = captureRunUpTimeSeconds;
        _captureDeliveryTimeSeconds = captureDeliveryTimeSeconds;
        _captureBallFlightTimeSeconds = captureBallFlightTimeSeconds;
        _captureBowlingTarget = captureBowlingTarget;
        _captureFeedbackPreview = captureFeedbackPreview;
        _captureDifficultyOverride = captureDifficultyOverride;
        _captureCameraPresetSpecified = captureCameraPreset is not null;
        _captureFielderActionClip = captureFielderActionClip;
        _captureFielderActionTimeSeconds = captureFielderActionTimeSeconds;
        _captureBatterFootworkActionClip = captureBatterFootworkActionClip;
        _captureBatterFootworkActionTimeSeconds = captureBatterFootworkActionTimeSeconds;
        if (captureCameraPreset is not null && !_camera.SelectPreset(captureCameraPreset))
            throw new ArgumentException($"Unknown capture camera '{captureCameraPreset}'. Use broadcast, behind-striker, bowler-end, square-leg, or ball-follow.", nameof(captureCameraPreset));
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "Super Cricket — Short Match";
        _graphics.PreferredBackBufferWidth = 1440;
        _graphics.PreferredBackBufferHeight = 900;
        _graphics.SynchronizeWithVerticalRetrace = true;
        IsFixedTimeStep = false;
    }


}
