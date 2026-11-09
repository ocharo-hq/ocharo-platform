using System.Text.RegularExpressions;

namespace OcharoBrand.Infrastructure.Brand.Validators;

/// <summary>Checks the text, colours and keys operators type into Brand.</summary>
public static partial class BrandInputValidator
{
    /// <summary>Trims <paramref name="value"/>, empty text reading as none; false when the trimmed text is longer than
    /// <paramref name="maximum"/>.</summary>
    public static bool TryClean(string? value, int maximum, out string? cleaned)
    {
        var trimmed = value?.Trim();
        cleaned = string.IsNullOrEmpty(trimmed) ? null : trimmed;
        return cleaned is null || cleaned.Length <= maximum;
    }

    /// <summary>Tells whether a value is a <c>#RRGGBB</c> colour.</summary>
    public static bool IsHex(string? value) => value is not null && Hex().IsMatch(value);

    /// <summary>Tells whether a value is a short lowercase slug.</summary>
    public static bool IsKey(string value) => Key().IsMatch(value);

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex Hex();

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex Key();
}
