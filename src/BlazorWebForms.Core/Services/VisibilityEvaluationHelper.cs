using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Core.Services;

public static class VisibilityEvaluationHelper
{
    public static bool IsSectionVisible(
        IConditionEvaluator evaluator,
        FormSectionDefinition section,
        IReadOnlyDictionary<string, string?> answers)
    {
        if (section.VisibilityRules is not null && section.VisibilityRules.Rules.Count > 0)
        {
            return evaluator.IsVisible(section.VisibilityRules, answers);
        }

        return evaluator.IsVisible(section.VisibilityCondition, answers);
    }

    public static bool IsFieldVisible(
        IConditionEvaluator evaluator,
        FormFieldDefinition field,
        IReadOnlyDictionary<string, string?> answers)
    {
        if (field.VisibilityRules is not null && field.VisibilityRules.Rules.Count > 0)
        {
            return evaluator.IsVisible(field.VisibilityRules, answers);
        }

        return evaluator.IsVisible(field.VisibilityCondition, answers);
    }
}
