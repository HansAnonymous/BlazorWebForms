namespace BlazorWebForms.Core.Models;

public sealed class FormDefinition
{
    public static int CurrentSchemaVersion => 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BrandingDefinition Branding { get; set; } = new();
    public List<FormSectionDefinition> Sections { get; set; } = [];
    public Dictionary<string, string> LocalizedTitles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class BrandingDefinition
{
    public string LogoUrl { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#0f766e";
    public string HeroText { get; set; } = "Collect structured submissions without rebuilding UI.";
}

public sealed class FormSectionDefinition
{
    public string Id { get; set; } = $"section-{Guid.NewGuid():N}";
    public string Title { get; set; } = "New section";
    public string Description { get; set; } = string.Empty;
    public string? VisibilityCondition { get; set; }
    public VisibilityConditionDefinition? VisibilityRules { get; set; }
    public List<FormFieldDefinition> Fields { get; set; } = [];
}

public sealed class FormFieldDefinition
{
    public string Id { get; set; } = $"field-{Guid.NewGuid():N}";
    public FormFieldKind Kind { get; set; } = FormFieldKind.Text;
    public string Label { get; set; } = "New field";
    public string Placeholder { get; set; } = string.Empty;
    public string HelpText { get; set; } = string.Empty;
    public bool Required { get; set; }
    public bool Searchable { get; set; }
    public string? RegexPattern { get; set; }
    public string? DefaultValue { get; set; }
    public string? ValidationHint { get; set; }
    public string? VisibilityCondition { get; set; }
    public VisibilityConditionDefinition? VisibilityRules { get; set; }
    public List<FormFieldOption> Options { get; set; } = [];
}

public sealed class FormFieldOption
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class VisibilityConditionDefinition
{
    public VisibilityJoinOperator Join { get; set; } = VisibilityJoinOperator.And;
    public List<VisibilityRuleDefinition> Rules { get; set; } = [];
}

public sealed class VisibilityRuleDefinition
{
    public string FieldId { get; set; } = string.Empty;
    public VisibilityRuleOperator Operator { get; set; } = VisibilityRuleOperator.Equals;
    public string? Value { get; set; }
}
