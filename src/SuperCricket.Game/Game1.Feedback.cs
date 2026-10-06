using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperCricket.Content;
using SuperCricket.Simulation;

namespace SuperCricket.Game;

public partial class Game1
{
    private const float PitchMapLateralHalfExtentMeters = 9f;
    private const float PitchMapLengthMarginMeters = 2f;
    private float _deliverySpeedKilometersPerHour;
    private Vector3? _firstBouncePosition;
    private Vector3? _activeBowlingTargetPosition;
    private float? _shotInputDelaySeconds;
    private BattingTimingCalibrationAsset _battingTimingCalibration = null!;
    private string CalibrationDeliveryName => _deliveryPresets[_activeDeliveryPresetIndex].Name;

    private void DrawDeliveryFeedbackCard()
    {
        if (!_deliveryComplete || CurrentDelivery.Result is not { } result)
            return;

        var lines = BuildDeliveryFeedbackLines(result);
        var viewport = GraphicsDevice.Viewport;
        var scale = _gameSettings.LargeText ? 1.1f : 1f;
        var lineSpacing = (int)MathF.Round(27f * scale);
        var hasPitchMap = _firstBouncePosition is not null || _activeBowlingTargetPosition is not null;
        var panelWidth = Math.Min(viewport.Width - 40, hasPitchMap ? 1180 : 980);
        const int pitchMapWidth = 176;
        const int pitchMapHeight = 168;
        var pitchMapX = 20 + panelWidth - pitchMapWidth - 12;
        var textWidth = hasPitchMap ? pitchMapX - 34 - 16 : panelWidth - 28;
        var displayLines = WrapFeedbackLines(lines, textWidth, scale);
        var panelHeight = 20 + displayLines.Count * lineSpacing;
        if (hasPitchMap)
            panelHeight = Math.Max(panelHeight, pitchMapHeight + 20);
        var panelY = Math.Max(20, viewport.Height - panelHeight - 20);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(_debugPanel, new Rectangle(20, panelY, panelWidth, panelHeight),
            _gameSettings.HighContrast ? Color.Black : new Color(9, 17, 21, 238));
        for (var index = 0; index < displayLines.Count; index++)
        {
            var color = index == 0
                ? (_gameSettings.HighContrast ? Color.Yellow : new Color(255, 218, 130))
                : Color.White;
            DrawOverlayText(displayLines[index], new Vector2(34, panelY + 11 + index * lineSpacing), color, scale);
        }
        if (hasPitchMap)
            DrawPitchMap(new Rectangle(pitchMapX, panelY + (panelHeight - pitchMapHeight) / 2, pitchMapWidth, pitchMapHeight));
        _spriteBatch.End();
    }

    private List<string> WrapFeedbackLines(IReadOnlyList<string> lines, float availableWidth, float scale)
    {
        var wrapped = new List<string>(lines.Count);
        foreach (var line in lines)
        {
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var current = string.Empty;
            foreach (var word in words)
            {
                var candidate = current.Length == 0 ? word : $"{current} {word}";
                if (current.Length > 0 && _debugFont.MeasureString(candidate).X * scale > availableWidth)
                {
                    wrapped.Add(current);
                    current = $"  {word}";
                }
                else
                {
                    current = candidate;
                }
            }
            if (current.Length > 0)
                wrapped.Add(current);
        }
        return wrapped;
    }

