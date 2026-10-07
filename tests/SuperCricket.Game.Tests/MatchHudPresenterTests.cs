using Microsoft.Xna.Framework;
using SuperCricket.Game;
using SuperCricket.Simulation;

namespace SuperCricket.Game.Tests;

public sealed class MatchHudPresenterTests
{
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
