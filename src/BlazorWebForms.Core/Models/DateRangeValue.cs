using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorWebForms.Core.Models;

public sealed record DateRangeValue(DateOnly? Start, DateOnly? End)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [JsonIgnore]
    public bool IsComplete => Start.HasValue && End.HasValue && Start.Value != default && End.Value != default && End >= Start;

    public string? Serialize() => Start is null && End is null ? null : JsonSerializer.Serialize(this, JsonOptions);

    public static bool TryParse(string? value, out DateRangeValue range)
    {
        range = new(null, null);
        if (string.IsNullOrWhiteSpace(value)) return true;
        try
        {
            var parsed = JsonSerializer.Deserialize<DateRangeValue>(value, JsonOptions);
            if (parsed is null) return false;
            range = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