    private void DrawPitchMap(Rectangle bounds)
    {
        DrawFeedbackRectangle(bounds, _gameSettings.HighContrast ? Color.Black : new Color(4, 12, 12, 238));
        DrawFeedbackOutline(bounds, _gameSettings.HighContrast ? Color.White : new Color(147, 167, 154));
        DrawOverlayText("PITCH MAP", new Vector2(bounds.X + 9, bounds.Y + 3),
            _gameSettings.HighContrast ? Color.Yellow : new Color(255, 218, 130), 0.65f);

        var plot = new Rectangle(bounds.X + 30, bounds.Y + 31, bounds.Width - 60, bounds.Height - 64);
        DrawFeedbackRectangle(plot, new Color(31, 72, 48));
        DrawFeedbackOutline(plot, new Color(104, 143, 104));

        var pitchHalfLength = _deliveryPreset.PitchLengthMeters / 2f;
        var bowlerWicket = MapPitchPosition(new Vector3(0f, 0f, pitchHalfLength), plot,
            _deliveryPreset.PitchLengthMeters);
        var batterWicket = MapPitchPosition(new Vector3(0f, 0f, -pitchHalfLength), plot,
            _deliveryPreset.PitchLengthMeters);
        var pitchWidth = Math.Max(18, (int)MathF.Round(plot.Width * _deliveryPreset.PitchWidthMeters /
            (PitchMapLateralHalfExtentMeters * 2f)));
        var pitch = new Rectangle(plot.Center.X - pitchWidth / 2, (int)MathF.Round(bowlerWicket.Y),
            pitchWidth, Math.Max(1, (int)MathF.Round(batterWicket.Y - bowlerWicket.Y)));
        DrawFeedbackRectangle(pitch, new Color(156, 111, 72));
        DrawFeedbackRectangle(new Rectangle(pitch.X - 5, pitch.Y + 3, pitch.Width + 10, 2), Color.White);
        DrawFeedbackRectangle(new Rectangle(pitch.X - 5, pitch.Bottom - 5, pitch.Width + 10, 2), Color.White);
        DrawOverlayText("BOWLER", new Vector2(bounds.X + 9, bounds.Y + 18), Color.White, 0.52f);
        DrawOverlayText("BATTER", new Vector2(bounds.X + 9, bounds.Bottom - 39), Color.White, 0.52f);

        if (_activeBowlingTargetPosition is { } target)
        {
            var targetPoint = MapPitchPosition(target, plot, _deliveryPreset.PitchLengthMeters);
            DrawFeedbackMarker(_feedbackMapRing, targetPoint, 17, Color.Cyan);
        }
        if (_firstBouncePosition is { } bounce)
        {
            var bouncePoint = MapPitchPosition(bounce, plot, _deliveryPreset.PitchLengthMeters);
            DrawFeedbackMarker(_feedbackMapDot, bouncePoint, 9, Color.Yellow);
        }

        var legendY = bounds.Bottom - 19;
        if (_activeBowlingTargetPosition is not null)
        {
            DrawFeedbackMarker(_feedbackMapRing, new Vector2(bounds.X + 15, legendY + 6), 11, Color.Cyan);
            DrawOverlayText("AIM", new Vector2(bounds.X + 23, legendY), Color.White, 0.48f);
            DrawFeedbackMarker(_feedbackMapDot, new Vector2(bounds.X + 78, legendY + 6), 7, Color.Yellow);
            DrawOverlayText("BOUNCE", new Vector2(bounds.X + 85, legendY), Color.White, 0.48f);
        }
        else
        {
            DrawFeedbackMarker(_feedbackMapDot, new Vector2(bounds.X + 52, legendY + 6), 7, Color.Yellow);
            DrawOverlayText("BOUNCE", new Vector2(bounds.X + 60, legendY), Color.White, 0.48f);
        }
    }

    private static Vector2 MapPitchPosition(Vector3 position, Rectangle plot, float pitchLengthMeters)
    {
        if (plot.Width <= 0 || plot.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(plot), "Pitch map plot bounds must have positive dimensions.");
        if (!float.IsFinite(pitchLengthMeters) || pitchLengthMeters <= 0f)
            throw new ArgumentOutOfRangeException(nameof(pitchLengthMeters));

        var lengthRange = pitchLengthMeters + PitchMapLengthMarginMeters * 2f;
        var topZ = lengthRange / 2f;
        var xFraction = Math.Clamp((position.X + PitchMapLateralHalfExtentMeters) /
            (PitchMapLateralHalfExtentMeters * 2f), 0f, 1f);
        var zFraction = Math.Clamp((topZ - position.Z) / lengthRange, 0f, 1f);
        return new Vector2(plot.Left + xFraction * plot.Width, plot.Top + zFraction * plot.Height);
    }

