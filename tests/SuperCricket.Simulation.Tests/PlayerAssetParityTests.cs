using System.Text.Json;
using System.Text.Json.Serialization;
using SuperCricket.Content;

namespace SuperCricket.Simulation.Tests;

public sealed class PlayerAssetParityTests
{
    private static readonly JsonSerializerOptions LegacyFixtureOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    [Fact]
    public void BattingPractice_HumanoidGlbPreservesLegacyContactSummary()
    {
        var shots = BattingShotSet.Load(TestAssets.Asset("batting", "shots.json"));
        var delivery = DeliveryPreset.Load(TestAssets.Asset("deliveries", "standard-pace.json"));
        var legacySamples = BattingPracticeAnalyzer.Analyze(
            LoadLegacyParityFixture(TestAssets.Asset("characters", "practice-batter.scplayer.json")),
            LoadLegacyParityFixture(TestAssets.Asset("characters", "practice-bowler.scplayer.json")),
            shots,
            delivery);
        var glbSamples = BattingPracticeAnalyzer.Analyze(
            PlayerAsset.Load(TestAssets.Asset("characters", "practice-batter-humanoid.glb")),
            PlayerAsset.Load(TestAssets.Asset("characters", "practice-bowler-humanoid.glb")),
            shots,
            delivery);

        Assert.Equal(legacySamples.Count, glbSamples.Count);
        foreach (var shotName in shots.Shots.Select(shot => shot.Name))
        {
            var legacyShot = legacySamples.Where(sample => sample.ShotName == shotName).ToArray();
            var glbShot = glbSamples.Where(sample => sample.ShotName == shotName).ToArray();
            Assert.Equal(legacyShot.Length, glbShot.Length);
            Assert.InRange(
                Math.Abs(legacyShot.Count(sample => sample.ContactQuality.HasValue) - glbShot.Count(sample => sample.ContactQuality.HasValue)),
                0,
                1);

            var legacyBest = legacyShot.MaxBy(sample => sample.ContactQuality ?? -1f)!;
            var glbBest = glbShot.MaxBy(sample => sample.ContactQuality ?? -1f)!;
            Assert.Equal(legacyBest.Outcome, glbBest.Outcome);
            Assert.Equal(legacyBest.FootworkOffsetMeters, glbBest.FootworkOffsetMeters);
            AssertClose(legacyBest.InputDelaySeconds, glbBest.InputDelaySeconds, shotName + " ideal input delay");
            AssertClose(legacyBest.ContactQuality, glbBest.ContactQuality, shotName + " best contact quality");
        }
    }

    private static PlayerAsset LoadLegacyParityFixture(string path)
    {
        var asset = JsonSerializer.Deserialize<PlayerAsset>(File.ReadAllText(path), LegacyFixtureOptions)
            ?? throw new InvalidDataException($"Legacy parity fixture '{path}' was empty.");
        var errors = asset.Validate();
        Assert.Empty(errors);
        return asset;
    }

    private static void AssertClose(float? expected, float? actual, string context)
    {
        if (!expected.HasValue)
        {
            Assert.Null(actual);
            return;
        }

        Assert.NotNull(actual);
        Assert.True(
            MathF.Abs(expected.Value - actual!.Value) <= 0.055f,
            $"{context} diverged: legacy {expected.Value:0.000000}, GLB {actual.Value:0.000000}.");
    }
}
