using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace UnitConversion.Domain;

public sealed class InMemoryUnitCatalog : IUnitCatalog
{
    // SI prefixes: https://www.bipm.org/en/measurement-units/si-prefixes
    // Length/mass/volume factors vs SI: NIST SP 811 Appendix B8
    // https://www.nist.gov/pml/special-publication-811/nist-guide-si-appendix-b-conversion-factors/nist-guide-si-appendix-b8
    private const decimal InchInMetres = 0.0254m;
    private const decimal InternationalFootInMetres = 0.3048m;
    private const decimal AvoirdupoisPoundInKilograms = 0.45359237m;
    private const decimal UsLiquidGallonInLitres = 3.785411784m;

    private static readonly decimal FahrenheitFactor = 5m / 9m;
    private static readonly decimal FahrenheitOffset = 273.15m - 32m * FahrenheitFactor;

    private readonly ImmutableArray<UnitDefinition> _units;
    private readonly ImmutableDictionary<string, UnitDefinition> _byCode;
    private readonly ImmutableDictionary<string, UnitDefinition> _byAlias;

    public InMemoryUnitCatalog()
        : this(CreateProductionUnits(), CreateProductionCanonicalBases())
    {
    }

    public InMemoryUnitCatalog(
        IEnumerable<UnitDefinition> units,
        IReadOnlyDictionary<Category, string> canonicalBaseCodes)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(canonicalBaseCodes);

        var unitList = units.ToList();
        Validate(unitList, canonicalBaseCodes);

