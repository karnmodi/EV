namespace UnitConversion.Domain.Tests;

public sealed class ConversionServiceTests
{
    // NIST SP 811 Appendix B8:
    // https://www.nist.gov/pml/special-publication-811/nist-guide-si-appendix-b-conversion-factors/nist-guide-si-appendix-b8
    // SI prefixes: https://www.bipm.org/en/measurement-units/si-prefixes
    private const decimal InchToMetre = 0.0254m;
    private const decimal FootToMetre = 0.3048m;
    private const decimal YardToMetre = 0.9144m;
    private const decimal MileToMetre = 1609.344m;
    private const decimal PoundToKilogram = 0.45359237m;
    private const decimal OunceToKilogram = 0.028349523125m;
    private const decimal UsLiquidGallonToLitre = 3.785411784m;

    private static readonly string[] ProductionCodes =
    [
        "m", "km", "cm", "mm", "ft", "in", "yd", "mi",
        "kg", "g", "mg", "lb", "oz",
        "K", "degC", "degF",
        "L", "mL", "gal_us"
    ];

    private readonly ConversionService _service = new(new InMemoryUnitCatalog());

    public static TheoryData<string, string, decimal, decimal> KnownAnswersToBase() => new()
    {
        { "m", "m", 1m, 1m },
        { "km", "m", 1m, 1000m },
        { "cm", "m", 1m, 0.01m },
        { "mm", "m", 1m, 0.001m },
        { "in", "m", 1m, InchToMetre },
        { "ft", "m", 1m, FootToMetre },
        { "yd", "m", 1m, YardToMetre },
        { "mi", "m", 1m, MileToMetre },
        { "kg", "kg", 1m, 1m },
        { "g", "kg", 1m, 0.001m },
        { "mg", "kg", 1m, 0.000001m },
        { "lb", "kg", 1m, PoundToKilogram },
        { "oz", "kg", 1m, OunceToKilogram },
        { "K", "K", 1m, 1m },
        { "degC", "K", 0m, 273.15m },
        { "degF", "K", 1m, (1m - 32m) * (5m / 9m) + 273.15m },
        { "L", "L", 1m, 1m },
        { "mL", "L", 1m, 0.001m },
        { "gal_us", "L", 1m, UsLiquidGallonToLitre }
    };

    // Independently sourced known-answers: each production unit converts to its canonical base.
    [Theory]
    [MemberData(nameof(KnownAnswersToBase))]
    public void KnownAnswer_ConvertsToCanonicalBase(string from, string to, decimal input, decimal expected)
    {
        var output = RequireSuccess(_service.Convert(from, to, input));
        DecimalAssertions.Equal(output, expected);
    }

    // Guards against a catalog unit that has no corresponding known-answer fixture.
    [Fact]
    public void ProductionCatalog_EveryRegisteredUnitHasAKnownAnswer()
    {
        var catalog = new InMemoryUnitCatalog();
        var codes = catalog.List().Select(u => u.Code).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(ProductionCodes.ToHashSet(StringComparer.Ordinal), codes);
    }

    // Standard Celsius–Fahrenheit checkpoints including the −40 identity.
    [Theory]
    [InlineData(0, 32)]
    [InlineData(100, 212)]
    [InlineData(-40, -40)]
    public void Temperature_CelsiusToFahrenheit(decimal celsius, decimal fahrenheit)
    {
        var output = RequireSuccess(_service.Convert("degC", "degF", celsius));
        DecimalAssertions.ApproximatelyEqual(output, fahrenheit);
    }

    // 0 °C is exactly 273.15 K (offset only, no factor scaling).
    [Fact]
    public void Temperature_ZeroCelsiusIs273_15Kelvin()
    {
        var output = RequireSuccess(_service.Convert("degC", "K", 0m));
        DecimalAssertions.Equal(output, 273.15m);
    }

    // Below absolute zero is allowed; conversion is mathematical, not physical.
    [Fact]
    public void Temperature_NegativeKelvinIsAllowed()
    {
        var output = RequireSuccess(_service.Convert("K", "degC", -10m));
        DecimalAssertions.Equal(output, -283.15m);
    }

    public static TheoryData<string, decimal> IdentityCases()
    {
        var data = new TheoryData<string, decimal>();
        decimal[] values =
        [
            0m,
            -2.5m,
            0.125m,
            0.00000000001m,
            decimal.MinValue,
            decimal.MaxValue
        ];
        foreach (var code in ProductionCodes)
        {
            foreach (var value in values)
            {
                data.Add(code, value);
            }
        }

        return data;
    }

    // Same-unit convert returns the input unchanged, including min/max decimal.
    [Theory]
    [MemberData(nameof(IdentityCases))]
    public void Identity_ReturnsInputUnchanged(string code, decimal value)
    {
        var output = RequireSuccess(_service.Convert(code, code, value));
        DecimalAssertions.Equal(output, value);
    }

    // Subnormal-looking tiny identity stays nonzero (not rounded away).
    [Fact]
    public void TinyNonzeroIdentity_StaysNonzero()
    {
        const decimal tiny = 0.00000000001m;
        var output = RequireSuccess(_service.Convert("m", "m", tiny));
        DecimalAssertions.Equal(output, tiny);
        Assert.NotEqual(0m, output);
    }

