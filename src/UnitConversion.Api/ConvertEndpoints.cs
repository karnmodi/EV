using Microsoft.AspNetCore.Http.HttpResults;
using UnitConversion.Api.Contracts;
using UnitConversion.Domain;

namespace UnitConversion.Api;

public static class ConvertEndpoints
{
    public static IEndpointRouteBuilder MapConvertEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1");

        group.MapGet("/convert", ConvertAsync)
            .WithName("Convert")
            .WithSummary("Convert a value between two units in the same category.")
            .WithDescription(
                "Query: from, to, value (each exactly once). value is a fixed-point decimal: optional + or -, digits, optional '.' and digits. Encode '+' as %2B.")
            .Produces<ConvertResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/units", ListUnits)
            .WithName("ListUnits")
            .WithSummary("List catalog units.")
            .WithDescription("Optional category filter: length, mass, temperature, or volume (case-insensitive, exactly once).")
            .Produces<IReadOnlyList<UnitResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/units/{code}", GetUnit)
            .WithName("GetUnit")
            .WithSummary("Look up one unit by code or word alias.")
            .Produces<UnitResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static Results<Ok<ConvertResponse>, ProblemHttpResult> ConvertAsync(
        HttpRequest request,
        ConversionService conversion,
        IUnitCatalog catalog)
    {
        var query = request.Query;
        var fromError = QueryValidation.ReadRequiredSingleton(query, "from", out var fromToken);
        if (fromError is not null)
        {
            return (ProblemHttpResult)fromError;
        }

        var toError = QueryValidation.ReadRequiredSingleton(query, "to", out var toToken);
        if (toError is not null)
        {
            return (ProblemHttpResult)toError;
        }

        var valueTokenError = QueryValidation.ReadRequiredSingleton(query, "value", out var valueToken);
        if (valueTokenError is not null)
        {
            return (ProblemHttpResult)valueTokenError;
        }

        var parseError = QueryValidation.ParseDecimal(valueToken, out var value);
        if (parseError is not null)
        {
            return (ProblemHttpResult)parseError;
        }

        if (!catalog.TryGet(fromToken, out var fromUnit))
        {
            return (ProblemHttpResult)ProblemTypes.ConvertUnitNotFound($"Unknown source unit '{fromToken}'.");
        }

        if (!catalog.TryGet(toToken, out var toUnit))
        {
            return (ProblemHttpResult)ProblemTypes.ConvertUnitNotFound($"Unknown destination unit '{toToken}'.");
        }

        var result = conversion.Convert(fromUnit.Code, toUnit.Code, value);
        return result switch
        {
            ConversionResult.Success success => TypedResults.Ok(new ConvertResponse(
                success.Output,
                ToRef(success.From),
                ToRef(success.To),
                value)),
            ConversionResult.Failure { Error: ConversionError.CategoryMismatch } =>
                (ProblemHttpResult)ProblemTypes.CategoriesDoNotMatch(
                    $"Cannot convert from {fromUnit.Category} to {toUnit.Category}."),
            ConversionResult.Failure { Error: ConversionError.NumericOutOfRange } =>
                (ProblemHttpResult)ProblemTypes.NumericRange("Conversion overflowed the decimal range."),
            ConversionResult.Failure { Error: ConversionError.UnitNotFound } =>
                (ProblemHttpResult)ProblemTypes.ConvertUnitNotFound("A unit could not be resolved."),
            _ => (ProblemHttpResult)ProblemTypes.Unexpected(request.HttpContext)
        };
    }

    private static Results<Ok<IReadOnlyList<UnitResponse>>, ProblemHttpResult> ListUnits(
        HttpRequest request,
        IUnitCatalog catalog)
    {
        var categoryError = QueryValidation.ReadOptionalSingleton(request.Query, "category", out var categoryName);
        if (categoryError is not null)
        {
            return (ProblemHttpResult)categoryError;
        }

        Category? filter = null;
        if (categoryName is not null)
        {
            var parseError = QueryValidation.ParseCategory(categoryName, out var category);
            if (parseError is not null)
            {
                return (ProblemHttpResult)parseError;
            }

            filter = category;
        }

        IReadOnlyList<UnitResponse> body = catalog.List(filter).Select(ToUnit).ToArray();
        return TypedResults.Ok(body);
    }

    private static Results<Ok<UnitResponse>, ProblemHttpResult> GetUnit(string code, IUnitCatalog catalog)
    {
        if (!catalog.TryGet(code, out var unit))
        {
            return (ProblemHttpResult)ProblemTypes.UnitDetailNotFound($"Unknown unit '{code}'.");
        }

        return TypedResults.Ok(ToUnit(unit));
    }

    private static UnitResponse ToUnit(UnitDefinition unit) =>
        new(unit.Code, unit.Name, CategoryName(unit.Category), unit.Aliases);

    private static UnitRefResponse ToRef(UnitDefinition unit) =>
        new(unit.Code, CategoryName(unit.Category));

    private static string CategoryName(Category category) =>
        category.ToString().ToLowerInvariant();
}
