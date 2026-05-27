using System.Text.Json;
using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Core.Services;

internal sealed class JsonFormDefinitionSerializer : IFormDefinitionSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public FormDefinition Deserialize(string json) =>
        JsonSerializer.Deserialize<FormDefinition>(json, SerializerOptions) ?? new FormDefinition();

    public string Serialize(FormDefinition definition) =>
        JsonSerializer.Serialize(definition, SerializerOptions);
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
        string.Equals(entry.SubmittedByEmail, user.Email, StringComparison.OrdinalIgnoreCase) ||
        form.Permissions.Any(p => p.UserId == user.UserId &&
                                  (p.Role == FormPermissionRole.Viewer || p.Role == FormPermissionRole.SelfViewer) &&
                                  (string.IsNullOrWhiteSpace(p.ScopeType) || p.ScopeType.Equals("Form", StringComparison.OrdinalIgnoreCase) || p.ScopeType.Equals("Global", StringComparison.OrdinalIgnoreCase)));
}
