using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Content.Tests;

public sealed class PlayerGlbRootMotionTests
{
    [Fact]
    public void Load_RemovesExtractedRunUpMotionFromRootPose()
    {
        var asset = PlayerAsset.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "practice-bowler-humanoid.glb"));
        var rootIndex = asset.Bones.FindIndex(bone => bone.ParentIndex < 0);
        var runUp = Assert.Single(asset.Animations, animation => animation.Name == "bowling-run-up");
        var finalSample = runUp.Samples[^1];
        var rootPoseTranslation = finalSample.Bones[rootIndex].Translation.ToVector3();
        var extractedMotion = finalSample.RootMotion.ToVector3();

        Assert.True(extractedMotion.Length() > 10f, "The test clip must contain its authored run-up root motion.");
        Assert.True(
            rootPoseTranslation.Length() < 0.5f,
            $"Root pose retained too much of its extracted motion: {rootPoseTranslation} versus {extractedMotion}.");
    }
}
