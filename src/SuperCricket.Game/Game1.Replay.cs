using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SuperCricket.Game.Rendering;
using SuperCricket.Simulation;

namespace SuperCricket.Game;

public partial class Game1
{
    private ReplayFrame? ReplayFrameForDraw => _replayPlayback?.CurrentFrame;

    private void QueueReplayEvent(
        string eventType,
        float? projectedImpactX = null,
        float? projectedImpactHeightMeters = null,
        float? creaseDistanceMeters = null,
        string? caption = null)
    {
        if (IsReviewRun || _replayPlayback is not null)
            return;

        var eventCapture = new ReplayEventCapture(
            eventType,
            _replayFrameTimestampSeconds,
            projectedImpactX,
            projectedImpactHeightMeters,
            creaseDistanceMeters,
            _match.InningsNumber,
            caption);
        if (_pendingReplayEvent is { } current && IsSameReplayEvent(current, eventCapture))
        {
            _pendingReplayEvent = MergeReplayEvent(current, eventCapture);
            return;
        }
        var queuedIndex = _queuedReplayEvents.FindIndex(candidate => IsSameReplayEvent(candidate, eventCapture));
        if (queuedIndex >= 0)
            _queuedReplayEvents[queuedIndex] = MergeReplayEvent(_queuedReplayEvents[queuedIndex], eventCapture);
        else
            _queuedReplayEvents.Add(eventCapture);
    }

