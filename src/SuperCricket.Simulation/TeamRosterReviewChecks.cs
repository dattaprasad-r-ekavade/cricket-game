using System.Numerics;
using SuperCricket.Content;

namespace SuperCricket.Simulation;

public static class TeamRosterReviewChecks
{
    public static void Run()
    {
        var home = TeamRosterAsset.CreatePlaceholder("Coastal XI");
        home.ShortName = "COA";
        home.Players[0].Name = "Mira Sen";
        home.Players[2].Name = "Ishaan Rao";
        var away = TeamRosterAsset.CreatePlaceholder("Highland XI");
        away.ShortName = "HIG";
        Require(home.Validate().Count == 0 && away.Validate().Count == 0,
            "valid eleven-player rosters were rejected");
        Require(Vector3.DistanceSquared(home.PrimaryKitColor, new Vector3(0x15 / 255f, 0x75 / 255f, 0xB8 / 255f)) < 0.000001f &&
            Vector3.DistanceSquared(home.AccentKitColor, new Vector3(0xF2 / 255f, 0xBB / 255f, 0x46 / 255f)) < 0.000001f,
            "a valid #RRGGBB kit palette parsed to the wrong RGB values");
        RequireThrows(() => _ = TeamRosterAsset.ParseKitColor("blue"),
            "the kit palette parser accepted an invalid color");

        var duplicateOrder = TeamRosterAsset.CreatePlaceholder("Invalid XI");
        duplicateOrder.Players[1].BattingOrder = 1;
        Require(duplicateOrder.Validate().Any(error => error.Contains("batting order", StringComparison.OrdinalIgnoreCase)),
            "a duplicate batting position was accepted");
        var invalidRating = TeamRosterAsset.CreatePlaceholder("Invalid Rating XI");
        invalidRating.Players[0].Power = 101;
        Require(invalidRating.Validate().Any(error => error.Contains("power", StringComparison.OrdinalIgnoreCase)),
            "a rating above 100 was accepted");
        var invalidColor = TeamRosterAsset.CreatePlaceholder("Invalid Color XI");
        invalidColor.PrimaryKitColorHex = "blue";
        Require(invalidColor.Validate().Any(error => error.Contains("#RRGGBB", StringComparison.OrdinalIgnoreCase)),
            "a kit color outside #RRGGBB format was accepted");
        var invalidAccentColor = TeamRosterAsset.CreatePlaceholder("Invalid Accent XI");
        invalidAccentColor.AccentKitColorHex = "#12G45Z";
        Require(invalidAccentColor.Validate().Any(error => error.Contains("accent kit color", StringComparison.OrdinalIgnoreCase)),
            "an invalid accent kit color was accepted");
        RequireThrows(() => new LimitedOversMatch(duplicateOrder, away),
            "the match accepted an invalid roster");

        var match = new LimitedOversMatch(home, away);
        Require(match.StrikerPlayer.Name == "Mira Sen" && match.NonStrikerPlayer.BattingOrder == 2,
            "the first batting pair did not follow batting order");
        var bowled = match.BeginDelivery(isNoBall: false);
        bowled.ResolveIncoming(isWide: false, hitsWickets: true);
        match.CompleteDelivery();
        Require(match.StrikerPlayer.Name == "Ishaan Rao" && match.StrikerPlayer.BattingOrder == 3,
            "the incoming batter did not follow the roster after a wicket");

        var shot = new BattingShotData
        {
            Name = "drive",
            AnimationClip = "front-foot-drive",
            LaunchAngleDegrees = 12f,
            SpeedTransfer = 0.78f
        };
        var incoming = new Vector3(0f, 0f, -34f);
        var weakTiming = TeamRosterAsset.CreatePlaceholder("Timing XI").Players[0];
        weakTiming.Timing = 0;
        var strongTiming = TeamRosterAsset.CreatePlaceholder("Timing XI").Players[0];
        strongTiming.Timing = 100;
        var weakContact = BattingImpactModel.Calculate(incoming, Vector3.Zero, Vector2.One, shot, weakTiming);
        var strongContact = BattingImpactModel.Calculate(incoming, Vector3.Zero, Vector2.One, shot, strongTiming);
        Require(strongContact.ContactQuality > weakContact.ContactQuality,
            "the batter timing rating did not improve edge-contact quality");

        var lowPower = TeamRosterAsset.CreatePlaceholder("Power XI").Players[0];
        lowPower.Power = 0;
        var highPower = TeamRosterAsset.CreatePlaceholder("Power XI").Players[0];
        highPower.Power = 100;
        var lowPowerImpact = BattingImpactModel.Calculate(incoming, Vector3.Zero, Vector2.Zero, shot, lowPower);
        var highPowerImpact = BattingImpactModel.Calculate(incoming, Vector3.Zero, Vector2.Zero, shot, highPower);
        Require(highPowerImpact.OutgoingVelocity.Length() > lowPowerImpact.OutgoingVelocity.Length(),
            "the batter power rating did not increase the outgoing shot speed");
        RequireThrows(() => BattingImpactModel.Calculate(incoming, Vector3.Zero, Vector2.Zero, shot, invalidRating.Players[0]),
            "batting impact accepted an out-of-range player rating");

        Console.WriteLine("PASS: team roster validation, batting order, and player timing/power attributes.");
    }

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            return;
        }
        throw new InvalidOperationException($"Team roster review failed: {message}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"Team roster review failed: {message}.");
    }
}
