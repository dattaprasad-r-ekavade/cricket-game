using SuperCricket.Content.Bowling;

namespace SuperCricket.Content.Tests;

public sealed class DeliveryCatalogueTests
{
    private static string CataloguePath => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "../../../../../assets/bowling/deliveries.json"));

    [Fact]
    public void Load_ContainsTheAuthoredPaceAndSpinDeliveryFamilies()
    {
        var catalogue = DeliveryCatalogue.Load(CataloguePath);

        Assert.Equal(17, catalogue.Deliveries.Count);
        Assert.Equal(8, catalogue.Deliveries.Count(delivery => delivery.Style == BowlerStyle.Pace));
        Assert.Equal(4, catalogue.Deliveries.Count(delivery => delivery.Style == BowlerStyle.OffSpin));
        Assert.Equal(5, catalogue.Deliveries.Count(delivery => delivery.Style == BowlerStyle.LegSpin));
        Assert.Equal(0.35f, catalogue.Deliveries.Single(delivery => delivery.Id == "off-spin-off-break").PitchSpinMultipliers[PitchType.Green]);
        Assert.Equal(1.05f, catalogue.Deliveries.Single(delivery => delivery.Id == "off-spin-off-break").PitchSpinMultipliers[PitchType.Dusty]);
        Assert.Empty(catalogue.Validate());
    }

    [Fact]
    public void Validate_RejectsInvalidRangesAndMissingPitchMultipliers()
    {
        var catalogue = DeliveryCatalogue.Load(CataloguePath);
        catalogue.Deliveries[0].MinSpeedKph = catalogue.Deliveries[0].MaxSpeedKph + 1;
        catalogue.Deliveries.Single(delivery => delivery.Id == "off-spin-off-break").PitchSpinMultipliers.Remove(PitchType.Dusty);

        var errors = catalogue.Validate();

        Assert.Contains(errors, error => error.Contains("speed range", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Dusty", StringComparison.Ordinal));
    }
}
