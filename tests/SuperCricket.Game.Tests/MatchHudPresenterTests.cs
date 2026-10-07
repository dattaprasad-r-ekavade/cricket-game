using Microsoft.Xna.Framework;
using SuperCricket.Game;
using SuperCricket.Simulation;

namespace SuperCricket.Game.Tests;

public sealed class MatchHudPresenterTests
{
    [Fact]
    public void CompactOverlayBuildsHumanBattingLinesAndTruncatesLongEvents()
    {
        var lines = MatchHudPresenter.BuildCompactOverlayLines(new CompactMatchOverlayState(
            CpuDifficulty.Standard,
            "12/1 after 2 overs",
            new string('x', 73),
            false,
            false,
            false,
            "Mira Sen",
            "Rian Das",
            "Space: defend"));

        Assert.Equal("SUPER CRICKET / SHORT MATCH  |  CPU Standard  |  TRAIL cool = slower / warm = faster", lines[0]);
        Assert.Equal("12/1 after 2 overs", lines[1]);
        Assert.Equal("On strike: Mira Sen    Non-striker: Rian Das", lines[2]);
        Assert.Equal(new string('x', 69) + "...", lines[3]);
        Assert.Equal("Space: defend", lines[4]);
    }

    [Theory]
    [InlineData(true, false, true, "Match complete", "Match finished")]
    [InlineData(false, true, true, "Innings complete; press N to start the chase", "Recent shot")]
    [InlineData(false, false, true, "CPU batting: Asha    Non-striker: Veer", "Recent shot")]
    public void CompactOverlayPrioritizesCurrentMatchPhase(
        bool isMatchComplete,
        bool isInningsComplete,
        bool isCpuBattingControlled,
        string expectedBatterLine,
        string expectedEventLine)
    {
        var lines = MatchHudPresenter.BuildCompactOverlayLines(new CompactMatchOverlayState(
            CpuDifficulty.Rookie,
            "score",
            "Recent shot",
            isMatchComplete,
            isInningsComplete,
            isCpuBattingControlled,
            "Asha",
            "Veer",
            "hint"));

        Assert.Equal(expectedBatterLine, lines[2]);
        Assert.Equal(expectedEventLine, lines[3]);
    }

    [Theory]
    [InlineData(false, "LEFT / RIGHT", "SPACE", "SHIFT", "ENTER")]
    [InlineData(true, "LEFT STICK", "A", "Y", "B")]
    public void BattingActionPanelMapsLaneAndShotControls(bool isGamePad, string aim, string ground, string loft, string run)
    {
        var hints = MatchHudPresenter.BuildActionHints(new MatchHudState(
            isGamePad, MatchHudPhase.Batting, "Standard pace"));

        Assert.Equal(new HudActionHint(aim, isGamePad ? "Aim left / straight / right" : "Choose shot direction"), hints[0]);
        Assert.Equal(new HudActionHint(ground, isGamePad
            ? "Centre = defend  |  aimed = drive"
            : "Centre = defend  |  aimed = ground drive"), hints[1]);
        Assert.Equal(new HudActionHint(loft, "Loft the shot"), hints[2]);
        Assert.Equal(new HudActionHint(run, "Run after contact  |  hold to turn back"), hints[3]);
    }

    [Theory]
    [InlineData("defence", false, "SPACE at centre aim")]
    [InlineData("drive", false, "LEFT / RIGHT + SPACE")]
    [InlineData("loft", false, "SHIFT")]
    [InlineData("defence", true, "A at centre aim")]
    [InlineData("drive", true, "left stick + A")]
    [InlineData("loft", true, "Y")]
    public void ShotResultCanNameTheInputThatSelectedTheAction(string shot, bool gamePad, string expected) =>
        Assert.Equal(expected, MatchHudPresenter.GetShotControlLabel(shot, gamePad));

