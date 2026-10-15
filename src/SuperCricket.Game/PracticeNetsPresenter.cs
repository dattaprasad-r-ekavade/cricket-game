using System;
using System.Collections.Generic;
using System.Linq;
using SuperCricket.Content;
using SuperCricket.Content.Bowling;
using SuperCricket.Simulation;
using SuperCricket.Simulation.Batting;
using SuperCricket.Simulation.Bowling;

namespace SuperCricket.Game;

internal enum PracticeNetDrill
{
    BattingTutorial,
    BowlingTarget
}

internal enum PracticeNetLine
{
    OutsideOff,
    Stumps,
    LegSide
}

internal enum PracticeNetLength
{
    Short,
    Good,
    Full,
    Yorker
}

internal enum PracticeNetPace
{
    Slow,
    Normal,
    Fast
}

internal readonly record struct PracticeNetsSetup(
    PracticeNetDrill Drill,
    BowlerStyle BowlerStyle,
    PracticeNetLine Line,
    PracticeNetLength Length,
    PracticeNetPace Pace)
{
    public static PracticeNetsSetup Default => new(
        PracticeNetDrill.BattingTutorial,
        BowlerStyle.Pace,
        PracticeNetLine.Stumps,
        PracticeNetLength.Good,
        PracticeNetPace.Normal);

    public float LineOffsetMeters => Line switch
    {
        PracticeNetLine.OutsideOff => -0.65f,
        PracticeNetLine.Stumps => 0f,
        _ => 0.65f
    };

    public float SpeedFraction => Pace switch
    {
        PracticeNetPace.Slow => 0.22f,
        PracticeNetPace.Normal => 0.55f,
        _ => 0.9f
    };

    public float LengthOffsetMeters => Length switch
    {
        PracticeNetLength.Short => -0.75f,
        PracticeNetLength.Good => 0f,
        PracticeNetLength.Full => 0.55f,
        _ => 1.45f
    };

    public static DeliveryPreset CreateDelivery(
        PracticeNetsSetup setup,
        DeliveryCatalogue catalogue,
        DeliveryPreset stockDelivery)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(stockDelivery);
        var definition = catalogue.Deliveries.FirstOrDefault(delivery => delivery.Style == setup.BowlerStyle)
            ?? throw new InvalidOperationException($"No {setup.BowlerStyle} delivery is available in the practice catalogue.");
        var speed = definition.MinSpeedKph + (definition.MaxSpeedKph - definition.MinSpeedKph) * setup.SpeedFraction;
        return PaceMeterModel.CreateDelivery(
            definition,
            stockDelivery,
            setup.LineOffsetMeters,
            setup.LengthOffsetMeters,
            speed,
            PitchType.Normal);
    }

    public static PracticeNetsSetup Adjust(PracticeNetsSetup setup, int row, int direction)
    {
        if (direction == 0) return setup;
        static T Cycle<T>(T value, int change) where T : struct, Enum
        {
            var choices = Enum.GetValues<T>();
            var index = Array.IndexOf(choices, value);
            return choices[(index + change + choices.Length) % choices.Length];
        }

        return row switch
        {
            0 => setup with { Drill = Cycle(setup.Drill, direction) },
            1 => setup with { BowlerStyle = Cycle(setup.BowlerStyle, direction) },
            2 => setup with { Line = Cycle(setup.Line, direction) },
            3 => setup with { Length = Cycle(setup.Length, direction) },
            4 => setup with { Pace = Cycle(setup.Pace, direction) },
            _ => setup
        };
    }
}

internal readonly record struct PracticeNetBall(int Index, TimingZone? Zone, bool TargetHit);

/// <summary>Tracks the guided batting steps and the rolling practice statistics panel.</summary>
internal sealed class PracticeNetsPresenter
{
    public const int IdealHitsPerTutorialStep = 3;
    public const int BowlingTargetHitCount = 5;
    public const int RecentBallCount = 10;
    private readonly Queue<PracticeNetBall> _recentBalls = new();

    public int TutorialStep { get; private set; }
    public int TutorialStepHits { get; private set; }
    public int BowlingTargetHits { get; private set; }
    public int BallsRecorded { get; private set; }
    public bool TutorialComplete => TutorialStep >= 3;
    public bool BowlingTargetComplete => BowlingTargetHits >= BowlingTargetHitCount;
    public IReadOnlyList<PracticeNetBall> RecentBalls => Array.AsReadOnly(_recentBalls.ToArray());

    public string TutorialPrompt => TutorialStep switch
    {
        0 => "Step 1 / 3: press S as the ball reaches the yellow band. Land three IDEAL hits.",
        1 => "Step 2 / 3: add a direction, then press S. Land three IDEAL hits.",
        2 => "Step 3 / 3: hold Shift to loft. Land three IDEAL hits.",
        _ => string.Empty
    };

    public void RecordBattingBall(TimingZone? zone, bool madeContact, bool addedDirection, bool lofted)
    {
        AddBall(zone, targetHit: false);
        if (TutorialComplete) return;

        var requiresDirection = TutorialStep == 1;
        var requiresLoft = TutorialStep == 2;
        var qualifies = madeContact && zone == TimingZone.Ideal &&
            (!requiresDirection || addedDirection) && (!requiresLoft || lofted);
        if (!qualifies) return;

        TutorialStepHits++;
        if (TutorialStepHits >= IdealHitsPerTutorialStep)
        {
            TutorialStep++;
            TutorialStepHits = 0;
        }
    }

    public void RecordBowlingBall(TimingZone? zone, bool targetHit)
    {
        AddBall(zone, targetHit);
        if (targetHit && !BowlingTargetComplete)
            BowlingTargetHits++;
    }

    public IReadOnlyDictionary<TimingZone, int> BuildZoneHistogram()
    {
        var histogram = Enum.GetValues<TimingZone>().ToDictionary(zone => zone, _ => 0);
        foreach (var ball in _recentBalls)
            if (ball.Zone is { } zone)
                histogram[zone]++;
        return histogram;
    }

    public static string FormatRecentBall(PracticeNetBall ball) =>
        ball.Zone is { } zone ? $"{ball.Index,2}: {zone,-8}" : $"{ball.Index,2}: NO TIMING";

    private void AddBall(TimingZone? zone, bool targetHit)
    {
        BallsRecorded++;
        _recentBalls.Enqueue(new PracticeNetBall(BallsRecorded, zone, targetHit));
        while (_recentBalls.Count > RecentBallCount)
            _recentBalls.Dequeue();
    }
}
