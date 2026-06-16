using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Core.Services;

public static class FormLayoutResolver
{
    public static int ResolveSectionColumns(FormSectionDefinition section)
    {
        var raw = section.Layout?.Columns;
        if (!raw.HasValue)
        {
            return 1;
        }

        return raw.Value switch
        {
            < 1 => 1,
            > 4 => 4,
            _ => raw.Value
        };
    }

    public static string ResolveFieldWidthHint(FormFieldDefinition field)
    {
        var hint = field.Layout?.WidthHint;
        if (string.IsNullOrWhiteSpace(hint))
        {
            return "Auto";
        }

        return hint.Trim() switch
        {
            "Auto" => "Auto",
            "Half" => "Half",
            "Full" => "Full",
            "Third" => "Third",
            "TwoThirds" => "TwoThirds",
            _ => "Auto"
        };
    }
}