    [Theory]
    [InlineData("Perfect", 0f, "PERFECT")]
    [InlineData("Early", -0.042f, "EARLY 42 ms")]
    [InlineData("Late", 0.038f, "LATE 38 ms")]
    public void BattingTimingAssessmentFormatsBandAndAbsoluteOffset(
        string band,
        float offsetSeconds,
        string expected)
    {
        Assert.Equal(expected, MatchHudPresenter.FormatBattingTimingText(
            new BattingTimingAssessment(Enum.Parse<BattingTimingBand>(band), offsetSeconds)));
    }

    [Fact]
    public void LiveFeedbackPrefersBowlingAccuracyWhenOtherFeedbackIsPresent()
    {
        var content = MatchHudPresenter.BuildLiveFeedbackBanner(CreateLiveFeedbackState(
            isHumanBowling: true,
            activeTarget: new Vector3(0f, 0f, -4f),
            firstBounce: new Vector3(0.2f, 0f, -4.2f),
            contactQuality: 0.95f,
            timingCue: new BattingTimingCue(BattingTimingCueState.SwingNow, 0.5f, 0.4f, 0.6f, 0f)));

        Assert.NotNull(content);
        Assert.Equal("BOWLING | ON TARGET", content.Value.Title);
        Assert.Contains("from your aim", content.Value.Detail);
        Assert.Equal(new Color(74, 224, 255), content.Value.Accent);
        Assert.Equal(5f, content.Value.RemainingSeconds);
    }

    [Fact]
    public void LiveFeedbackBuildsBattingContactDetailWithTiming()
    {
        var content = MatchHudPresenter.BuildLiveFeedbackBanner(CreateLiveFeedbackState(
            contactQuality: 0.91f,
            shotName: "Drive",
            timingText: "EARLY 80 ms"));

        Assert.NotNull(content);
        Assert.Equal("BATTING | MIDDLE", content.Value.Title);
        Assert.Equal("Drive | TIMING EARLY 80 ms | 91% contact", content.Value.Detail);
        Assert.Equal(new Color(135, 255, 159), content.Value.Accent);
        Assert.Equal(5f, content.Value.RemainingSeconds);
    }

    [Fact]
    public void LiveFeedbackLinksShotTypeToTheControlThatSelectedIt()
    {
        var content = MatchHudPresenter.BuildLiveFeedbackBanner(CreateLiveFeedbackState(
            contactQuality: 0.91f,
            shotName: "drive",
            timingText: "PERFECT",
            shotControlLabel: "LEFT / RIGHT + SPACE"));

        Assert.NotNull(content);
        Assert.Equal("drive (LEFT / RIGHT + SPACE) | PERFECT TIMING | 91% contact", content.Value.Detail);
    }

    [Fact]
    public void LiveFeedbackBuildsControllerTimingPromptAndBounceHint()
    {
        var content = MatchHudPresenter.BuildLiveFeedbackBanner(CreateLiveFeedbackState(
            timingCue: new BattingTimingCue(BattingTimingCueState.SwingNow, 0.5f, 0.4f, 0.6f, 0f),
            isGamePad: true,
            highContrast: true,
            hasPredictedBounce: true,
            difficulty: CpuDifficulty.Pro));

        Assert.NotNull(content);
        Assert.Equal("SWING NOW", content.Value.Title);
        Assert.Equal("Press A / Y now for on-time contact | ring = projected bounce", content.Value.Detail);
        Assert.Equal(Color.Yellow, content.Value.Accent);
        Assert.Equal(1f, content.Value.RemainingSeconds);
    }

    [Fact]
    public void LiveFeedbackBuildsBounceSummaryOrReturnsNoBanner()
    {
        var bounce = MatchHudPresenter.BuildLiveFeedbackBanner(CreateLiveFeedbackState(
            firstBounce: Vector3.Zero));
        Assert.NotNull(bounce);
        Assert.Equal("YOUR DELIVERY | SHORT", bounce.Value.Title);
        Assert.Equal("on the stumps | 8.7 m from you", bounce.Value.Detail);

        var absent = MatchHudPresenter.BuildLiveFeedbackBanner(CreateLiveFeedbackState(remainingSeconds: 0f));
        Assert.Null(absent);
    }

