using SuperCricket.Content;
using SuperCricket.Simulation;

namespace SuperCricket.Simulation.Tests;

public sealed class BallCategoryAndStrokeSelectorTests
{
    [Theory]
    [InlineData("standard-pace.json", BallLength.Good, BallLine.Stumps)]
    [InlineData("wide-pace.json", BallLength.Good, BallLine.WideOutsideOff)]
    [InlineData("yorker-pace.json", BallLength.Yorker, BallLine.Stumps)]
    [InlineData("no-ball-pace.json", BallLength.Good, BallLine.Stumps)]
    public void Classify_RecognizesEveryPreparedDeliveryPreset(string fileName, BallLength length, BallLine line)
    {
        var delivery = DeliveryPreset.Load(TestAssets.Asset("deliveries", fileName));

        var category = BallCategoryClassifier.Classify(delivery, batterStanceX: 0f, leftHanded: false);

        Assert.Equal(length, category.Length);
        Assert.Equal(line, category.Line);
        Assert.True(float.IsFinite(category.ArrivalHeightMeters));
        Assert.True(category.ArrivalHeightMeters >= 0f);
    }

    [Theory]
    [InlineData(0f, BallLength.FullToss)]
    [InlineData(0.999f, BallLength.Yorker)]
    [InlineData(1f, BallLength.Full)]
    [InlineData(2.999f, BallLength.Full)]
    [InlineData(3f, BallLength.Good)]
    [InlineData(6.5f, BallLength.Good)]
    [InlineData(6.501f, BallLength.Short)]
    public void ClassifyLength_UsesTheAuthoredCutoffs(float distance, BallLength expected)
    {
        Assert.Equal(expected, BallCategoryClassifier.ClassifyLength(distance));
    }

    [Fact]
    public void Classify_FullTossesAHighDeliveryThatHasNotBouncedBeforeTheCrease()
    {
        var delivery = DeliveryPreset.Load(TestAssets.Asset("deliveries", "standard-pace.json"));
        delivery.ReleaseVelocity.Y = 10f;

        var category = BallCategoryClassifier.Classify(delivery, batterStanceX: 0f, leftHanded: false);

        Assert.Equal(BallLength.FullToss, category.Length);
        Assert.True(category.ArrivalHeightMeters > BallCategoryClassifier.HighArrivalThresholdMeters);
    }

    [Fact]
    public void ClassifyLine_UsesStumpAndWideLineBoundariesAndBatterStance()
    {
        var stumpHalfWidth = CricketPitchGeometry.WicketHalfWidthMeters;
        var wideLimit = stumpHalfWidth + BallCategoryClassifier.WideLineMarginMeters;

        Assert.Equal(BallLine.Stumps, BallCategoryClassifier.ClassifyLine(stumpHalfWidth, 0f, leftHanded: false));
        Assert.Equal(BallLine.OutsideOff, BallCategoryClassifier.ClassifyLine(stumpHalfWidth + 0.001f, 0f, leftHanded: false));
        Assert.Equal(BallLine.OutsideOff, BallCategoryClassifier.ClassifyLine(wideLimit, 0f, leftHanded: false));
        Assert.Equal(BallLine.WideOutsideOff, BallCategoryClassifier.ClassifyLine(wideLimit + 0.001f, 0f, leftHanded: false));
        Assert.Equal(BallLine.Leg, BallCategoryClassifier.ClassifyLine(-stumpHalfWidth - 0.001f, 0f, leftHanded: false));
        Assert.Equal(BallLine.WideLeg, BallCategoryClassifier.ClassifyLine(-wideLimit - 0.001f, 0f, leftHanded: false));
        Assert.Equal(BallLine.Stumps, BallCategoryClassifier.ClassifyLine(1.25f, 1.25f, leftHanded: false));
    }

    [Fact]
    public void Classify_MirroredDeliveryAndBatterProduceTheSameOffSideLine()
    {
        var delivery = DeliveryPreset.Load(TestAssets.Asset("deliveries", "wide-pace.json"));
        var mirrored = delivery.DeepCopy();
        mirrored.ReleasePosition.X *= -1f;
        mirrored.ReleaseVelocity.X *= -1f;
        mirrored.LateralAccelerationMetersPerSecondSquared *= -1f;

        var rightHanded = BallCategoryClassifier.Classify(delivery, 0f, leftHanded: false);
        var leftHanded = BallCategoryClassifier.Classify(mirrored, 0f, leftHanded: true);
        var unmirroredLine = BallCategoryClassifier.Classify(mirrored, 0f, leftHanded: false).Line;

        Assert.Equal(rightHanded.Length, leftHanded.Length);
        Assert.Equal(rightHanded.Line, leftHanded.Line);
        Assert.Equal(BallLine.WideLeg, unmirroredLine);
    }

