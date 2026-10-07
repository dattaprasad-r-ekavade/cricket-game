using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperCricket.Simulation;
using NumericsVector3 = System.Numerics.Vector3;

namespace SuperCricket.Game;

public partial class Game1
{
    private const float FieldRadarMapInsetPixels = 18f;

    private void DrawBattingFieldInset()
    {
        if (IsHumanBowling || IsCpuBattingControlled || _deliveryComplete ||
            (_simulationPaused && _captureTarget is null))
            return;

        var viewport = GraphicsDevice.Viewport;
        var plotSize = Math.Clamp(viewport.Width / 5 - 36, 172, 252);
        plotSize = Math.Min(plotSize, Math.Max(150, viewport.Height - 166));
        var panel = new Rectangle(
            viewport.Width - plotSize - (int)(FieldRadarMapInsetPixels * 2f) - 18,
            viewport.Height - plotSize - 84 - 18,
            plotSize + (int)(FieldRadarMapInsetPixels * 2f),
            plotSize + 84);
        var plot = new Rectangle(
            panel.X + (int)FieldRadarMapInsetPixels,
            panel.Y + 34,
            plotSize,
            plotSize);
        var boundaryRadius = _deliveryPreset.FieldBoundaryRadiusMeters;
        var isLiveBall = _battedBall;
        var titleColor = _gameSettings.HighContrast
            ? Color.Yellow
            : isLiveBall ? new Color(255, 198, 105) : new Color(128, 233, 255);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);
        DrawFeedbackRectangle(panel, _gameSettings.HighContrast ? Color.Black : new Color(4, 11, 15, 244));
        DrawFeedbackOutline(panel, _gameSettings.HighContrast ? Color.White : new Color(134, 167, 161));
        DrawFeedbackRectangle(new Rectangle(panel.X, panel.Y, panel.Width, 4), titleColor);
        DrawOverlayText(isLiveBall ? "LIVE FIELD PIP" : "BATTING FIELD MAP",
            new Vector2(panel.X + 11, panel.Y + 8), titleColor, 0.7f);

        DrawFeedbackRectangle(plot, new Color(35, 91, 59));
        DrawFeedbackOutline(plot, new Color(92, 139, 100));
        var center = MapFieldPosition(Vector3.Zero, plot, boundaryRadius);
        var mapRadius = MathF.Min(plot.Width, plot.Height) * 0.5f;
        DrawFieldCircle(center, mapRadius, new Color(223, 236, 204, 205), 2.1f);
        DrawFieldCircle(center, mapRadius * 0.48f, new Color(205, 223, 184, 64), 1.1f);

        var farWicket = MapFieldPosition(new Vector3(0f, 0f, FarBatterZ), plot, boundaryRadius);
        var nearWicket = MapFieldPosition(new Vector3(0f, 0f, NearBatterZ), plot, boundaryRadius);
        var pitchWidthPixels = MathF.Max(5f,
            plot.Width * _deliveryPreset.PitchWidthMeters / (2f * boundaryRadius));
        DrawHudLine(farWicket, nearWicket, new Color(183, 139, 92), pitchWidthPixels);
        DrawWicketMark(farWicket, plot.Width * 0.065f);
        DrawWicketMark(nearWicket, plot.Width * 0.065f);

        for (var index = 0; index < _fieldingSide.Positions.Count; index++)
        {
            var fielder = ToXna(_fieldingSide.Positions[index]);
            var point = MapFieldPosition(fielder, plot, boundaryRadius);
            var isChaser = isLiveBall && index == _fieldingSide.ActiveChaserIndex;
            DrawFeedbackMarker(_feedbackMapDot, point, isChaser ? 12 : 9,
                isChaser ? new Color(255, 176, 76) : new Color(246, 250, 236));
            if (isChaser)
                DrawFeedbackMarker(_feedbackMapRing, point, 20, new Color(255, 206, 108, 230));
        }

        var (strikerWorld, nonStrikerWorld) = GetBatterWorlds();
        var strikerPoint = MapFieldPosition(strikerWorld.Translation, plot, boundaryRadius);
        var partnerPoint = MapFieldPosition(nonStrikerWorld.Translation, plot, boundaryRadius);
        DrawFeedbackMarker(_feedbackMapDot, strikerPoint, 12, new Color(127, 255, 170));
        DrawFeedbackMarker(_feedbackMapRing, partnerPoint, 17, new Color(127, 255, 170));

