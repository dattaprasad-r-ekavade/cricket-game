using SuperCricket.Content;
using SuperCricket.Content.Bowling;

namespace SuperCricket.Content.Tests;

public sealed class FieldFormationCatalogTests
{
    private static string AssetsDirectory => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "../../../../../assets/fields"));

    [Fact]
    public void Load_ContainsAllNineLegalStyleAndIntentCombinations()
    {
        var assets = AssetsDirectory;
        var catalog = FieldFormationCatalog.Load(
            Path.Combine(assets, "positions.json"), Path.Combine(assets, "presets"));

        Assert.Equal(25, catalog.Positions.Positions.Count);
        Assert.Equal(9, catalog.Formations.Count);
        Assert.Empty(catalog.Validate(limitedOversRestrictionsEnabled: true, powerplayActive: false));
        Assert.Empty(catalog.Validate(limitedOversRestrictionsEnabled: true, powerplayActive: true));
        foreach (var style in Enum.GetValues<BowlerStyle>())
        foreach (var intent in Enum.GetValues<FieldingIntent>())
        {
            var formation = catalog.Get(style, intent);
            Assert.Equal(10, formation.Positions.Count);
            Assert.Contains("Wicketkeeper", formation.Positions);
        }
    }

    [Fact]
    public void Resolve_MirrorsTheHorizontalCoordinateForLeftHanders()
    {
        var assets = AssetsDirectory;
        var positions = FieldPositionCatalog.Load(Path.Combine(assets, "positions.json"));

        var right = positions.Resolve("Cover", BatterHandedness.Right);
        var left = positions.Resolve("Cover", BatterHandedness.Left);

        Assert.Equal(-right.X, left.X, 5);
        Assert.Equal(right.Y, left.Y, 5);
        Assert.Equal(right.Z, left.Z, 5);
    }

    [Fact]
    public void Validate_RejectsMissingReferencesAndLimitedOversViolations()
    {
        var assets = AssetsDirectory;
        var catalog = FieldFormationCatalog.Load(
            Path.Combine(assets, "positions.json"), Path.Combine(assets, "presets"));
        var formation = catalog.Formations[0];
        formation.Positions[0] = "Unknown position";

        var referenceErrors = formation.Validate(catalog.Positions);

        Assert.Contains(referenceErrors, error => error.Contains("unknown position", StringComparison.OrdinalIgnoreCase));

        formation.Positions = [
            "Short leg", "Mid-on", "Mid-wicket", "Square leg", "Fine leg", "Deep square",
            "Deep mid-wicket", "Long-on", "Long leg", "Wicketkeeper"];
        var restrictionErrors = formation.Validate(catalog.Positions, limitedOversRestrictionsEnabled: true, powerplayActive: true);

        Assert.Contains(restrictionErrors, error => error.Contains("leg side", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(restrictionErrors, error => error.Contains("outside the inner ring", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(formation.Validate(catalog.Positions, limitedOversRestrictionsEnabled: false, powerplayActive: false));
    }
}
