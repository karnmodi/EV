using Microsoft.AspNetCore.Mvc;

namespace UnitConversion.Api;

public static class ProblemTypes
{
    public const string ValidationError = "urn:unitconversion:problem:validation-error";
    public const string NumericOutOfRange = "urn:unitconversion:problem:numeric-out-of-range";
    public const string UnitNotFound = "urn:unitconversion:problem:unit-not-found";
    public const string CategoryMismatch = "urn:unitconversion:problem:category-mismatch";
    public const string InternalError = "urn:unitconversion:problem:internal-error";

    public static IResult Validation(string detail) =>
        Problem(StatusCodes.Status400BadRequest, "Validation error", detail, ValidationError, "validation-error");

    public static IResult NumericRange(string detail) =>
        Problem(StatusCodes.Status400BadRequest, "Numeric value out of range", detail, NumericOutOfRange, "numeric-out-of-range");

    public static IResult ConvertUnitNotFound(string detail) =>
        Problem(StatusCodes.Status400BadRequest, "Unit not found", detail, UnitNotFound, "unit-not-found");

    public static IResult UnitDetailNotFound(string detail) =>
        Problem(StatusCodes.Status404NotFound, "Unit not found", detail, UnitNotFound, "unit-not-found");

    public static IResult CategoriesDoNotMatch(string detail) =>
        Problem(StatusCodes.Status400BadRequest, "Category mismatch", detail, CategoryMismatch, "category-mismatch");

    public static IResult Unexpected(HttpContext http) =>
        Problem(
            StatusCodes.Status500InternalServerError,
            "An unexpected error occurred.",
            $"Trace ID: {http.TraceIdentifier}",
            InternalError,
            "internal-error");

    private static IResult Problem(int status, string title, string detail, string type, string code) =>
        TypedResults.Problem(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Type = type,
            Extensions = { ["code"] = code }
        });
}
