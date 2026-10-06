using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperCricket.Simulation;

namespace SuperCricket.Game;

public partial class Game1
{
    private float _deliverySpeedKilometersPerHour;
    private Vector3? _firstBouncePosition;
    private Vector3? _activeBowlingTargetPosition;

    private void DrawDeliveryFeedbackCard()
    {
        if (!_deliveryComplete || CurrentDelivery.Result is not { } result)
            return;

        var lines = BuildDeliveryFeedbackLines(result);
        var viewport = GraphicsDevice.Viewport;
        var scale = _gameSettings.LargeText ? 1.1f : 1f;
        var lineSpacing = (int)MathF.Round(27f * scale);
        var panelWidth = Math.Min(viewport.Width - 40, 980);
        var panelHeight = 20 + lines.Count * lineSpacing;
        var panelY = Math.Max(20, viewport.Height - panelHeight - 20);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(_debugPanel, new Rectangle(20, panelY, panelWidth, panelHeight),
            _gameSettings.HighContrast ? Color.Black : new Color(9, 17, 21, 238));
        for (var index = 0; index < lines.Count; index++)
        {
            var color = index == 0
                ? (_gameSettings.HighContrast ? Color.Yellow : new Color(255, 218, 130))
                : Color.White;
            DrawOverlayText(lines[index], new Vector2(34, panelY + 11 + index * lineSpacing), color, scale);
        }
        _spriteBatch.End();
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
            _contactQuality = 0.91f;
            _shotOutcome = "FOUR: reached the boundary";
        }

        _match.CompleteDelivery();
        _shotResolved = true;
        _simulationPaused = true;
    }
}
