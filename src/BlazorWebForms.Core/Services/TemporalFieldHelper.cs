using System.Globalization;
using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Core.Services;

public static class TemporalFieldHelper
{
    public const string DateFormatKey = "date.format";
    public const string DurationFormatKey = "duration.format";
    public const string DefaultDurationFormat = "years-months";

    public static readonly IReadOnlyList<(string Value, string Label)> DateFormats =
    [
        ("", "Default"),
        ("yyyy-MM-dd", "2026-11-24 (ISO)"),
        ("MM/dd/yyyy", "11/24/2026"),
        ("dd/MM/yyyy", "24/11/2026"),
        ("MMMM d, yyyy", "November 24, 2026"),
        ("MMM d, yyyy", "Nov 24, 2026")
    ];

    public static readonly IReadOnlyList<(string Value, string Label)> DurationFormats =
    [
        (DefaultDurationFormat, "Years and months"),
        ("years-months-days", "Years, months, and days"),
        ("months", "Total completed months"),
        ("days", "Total days"),
        ("range", "Start date through end date")
    ];

    public static string GetDateFormat(FormFieldDefinition field)
        => GetFormat(field, DateFormatKey, DateFormats, "");

    public static string GetDurationFormat(FormFieldDefinition field)
        => GetFormat(field, DurationFormatKey, DurationFormats, DefaultDurationFormat);

    private static string GetFormat(FormFieldDefinition field, string key,
        IReadOnlyList<(string Value, string Label)> formats, string defaultValue)
    {
        if (!field.Metadata.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)) return defaultValue;
        return formats.Any(format => format.Value == value)
            ? value : throw new InvalidOperationException($"Unsupported format '{value}' for field '{field.Label}'.");
    }

    public static void SetFormat(FormFieldDefinition field, string key, string? value)
    {
        var formats = key switch
        {
            DateFormatKey => DateFormats,
            DurationFormatKey => DurationFormats,
            _ => throw new InvalidOperationException("Unsupported temporal format setting.")
        };
        if (value is not null && !formats.Any(format => format.Value == value))
            throw new InvalidOperationException("Unsupported temporal format.");
        if (string.IsNullOrEmpty(value)) field.Metadata.Remove(key);
        else field.Metadata[key] = value;
    }

    public static string FormatDate(FormFieldDefinition field, DateOnly date, string defaultFormat = "yyyy-MM-dd", CultureInfo? culture = null)
    {
        var format = GetDateFormat(field);
        return date.ToString(string.IsNullOrEmpty(format) ? defaultFormat : format, culture ?? CultureInfo.CurrentCulture);
    }

    public static string FormatRange(FormFieldDefinition field, DateOnly start, DateOnly end, CultureInfo? culture = null)
    {
        EnsureRange(start, end);
        return $"{FormatDate(field, start, culture: culture)} through {FormatDate(field, end, culture: culture)}";
    }

    public static string FormatDuration(FormFieldDefinition field, DateOnly start, DateOnly end, CultureInfo? culture = null)
    {
        EnsureRange(start, end);
        var format = GetDurationFormat(field);
        if (format == "range") return FormatRange(field, start, end, culture);
        var months = (end.Year - start.Year) * 12 + end.Month - start.Month;
        if (start.AddMonths(months) > end) months--;
        var days = end.DayNumber - start.AddMonths(months).DayNumber;
        static string Unit(int count, string unit) => $"{count} {unit}{(count == 1 ? "" : "s")}";
        if (format == "months") return Unit(months, "month");
        if (format == "days") return Unit(end.DayNumber - start.DayNumber, "day");
        var parts = new List<string>();
        if (months / 12 > 0) parts.Add(Unit(months / 12, "year"));
        if (months % 12 > 0) parts.Add(Unit(months % 12, "month"));
        if (format == "years-months-days" && days > 0) parts.Add(Unit(days, "day"));
        return parts.Count > 0 ? string.Join(", ", parts) : format == "years-months-days" ? "0 days" : "0 months";
    }

    public static string FormatDurationAnswer(FormFieldDefinition field, string? value, CultureInfo? culture = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        if (!DateRangeValue.TryParse(value, out var range) || !range.IsComplete
            || range.Start is not { } start || range.End is not { } end)
            throw new InvalidOperationException($"Duration '{field.Label}' requires a valid start and end date; end cannot precede start.");
        return FormatDuration(field, start, end, culture);
    }

    private static void EnsureRange(DateOnly start, DateOnly end)
    {
        if (start == default || end == default || end < start)
            throw new InvalidOperationException("A duration requires valid dates with the end on or after the start.");
    }
}
