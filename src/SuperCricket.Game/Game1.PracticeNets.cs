using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SuperCricket.Content.Bowling;
using SuperCricket.Simulation;
using SuperCricket.Simulation.Batting;

namespace SuperCricket.Game;

public partial class Game1
{
    private bool HandleModeMenuInput(KeyboardState keyboard, MatchControllerButtons pressedButtons)
    {
        _inputRouter.BeginKeyboardFrame(keyboard);
        bool KeyPressed(Keys key) => _inputRouter.WasKeyPressed(key);
        var confirm = KeyPressed(Keys.Enter) || KeyPressed(Keys.Space) ||
            (pressedButtons & MatchControllerButtons.A) != 0;
        var cancel = KeyPressed(Keys.Escape) || KeyPressed(Keys.Back) ||
            (pressedButtons & MatchControllerButtons.B) != 0;
        var direction = 0;
        if (KeyPressed(Keys.Up) || (pressedButtons & MatchControllerButtons.DPadUp) != 0) direction = -1;
        if (KeyPressed(Keys.Down) || (pressedButtons & MatchControllerButtons.DPadDown) != 0) direction = 1;
        var horizontal = 0;
        if (KeyPressed(Keys.Left) || (pressedButtons & MatchControllerButtons.DPadLeft) != 0) horizontal = -1;
        if (KeyPressed(Keys.Right) || (pressedButtons & MatchControllerButtons.DPadRight) != 0) horizontal = 1;

        if (cancel)
        {
            if (_practiceSetupOpen)
            {
                _practiceSetupOpen = false;
                if (_returnToActiveMatchOnMenuCancel)
                {
                    _titleMenuOpen = false;
                    _simulationPaused = false;
                    _returnToActiveMatchOnMenuCancel = false;
                }
                else
                {
                    _titleMenuOpen = true;
                    _titleMenuSelectionIndex = 1;
                }
            }
            else if (_returnToActiveMatchOnMenuCancel)
            {
                _titleMenuOpen = false;
                _simulationPaused = false;
                _returnToActiveMatchOnMenuCancel = false;
            }
            else
            {
                Exit();
            }
        }
        else if (_practiceSetupOpen)
        {
            if (direction != 0)
                _practiceMenuSelectionIndex = Math.Clamp(_practiceMenuSelectionIndex + direction, 0, 5);
            if (horizontal != 0 && _practiceMenuSelectionIndex < 5)
                _practiceNetsSetup = PracticeNetsSetup.Adjust(_practiceNetsSetup, _practiceMenuSelectionIndex, horizontal);
            if (confirm)
            {
                if (_practiceMenuSelectionIndex == 5)
                    StartPracticeNets();
                else
                    _practiceMenuSelectionIndex = Math.Min(5, _practiceMenuSelectionIndex + 1);
            }
        }
        else if (_titleMenuOpen)
        {
            if (direction != 0)
                _titleMenuSelectionIndex = Math.Clamp(_titleMenuSelectionIndex + direction, 0, 1);
            if (confirm)
            {
                if (_titleMenuSelectionIndex == 0)
                {
                    _titleMenuOpen = false;
                    _practiceNetsMode = false;
                    _practiceTargetCenter = null;
                    _simulationPaused = false;
                    _returnToActiveMatchOnMenuCancel = false;
                    _selectedOversPerInnings = _gameSettings.OversPerInnings;
                    StartNewMatch();
                }
                else
                {
                    _titleMenuOpen = false;
                    _practiceSetupOpen = true;
                    _practiceMenuSelectionIndex = 0;
                }
            }
        }

        _inputRouter.EndKeyboardFrame();
        _frameTimeMilliseconds = 0d;
        _updateMilliseconds = 0d;
        return true;
    }

    private void OpenModeMenuFromMatch()
    {
        _returnToActiveMatchOnMenuCancel = true;
        _titleMenuOpen = true;
        _titleMenuSelectionIndex = 0;
        _simulationPaused = true;
    }

    private void StartPracticeNets()
    {
        _titleMenuOpen = false;
        _practiceSetupOpen = false;
        _returnToActiveMatchOnMenuCancel = false;
        _practiceNetsMode = true;
        _practiceNetsPresenter = new PracticeNetsPresenter();
        _practiceRepeatDelaySeconds = 0f;
        _practiceTargetHitThisDelivery = false;
        _bowlingAimOffsetX = _practiceNetsSetup.LineOffsetMeters;
        _bowlingAimOffsetZ = _practiceNetsSetup.LengthOffsetMeters;

        if (_practiceNetsSetup.Drill == PracticeNetDrill.BattingTutorial)
        {
            _selectedOversPerInnings = 20;
            StartNewMatch();
            return;
        }

        _selectedOversPerInnings = 1;
        StartPracticeBowlingInnings();
    }

