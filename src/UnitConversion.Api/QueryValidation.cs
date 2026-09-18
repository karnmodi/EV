using System.Globalization;
using System.Text.RegularExpressions;
using UnitConversion.Domain;

namespace UnitConversion.Api;

public static partial class QueryValidation
{
    [GeneratedRegex(@"^[+-]?[0-9]+(\.[0-9]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex DecimalSyntax();

    public static IResult? ReadRequiredSingleton(IQueryCollection query, string name, out string trimmed)
    {
        trimmed = "";
        if (!query.TryGetValue(name, out var values) || values.Count == 0)
        {
            return ProblemTypes.Validation($"Query parameter '{name}' is required.");
        }

        if (values.Count != 1)
        {
            return ProblemTypes.Validation($"Query parameter '{name}' must be specified exactly once.");
        }

        var raw = values[0];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ProblemTypes.Validation($"Query parameter '{name}' must be nonempty.");
        }

        trimmed = raw.Trim();
        return null;
    }

    public static IResult? ReadOptionalSingleton(IQueryCollection query, string name, out string? trimmed)
    {
        trimmed = null;
        if (!query.TryGetValue(name, out var values) || values.Count == 0)
        {
            return null;
        }

        if (values.Count != 1)
        {
            return ProblemTypes.Validation($"Query parameter '{name}' must be specified exactly once.");
        }

        var raw = values[0];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ProblemTypes.Validation($"Query parameter '{name}' must be nonempty.");
        }

        trimmed = raw.Trim();
        return null;
    }

    public static IResult? ParseDecimal(string raw, out decimal value)
    {
        value = default;
        if (!DecimalSyntax().IsMatch(raw))
        {
            return ProblemTypes.Validation("Query parameter 'value' must be a fixed-point decimal (optional sign, digits, optional fraction).");
        }

        if (!decimal.TryParse(
                raw,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out value))
        {
            return ProblemTypes.NumericRange("Query parameter 'value' is outside the decimal range.");
        }

        var formatted = value.ToString("0.############################", CultureInfo.InvariantCulture);
        if (!string.Equals(NormalizeFixedPoint(raw), NormalizeFixedPoint(formatted), StringComparison.Ordinal))
        {
            return ProblemTypes.NumericRange("Query parameter 'value' cannot be represented exactly as a decimal.");
        }

        return null;
    }

    public static IResult? ParseCategory(string name, out Category category)
    {
        category = default;

        // Match supported names only. Enum.TryParse alone accepts numeric tokens (+1)
        // and comma-combined names (Mass,Temperature).
        if (string.Equals(name, "length", StringComparison.OrdinalIgnoreCase))
        {
            category = Category.Length;
            return null;
        }

        if (string.Equals(name, "mass", StringComparison.OrdinalIgnoreCase))
        {
            category = Category.Mass;
            return null;
        }

        if (string.Equals(name, "temperature", StringComparison.OrdinalIgnoreCase))
        {
            category = Category.Temperature;
            return null;
        }

        if (string.Equals(name, "volume", StringComparison.OrdinalIgnoreCase))
        {
            category = Category.Volume;
            return null;
        }

        return ProblemTypes.Validation($"Unknown category '{name}'.");
    }

    internal static string NormalizeFixedPoint(string input)
    {
        var s = input;
        if (s.StartsWith('+'))
        {
            s = s[1..];
        }

        var negative = false;
        if (s.StartsWith('-'))
        {
            negative = true;
            s = s[1..];
        }

        var dot = s.IndexOf('.');
        string integer;
        string fraction;
        if (dot < 0)
        {
            integer = s;
            fraction = "";
        }
        else
        {
            integer = s[..dot];
            fraction = s[(dot + 1)..];
        }

        integer = integer.TrimStart('0');
        if (integer.Length == 0)
        {
            integer = "0";
        }

        fraction = fraction.TrimEnd('0');
        if (integer == "0" && fraction.Length == 0)
        {
            return "0";
        }

        var body = fraction.Length == 0 ? integer : $"{integer}.{fraction}";
        return negative ? "-" + body : body;
    }
}
