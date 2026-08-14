using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
            VisibilityRuleOperator.Contains => hasValue && GetContainsCandidates(normalizedActual)
                .Any(candidate => candidate.Contains(normalizedExpected, StringComparison.OrdinalIgnoreCase)),
            VisibilityRuleOperator.NotContains => !hasValue || GetContainsCandidates(normalizedActual)
                .All(candidate => !candidate.Contains(normalizedExpected, StringComparison.OrdinalIgnoreCase)),
            VisibilityRuleOperator.Empty => !hasValue || string.IsNullOrWhiteSpace(normalizedActual),
            VisibilityRuleOperator.NotEmpty => hasValue && !string.IsNullOrWhiteSpace(normalizedActual),
            VisibilityRuleOperator.StartsWith => hasValue && normalizedActual.StartsWith(normalizedExpected, StringComparison.OrdinalIgnoreCase),
            VisibilityRuleOperator.EndsWith => hasValue && normalizedActual.EndsWith(normalizedExpected, StringComparison.OrdinalIgnoreCase),
            VisibilityRuleOperator.GreaterThan => decimal.TryParse(normalizedActual, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var gtActual) &&
                                                  decimal.TryParse(normalizedExpected, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var gtExpected) &&
                                                  gtActual > gtExpected,
            VisibilityRuleOperator.LessThan => decimal.TryParse(normalizedActual, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ltActual) &&
                                               decimal.TryParse(normalizedExpected, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ltExpected) &&
                                               ltActual < ltExpected,
            _ => true
        };
    }

    private static IReadOnlyList<string> GetContainsCandidates(string actual)
    {
        if (TryParseStringArray(actual, out var arrayValues))
        {
            return arrayValues;
        }

        if (actual.Contains(',', StringComparison.Ordinal))
        {
            return actual.Split(',', StringSplitOptions.TrimEntries);
        }

        return [actual];
    }

    private static bool TryParseStringArray(string value, out IReadOnlyList<string> parsed)
    {
        parsed = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (!trimmed.StartsWith('[') || !trimmed.EndsWith(']'))
        {
            return false;
        }

        try
        {
            var values = JsonSerializer.Deserialize<List<string>>(trimmed);
            if (values is null)
            {
                return false;
            }

            parsed = values;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
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
        entry.ApprovalSteps.Any(s => string.Equals(s.ApproverEmail, user.Email, StringComparison.OrdinalIgnoreCase)
                                  || s.Acceptors.Any(a => string.Equals(a.Email, user.Email, StringComparison.OrdinalIgnoreCase))
                                  || string.Equals(s.DelegatedToEmail, user.Email, StringComparison.OrdinalIgnoreCase)) ||
        string.Equals(entry.SubmittedByEmail, user.Email, StringComparison.OrdinalIgnoreCase) ||
        form.Permissions.Any(p => p.UserId == user.UserId &&
                                  (p.Role == FormPermissionRole.Viewer || p.Role == FormPermissionRole.SelfViewer) &&
                                  (string.IsNullOrWhiteSpace(p.ScopeType) || p.ScopeType.Equals("Form", StringComparison.OrdinalIgnoreCase) || p.ScopeType.Equals("Global", StringComparison.OrdinalIgnoreCase)));
}

/// <summary>No-op implementation of <see cref="IWebhookDispatcher"/>. Used when no real dispatcher is registered.</summary>
internal sealed class NoOpWebhookDispatcher : IWebhookDispatcher
{
    public Task DispatchAsync(FormAggregate form, WebhookTriggerEvent triggerEvent, EntryRecord entry, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<WebhookTestResult> SendTestDeliveryAsync(FormAggregate form, FormWebhookDefinition webhook, CancellationToken cancellationToken = default) =>
        Task.FromResult(new WebhookTestResult
        {
            WebhookId = webhook.Id,
            Url = webhook.Url,
            Success = false,
            ErrorMessage = "No webhook dispatcher is registered. Replace IWebhookDispatcher via DI to enable real deliveries."
        });
}

/// <summary>
/// Default formula evaluator. Substitutes <c>{fieldId}</c> references with current answer values
/// then evaluates the resulting arithmetic expression using <see cref="DataTable.Compute"/>.
/// </summary>
internal sealed class SimpleFormulaEvaluator : IFormulaEvaluator
{
    private static readonly Regex FieldRef = new(@"\{([^}]+)\}", RegexOptions.Compiled);

    public string? Evaluate(string expression, IReadOnlyDictionary<string, string?> answers)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var substituted = FieldRef.Replace(expression, match =>
        {
            var fieldId = match.Groups[1].Value;
            return answers.TryGetValue(fieldId, out var v) &&
                   decimal.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var n)
                ? n.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "0";
        });

        try
        {
            var result = new DataTable().Compute(substituted, null);
            return result?.ToString() ?? null;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Pass-through CAPTCHA validator. Always returns <c>true</c>. Replace via DI with a real provider.</summary>
internal sealed class NoOpCaptchaValidator : ICaptchaValidator
{
    public Task<bool> ValidateAsync(string token, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}

/// <summary>No-op analytics store. All tracking calls are silent no-ops.</summary>
internal sealed class NoOpFormAnalyticsStore : IFormAnalyticsStore
{
    public Task TrackViewAsync(Guid formId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task TrackStartAsync(Guid formId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task TrackSubmissionAsync(Guid formId, TimeSpan? completionTime, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task TrackAbandonAsync(Guid formId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<FormAnalyticsSummary> GetSummaryAsync(Guid formId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new FormAnalyticsSummary { FormId = formId });
}

/// <summary>
/// Resolves response-piping placeholders (<c>{fieldId}</c>) in field labels, placeholder text,
/// and help text so that previously entered answer values are interpolated at render time.
/// </summary>
public static class ResponsePipeHelper
{
    private static readonly Regex FieldRef = new(@"\{([^}]+)\}", RegexOptions.Compiled);

    /// <summary>
    /// Replaces every <c>{fieldId}</c> token in <paramref name="template"/> with the corresponding
    /// value from <paramref name="answers"/>. Unknown field IDs are left as-is.
    /// </summary>
    public static string Pipe(string template, IReadOnlyDictionary<string, string?> answers)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        return FieldRef.Replace(template, match =>
        {
            var fieldId = match.Groups[1].Value;
            return answers.TryGetValue(fieldId, out var v) && v is not null ? v : match.Value;
        });
    }

    /// <summary>
    /// Applies response piping to all user-visible text properties of every field in
    /// <paramref name="definition"/> and returns a new definition copy with piped strings.
    /// The original definition is not mutated.
    /// </summary>
    public static FormDefinition ApplyPiping(FormDefinition definition, IReadOnlyDictionary<string, string?> answers)
    {
        // Shallow-clone sections and fields so callers get a render-time copy without mutating the cached definition.
        var piped = new FormDefinition
        {
            SchemaVersion = definition.SchemaVersion,
            Title = Pipe(definition.Title, answers),
            Description = Pipe(definition.Description, answers),
            DefaultCulture = definition.DefaultCulture,
            Branding = definition.Branding,
            LocalizedTitles = definition.LocalizedTitles,
            LocalizedDescriptions = definition.LocalizedDescriptions,
            Metadata = definition.Metadata,
            ApprovalWorkflow = definition.ApprovalWorkflow,
            PaginationMode = definition.PaginationMode,
            ShowProgressBar = definition.ShowProgressBar,
            IsQuizMode = definition.IsQuizMode,
            QuizScoringMode = definition.QuizScoringMode,
            PassScore = definition.PassScore,
            ShowScoreOnCompletion = definition.ShowScoreOnCompletion
        };

        foreach (var section in definition.Sections)
        {
            var pipedSection = new FormSectionDefinition
            {
                Id = section.Id,
                Title = Pipe(section.Title, answers),
                Description = Pipe(section.Description, answers),
                VisibilityCondition = section.VisibilityCondition,
                VisibilityRules = section.VisibilityRules,
                Layout = section.Layout,
                LocalizedTitles = section.LocalizedTitles,
                LocalizedDescriptions = section.LocalizedDescriptions,
                Metadata = section.Metadata,
                Branches = section.Branches
            };

            foreach (var field in section.Fields)
            {
                pipedSection.Fields.Add(new FormFieldDefinition
                {
                    Id = field.Id,
                    Kind = field.Kind,
                    Label = Pipe(field.Label, answers),
                    Placeholder = Pipe(field.Placeholder, answers),
                    HelpText = Pipe(field.HelpText, answers),
                    Required = field.Required,
                    Searchable = field.Searchable,
                    ReadOnly = field.ReadOnly,
                    RegexPattern = field.RegexPattern,
                    DefaultValue = field.DefaultValue,
                    ValidationHint = field.ValidationHint,
                    Prefill = field.Prefill,
                    VisibilityCondition = field.VisibilityCondition,
                    VisibilityRules = field.VisibilityRules,
                    Layout = field.Layout,
                    RepeatableItemLabel = field.RepeatableItemLabel,
                    RepeatableAddButtonText = field.RepeatableAddButtonText,
                    MinItems = field.MinItems,
                    MaxItems = field.MaxItems,
                    RepeatableColumns = field.RepeatableColumns,
                    NumberDisplayKind = field.NumberDisplayKind,
                    NumberUnit = field.NumberUnit,
                    MinValue = field.MinValue,
                    MaxValue = field.MaxValue,
                    NumberStep = field.NumberStep,
                    RankCount = field.RankCount,
                    LocalizedLabels = field.LocalizedLabels,
                    LocalizedPlaceholders = field.LocalizedPlaceholders,
                    LocalizedHelpTexts = field.LocalizedHelpTexts,
                    LocalizedValidationHints = field.LocalizedValidationHints,
                    MaxFileSizeBytes = field.MaxFileSizeBytes,
                    MaxFileCount = field.MaxFileCount,
                    AllowedMimeTypes = field.AllowedMimeTypes,
                    AllowedExtensions = field.AllowedExtensions,
                    Options = field.Options,
                    MatrixRows = field.MatrixRows,
                    MatrixColumns = field.MatrixColumns,
                    MatrixLimitOneResponsePerColumn = field.MatrixLimitOneResponsePerColumn,
                    MatrixShuffleRowOrder = field.MatrixShuffleRowOrder,
                    CustomKind = field.CustomKind,
                    Metadata = field.Metadata,
                    MaxLength = field.MaxLength,
                    MinLength = field.MinLength,
                    MaxWords = field.MaxWords,
                    MinWords = field.MinWords,
                    FormulaExpression = field.FormulaExpression,
                    LookupSourceUrl = field.LookupSourceUrl,
                    LookupValuePath = field.LookupValuePath,
                    LookupLabelPath = field.LookupLabelPath,
                    LookupMinChars = field.LookupMinChars,
                    LookupMaxResults = field.LookupMaxResults,
                    LookupDebounceMs = field.LookupDebounceMs,
                    LookupDisplay = field.LookupDisplay,
                    ScoreWeight = field.ScoreWeight,
                    CorrectAnswer = field.CorrectAnswer
                });
            }

            piped.Sections.Add(pipedSection);
        }

        return piped;
    }
}
