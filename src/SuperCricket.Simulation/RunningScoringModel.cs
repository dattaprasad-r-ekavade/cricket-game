namespace SuperCricket.Simulation;

/// <summary>Scoring at dead ball for the current equal-speed, straight-line running model.</summary>
public static class RunningScoringModel
{
    public static bool HasCrossed(float elapsedInCurrentRunSeconds, float runDurationSeconds)
    {
        ValidateTime(elapsedInCurrentRunSeconds, runDurationSeconds);
        return elapsedInCurrentRunSeconds >= runDurationSeconds * 0.5f;
    }

    /// <summary>Completed runs plus a crossed run in progress, capped by the runs actually attempted.</summary>
    public static int CountRunsAtDeadBall(float elapsedSeconds, float runDurationSeconds, int maximumRuns)
    {
        ValidateTime(elapsedSeconds, runDurationSeconds);
        if (maximumRuns < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumRuns));
        var completedRuns = (int)Math.Min(Math.Floor((double)elapsedSeconds / runDurationSeconds), maximumRuns);
        if (completedRuns >= maximumRuns)
            return maximumRuns;
        var currentRunElapsed = elapsedSeconds - completedRuns * runDurationSeconds;
        return completedRuns + (HasCrossed(currentRunElapsed, runDurationSeconds) ? 1 : 0);
    }

    private static void ValidateTime(float elapsedSeconds, float runDurationSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (!float.IsFinite(runDurationSeconds) || runDurationSeconds <= 0f)
            throw new ArgumentOutOfRangeException(nameof(runDurationSeconds));
    }
}