        if (isLiveBall)
        {
            var ballWorld = _fielderThrowActive
                ? ToXna(GetFielderThrowBallPosition())
                : _ballFlight.CurrentFrame.Position;
            var ballPoint = MapFieldPosition(ballWorld, plot, boundaryRadius);
            DrawHudLine(strikerPoint, partnerPoint, new Color(119, 255, 175, 85), 1.4f);
            DrawFeedbackMarker(_feedbackMapRing, ballPoint, 21, new Color(255, 232, 112, 235));
            DrawFeedbackMarker(_feedbackMapDot, ballPoint, 10, new Color(255, 117, 84));
        }
        else
        {
            var aimHorizontal = _chosenShot?.HorizontalAim ?? Math.Clamp(_humanShotAimOffset, -1f, 1f);
            var aimForward = _chosenShot?.ForwardAim ?? _humanForwardShotAim;
            var aimPoint = GetShotAimBoundaryPoint(strikerWorld.Translation, aimHorizontal, aimForward, boundaryRadius);
            var aimEnd = MapFieldPosition(aimPoint, plot, boundaryRadius);
            var aimColor = _gameSettings.HighContrast ? Color.Yellow : new Color(125, 228, 255);
            DrawHudLine(strikerPoint, aimEnd, new Color(8, 20, 19, 180), 5f);
            DrawHudLine(strikerPoint, aimEnd, aimColor, 2.3f);
            DrawAimArrowHead(strikerPoint, aimEnd, aimColor);

            if (_predictedBouncePosition is { } predictedBounce)
            {
                var landingPoint = MapFieldPosition(predictedBounce, plot, boundaryRadius);
                DrawFeedbackMarker(_feedbackMapRing, landingPoint, 18,
                    _gameSettings.HighContrast ? Color.White : new Color(100, 235, 255, 235));
                DrawFeedbackMarker(_feedbackMapDot, landingPoint, 8, Color.White);
            }
        }

