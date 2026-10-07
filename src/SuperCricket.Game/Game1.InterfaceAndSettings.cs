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
    protected override void UnloadContent()
    {
        _playerRenderer?.Dispose();
        _bowlerRenderer?.Dispose();
        _crowdVertexBuffer?.Dispose();
        _worldEffect?.Dispose();
        _crowdEffect?.Dispose();
        _lineEffect?.Dispose();
        _captureTarget?.Dispose();
        foreach (var audioCue in _audioCues.Values)
            audioCue.Dispose();
        _audioCues.Clear();
        _debugPanel?.Dispose();
        _feedbackMapPixel?.Dispose();
        _feedbackMapDot?.Dispose();
        _feedbackMapRing?.Dispose();
        _spriteBatch?.Dispose();
        base.UnloadContent();
    }

    private string GetPrimaryControlHint()
    {
        var phase = MatchHudPresenter.ResolvePhase(new MatchHudConditions(
            _match.IsMatchComplete,
            _match.IsInningsComplete,
            IsCpuBattingControlled,
            _deliveryComplete,
            _isRunning,
            _battedBall));
        return MatchHudPresenter.GetPrimaryControlHint(new MatchHudState(
            _lastInputWasGamePad,
            phase,
            _deliveryPresets[_nextDeliveryPresetIndex].Name));
    }

    private void DrawDebugOverlay()
    {
        if (_simulationPaused && _captureTarget is null)
        {
            DrawPauseMenu();
            return;
        }

        if (!_showDebugOverlay)
        {
            var eventText = _shotOutcome.Length > 72 ? _shotOutcome[..69] + "..." : _shotOutcome;
            var batterText = _match.IsMatchComplete
                ? "Match complete"
                : _match.IsInningsComplete
                    ? "Innings complete; press N to start the chase"
                    : IsCpuBattingControlled
                        ? $"CPU batting: {_match.StrikerPlayer.Name}    Non-striker: {_match.NonStrikerPlayer.Name}"
                        : $"On strike: {_match.StrikerPlayer.Name}    Non-striker: {_match.NonStrikerPlayer.Name}";
            var matchLines = new List<string>
            {
                $"SUPER CRICKET / SHORT MATCH  |  CPU {_cpuDifficulty}  |  TRAIL cool = slower / warm = faster",
                ScoreStatusText,
                batterText,
                _match.IsMatchComplete ? "Match finished" : eventText,
                GetPrimaryControlHint()
            };
            var scale = _gameSettings.LargeText ? 1.25f : 1f;
            var lineSpacing = (int)MathF.Round(22 * scale);
            var contentWidth = 0f;
            foreach (var line in matchLines)
                contentWidth = Math.Max(contentWidth, _debugFont.MeasureString(line).X * scale);
            var panelWidth = Math.Min(GraphicsDevice.Viewport.Width - 40,
                (int)MathF.Ceiling(contentWidth + 28 * scale));
            var panelHeight = 14 + (int)MathF.Ceiling(matchLines.Count * lineSpacing + 14 * scale);
            _matchHudBounds = new Rectangle(20, 20, panelWidth, panelHeight);
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            _spriteBatch.Draw(_debugPanel, _matchHudBounds,
                _gameSettings.HighContrast ? Color.Black : Color.White);
            for (var index = 0; index < matchLines.Count; index++)
            {
                var color = index == 0
                    ? (_gameSettings.HighContrast ? Color.Yellow : new Color(242, 206, 116))
                    : Color.White;
                DrawOverlayText(matchLines[index], new Vector2(34, 24 + index * lineSpacing), color, scale);
            }
            _spriteBatch.End();
            return;
        }

        var ball = _ballFlight.CurrentFrame;
        var lines = new[]
        {
            $"SUPER CRICKET  /  SHORT MATCH    CPU {_cpuDifficulty}    seed {_matchController.BowlingSeed}",
            $"Pitch {PracticeGround.PitchLength:0.00} m x {PracticeGround.PitchWidth:0.00} m    Stumps {PracticeGround.WicketHeight:0.00} m",
            $"{ScoreStatusText}    Striker {_match.StrikerPlayer.Name}    legal balls {_match.LegalBalls}/{_match.OversPerInnings * OverScoreboard.BallsPerOver}",
            $"Preset: {_deliveryPreset.Name}    next {_deliveryPresets[_nextDeliveryPresetIndex].Name}    release ({_deliveryPreset.ReleasePosition.X:0.00}, {_deliveryPreset.ReleasePosition.Y:0.00}, {_deliveryPreset.ReleasePosition.Z:0.00}) m",
            $"Ball {(_bowlerReleased ? (_simulationPaused ? "Paused" : ball.Phase.ToString()) : "Awaiting release")}    {(_bowlerReleased ? $"speed {ball.Velocity.Length():0.0} m/s    bounces {ball.BounceCount}    position ({ball.Position.X:0.0}, {ball.Position.Y:0.0}, {ball.Position.Z:0.0}) m" : "flight simulation starts at the bowler's release")}",
            $"Player: {_playerAsset.Name}    animation {_playerAnimator.CurrentClipName}{(_playerAnimator.IsTransitioning ? " (crossfade)" : string.Empty)}",
            $"Batter footwork: {_batterFootworkOffsetX:+0.00;-0.00;0.00} m lateral",
            $"Bowler: {_bowlerAsset.Name}    {(_bowlerReleased ? "released" : _bowlerActionStarted ? "delivery stride" : "run-up")}    animation {_bowlerAnimator.CurrentClipName}",
            $"Delivery: {(_deliveryComplete ? "complete" : "live")}    {_fieldPreset.Name} ({_activeFieldingTactic}, {_fieldingSide.Positions.Count} fielders)    run {(_isRunning ? $"{MathHelper.Clamp(_runElapsed / _runDurationSeconds, 0f, 1f):P0}" : "ready")}",
            $"Event: {_shotOutcome}",
            $"Release: {(_releaseMarkerPosition is { } release ? $"t=0.000 s @ {FormatPosition(release)} m" : "not yet released")}    Contact: {(_contactMarkerPosition is { } contact ? $"t={_contactTimeSeconds:0.000} s @ {FormatPosition(contact)} m" : "waiting")}",
            $"Sweet spot: {(_sweetSpotMarkerPosition is { } sweetSpot && _contactSweetSpotOffset is { } offset && _contactQuality is { } quality ? $"q={quality:0.00} offset ({offset.X:+0.00;-0.00;0.00}, {offset.Y:+0.00;-0.00;0.00}) @ {FormatPosition(sweetSpot)} m" : "waiting for bat contact")}    Markers: gold / orange / cyan",
            $"View {_camera.PresetName}    distance {_camera.Distance:0.0} m    elevation {MathHelper.ToDegrees(_camera.Elevation):0}°    FPS {_framesPerSecond}    frame {_frameTimeMilliseconds:0.0} ms    CPU update/draw {_updateMilliseconds:0.00}/{_drawMilliseconds:0.00} ms",
            $"Skinned players {_fielderAnimators.Length + 3} ({_fielderAnimators.Length} fielders)    material batches batter/bowler {_playerRenderer.MaterialBatchCount}/{_bowlerRenderer.MaterialBatchCount}",
            "Debug: --debug enables A/S/D shots, J/L aim, Q/E steps, T animations, digits, orbit and developer overlay."
        };
        var debugScale = _gameSettings.LargeText ? 1.2f : 1f;
        var debugLineSpacing = (int)MathF.Round(23 * debugScale);
        var panel = new Rectangle(16, 16, GraphicsDevice.Viewport.Width - 32,
            18 + (int)MathF.Ceiling(lines.Length * debugLineSpacing + 5 * debugScale));
        _matchHudBounds = panel;

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(_debugPanel, panel,
            _gameSettings.HighContrast ? Color.Black : new Color(255, 255, 255, 190));
        for (var index = 0; index < lines.Length; index++)
        {
            var color = index == 0
                ? (_gameSettings.HighContrast ? Color.Yellow : new Color(242, 206, 116))
                : Color.White;
            DrawOverlayText(lines[index], new Vector2(30, 17 + index * debugLineSpacing), color, debugScale);
        }
        _spriteBatch.End();
    }

    private void DrawPauseMenu()
    {
        var viewport = GraphicsDevice.Viewport;
        var scale = _gameSettings.LargeText ? 1.3f : 1.1f;
        var lineSpacing = (int)MathF.Round(30 * scale);
        var scoreLine = $"Innings {_match.InningsNumber}/2    {_match.BattingTeamName} {_match.Runs}/{_match.Wickets}    {_match.OversText} overs";
        var lines = new List<string>
        {
            "SUPER CRICKET  /  PAUSED",
            scoreLine,
            $"CPU difficulty: {_cpuDifficulty}",
            "Batting: Left/Right aim | Space ground/defend | Shift loft",
            "GamePad batting: left stick aim | A ground/defend | Y loft",
            "Running: Enter/B starts; tap again for another; hold to turn back",
            "Bowling: arrows/D-pad move pitch target | C/LB changes delivery | N/RB bowls",
            "V/L3: camera | PgDn zoom in / PgUp out | R/A replay | D/LB difficulty | O/RB overs when match ends",
            $"Paused: P/Start resumes | Esc/Back quits | H/Y contrast {(_gameSettings.HighContrast ? "ON" : "OFF")}",
            $"T/Pad X: larger text {(_gameSettings.LargeText ? "ON" : "OFF")} | -/LB volume down | +/RB volume up { _gameSettings.EffectsVolume:P0}",
            _settingsStatusMessage ?? (_audioUnavailable
                ? "Audio output is unavailable; the match remains playable."
                : "Match, audio, and accessibility settings save on this device.")
        };
        if (_developerMode)
            lines.Insert(3, "Debug: A/S/D shots | J/L aim | Q/E steps | 1-4 presets | arrows orbit | PgUp/PgDn height");
        var panelWidth = Math.Min(900, viewport.Width - 40);
        var panelHeight = 44 + lines.Count * lineSpacing;
        var panelX = Math.Max(20, (viewport.Width - panelWidth) / 2);
        var panelY = Math.Max(20, (viewport.Height - panelHeight) / 2);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(_debugPanel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(0, 0, 0, 210));
        _spriteBatch.Draw(_debugPanel, new Rectangle(panelX, panelY, panelWidth, panelHeight), Color.Black);
        for (var index = 0; index < lines.Count; index++)
        {
            var color = index == 0 ? Color.Yellow : Color.White;
            DrawOverlayText(lines[index], new Vector2(panelX + 28, panelY + 18 + index * lineSpacing), color, scale);
        }
        _spriteBatch.End();
    }

    private void DrawOverlayText(string text, Vector2 position, Color color, float scale) =>
        _spriteBatch.DrawString(_debugFont, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

    private void LoadGameSettings()
    {
        if (IsReviewRun)
        {
            _gameSettings = new GameSettings();
            _cpuDifficulty = _gameSettings.Difficulty;
            _selectedOversPerInnings = _gameSettings.OversPerInnings;
            return;
        }
        try
        {
            _gameSettings = GameSettingsStore.Load(GameSettingsStore.DefaultPath);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or
            UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException)
        {
            _gameSettings = new GameSettings();
            _settingsStatusMessage = exception is InvalidDataException
                ? "Saved settings were invalid; defaults are active. Change a setting to replace them."
                : "Settings could not be read; defaults are active for this session.";
        }

        _cpuDifficulty = _gameSettings.Difficulty;
        _selectedOversPerInnings = _gameSettings.OversPerInnings;
    }

    private void SaveGameSettings()
    {
        if (IsReviewRun) return;
        try
        {
            _gameSettings.Difficulty = _cpuDifficulty;
            _gameSettings.OversPerInnings = _selectedOversPerInnings;
            GameSettingsStore.Save(GameSettingsStore.DefaultPath, _gameSettings);
            _settingsStatusMessage = null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            InvalidOperationException or System.Security.SecurityException)
        {
            _settingsStatusMessage = "Settings could not be saved; changes last for this session.";
        }
    }

    private void AdjustEffectsVolume(float amount)
    {
        var volume = Math.Clamp(MathF.Round((_gameSettings.EffectsVolume + amount) * 10f) / 10f, 0f, 1f);
        if (MathF.Abs(volume - _gameSettings.EffectsVolume) < 0.0001f)
            return;
        _gameSettings.EffectsVolume = volume;
        SaveGameSettings();
    }

    private void LoadAudioCues()
    {
        try
        {
            foreach (var cue in Enum.GetValues<CricketAudioCue>())
                _audioCues.Add(cue, new SoundEffect(
                    ProceduralCricketAudio.CreatePcmSamples(cue),
                    ProceduralCricketAudio.SampleRate,
                    AudioChannels.Mono));
        }
        catch (Exception exception) when (exception is NoAudioHardwareException or DllNotFoundException or
            PlatformNotSupportedException)
        {
            foreach (var audioCue in _audioCues.Values)
                audioCue.Dispose();
            _audioCues.Clear();
            _audioUnavailable = true;
        }
    }

    private void PlayAudio(CricketAudioCue cue)
    {
        if (IsReviewRun) return;
        if (_audioUnavailable || _gameSettings.EffectsVolume <= 0f || !_audioCues.TryGetValue(cue, out var sound))
            return;
        try
        {
            _ = sound.Play(_gameSettings.EffectsVolume, pitch: 0f, pan: 0f);
        }
        catch (InstancePlayLimitException)
        {
            // Skip a cue when the audio system is already at its instance limit.
        }
        catch (NoAudioHardwareException)
        {
            _audioUnavailable = true;
        }
    }


}
