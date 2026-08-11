namespace BlazorWebForms.Core.Models;

public enum FormFieldKind
{
    Text,
    TextArea,
    Number,
    Select,
    Checkbox,
    Radio,
    Date,
    File,
    RichText,
    Signature,
    RepeatableList,
    RankedChoice,
    Custom,
    /// <summary>Value is computed at runtime from a formula expression; never entered by the submitter.</summary>
    Calculated,
    /// <summary>Value is stored with the entry but not shown to the submitter on the rendered form.</summary>
    Hidden,
    /// <summary>Presents a search input backed by an external URL; selected value is stored as a string.</summary>
    Lookup
}

public enum NumberDisplayKind
{
    Plain,
    Unit,
    Percentage
}

public enum RepeatableColumnKind
{
    Text,
    TextArea,
    Number,
    Date,
    Checkbox
}

public enum PrefillSourceKind
{
    None,
    Claim,
    Employee,
    FixedValue,
    Custom
}

public enum FormAccessMode
{
    Authenticated,
    Public
}

public enum FormPermissionRole
{
    Owner,
    Manager,
    Approver,
    Submitter,
    Viewer,
    SelfViewer,
    Admin
}

public enum EntryStatus
{
    Draft,
    Submitted,
    NeedsApproval,
    Approved,
    Rejected
}

public enum ApprovalStepStatus
{
    Pending,
    Approved,
    Rejected
}

public enum SubmissionEditMode
{
    ImmutableRevisions,
    OverwriteLatest
}

public enum InvitationStatus
{
    Pending,
    Accepted,
    Revoked,
    Expired
}

public enum VisibilityJoinOperator
{
    And,
    Or
}

public enum VisibilityRuleOperator
{
    Equals,
    NotEquals,
    Contains,
    Empty,
    NotEmpty,
    StartsWith,
    EndsWith,
    GreaterThan,
    LessThan
}

public enum WebhookTriggerEvent
{
    EntrySubmitted,
    EntryApproved,
    EntryRejected,
    StepApproved,
    StepRejected,
    StepDelegated,
    EntryResubmitted
}

public enum ApprovalStepAcceptorMode
{
    /// <summary>A single designated approver must act on this step.</summary>
    Single,
    /// <summary>Any one of the listed acceptors may act on this step.</summary>
    AnyOf
}

public enum ApprovalAuditAction
{
    StepApproved,
    StepRejected,
    Resubmitted,
    GraphApproverAssigned,
    GraphApproverReminder,
    StepDelegated,
    WebhookDispatched
}

/// <summary>Controls how a form's sections are presented to the submitter.</summary>
public enum FormPaginationMode
{
    /// <summary>All sections are rendered on a single page (default).</summary>
    SinglePage,
    /// <summary>Each section is a discrete page; the submitter navigates forward and back.</summary>
    MultiPage
}

/// <summary>Controls how quiz/scoring mode computes totals.</summary>
public enum QuizScoringMode
{
    /// <summary>Sum of points from all correct option selections.</summary>
    Additive,
    /// <summary>Points are awarded per section and then averaged.</summary>
    SectionAverage
}

/// <summary>Indicates when a section branch condition is evaluated.</summary>
public enum SectionBranchTrigger
{
    /// <summary>Branch is evaluated when the section is completed and the submitter attempts to advance.</summary>
    OnAdvance,
    /// <summary>Branch is evaluated reactively whenever the referenced field answer changes.</summary>
    OnFieldChange
}
