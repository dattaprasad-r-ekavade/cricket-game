using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SuperCricket.Content;
using SuperCricket.Game.Animation;
using SuperCricket.Game.Rendering;
using SuperCricket.Simulation;
using NumericsVector3 = System.Numerics.Vector3;

namespace SuperCricket.Game;

public partial class Game1
{
    protected override void Draw(GameTime gameTime)
    {
        var drawStart = Stopwatch.GetTimestamp();
        if (_captureTarget is not null)
            GraphicsDevice.SetRenderTarget(_captureTarget);
        GraphicsDevice.Clear(new Color(116, 161, 195));
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        GraphicsDevice.BlendState = BlendState.Opaque;

        var replayFrame = ReplayFrameForDraw;
        var cameraPosition = replayFrame?.CameraPosition ?? _camera.Position;
        var cameraTarget = replayFrame?.CameraTarget ?? _camera.Target;
        var cameraFieldOfView = replayFrame?.CameraFieldOfViewDegrees ?? _camera.FieldOfViewDegrees;
        if (_replayPlayback is { } playback &&
            !string.Equals(playback.CurrentCameraPreset, "recorded", StringComparison.OrdinalIgnoreCase))
        {
            _replayCamera.SelectPreset(playback.CurrentCameraPreset);
            cameraPosition = _replayCamera.Position;
            cameraTarget = _replayCamera.Target;
            cameraFieldOfView = _replayCamera.FieldOfViewDegrees;
        }
        _worldEffect.World = Matrix.Identity;
        _worldEffect.View = Matrix.CreateLookAt(cameraPosition, cameraTarget, Vector3.Up);
        _worldEffect.Projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(cameraFieldOfView),
            GraphicsDevice.Viewport.AspectRatio,
            0.05f,
            250f);
        DrawTexturedSurfaces();
        var liveBatterWorlds = GetBatterWorlds();
        var strikerWorld = replayFrame?.Actors[0].World ?? liveBatterWorlds.Striker;
        var nonStrikerWorld = replayFrame?.Actors[1].World ?? liveBatterWorlds.NonStriker;
        var ballPosition = replayFrame is not null
            ? ToNumerics(replayFrame.BallPosition)
            : _fielderThrowActive
            ? GetFielderThrowBallPosition()
            : _ballFlight.CurrentFrame.Position;
        BuildShadowVertices(strikerWorld, nonStrikerWorld, ballPosition);

        foreach (var pass in _worldEffect.CurrentTechnique.Passes)
        {
            _worldEffect.World = Matrix.Identity;
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(
                PrimitiveType.TriangleList,
                _groundVertices,
                0,
                _groundVertices.Length / 3);

        }

        _stadiumPropRenderer.Draw(_worldEffect, _worldEffect.View, _worldEffect.Projection);
        DrawCrowd();
        DrawShadows();

