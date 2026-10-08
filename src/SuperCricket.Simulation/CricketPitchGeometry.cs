namespace SuperCricket.Simulation;

/// <summary>Shared dimensions of the current pitch and its physical wickets, in metres.</summary>
public static class CricketPitchGeometry
{
    public const float PitchLengthMeters = 20.12f;
    public const float PitchWidthMeters = 3.05f;
    public const float WicketHeightMeters = 0.71f;
    public const float WicketOffsetMeters = PitchLengthMeters / 2f;
    public const float NearWicketZ = -WicketOffsetMeters;
    public const float FarWicketZ = WicketOffsetMeters;
    public const float PoppingCreaseOffsetMeters = 1.22f;
    public const float BatterAnchorOffsetTowardWicketMeters = 0.016f;
    public const float NearPoppingCreaseZ = NearWicketZ + PoppingCreaseOffsetMeters;
    public const float FarPoppingCreaseZ = FarWicketZ - PoppingCreaseOffsetMeters;
    public const float NearBatterAnchorZ = NearPoppingCreaseZ - BatterAnchorOffsetTowardWicketMeters;
    public const float FarBatterAnchorZ = FarPoppingCreaseZ + BatterAnchorOffsetTowardWicketMeters;
}
