using System.Diagnostics.CodeAnalysis;

namespace UnitConversion.Domain;

public interface IUnitCatalog
{
    bool TryGet(string codeOrAlias, [NotNullWhen(true)] out UnitDefinition? unit);

    IReadOnlyList<UnitDefinition> List(Category? category = null);
}