    [Fact]
    public void BallTrailColorMapsSlowMediumAndFastSpeedsToCoolWarmAndHot()
    {
        Assert.Equal(new Color(68, 220, 255), MatchHudPresenter.GetBallTrailColor(8f));
        Assert.Equal(new Color(255, 226, 70), MatchHudPresenter.GetBallTrailColor(22f));
        Assert.Equal(new Color(255, 86, 58), MatchHudPresenter.GetBallTrailColor(36f));
        Assert.Equal(MatchHudPresenter.GetBallTrailColor(8f), MatchHudPresenter.GetBallTrailColor(float.NaN));
        Assert.Equal(MatchHudPresenter.GetBallTrailColor(8f), MatchHudPresenter.GetBallTrailColor(float.PositiveInfinity));
    }

    [Fact]
    public void BattingContactZoneAlphaStepsDownWithDifficulty()
    {
        Assert.Equal(220, MatchHudPresenter.GetBattingContactZoneAlpha(CpuDifficulty.Rookie));
        Assert.Equal(120, MatchHudPresenter.GetBattingContactZoneAlpha(CpuDifficulty.Standard));
        Assert.Equal(0, MatchHudPresenter.GetBattingContactZoneAlpha(CpuDifficulty.Pro));
    }

    [Fact]
    public void PauseMenuLayoutPreservesComfortableViewportSizing()
    {
        var layout = MatchHudPresenter.CalculatePauseMenuLayout(
            viewportWidth: 1280,
            viewportHeight: 720,
            largeText: false,
            lineCount: 11);

        Assert.Equal(new Rectangle(0, 0, 1280, 720), layout.OverlayBounds);
        Assert.Equal(new Rectangle(190, 156, 900, 407), layout.PanelBounds);
        Assert.Equal(1.1f, layout.TextScale);
        Assert.Equal(33, layout.LineSpacing);
    }

    [Fact]
    public void PauseMenuLayoutFitsDeveloperHelpInsideSmallerWindow()
    {
        var layout = MatchHudPresenter.CalculatePauseMenuLayout(
            viewportWidth: 640,
            viewportHeight: 480,
            largeText: true,
            lineCount: 12);

        Assert.Equal(new Rectangle(0, 0, 640, 480), layout.OverlayBounds);
        Assert.Equal(new Rectangle(20, 20, 600, 440), layout.PanelBounds);
        Assert.Equal(1.1f, layout.TextScale);
        Assert.Equal(33, layout.LineSpacing);
        Assert.True(layout.PanelBounds.Left >= 0);
        Assert.True(layout.PanelBounds.Top >= 0);
        Assert.True(layout.PanelBounds.Right <= layout.OverlayBounds.Right);
        Assert.True(layout.PanelBounds.Bottom <= layout.OverlayBounds.Bottom);
    }

    [Fact]
    public void FeedbackTextWrappingUsesMeasuredWidthAndKeepsContinuationIndent()
    {
        var lines = MatchHudPresenter.WrapTextLines(
            ["one two three", "", "unbroken"],
            availableWidth: 6,
            scale: 1f,
            measureText: text => text.Length);

        Assert.Equal(["one", "  two", "  three", "unbroken"], lines);
    }

    [Fact]
    public void FeedbackTextWrappingRejectsMissingInputs()
    {
        Assert.Throws<ArgumentNullException>(() => MatchHudPresenter.WrapTextLines(
            null!, 100, 1f, _ => 1));
        Assert.Throws<ArgumentNullException>(() => MatchHudPresenter.WrapTextLines(
            ["line"], 100, 1f, null!));
    }

    [Theory]
    [InlineData(0.88f, "MIDDLE")]
    [InlineData(0.75f, "CLEAN CONTACT")]
    [InlineData(0.60f, "EDGE CONTACT")]
    [InlineData(0.01f, "THIN CONTACT")]
    [InlineData(0f, "")]
    public void ContactFeedbackLabelUsesCalibratedQualityBands(float quality, string expected)
    {
        Assert.Equal(expected, MatchHudPresenter.GetContactFeedbackLabel(
            new ContactFeedbackPresentationState(false, quality, HighContrast: false)));
    }

