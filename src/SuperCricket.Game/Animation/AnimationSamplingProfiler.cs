using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using SuperCricket.Content;

namespace SuperCricket.Game.Animation;

/// <summary>Measures CPU skin-palette sampling without creating the MonoGame window.</summary>
internal static class AnimationSamplingProfiler
{
    private const float FrameSeconds = 1f / 60f;
    private const float BlendSeconds = 0.35f;

    public static void Run(int frameCount)
    {
        if (frameCount is < 1 or > 36_000)
            throw new ArgumentOutOfRangeException(nameof(frameCount));

        Console.WriteLine("Humanoid animation sampling profile (headless; no game window or GPU work)");
        Console.WriteLine($"Frames per role: {frameCount.ToString(CultureInfo.InvariantCulture)} at 60 Hz");
        Console.WriteLine($"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}; {Environment.ProcessorCount} logical processors");

        ProfileRole("batter", "practice-batter-humanoid.glb", "practice-stance", "front-foot-drive", frameCount);
        ProfileRole("bowler", "practice-bowler-humanoid.glb", "bowling-run-up", "overarm-delivery", frameCount);
    }

    private static void ProfileRole(string role, string assetFile, string firstClip, string secondClip, int frameCount)
    {
        var assetPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Characters", assetFile);
        var asset = PlayerAsset.Load(assetPath);
        var animator = new PlayerAnimator(asset);
        var frameTimesMilliseconds = new double[frameCount];
        var nextClip = firstClip;
        var outputChecksum = 0f;

        for (var frame = 0; frame < 120; frame++)
            Advance(animator, ref nextClip, firstClip, secondClip, frame, ref outputChecksum);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var totalTicks = 0L;
        for (var frame = 0; frame < frameCount; frame++)
        {
            var started = Stopwatch.GetTimestamp();
            Advance(animator, ref nextClip, firstClip, secondClip, frame, ref outputChecksum);
            var elapsedTicks = Stopwatch.GetTimestamp() - started;
            totalTicks += elapsedTicks;
            frameTimesMilliseconds[frame] = elapsedTicks * 1000d / Stopwatch.Frequency;
        }
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Array.Sort(frameTimesMilliseconds);
        var p50 = Percentile(frameTimesMilliseconds, 0.50);
        var p95 = Percentile(frameTimesMilliseconds, 0.95);
        var mean = totalTicks * 1000d / Stopwatch.Frequency / frameCount;
        var allocationsPerFrame = allocatedBytes / (double)frameCount;
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "{0}: {1} bones, clips {2}/{3}; mean {4:F4} ms, p50 {5:F4} ms, p95 {6:F4} ms; {7:F1} B/frame",
            role, asset.Bones.Count, firstClip, secondClip, mean, p50, p95, allocationsPerFrame));

        GC.KeepAlive(outputChecksum);
    }

    private static void Advance(
        PlayerAnimator animator,
        ref string nextClip,
        string firstClip,
        string secondClip,
        int frame,
        ref float outputChecksum)
    {
        if (!animator.IsTransitioning)
        {
            var playedClip = nextClip;
            animator.Play(playedClip, BlendSeconds);
            nextClip = playedClip == firstClip ? secondClip : firstClip;
        }

        animator.Update(FrameSeconds);
        var skinMatrices = animator.GetSkinMatrices();
        outputChecksum += skinMatrices[frame % skinMatrices.Length].M11;
    }

    private static double Percentile(double[] sortedSamples, double percentile)
    {
        var index = Math.Clamp((int)Math.Ceiling(sortedSamples.Length * percentile) - 1, 0, sortedSamples.Length - 1);
        return sortedSamples[index];
    }
}
