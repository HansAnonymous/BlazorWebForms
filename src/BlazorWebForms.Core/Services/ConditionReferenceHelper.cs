using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Core.Services;

public static class ConditionReferenceHelper
{
    public static IEnumerable<string> GetConditionReferences(string? condition, VisibilityConditionDefinition? rules)
    {
        if (rules is not null)
        {
            foreach (var rule in rules.Rules)
            {
                if (!string.IsNullOrWhiteSpace(rule.FieldId))
                {
                    yield return rule.FieldId;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(condition))
        {
            yield break;
        }

        var parts = condition.Split('=', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]))
        {
            yield return parts[0];
        }
    }
}
