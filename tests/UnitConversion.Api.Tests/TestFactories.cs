using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UnitConversion.Domain;

namespace UnitConversion.Api.Tests;

internal sealed class ThrowingCatalogFactory(string environment) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IUnitCatalog>();
            services.RemoveAll<ConversionService>();
            services.AddSingleton<IUnitCatalog, ThrowingUnitCatalog>();
            services.AddSingleton<ConversionService>();
        });
    }
}

internal sealed class InvalidCatalogFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IUnitCatalog>();
            services.RemoveAll<ConversionService>();
            services.AddSingleton<IUnitCatalog>(_ => new InMemoryUnitCatalog(
                [new UnitDefinition("m", "metre", Category.Length, 2m, 0m)],
                new Dictionary<Category, string> { [Category.Length] = "m" }));
            services.AddSingleton<ConversionService>();
        });
    }
}
