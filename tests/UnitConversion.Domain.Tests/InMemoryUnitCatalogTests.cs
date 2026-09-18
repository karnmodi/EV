namespace UnitConversion.Domain.Tests;

public sealed class InMemoryUnitCatalogTests
{
    // Unfiltered list is every production unit, ordinal by code.
    [Fact]
    public void List_All_ReturnsEveryProductionUnitInOrdinalCodeOrder()
    {
        var catalog = new InMemoryUnitCatalog();
        var codes = catalog.List().Select(u => u.Code).ToArray();
        var expected = codes.OrderBy(c => c, StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, codes);
        Assert.Equal(19, codes.Length);
    }

    // Category filter returns only that category, still ordinal by code.
    [Theory]
    [InlineData(Category.Length, new[] { "cm", "ft", "in", "km", "m", "mi", "mm", "yd" })]
    [InlineData(Category.Mass, new[] { "g", "kg", "lb", "mg", "oz" })]
    [InlineData(Category.Temperature, new[] { "K", "degC", "degF" })]
    [InlineData(Category.Volume, new[] { "L", "gal_us", "mL" })]
    public void List_Category_ReturnsOnlyMatchingUnitsInOrdinalOrder(Category category, string[] expected)
    {
        var catalog = new InMemoryUnitCatalog();
        var codes = catalog.List(category).Select(u => u.Code).ToArray();
        Assert.Equal(expected.OrderBy(c => c, StringComparer.Ordinal).ToArray(), codes);
    }

    // Production word aliases resolve case-insensitively to the canonical code.
    [Theory]
    [InlineData("meter", "m")]
    [InlineData("metre", "m")]
    [InlineData("METRE", "m")]
    [InlineData("celsius", "degC")]
    [InlineData("fahrenheit", "degF")]
    [InlineData("kelvin", "K")]
    [InlineData("kilogram", "kg")]
    [InlineData("kilograms", "kg")]
    [InlineData("pound", "lb")]
    [InlineData("pounds", "lb")]
    [InlineData("litre", "L")]
    [InlineData("liter", "L")]
    [InlineData("us-gallon", "gal_us")]
    [InlineData("us-gallons", "gal_us")]
    public void TryGet_WordAlias_ResolvesCanonicalCode(string alias, string code)
    {
        var catalog = new InMemoryUnitCatalog();
        Assert.True(catalog.TryGet(alias, out var unit));
        Assert.Equal(code, unit!.Code);
    }