    // Native decimal underflow: a tiny mm→km conversion may become zero.
    [Fact]
    public void NativeDecimalUnderflow_TinyMillimetreToKilometreIsZero()
    {
        var output = RequireSuccess(_service.Convert("mm", "km", 1e-28m));
        DecimalAssertions.Equal(output, 0m);
    }

    public static TheoryData<string, string, decimal, decimal> ForwardCases() => new()
    {
        { "in", "m", 2m, 0.0508m },
        { "lb", "kg", 1m, PoundToKilogram },
        { "gal_us", "L", 1m, UsLiquidGallonToLitre },
        { "ft", "yd", 3m, 1m }
    };

    // Forward conversions (inch, pound, US gallon, foot→yard) match independent expectations.
    [Theory]
    [MemberData(nameof(ForwardCases))]
    public void ForwardConversion_MatchesIndependentExpectation(
        string from,
        string to,
        decimal input,
        decimal expected)
    {
        var output = RequireSuccess(_service.Convert(from, to, input));
        DecimalAssertions.Equal(output, expected);
    }

    public static TheoryData<string, string, decimal, decimal> ReverseCases() => new()
    {
        { "m", "in", 0.0508m, 2m },
        { "kg", "lb", PoundToKilogram, 1m },
        { "L", "gal_us", UsLiquidGallonToLitre, 1m },
        { "yd", "ft", 1m, 3m }
    };

    // Reverse of those same fixtures must invert back to the original quantity.
    [Theory]
    [MemberData(nameof(ReverseCases))]
    public void ReverseConversion_MatchesIndependentExpectation(
        string from,
        string to,
        decimal input,
        decimal expected)
    {
        var output = RequireSuccess(_service.Convert(from, to, input));
        DecimalAssertions.Equal(output, expected);
    }

    // A→B→A round-trip stays within the approximate equality tolerance.
    [Theory]
    [InlineData("ft", "m", -4.5)]
    [InlineData("lb", "oz", 2.25)]
    [InlineData("gal_us", "mL", 0.5)]
    [InlineData("degC", "degF", 37)]
    public void RoundTrip_ReturnsCloseToOriginal(string from, string to, decimal value)
    {
        var forward = RequireSuccess(_service.Convert(from, to, value));
        var back = RequireSuccess(_service.Convert(to, from, forward));
        DecimalAssertions.ApproximatelyEqual(back, value);
    }

    // Huge km→m overflows; the same value as km→km identity still succeeds.
    [Fact]
    public void IntermediateOverflow_KmToMetresIsOutOfRange_IdentitySucceeds()
    {
        const decimal value = 100000000000000000000000000m;
        Assert.Equal(
            ConversionError.NumericOutOfRange,
            RequireFailure(_service.Convert("km", "m", value)));
        DecimalAssertions.Equal(RequireSuccess(_service.Convert("km", "km", value)), value);
    }

    // MaxValue metres to millimetres overflows on output scaling.
    [Fact]
    public void OutputOverflow_MaxMetresToMillimetresIsOutOfRange()
    {
        Assert.Equal(
            ConversionError.NumericOutOfRange,
            RequireFailure(_service.Convert("m", "mm", decimal.MaxValue)));
    }

    // Unknown tokens (including KM as a code) yield UnitNotFound.
    [Theory]
    [InlineData("nope", "m")]
    [InlineData("m", "nope")]
    [InlineData("KM", "m")]
    public void UnknownUnit_ReturnsUnitNotFound(string from, string to)
    {
        Assert.Equal(ConversionError.UnitNotFound, RequireFailure(_service.Convert(from, to, 1m)));
    }

    // Length vs mass is a category mismatch, not a missing unit.
    [Fact]
    public void CategoryMismatch_ReturnsError()
    {
        Assert.Equal(ConversionError.CategoryMismatch, RequireFailure(_service.Convert("m", "kg", 1m)));
    }

    // meter/metre/METRE are word aliases for m; conversion uses the resolved unit.
    [Theory]
    [InlineData("meter")]
    [InlineData("metre")]
    [InlineData("METRE")]
    public void WordAliases_ResolveToMetre(string token)
    {
        var output = RequireSuccess(_service.Convert(token, "m", 2m));
        DecimalAssertions.Equal(output, 2m);
    }

    // Canonical codes are case-sensitive: KM is not km.
    [Fact]
    public void UppercaseKilometreCode_DoesNotResolve()
    {
        Assert.Equal(ConversionError.UnitNotFound, RequireFailure(_service.Convert("KM", "m", 1m)));
    }

    // Unchanged ConversionService converts a novel furlong from TestUnitCatalog.
    [Fact]
    public void TestUnitCatalog_NovelFurlongConvertsWithoutChangingService()
    {
        var service = new ConversionService(new TestUnitCatalog());
        var output = RequireSuccess(service.Convert(TestUnitCatalog.FurlongCode, "m", 1m));
        DecimalAssertions.Equal(output, TestUnitCatalog.FurlongInMetres);
    }

    private static decimal RequireSuccess(ConversionResult result)
    {
        var success = Assert.IsType<ConversionResult.Success>(result);
        return success.Output;
    }

    private static ConversionError RequireFailure(ConversionResult result)
    {
        var failure = Assert.IsType<ConversionResult.Failure>(result);
        return failure.Error;
    }
}
