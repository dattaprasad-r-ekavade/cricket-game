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
}
