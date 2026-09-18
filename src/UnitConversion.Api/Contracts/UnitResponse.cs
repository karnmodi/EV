namespace UnitConversion.Api.Contracts;

public sealed record UnitResponse(
    string Code,
    string Name,
    string Category,
    IReadOnlyList<string> Aliases);
