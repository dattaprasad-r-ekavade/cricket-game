using Microsoft.Xna.Framework;
using SuperCricket.Game;
using SuperCricket.Simulation;

namespace SuperCricket.Game.Tests;

public sealed class MatchHudPresenterTests
{
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
        Assert.Contains("Batting: Left/Right aim | Space ground/defend | Shift loft", lines);
        Assert.Contains("GamePad batting: left stick aim | A ground/defend | Y loft", lines);
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
    [InlineData(false, "Left / Right: aim    Space: ground / defend    Shift: loft    P: pause    V: camera    PgDn: zoom in / PgUp: out")]
    [InlineData(true, "Left stick: aim    A: ground / defend    Y: loft    Start: pause    L3: camera")]
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
    public void FeedbackBannerIsCenteredBelowHudAndClampedToViewport()
    {
        Assert.Equal(new Rectangle(340, 170, 760, 96),
            MatchHudPresenter.CalculateFeedbackBannerBounds(1440, 900, 158, 760, 96));
        Assert.Equal(new Rectangle(496, 170, 448, 116),
            MatchHudPresenter.CalculateFeedbackBannerBounds(1440, 900, 158, 448, 116));
        var narrow = MatchHudPresenter.CalculateFeedbackBannerBounds(1280, 720, 158, 720, 96);

        Assert.Equal(new Rectangle(280, 170, 720, 96), narrow);
        Assert.True(narrow.Bottom <= 696);
    }

    private static string Hint(bool isGamePad, MatchHudPhase phase) =>
        MatchHudPresenter.GetPrimaryControlHint(new MatchHudState(isGamePad, phase, "Yorker"));
}
