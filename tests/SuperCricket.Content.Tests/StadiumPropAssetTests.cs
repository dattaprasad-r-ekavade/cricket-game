using SuperCricket.Content;
using System.Numerics;

namespace SuperCricket.Content.Tests;

public sealed class StadiumPropAssetTests
{
    [Fact]
    public void StadiumLayout_LoadsAndValidatesEveryWp20Prop()
    {
        var layoutPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "wp20", "stadium-layout.json");
        var layout = StadiumPropLayoutAsset.Load(layoutPath);
        var uniqueAssets = layout.Placements.Select(placement => placement.Asset)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        Assert.Equal(15, layout.Placements.Count);
        Assert.Equal(11, uniqueAssets.Length);
        foreach (var assetFile in uniqueAssets)
        {
            var asset = StadiumPropAsset.Load(layout.GetAssetPath(layoutPath, assetFile));
            Assert.NotEmpty(asset.Meshes);
            Assert.All(asset.Meshes, mesh =>
            {
                Assert.NotEmpty(mesh.Positions);
                Assert.Equal(mesh.Positions.Length, mesh.Normals.Length);
                Assert.NotEmpty(mesh.Indices);
                Assert.All(mesh.Positions, position => Assert.True(IsFinite(position)));
                Assert.All(mesh.Normals, normal => Assert.True(IsFinite(normal)));
                Assert.All(mesh.Indices, index => Assert.InRange(index, 0, mesh.Positions.Length - 1));
                Assert.True(IsFinite(mesh.DiffuseColor));
            });
        }
    }

    [Fact]
    public void StadiumLayout_RejectsAssetPathsThatEscapeThePropDirectory()
    {
        var layoutPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "wp20", "stadium-layout.json");
        var layout = StadiumPropLayoutAsset.Load(layoutPath);
        layout.Placements[0].Asset = "../outside.glb";

        var errors = layout.Validate(Path.GetDirectoryName(layoutPath)!);

        Assert.Contains(errors, error => error.Contains("must exist inside", StringComparison.Ordinal));
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
