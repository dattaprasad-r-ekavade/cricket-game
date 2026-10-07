using Microsoft.Xna.Framework;
using SuperCricket.Content;
using SuperCricket.Game.Animation;
using SuperCricket.Simulation;

namespace SuperCricket.Game.Tests;

public sealed class BatterAnimationControllerTests
{
    private static readonly Lazy<PlayerAsset> BatterAsset = new(() => PlayerAsset.Load(
        Path.Combine(AppContext.BaseDirectory, "Assets", "Characters", "practice-batter-humanoid.glb")));

    [Fact]
    public void ActualHumanoidAssetHasIndependentAnimatorsAndBonePalettes()
    {
        var pair = Create();
        Assert.Equal(61, BatterAsset.Value.Bones.Count);
        Assert.NotSame(pair.Striker, pair.NonStriker);
        Assert.Equal("practice-stance", pair.Striker.CurrentClipName);
        Assert.Equal("practice-stance", pair.NonStriker.CurrentClipName);
        Assert.NotSame(pair.Striker.GetSkinMatrices(), pair.NonStriker.GetSkinMatrices());
        Assert.Equal(61, pair.NonStriker.GetSkinMatrices().Length);
    }

    [Theory]
    [InlineData("defensive-block")]
    [InlineData("front-foot-drive")]
    [InlineData("lofted-drive")]
    [InlineData("back-foot-drive")]
    [InlineData("back-foot-loft")]
    [InlineData("batting-step-offside")]
    [InlineData("batting-step-legside")]
    public void StrikerActionDoesNotBecomeTheNonStrikerPose(string clip)
    {
        var pair = Create();
        pair.Update(0.2f);
        var nonStrikerTime = pair.NonStriker.CurrentTimeSeconds;
        pair.Striker.Play(clip, 0.12f);
        Assert.Equal(nonStrikerTime, pair.NonStriker.CurrentTimeSeconds);
        Assert.Equal("practice-stance", pair.NonStriker.CurrentClipName);
        pair.Update(0.4f);
        var strikerPose = pair.Striker.GetSkinMatrices();
        var nonStrikerPose = pair.NonStriker.GetSkinMatrices();
        Assert.Contains(Enumerable.Range(0, strikerPose.Length), bone => strikerPose[bone] != nonStrikerPose[bone]);
        Assert.All(strikerPose.Concat(nonStrikerPose), AssertFinite);
    }

    [Theory]
    [InlineData("defensive-block")]
    [InlineData("front-foot-drive")]
    [InlineData("lofted-drive")]
    [InlineData("back-foot-drive")]
    [InlineData("back-foot-loft")]
    public void ShotActionPlaysOnceAndReturnsToReadyPose(string clip)
    {
        var pair = Create();
        pair.Update(0.2f);
        var readyPose = pair.Striker.GetSkinMatrices().ToArray();

        pair.PlayShot(clip);
        Assert.Equal(clip, pair.Striker.CurrentClipName);
        pair.Update(0.4f);
        Assert.Contains(Enumerable.Range(0, readyPose.Length), bone =>
            readyPose[bone] != pair.Striker.GetSkinMatrices()[bone]);

        pair.Update(2.1f);
        Assert.Equal("practice-stance", pair.Striker.CurrentClipName);
        Assert.Equal("practice-stance", pair.NonStriker.CurrentClipName);
    }

    [Fact]
    public void DeveloperClipCyclingLeavesTheNonStrikerInStance()
    {
        var pair = Create();
        pair.Striker.PlayNext();
        pair.Update(0.4f);
        Assert.NotEqual("practice-stance", pair.Striker.CurrentClipName);
        Assert.Equal("practice-stance", pair.NonStriker.CurrentClipName);
    }

