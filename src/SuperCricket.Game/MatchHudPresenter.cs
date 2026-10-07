using System;
using Microsoft.Xna.Framework;

namespace SuperCricket.Game;

internal enum MatchHudPhase
{
    Batting,
    BallBatted,
    Running,
    DeliveryComplete,
    Bowling,
    BowlingDeliveryComplete,
    InningsComplete,
    MatchComplete
}

internal readonly record struct MatchHudState(
    bool IsGamePad,
    MatchHudPhase Phase,
    string NextDeliveryName);

/// <summary>Builds match HUD text and layout values without depending on a graphics device.</summary>
internal static class MatchHudPresenter
{
    public static string GetPrimaryControlHint(MatchHudState state)
    {
        var pause = state.IsGamePad ? "Start: pause" : "P: pause";
        var camera = state.IsGamePad ? "L3: camera" : "V: camera    PgDn: zoom in / PgUp: out";
        string WithCamera(string hint) => $"{hint}    {camera}";

        return state.Phase switch
        {
            MatchHudPhase.MatchComplete => WithCamera(state.IsGamePad
                ? $"A: replay    LB: difficulty    RB: overs    {pause}"
                : $"R: replay    D: difficulty    O: overs    {pause}"),
            MatchHudPhase.InningsComplete => WithCamera(state.IsGamePad
                ? $"RB: start the chase    {pause}"
                : $"N: start the chase    {pause}"),
            MatchHudPhase.Bowling => WithCamera(GetBowlingHint(state, pause, includeNextBall: false)),
            MatchHudPhase.BowlingDeliveryComplete => WithCamera(GetBowlingHint(state, pause, includeNextBall: true)),
            MatchHudPhase.DeliveryComplete => WithCamera(state.IsGamePad
                ? $"RB: next ball    {pause}"
                : $"N: next ball    {pause}"),
            MatchHudPhase.Running => WithCamera(state.IsGamePad
                ? $"B: request another run    hold B: turn back    {pause}"
                : $"Enter: request another run    hold Enter: turn back    {pause}"),
            MatchHudPhase.BallBatted => WithCamera(state.IsGamePad
                ? $"B: run    {pause}"
                : $"Enter: run    {pause}"),
            MatchHudPhase.Batting => WithCamera(state.IsGamePad
                ? $"Left stick: aim    A: ground / defend    Y: loft    {pause}"
                : $"Left / Right: aim    Space: ground / defend    Shift: loft    {pause}"),
            _ => throw new ArgumentOutOfRangeException(nameof(state), state.Phase, "Unsupported match HUD phase.")
        };
    }

    public static Rectangle CalculateFeedbackBannerBounds(
        int viewportWidth,
        int viewportHeight,
        int hudBottom,
        int panelWidth,
        int panelHeight)
    {
        var width = Math.Min(viewportWidth - 40, panelWidth);
        var height = Math.Min(viewportHeight - 24, panelHeight);
        var x = (viewportWidth - width) / 2;
        var y = Math.Clamp(hudBottom + 12, 20, viewportHeight - height - 12);
        return new Rectangle(x, y, width, height);
    }

    private static string GetBowlingHint(MatchHudState state, string pause, bool includeNextBall)
    {
        var aim = state.IsGamePad ? "Next pitch: D-pad / left stick aim" : "Next pitch: arrows aim";
        var changeDelivery = state.IsGamePad ? "LB: delivery" : "C: delivery";
        var nextDelivery = state.IsGamePad ? "RB: bowl next" : "N: bowl next";
        var delivery = includeNextBall
            ? $"{changeDelivery} ({state.NextDeliveryName})"
            : $"{changeDelivery} ({state.NextDeliveryName}; next ball)";

        return includeNextBall
            ? $"{aim}    {delivery}    {nextDelivery}    {pause}"
            : $"{aim}    {delivery}    {pause}";
    }
}
