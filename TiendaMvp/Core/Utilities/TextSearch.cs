using System.Globalization;
using System.Text;

namespace TiendaMvp.Core.Utilities;

public static class TextSearch
{
    public static bool Contains(string? value, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return Normalize(value).Contains(Normalize(query), StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).Trim().ToLowerInvariant();
    }
}