    private void StartPracticeBowlingInnings()
    {
        StartNewMatch();
        for (var ball = 0; ball < OverScoreboard.BallsPerOver; ball++)
        {
            CurrentDelivery.ResolveBoundary(clearedInTheAir: false, currentRunCrossed: false);
            _matchController.CompleteDelivery();
            if (ball + 1 < OverScoreboard.BallsPerOver)
                BeginDelivery();
        }

        _matchController.StartNextInnings();
        BeginDelivery();
        StartPracticeBowlingDelivery();
    }

    private void StartPracticeBowlingDelivery()
    {
        if (!HasHumanBowlingControls) return;
        RefreshBowlingOptions();
        var preferred = _bowlingOptions.Select((delivery, index) => (delivery, index))
            .FirstOrDefault(item => item.delivery.Style == _practiceNetsSetup.BowlerStyle);
        if (preferred.delivery is null) return;
        var targetDelivery = PracticeNetsSetup.CreateDelivery(
            _practiceNetsSetup, _bowlingCatalogue, _deliveryPresets[0]);
        var targetBounce = BowlingAimModel.FindFirstBounce(targetDelivery)
            ?? throw new InvalidOperationException("The selected practice delivery does not reach a pitch bounce.");
        _practiceTargetCenter = ToXna(targetBounce.Position);
        _bowlingTargetMarkerPosition = _practiceTargetCenter;
        SelectHumanBowlingDelivery(preferred.index);
        _bowlingTargetMarkerPosition = _practiceTargetCenter;
    }

    private void UpdatePracticeNets(float elapsedSeconds)
    {
        if (!_practiceNetsMode || _simulationPaused || !_deliveryComplete ||
            _practiceNetsPresenter.BowlingTargetComplete || _replayPlayback is not null ||
            _pendingReplayEvent is not null || _queuedReplayEvents.Count > 0)
            return;

        _practiceRepeatDelaySeconds = MathF.Max(0f, _practiceRepeatDelaySeconds - elapsedSeconds);
        if (_practiceRepeatDelaySeconds > 0f) return;

        if (_match.IsInningsComplete)
        {
            if (_practiceNetsSetup.Drill == PracticeNetDrill.BowlingTarget)
                StartPracticeBowlingInnings();
            else
                StartNewMatch();
            return;
        }

        BeginDelivery();
        if (_practiceNetsSetup.Drill == PracticeNetDrill.BowlingTarget)
            StartPracticeBowlingDelivery();
    }