    [Fact]
    public void ContactFeedbackLabelShowsMissAndMissingQualityStates()
    {
        Assert.Equal("NO CONTACT", MatchHudPresenter.GetContactFeedbackLabel(
            new ContactFeedbackPresentationState(true, 0.95f, HighContrast: false)));
        Assert.Equal(string.Empty, MatchHudPresenter.GetContactFeedbackLabel(
            new ContactFeedbackPresentationState(false, null, HighContrast: false)));
    }

    [Theory]
    [InlineData(0.90f, false, 135, 255, 159)]
    [InlineData(0.80f, false, 89, 232, 255)]
    [InlineData(0.65f, false, 255, 220, 85)]
    [InlineData(0.20f, false, 255, 143, 75)]
    [InlineData(0.80f, true, 255, 255, 0)]
    [InlineData(0.65f, true, 255, 0, 0)]
    public void ContactFeedbackColorReflectsQualityAndContrast(
        float quality,
        bool highContrast,
        byte red,
        byte green,
        byte blue)
    {
        Assert.Equal(new Color(red, green, blue), MatchHudPresenter.GetContactFeedbackColor(
            new ContactFeedbackPresentationState(false, quality, highContrast)));
    }

    [Fact]
    public void ContactFeedbackColorHandlesMissAndUnavailableQuality()
    {
        Assert.Equal(Color.Red, MatchHudPresenter.GetContactFeedbackColor(
            new ContactFeedbackPresentationState(true, null, HighContrast: true)));
        Assert.Equal(new Color(255, 91, 77), MatchHudPresenter.GetContactFeedbackColor(
            new ContactFeedbackPresentationState(true, null, HighContrast: false)));
        Assert.Equal(Color.White, MatchHudPresenter.GetContactFeedbackColor(
            new ContactFeedbackPresentationState(false, null, HighContrast: false)));
    }

    [Fact]
    public void PauseMenuListsControlsAndAccessibilitySettingsWithoutDebugActions()
    {
        var lines = MatchHudPresenter.BuildPauseMenuLines(new MatchPauseMenuState(
            "Innings 1/2    Coastal XI 4/0    0.1 overs",
            CpuDifficulty.Rookie,
            DeveloperMode: false,
            HighContrast: true,
            LargeText: false,
            EffectsVolume: 0.75f,
            SettingsStatusMessage: null,
            AudioUnavailable: false));

        Assert.Equal("SUPER CRICKET  /  PAUSED", lines[0]);
        Assert.Contains("Batting: arrows choose lane | Space: centre defend / aimed drive | Shift loft", lines);
        Assert.Contains("GamePad batting: left stick choose lane | A: centre defend / aimed drive | Y loft", lines);
        Assert.Contains("Running: Enter/B starts; tap again for another; hold to turn back", lines);
        Assert.Contains("Bowling: arrows/D-pad move pitch target | C/LB changes delivery | N/RB bowls", lines);
        Assert.Contains("Paused: P/Start resumes | Esc/Back quits | H/Y contrast ON", lines);
        Assert.Contains("T/Pad X: larger text OFF | -/LB volume down | +/RB volume up 75%", lines);
        Assert.Contains("Match, audio, and accessibility settings save on this device.", lines);
        Assert.DoesNotContain(lines, line => line.StartsWith("Debug:", StringComparison.Ordinal));
    }

    [Fact]
    public void PauseMenuShowsDebugActionsAndPrioritizesSettingsStatus()
    {
        var lines = MatchHudPresenter.BuildPauseMenuLines(new MatchPauseMenuState(
            "Current score",
            CpuDifficulty.Pro,
            DeveloperMode: true,
            HighContrast: false,
            LargeText: true,
            EffectsVolume: 1f,
            SettingsStatusMessage: "Settings saved.",
            AudioUnavailable: true));

        Assert.Equal("Debug: A/S/D shots | J/L aim | Q/E steps | 1-4 presets | arrows orbit | PgUp/PgDn height", lines[3]);
        Assert.Equal("Settings saved.", lines[^1]);
    }

