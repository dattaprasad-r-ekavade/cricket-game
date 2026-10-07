using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SuperCricket.Content;
using SuperCricket.Simulation;

namespace SuperCricket.Game;

public partial class Game1
{
    private static readonly (int Start, int End)[] BatContactZoneEdges =
    [
        (0, 1), (2, 3), (4, 5), (6, 7),
        (0, 2), (1, 3), (4, 6), (5, 7),
        (0, 4), (1, 5), (2, 6), (3, 7)
    ];

    private const float BounceSpotFeedbackDurationSeconds = 4.5f;
    private const float ContactFeedbackDurationSeconds = 4f;
    private const float LiveFeedbackBannerDurationSeconds = 7f;
    private const float PitchMapLateralHalfExtentMeters = 9f;
    private const float PitchMapLengthMarginMeters = 2f;
    private float _deliverySpeedKilometersPerHour;
    private Vector3? _predictedBouncePosition;
    private Vector3? _firstBouncePosition;
    private Vector3? _activeBowlingTargetPosition;
    private float _bounceSpotFeedbackRemainingSeconds;
    private float _contactFeedbackRemainingSeconds;
    private float _liveFeedbackBannerRemainingSeconds;
    private float? _contactFeedbackQuality;
    private bool _contactFeedbackIsMiss;
    private float? _shotInputDelaySeconds;
    private BattingTimingCalibrationAsset _battingTimingCalibration = null!;
    private bool IsHumanBowling => _activeBowlingTargetPosition is not null;
    private ContactFeedbackPresentationState ContactFeedbackPresentation => new(
        _contactFeedbackIsMiss,
        _contactFeedbackQuality,
        _gameSettings.HighContrast);

    private string GetContactFeedbackLabel() =>
        MatchHudPresenter.GetContactFeedbackLabel(ContactFeedbackPresentation);

    private Color GetContactFeedbackColor() =>
        MatchHudPresenter.GetContactFeedbackColor(ContactFeedbackPresentation);

    private static Color GetBallTrailColor(float speedMetersPerSecond) =>
        MatchHudPresenter.GetBallTrailColor(speedMetersPerSecond);

