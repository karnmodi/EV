namespace UnitConversion.Api.Contracts;

public sealed record UnitRefResponse(string Code, string Category);

public sealed record ConvertResponse(
    decimal Value,
    UnitRefResponse From,
    UnitRefResponse To,
    decimal Input);