        var statusY = plot.Bottom + 5;
        if (isLiveBall)
        {
            var runStatus = _isRunning
                ? $"RUN {_runners.CompletedRuns + 1}  /  {_runners.Progress:P0}{(_runners.IsReturning ? "  RETURNING" : "  CROSSING")}"
                : $"BALL LIVE  /  {(_lastInputWasGamePad ? "B RUN  /  HOLD B TO TURN" : "D RUN  /  A TURN")}";
            DrawOverlayText(runStatus, new Vector2(panel.X + 11, statusY), Color.White, 0.59f);
            DrawInsetLegend(panel, plot.Bottom + 25, showBall: true);
        }
        else
        {
            DrawOverlayText("AIM LANE + PROJECTED BOUNCE", new Vector2(panel.X + 11, statusY), Color.White, 0.53f);
            DrawInsetLegend(panel, plot.Bottom + 25, showBall: false);
        }
        _spriteBatch.End();
    }

    private void PrepareLiveFieldInsetCapture()
    {
        var contact = new NumericsVector3(-0.48f, 0.28f, NearBatterZ);
        _ballFlight.ApplyBatContact(contact, new NumericsVector3(4.2f, 5.8f, 13.5f));
        _bowlerReleased = true;
        _battedBall = true;
        _shotResolved = true;
        _chosenShot = _shotSet.Get("drive");
        _shotControlLabel = "S + D";
        _shotOutcome = "LIVE BALL: RUN IN PROGRESS";
        _fieldingSide.Reset();
        _runners.StartRun();
        _runners.Advance(_runDurationSeconds * 0.43f, _runDurationSeconds);
        _batterAnimations.SetRunning(true);

        var sampleCount = (int)MathF.Round(0.34f / _ballFlight.FixedTimeStepSeconds);
        for (var sample = 0; sample < sampleCount; sample++)
        {
            var frame = _ballFlight.Step(enforceSimulationLimit: false);
            _fieldingSide.Step(_ballFlight.FixedTimeStepSeconds, frame.Position);
        }

        _simulationPaused = true;
    }

    private void DrawInsetLegend(Rectangle panel, int y, bool showBall)
    {
        var x = panel.X + 12;
        DrawFeedbackMarker(_feedbackMapDot, new Vector2(x + 5, y + 6), 8,
            showBall ? new Color(255, 117, 84) : new Color(100, 235, 255));
        DrawOverlayText(showBall ? "BALL" : "LANDING", new Vector2(x + 12, y), Color.White, 0.46f);
        x += showBall ? 63 : 79;
        DrawFeedbackMarker(_feedbackMapDot, new Vector2(x + 5, y + 6), 8, new Color(246, 250, 236));
        DrawOverlayText("FIELDERS", new Vector2(x + 12, y), Color.White, 0.46f);
        x += 85;
        DrawFeedbackMarker(_feedbackMapDot, new Vector2(x + 5, y + 6), 8, new Color(127, 255, 170));
        DrawOverlayText("BATTERS", new Vector2(x + 12, y), Color.White, 0.46f);
    }

    private void DrawWicketMark(Vector2 center, float halfWidth)
    {
        DrawHudLine(center - new Vector2(halfWidth, 0f), center + new Vector2(halfWidth, 0f),
            new Color(255, 247, 211), 2.2f);
    }

    private void DrawAimArrowHead(Vector2 start, Vector2 end, Color color)
    {
        var direction = end - start;
        if (direction.LengthSquared() <= 0.001f)
            return;
        direction.Normalize();
        var side = new Vector2(-direction.Y, direction.X);
        const float arrowLength = 8f;
        const float arrowHalfWidth = 4f;
        DrawHudLine(end, end - direction * arrowLength + side * arrowHalfWidth, color, 2f);
        DrawHudLine(end, end - direction * arrowLength - side * arrowHalfWidth, color, 2f);
    }

    private void DrawFieldCircle(Vector2 center, float radius, Color color, float thickness)
    {
        const int segments = 64;
        var previous = center + new Vector2(radius, 0f);
        for (var index = 1; index <= segments; index++)
        {
            var angle = MathHelper.TwoPi * index / segments;
            var next = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            DrawHudLine(previous, next, color, thickness);
            previous = next;
        }
    }

    private void DrawHudLine(Vector2 start, Vector2 end, Color color, float thickness)
    {
        var delta = end - start;
        var length = delta.Length();
        if (length <= 0.1f)
            return;
        _spriteBatch.Draw(_feedbackMapPixel, start, null, color,
            MathF.Atan2(delta.Y, delta.X), Vector2.Zero,
            new Vector2(length, thickness), SpriteEffects.None, 0f);
    }

    private static Vector2 MapFieldPosition(Vector3 position, Rectangle plot, float boundaryRadiusMeters)
    {
        if (plot.Width <= 0 || plot.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(plot), "Field map plot bounds must have positive dimensions.");
        if (!float.IsFinite(boundaryRadiusMeters) || boundaryRadiusMeters <= 0f)
            throw new ArgumentOutOfRangeException(nameof(boundaryRadiusMeters));

        var xFraction = Math.Clamp((position.X / boundaryRadiusMeters + 1f) * 0.5f, 0f, 1f);
        var zFraction = Math.Clamp((1f - position.Z / boundaryRadiusMeters) * 0.5f, 0f, 1f);
        return new Vector2(plot.Left + xFraction * plot.Width, plot.Top + zFraction * plot.Height);
    }

    private static Vector3 GetShotAimBoundaryPoint(Vector3 batter, float horizontalAim, float forwardAim, float boundaryRadiusMeters)
    {
        var direction = new Vector2(horizontalAim, forwardAim);
        if (direction.LengthSquared() < 0.0001f)
            direction = Vector2.UnitY;
        direction.Normalize();
        var origin = new Vector2(batter.X, batter.Z);
        var alongDirection = Vector2.Dot(origin, direction);
        var discriminant = alongDirection * alongDirection - (origin.LengthSquared() - boundaryRadiusMeters * boundaryRadiusMeters);
        var distance = -alongDirection + MathF.Sqrt(MathF.Max(0f, discriminant));
        var end = origin + direction * MathF.Max(0f, distance);
        return new Vector3(end.X, 0f, end.Y);
    }
}
