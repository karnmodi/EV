using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace UnitConversion.Api.Tests;

public sealed class StaticPageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public StaticPageTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Root_ReturnsConverterHtml()
    {
        var response = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("id=\"convert-form\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"category\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"from\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"to\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"value\"", html, StringComparison.Ordinal);
    }
}