    private void DrawFeedbackMarker(Texture2D texture, Vector2 center, int diameter, Color tint)
    {
        var bounds = new Rectangle(
            (int)MathF.Round(center.X - diameter / 2f),
            (int)MathF.Round(center.Y - diameter / 2f),
            diameter,
            diameter);
        _spriteBatch.Draw(texture, bounds, tint);
    }

    private void DrawFeedbackRectangle(Rectangle rectangle, Color color) =>
        _spriteBatch.Draw(_feedbackMapPixel, rectangle, color);

    private void DrawFeedbackOutline(Rectangle rectangle, Color color)
    {
        DrawFeedbackRectangle(new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, 1), color);
        DrawFeedbackRectangle(new Rectangle(rectangle.X, rectangle.Bottom - 1, rectangle.Width, 1), color);
        DrawFeedbackRectangle(new Rectangle(rectangle.X, rectangle.Y, 1, rectangle.Height), color);
        DrawFeedbackRectangle(new Rectangle(rectangle.Right - 1, rectangle.Y, 1, rectangle.Height), color);
    }

    private static Texture2D CreateCircularMarkerTexture(GraphicsDevice graphicsDevice, int size, float innerRadiusFraction)
    {
        var pixels = new Color[size * size];
        var center = (size - 1) / 2f;
        var outerRadius = size / 2f - 1f;
        var innerRadius = outerRadius * Math.Clamp(innerRadiusFraction, 0f, 0.95f);
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center));
            var coverage = Math.Clamp(outerRadius + 0.5f - distance, 0f, 1f);
            if (innerRadiusFraction > 0f)
                coverage *= Math.Clamp(distance - innerRadius + 0.5f, 0f, 1f);
            var alpha = (byte)MathF.Round(coverage * 255f);
            pixels[y * size + x] = new Color(alpha, alpha, alpha, alpha);
        }

        var texture = new Texture2D(graphicsDevice, size, size, false, SurfaceFormat.Color);
        texture.SetData(pixels);
        return texture;
    }

    private List<string> BuildDeliveryFeedbackLines(DeliveryResult result)
    {
        var outcome = result.Dismissal != DismissalKind.None
            ? $"WICKET - {result.Dismissal.ToString().ToUpperInvariant()}"
            : result.Extra switch
            {
                DeliveryExtra.Wide => $"WIDE - {result.ExtraRuns} extra run(s)",
                DeliveryExtra.NoBall => $"NO BALL - {result.ExtraRuns} extra run(s)",
                _ => result.BatterRuns switch
                {
                    0 => "DOT BALL",
                    1 => "1 RUN",
                    4 => "FOUR",
                    6 => "SIX",
                    _ => $"{result.BatterRuns} RUNS"
                }
            };

        var bounce = _firstBouncePosition;
        var pitchDescription = bounce is { } position
            ? $"{GetPitchLengthLabel(position)} - {GetPitchLineLabel(position)} ({MathF.Abs(position.Z - NearBatterZ):0.0} m from striker)"
            : "full toss - no pitch bounce";
        var battingDescription = _chosenShot is { } shot
            ? _contactQuality is { } quality
                ? $"{shot.Name}: {GetContactQualityLabel(quality)} ({quality:P0})"
                : $"{shot.Name}: no contact - ball beat the bat"
            : "No shot played";
        if (GetBattingTimingText() is { } timingText)
            battingDescription += $" | timing {timingText}";

        var lines = new List<string>
        {
            $"LAST DELIVERY  |  {outcome}",
            $"BALL  |  {_deliverySpeedKilometersPerHour:0} km/h  |  {pitchDescription}",
            $"BAT  |  {battingDescription}"
        };

        if (_activeBowlingTargetPosition is { } target)
        {
            var targetDescription = bounce is { } landed
                ? $"landed {GetPitchLengthLabel(landed)} / {GetPitchLineLabel(landed)}; {MathF.Sqrt(
                    MathF.Pow(landed.X - target.X, 2f) + MathF.Pow(landed.Z - target.Z, 2f)):0.0} m from aim"
                : $"aimed at {GetPitchLengthLabel(target)} / {GetPitchLineLabel(target)}; no bounce";
            lines.Add($"BOWL  |  Target {GetPitchLengthLabel(target)} / {GetPitchLineLabel(target)}  |  {targetDescription}");
        }

        return lines;
    }

    private static string GetPitchLengthLabel(Vector3 position)
    {
        var distanceFromStriker = MathF.Abs(position.Z - NearBatterZ);
        return distanceFromStriker switch
        {
            <= 1.6f => "yorker",
            <= 3.0f => "full",
            <= 6.3f => "good length",
            <= 8.3f => "back of a length",
            _ => "short"
        };
    }

    private static string GetPitchLineLabel(Vector3 position) => MathF.Abs(position.X) switch
    {
        <= 0.35f => "on the stumps",
        <= 1.525f => position.X < 0f ? "left of the stumps" : "right of the stumps",
        _ => position.X < 0f ? "wide left" : "wide right"
    };

    private static string GetContactQualityLabel(float quality) => quality switch
    {
        >= 0.88f => "middled",
        >= 0.75f => "clean contact",
        >= 0.60f => "edged",
        _ => "thin contact"
    };

    private string? GetBattingTimingText()
    {
        if (IsCpuBattingControlled || _chosenShot is not { } shot ||
            _shotInputDelaySeconds is not { } actualInputDelay)
            return null;

        var idealInputDelay = _battingTimingCalibration.FindIdealInputDelaySeconds(CalibrationDeliveryName, shot.Name);
        if (idealInputDelay is not { } ideal)
            return null;

        var assessment = BattingTimingFeedbackModel.Assess(
            actualInputDelay,
            ideal,
            _battingTimingCalibration.OnTimeWindowSeconds);
        var offsetMilliseconds = (int)MathF.Round(MathF.Abs(assessment.OffsetFromIdealSeconds) * 1000f);
        return assessment.Band switch
        {
            BattingTimingBand.Perfect => "PERFECT",
            BattingTimingBand.Early => $"EARLY {offsetMilliseconds} ms",
            _ => $"LATE {offsetMilliseconds} ms"
        };
    }

    private void PrepareFeedbackPreviewCapture()
    {
        _firstBouncePosition = BowlingAimModel.FindFirstBounce(_deliveryPreset) is { } bounce
            ? new Vector3(bounce.Position.X, bounce.Position.Y, bounce.Position.Z)
            : null;
        if (IsCpuBattingControlled)
        {
            CurrentDelivery.RecordCompletedRun();
            _battedBall = true;
            _chosenShot = _shotSet.Get("drive");
            _contactQuality = 0.81f;
            _shotOutcome = "RUN completed: 1 batter run";
        }
        else
        {
            CurrentDelivery.ResolveBoundary(clearedInTheAir: false, currentRunCrossed: false);
            _battedBall = true;
            _chosenShot = _shotSet.Get("drive");
            _shotInputDelaySeconds = _battingTimingCalibration.FindIdealInputDelaySeconds(CalibrationDeliveryName, _chosenShot.Name);
            _contactQuality = 0.91f;
            _shotOutcome = "FOUR: reached the boundary";
        }

        _match.CompleteDelivery();
        _shotResolved = true;
        _simulationPaused = true;
    }
}
