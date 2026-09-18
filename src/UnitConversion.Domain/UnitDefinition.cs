using System.Collections.Immutable;

namespace UnitConversion.Domain;

public sealed class UnitDefinition
{
    public UnitDefinition(
        string code,
        string name,
        Category category,
        decimal factor,
        decimal offset,
        IEnumerable<string>? aliases = null)
    {
        Code = code;
        Name = name;
        Category = category;
        Factor = factor;
        Offset = offset;
        Aliases = aliases is null
            ? ImmutableArray<string>.Empty
            : [.. aliases];
    }

    public string Code { get; }

    public string Name { get; }

    public Category Category { get; }

    public decimal Factor { get; }

    public decimal Offset { get; }

    public IReadOnlyList<string> Aliases { get; }
}
