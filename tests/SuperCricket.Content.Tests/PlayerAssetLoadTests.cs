using SuperCricket.Content;

namespace SuperCricket.Content.Tests;

public sealed class PlayerAssetLoadTests
{
    [Fact]
    public void Load_RejectsLegacyPlayerJsonAsRuntimeInput()
    {
        var error = Assert.Throws<NotSupportedException>(() => PlayerAsset.Load("practice-batter.scplayer.json"));

        Assert.Contains("humanoid GLB (.glb)", error.Message);
        Assert.Contains("parity fixtures only", error.Message);
    }

    [Fact]
    public void Validate_RejectsOutOfRangeGlbBindPoseScale()
    {
        var asset = PlayerAsset.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "practice-batter-humanoid.glb"));
        asset.Bones[0].BindPose.Scale.X = 10f;

        var errors = asset.Validate();

        Assert.Contains(errors, error => error.Contains("bind-pose scale", StringComparison.Ordinal));
    }
}