    private static bool IsSameReplayEvent(ReplayEventCapture first, ReplayEventCapture second)
    {
        var sameType = string.Equals(first.EventType, second.EventType, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(first.EventType, "near-run-out", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(second.EventType, "run-out", StringComparison.OrdinalIgnoreCase);
        return sameType && first.InningsNumber == second.InningsNumber &&
            Math.Abs(first.TimestampSeconds - second.TimestampSeconds) <= 0.02d;
    }

    private static ReplayEventCapture MergeReplayEvent(ReplayEventCapture current, ReplayEventCapture next) =>
        current with
        {
            EventType = string.Equals(next.EventType, "run-out", StringComparison.OrdinalIgnoreCase)
                ? next.EventType : current.EventType,
            ProjectedImpactX = next.ProjectedImpactX ?? current.ProjectedImpactX,
            ProjectedImpactHeightMeters = next.ProjectedImpactHeightMeters ?? current.ProjectedImpactHeightMeters,
            CreaseDistanceMeters = next.CreaseDistanceMeters ?? current.CreaseDistanceMeters,
            Caption = next.Caption ?? current.Caption
        };

    private void RecordReplayFrame()
    {
        if (IsReviewRun || _replayPlayback is not null)
            return;

        var ball = _fielderThrowActive ? GetFielderThrowBallPosition() : _ballFlight.CurrentFrame.Position;
        var (strikerWorld, nonStrikerWorld) = GetBatterWorlds();
        var actors = new ReplayActorPose[_fielderAnimators.Length + 3];
        actors[0] = CaptureReplayActor(strikerWorld, _playerAnimator);
        actors[1] = CaptureReplayActor(nonStrikerWorld, _batterAnimations.NonStriker);
        actors[2] = CaptureReplayActor(GetBowlerWorld(), _bowlerAnimator);
        for (var index = 0; index < _fielderAnimators.Length; index++)
            actors[index + 3] = CaptureReplayActor(GetFielderWorld(index, ball), _fielderAnimators[index]);

        _replayRecorder.Record(new ReplayFrame(
            _replayFrameTimestampSeconds,
            ToXna(ball),
            _bowlerReleased,
            _camera.Position,
            _camera.Target,
            _camera.FieldOfViewDegrees,
            actors,
            _trajectoryVertices.ToArray()));
        _replayClockSeconds = _replayFrameTimestampSeconds;
        TryStartPendingReplay();
        TryStartFallOfWicketsReplay();
    }

    private static ReplayActorPose CaptureReplayActor(Matrix world, Animation.PlayerAnimator animator) =>
        new(world, animator.CurrentClipName, animator.CurrentTimeSeconds, animator.GetSkinMatrices().ToArray());

    private void TryStartPendingReplay()
    {
        if (_pendingReplayEvent is null && _queuedReplayEvents.Count > 0)
        {
            _pendingReplayEvent = _queuedReplayEvents[0];
            _queuedReplayEvents.RemoveAt(0);
        }
        if (_pendingReplayEvent is not { } replayEvent || _replayScriptCatalog is null || _replayRecorder.Count == 0)
            return;

        var script = _replayScriptCatalog.GetEvent(replayEvent.EventType);
        var sourceStartOffset = script.Shots.Min(shot => shot.StartOffsetSeconds);
        var sourceEndOffset = script.Shots.Max(shot => shot.StartOffsetSeconds + shot.DurationSeconds);
        if (_replayClockSeconds < replayEvent.TimestampSeconds + sourceEndOffset)
            return;

        var frames = _replayRecorder.Snapshot(
            replayEvent.TimestampSeconds + sourceStartOffset,
            replayEvent.TimestampSeconds + sourceEndOffset);
        if (frames.Length == 0)
        {
            _pendingReplayEvent = null;
            return;
        }

        if (replayEvent.Caption is { } caption && replayEvent.InningsNumber == _replayBookmarkInningsNumber)
        {
            _inningsWicketReplays.Add(new ReplayBookmark(
                script,
                replayEvent.TimestampSeconds,
                frames,
                caption,
                replayEvent.ProjectedImpactX,
                replayEvent.ProjectedImpactHeightMeters,
                replayEvent.CreaseDistanceMeters));
        }

        _replayPlayback = new ReplayPlayback(
            script,
            replayEvent.TimestampSeconds,
            frames,
            replayEvent.ProjectedImpactX,
            replayEvent.ProjectedImpactHeightMeters,
            replayEvent.CreaseDistanceMeters,
            replayEvent.Caption);
        _pendingReplayEvent = null;
    }

    private string BuildFallOfWicketsCaption(string dismissalKind)
    {
        var result = _match.CurrentDelivery?.Result;
        var dismissedName = result?.DismissedEnd == DismissedEnd.NonStriker
            ? _deliveryNonStrikerName
            : _deliveryStrikerName;
        var dismissalText = dismissalKind.Equals("RunOut", StringComparison.OrdinalIgnoreCase)
            ? "run out"
            : dismissalKind.ToLowerInvariant();
        return $"FOW {_match.Runs}/{_match.Wickets}, {dismissedName ?? "Batter"} {dismissalText} {result?.BatterRuns ?? 0}";
    }

    private void TryStartFallOfWicketsReplay()
    {
        if (!_match.IsInningsComplete || _replayPlayback is not null || _pendingReplayEvent is not null || _queuedReplayEvents.Count > 0)
            return;
        if (_fallOfWicketsInningsNumber != _match.InningsNumber)
        {
            _fallOfWicketsInningsNumber = _match.InningsNumber;
            _fallOfWicketsReplayIndex = 0;
        }
        if (_fallOfWicketsReplayIndex >= _inningsWicketReplays.Count)
            return;

        var bookmark = _inningsWicketReplays[_fallOfWicketsReplayIndex++];
        _replayPlayback = new ReplayPlayback(
            bookmark.Script,
            bookmark.EventTimestampSeconds,
            bookmark.Frames,
            bookmark.ProjectedImpactX,
            bookmark.ProjectedImpactHeightMeters,
            bookmark.CreaseDistanceMeters,
            bookmark.Caption);
    }

    private void PrepareLbwReplayPreview()
    {
        SetBowlerCaptureReleasePose();
        _camera.SelectPreset("square-leg");
        var (strikerWorld, nonStrikerWorld) = GetBatterWorlds();
        var actors = new ReplayActorPose[_fielderAnimators.Length + 3];
        actors[0] = CaptureReplayActor(strikerWorld, _playerAnimator);
        actors[1] = CaptureReplayActor(nonStrikerWorld, _batterAnimations.NonStriker);
        actors[2] = CaptureReplayActor(GetBowlerWorld(), _bowlerAnimator);
        for (var index = 0; index < _fielderAnimators.Length; index++)
            actors[index + 3] = CaptureReplayActor(GetFielderWorld(index, _ballFlight.CurrentFrame.Position), _fielderAnimators[index]);

        var frames = new ReplayFrame[61];
        var trail = new VertexPositionColor[2];
        for (var index = 0; index < frames.Length; index++)
        {
            var amount = index / (float)(frames.Length - 1);
            var ball = new Vector3(
                0.15f + MathF.Sin(amount * MathHelper.Pi) * 0.12f,
                0.82f + MathF.Sin(amount * MathHelper.Pi) * 0.2f,
                4.5f - amount * 13f);
            trail[0] = new VertexPositionColor(new Vector3(0.15f, 0.85f, 5f), new Color(255, 185, 92));
            trail[1] = new VertexPositionColor(ball, new Color(255, 185, 92));
            frames[index] = new ReplayFrame(
                7d + amount * 5d,
                ball,
                true,
                _camera.Position,
                _camera.Target,
                _camera.FieldOfViewDegrees,
                actors,
                trail.ToArray());
        }

        _replayPlayback = new ReplayPlayback(
            _replayScriptCatalog.GetEvent("lbw"),
            eventTimestampSeconds: 10d,
            frames,
            projectedImpactX: 0.12f,
            projectedImpactHeightMeters: 0.82f);
    }

    private void UpdateReplay(GameTime gameTime, KeyboardState keyboard, MatchControllerButtons controllerPressedButtons)
    {
        var playback = _replayPlayback;
        if (playback is null)
            return;
        if (!_captureLbwReplayPreview &&
            (keyboard.GetPressedKeys().Length > 0 || controllerPressedButtons != MatchControllerButtons.None))
            playback.Skip();
        else
            playback.Advance(MathF.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds));
        if (playback.IsComplete)
            _replayPlayback = null;
        if (_replayPlayback is null)
            TryStartFallOfWicketsReplay();
    }

    private void DrawReplayOverlay()
    {
        if (_replayPlayback is not { } playback)
            return;

        var viewport = GraphicsDevice.Viewport;
        var label = playback.Caption ?? $"REPLAY  /  {playback.EventType.ToUpperInvariant()}";
        var labelSize = _debugFont.MeasureString(label) * 1.1f;
        var panel = new Rectangle(18, 18, Math.Max(300, (int)labelSize.X + 44), 58);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(_feedbackMapPixel, panel, new Color(10, 18, 29, 225));
        _spriteBatch.Draw(_feedbackMapPixel, new Rectangle(panel.X, panel.Y, 5, panel.Height), new Color(255, 192, 84));
        _spriteBatch.DrawString(_debugFont, label, new Vector2(panel.X + 18, panel.Y + 5), Color.White, 0f, Vector2.Zero, 1.1f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_debugFont, "Any key to skip", new Vector2(panel.X + 18, panel.Y + 32), new Color(188, 206, 217), 0f, Vector2.Zero, 0.82f, SpriteEffects.None, 0f);

        if (string.Equals(playback.CurrentOverlay, "stump-line-strip", StringComparison.OrdinalIgnoreCase))
            DrawReplayStumpLine(playback, viewport);
        else if (string.Equals(playback.CurrentOverlay, "crease-zoom", StringComparison.OrdinalIgnoreCase))
            DrawReplayCreaseZoom(playback, viewport);

        _spriteBatch.End();
    }

    private void DrawReplayStumpLine(ReplayPlayback playback, Viewport viewport)
    {
        var bounds = new Rectangle(viewport.Width - 300, viewport.Height - 152, 278, 128);
        _spriteBatch.Draw(_feedbackMapPixel, bounds, new Color(10, 18, 29, 228));
        _spriteBatch.DrawString(_debugFont, "STUMP LINE  /  IMPACT", new Vector2(bounds.X + 14, bounds.Y + 8), Color.White, 0f, Vector2.Zero, 0.78f, SpriteEffects.None, 0f);
        var wicketY = bounds.Y + 90;
        var centerX = bounds.X + bounds.Width / 2;
        _spriteBatch.Draw(_feedbackMapPixel, new Rectangle(centerX - 49, wicketY, 98, 3), new Color(178, 208, 220));
        for (var stump = -1; stump <= 1; stump++)
            _spriteBatch.Draw(_feedbackMapPixel, new Rectangle(centerX + stump * 11 - 2, wicketY - 30, 4, 32), new Color(238, 224, 181));

        if (playback.ProjectedImpactX is { } impactX && playback.ProjectedImpactHeightMeters is { } impactHeight)
        {
            var markerX = centerX + (int)(Math.Clamp(impactX / 0.45f, -1f, 1f) * 52f);
            var markerY = wicketY - 30 - (int)(Math.Clamp(impactHeight / 1.8f, 0f, 1f) * 45f);
            _spriteBatch.Draw(_feedbackMapPixel, new Rectangle(markerX - 6, markerY - 6, 13, 13), new Color(252, 105, 72));
        }
    }

    private void DrawReplayCreaseZoom(ReplayPlayback playback, Viewport viewport)
    {
        var bounds = new Rectangle(viewport.Width - 280, viewport.Height - 98, 258, 74);
        _spriteBatch.Draw(_feedbackMapPixel, bounds, new Color(10, 18, 29, 228));
        var text = playback.CreaseDistanceMeters is { } distance
            ? $"CREASE REVIEW  /  {distance:0.00} m"
            : "CREASE REVIEW";
        _spriteBatch.DrawString(_debugFont, text, new Vector2(bounds.X + 13, bounds.Y + 10), Color.White, 0f, Vector2.Zero, 0.82f, SpriteEffects.None, 0f);
        _spriteBatch.Draw(_feedbackMapPixel, new Rectangle(bounds.X + 15, bounds.Y + 49, bounds.Width - 30, 3), new Color(178, 208, 220));
        _spriteBatch.Draw(_feedbackMapPixel, new Rectangle(bounds.X + bounds.Width / 2, bounds.Y + 40, 3, 22), new Color(255, 192, 84));
    }
}
