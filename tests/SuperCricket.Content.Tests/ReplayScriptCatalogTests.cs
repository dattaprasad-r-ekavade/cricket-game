using SuperCricket.Content.Replays;

namespace SuperCricket.Content.Tests;

public sealed class ReplayScriptCatalogTests
{
    [Fact]
    public void AuthoredReplayScriptsCoverEveryEventAndValidate()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "replays", "replay-scripts.json"));

        var catalog = ReplayScriptCatalog.Load(path);

        Assert.Equal(6, catalog.Events.Count);
        Assert.Equal("stump-line-strip", catalog.GetEvent("lbw").Shots[0].Overlay);
        Assert.Equal("crease-zoom", catalog.GetEvent("run-out").Shots[0].Overlay);
    }

    [Fact]
    public void InvalidSpeedAndWrongEventOverlayAreReported()
    {
        var catalog = new ReplayScriptCatalog(
        [
            new ReplayEventScript("lbw", [new ReplayScriptShot("square-leg", -1f, 2f, 0.75f, "crease-zoom")])
        ]);

        var errors = catalog.Validate();

        Assert.Contains(errors, error => error.Contains("speed must be", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("missing.", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("non-run-out event", StringComparison.Ordinal));
    }
}
