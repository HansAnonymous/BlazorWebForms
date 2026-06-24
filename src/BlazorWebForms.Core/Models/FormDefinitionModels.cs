namespace BlazorWebForms.Core.Models;

public sealed class FormDefinition
{
    public static int CurrentSchemaVersion => 4;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DefaultCulture { get; set; } = "en-US";
    public BrandingDefinition Branding { get; set; } = new();
    public List<FormSectionDefinition> Sections { get; set; } = [];
    public Dictionary<string, string> LocalizedTitles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> LocalizedDescriptions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class BrandingDefinition
{
    public string LogoUrl { get; set; } = string.Empty;
    public string LogoFileRef { get; set; } = string.Empty;
    public string HeroImageUrl { get; set; } = string.Empty;
    public string HeroImageFileRef { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#0f766e";
    public string SurfaceColor { get; set; } = "#ffffff";
    public string TextColor { get; set; } = "#124040";
    public string ButtonRadius { get; set; } = "999px";
    public string HeroText { get; set; } = "Collect structured submissions without rebuilding UI.";
}

public sealed class FormSectionDefinition
{
    public string Id { get; set; } = $"section-{Guid.NewGuid():N}";
    public string Title { get; set; } = "New section";
    public string Description { get; set; } = string.Empty;
    public string? VisibilityCondition { get; set; }
    public VisibilityConditionDefinition? VisibilityRules { get; set; }
    public FormSectionLayoutDefinition? Layout { get; set; } = new();
    public Dictionary<string, string> LocalizedTitles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> LocalizedDescriptions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
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
    public bool ReadOnly { get; set; }
    public string? RegexPattern { get; set; }
    public string? DefaultValue { get; set; }
    public string? ValidationHint { get; set; }
    public FormFieldPrefillDefinition Prefill { get; set; } = new();
    public string? VisibilityCondition { get; set; }
    public VisibilityConditionDefinition? VisibilityRules { get; set; }
    public FormFieldLayoutDefinition? Layout { get; set; } = new();
    public string RepeatableItemLabel { get; set; } = "Item";
    public string RepeatableAddButtonText { get; set; } = "Add item";
    public int? MinItems { get; set; }
    public int? MaxItems { get; set; }
    public List<RepeatableListColumnDefinition> RepeatableColumns { get; set; } = [];
    public NumberDisplayKind NumberDisplayKind { get; set; } = NumberDisplayKind.Plain;
    public string NumberUnit { get; set; } = string.Empty;
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public decimal? NumberStep { get; set; }
    public int? RankCount { get; set; }
    public Dictionary<string, string> LocalizedLabels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> LocalizedPlaceholders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> LocalizedHelpTexts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> LocalizedValidationHints { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public long? MaxFileSizeBytes { get; set; }
    public int MaxFileCount { get; set; } = 1;
    public List<string> AllowedMimeTypes { get; set; } = [];
    public List<string> AllowedExtensions { get; set; } = [];
    public List<FormFieldOption> Options { get; set; } = [];
}

public sealed class FormFieldPrefillDefinition
{
    public PrefillSourceKind Source { get; set; } = PrefillSourceKind.None;
    public string ProviderKey { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public bool ApplyWhenEmpty { get; set; } = true;
}

public sealed class FormFieldOption
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public Dictionary<string, string> LocalizedLabels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RepeatableListColumnDefinition
{
    public string Id { get; set; } = $"col-{Guid.NewGuid():N}";
    public string Label { get; set; } = "Column";
    public RepeatableColumnKind Kind { get; set; } = RepeatableColumnKind.Text;
    public string Placeholder { get; set; } = string.Empty;
    public bool Required { get; set; }
    public List<FormFieldOption> Options { get; set; } = [];
    public Dictionary<string, string> LocalizedLabels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> LocalizedPlaceholders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
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

public sealed class FormSectionLayoutDefinition
{
    public int? Columns { get; set; }
    public string? Group { get; set; }
}

public sealed class FormFieldLayoutDefinition
{
    public string WidthHint { get; set; } = "Auto";
    public string? Group { get; set; }
}

public sealed class SignatureFieldValue
{
    public string SignerName { get; set; } = string.Empty;
    public bool Confirmed { get; set; }
    public DateTimeOffset? SignedUtc { get; set; }
}
