namespace BlazorWebForms.Core.Models;

public sealed class FormDefinition
{
    public static int CurrentSchemaVersion => 6;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DefaultCulture { get; set; } = "en-US";
    public BrandingDefinition Branding { get; set; } = new();
    public List<FormSectionDefinition> Sections { get; set; } = [];
    public Dictionary<string, string> LocalizedTitles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> LocalizedDescriptions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Optional structured approval workflow defined at design time. When present, drives the default step list on submission.</summary>
    public ApprovalWorkflowDefinition? ApprovalWorkflow { get; set; }

    // ── Pagination ────────────────────────────────────────────────────────────
    /// <summary>Controls how sections are presented — single scrolling page or one section per page.</summary>
    public FormPaginationMode PaginationMode { get; set; } = FormPaginationMode.SinglePage;
    /// <summary>When <see cref="PaginationMode"/> is <see cref="FormPaginationMode.MultiPage"/>, shows a progress indicator to the submitter.</summary>
    public bool ShowProgressBar { get; set; } = true;

    // ── Quiz / Scoring ────────────────────────────────────────────────────────
    /// <summary>Enables quiz mode. Points are summed from <see cref="FormFieldOption.Points"/> on correct answers.</summary>
    public bool IsQuizMode { get; set; }
    public QuizScoringMode QuizScoringMode { get; set; } = QuizScoringMode.Additive;
    /// <summary>Minimum score required to pass. Null means no pass/fail determination.</summary>
    public decimal? PassScore { get; set; }
    /// <summary>When true, the score and pass/fail result are shown to the submitter on the confirmation screen.</summary>
    public bool ShowScoreOnCompletion { get; set; }
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
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<FormFieldDefinition> Fields { get; set; } = [];
    /// <summary>
    /// Ordered list of branching rules evaluated when the submitter attempts to advance past this section.
    /// The first branch whose condition is satisfied determines the next section. When no branch matches,
    /// the form proceeds to the next section in definition order.
    /// </summary>
    public List<SectionBranchDefinition> Branches { get; set; } = [];
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
    public string CustomKind { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // ── Character / word limits ───────────────────────────────────────────────
    /// <summary>Maximum number of characters accepted for text-based fields.</summary>
    public int? MaxLength { get; set; }
    /// <summary>Minimum number of characters required for text-based fields.</summary>
    public int? MinLength { get; set; }
    /// <summary>Maximum word count for TextArea / RichText fields.</summary>
    public int? MaxWords { get; set; }
    /// <summary>Minimum word count for TextArea / RichText fields.</summary>
    public int? MinWords { get; set; }

    // ── Calculated / formula fields ───────────────────────────────────────────
    /// <summary>
    /// Formula expression evaluated at runtime for <see cref="FormFieldKind.Calculated"/> and
    /// <see cref="FormFieldKind.Hidden"/> (computed) fields.
    /// Use <c>{fieldId}</c> to reference another field's current answer value.
    /// Supports arithmetic operators: <c>+</c> <c>-</c> <c>*</c> <c>/</c>.
    /// Example: <c>{price-field} * {qty-field}</c>
    /// </summary>
    public string? FormulaExpression { get; set; }

    // ── Lookup / typeahead fields ─────────────────────────────────────────────
    /// <summary>Remote URL queried by the client for <see cref="FormFieldKind.Lookup"/> fields. Receives a <c>?q=</c> query parameter.</summary>
    public string LookupSourceUrl { get; set; } = string.Empty;
    /// <summary>JSON path within each result object to use as the stored value (e.g. <c>"id"</c>).</summary>
    public string LookupValuePath { get; set; } = string.Empty;
    /// <summary>JSON path within each result object to use as the display label (e.g. <c>"name"</c>).</summary>
    public string LookupLabelPath { get; set; } = string.Empty;
    /// <summary>Minimum characters typed before the lookup query fires.</summary>
    public int LookupMinChars { get; set; } = 2;
    /// <summary>Maximum results to show in the lookup dropdown.</summary>
    public int LookupMaxResults { get; set; } = 10;
    /// <summary>Optional debounce delay in milliseconds between keystrokes and the lookup request.</summary>
    public int LookupDebounceMs { get; set; } = 300;
    /// <summary>Display configuration for the rendered lookup field.</summary>
    public LookupDisplayDefinition LookupDisplay { get; set; } = new();

    // ── Quiz / scoring
    /// <summary>Weight multiplier applied to this field's score contribution in quiz mode (default 1).</summary>
    public decimal ScoreWeight { get; set; } = 1m;
    /// <summary>The correct answer value used for automatic scoring in quiz mode. Compared against the submitter's answer.</summary>
    public string? CorrectAnswer { get; set; }
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
    /// <summary>Points awarded when this option is selected in quiz/scoring mode.</summary>
    public decimal? Points { get; set; }
    /// <summary>When true, this is the designated correct answer in quiz mode. Affects automatic score evaluation.</summary>
    public bool IsCorrectAnswer { get; set; }
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

/// <summary>Design-time specification of the approval workflow embedded in the form definition.</summary>
public sealed class ApprovalWorkflowDefinition
{
    public List<ApprovalStepDefinition> Steps { get; set; } = [];
}

/// <summary>A single step within the design-time approval workflow.</summary>
public sealed class ApprovalStepDefinition
{
    public string Id { get; set; } = $"step-{Guid.NewGuid():N}";
    public string Label { get; set; } = "Approval step";
    /// <summary>Instructions shown to approver(s) when acting on this step.</summary>
    public string Instructions { get; set; } = string.Empty;
    public ApprovalStepAcceptorMode AcceptorMode { get; set; } = ApprovalStepAcceptorMode.Single;
    /// <summary>Pre-configured acceptor pool. Used when <see cref="AcceptorMode"/> is <see cref="ApprovalStepAcceptorMode.AnyOf"/>.</summary>
    public List<ApprovalAcceptorDefinition> Acceptors { get; set; } = [];
    /// <summary>
    /// Optional formula that resolves the approver's email at submission time.
    /// Use <c>{fieldId}</c> to reference a form field value (e.g. <c>{manager-email-field}</c>).
    /// When set, this overrides any static <see cref="Acceptors"/> for <see cref="ApprovalStepAcceptorMode.Single"/> steps.
    /// </summary>
    public string? ApproverEmailExpression { get; set; }
    /// <summary>Similarly resolves the approver's display name at submission time.</summary>
    public string? ApproverNameExpression { get; set; }
}

/// <summary>A pre-configured acceptor entry within a design-time step definition.</summary>
public sealed class ApprovalAcceptorDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// A conditional branching rule attached to a section. When the condition is satisfied as the
/// submitter advances past the section, the form jumps to <see cref="TargetSectionId"/> instead of
/// continuing to the next section in definition order.
/// </summary>
public sealed class SectionBranchDefinition
{
    public string Id { get; set; } = $"branch-{Guid.NewGuid():N}";
    /// <summary>Structured condition that must evaluate to true for this branch to activate.</summary>
    public VisibilityConditionDefinition? Condition { get; set; }
    /// <summary>Legacy single-expression condition string. Evaluated only when <see cref="Condition"/> is null.</summary>
    public string? ConditionExpression { get; set; }
    /// <summary>The <see cref="FormSectionDefinition.Id"/> to jump to when this branch activates.
    /// Use <c>"__end"</c> to jump directly to the confirmation screen (skip remaining sections).</summary>
    public string TargetSectionId { get; set; } = string.Empty;
    public SectionBranchTrigger Trigger { get; set; } = SectionBranchTrigger.OnAdvance;
    /// <summary>Optional label shown in the form builder to describe this branch's purpose.</summary>
    public string Label { get; set; } = string.Empty;
}

/// <summary>Definition for a Lookup field's display options on the rendered form.</summary>
public sealed class LookupDisplayDefinition
{
    /// <summary>Placeholder shown inside the search input before the user types.</summary>
    public string SearchPlaceholder { get; set; } = "Search...";
    /// <summary>Text shown when no results are returned.</summary>
    public string NoResultsText { get; set; } = "No results found.";
    /// <summary>Text shown while the lookup query is in flight.</summary>
    public string LoadingText { get; set; } = "Searching...";
}
