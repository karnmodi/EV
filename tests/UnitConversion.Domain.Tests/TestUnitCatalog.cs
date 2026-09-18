using System.Diagnostics.CodeAnalysis;

namespace UnitConversion.Domain.Tests;

public sealed class TestUnitCatalog : IUnitCatalog
{
    public const string FurlongCode = "fur";

    // 1 international furlong = 220 yd; 1 yd = 3 × 0.3048 m (NIST SP 811 international foot).
    public const decimal FurlongInMetres = 201.168m;

    private readonly UnitDefinition _metre = new("m", "metre", Category.Length, 1m, 0m);
    private readonly UnitDefinition _furlong;

    public TestUnitCatalog()
    {
        _furlong = new UnitDefinition(FurlongCode, "furlong", Category.Length, FurlongInMetres, 0m);
    }

    public bool TryGet(string codeOrAlias, [NotNullWhen(true)] out UnitDefinition? unit)
    {
        unit = null;
        if (codeOrAlias is null)
        {
            return false;
        }

        var token = codeOrAlias.Trim();
        if (string.Equals(token, _metre.Code, StringComparison.Ordinal))
        {
            unit = _metre;
            return true;
        }

        if (string.Equals(token, _furlong.Code, StringComparison.Ordinal))
        {
            unit = _furlong;
            return true;
        }

        return false;
    }

    public IReadOnlyList<UnitDefinition> List(Category? category = null)
    {
        UnitDefinition[] all = [_metre, _furlong];
        if (category is null)
        {
            return all;
        }

        return all.Where(u => u.Category == category.Value).ToArray();
    }
}