    // KM, unqualified gal/gallon, and blank tokens do not resolve.
    [Theory]
    [InlineData("KM")]
    [InlineData("gal")]
    [InlineData("gallon")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGet_UnknownOrBlank_ReturnsFalse(string token)
    {
        var catalog = new InMemoryUnitCatalog();
        Assert.False(catalog.TryGet(token, out _));
    }

    // Lookup trims surrounding whitespace then matches the canonical code.
    [Fact]
    public void TryGet_TrimsWhitespaceThenMatchesCode()
    {
        var catalog = new InMemoryUnitCatalog();
        Assert.True(catalog.TryGet("  m  ", out var unit));
        Assert.Equal("m", unit!.Code);
    }

    // mm and Mm may coexist as distinct canonical symbols.
    [Fact]
    public void SyntheticMmAndMmMegametre_RemainDistinct()
    {
        var units = new[]
        {
            new UnitDefinition("m", "metre", Category.Length, 1m, 0m),
            new UnitDefinition("mm", "millimetre", Category.Length, 0.001m, 0m),
            new UnitDefinition("Mm", "megametre", Category.Length, 1_000_000m, 0m)
        };
        var catalog = new InMemoryUnitCatalog(
            units,
            new Dictionary<Category, string> { [Category.Length] = "m" });

        Assert.True(catalog.TryGet("mm", out var milli));
        Assert.True(catalog.TryGet("Mm", out var mega));
        Assert.Equal("mm", milli!.Code);
        Assert.Equal("Mm", mega!.Code);
        Assert.NotEqual(milli.Factor, mega.Factor);
    }

    // Empty unit code is rejected at catalog construction.
    [Fact]
    public void Construction_RejectsEmptyCode()
    {
        Assert.Throws<ArgumentException>(() => Catalog(
            new UnitDefinition("", "metre", Category.Length, 1m, 0m)));
    }

    // Whitespace-padded codes are rejected (not silently trimmed).
    [Fact]
    public void Construction_RejectsWhitespacePaddedCode()
    {
        Assert.Throws<ArgumentException>(() => Catalog(
            new UnitDefinition(" m", "metre", Category.Length, 1m, 0m)));
    }

    // Blank or padded names are rejected.
    [Fact]
    public void Construction_RejectsEmptyName()
    {
        Assert.Throws<ArgumentException>(() => Catalog(
            new UnitDefinition("m", "  ", Category.Length, 1m, 0m)));
    }

    // Padded aliases are rejected.
    [Fact]
    public void Construction_RejectsPaddedAlias()
    {
        Assert.Throws<ArgumentException>(() => Catalog(
            new UnitDefinition("m", "metre", Category.Length, 1m, 0m, [" metre"])));
    }

    // Duplicate exact canonical codes are rejected.
    [Fact]
    public void Construction_RejectsDuplicateCodes()
    {
        Assert.Throws<ArgumentException>(() => new InMemoryUnitCatalog(
            [
                new UnitDefinition("m", "metre", Category.Length, 1m, 0m),
                new UnitDefinition("m", "metre again", Category.Length, 1m, 0m)
            ],
            new Dictionary<Category, string> { [Category.Length] = "m" }));
    }

    // Duplicate aliases on one unit (ignore case) are rejected.
    [Fact]
    public void Construction_RejectsDuplicateAliasOnSameUnit()
    {
        Assert.Throws<ArgumentException>(() => Catalog(
            new UnitDefinition("m", "metre", Category.Length, 1m, 0m, ["meter", "METER"])));
    }

    // The same alias cannot be claimed by two units (ignore case).
    [Fact]
    public void Construction_RejectsDuplicateAliasAcrossUnits()
    {
        Assert.Throws<ArgumentException>(() => new InMemoryUnitCatalog(
            [
                new UnitDefinition("m", "metre", Category.Length, 1m, 0m, ["length"]),
                new UnitDefinition("km", "kilometre", Category.Length, 1000m, 0m, ["LENGTH"])
            ],
            new Dictionary<Category, string> { [Category.Length] = "m" }));
    }

    // An alias may not collide with any canonical code ignoring case.
    [Fact]
    public void Construction_RejectsAliasMatchingAnyCanonicalCode()
    {
        Assert.Throws<ArgumentException>(() => new InMemoryUnitCatalog(
            [
                new UnitDefinition("m", "metre", Category.Length, 1m, 0m),
                new UnitDefinition("km", "kilometre", Category.Length, 1000m, 0m, ["M"])
            ],
            new Dictionary<Category, string> { [Category.Length] = "m" }));
    }

    // Factor must be positive.
    [Fact]
    public void Construction_RejectsNonpositiveFactor()
    {
        Assert.Throws<ArgumentException>(() => new InMemoryUnitCatalog(
            [
                new UnitDefinition("m", "metre", Category.Length, 1m, 0m),
                new UnitDefinition("x", "zero", Category.Length, 0m, 0m)
            ],
            new Dictionary<Category, string> { [Category.Length] = "m" }));
    }

    // Undefined Category enum values are rejected.
    [Fact]
    public void Construction_RejectsUndefinedCategory()
    {
        Assert.Throws<ArgumentException>(() => Catalog(
            new UnitDefinition("m", "metre", (Category)999, 1m, 0m)));
    }

    // Every category present in the catalog must declare a canonical base.
    [Fact]
    public void Construction_RejectsMissingCanonicalBase()
    {
        Assert.Throws<ArgumentException>(() => new InMemoryUnitCatalog(
            [new UnitDefinition("m", "metre", Category.Length, 1m, 0m)],
            new Dictionary<Category, string>()));
    }

    // A canonical-base declaration for a category with no units is rejected.
    [Fact]
    public void Construction_RejectsCanonicalBaseForMissingCategory()
    {
        Assert.Throws<ArgumentException>(() => new InMemoryUnitCatalog(
            [new UnitDefinition("m", "metre", Category.Length, 1m, 0m)],
            new Dictionary<Category, string>
            {
                [Category.Length] = "m",
                [Category.Mass] = "kg"
            }));
    }

    // Duplicate canonical-base entries for one category are rejected.
    [Fact]
    public void Construction_RejectsDuplicateCanonicalBaseDeclarations()
    {
        Assert.Throws<ArgumentException>(() => new InMemoryUnitCatalog(
            [new UnitDefinition("m", "metre", Category.Length, 1m, 0m)],
            new DuplicateCategoryBases()));
    }

    // Canonical base must have factor 1.
    [Fact]
    public void Construction_RejectsCanonicalBaseWithNonIdentityTransform()
    {
        Assert.Throws<ArgumentException>(() => new InMemoryUnitCatalog(
            [new UnitDefinition("m", "metre", Category.Length, 2m, 0m)],
            new Dictionary<Category, string> { [Category.Length] = "m" }));
    }

    // Canonical base must have offset 0.
    [Fact]
    public void Construction_RejectsCanonicalBaseWithOffset()
    {
        Assert.Throws<ArgumentException>(() => new InMemoryUnitCatalog(
            [new UnitDefinition("K", "kelvin", Category.Temperature, 1m, 1m)],
            new Dictionary<Category, string> { [Category.Temperature] = "K" }));
    }

    // Mutating caller lists/dictionaries after construction must not change the catalog.
    [Fact]
    public void Construction_CallerCannotMutateCatalogThroughInputCollections()
    {
        var aliases = new List<string> { "meter" };
        var units = new List<UnitDefinition>
        {
            new("m", "metre", Category.Length, 1m, 0m, aliases)
        };
        var bases = new Dictionary<Category, string> { [Category.Length] = "m" };
        var catalog = new InMemoryUnitCatalog(units, bases);

        units.Add(new UnitDefinition("km", "kilometre", Category.Length, 1000m, 0m));
        aliases.Add("extra");
        bases[Category.Mass] = "kg";

        Assert.False(catalog.TryGet("km", out _));
        Assert.False(catalog.TryGet("extra", out _));
        Assert.Single(catalog.List());
        Assert.Equal(["meter"], catalog.List()[0].Aliases);
    }

    // List() does not expose a mutable backing collection.
    [Fact]
    public void List_ReturnedCollectionIsNotMutatedByCaller()
    {
        var catalog = new InMemoryUnitCatalog();
        var listed = catalog.List();
        Assert.Throws<NotSupportedException>(() => ((IList<UnitDefinition>)listed).Add(
            new UnitDefinition("x", "x", Category.Length, 1m, 0m)));
        Assert.Equal(19, catalog.List().Count);
    }

    private static InMemoryUnitCatalog Catalog(UnitDefinition unit) =>
        new(new[] { unit }, new Dictionary<Category, string> { [unit.Category] = unit.Code });

    private sealed class DuplicateCategoryBases : IReadOnlyDictionary<Category, string>
    {
        private static readonly KeyValuePair<Category, string>[] Pairs =
        [
            new(Category.Length, "m"),
            new(Category.Length, "m")
        ];

        public string this[Category key] => "m";

        public IEnumerable<Category> Keys => Pairs.Select(p => p.Key);

        public IEnumerable<string> Values => Pairs.Select(p => p.Value);

        public int Count => Pairs.Length;

        public bool ContainsKey(Category key) => key == Category.Length;

        public IEnumerator<KeyValuePair<Category, string>> GetEnumerator() =>
            ((IEnumerable<KeyValuePair<Category, string>>)Pairs).GetEnumerator();

        public bool TryGetValue(Category key, out string value)
        {
            value = "m";
            return key == Category.Length;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
