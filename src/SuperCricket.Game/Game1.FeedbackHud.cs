using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperCricket.Content;
using SuperCricket.Simulation;

namespace SuperCricket.Game;

public partial class Game1
{
    private void DrawLiveFeedbackBanner()
    {
        var timingCue = GetLiveBattingTimingCue();
        var hasContactFeedback = !IsHumanBowling && _liveFeedbackBannerRemainingSeconds > 0f &&
            (_contactFeedbackQuality is not null || _contactFeedbackIsMiss) && _chosenShot is not null;
        var feedback = MatchHudPresenter.BuildLiveFeedbackBanner(new LiveFeedbackBannerState(
            IsHumanBowling,
            _activeBowlingTargetPosition,
            _firstBouncePosition,
            _liveFeedbackBannerRemainingSeconds,
            _contactFeedbackQuality,
            _contactFeedbackIsMiss,
            _chosenShot?.Name,
            hasContactFeedback ? GetBattingTimingText() : null,
            timingCue,
            _lastInputWasGamePad,
            _gameSettings.HighContrast,
            _predictedBouncePosition is not null,
            _cpuDifficulty,
            NearBatterZ));
        if (feedback is not { } banner)
            return;

        var detail = banner.Detail;
        var accent = banner.Accent;

        var viewport = GraphicsDevice.Viewport;
        var titleScale = _gameSettings.LargeText ? 1.65f : 1.55f;
        var detailScale = _gameSettings.LargeText ? 1.25f : 1.15f;
        var panelWidth = Math.Min(viewport.Width - 40, _gameSettings.LargeText ? 820 : 760);
        var textWidth = panelWidth - 52;
        var detailLines = MatchHudPresenter.WrapTextLines(
            new[] { detail },
            textWidth,
            detailScale,
            text => _debugFont.MeasureString(text).X);
        var lineSpacing = (int)MathF.Round(25 * detailScale);
        var timingGaugeHeight = timingCue is null ? 8 : 18;
        var panelHeight = Math.Max(90, 18 + 36 + detailLines.Count * lineSpacing + timingGaugeHeight);
        var hudLineSpacing = (int)MathF.Round(22 * (_gameSettings.LargeText ? 1.25f : 1f));
        var hudBottom = (int)MathF.Ceiling(20 + 14 + 5 * hudLineSpacing + 14 * (_gameSettings.LargeText ? 1.25f : 1f));
        var panel = MatchHudPresenter.CalculateFeedbackBannerBounds(
            viewport.Width,
            viewport.Height,
            hudBottom,
            panelWidth,
            panelHeight);
        var fade = Math.Clamp(banner.RemainingSeconds / 0.32f, 0f, 1f);
        accent = WithAlpha(accent, fade);
        var background = WithAlpha(_gameSettings.HighContrast ? Color.Black : new Color(5, 12, 16), 0.98f * fade);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);
        _spriteBatch.Draw(_feedbackMapPixel, panel, background);
        _spriteBatch.Draw(_feedbackMapPixel, new Rectangle(panel.X, panel.Y, 9, panel.Height), accent);
        _spriteBatch.Draw(_feedbackMapPixel, new Rectangle(panel.X, panel.Y, panel.Width, 5), accent);
        DrawOverlayText(banner.Title, new Vector2(panel.X + 22, panel.Y + 8), accent, titleScale);
        for (var index = 0; index < detailLines.Count; index++)
            DrawOverlayText(detailLines[index], new Vector2(panel.X + 22, panel.Y + 46 + index * lineSpacing),
                Color.White, detailScale);
        if (timingCue is { } gauge)
            DrawBattingTimingGauge(new Rectangle(panel.X + 22, panel.Bottom - 13, panel.Width - 44, 7), gauge);
        _spriteBatch.End();
    }

    private void DrawBattingTimingGauge(Rectangle bounds, BattingTimingCue cue)
    {
        _spriteBatch.Draw(_feedbackMapPixel, bounds, _gameSettings.HighContrast
            ? new Color(72, 72, 72)
            : new Color(60, 74, 80));
        var targetStart = bounds.X + (int)MathF.Round(cue.WindowStart * bounds.Width);
        var targetEnd = bounds.X + (int)MathF.Round(cue.WindowEnd * bounds.Width);
        _spriteBatch.Draw(_feedbackMapPixel,
            new Rectangle(targetStart, bounds.Y, Math.Max(2, targetEnd - targetStart), bounds.Height),
            _gameSettings.HighContrast ? Color.Yellow : new Color(85, 208, 126));
        var markerX = bounds.X + (int)MathF.Round(cue.Progress * (bounds.Width - 3));
        _spriteBatch.Draw(_feedbackMapPixel,
            new Rectangle(markerX, bounds.Y - 3, 3, bounds.Height + 6), Color.White);
    }

    private void DrawDeliveryFeedbackCard()
    {
        if (!_deliveryComplete || CurrentDelivery.Result is not { } result)
            return;

        var lines = BuildDeliveryFeedbackLines(result);
        var viewport = GraphicsDevice.Viewport;
        var scale = _gameSettings.LargeText ? 0.98f : 0.9f;
        var lineSpacing = (int)MathF.Round(26f * scale);
        var hasPitchMap = _firstBouncePosition is not null || _activeBowlingTargetPosition is not null;
        var panelWidth = Math.Min(viewport.Width - 40, hasPitchMap ? 690 : 620);
        const int pitchMapWidth = 140;
        const int pitchMapHeight = 126;
        var panelX = viewport.Width - panelWidth - 20;
        var pitchMapX = panelX + panelWidth - pitchMapWidth - 12;
        var textWidth = hasPitchMap ? pitchMapX - panelX - 42 : panelWidth - 28;
        var displayLines = MatchHudPresenter.WrapTextLines(
            lines,
            textWidth,
            scale,
            text => _debugFont.MeasureString(text).X);
        var panelHeight = 16 + displayLines.Count * lineSpacing;
        if (hasPitchMap)
            panelHeight = Math.Max(panelHeight, pitchMapHeight + 16);
        var panelY = Math.Max(20, viewport.Height - panelHeight - 20);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(_debugPanel, new Rectangle(panelX, panelY, panelWidth, panelHeight),
            _gameSettings.HighContrast ? Color.Black : new Color(5, 12, 16, 248));
        var accent = _gameSettings.HighContrast
            ? Color.Yellow
            : IsHumanBowling ? new Color(74, 224, 255) : new Color(135, 255, 159);
        _spriteBatch.Draw(_feedbackMapPixel, new Rectangle(panelX, panelY, 9, panelHeight), accent);
        for (var index = 0; index < displayLines.Count; index++)
        {
            var color = index == 0 ? accent : Color.White;
            var lineScale = index == 0 ? scale * 1.12f : scale;
            DrawOverlayText(displayLines[index], new Vector2(panelX + 20, panelY + 9 + index * lineSpacing), color, lineScale);
        }
        if (hasPitchMap)
            DrawPitchMap(new Rectangle(pitchMapX, panelY + (panelHeight - pitchMapHeight) / 2, pitchMapWidth, pitchMapHeight));
        _spriteBatch.End();
    }

}