    [Theory]
    [InlineData(BallLength.Short, 0.2f, ShotDirection8.Cover, StrokeFoot.Back)]
    [InlineData(BallLength.Good, 1.1f, ShotDirection8.OffDrive, StrokeFoot.Back)]
    [InlineData(BallLength.Full, 0.3f, ShotDirection8.Cover, StrokeFoot.Front)]
    [InlineData(BallLength.Yorker, 0.3f, ShotDirection8.Square, StrokeFoot.Front)]
    [InlineData(BallLength.FullToss, 0.3f, ShotDirection8.Fine, StrokeFoot.Front)]
    [InlineData(BallLength.Good, 0.3f, ShotDirection8.MidWicket, StrokeFoot.Front)]
    [InlineData(BallLength.Good, 0.3f, ShotDirection8.LegFine, StrokeFoot.Back)]
    public void ChooseFoot_AppliesTheAutomaticFootRules(
        BallLength length, float arrivalHeight, ShotDirection8 direction, StrokeFoot expected)
    {
        var category = new BallCategory(length, BallLine.Stumps, arrivalHeight);

        Assert.Equal(expected, BallCategoryClassifier.ChooseFoot(category, direction));
    }

    [Fact]
    public void Select_PrefersExactDirectionIntentLoftAndFoot()
    {
        var table = LoadTable();
        var request = new StrokeRequest(ShotDirection8.OffDrive, StrokeIntent.Attack, Loft: false, StrokeFoot.Front);
        var ball = new BallCategory(BallLength.Good, BallLine.OutsideOff, 0.5f);

        var selected = StrokeSelector.Select(table, request, ball);

        Assert.Equal("offdrive-attack-front", selected.Id);
    }

    [Fact]
    public void Select_ResolvesAutoFootFromTheBallCategory()
    {
        var table = LoadTable();
        var request = new StrokeRequest(ShotDirection8.Cover, StrokeIntent.Attack, Loft: false, StrokeFoot.Auto);
        var ball = new BallCategory(BallLength.Short, BallLine.OutsideOff, 0.5f);

        var selected = StrokeSelector.Select(table, request, ball);

        Assert.Equal("cover-attack-back", selected.Id);
    }

    [Fact]
    public void Select_PrefersBallSuitabilityBeforeDirectionDistanceDuringFallback()
    {
        var table = LoadTable();
        var cover = table.Strokes.Single(stroke => stroke.Direction == ShotDirection8.Cover && stroke.Loft && stroke.Foot == StrokeFoot.Front);
        var fine = table.Strokes.Single(stroke => stroke.Direction == ShotDirection8.Fine && stroke.Loft && stroke.Foot == StrokeFoot.Front);
        cover.Suits.Lines = [BallLine.Leg];
        fine.Suits.Lines = [BallLine.WideOutsideOff];
        var request = new StrokeRequest(ShotDirection8.OffDrive, StrokeIntent.Attack, Loft: true, StrokeFoot.Front);
        var ball = new BallCategory(BallLength.Good, BallLine.WideOutsideOff, 0.5f);

        var selected = StrokeSelector.Select(table, request, ball);

        Assert.Equal(fine.Id, selected.Id);
    }

    [Fact]
    public void Select_UsesNearestSameSideAndStableIdTieBreaks()
    {
        var table = LoadTable();
        var request = new StrokeRequest(ShotDirection8.OffDrive, StrokeIntent.Attack, Loft: true, StrokeFoot.Front);
        var ball = new BallCategory(BallLength.Good, BallLine.OutsideOff, 0.5f);

        var first = StrokeSelector.Select(table, request, ball);
        var repeated = StrokeSelector.Select(table, request, ball);

        Assert.Equal("cover-loft", first.Id);
        Assert.Equal(first.Id, repeated.Id);

        var duplicate = Clone(table.Strokes.Single(stroke => stroke.Id == first.Id), "aa-alternate");
        table.Strokes.Add(duplicate);
        var ordered = StrokeSelector.Select(table, request, ball).Id;
        table.Strokes.Reverse();
        var reversed = StrokeSelector.Select(table, request, ball).Id;

        Assert.Equal("aa-alternate", ordered);
        Assert.Equal(ordered, reversed);
    }

    private static StrokeTableAsset LoadTable() =>
        StrokeTableAsset.Load(TestAssets.Asset("batting", "strokes.json"));

    private static StrokeDefinition Clone(StrokeDefinition source, string id) => new()
    {
        Id = id,
        Direction = source.Direction,
        Intent = source.Intent,
        Foot = source.Foot,
        Loft = source.Loft,
        AnimationClip = source.AnimationClip,
        Aggression = source.Aggression,
        Difficulty = source.Difficulty,
        Reward = source.Reward,
        Suits = new StrokeSuit { Lengths = [.. source.Suits.Lengths], Lines = [.. source.Suits.Lines] },
        Zones = source.Zones.ToDictionary(pair => pair.Key, pair => pair.Value)
    };
}