        if (replayFrame?.BallVisible ?? _bowlerReleased)
        {
            _worldEffect.World = Matrix.CreateScale(_deliveryPreset.BallRadiusMeters)
                * Matrix.CreateTranslation(ToXna(ballPosition));
            foreach (var pass in _worldEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _ballVertices, 0, _ballVertices.Length / 3);
            }
            _worldEffect.World = Matrix.Identity;
        }

        _lineEffect.World = Matrix.Identity;
        _lineEffect.View = _worldEffect.View;
        _lineEffect.Projection = _worldEffect.Projection;
        DrawBowlingTargetMarker();
        var trajectoryVertices = replayFrame?.Trajectory ?? _trajectoryVertices.ToArray();
        if ((replayFrame?.BallVisible ?? _bowlerReleased) && trajectoryVertices.Length >= 2)
        {
            foreach (var pass in _lineEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(
                    PrimitiveType.LineStrip,
                    trajectoryVertices,
                    0,
                    trajectoryVertices.Length - 1);
            }
        }

        var battingPrimaryColor = ToXna(_match.BattingTeam.PrimaryKitColor);
        var battingAccentColor = ToXna(_match.BattingTeam.AccentKitColor);
        var fieldingPrimaryColor = ToXna(_match.FieldingTeam.PrimaryKitColor);
        var fieldingAccentColor = ToXna(_match.FieldingTeam.AccentKitColor);
        for (var fielderIndex = 0; fielderIndex < _fielderAnimators.Length; fielderIndex++)
        {
            var actorPose = replayFrame?.Actors[fielderIndex + 3];
            _playerAppearanceRenderer.Draw(
                "balanced",
                actorPose?.World ?? GetFielderWorld(fielderIndex, ballPosition),
                cameraPosition,
                _worldEffect.View,
                _worldEffect.Projection,
                actorPose?.SkinMatrices ?? _fielderAnimators[fielderIndex].GetSkinMatrices(),
                fieldingPrimaryColor,
                fieldingAccentColor);
        }

        var skinMatrices = replayFrame?.Actors[0].SkinMatrices ?? _playerAnimator.GetSkinMatrices();
        _playerAppearanceRenderer.Draw(_match.StrikerPlayer.BodyVariant, strikerWorld, cameraPosition,
            _worldEffect.View, _worldEffect.Projection,
            skinMatrices, battingPrimaryColor, battingAccentColor);
        _playerAppearanceRenderer.Draw(_match.NonStrikerPlayer.BodyVariant, nonStrikerWorld, cameraPosition,
            _worldEffect.View, _worldEffect.Projection,
            replayFrame?.Actors[1].SkinMatrices ?? _batterAnimations.NonStriker.GetSkinMatrices(), battingPrimaryColor, battingAccentColor);
        _playerAppearanceRenderer.Draw(_match.CurrentBowler.BodyVariant,
            replayFrame?.Actors[2].World ?? GetBowlerWorld(), cameraPosition,
            _worldEffect.View, _worldEffect.Projection,
            replayFrame?.Actors[2].SkinMatrices ?? _bowlerAnimator.GetSkinMatrices(), fieldingPrimaryColor, fieldingAccentColor);
        if (replayFrame is null)
            DrawWorldFeedbackMarkers();
        if (_showDebugOverlay)
        {
            GraphicsDevice.DepthStencilState = DepthStencilState.None;
            DrawDebugMarkers();
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        }
        if (replayFrame is null)
        {
            if (_practiceSetupOpen || _titleMenuOpen)
            {
                DrawDebugOverlay();
            }
            else
            {
                DrawDeliveryFeedbackCard();
                DrawDebugOverlay();
                DrawTimingZoneInset();
                DrawLiveFeedbackBanner();
                DrawBattingFieldInset();
            }
        }
        DrawReplayOverlay();
        base.Draw(gameTime);
        _drawMilliseconds = Stopwatch.GetElapsedTime(drawStart).TotalMilliseconds;

        if (_profileFrameTarget > 0)
        {
            if (_profileWarmupRemaining > 0)
            {
                _profileWarmupRemaining--;
            }
            else
            {
                _profileTimings.Add(new FrameTiming(
                    _frameTimeMilliseconds,
                    _updateMilliseconds,
                    _drawMilliseconds));
                if (_profileTimings.Count >= _profileFrameTarget)
                {
                    PrintFrameProfile();
                    Exit();
                    return;
                }
            }
        }

        if (_captureTarget is not null)
        {
            var target = _captureTarget;
            GraphicsDevice.SetRenderTarget(null);
            using (var output = File.Create(_capturePath!))
                target.SaveAsPng(output, target.Width, target.Height);
            _captureTarget = null;
            target.Dispose();
            Exit();
        }
    }

    private void DrawDebugMarkers()
    {
        var vertexCount = 0;

        void AddMarker(Vector3? position, Color color)
        {
            if (position is not { } point)
                return;

            const float halfLength = 0.24f;
            _debugMarkerVertices[vertexCount++] = new VertexPositionColor(point - Vector3.UnitX * halfLength, color);
            _debugMarkerVertices[vertexCount++] = new VertexPositionColor(point + Vector3.UnitX * halfLength, color);
            _debugMarkerVertices[vertexCount++] = new VertexPositionColor(point - Vector3.UnitY * halfLength, color);
            _debugMarkerVertices[vertexCount++] = new VertexPositionColor(point + Vector3.UnitY * halfLength, color);
            _debugMarkerVertices[vertexCount++] = new VertexPositionColor(point - Vector3.UnitZ * halfLength, color);
            _debugMarkerVertices[vertexCount++] = new VertexPositionColor(point + Vector3.UnitZ * halfLength, color);
        }

        AddMarker(_releaseMarkerPosition, new Color(248, 195, 82));
        AddMarker(_contactMarkerPosition, new Color(255, 126, 64));
        AddMarker(_sweetSpotMarkerPosition, new Color(81, 224, 218));
        if (vertexCount == 0)
            return;

        foreach (var pass in _lineEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(
                PrimitiveType.LineList,
                _debugMarkerVertices,
                0,
                vertexCount / 2);
        }
    }

    private void DrawBowlingTargetMarker()
    {
        var practiceTarget = _practiceNetsMode && _practiceNetsSetup.Drill == PracticeNetDrill.BowlingTarget
            ? _practiceTargetCenter
            : null;
        if (practiceTarget is { } fixedPracticeTarget)
            _bowlingTargetMarkerPosition = fixedPracticeTarget;
        if (_bowlingTargetMarkerPosition is not { } center)
            return;

        const int ringSegments = 12;
        var runUpProgress = HasHumanBowlingControls && _humanBowlingStarted
            ? Math.Clamp(_bowlerRunUpElapsed / MathF.Max(0.001f, _bowlerRunUpDurationSeconds), 0f, 1f)
            : 0f;
        var ringRadius = practiceTarget is not null
            ? PracticeTargetRadiusMeters
            : HasHumanBowlingControls
            ? MathHelper.Lerp(MathF.Max(0.68f, _bowlingTargetRadiusMeters), _bowlingTargetRadiusMeters, runUpProgress)
            : 0.55f;
        const float crossHalfLength = 0.16f;
        var markerColor = new Color(82, 255, 220);
        for (var segment = 0; segment < ringSegments; segment++)
        {
            var startAngle = MathHelper.TwoPi * segment / ringSegments;
            var endAngle = MathHelper.TwoPi * (segment + 1) / ringSegments;
            _bowlingTargetMarkerVertices[segment * 2] = new VertexPositionColor(
                center + new Vector3(MathF.Cos(startAngle) * ringRadius, 0f, MathF.Sin(startAngle) * ringRadius),
                markerColor);
            _bowlingTargetMarkerVertices[segment * 2 + 1] = new VertexPositionColor(
                center + new Vector3(MathF.Cos(endAngle) * ringRadius, 0f, MathF.Sin(endAngle) * ringRadius),
                markerColor);
        }
        _bowlingTargetMarkerVertices[24] = new VertexPositionColor(center - Vector3.UnitX * crossHalfLength, markerColor);
        _bowlingTargetMarkerVertices[25] = new VertexPositionColor(center + Vector3.UnitX * crossHalfLength, markerColor);
        _bowlingTargetMarkerVertices[26] = new VertexPositionColor(center - Vector3.UnitZ * crossHalfLength, markerColor);
        _bowlingTargetMarkerVertices[27] = new VertexPositionColor(center + Vector3.UnitZ * crossHalfLength, markerColor);
        foreach (var pass in _lineEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(
                PrimitiveType.LineList,
                _bowlingTargetMarkerVertices,
                0,
                14);
        }
    }

    private void PrintFrameProfile()
    {
        static (double Average, double Median, double P95, double Maximum) Summarize(
            IReadOnlyList<FrameTiming> samples,
            Func<FrameTiming, double> valueSelector)
        {
            var values = new double[samples.Count];
            var total = 0d;
            for (var index = 0; index < samples.Count; index++)
            {
                values[index] = valueSelector(samples[index]);
                total += values[index];
            }
            Array.Sort(values);
            var medianIndex = Math.Clamp((int)Math.Ceiling(values.Length * 0.5d) - 1, 0, values.Length - 1);
            var p95Index = Math.Clamp((int)Math.Ceiling(values.Length * 0.95d) - 1, 0, values.Length - 1);
            return (total / values.Length, values[medianIndex], values[p95Index], values[^1]);
        }

        static void WriteSummary(string name, (double Average, double Median, double P95, double Maximum) summary) =>
            Console.WriteLine($"{name}: avg {summary.Average:0.00} ms, p50 {summary.Median:0.00} ms, p95 {summary.P95:0.00} ms, max {summary.Maximum:0.00} ms");

        Console.WriteLine($"Renderer profile: {GraphicsDevice.Viewport.Width}x{GraphicsDevice.Viewport.Height}, VSync enabled");
        Console.WriteLine($"Measured {_profileTimings.Count} rendered frames after {_profileWarmupFrameCount} warm-up frames.");
        WriteSummary("Frame interval", Summarize(_profileTimings, static sample => sample.FrameIntervalMilliseconds));
        WriteSummary("CPU update", Summarize(_profileTimings, static sample => sample.UpdateCpuMilliseconds));
        WriteSummary("CPU draw submission", Summarize(_profileTimings, static sample => sample.DrawCpuMilliseconds));
        Console.WriteLine("GPU execution time is not included in CPU draw submission; use a GPU profiler for that measurement.");
    }


}
