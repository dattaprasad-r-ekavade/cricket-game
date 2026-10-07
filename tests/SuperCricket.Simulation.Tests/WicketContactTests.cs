using System.Numerics;

namespace SuperCricket.Simulation.Tests;

public sealed class WicketContactTests
{
    [Theory]
    [InlineData(0f, 0.55f, -10.06f, true, true, true)]
    [InlineData(0f, 0.55f, -10.06f, false, true, false)]
    [InlineData(0f, 0.55f, -10.06f, true, false, false)]
    [InlineData(0.2f, 0.55f, -10.06f, true, true, false)]
    [InlineData(0f, 0.8f, -10.06f, true, true, false)]
    [InlineData(0f, -0.1f, -10.06f, true, true, false)]
    [InlineData(0f, 0.55f, -9f, true, true, false)]
    public void ReturnMustBeReleasedAfterPossessionAndReachTheStumps(
        float x, float y, float z, bool secured, bool released, bool expected) =>
        Assert.Equal(expected, WicketContactModel.IsBroken(new Vector3(x, y, z), -10.06f, 0.036f, secured, released));

    [Fact]
    public void FarWicketUsesItsOwnPlane() =>
        Assert.True(WicketContactModel.IsBroken(new Vector3(0f, 0.55f, 10.06f), 10.06f, 0.036f, true, true));

    [Fact]
    public void InvalidPositionsAndRadiiAreRejected()
    {
        Assert.Throws<ArgumentException>(() => WicketContactModel.IsBroken(new Vector3(float.NaN), 10f, 0.036f, true, true));
        Assert.Throws<ArgumentException>(() => WicketContactModel.IsBroken(Vector3.Zero, float.NaN, 0.036f, true, true));
        Assert.Throws<ArgumentException>(() => WicketContactModel.IsBroken(Vector3.Zero, 10f, 0f, true, true));
    }
}
