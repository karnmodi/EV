using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using UnitConversion.Api.Contracts;

namespace UnitConversion.Api.Tests;

public sealed class UnitsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public UnitsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    // Full unit list is complete and sorted by ordinal code.
    [Fact]
    public async Task List_IsOrdinalByCode()
    {
        var units = await _client.GetFromJsonAsync<List<UnitResponse>>("/api/v1/units");
        Assert.NotNull(units);
        var codes = units.Select(u => u.Code).ToArray();
        Assert.Equal(codes.OrderBy(c => c, StringComparer.Ordinal).ToArray(), codes);
        Assert.Equal(19, codes.Length);
    }

    // Case-insensitive category=volume returns only volume units in ordinal order.
    [Fact]
    public async Task List_ValidCategoryFilter()
    {
        var units = await _client.GetFromJsonAsync<List<UnitResponse>>("/api/v1/units?category=VOLUME");
        Assert.NotNull(units);
        Assert.All(units, u => Assert.Equal("volume", u.Category));
        Assert.Equal(new[] { "L", "gal_us", "mL" }.OrderBy(c => c, StringComparer.Ordinal), units.Select(u => u.Code));
    }

    // Empty, unknown, numeric, signed-numeric, comma-combined, or repeated category → 400 validation-error.
    [Theory]
    [InlineData("/api/v1/units?category=")]
    [InlineData("/api/v1/units?category=%20")]
    [InlineData("/api/v1/units?category=flux")]
    [InlineData("/api/v1/units?category=1")]
    [InlineData("/api/v1/units?category=%2B1")]
    [InlineData("/api/v1/units?category=-1")]
    [InlineData("/api/v1/units?category=Mass,Temperature")]
    [InlineData("/api/v1/units?category=length&category=mass")]
    public async Task List_InvalidCategory_IsValidationError(string url)
    {
        var response = await _client.GetAsync(url);
        await ProblemAssert.HasProblem(response, StatusCodes.Status400BadRequest, "validation-error");
    }

    // Unit detail by canonical code returns name, category, and aliases.
    [Fact]
    public async Task Get_ByCode()
    {
        var unit = await _client.GetFromJsonAsync<UnitResponse>("/api/v1/units/gal_us");
        Assert.NotNull(unit);
        Assert.Equal("gal_us", unit.Code);
        Assert.Equal("volume", unit.Category);
        Assert.Contains("us-gallon", unit.Aliases);
    }

    // Unit detail by word alias uses the same lookup as convert and returns the canonical unit.
    [Fact]
    public async Task Get_ByAlias()
    {
        var unit = await _client.GetFromJsonAsync<UnitResponse>("/api/v1/units/metre");
        Assert.NotNull(unit);
        Assert.Equal("m", unit.Code);
    }

    // Unknown unit on the detail route is 404 unit-not-found (not 400).
    [Fact]
    public async Task Get_Unknown_Is404()
    {
        var response = await _client.GetAsync("/api/v1/units/nope");
        await ProblemAssert.HasProblem(response, StatusCodes.Status404NotFound, "unit-not-found");
    }

    // 404 Problem Details keep application/problem+json across Accept header variants.
    [Theory]
    [InlineData(null)]
    [InlineData("application/json")]
    [InlineData("application/problem+json")]
    public async Task Get_Unknown_AcceptHeaderVariants(string? accept)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/units/nope");
        if (accept is not null)
        {
            request.Headers.Accept.ParseAdd(accept);
        }

        var response = await _client.SendAsync(request);
        await ProblemAssert.HasProblem(response, StatusCodes.Status404NotFound, "unit-not-found");
    }
}
