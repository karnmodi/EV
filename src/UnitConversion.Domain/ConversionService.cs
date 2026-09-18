namespace UnitConversion.Domain;

public sealed class ConversionService
{
    private readonly IUnitCatalog _catalog;

    public ConversionService(IUnitCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    public ConversionResult Convert(string from, string to, decimal value)
    {
        if (!_catalog.TryGet(from, out var fromUnit))
        {
            return new ConversionResult.Failure(ConversionError.UnitNotFound);
        }

        if (!_catalog.TryGet(to, out var toUnit))
        {
            return new ConversionResult.Failure(ConversionError.UnitNotFound);
        }

        if (fromUnit.Category != toUnit.Category)
        {
            return new ConversionResult.Failure(ConversionError.CategoryMismatch);
        }

        if (string.Equals(fromUnit.Code, toUnit.Code, StringComparison.Ordinal))
        {
            return new ConversionResult.Success(value, fromUnit, toUnit);
        }

        try
        {
            var toBase = value * fromUnit.Factor + fromUnit.Offset;
            var output = (toBase - toUnit.Offset) / toUnit.Factor;
            return new ConversionResult.Success(output, fromUnit, toUnit);
        }
        catch (OverflowException)
        {
            return new ConversionResult.Failure(ConversionError.NumericOutOfRange);
        }
    }
}
