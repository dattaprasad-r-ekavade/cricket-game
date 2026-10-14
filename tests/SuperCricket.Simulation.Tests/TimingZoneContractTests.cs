using SuperCricket.Simulation;

namespace SuperCricket.Simulation.Tests;

public sealed class TimingZoneContractTests
{
    [Fact]
    public void TimingZoneNumericValuesMatchTheBuildPlanTable()
    {
        Assert.Equal(0, (int)TimingZone.TooLate);
        Assert.Equal(1, (int)TimingZone.Start);
        Assert.Equal(2, (int)TimingZone.Early);
        Assert.Equal(3, (int)TimingZone.Ideal);
        Assert.Equal(4, (int)TimingZone.Late);
        Assert.Equal(5, (int)TimingZone.End);
        Assert.Equal(6, (int)TimingZone.TooEarly);
    }
}
