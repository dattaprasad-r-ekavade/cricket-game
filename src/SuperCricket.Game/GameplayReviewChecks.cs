using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SuperCricket.Simulation;
using NumericsVector3 = System.Numerics.Vector3;

namespace SuperCricket.Game;

public partial class Game1
{
    // Runs the actual match update with scripted keyboard state after loading real content.
    private void RunGameplayReviewChecks()
    {
        void Tick(float seconds, params Keys[] keys) =>
            UpdateMatch(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)), new KeyboardState(keys));
        void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"Gameplay review failed: {message}");
        }
        void Reset()
        {
            _previousKeyboard = default;
            _nextDeliveryPresetIndex = 0;
            StartNewOver();
        }

        Reset();
        Tick(0.1f);
        Tick(0f, Keys.P);
        var batterTime = _playerAnimator.CurrentTimeSeconds;
        var bowlerTime = _bowlerRunUpElapsed;
        var fielderTime = _fielderAnimators[0].CurrentTimeSeconds;
        var ballFrame = _ballFlight.CurrentFrame;
        Tick(1f);
        Require(_playerAnimator.CurrentTimeSeconds == batterTime && _bowlerRunUpElapsed == bowlerTime &&
            _fielderAnimators[0].CurrentTimeSeconds == fielderTime && _ballFlight.CurrentFrame == ballFrame,
            "pause advanced an animation, ball, or run-up");
        Tick(0.1f, Keys.S, Keys.Enter);
        Require(_chosenShot is null && !_runRequestedPending, "pause accepted shot/run input");
        Tick(0f);
        Tick(0.01f, Keys.P);
        Require(_bowlerRunUpElapsed > bowlerTime, "resume did not advance run-up");
        Console.WriteLine("PASS: pause/resume freezes match clocks and ignores gameplay input.");

        Reset();
        _battedBall = true;
        StartRun();
        ResolveFieldingContact(new FieldingContact(0, _fieldingSide.Positions[0], FieldingContactKind.GroundPickup));
        Tick(0f, Keys.P);
        var pickupTime = _fielderAnimators[0].CurrentTimeSeconds;
        Tick(0.75f);
        Require(_runElapsed == 0f && _fielderAnimators[0].CurrentTimeSeconds == pickupTime &&
            _fielderSequencePhase == FielderSequencePhase.Pickup && !_deliveryComplete,
            "pause advanced running or pickup/throw sequence");
        Tick(0f, Keys.P);
        for (var step = 0; step < 70 && _fielderSequencePhase != FielderSequencePhase.Throw; step++) Tick(1f / 120f);
        Require(_fielderSequencePhase == FielderSequencePhase.Throw, "pickup did not start the throw action");
        Tick(0f, Keys.P);
        var throwTime = _fielderAnimators[0].CurrentTimeSeconds;
        var runTime = _runElapsed;
        Tick(0.75f);
        Require(_fielderAnimators[0].CurrentTimeSeconds == throwTime && _runElapsed == runTime && !_deliveryComplete,
            "paused throw advanced or resolved delivery");
        Tick(0f, Keys.P);
        for (var step = 0; step < 150 && !_deliveryComplete; step++) Tick(1f / 120f);
        Require(_deliveryComplete && _dismissal == DismissalKind.RunOut && _scoreboard.Wickets == 1,
            "resumed pickup/throw did not finish as a run-out");
        Console.WriteLine("PASS: pickup/throw pause and authored throw completion.");

        Reset();
        for (var delivery = 0; delivery < 6; delivery++)
        {
            for (var step = 0; step < 600 && !_deliveryComplete; step++) Tick(1f / 120f);
            Require(_deliveryComplete && _dismissal == DismissalKind.Bowled, "unplayed delivery failed to resolve bowled");
            if (delivery < 5) BeginDelivery();
        }
        Require(_scoreboard.IsOverComplete && _scoreboard.LegalBalls == 6 && _scoreboard.Wickets == 6,
            "six legal deliveries did not complete the over");
        Console.WriteLine("PASS: six bowled deliveries complete an over.");

        foreach (var presetIndex in new[] { 1, 2 })
        {
            Reset();
            _nextDeliveryPresetIndex = presetIndex;
            BeginDelivery();
            for (var step = 0; step < 600 && !_deliveryComplete; step++) Tick(1f / 120f);
            Require(_deliveryComplete && _scoreboard.Runs == 1 && _scoreboard.LegalBalls == 0 && _scoreboard.Wickets == 0,
                "wide/no-ball did not award one extra without a legal ball or bowled wicket");
        }
        Console.WriteLine("PASS: wide and no-ball gameplay scoring.");

        foreach (var frameRate in new[] { 30, 60, 120 })
        {
            foreach (var (key, delay) in new[] { (Keys.A, 0.2f), (Keys.S, 0.25f), (Keys.D, 0.25f) })
            {
                Reset();
                var elapsed = 1f / frameRate;
                for (var step = 0; step < frameRate * 5 && !_bowlerReleased; step++) Tick(elapsed);
                for (var step = 0; step < frameRate && _ballFlight.CurrentFrame.TimeSeconds < delay; step++) Tick(elapsed);
                Tick(elapsed, key);
                for (var step = 0; step < frameRate * 6 && !_deliveryComplete; step++) Tick(elapsed);
                Require(_battedBall && _deliveryComplete, $"shot {key} failed contact/resolution at {frameRate} fps");
            }
        }
        Console.WriteLine("PASS: defence, drive, and loft contact and resolve at 30/60/120 fps.");

        foreach (var boundaryRuns in new[] { 4, 6 })
        {
            Reset();
            _battedBall = true;
            _batterRuns = 1;
            _completedRuns = 1;
            ResolveSettledBall(new BallFlightFrame(2f, new NumericsVector3(_deliveryPreset.FieldBoundaryRadiusMeters, 3f, 0f),
                NumericsVector3.Zero, boundaryRuns == 6 ? 0 : 1, BallMotionPhase.Settled));
            Require(_scoreboard.Runs == boundaryRuns && _scoreboard.Striker == 1 && _scoreboard.NonStriker == 2,
                "boundary incorrectly added an earlier run or changed strike");
        }
        Console.WriteLine("PASS: four/six scoring replaces earlier completed runs and restores ends.");

        Reset();
        _battedBall = true;
        _batterRuns = 4;
        _completedRuns = 4;
        _isRunning = true;
        _runElapsed = _runDurationSeconds * 0.6f;
        ResolveSettledBall(new BallFlightFrame(2f, new NumericsVector3(_deliveryPreset.FieldBoundaryRadiusMeters, 0.1f, 0f),
            NumericsVector3.Zero, 1, BallMotionPhase.Settled));
        Require(_scoreboard.Runs == 5 && _scoreboard.Striker == 2,
            "boundary failed to preserve the greater running allowance including a crossed run");
        Console.WriteLine("PASS: running allowance greater than the boundary allowance.");

        Reset();
        _battedBall = true;
        _batterRuns = 1;
        _completedRuns = 1;
        ResolveFieldingContact(new FieldingContact(0, _fieldingSide.Positions[0], FieldingContactKind.Catch));
        Require(_scoreboard.Runs == 0 && _scoreboard.Wickets == 1 && _scoreboard.Striker == 3 && _scoreboard.NonStriker == 2,
            "catch retained completed runs or replaced the wrong batter");
        Console.WriteLine("PASS: a legal catch voids completed runs and replaces the striker.");

        Reset();
        _nextDeliveryPresetIndex = 2;
        BeginDelivery();
        _battedBall = true;
        _batterRuns = 1;
        _completedRuns = 1;
        ResolveFieldingContact(new FieldingContact(0, _fieldingSide.Positions[0], FieldingContactKind.Catch));
        Require(_scoreboard.Runs == 2 && _scoreboard.Wickets == 0 && _scoreboard.LegalBalls == 0 && _scoreboard.Striker == 2,
            "no-ball catch incorrectly voided runs or dismissed the striker");
        Console.WriteLine("PASS: no-ball catch retains completed runs plus the penalty.");
        Console.WriteLine("Gameplay review checks passed.");
    }
}
