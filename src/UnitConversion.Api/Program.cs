using Microsoft.AspNetCore.Diagnostics;
using Microsoft.OpenApi;
using UnitConversion.Api;
using UnitConversion.Domain;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        if (context.ProblemDetails.Status == StatusCodes.Status500InternalServerError)
        {
            context.ProblemDetails.Title = "An unexpected error occurred.";
            context.ProblemDetails.Detail = $"Trace ID: {context.HttpContext.TraceIdentifier}";
            context.ProblemDetails.Type = ProblemTypes.InternalError;
            context.ProblemDetails.Extensions["code"] = "internal-error";
        }
    };
});
builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();
builder.Services.AddOpenApi(options =>
{
    options.AddOperationTransformer((operation, context, _) =>
    {
        var path = context.Description.RelativePath ?? "";
        var method = context.Description.HttpMethod ?? "";
        if (string.Equals(path, "api/v1/convert", StringComparison.OrdinalIgnoreCase))
        {
            operation.Parameters ??= [];
            AddQuery(operation, "from", "Source unit code or word alias.", required: true);
            AddQuery(operation, "to", "Destination unit code or word alias.", required: true);
            AddQuery(operation, "value", "Fixed-point decimal. Encode '+' as %2B.", required: true);
        }

        if (string.Equals(path, "api/v1/units", StringComparison.OrdinalIgnoreCase)
            && method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            operation.Parameters ??= [];
            AddQuery(operation, "category", "Optional category name: length, mass, temperature, volume.", required: false);
        }

        return Task.CompletedTask;
    });
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Paths ??= [];
        if (!document.Paths.ContainsKey("/health"))
        {
            document.Paths.Add("/health", new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>
                {
                    [HttpMethod.Get] = new OpenApiOperation
                    {
                        Summary = "Liveness health check.",
                        Responses = new OpenApiResponses
                        {
                            ["200"] = new OpenApiResponse { Description = "Plaintext Healthy" }
                        }
                    }
                }
            });
        }

        return Task.CompletedTask;
    });
});
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<IUnitCatalog, InMemoryUnitCatalog>();
builder.Services.AddSingleton<ConversionService>();

var app = builder.Build();
_ = app.Services.GetRequiredService<IUnitCatalog>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapOpenApi();
app.MapHealthChecks("/health");
app.MapConvertEndpoints();
app.Run();

static void AddQuery(OpenApiOperation operation, string name, string description, bool required)
{
    operation.Parameters ??= [];
    if (operation.Parameters.Any(p => string.Equals(p.Name, name, StringComparison.Ordinal)))
    {
        return;
    }

    operation.Parameters.Add(new OpenApiParameter
    {
        Name = name,
        In = ParameterLocation.Query,
        Required = required,
        Description = description,
        Schema = new OpenApiSchema { Type = JsonSchemaType.String }
    });
}

internal sealed class UnhandledExceptionHandler(ILogger<UnhandledExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception. TraceId {TraceId}", httpContext.TraceIdentifier);
        await ProblemTypes.Unexpected(httpContext).ExecuteAsync(httpContext);
        return true;
    }
}

public partial class Program;
