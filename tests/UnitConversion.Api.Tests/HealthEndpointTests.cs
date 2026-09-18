using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace UnitConversion.Api.Tests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    // Liveness endpoint returns 200 plaintext Healthy.
    [Fact]
    public async Task Health_ReturnsHealthyPlaintext()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    // OpenAPI lists convert/units/health routes, query parameters, and error responses.
    [Fact]
    public async Task OpenApi_DocumentsRoutesParametersAndErrors()
    {
        var json = await _client.GetStringAsync("/openapi/v1.json");
        using var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/convert", out var convert));
        var convertGet = convert.GetProperty("get");
        AssertQueryParam(convertGet, "from");
        AssertQueryParam(convertGet, "to");
        AssertQueryParam(convertGet, "value");
        Assert.True(convertGet.GetProperty("responses").TryGetProperty("400", out _));
        Assert.True(convertGet.GetProperty("responses").TryGetProperty("500", out _));

        Assert.True(paths.TryGetProperty("/api/v1/units", out var units));
        var unitsGet = units.GetProperty("get");
        AssertQueryParam(unitsGet, "category");
        Assert.True(unitsGet.GetProperty("responses").TryGetProperty("400", out _));

        Assert.True(paths.TryGetProperty("/api/v1/units/{code}", out var unit));
        Assert.True(unit.GetProperty("get").GetProperty("responses").TryGetProperty("404", out _));

        Assert.True(paths.TryGetProperty("/health", out _));
    }

    private static void AssertQueryParam(JsonElement operation, string name)
    {
        Assert.Contains(
            operation.GetProperty("parameters").EnumerateArray(),
            p => p.GetProperty("name").GetString() == name
                 && p.GetProperty("in").GetString() == "query");
    }
}
