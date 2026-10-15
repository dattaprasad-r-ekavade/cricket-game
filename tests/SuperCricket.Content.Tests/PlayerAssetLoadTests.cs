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

    [Fact]
    public void PlayerVariantCatalog_LoadsAllProfilesAndAppliesTeamMasksToEveryLod()
    {
        var fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "wp19");
        var catalog = PlayerVariantCatalog.Load(Path.Combine(fixtureDirectory, "character-variants.json"));
        var animationTemplate = PlayerAsset.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "practice-batter-humanoid.glb"));

        Assert.Equal(3, catalog.Variants.Count);
        Assert.Equal(0, catalog.SelectLod(17.99f));
        Assert.Equal(1, catalog.SelectLod(18f));
        Assert.Equal(1, catalog.SelectLod(41.99f));
        Assert.Equal(2, catalog.SelectLod(42f));

        foreach (var variant in catalog.Variants)
        foreach (var lod in variant.Lods)
        {
            var asset = PlayerAsset.LoadSkinnedVariant(
                catalog.GetGlbPath(fixtureDirectory, variant.Id, lod.Lod),
                animationTemplate);
            catalog.ApplyTeamColorMasks(asset, fixtureDirectory);

            Assert.Equal(animationTemplate.Animations.Count, asset.Animations.Count);
            Assert.Equal(animationTemplate.Bones.Select(bone => bone.Name), asset.Bones.Select(bone => bone.Name));
            Assert.Equal(2, asset.Textures.Count);
            Assert.Equal(0, asset.Meshes.Single(mesh => mesh.Name.StartsWith("WP19 Jersey ", StringComparison.Ordinal)).BaseColorTextureIndex);
            Assert.Equal(1, asset.Meshes.Single(mesh => mesh.Name.StartsWith("WP19 Trouser ", StringComparison.Ordinal)).BaseColorTextureIndex);
        }
    }
}
