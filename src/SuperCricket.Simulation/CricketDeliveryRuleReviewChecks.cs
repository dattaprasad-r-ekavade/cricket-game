namespace SuperCricket.Simulation;

public static class CricketDeliveryRuleReviewChecks
{
    public static void Run()
    {
        const float pitchWidth = 3.05f;
        var wideLimit = pitchWidth / 2f + CricketDeliveryRuleModel.WideClearanceMeters;
        Require(!CricketDeliveryRuleModel.IsWide(0f, pitchWidth), "a line at the stumps was called wide");
        Require(!CricketDeliveryRuleModel.IsWide(wideLimit, pitchWidth) &&
            !CricketDeliveryRuleModel.IsWide(-wideLimit, pitchWidth),
            "a ball exactly on the wide limit was called wide");
        Require(CricketDeliveryRuleModel.IsWide(MathF.BitIncrement(wideLimit), pitchWidth) &&
            CricketDeliveryRuleModel.IsWide(-MathF.BitIncrement(wideLimit), pitchWidth),
            "a ball beyond either side of the wide limit was not called wide");
        ExpectOutOfRange(() => CricketDeliveryRuleModel.IsWide(float.NaN, pitchWidth),
            "a non-finite wicket-line position was accepted");
        ExpectOutOfRange(() => CricketDeliveryRuleModel.IsWide(0f, 0f),
            "a non-positive pitch width was accepted");
        Console.WriteLine("PASS: shared wide-line rules handle both sides, exact limits, and invalid inputs.");
    }

    private static void ExpectOutOfRange(Action action, string message)
    {
        try
        {
            action();
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }
        throw new InvalidOperationException($"Delivery-rule check failed: {message}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"Delivery-rule check failed: {message}");
    }
}