    [Fact]
    public void PauseMenuExplainsUnavailableAudioWhenNoStatusMessageExists()
    {
        var lines = MatchHudPresenter.BuildPauseMenuLines(new MatchPauseMenuState(
            "Current score",
            CpuDifficulty.Standard,
            DeveloperMode: false,
            HighContrast: false,
            LargeText: false,
            EffectsVolume: 0.5f,
            SettingsStatusMessage: null,
            AudioUnavailable: true));

        Assert.Equal("Audio output is unavailable; the match remains playable.", lines[^1]);
    }

    [Fact]
    public void BattingResultCardExplainsContactAndTiming()
    {
        var lines = MatchHudPresenter.BuildDeliveryFeedbackLines(new DeliveryFeedbackState(
            new DeliveryResult(4, 0, 0),
            IsHumanBowling: false,
            DeliverySpeedKilometersPerHour: 123,
            FirstBouncePosition: new Vector3(0f, 0f, -10f),
            BatterWicketLineZ: -8.72f,
            ShotName: "drive",
            ContactQuality: 0.94f,
            TimingText: "PERFECT",
            ActiveBowlingTargetPosition: null));

        Assert.Equal(new[]
        {
            "YOUR BATTING RESULT  |  FOUR",
            "BALL  |  123 km/h  |  yorker on the stumps",
            "PITCH  |  1.3 m from striker",
            "YOUR SHOT  |  drive: middled (94%) | timing PERFECT"
        }, lines);
    }

    [Fact]
    public void BattingResultCardLinksFourToTheShotButtonAndDirectionInput()
    {
        var lines = MatchHudPresenter.BuildDeliveryFeedbackLines(new DeliveryFeedbackState(
            new DeliveryResult(4, 0, 0),
            IsHumanBowling: false,
            DeliverySpeedKilometersPerHour: 123,
            FirstBouncePosition: new Vector3(0f, 0f, -10f),
            BatterWicketLineZ: -8.72f,
            ShotName: "drive",
            ContactQuality: 0.94f,
            TimingText: "PERFECT",
            ActiveBowlingTargetPosition: null,
            ShotControlLabel: "LEFT / RIGHT + SPACE"));

        Assert.Contains("YOUR BATTING RESULT  |  FOUR", lines);
        Assert.Contains("YOUR SHOT  |  drive (LEFT / RIGHT + SPACE): middled (94%) | timing PERFECT", lines);
    }

    [Fact]
    public void BowlingResultCardShowsLandingAccuracyAndMissedContact()
    {
        var target = new Vector3(0f, 0f, -14f);
        var landing = new Vector3(0.3f, 0f, -14.5f);
        var lines = MatchHudPresenter.BuildDeliveryFeedbackLines(new DeliveryFeedbackState(
            new DeliveryResult(0, 0, 0),
            IsHumanBowling: true,
            DeliverySpeedKilometersPerHour: 118,
            FirstBouncePosition: landing,
            BatterWicketLineZ: -8.72f,
            ShotName: "drive",
            ContactQuality: null,
            TimingText: null,
            ActiveBowlingTargetPosition: target));

        Assert.Equal(new[]
        {
            "YOUR BOWLING RESULT  |  DOT BALL",
            "BALL  |  118 km/h  |  good length on the stumps",
            "PITCH  |  5.8 m from striker",
            "YOUR BOWL  |  ON TARGET (0.6 m from aim)",
            "BATTER  |  drive: no contact - ball beat the bat"
        }, lines);
        Assert.Equal(("ON TARGET", "good length | on the stumps"),
            MatchHudPresenter.GetBowlingFeedbackSummary(target, landing, -8.72f));
    }

    [Fact]
    public void FullTossBowlingCardReportsTheIntendedPitchPoint()
    {
        var lines = MatchHudPresenter.BuildDeliveryFeedbackLines(new DeliveryFeedbackState(
            new DeliveryResult(0, 1, 0, IsLegal: false, Extra: DeliveryExtra.Wide),
            IsHumanBowling: true,
            DeliverySpeedKilometersPerHour: 112,
            FirstBouncePosition: null,
            BatterWicketLineZ: -8.72f,
            ShotName: null,
            ContactQuality: null,
            TimingText: null,
            ActiveBowlingTargetPosition: new Vector3(-3f, 0f, -19f)));

        Assert.Equal(new[]
        {
            "YOUR BOWLING RESULT  |  WIDE - 1 extra run(s)",
            "BALL  |  112 km/h  |  full toss",
            "PITCH  |  full toss - no bounce",
            "YOUR BOWL  |  aimed short / wide left  |  full toss",
            "BATTER  |  No shot played"
        }, lines);
    }