    private void DrawWorldFeedbackMarkers()
    {
        var showPredictedBounce = ShouldShowPredictedBounce(
            IsHumanBowling,
            _deliveryComplete,
            _firstBouncePosition is not null,
            _predictedBouncePosition is not null);
        var showBattingContactZone = GetBattingContactZoneAlpha() > 0 && !IsCpuBattingControlled &&
            _bowlerReleased && !_battedBall && !_deliveryComplete;
        if (!showPredictedBounce && !showBattingContactZone &&
            _bounceSpotFeedbackRemainingSeconds <= 0f && _contactFeedbackRemainingSeconds <= 0f)
            return;

        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.BlendState = BlendState.AlphaBlend;

        if (showPredictedBounce && _predictedBouncePosition is { } predictedBounce)
        {
            var withinPitch = MathF.Abs(predictedBounce.X) <= _deliveryPreset.PitchWidthMeters * 0.5f &&
                MathF.Abs(predictedBounce.Z) <= _deliveryPreset.PitchLengthMeters * 0.5f;
            var surfaceHeight = withinPitch
                ? _deliveryPreset.PitchSurfaceHeightMeters
                : _deliveryPreset.FieldSurfaceHeightMeters;
            var center = new Vector3(predictedBounce.X, surfaceHeight + 0.04f, predictedBounce.Z);
            var alpha = _gameSettings.HighContrast ? 0.98f : !_bowlerReleased ? 0.94f : _cpuDifficulty switch
            {
                CpuDifficulty.Rookie => 0.92f,
                CpuDifficulty.Standard => 0.72f,
                _ => 0.52f
            };
            var color = WithAlpha(_gameSettings.HighContrast ? Color.White : new Color(72, 222, 255), alpha);
            var radius = !_bowlerReleased ? 0.68f : _cpuDifficulty switch
            {
                CpuDifficulty.Rookie => 0.72f,
                CpuDifficulty.Standard => 0.58f,
                _ => 0.44f
            };
            GraphicsDevice.DepthStencilState = DepthStencilState.None;
            DrawFeedbackWorldRing(center, Vector3.UnitX, Vector3.UnitZ, radius, color);
            DrawFeedbackWorldRing(center, Vector3.UnitX, Vector3.UnitZ, 0.12f, WithAlpha(Color.White, alpha));
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        }

        if (showBattingContactZone)
            DrawBattingContactZone();

        if (_firstBouncePosition is { } bounce && _bounceSpotFeedbackRemainingSeconds > 0f)
        {
            var progress = 1f - _bounceSpotFeedbackRemainingSeconds / BounceSpotFeedbackDurationSeconds;
            var withinPitch = MathF.Abs(bounce.X) <= _deliveryPreset.PitchWidthMeters * 0.5f &&
                MathF.Abs(bounce.Z) <= _deliveryPreset.PitchLengthMeters * 0.5f;
            var surfaceHeight = withinPitch
                ? _deliveryPreset.PitchSurfaceHeightMeters
                : _deliveryPreset.FieldSurfaceHeightMeters;
            var center = new Vector3(bounce.X, surfaceHeight + 0.035f, bounce.Z);
            var alpha = Math.Clamp(_bounceSpotFeedbackRemainingSeconds / 0.28f, 0f, 1f);
            var color = WithAlpha(_gameSettings.HighContrast ? Color.Yellow : new Color(255, 220, 74), alpha);
            DrawFeedbackWorldRing(center, Vector3.UnitX, Vector3.UnitZ, 0.25f + progress * 0.55f, color);
            DrawFeedbackWorldRing(center, Vector3.UnitX, Vector3.UnitZ, 0.10f, WithAlpha(Color.White, alpha * 0.9f));
        }

        if (_contactMarkerPosition is { } contact && _contactFeedbackRemainingSeconds > 0f)
        {
            GraphicsDevice.DepthStencilState = DepthStencilState.None;
            var progress = 1f - _contactFeedbackRemainingSeconds / ContactFeedbackDurationSeconds;
            var alpha = Math.Clamp(_contactFeedbackRemainingSeconds / 0.24f, 0f, 1f);
            var quality = _contactFeedbackQuality ?? 0f;
            var tint = WithAlpha(GetContactFeedbackColor(), alpha);
            var viewDirection = _camera.Target - _camera.Position;
            if (viewDirection.LengthSquared() < 0.0001f)
                viewDirection = Vector3.UnitZ;
            viewDirection.Normalize();
            var right = Vector3.Cross(viewDirection, Vector3.Up);
            if (right.LengthSquared() < 0.0001f)
                right = Vector3.UnitX;
            right.Normalize();
            var up = Vector3.Normalize(Vector3.Cross(right, viewDirection));
            var center = contact + Vector3.UnitY * 0.04f;
            var radius = 0.18f + Math.Clamp(quality, 0f, 1f) * 0.14f + progress * 0.18f;
            DrawFeedbackWorldRing(center, right, up, radius, tint);
        }

        GraphicsDevice.BlendState = BlendState.Opaque;
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
    }

    private static bool ShouldShowPredictedBounce(
        bool isHumanBowling,
        bool deliveryComplete,
        bool hasBounced,
        bool hasPrediction) =>
        !isHumanBowling && !deliveryComplete && !hasBounced && hasPrediction;

    private int GetBattingContactZoneAlpha() => MatchHudPresenter.GetBattingContactZoneAlpha(_cpuDifficulty);

