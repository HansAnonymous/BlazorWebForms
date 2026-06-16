using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Core.Services;

public static class SearchIndexBuilder
{
    public static Dictionary<string, string> Build(FormDefinition definition, IReadOnlyDictionary<string, string?> answers)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in definition.Sections.SelectMany(s => s.Fields).Where(f => f.Searchable))
        {
            if (answers.TryGetValue(field.Id, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                index[field.Id] = value!;
            }
        }
        return index;
    }
}