        _units = [.. unitList.OrderBy(u => u.Code, StringComparer.Ordinal)];
        _byCode = _units.ToImmutableDictionary(u => u.Code, StringComparer.Ordinal);
        _byAlias = BuildAliasIndex(_units);
    }

    public bool TryGet(string codeOrAlias, [NotNullWhen(true)] out UnitDefinition? unit)
    {
        unit = null;
        if (codeOrAlias is null)
        {
            return false;
        }

        var token = codeOrAlias.Trim();
        if (token.Length == 0)
        {
            return false;
        }

        if (_byCode.TryGetValue(token, out unit))
        {
            return true;
        }

        return _byAlias.TryGetValue(token, out unit);
    }

    public IReadOnlyList<UnitDefinition> List(Category? category = null)
    {
        if (category is null)
        {
            return _units;
        }

        return _units.Where(u => u.Category == category.Value).ToImmutableArray();
    }

    private static ImmutableDictionary<string, UnitDefinition> BuildAliasIndex(
        ImmutableArray<UnitDefinition> units)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, UnitDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var unit in units)
        {
            foreach (var alias in unit.Aliases)
            {
                builder.Add(alias, unit);
            }
        }

        return builder.ToImmutable();
    }

    private static void Validate(
        IReadOnlyList<UnitDefinition> units,
        IReadOnlyDictionary<Category, string> canonicalBaseCodes)
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var categoriesInUse = new HashSet<Category>();

        foreach (var unit in units)
        {
            ArgumentNullException.ThrowIfNull(unit);

            if (!Enum.IsDefined(unit.Category))
            {
                throw new ArgumentException("Catalog contains an undefined category.");
            }

            RejectPaddedOrEmpty(unit.Code, "code");
            RejectPaddedOrEmpty(unit.Name, "name");

            if (unit.Factor <= 0m)
            {
                throw new ArgumentException($"Unit '{unit.Code}' must have a positive factor.");
            }

            if (!codes.Add(unit.Code))
            {
                throw new ArgumentException($"Duplicate unit code '{unit.Code}'.");
            }

            var unitAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var alias in unit.Aliases)
            {
                RejectPaddedOrEmpty(alias, "alias");
                if (!unitAliases.Add(alias))
                {
                    throw new ArgumentException($"Duplicate alias '{alias}' on unit '{unit.Code}'.");
                }

                if (!aliases.Add(alias))
                {
                    throw new ArgumentException($"Duplicate alias '{alias}'.");
                }
            }

            categoriesInUse.Add(unit.Category);
        }

        foreach (var alias in aliases)
        {
            foreach (var code in codes)
            {
                if (string.Equals(alias, code, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"Alias '{alias}' collides with a canonical code.");
                }
            }
        }

        var baseCategories = new HashSet<Category>();
        foreach (var pair in canonicalBaseCodes)
        {
            if (!Enum.IsDefined(pair.Key))
            {
                throw new ArgumentException("Canonical base declared for an undefined category.");
            }

            if (!baseCategories.Add(pair.Key))
            {
                throw new ArgumentException($"Duplicate canonical base for category '{pair.Key}'.");
            }

            RejectPaddedOrEmpty(pair.Value, "canonical base code");
        }

        if (!categoriesInUse.SetEquals(baseCategories))
        {
            throw new ArgumentException("Canonical bases must match the categories present in the catalog.");
        }

        foreach (var pair in canonicalBaseCodes)
        {
            var match = units.FirstOrDefault(u =>
                u.Category == pair.Key && string.Equals(u.Code, pair.Value, StringComparison.Ordinal));
            if (match is null)
            {
                throw new ArgumentException($"Canonical base '{pair.Value}' is not defined for category '{pair.Key}'.");
            }

            if (match.Factor != 1m || match.Offset != 0m)
            {
                throw new ArgumentException($"Canonical base '{match.Code}' must have factor 1 and offset 0.");
            }
        }
    }

    private static void RejectPaddedOrEmpty(string? value, string field)
    {
        if (string.IsNullOrEmpty(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException($"Unit {field} must be nonempty and not whitespace-padded.");
        }
    }

    private static IReadOnlyDictionary<Category, string> CreateProductionCanonicalBases() =>
        new Dictionary<Category, string>
        {
            [Category.Length] = "m",
            [Category.Mass] = "kg",
            [Category.Temperature] = "K",
            [Category.Volume] = "L"
        };

    private static IEnumerable<UnitDefinition> CreateProductionUnits()
    {
        yield return new UnitDefinition("m", "metre", Category.Length, 1m, 0m, ["meter", "metre"]);
        yield return new UnitDefinition("km", "kilometre", Category.Length, 1000m, 0m);
        yield return new UnitDefinition("cm", "centimetre", Category.Length, 0.01m, 0m);
        yield return new UnitDefinition("mm", "millimetre", Category.Length, 0.001m, 0m);
        yield return new UnitDefinition(
            "ft",
            "international foot",
            Category.Length,
            InternationalFootInMetres,
            0m);
        yield return new UnitDefinition("in", "inch", Category.Length, InchInMetres, 0m);
        yield return new UnitDefinition("yd", "international yard", Category.Length, 0.9144m, 0m);
        yield return new UnitDefinition("mi", "international mile", Category.Length, 1609.344m, 0m);

        yield return new UnitDefinition("kg", "kilogram", Category.Mass, 1m, 0m, ["kilogram", "kilograms"]);
        yield return new UnitDefinition("g", "gram", Category.Mass, 0.001m, 0m);
        yield return new UnitDefinition("mg", "milligram", Category.Mass, 0.000001m, 0m);
        yield return new UnitDefinition(
            "lb",
            "avoirdupois pound",
            Category.Mass,
            AvoirdupoisPoundInKilograms,
            0m,
            ["pound", "pounds"]);
        yield return new UnitDefinition(
            "oz",
            "avoirdupois ounce",
            Category.Mass,
            AvoirdupoisPoundInKilograms / 16m,
            0m);

        yield return new UnitDefinition("K", "kelvin", Category.Temperature, 1m, 0m, ["kelvin"]);
        yield return new UnitDefinition("degC", "degree Celsius", Category.Temperature, 1m, 273.15m, ["celsius"]);
        yield return new UnitDefinition(
            "degF",
            "degree Fahrenheit",
            Category.Temperature,
            FahrenheitFactor,
            FahrenheitOffset,
            ["fahrenheit"]);

        yield return new UnitDefinition("L", "litre", Category.Volume, 1m, 0m, ["litre", "liter"]);
        yield return new UnitDefinition("mL", "millilitre", Category.Volume, 0.001m, 0m);
        yield return new UnitDefinition(
            "gal_us",
            "US liquid gallon",
            Category.Volume,
            UsLiquidGallonInLitres,
            0m,
            ["us-gallon", "us-gallons"]);
    }
}
