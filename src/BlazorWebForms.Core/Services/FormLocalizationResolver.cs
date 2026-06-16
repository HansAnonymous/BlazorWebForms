using System.Globalization;

namespace BlazorWebForms.Core.Services;

public static class FormLocalizationResolver
{
    public static string ResolveText(string baseText, IReadOnlyDictionary<string, string>? localizedValues, string? requestedCulture, string? defaultCulture)
    {
        if (localizedValues is null || localizedValues.Count == 0)
        {
            return baseText;
        }

        if (TryResolveForCulture(localizedValues, requestedCulture, out var requested))
        {
            return requested;
        }

        if (TryResolveForCulture(localizedValues, defaultCulture, out var fallback))
        {
            return fallback;
        }

        var first = localizedValues.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        return string.IsNullOrWhiteSpace(first) ? baseText : first;
    }

    private static bool TryResolveForCulture(IReadOnlyDictionary<string, string> localizedValues, string? culture, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(culture))
        {
            return false;
        }

        if (localizedValues.TryGetValue(culture, out var exact) && !string.IsNullOrWhiteSpace(exact))
        {
            value = exact;
            return true;
        }

        try
        {
            var info = CultureInfo.GetCultureInfo(culture);
            if (!string.IsNullOrWhiteSpace(info.TwoLetterISOLanguageName) &&
                localizedValues.TryGetValue(info.TwoLetterISOLanguageName, out var parent) &&
                !string.IsNullOrWhiteSpace(parent))
            {
                value = parent;
                return true;
            }
        }
        catch (CultureNotFoundException)
        {
            return false;
        }

        return false;
    }
}
