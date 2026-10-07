using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperCricket.Game.Animation;

namespace SuperCricket.Game;

public partial class Game1
{
    private const int HudMargin = 18;
    private const int HudScorePanelHeight = 120;
    private const int HudControlRowHeight = 26;

    private void DrawMatchHud()
    {
        var viewport = GraphicsDevice.Viewport;
        var phase = MatchHudPresenter.ResolvePhase(new MatchHudConditions(
            _match.IsMatchComplete,
            _match.IsInningsComplete,
            IsCpuBattingControlled,
            _deliveryComplete,
            _isRunning,
            _battedBall));
        var hudState = new MatchHudState(
            _lastInputWasGamePad,
            phase,
            _deliveryPresets[_nextDeliveryPresetIndex].Name);
        var hints = MatchHudPresenter.BuildActionHints(hudState);
        var controlsWidth = Math.Clamp(viewport.Width / 2 - HudMargin * 2, 280, 520);
        var scoreWidth = Math.Min(600, viewport.Width - controlsWidth - HudMargin * 3);
        var controlsHeight = 12 + 34 + hints.Count * HudControlRowHeight +
            (phase == MatchHudPhase.Batting ? 42 : 0) + 27 + 10;
        var scorePanel = new Rectangle(HudMargin, HudMargin, scoreWidth, HudScorePanelHeight);
        var controlsPanel = new Rectangle(
            viewport.Width - HudMargin - controlsWidth,
            HudMargin,
            controlsWidth,
            controlsHeight);

        _scoreHudBounds = scorePanel;
        _matchHudBounds = Rectangle.Union(scorePanel, controlsPanel);
        var scale = _gameSettings.LargeText ? 1.08f : 1f;
        var background = _gameSettings.HighContrast ? Color.Black : new Color(4, 10, 15, 232);
        var accent = _gameSettings.HighContrast ? Color.Yellow : new Color(242, 206, 116);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);
        DrawHudPanel(scorePanel, background, accent);
        DrawHudPanel(controlsPanel, background, _gameSettings.HighContrast ? Color.Yellow : new Color(88, 219, 240));
        DrawScorePanel(scorePanel, accent, scale);
        DrawControlsPanel(controlsPanel, hints, phase, accent, scale);
        _spriteBatch.End();
    }

    private void DrawScorePanel(Rectangle panel, Color accent, float scale)
    {
        var header = _match.IsMatchComplete
            ? "SHORT MATCH  /  COMPLETE"
            : $"SHORT MATCH  /  INNINGS {_match.InningsNumber} OF 2  /  CPU {_cpuDifficulty.ToString().ToUpperInvariant()}";
        var score = _match.IsMatchComplete
            ? _match.ResultText
            : $"{_match.BattingTeamName}    {_match.Runs}/{_match.Wickets}";
        var overLine = _match.IsMatchComplete
            ? $"{_match.OversPerInnings} overs per innings"
            : _match.Target is { } target
                ? $"OVER  {_match.OversText} / {_match.OversPerInnings}.0     TARGET  {target}"
                : $"OVER  {_match.OversText} / {_match.OversPerInnings}.0     BOWLER  {_match.CurrentBowler.Name}";
        var batterLine = _match.IsMatchComplete
            ? "Press R to begin another match"
            : $"STRIKER  {_match.StrikerPlayer.Name}     NON-STRIKER  {_match.NonStrikerPlayer.Name}";

        DrawOverlayText(header, new Vector2(panel.X + 14, panel.Y + 9), accent, 0.76f * scale);
        DrawOverlayText(score, new Vector2(panel.X + 14, panel.Y + 29), Color.White, 1.12f * scale);
        DrawOverlayText(overLine, new Vector2(panel.X + 14, panel.Y + 62), Color.White, 0.78f * scale);
        DrawOverlayText(batterLine, new Vector2(panel.X + 14, panel.Y + 88), Color.White, 0.73f * scale);
    }

    private void DrawControlsPanel(
        Rectangle panel,
        System.Collections.Generic.IReadOnlyList<HudActionHint> hints,
        MatchHudPhase phase,
        Color accent,
        float scale)
    {
        var title = phase switch
        {
            MatchHudPhase.Batting => "BATTING  /  WHAT TO PRESS",
            MatchHudPhase.BallBatted or MatchHudPhase.Running => "RUNNING  /  WHAT TO PRESS",
            MatchHudPhase.Bowling or MatchHudPhase.BowlingDeliveryComplete => "BOWLING  /  WHAT TO PRESS",
            MatchHudPhase.DeliveryComplete => "DELIVERY COMPLETE",
            MatchHudPhase.InningsComplete => "INNINGS COMPLETE",
            MatchHudPhase.MatchComplete => "MATCH COMPLETE",
            _ => "MATCH CONTROLS"
        };
        var animationStatus = GetBatterActionStatus();
        var headerScale = 0.76f * scale;
        DrawOverlayText(title, new Vector2(panel.X + 14, panel.Y + 9), accent, headerScale);
        if (phase == MatchHudPhase.Batting || phase == MatchHudPhase.BallBatted || phase == MatchHudPhase.Running)
        {
            var statusWidth = _debugFont.MeasureString(animationStatus).X * headerScale;
            DrawOverlayText(animationStatus,
                new Vector2(Math.Max(panel.X + 14, panel.Right - statusWidth - 14), panel.Y + 9),
                Color.White,
                headerScale);
        }

        var keyWidth = Math.Min(128, Math.Max(58,
            (int)MathF.Ceiling(hints.Max(hint => _debugFont.MeasureString(hint.Input).X * 0.72f + 18))));
        var rowY = panel.Y + 35;
        var textScale = 0.74f * scale;
        for (var index = 0; index < hints.Count; index++)
        {
            var row = hints[index];
            var keyBounds = new Rectangle(panel.X + 12, rowY + 1, keyWidth, 22);
            var keyBackground = _gameSettings.HighContrast ? new Color(45, 45, 0) : new Color(28, 47, 55);
            _spriteBatch.Draw(_feedbackMapPixel, keyBounds, keyBackground);
            _spriteBatch.Draw(_feedbackMapPixel, new Rectangle(keyBounds.X, keyBounds.Y, 3, keyBounds.Height), accent);
            var inputSize = _debugFont.MeasureString(row.Input) * textScale;
            var inputPosition = new Vector2(
                keyBounds.X + (keyBounds.Width - inputSize.X) * 0.5f,
                keyBounds.Y + (keyBounds.Height - inputSize.Y) * 0.5f - 1f);
            DrawOverlayText(row.Input, inputPosition, accent, textScale);
            DrawOverlayText(row.Action,
                new Vector2(keyBounds.Right + 10, rowY + 3),
                Color.White,
                textScale);
            rowY += HudControlRowHeight;
        }

        if (phase == MatchHudPhase.Batting)
        {
            rowY += 2;
            DrawShotLaneMeter(panel, rowY, accent, textScale);
            rowY += 38;
        }

        var footer = _lastInputWasGamePad
            ? "START  PAUSE     L3  CAMERA"
            : "P  PAUSE     V  CAMERA     PGDN  CLOSER / PGUP  FARTHER";
        DrawOverlayText(footer,
            new Vector2(panel.X + 14, panel.Bottom - 22),
            _gameSettings.HighContrast ? Color.Yellow : new Color(200, 214, 220),
            0.65f * scale);
    }

    private void DrawShotLaneMeter(Rectangle panel, int y, Color accent, float scale)
    {
        var aim = _chosenShot is { } shot ? shot.HorizontalAim : Math.Clamp(_humanShotAimOffset, -1f, 1f);
        var forward = _chosenShot is { } chosen ? chosen.ForwardAim : _humanForwardShotAim;
        var direction = aim switch
        {
            < -0.12f => "LEFT",
            > 0.12f => "RIGHT",
            _ => "STRAIGHT"
        };
        var depth = forward < -0.12f ? "BEHIND" : "DOWNFIELD";
        DrawOverlayText($"SHOT AIM  /  {direction} / {depth}",
            new Vector2(panel.X + 14, y),
            accent,
            0.68f * scale);

        var bar = new Rectangle(panel.X + 14, y + 18, panel.Width - 28, 5);
        _spriteBatch.Draw(_feedbackMapPixel, bar, _gameSettings.HighContrast ? Color.DarkGray : new Color(57, 74, 81));
        foreach (var lane in new[] { 0.25f, 0.5f, 0.75f })
        {
            var x = bar.X + (int)MathF.Round(lane * bar.Width);
            _spriteBatch.Draw(_feedbackMapPixel, new Rectangle(x, bar.Y - 3, 2, bar.Height + 6), Color.White);
        }
        var markerX = bar.X + (int)MathF.Round((Math.Clamp(aim, -1f, 1f) + 1f) * 0.5f * bar.Width);
        _spriteBatch.Draw(_feedbackMapPixel, new Rectangle(markerX - 4, bar.Y - 5, 8, bar.Height + 10), accent);
    }

    private void DrawHudPanel(Rectangle bounds, Color background, Color accent)
    {
        _spriteBatch.Draw(_feedbackMapPixel, bounds, background);
        _spriteBatch.Draw(_feedbackMapPixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 4), accent);
        _spriteBatch.Draw(_feedbackMapPixel, new Rectangle(bounds.X, bounds.Y, 5, bounds.Height), accent);
    }

    private string GetBatterActionStatus()
    {
        if (_isRunning)
            return "RUNNING";
        if (_battingInputRecorder.HasPendingInput)
            return $"REC  {_battingInputRecorder.PendingLabel}";
        if (_chosenShot is not { } shot)
            return _playerAnimator.CurrentClipName switch
            {
                "batting-step-offside" => "STEP LEFT",
                "batting-step-legside" => "STEP RIGHT",
                _ => "READY"
            };
        var action = shot.AnimationClip switch
        {
            "defensive-block" => "DEFEND",
            "front-foot-drive" => "FRONT DRIVE",
            "back-foot-drive" => "BACK DRIVE",
            "lofted-drive" => "FRONT LOFT",
            "back-foot-loft" => "BACK LOFT",
            _ => shot.Name.ToUpperInvariant()
        };
        var state = string.Equals(_playerAnimator.CurrentClipName, shot.AnimationClip, StringComparison.OrdinalIgnoreCase)
            ? "SWINGING"
            : "SHOT";
        var label = string.IsNullOrWhiteSpace(_shotControlLabel) ? string.Empty : $"  [{_shotControlLabel}]";
        return $"{state} {action}{label}";
    }
}
