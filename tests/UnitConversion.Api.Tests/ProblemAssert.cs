using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace UnitConversion.Api.Tests;

internal static class ProblemAssert
{
    public static async Task HasProblem(
        HttpResponseMessage response,
        int status,
        string code)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(status, root.GetProperty("status").GetInt32());
        Assert.Equal($"urn:unitconversion:problem:{code}", root.GetProperty("type").GetString());
        Assert.Equal(code, root.GetProperty("code").GetString());
    }

    public static async Task HasHiddenInternalError(HttpResponseMessage response, string secret)
    {
        await HasProblem(response, StatusCodes.Status500InternalServerError, "internal-error");
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(secret, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", body, StringComparison.Ordinal);
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("traceId", out var trace));
        Assert.False(string.IsNullOrWhiteSpace(trace.GetString()));
    }
}
