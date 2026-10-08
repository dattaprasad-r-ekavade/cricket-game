using System.Numerics;
using Microsoft.Xna.Framework;
using SuperCricket.Content;
using SuperCricket.Game.Animation;
using XnaMatrix = Microsoft.Xna.Framework.Matrix;

namespace SuperCricket.Game.Tests;

public sealed class PlayerAnimatorSamplingTests
{
    private const float FrameSeconds = 1f / 60f;
    private static readonly Lazy<PlayerAsset> BatterAsset = new(() => PlayerAsset.Load(
        Path.Combine(AppContext.BaseDirectory, "Assets", "Characters", "practice-batter-humanoid.glb")));
    private static readonly Lazy<PlayerAsset> BowlerAsset = new(() => PlayerAsset.Load(
        Path.Combine(AppContext.BaseDirectory, "Assets", "Characters", "practice-bowler-humanoid.glb")));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OptimizedSkinPaletteMatchesReferenceSamplingForSteadyAndBlendedClips(bool useBowler)
    {
        var asset = Asset(useBowler);
        var animator = new PlayerAnimator(asset);
        animator.Update(0.27f);

        var steadyClip = asset.Animations.Single(clip =>
            string.Equals(clip.Name, animator.CurrentClipName, StringComparison.OrdinalIgnoreCase));
        AssertSkinPaletteNear(
            BuildReferencePalette(asset, steadyClip, animator.CurrentTimeSeconds),
            animator.GetSkinMatrices(),
            "steady pose");

        var previousClip = steadyClip;
        var previousTime = animator.CurrentTimeSeconds;
        const float transitionSeconds = 0.35f;
        const float elapsedTransitionSeconds = 0.13f;
        var shotClipName = useBowler ? "overarm-delivery" : "front-foot-drive";
        var shotClip = asset.Animations.Single(clip => clip.Name == shotClipName);
        animator.Play(shotClip.Name, transitionSeconds);
        animator.Update(elapsedTransitionSeconds);

        var expectedPreviousTime = (previousTime + elapsedTransitionSeconds) % previousClip.DurationSeconds;
        var expectedBlend = elapsedTransitionSeconds / transitionSeconds;
        AssertSkinPaletteNear(
            BuildReferencePalette(asset, shotClip, elapsedTransitionSeconds,
                previousClip, expectedPreviousTime, expectedBlend),
            animator.GetSkinMatrices(),
            "crossfade");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepeatedSkinPaletteSamplingAllocatesNoManagedMemory(bool useBowler)
    {
        var asset = Asset(useBowler);
        var animator = new PlayerAnimator(asset);
        var firstClip = useBowler ? "overarm-delivery" : "front-foot-drive";
        var secondClip = useBowler ? "bowling-run-up" : "practice-stance";
        var nextClip = firstClip;
        var checksum = 0f;

        for (var frame = 0; frame < 120; frame++)
            Advance(animator, ref nextClip, firstClip, secondClip, asset.Bones.Count, frame, ref checksum);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        for (var frame = 0; frame < 600; frame++)
            Advance(animator, ref nextClip, firstClip, secondClip, asset.Bones.Count, frame, ref checksum);

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert.True(float.IsFinite(checksum));
        Assert.Equal(0L, allocatedBytes);
    }

    private static void Advance(
        PlayerAnimator animator,
        ref string nextClip,
        string firstClip,
        string secondClip,
        int boneCount,
        int frame,
        ref float checksum)
    {
        if (!animator.IsTransitioning)
        {
            var playedClip = nextClip;
            animator.Play(playedClip, 0.35f);
            nextClip = playedClip == firstClip ? secondClip : firstClip;
        }

        animator.Update(FrameSeconds);
        checksum += animator.GetSkinMatrices()[frame % boneCount].M11;
    }

    private static PlayerAsset Asset(bool useBowler) => useBowler ? BowlerAsset.Value : BatterAsset.Value;

    private static XnaMatrix[] BuildReferencePalette(
        PlayerAsset asset,
        PlayerAnimationData currentClip,
        float currentTime,
        PlayerAnimationData? previousClip = null,
        float previousTime = 0f,
        float blend = 1f)
    {
        var inverseBind = asset.Bones.Select(bone =>
        {
            if (!Matrix4x4.Invert(bone.BindPose.ToNumericsMatrix(), out var inverse))
                throw new InvalidOperationException($"Bone '{bone.Name}' has a non-invertible bind pose.");
            return ToXna(inverse);
        }).ToArray();
        var poseMatrices = new XnaMatrix[asset.Bones.Count];
        var skinMatrices = new XnaMatrix[asset.Bones.Count];

        for (var boneIndex = 0; boneIndex < asset.Bones.Count; boneIndex++)
        {
            var pose = SamplePose(currentClip, currentTime, boneIndex);
            if (previousClip is not null)
                pose = TransformData.Interpolate(
                    SamplePose(previousClip, previousTime, boneIndex), pose, blend);

            var poseMatrix = ToXna(pose.ToNumericsMatrix());
            var parentIndex = asset.Bones[boneIndex].ParentIndex;
            if (asset.PoseSpace == "local" && parentIndex >= 0)
                poseMatrix *= poseMatrices[parentIndex];
            poseMatrices[boneIndex] = poseMatrix;
            skinMatrices[boneIndex] = inverseBind[boneIndex] * poseMatrix;
        }

        return skinMatrices;
    }

    private static TransformData SamplePose(PlayerAnimationData clip, float timeSeconds, int boneIndex)
    {
        var samples = clip.Samples;
        var time = Math.Clamp(timeSeconds, 0f, clip.DurationSeconds);
        for (var nextIndex = 1; nextIndex < samples.Count; nextIndex++)
        {
            var next = samples[nextIndex];
            if (time > next.TimeSeconds)
                continue;

            var previous = samples[nextIndex - 1];
            var span = next.TimeSeconds - previous.TimeSeconds;
            var amount = span <= 0f ? 0f : (time - previous.TimeSeconds) / span;
            return TransformData.Interpolate(previous.Bones[boneIndex], next.Bones[boneIndex], amount);
        }

        return samples[^1].Bones[boneIndex];
    }

    private static void AssertSkinPaletteNear(XnaMatrix[] expected, XnaMatrix[] actual, string state)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var boneIndex = 0; boneIndex < actual.Length; boneIndex++)
        {
            var expectedValues = MatrixValues(expected[boneIndex]);
            var actualValues = MatrixValues(actual[boneIndex]);
            for (var valueIndex = 0; valueIndex < expectedValues.Length; valueIndex++)
            {
                Assert.True(float.IsFinite(actualValues[valueIndex]), $"{state}: bone {boneIndex} value {valueIndex} is not finite.");
                Assert.True(MathF.Abs(expectedValues[valueIndex] - actualValues[valueIndex]) <= 0.00001f,
                    $"{state}: bone {boneIndex} value {valueIndex} expected {expectedValues[valueIndex]} but got {actualValues[valueIndex]}.");
            }
        }
    }

    private static float[] MatrixValues(XnaMatrix matrix) =>
    [
        matrix.M11, matrix.M12, matrix.M13, matrix.M14,
        matrix.M21, matrix.M22, matrix.M23, matrix.M24,
        matrix.M31, matrix.M32, matrix.M33, matrix.M34,
        matrix.M41, matrix.M42, matrix.M43, matrix.M44
    ];

    private static XnaMatrix ToXna(Matrix4x4 matrix) => new(
        matrix.M11, matrix.M12, matrix.M13, matrix.M14,
        matrix.M21, matrix.M22, matrix.M23, matrix.M24,
        matrix.M31, matrix.M32, matrix.M33, matrix.M34,
        matrix.M41, matrix.M42, matrix.M43, matrix.M44);
}
