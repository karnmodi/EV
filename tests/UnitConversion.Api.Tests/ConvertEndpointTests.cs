using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using UnitConversion.Api.Contracts;

namespace UnitConversion.Api.Tests;

public sealed class ConvertEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ConvertEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    // Happy-path convert: 0 °C → K returns the exact offset and canonical codes/categories.
    [Fact]
    public async Task Convert_Success_ReturnsCanonicalCodes()
    {
        var body = await _client.GetFromJsonAsync<ConvertResponse>(
            "/api/v1/convert?from=degC&to=K&value=0");
        Assert.NotNull(body);
        Assert.Equal(273.15m, body.Value);
        Assert.Equal("degC", body.From.Code);
        Assert.Equal("temperature", body.From.Category);
        Assert.Equal("K", body.To.Code);
        Assert.Equal(0m, body.Input);
    }

    // Word alias METRE must resolve to canonical m in both from and to.
    [Fact]
    public async Task Convert_WordAlias_ResolvesCanonicalMetre()
    {
        var body = await _client.GetFromJsonAsync<ConvertResponse>(
            "/api/v1/convert?from=METRE&to=m&value=2");
        Assert.NotNull(body);
        Assert.Equal(2m, body.Value);
        Assert.Equal("m", body.From.Code);
        Assert.Equal("m", body.To.Code);
        Assert.Equal("length", body.From.Category);
    }

    // Signed inputs are accepted; identity convert of -5 m stays -5.
    [Fact]
    public async Task Convert_SignedValue()
    {
        var body = await _client.GetFromJsonAsync<ConvertResponse>(
            "/api/v1/convert?from=m&to=m&value=-5");
        Assert.NotNull(body);
        Assert.Equal(-5m, body.Value);
    }

    // Tiny nonzero identity must round-trip without collapsing to zero.
    [Fact]
    public async Task Convert_TinyIdentity()
    {
        var body = await _client.GetFromJsonAsync<ConvertResponse>(
            "/api/v1/convert?from=m&to=m&value=0.00000000001");
        Assert.NotNull(body);
        Assert.Equal(0.00000000001m, body.Value);
        Assert.NotEqual(0m, body.Value);
    }

    // JSON-serialized output reused as the next value must round-trip within tolerance.
    [Fact]
    public async Task Convert_SerializedRoundTrip()
    {
        var forward = await _client.GetAsync("/api/v1/convert?from=ft&to=m&value=2.5");
        forward.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await forward.Content.ReadAsStringAsync());
        var serialized = doc.RootElement.GetProperty("value").GetRawText();
        var back = await _client.GetFromJsonAsync<ConvertResponse>(
            $"/api/v1/convert?from=m&to=ft&value={serialized}");
        Assert.NotNull(back);
        var delta = Math.Abs(back.Value - 2.5m);
        var tolerance = Math.Max(1e-20m, Math.Abs(2.5m) * 1e-24m);
        Assert.True(delta <= tolerance, $"round-trip {back.Value} vs 2.5, delta {delta}");
    }

    // Unknown source, destination, or case-mismatched symbol → 400 unit-not-found.
    [Theory]
    [InlineData("/api/v1/convert?from=nope&to=m&value=1")]
    [InlineData("/api/v1/convert?from=m&to=nope&value=1")]
    [InlineData("/api/v1/convert?from=KM&to=m&value=1")]
    public async Task Convert_UnknownUnit_Is400(string url)
    {
        var response = await _client.GetAsync(url);
        await ProblemAssert.HasProblem(response, StatusCodes.Status400BadRequest, "unit-not-found");
    }

    // Cross-category convert (length → mass) → 400 category-mismatch.
    [Fact]
    public async Task Convert_CategoryMismatch_Is400()
    {
        var response = await _client.GetAsync("/api/v1/convert?from=m&to=kg&value=1");
        await ProblemAssert.HasProblem(response, StatusCodes.Status400BadRequest, "category-mismatch");
    }

    public static TheoryData<string> MissingEmptyRepeated() => new()
    {
        "/api/v1/convert?to=m&value=1",
        "/api/v1/convert?from=m&value=1",
        "/api/v1/convert?from=m&to=m",
        "/api/v1/convert?from=&to=m&value=1",
        "/api/v1/convert?from=m&to=&value=1",
        "/api/v1/convert?from=m&to=m&value=",
        "/api/v1/convert?from=%20%20&to=m&value=1",
        "/api/v1/convert?from=m&to=%20&value=1",
        "/api/v1/convert?from=m&to=m&value=%20",
        "/api/v1/convert?from=m&from=km&to=m&value=1",
        "/api/v1/convert?from=m&to=m&to=km&value=1",
        "/api/v1/convert?from=m&to=m&value=1&value=2"
    };

    // Missing, empty, whitespace, or repeated from/to/value → 400 validation-error.
    [Theory]
    [MemberData(nameof(MissingEmptyRepeated))]
    public async Task Convert_InvalidQueryShape_IsValidationError(string url)
    {
        var response = await _client.GetAsync(url);
        await ProblemAssert.HasProblem(response, StatusCodes.Status400BadRequest, "validation-error");
    }

    public static TheoryData<string> MalformedNumbers() => new()
    {
        "/api/v1/convert?from=m&to=m&value=1,000",
        "/api/v1/convert?from=m&to=m&value=1e2",
        "/api/v1/convert?from=m&to=m&value=NaN",
        "/api/v1/convert?from=m&to=m&value=Infinity",
        "/api/v1/convert?from=m&to=m&value=.5",
        "/api/v1/convert?from=m&to=m&value=5."
    };

    // Commas, exponents, NaN, Infinity, and incomplete fractions → 400 validation-error.
    [Theory]
    [MemberData(nameof(MalformedNumbers))]
    public async Task Convert_MalformedNumber_IsValidationError(string url)
    {
        var response = await _client.GetAsync(url);
        await ProblemAssert.HasProblem(response, StatusCodes.Status400BadRequest, "validation-error");
    }

    // Encoded plus (%2B) is a valid leading sign on value.
    [Fact]
    public async Task Convert_EncodedLeadingPlus_Succeeds()
    {
        var body = await _client.GetFromJsonAsync<ConvertResponse>(
            "/api/v1/convert?from=m&to=m&value=%2B1");
        Assert.NotNull(body);
        Assert.Equal(1m, body.Value);
    }

    // Leading/trailing zeros and signed zero normalize and still parse as the same decimal.
    [Fact]
    public async Task Convert_TrailingZerosAndSignedZero_Normalize()
    {
        var zeros = await _client.GetFromJsonAsync<ConvertResponse>(
            "/api/v1/convert?from=m&to=m&value=001.2300");
        Assert.NotNull(zeros);
        Assert.Equal(1.23m, zeros.Value);

        var signedZero = await _client.GetFromJsonAsync<ConvertResponse>(
            "/api/v1/convert?from=m&to=m&value=-0");
        Assert.NotNull(signedZero);
        Assert.Equal(0m, signedZero.Value);
    }

    // Value larger than decimal.MaxValue → 400 numeric-out-of-range, not a parse crash.
    [Fact]
    public async Task Convert_DecimalOverflowOnParse_IsNumericOutOfRange()
    {
        var response = await _client.GetAsync(
            "/api/v1/convert?from=m&to=m&value=79228162514264337593543950336");
        await ProblemAssert.HasProblem(response, StatusCodes.Status400BadRequest, "numeric-out-of-range");
    }

    // Extra fractional digits that decimal cannot store exactly → 400 numeric-out-of-range.
    [Fact]
    public async Task Convert_PrecisionLoss_IsNumericOutOfRange()
    {
        var response = await _client.GetAsync(
            "/api/v1/convert?from=m&to=m&value=1.00000000000000000000000000001");
        await ProblemAssert.HasProblem(response, StatusCodes.Status400BadRequest, "numeric-out-of-range");
    }

    // Intermediate conversion overflow is a 400 numeric error, never a 500.
    [Fact]
    public async Task Convert_ArithmeticOverflow_Is400Not500()
    {
        var response = await _client.GetAsync(
            "/api/v1/convert?from=km&to=m&value=100000000000000000000000000");
        await ProblemAssert.HasProblem(response, StatusCodes.Status400BadRequest, "numeric-out-of-range");
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    // Validation errors stay application/problem+json for any Accept header (including none).
    [Theory]
    [InlineData(null)]
    [InlineData("application/json")]
    [InlineData("application/problem+json")]
    public async Task Convert_ValidationError_AcceptHeaderVariants(string? accept)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/convert?from=m&to=m");
        if (accept is not null)
        {
            request.Headers.Accept.ParseAdd(accept);
        }

        var response = await _client.SendAsync(request);
        await ProblemAssert.HasProblem(response, StatusCodes.Status400BadRequest, "validation-error");
    }
}