    [Fact]
    public void RunningAndReturnHomeChangeBothBattersWithoutSharingTheirBuffers()
    {
        var pair = Create();
        pair.Striker.Play("front-foot-drive");
        pair.SetRunning(true);
        Assert.Equal("between-wickets", pair.Striker.CurrentClipName);
        Assert.Equal("between-wickets", pair.NonStriker.CurrentClipName);
        pair.Update(0.3f);
        Assert.Equal(pair.Striker.GetSkinMatrices(), pair.NonStriker.GetSkinMatrices());
        Assert.NotSame(pair.Striker.GetSkinMatrices(), pair.NonStriker.GetSkinMatrices());
        pair.SetRunning(false);
        Assert.Equal("practice-stance", pair.Striker.CurrentClipName);
        Assert.Equal("practice-stance", pair.NonStriker.CurrentClipName);
        pair.Update(0.2f);
        Assert.Equal(pair.Striker.GetSkinMatrices(), pair.NonStriker.GetSkinMatrices());
    }

    [Fact]
    public void TurnBackKeepsBothRunningClocksContinuousUntilHome()
    {
        var pair = Create();
        var runners = new BetweenWicketsState();
        runners.StartRun();
        pair.SetRunning(true);
        runners.Advance(0.6f, 1f);
        pair.Update(0.3f);
        var time = pair.NonStriker.CurrentTimeSeconds;
        runners.TurnBack();
        pair.SetRunning(runners.IsMoving);
        Assert.Equal(time, pair.NonStriker.CurrentTimeSeconds);
        Assert.Equal("between-wickets", pair.NonStriker.CurrentClipName);
        runners.Advance(0.6f, 1f);
        pair.SetRunning(runners.IsMoving);
        Assert.Equal("practice-stance", pair.Striker.CurrentClipName);
        Assert.Equal("practice-stance", pair.NonStriker.CurrentClipName);
    }

    [Fact]
    public void QueuedFollowUpRunDoesNotRestartEitherGait()
    {
        var pair = Create();
        var runners = new BetweenWicketsState();
        runners.StartRun();
        pair.SetRunning(true);
        pair.Update(0.25f);
        var strikerTime = pair.Striker.CurrentTimeSeconds;
        var nonStrikerTime = pair.NonStriker.CurrentTimeSeconds;
        Assert.Equal(RunMovementResult.CompletedRun, runners.Advance(1f, 1f));
        runners.StartRun();
        pair.SetRunning(runners.IsMoving);
        Assert.Equal(strikerTime, pair.Striker.CurrentTimeSeconds);
        Assert.Equal(nonStrikerTime, pair.NonStriker.CurrentTimeSeconds);
        Assert.Equal("between-wickets", pair.NonStriker.CurrentClipName);
    }

    [Theory]
    [InlineData("front-foot-drive")]
    [InlineData("between-wickets")]
    public void NextDeliveryResetsBothClocksAndClearsPreviousActionPoses(string clip)
    {
        var pair = Create();
        if (clip == "between-wickets") pair.SetRunning(true);
        else pair.Striker.Play(clip);
        pair.Update(0.4f);
        pair.ResetDelivery();
        Assert.Equal(0f, pair.Striker.CurrentTimeSeconds);
        Assert.Equal(0f, pair.NonStriker.CurrentTimeSeconds);
        pair.Update(0.2f);
        Assert.Equal("practice-stance", pair.Striker.CurrentClipName);
        Assert.Equal("practice-stance", pair.NonStriker.CurrentClipName);
        Assert.Equal(pair.Striker.GetSkinMatrices(), pair.NonStriker.GetSkinMatrices());
        pair.SetRunning(true);
        Assert.Equal("between-wickets", pair.NonStriker.CurrentClipName);
    }

    [Fact]
    public void DeliveryStopReturnsBothRunningBattersToStance()
    {
        var pair = Create();
        var runners = new BetweenWicketsState();
        runners.StartRun();
        pair.SetRunning(true);
        runners.Advance(0.7f, 1f);
        pair.Update(0.3f);
        runners.Stop();
        pair.SetRunning(runners.IsMoving);
        Assert.Equal("practice-stance", pair.Striker.CurrentClipName);
        Assert.Equal("practice-stance", pair.NonStriker.CurrentClipName);
        Assert.InRange(runners.Progress, 0.699f, 0.701f);
    }