    private void DrawBattingContactZone()
    {
        var padding = _deliveryPreset.BallRadiusMeters + _shotSet.ContactPaddingMeters;
        var margin = new Vector3(padding);
        var minimum = _batBladeMinimum - margin;
        var maximum = _batBladeMaximum + margin;
        Span<Vector3> corners = stackalloc Vector3[8];
        for (var index = 0; index < corners.Length; index++)
        {
            var local = new Vector3(
                (index & 1) == 0 ? minimum.X : maximum.X,
                (index & 2) == 0 ? minimum.Y : maximum.Y,
                (index & 4) == 0 ? minimum.Z : maximum.Z);
            corners[index] = Vector3.Transform(local, _currentBatWorld);
        }

        var alpha = GetBattingContactZoneAlpha();
        var zoneColor = _gameSettings.HighContrast
            ? WithAlpha(Color.Yellow, alpha / 255f)
            : WithAlpha(_cpuDifficulty == CpuDifficulty.Rookie
                ? new Color(120, 255, 177)
                : new Color(111, 224, 255), alpha / 255f);
        for (var edgeIndex = 0; edgeIndex < BatContactZoneEdges.Length; edgeIndex++)
        {
            var edge = BatContactZoneEdges[edgeIndex];
            _feedbackRingVertices[edgeIndex * 2] = new VertexPositionColor(corners[edge.Start], zoneColor);
            _feedbackRingVertices[edgeIndex * 2 + 1] = new VertexPositionColor(corners[edge.End], zoneColor);
        }

        foreach (var pass in _lineEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(
                PrimitiveType.LineList,
                _feedbackRingVertices,
                0,
                BatContactZoneEdges.Length);
        }
    }

    private void DrawFeedbackWorldRing(Vector3 center, Vector3 axisA, Vector3 axisB, float radius, Color color)
    {
        const int segmentCount = 32;
        for (var segment = 0; segment < segmentCount; segment++)
        {
            var startAngle = MathHelper.TwoPi * segment / segmentCount;
            var endAngle = MathHelper.TwoPi * (segment + 1) / segmentCount;
            _feedbackRingVertices[segment * 2] = new VertexPositionColor(
                center + (axisA * MathF.Cos(startAngle) + axisB * MathF.Sin(startAngle)) * radius,
                color);
            _feedbackRingVertices[segment * 2 + 1] = new VertexPositionColor(
                center + (axisA * MathF.Cos(endAngle) + axisB * MathF.Sin(endAngle)) * radius,
                color);
        }

        foreach (var pass in _lineEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, _feedbackRingVertices, 0, segmentCount);
        }
    }

    private static Color WithAlpha(Color color, float alpha) =>
        new(color.R, color.G, color.B, (byte)MathF.Round(color.A * Math.Clamp(alpha, 0f, 1f)));

    private BattingTimingCue? GetLiveBattingTimingCue()
    {
        if (IsCpuBattingControlled || IsHumanBowling || !_bowlerReleased || _deliveryComplete ||
            _battedBall || _chosenShot is not null || _shotResolved || _footworkTransitionActive ||
            (_simulationPaused && _captureTarget is null))
            return null;

        var possibleShots = MathF.Abs(_humanShotAimOffset) < 0.08f
            ? new[] { "defence", "loft" }
            : new[] { "drive", "loft" };
        var safeWindowStart = float.NegativeInfinity;
        var safeWindowEnd = float.PositiveInfinity;
        foreach (var shotName in possibleShots)
        {
            var idealInputDelay = FindCurrentIdealInputDelaySeconds(shotName);
            if (idealInputDelay is not { } ideal)
                return null;
            safeWindowStart = MathF.Max(safeWindowStart,
                ideal - _battingTimingCalibration.OnTimeWindowSeconds);
            safeWindowEnd = MathF.Min(safeWindowEnd,
                ideal + _battingTimingCalibration.OnTimeWindowSeconds);
        }

        if (safeWindowEnd <= safeWindowStart)
            return null;
        var safeIdealInputDelay = (safeWindowStart + safeWindowEnd) * 0.5f;
        var safeWindowHalfWidth = (safeWindowEnd - safeWindowStart) * 0.5f;
        return BattingTimingFeedbackModel.EvaluateCue(
            _ballFlight.CurrentFrame.TimeSeconds,
            safeIdealInputDelay,
            safeWindowHalfWidth);
    }


}
