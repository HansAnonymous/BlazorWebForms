using System.Text.Json;
using System.Text.Json.Nodes;
using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Core.Services;

internal sealed class JsonFormDefinitionSerializer : IFormDefinitionSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public FormDefinition Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new FormDefinition();
        }

        var node = JsonNode.Parse(json);
        if (node is not JsonObject obj)
        {
            return new FormDefinition();
        }

        var version = obj["schemaVersion"]?.GetValue<int?>() ?? 1;
        if (version <= 1)
        {
            obj["schemaVersion"] = FormDefinition.CurrentSchemaVersion;
        }

        if (version > FormDefinition.CurrentSchemaVersion)
        {
            throw new InvalidOperationException($"Unsupported form schema version '{version}'. Current version is '{FormDefinition.CurrentSchemaVersion}'.");
        }

        var normalized = obj.ToJsonString(SerializerOptions);
        var definition = JsonSerializer.Deserialize<FormDefinition>(normalized, SerializerOptions) ?? new FormDefinition();

        if (definition.SchemaVersion <= 0)
        {
            definition.SchemaVersion = FormDefinition.CurrentSchemaVersion;
        }

        if (definition.SchemaVersion > FormDefinition.CurrentSchemaVersion)
        {
            throw new InvalidOperationException($"Unsupported form schema version '{definition.SchemaVersion}'. Current version is '{FormDefinition.CurrentSchemaVersion}'.");
        }

        return definition;
    }

    public string Serialize(FormDefinition definition)
    {
        definition.SchemaVersion = FormDefinition.CurrentSchemaVersion;
        return JsonSerializer.Serialize(definition, SerializerOptions);
    }
}

internal sealed class SimpleConditionEvaluator : IConditionEvaluator
{
    public bool IsVisible(string? expression, IReadOnlyDictionary<string, string?> answers)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return true;
        }

        var parts = expression.Split('=', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return true;
        }

        return answers.TryGetValue(parts[0], out var value) &&
               string.Equals(value, parts[1], StringComparison.OrdinalIgnoreCase);
    }

    public bool IsVisible(VisibilityConditionDefinition? condition, IReadOnlyDictionary<string, string?> answers)
    {
        if (condition is null || condition.Rules.Count == 0)
        {
            return true;
        }

        var results = condition.Rules.Select(rule => EvaluateRule(rule, answers)).ToList();
        return condition.Join == VisibilityJoinOperator.Or
            ? results.Any(x => x)
            : results.All(x => x);
    }

    private static bool EvaluateRule(VisibilityRuleDefinition rule, IReadOnlyDictionary<string, string?> answers)
    {
        var hasValue = answers.TryGetValue(rule.FieldId, out var actual);
        var normalizedActual = actual ?? string.Empty;
        var normalizedExpected = rule.Value ?? string.Empty;

        return rule.Operator switch
        {
            VisibilityRuleOperator.Equals => hasValue && string.Equals(normalizedActual, normalizedExpected, StringComparison.OrdinalIgnoreCase),
            VisibilityRuleOperator.NotEquals => !hasValue || !string.Equals(normalizedActual, normalizedExpected, StringComparison.OrdinalIgnoreCase),
            VisibilityRuleOperator.Contains => hasValue && normalizedActual.Contains(normalizedExpected, StringComparison.OrdinalIgnoreCase),
            VisibilityRuleOperator.Empty => !hasValue || string.IsNullOrWhiteSpace(normalizedActual),
            _ => true
        };
    }
}

internal sealed class DefaultFieldComponentRegistry : IFieldComponentRegistry
{
    private static readonly HashSet<FormFieldKind> Kinds =
    [
        FormFieldKind.Text,
        FormFieldKind.TextArea,
        FormFieldKind.Number,
        FormFieldKind.Select,
        FormFieldKind.Checkbox,
        FormFieldKind.Radio,
        FormFieldKind.Date,
        FormFieldKind.File,
        FormFieldKind.RichText,
        FormFieldKind.Signature
    ];

    public IReadOnlyCollection<FormFieldKind> SupportedFieldKinds => Kinds;

    public bool Supports(FormFieldKind kind) => Kinds.Contains(kind);
}

internal sealed class DefaultPermissionEvaluator : IPermissionEvaluator
{
    private static bool IsAdmin(UserProfile user) => user.Roles.Contains(FormPermissionRole.Admin);

    public bool CanManageForm(FormAggregate form, UserProfile user) =>
        IsAdmin(user) ||
        form.OwnerUserId == user.UserId ||
        user.Roles.Contains(FormPermissionRole.Owner) ||
        form.Permissions.Any(p => p.UserId == user.UserId &&
                                  (p.Role == FormPermissionRole.Owner || p.Role == FormPermissionRole.Manager) &&
                                  (string.IsNullOrWhiteSpace(p.ScopeType) || p.ScopeType.Equals("Form", StringComparison.OrdinalIgnoreCase) || p.ScopeType.Equals("Global", StringComparison.OrdinalIgnoreCase)));

    public bool CanSubmitForm(FormAggregate form, UserProfile user) =>
        form.Publication.AccessMode == FormAccessMode.Public ||
        CanManageForm(form, user) ||
        user.IsAuthenticated;

    public bool CanViewEntry(FormAggregate form, EntryRecord entry, UserProfile user) =>
        IsAdmin(user) ||
        CanManageForm(form, user) ||
        entry.ApprovalSteps.Any(s => string.Equals(s.ApproverEmail, user.Email, StringComparison.OrdinalIgnoreCase)) ||
        string.Equals(entry.SubmittedByEmail, user.Email, StringComparison.OrdinalIgnoreCase) ||
        form.Permissions.Any(p => p.UserId == user.UserId &&
                                  (p.Role == FormPermissionRole.Viewer || p.Role == FormPermissionRole.SelfViewer) &&
                                  (string.IsNullOrWhiteSpace(p.ScopeType) || p.ScopeType.Equals("Form", StringComparison.OrdinalIgnoreCase) || p.ScopeType.Equals("Global", StringComparison.OrdinalIgnoreCase)));
}