    [Fact]
    public void IdleMovementUpdatesDoNotCancelAStrikerShot()
    {
        var pair = Create();
        pair.Striker.Play("lofted-drive");
        pair.SetRunning(false);
        Assert.Equal("lofted-drive", pair.Striker.CurrentClipName);
        Assert.Equal("practice-stance", pair.NonStriker.CurrentClipName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PauseOrCaptureFreezePreservesBothClocksAndPalettes(bool running)
    {
        var pair = Create();
        if (running) pair.SetRunning(true);
        else pair.Striker.Play("front-foot-drive", 0.12f);
        pair.Update(0.04f);
        var strikerTime = pair.Striker.CurrentTimeSeconds;
        var nonStrikerTime = pair.NonStriker.CurrentTimeSeconds;
        var strikerPose = pair.Striker.GetSkinMatrices().ToArray();
        var nonStrikerPose = pair.NonStriker.GetSkinMatrices().ToArray();
        pair.Update(0.7f, frozen: true);
        Assert.Equal(strikerTime, pair.Striker.CurrentTimeSeconds);
        Assert.Equal(nonStrikerTime, pair.NonStriker.CurrentTimeSeconds);
        Assert.Equal(strikerPose, pair.Striker.GetSkinMatrices());
        Assert.Equal(nonStrikerPose, pair.NonStriker.GetSkinMatrices());
        pair.Update(0.01f);
        Assert.True(pair.Striker.CurrentTimeSeconds > strikerTime);
        Assert.True(pair.NonStriker.CurrentTimeSeconds > nonStrikerTime);
    }

    [Fact]
    public void SamplingOrAdvancingNonStrikerDoesNotOverwriteStrikerPaletteOrAsset()
    {
        var pair = Create();
        var bindPoses = BatterAsset.Value.Bones.Select(bone => bone.BindPose.ToNumericsMatrix()).ToArray();
        pair.Striker.Play("front-foot-drive");
        pair.Update(0.4f);
        var strikerBuffer = pair.Striker.GetSkinMatrices();
        var saved = strikerBuffer.ToArray();
        pair.NonStriker.Play("between-wickets");
        pair.NonStriker.Update(0.3f);
        pair.NonStriker.GetSkinMatrices();
        Assert.Equal(saved, strikerBuffer);
        Assert.Equal(bindPoses, BatterAsset.Value.Bones.Select(bone => bone.BindPose.ToNumericsMatrix()));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void BothIndependentRunningPosesAdvanceAtSupportedUpdateRates(int frameRate)
    {
        var pair = Create();
        pair.SetRunning(true);
        for (var frame = 0; frame < frameRate / 2; frame++) pair.Update(1f / frameRate);
        Assert.Equal("between-wickets", pair.Striker.CurrentClipName);
        Assert.Equal("between-wickets", pair.NonStriker.CurrentClipName);
        Assert.Equal(pair.Striker.CurrentTimeSeconds, pair.NonStriker.CurrentTimeSeconds);
        Assert.Equal(pair.Striker.GetSkinMatrices(), pair.NonStriker.GetSkinMatrices());
        Assert.NotSame(pair.Striker.GetSkinMatrices(), pair.NonStriker.GetSkinMatrices());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidElapsedTimeIsRejectedEvenWhenFrozen(bool frozen)
    {
        var pair = Create();
        foreach (var elapsed in new[] { -1f, float.NaN, float.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => pair.Update(elapsed, frozen));
        Assert.Throws<ArgumentNullException>(() => new BatterAnimationController(null!));
    }

    private static BatterAnimationController Create() => new(BatterAsset.Value);

    private static void AssertFinite(Matrix matrix)
    {
        Assert.All(new[] { matrix.M11, matrix.M12, matrix.M13, matrix.M14, matrix.M21, matrix.M22, matrix.M23, matrix.M24,
            matrix.M31, matrix.M32, matrix.M33, matrix.M34, matrix.M41, matrix.M42, matrix.M43, matrix.M44 },
            value => Assert.True(float.IsFinite(value)));
    }
}
