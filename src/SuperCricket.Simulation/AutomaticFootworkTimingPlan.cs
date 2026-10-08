using SuperCricket.Content;

namespace SuperCricket.Simulation;

/// <summary>Timing and the smallest reachable lateral stance measured for one delivery.</summary>
public readonly record struct AutomaticFootworkTimingPlan(
    float FootworkOffsetMeters,
    BattingTimingDeliveryProfile TimingProfile);
