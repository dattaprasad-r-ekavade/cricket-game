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
        void Reset(int deliveryPresetIndex = 0)
        {
            _previousKeyboard = default;
            _nextDeliveryPresetIndex = deliveryPresetIndex;
            _selectedOversPerInnings = 1;
            _match.Reset(_selectedOversPerInnings);
            BeginDelivery();
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
        Require(_deliveryComplete && _dismissal == DismissalKind.RunOut && _match.Wickets == 1,
            "resumed pickup/throw did not finish as a run-out");
        Console.WriteLine("PASS: pickup/throw pause and authored throw completion.");

        Reset();
        _battedBall = true;
        CurrentDelivery.RecordCompletedRun();
        _isRunning = true;
        _runElapsed = 0.15f;
        ResolveFieldingContact(new FieldingContact(0, _fieldingSide.Positions[0], FieldingContactKind.GroundPickup));
        for (var step = 0; step < 180 && !_deliveryComplete; step++) Tick(1f / 120f);
        Require(_deliveryComplete && _dismissal == DismissalKind.RunOut && _match.Runs == 1 &&
            _match.Wickets == 1 && _match.LegalBalls == 1,
            "the authored throw failed to run out a batter still short of the far crease after the turn");
        Console.WriteLine("PASS: pickup and return throw resolve a run-out while the batter is turning.");

        Reset();
        for (var delivery = 0; delivery < 6; delivery++)
        {
            for (var step = 0; step < 600 && !_deliveryComplete; step++) Tick(1f / 120f);
            Require(_deliveryComplete && _dismissal == DismissalKind.Bowled, "unplayed delivery failed to resolve bowled");
            if (delivery < 5) BeginDelivery();
        }
        Require(_match.IsOverComplete && _match.LegalBalls == 6 && _match.Wickets == 6,
            "six legal deliveries did not complete the over");
        Console.WriteLine("PASS: six bowled deliveries complete an over.");

        Reset();
        for (var ball = 0; ball < 6; ball++)
        {
            for (var step = 0; step < 600 && !_deliveryComplete; step++) Tick(1f / 120f);
            Require(_deliveryComplete, "the live first innings did not resolve its delivery");
            if (ball < 5)
            {
                Tick(0f, Keys.N);
                Tick(0f);
            }
        }
        Require(_match.IsInningsComplete && !_match.IsMatchComplete && _match.FirstInnings is { Runs: 0, LegalBalls: 6 },
            "the first live innings did not stop at its over limit");
        Tick(0f, Keys.N);
        Require(_match.InningsNumber == 2 && _match.BattingTeamName == "Highland XI" && _match.Target == 1 && !_deliveryComplete,
            "N did not start the target chase as the second innings");
        Tick(0f);
        for (var ball = 0; ball < 6; ball++)
        {
            for (var step = 0; step < 600 && !_deliveryComplete; step++) Tick(1f / 120f);
            Require(_deliveryComplete, "the live second innings did not resolve its delivery");
            if (ball < 5)
            {
                Tick(0f, Keys.N);
                Tick(0f);
            }
        }
        Require(_match.IsMatchComplete && _match.ResultText == "Match tied",
            "the second live innings did not show a tied result after its over");
        Tick(0f, Keys.O);
        Require(!_match.IsMatchComplete && _match.OversPerInnings == 2 && _match.InningsNumber == 1 && _match.Runs == 0,
            "O did not restart the match with the next overs selection");
        Console.WriteLine("PASS: N starts the second innings, the chase resolves, and O changes overs after the result.");

        foreach (var presetIndex in new[] { 1, 2 })
        {
            Reset(presetIndex);
            for (var step = 0; step < 600 && !_deliveryComplete; step++) Tick(1f / 120f);
            Require(_deliveryComplete && _match.Runs == 1 && _match.LegalBalls == 0 && _match.Wickets == 0,
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
                Require(_releaseMarkerPosition is not null && _contactMarkerPosition is not null &&
                    _sweetSpotMarkerPosition is not null && _contactTimeSeconds is >= 0f &&
                    _contactQuality is >= 0f and <= 1f && _contactSweetSpotOffset is not null,
                    $"shot {key} failed to record release/contact/sweet-spot marker data at {frameRate} fps");
            }
        }
        Console.WriteLine("PASS: defence, drive, and loft contact and resolve at 30/60/120 fps.");

        foreach (var frameRate in new[] { 30, 60, 120 })
        {
            Reset(1);
            for (var step = 0; step < BatterFootwork.MaximumSteps; step++)
            {
                Tick(0f, Keys.Q);
                Tick(0f);
            }
            Tick(0.6f);
            var elapsed = 1f / frameRate;
            for (var step = 0; step < frameRate * 5 && !_bowlerReleased; step++) Tick(elapsed);
            for (var step = 0; step < frameRate && _ballFlight.CurrentFrame.TimeSeconds < 0.35f; step++) Tick(elapsed);
            Tick(elapsed, Keys.S);
            for (var step = 0; step < frameRate * 6 && !_deliveryComplete; step++) Tick(elapsed);
            Require(_battedBall && _deliveryComplete &&
                MathF.Abs(_batterFootworkOffsetX - BatterFootwork.MaximumOffsetMeters) < 0.001f,
                $"off-side footwork failed to reach the wide delivery at {frameRate} fps");
            Require(_releaseMarkerPosition is not null && _contactMarkerPosition is not null &&
                _sweetSpotMarkerPosition is not null && _contactTimeSeconds is >= 0f &&
                _contactQuality is >= 0f and <= 1f && _contactSweetSpotOffset is not null,
                $"wide shot failed to record release/contact/sweet-spot markers at {frameRate} fps");
        }
        Console.WriteLine("PASS: Q steps into the wide line and the drive makes contact at 30/60/120 fps.");

        foreach (var boundaryRuns in new[] { 4, 6 })
        {
            Reset();
            _battedBall = true;
            CurrentDelivery.RecordCompletedRun();
            ResolveBoundaryCrossing(new BoundaryCrossing(
                1f,
                new NumericsVector3(_deliveryPreset.FieldBoundaryRadiusMeters, 3f, 0f),
                boundaryRuns == 6));
            Require(_match.Runs == boundaryRuns && _match.Striker == 1 && _match.NonStriker == 2,
                "boundary incorrectly added an earlier run or changed strike");
        }
        Console.WriteLine("PASS: four/six scoring replaces earlier completed runs and restores ends.");

        Reset();
        _battedBall = true;
        for (var run = 0; run < 4; run++)
            CurrentDelivery.RecordCompletedRun();
        _isRunning = true;
        _runElapsed = _runDurationSeconds * 0.6f;
        ResolveBoundaryCrossing(new BoundaryCrossing(
            1f,
            new NumericsVector3(_deliveryPreset.FieldBoundaryRadiusMeters, 0.1f, 0f),
            false));
        Require(_match.Runs == 5 && _match.Striker == 2,
            "boundary failed to preserve the greater running allowance including a crossed run");
        Console.WriteLine("PASS: running allowance greater than the boundary allowance.");

        Reset();
        _battedBall = true;
        CurrentDelivery.RecordCompletedRun();
        ResolveFieldingContact(new FieldingContact(0, _fieldingSide.Positions[0], FieldingContactKind.Catch));
        Require(_match.Runs == 0 && _match.Wickets == 1 && _match.Striker == 3 && _match.NonStriker == 2,
            "catch retained completed runs or replaced the wrong batter");
        Console.WriteLine("PASS: a legal catch voids completed runs and replaces the striker.");

        Reset(2);
        _battedBall = true;
        CurrentDelivery.RecordCompletedRun();
        ResolveFieldingContact(new FieldingContact(0, _fieldingSide.Positions[0], FieldingContactKind.Catch));
        Require(_match.Runs == 2 && _match.Wickets == 0 && _match.LegalBalls == 0 && _match.Striker == 2,
            "no-ball catch incorrectly voided runs or dismissed the striker");
        Console.WriteLine("PASS: no-ball catch retains completed runs plus the penalty.");
        Console.WriteLine("Gameplay review checks passed.");
    }
}
