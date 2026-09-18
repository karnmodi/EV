using System.Diagnostics.CodeAnalysis;
using UnitConversion.Domain;

namespace UnitConversion.Api.Tests;

internal sealed class ThrowingUnitCatalog : IUnitCatalog
{
    public const string Secret = "throwing-catalog-secret-marker";

    public bool TryGet(string codeOrAlias, [NotNullWhen(true)] out UnitDefinition? unit)
    {
        unit = null;
        throw new InvalidOperationException(Secret);
    }

    public IReadOnlyList<UnitDefinition> List(Category? category = null) =>
        throw new InvalidOperationException(Secret);
}
