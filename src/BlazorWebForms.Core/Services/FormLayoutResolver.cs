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

    public static int ResolveFieldGridColumns(FormFieldDefinition field, FormSectionDefinition section) =>
        int.TryParse(NormalizeWidthHint(field.Layout?.WidthHint), out var width)
            ? width
            : 12 / ResolveSectionColumns(section);

    public static string NormalizeWidthHint(string? hint)
    {
        var value = hint?.Trim();
        if (int.TryParse(value, out var width) && width is >= 1 and <= 12)
            return width.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return value?.ToUpperInvariant() switch
        {
            "HALF" => "6",
            "THIRD" => "4",
            "TWOTHIRDS" => "8",
            "FULL" => "12",
            _ => "Auto"
        };
    }

    public static int ResolveRepeatableColumnWidth(RepeatableListColumnDefinition column, FormFieldDefinition field)
    {
        if (column.WidthUnits is >= 1 and <= 12)
            return column.WidthUnits.Value;

        var columns = field.RepeatableColumnsPerItem.GetValueOrDefault();
        if (columns <= 0)
            columns = Math.Max(field.RepeatableColumns.Count, 1);
        return 12 / Math.Clamp(columns, 1, 4);
    }

    public static string ResolveFieldWidthHint(FormFieldDefinition field)
    {
        var hint = field.Layout?.WidthHint;
        if (string.IsNullOrWhiteSpace(hint))
        {
            return "Auto";
        }

        if (int.TryParse(hint.Trim(), out var width) && width is >= 1 and <= 12)
            return width.ToString(System.Globalization.CultureInfo.InvariantCulture);

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