    private void DrawPracticeModeMenu()
    {
        var viewport = GraphicsDevice.Viewport;
        var panel = new Rectangle(viewport.Width / 2 - 310, viewport.Height / 2 - 140, 620, 280);
        var background = _gameSettings.HighContrast ? Color.Black : new Color(4, 10, 15, 242);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);
        _spriteBatch.Draw(_feedbackMapPixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(0, 0, 0, 132));
        DrawHudPanel(panel, background, _gameSettings.HighContrast ? Color.Yellow : new Color(242, 206, 116));
        DrawOverlayText("SUPER CRICKET", new Vector2(panel.X + 32, panel.Y + 24), Color.Yellow, 1.2f);
        DrawMenuLine(panel, 0, "SHORT MATCH", _titleMenuSelectionIndex == 0);
        DrawMenuLine(panel, 1, "PRACTICE NETS", _titleMenuSelectionIndex == 1);
        DrawOverlayText("UP / DOWN choose     ENTER select     ESC return",
            new Vector2(panel.X + 32, panel.Bottom - 42), Color.White, 0.72f);
        _spriteBatch.End();
    }

    private void DrawPracticeSetupMenu()
    {
        var viewport = GraphicsDevice.Viewport;
        var panel = new Rectangle(viewport.Width / 2 - 365, viewport.Height / 2 - 235, 730, 470);
        var background = _gameSettings.HighContrast ? Color.Black : new Color(4, 10, 15, 244);
        var options = new[]
        {
            $"DRILL       {_practiceNetsSetup.Drill switch { PracticeNetDrill.BattingTutorial => "Batting tutorial", _ => "Bowling target" }}",
            $"BOWLER      {_practiceNetsSetup.BowlerStyle}",
            $"LINE        {_practiceNetsSetup.Line}",
            $"LENGTH      {_practiceNetsSetup.Length}",
            $"PACE        {_practiceNetsSetup.Pace}",
            "START NETS"
        };
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);
        _spriteBatch.Draw(_feedbackMapPixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(0, 0, 0, 132));
        DrawHudPanel(panel, background, _gameSettings.HighContrast ? Color.Yellow : new Color(88, 219, 240));
        DrawOverlayText("PRACTICE NETS  /  SETUP", new Vector2(panel.X + 30, panel.Y + 24), Color.Yellow, 1.05f);
        for (var index = 0; index < options.Length; index++)
            DrawMenuLine(panel, index, options[index], _practiceMenuSelectionIndex == index);
        DrawOverlayText("LEFT / RIGHT change     UP / DOWN move     ENTER next / start     ESC back",
            new Vector2(panel.X + 30, panel.Bottom - 42), Color.White, 0.68f);
        _spriteBatch.End();
    }

    private void DrawMenuLine(Rectangle panel, int index, string text, bool selected)
    {
        var color = selected ? Color.Yellow : Color.White;
        DrawOverlayText((selected ? ">  " : "   ") + text,
            new Vector2(panel.X + 36, panel.Y + 92 + index * 43), color, 0.9f);
    }

    private void DrawPracticeNetsPanel()
    {
        if (!_practiceNetsMode) return;
        var viewport = GraphicsDevice.Viewport;
        var panel = new Rectangle(18, viewport.Height - 250, Math.Min(630, viewport.Width - 36), 232);
        var background = _gameSettings.HighContrast ? Color.Black : new Color(4, 10, 15, 228);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);
        DrawHudPanel(panel, background, _gameSettings.HighContrast ? Color.Yellow : new Color(242, 206, 116));

        string prompt;
        if (_practiceNetsSetup.Drill == PracticeNetDrill.BowlingTarget)
        {
            prompt = _practiceNetsPresenter.BowlingTargetComplete
                ? "TARGET COMPLETE: five deliveries landed in the ring."
                : $"BOWLING TARGET  {_practiceNetsPresenter.BowlingTargetHits}/5  /  aim for the ring; pace goal {_practiceNetsSetup.Pace}.";
        }
        else
        {
            prompt = _practiceNetsPresenter.TutorialComplete
                ? "Tutorial complete. Keep batting to study your last ten timing zones."
                : _practiceNetsPresenter.TutorialPrompt;
        }

        DrawOverlayText("PRACTICE NETS", new Vector2(panel.X + 14, panel.Y + 10), Color.Yellow, 0.78f);
        foreach (var line in MatchHudPresenter.WrapTextLines([prompt], panel.Width - 34, 0.68f,
                     text => _debugFont.MeasureString(text).X))
            DrawOverlayText(line, new Vector2(panel.X + 14, panel.Y + 34), Color.White, 0.68f);
        DrawOverlayText("LAST 10 BALLS", new Vector2(panel.X + 14, panel.Y + 71),
            _gameSettings.HighContrast ? Color.Yellow : new Color(88, 219, 240), 0.62f);
        var recent = _practiceNetsPresenter.RecentBalls;
        for (var index = 0; index < recent.Count; index++)
        {
            var column = index / 5;
            var row = index % 5;
            var label = PracticeNetsPresenter.FormatRecentBall(recent[index]);
            DrawOverlayText(label,
                new Vector2(panel.X + 14 + column * 142, panel.Y + 94 + row * 23),
                Color.White, 0.58f);
        }

        var histogram = _practiceNetsPresenter.BuildZoneHistogram();
        var zoneLabels = new[] { TimingZone.Ideal, TimingZone.Early, TimingZone.Late, TimingZone.Start,
            TimingZone.End, TimingZone.TooEarly, TimingZone.TooLate };
        DrawOverlayText("ZONE HISTOGRAM", new Vector2(panel.X + 310, panel.Y + 71),
            _gameSettings.HighContrast ? Color.Yellow : new Color(88, 219, 240), 0.62f);
        for (var index = 0; index < zoneLabels.Length; index++)
        {
            var zone = zoneLabels[index];
            var label = $"{zone,-8} {histogram[zone]}";
            DrawOverlayText(label, new Vector2(panel.X + 310, panel.Y + 94 + index * 19),
                zone == TimingZone.Ideal ? Color.LightGreen : Color.White, 0.55f);
        }
        _spriteBatch.End();
    }
}
