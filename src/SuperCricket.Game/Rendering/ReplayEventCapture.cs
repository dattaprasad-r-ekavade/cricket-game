using SuperCricket.Content.Replays;

namespace SuperCricket.Game.Rendering;

internal sealed record ReplayEventCapture(
    string EventType,
    double TimestampSeconds,
    float? ProjectedImpactX = null,
    float? ProjectedImpactHeightMeters = null,
    float? CreaseDistanceMeters = null,
    int InningsNumber = 0,
    string? Caption = null);

internal sealed record ReplayBookmark(
    ReplayEventScript Script,
    double EventTimestampSeconds,
    ReplayFrame[] Frames,
    string Caption,
    float? ProjectedImpactX,
    float? ProjectedImpactHeightMeters,
    float? CreaseDistanceMeters);
