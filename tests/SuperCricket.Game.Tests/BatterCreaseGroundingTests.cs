using Microsoft.Xna.Framework;
using SuperCricket.Content;
using SuperCricket.Game.Animation;
using SuperCricket.Simulation;

namespace SuperCricket.Game.Tests;

public sealed class BatterCreaseGroundingTests(ITestOutputHelper output)
{
    private const float PitchSurfaceY = -0.025f;
    private static readonly Lazy<PlayerAsset> BatterAsset = new(() => PlayerAsset.Load(
        Path.Combine(AppContext.BaseDirectory, "Assets", "Characters", "practice-batter-humanoid.glb")));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PracticeStanceGroundsBothShoesPastThePoppingCrease(bool atNearEnd)
    {
        var asset = BatterAsset.Value;
        var animator = new PlayerAnimator(asset);
        animator.Play("practice-stance", 0.001f);
        animator.Update(0.001f);
        Assert.Equal("practice-stance", animator.CurrentClipName);
        var world = BatterRunningPresenter.CreateWorld(
            atNearEnd ? CricketPitchGeometry.NearBatterAnchorZ : CricketPitchGeometry.FarBatterAnchorZ,
            facesFar: atNearEnd);
        var creaseZ = atNearEnd ? CricketPitchGeometry.NearPoppingCreaseZ : CricketPitchGeometry.FarPoppingCreaseZ;
        var skinMatrices = animator.GetSkinMatrices();
        var contactCounts = new List<string>();

        foreach (var mesh in asset.Meshes)
        {
            var verticesBehindCrease = 0;
            var lowestGroundDelta = float.PositiveInfinity;
            var furthestBehindMeters = 0f;
            var closestInsideGapMeters = float.PositiveInfinity;
            for (var vertex = 0; vertex < mesh.Positions.Length / 3; vertex++)
            {
                var position = new Vector3(
                    mesh.Positions[vertex * 3],
                    mesh.Positions[vertex * 3 + 1],
                    mesh.Positions[vertex * 3 + 2]);
                var skinned = Vector3.Zero;
                for (var influence = 0; influence < 4; influence++)
                {
                    var weight = mesh.BoneWeights[vertex * 4 + influence];
                    if (weight <= 0f)
                        continue;
                    var boneIndex = mesh.BoneIndices[vertex * 4 + influence];
                    skinned += Vector3.Transform(position, skinMatrices[boneIndex]) * weight;
                }

                var worldPosition = Vector3.Transform(skinned, world);
                var groundDelta = MathF.Abs(worldPosition.Y - PitchSurfaceY);
                lowestGroundDelta = MathF.Min(lowestGroundDelta, groundDelta);
                var behindDistance = atNearEnd ? creaseZ - worldPosition.Z : worldPosition.Z - creaseZ;
                if (groundDelta <= 0.02f && behindDistance >= 0f)
                {
                    verticesBehindCrease++;
                    furthestBehindMeters = MathF.Max(furthestBehindMeters, behindDistance);
                }
                else if (groundDelta <= 0.02f)
                {
                    closestInsideGapMeters = MathF.Min(closestInsideGapMeters, -behindDistance);
                }
            }

            if (mesh.Name.Contains("Shoe Sole", StringComparison.OrdinalIgnoreCase))
            {
                Assert.True(lowestGroundDelta <= 0.02f,
                    $"{mesh.Name} has no shoe vertex grounded on the pitch at the {(atNearEnd ? "near" : "far")} end.");
                Assert.True(furthestBehindMeters >= 0.02f,
                    $"{mesh.Name} reaches only {furthestBehindMeters:0.000} m beyond the {(atNearEnd ? "near" : "far")} popping crease.");
                contactCounts.Add($"{mesh.Name}: grounded-behind={verticesBehindCrease}, furthest={furthestBehindMeters:0.000} m, inside-gap={closestInsideGapMeters:0.000} m, nearest-ground={lowestGroundDelta:0.000} m");
            }
        }

        Assert.Equal(2, contactCounts.Count);
        output.WriteLine($"End={(atNearEnd ? "near" : "far")}, anchor={(atNearEnd ? CricketPitchGeometry.NearBatterAnchorZ : CricketPitchGeometry.FarBatterAnchorZ):0.000} m, crease={creaseZ:0.000} m");
        foreach (var summary in contactCounts)
            output.WriteLine(summary);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void BothRunnersGroundBeyondTheirCreasesWhenRunCompletes(int frameRate)
    {
        const float fixedStepSeconds = 1f / 120f;
        var runners = new BetweenWicketsState();
        var animations = new BatterAnimationController(BatterAsset.Value);
        runners.StartRun();
        animations.SetRunning(true);

        var accumulator = 0f;
        var frameSeconds = 1f / frameRate;
        for (var frame = 0; frame < frameRate * 3 && runners.IsMoving; frame++)
        {
            animations.Update(frameSeconds);
            accumulator += frameSeconds;
            while (accumulator >= fixedStepSeconds && runners.IsMoving)
            {
                var movement = runners.Advance(fixedStepSeconds, CpuLiveRunningDecisionModel.DefaultRunDurationSeconds);
                accumulator -= fixedStepSeconds;
                if (movement == RunMovementResult.CompletedRun)
                    animations.SetRunning(false);
            }
        }

        Assert.False(runners.IsMoving, "The simulated batter did not reach the opposite crease.");
        Assert.Equal(1, runners.CompletedRuns);
        var worlds = BatterRunningPresenter.GetWorlds(
            runners, CricketPitchGeometry.NearBatterAnchorZ, CricketPitchGeometry.FarBatterAnchorZ, 0f);
        AssertRunnersGroundedBeyondCreases(animations, worlds.Striker, worlds.NonStriker, frameRate, "run completion");

        var transitionFrames = (int)MathF.Ceiling(0.12f / frameSeconds);
        for (var transitionFrame = 0; transitionFrame < transitionFrames; transitionFrame++)
            animations.Update(frameSeconds);
        Assert.False(animations.Striker.IsTransitioning, "The striker animation did not finish its stance transition.");
        Assert.False(animations.NonStriker.IsTransitioning, "The non-striker animation did not finish its stance transition.");
        AssertRunnersGroundedBeyondCreases(animations, worlds.Striker, worlds.NonStriker, frameRate, "completed stance transition");
    }

    private static void AssertRunnersGroundedBeyondCreases(
        BatterAnimationController animations, Matrix strikerWorld, Matrix nonStrikerWorld, int frameRate, string phase)
    {
        AssertShoeGroundedBeyondCrease(animations.Striker, strikerWorld, atNearEnd: false, frameRate, phase);
        AssertShoeGroundedBeyondCrease(animations.NonStriker, nonStrikerWorld, atNearEnd: true, frameRate, phase);
    }

    private static void AssertShoeGroundedBeyondCrease(
        PlayerAnimator animator, Matrix world, bool atNearEnd, int frameRate, string phase)
    {
        var creaseZ = atNearEnd ? CricketPitchGeometry.NearPoppingCreaseZ : CricketPitchGeometry.FarPoppingCreaseZ;
        var skinMatrices = animator.GetSkinMatrices();
        var furthestGroundedBehindMeters = 0f;
        foreach (var mesh in BatterAsset.Value.Meshes.Where(mesh =>
                     mesh.Name.Contains("Shoe Sole", StringComparison.OrdinalIgnoreCase)))
        {
            for (var vertex = 0; vertex < mesh.Positions.Length / 3; vertex++)
            {
                var position = new Vector3(mesh.Positions[vertex * 3], mesh.Positions[vertex * 3 + 1], mesh.Positions[vertex * 3 + 2]);
                var skinned = Vector3.Zero;
                for (var influence = 0; influence < 4; influence++)
                {
                    var weight = mesh.BoneWeights[vertex * 4 + influence];
                    if (weight <= 0f)
                        continue;
                    skinned += Vector3.Transform(position, skinMatrices[mesh.BoneIndices[vertex * 4 + influence]]) * weight;
                }

                var worldPosition = Vector3.Transform(skinned, world);
                if (MathF.Abs(worldPosition.Y - PitchSurfaceY) > 0.02f)
                    continue;
                var behindDistance = atNearEnd ? creaseZ - worldPosition.Z : worldPosition.Z - creaseZ;
                furthestGroundedBehindMeters = MathF.Max(furthestGroundedBehindMeters, behindDistance);
            }
        }

        Assert.True(furthestGroundedBehindMeters >= 0.02f,
            $"At {frameRate} Hz during {phase}, the {(atNearEnd ? "near" : "far")} runner reaches only {furthestGroundedBehindMeters:0.000} m beyond the crease with a grounded shoe sole.");
    }
}
