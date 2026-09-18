using Microsoft.AspNetCore.Mvc.Testing;

namespace UnitConversion.Api.Tests;

public sealed class StartupValidationTests
{
    // Catalog throw on request → generic 500 with trace id, no exception text, in Dev and Production.
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task UnexpectedException_IsGeneric500(string environment)
    {
        await using var factory = new ThrowingCatalogFactory(environment);
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/convert?from=m&to=m&value=1");
        var response = await client.SendAsync(request);
        await ProblemAssert.HasHiddenInternalError(response, ThrowingUnitCatalog.Secret);
    }

    // Unexpected 500 still uses Problem Details for every Accept header variant.
    [Theory]
    [InlineData(null)]
    [InlineData("application/json")]
    [InlineData("application/problem+json")]
    public async Task UnexpectedException_AcceptHeaderVariants(string? accept)
    {
        await using var factory = new ThrowingCatalogFactory("Production");
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/convert?from=m&to=m&value=1");
        if (accept is not null)
        {
            request.Headers.Accept.ParseAdd(accept);
        }

        var response = await client.SendAsync(request);
        await ProblemAssert.HasHiddenInternalError(response, ThrowingUnitCatalog.Secret);
    }

    // Invalid production-style catalog must fail host startup, not serve traffic.
    [Fact]
    public async Task InvalidCatalog_PreventsHostStartup()
    {
        await using var factory = new InvalidCatalogFactory();
        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
        {
            _ = factory.CreateClient();
            return Task.CompletedTask;
        });
        Assert.Contains("factor", ex.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