    [Theory]
    [InlineData(false, "", 1, "Coastal XI", 10, 1, "1.2", 5, null, "Asha", "Innings 1/2    Coastal XI 10/1    1.2/5 overs    bowler Asha")]
    [InlineData(false, "", 2, "Highland XI", 8, 0, "0.4", 5, 11, "Dev", "Innings 2/2    Highland XI 8/0    0.4/5 overs    target 11    bowler Dev")]
    [InlineData(true, "Coastal XI wins by 2 runs", 2, "Highland XI", 9, 4, "1.0", 1, 11, "Dev", "Coastal XI wins by 2 runs")]
    public void ScoreStatusReflectsCurrentInningsAndResult(
        bool isMatchComplete,
        string resultText,
        int inningsNumber,
        string battingTeamName,
        int runs,
        int wickets,
        string oversText,
        int oversPerInnings,
        int? target,
        string currentBowlerName,
        string expected) =>
        Assert.Equal(expected, MatchHudPresenter.GetScoreStatus(new MatchScoreStatusState(
            isMatchComplete,
            resultText,
            inningsNumber,
            battingTeamName,
            runs,
            wickets,
            oversText,
            oversPerInnings,
            target,
            currentBowlerName)));

    [Theory]
    [InlineData(false, false, false, false, false, false, "Batting")]
    [InlineData(false, false, false, false, false, true, "BallBatted")]
    [InlineData(false, false, false, false, true, true, "Running")]
    [InlineData(false, false, false, true, true, true, "DeliveryComplete")]
    [InlineData(false, false, true, false, false, false, "Bowling")]
    [InlineData(false, false, true, true, false, false, "BowlingDeliveryComplete")]
    [InlineData(false, true, true, true, true, true, "InningsComplete")]
    [InlineData(true, true, true, true, true, true, "MatchComplete")]
    public void PhaseResolutionUsesMatchPriorityThenLiveDeliveryState(
        bool matchComplete,
        bool inningsComplete,
        bool bowling,
        bool deliveryComplete,
        bool running,
        bool battedBall,
        string expectedPhase)
    {
        var conditions = new MatchHudConditions(
            matchComplete, inningsComplete, bowling, deliveryComplete, running, battedBall);

        Assert.Equal(Enum.Parse<MatchHudPhase>(expectedPhase), MatchHudPresenter.ResolvePhase(conditions));
    }

    [Theory]
    [InlineData(false, "Left / Right: choose direction    Space: centre defend / aimed drive    Shift: loft    P: pause    V: camera    PgDn: zoom in / PgUp: out")]
    [InlineData(true, "Left stick: choose direction    A: centre defend / aimed drive    Y: loft    Start: pause    L3: camera")]
    public void BattingHintShowsOnlyTheActiveDeviceControls(bool isGamePad, string expected) =>
        Assert.Equal(expected, Hint(isGamePad, MatchHudPhase.Batting));

    [Theory]
    [InlineData(false, "R: replay    D: difficulty    O: overs    P: pause    V: camera    PgDn: zoom in / PgUp: out")]
    [InlineData(true, "A: replay    LB: difficulty    RB: overs    Start: pause    L3: camera")]
    public void CompletedMatchHintShowsReplayAndMatchOptions(bool isGamePad, string expected) =>
        Assert.Equal(expected, Hint(isGamePad, MatchHudPhase.MatchComplete));

    [Theory]
    [InlineData(false, "N: start the chase    P: pause    V: camera    PgDn: zoom in / PgUp: out")]
    [InlineData(true, "RB: start the chase    Start: pause    L3: camera")]
    public void CompletedInningsHintStartsTheChase(bool isGamePad, string expected) =>
        Assert.Equal(expected, Hint(isGamePad, MatchHudPhase.InningsComplete));

