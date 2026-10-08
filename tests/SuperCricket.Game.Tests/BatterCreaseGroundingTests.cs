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
}
