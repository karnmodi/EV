namespace UnitConversion.Domain.Tests;

internal static class DecimalAssertions
{
    public static void Equal(decimal actual, decimal expected)
    {
        Assert.Equal(expected, actual);
    }

    public static void ApproximatelyEqual(decimal actual, decimal expected)
    {
        var tolerance = Math.Max(1e-20m, Math.Abs(expected) * 1e-24m);
        var delta = Math.Abs(actual - expected);
        Assert.True(
            delta <= tolerance,
            $"Expected {expected} ± {tolerance}, actual {actual} (delta {delta}).");
    }
}
