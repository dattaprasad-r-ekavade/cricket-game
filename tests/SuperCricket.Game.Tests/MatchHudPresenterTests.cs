using Microsoft.Xna.Framework;
using SuperCricket.Game;

namespace SuperCricket.Game.Tests;

public sealed class MatchHudPresenterTests
{
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