    [Theory]
    [InlineData(false, "Bowling", "Next pitch: arrows aim    C: delivery (Yorker; next ball)    P: pause    V: camera    PgDn: zoom in / PgUp: out")]
    [InlineData(true, "Bowling", "Next pitch: D-pad / left stick aim    LB: delivery (Yorker; next ball)    Start: pause    L3: camera")]
    [InlineData(false, "BowlingDeliveryComplete", "Next pitch: arrows aim    C: delivery (Yorker)    N: bowl next    P: pause    V: camera    PgDn: zoom in / PgUp: out")]
    [InlineData(true, "BowlingDeliveryComplete", "Next pitch: D-pad / left stick aim    LB: delivery (Yorker)    RB: bowl next    Start: pause    L3: camera")]
    public void BowlingHintShowsDeliveryAndNextBallActions(bool isGamePad, string phase, string expected) =>
        Assert.Equal(expected, Hint(isGamePad, Enum.Parse<MatchHudPhase>(phase)));

    [Theory]
    [InlineData(false, "DeliveryComplete", "N: next ball    P: pause    V: camera    PgDn: zoom in / PgUp: out")]
    [InlineData(true, "DeliveryComplete", "RB: next ball    Start: pause    L3: camera")]
    [InlineData(false, "BallBatted", "Enter: run    P: pause    V: camera    PgDn: zoom in / PgUp: out")]
    [InlineData(true, "BallBatted", "B: run    Start: pause    L3: camera")]
    public void DeliveryAndRunningPhasesShowContextualActions(bool isGamePad, string phase, string expected) =>
        Assert.Equal(expected, Hint(isGamePad, Enum.Parse<MatchHudPhase>(phase)));

    [Theory]
    [InlineData(false, "Enter: request another run    hold Enter: turn back    P: pause    V: camera    PgDn: zoom in / PgUp: out")]
    [InlineData(true, "B: request another run    hold B: turn back    Start: pause    L3: camera")]
    public void RunningHintExplainsRepeatAndTurnBack(bool isGamePad, string expected) =>
        Assert.Equal(expected, Hint(isGamePad, MatchHudPhase.Running));

    [Fact]
    public void FeedbackBannerSitsBelowTheScorePanelAndClampsToViewport()
    {
        Assert.Equal(new Rectangle(20, 150, 760, 96),
            MatchHudPresenter.CalculateFeedbackBannerBounds(1440, 900, 138, 760, 96));
        Assert.Equal(new Rectangle(20, 150, 448, 116),
            MatchHudPresenter.CalculateFeedbackBannerBounds(1440, 900, 138, 448, 116));
        var narrow = MatchHudPresenter.CalculateFeedbackBannerBounds(1280, 720, 138, 720, 96);

        Assert.Equal(new Rectangle(20, 150, 720, 96), narrow);
        Assert.True(narrow.Bottom <= 696);
    }

    private static LiveFeedbackBannerState CreateLiveFeedbackState(
        bool isHumanBowling = false,
        Vector3? activeTarget = null,
        Vector3? firstBounce = null,
        float remainingSeconds = 5f,
        float? contactQuality = null,
        bool contactIsMiss = false,
        string? shotName = null,
        string? timingText = null,
        BattingTimingCue? timingCue = null,
        bool isGamePad = false,
        bool highContrast = false,
        bool hasPredictedBounce = false,
        CpuDifficulty difficulty = CpuDifficulty.Standard,
        float batterWicketLineZ = -8.72f,
        string? shotControlLabel = null) => new(
            isHumanBowling,
            activeTarget,
            firstBounce,
            remainingSeconds,
            contactQuality,
            contactIsMiss,
            shotName,
            timingText,
            timingCue,
            isGamePad,
            highContrast,
            hasPredictedBounce,
            difficulty,
            batterWicketLineZ,
            shotControlLabel);

    private static string Hint(bool isGamePad, MatchHudPhase phase) =>
        MatchHudPresenter.GetPrimaryControlHint(new MatchHudState(isGamePad, phase, "Yorker"));
}
