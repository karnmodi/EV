namespace UnitConversion.Domain;

public abstract record ConversionResult
{
    private ConversionResult()
    {
    }

    public sealed record Success(decimal Output, UnitDefinition From, UnitDefinition To) : ConversionResult;

    public sealed record Failure(ConversionError Error) : ConversionResult;
}
