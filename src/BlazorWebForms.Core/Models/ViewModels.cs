namespace BlazorWebForms.Core.Models;

public sealed class DashboardViewModel
{
    public required IReadOnlyList<FormAggregate> Forms { get; init; }
    public required IReadOnlyList<EntryRecord> RecentEntries { get; init; }
    public required UserProfile CurrentUser { get; init; }
}

public sealed class BuilderState
{
    public required FormAggregate Form { get; init; }
    public required bool CanManage { get; init; }
}

public sealed class PublishedFormViewModel
{
    public required FormAggregate Form { get; init; }
    public required FormVersionRecord Version { get; init; }
    public required FormDefinition Definition { get; init; }
    public required bool CanSubmit { get; init; }
    /// <summary>When false the form is not currently accepting submissions and <see cref="AccessBlockReason"/> explains why.</summary>
    public bool IsAcceptingSubmissions { get; init; } = true;
    /// <summary>Human-readable reason why submissions are currently blocked (scheduling, cap reached, etc.).</summary>
    public string? AccessBlockReason { get; init; }
}

public sealed class EntryDetailViewModel
{
    public required FormAggregate Form { get; init; }
    public required EntryRecord Entry { get; init; }
    public required FormDefinition Definition { get; init; }
    public required bool CanView { get; init; }
    public string? HistoricalRenderWarning { get; init; }
}

public sealed class SaveDraftRequest
{
    public Guid? FormId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public FormAccessMode AccessMode { get; set; } = FormAccessMode.Authenticated;
    public SubmissionEditMode EditMode { get; set; } = SubmissionEditMode.ImmutableRevisions;
    public FormDefinition Definition { get; set; } = new();
    public List<string> NotificationEmails { get; set; } = [];
    public List<FormWebhookDefinition> Webhooks { get; set; } = [];
    // Publication settings forwarded from the builder
    public DateTimeOffset? OpenUtc { get; set; }
    public DateTimeOffset? CloseUtc { get; set; }
    public string NotYetOpenMessage { get; set; } = string.Empty;
    public string ClosedMessage { get; set; } = string.Empty;
    public int? MaxSubmissions { get; set; }
    public string CapReachedMessage { get; set; } = string.Empty;
    public string ConfirmationMessage { get; set; } = string.Empty;
    public string ConfirmationRedirectUrl { get; set; } = string.Empty;
    public string? AccessPasswordPlainText { get; set; }
    public bool RequireCaptcha { get; set; }
    public int? AutoSaveIntervalSeconds { get; set; }
}

public sealed class SubmitEntryRequest
{
    public Guid? DraftEntryId { get; set; }
    public Dictionary<string, string?> Answers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ApproverInput> Approvers { get; set; } = [];
    /// <summary>Per-step acceptor pools for AnyOf steps. Key is the step index (0-based).</summary>
    public Dictionary<int, List<ApproverAcceptorInput>> StepAcceptors { get; set; } = [];
    public List<SubmittedFileInput> Files { get; set; } = [];
    /// <summary>CAPTCHA token from the client-side widget. Required when <see cref="FormPublication.RequireCaptcha"/> is true.</summary>
    public string? CaptchaToken { get; set; }
    /// <summary>Plain-text access password. Required when <see cref="FormPublication.AccessPasswordHash"/> is set.</summary>
    public string? AccessPassword { get; set; }
    /// <summary>UTC timestamp when the submitter first started filling in the form. Used for completion-time analytics.</summary>
    public DateTimeOffset? StartedUtc { get; set; }
}

public sealed class SaveDraftSubmissionRequest
{
    public Guid? DraftEntryId { get; set; }
    public Dictionary<string, string?> Answers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ApproverInput> Approvers { get; set; } = [];
    public List<SubmittedFileInput> Files { get; set; } = [];
}

public sealed class FormPrefillRequest
{
    public required FormDefinition Definition { get; init; }
    public required FormFieldDefinition Field { get; init; }
    public required UserProfile Requester { get; init; }
    public string? SubjectEmail { get; init; }
    public IReadOnlyDictionary<string, string?> ExistingAnswers { get; init; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
}

public sealed class ManagerPrefillDraftRequest
{
    public string SubmitterName { get; set; } = string.Empty;
    public string SubmitterEmail { get; set; } = string.Empty;
    public Dictionary<string, string?> Answers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ApproverInput> Approvers { get; set; } = [];
    public List<SubmittedFileInput> Files { get; set; } = [];
}

public sealed class SubmittedFileInput
{
    public string FieldId { get; set; } = string.Empty;
    public StoredFile File { get; set; } = new();
}

public sealed class FileUploadInput
{
    public string FieldId { get; set; } = string.Empty;
    public FileUploadRequest Request { get; set; } = new();
}

public sealed class EntryFileDownload
{
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required Stream Content { get; init; }
}

public sealed class FileCleanupResult
{
    public int DeletedDraftEntries { get; init; }
    public int DeletedFiles { get; init; }
}

public sealed class EntryPdfExport
{
    public required string FileName { get; init; }
    public required byte[] Content { get; init; }
    public required string ContentType { get; init; }
}

public sealed class ApproverInput
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public sealed class ResubmitEntryRequest
{
    public Dictionary<string, string?> Answers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ApproverInput> Approvers { get; set; } = [];
}

public sealed class CreateInvitationRequest
{
    public Guid FormId { get; set; }
    public string Email { get; set; } = string.Empty;
    public FormPermissionRole Role { get; set; }
    public string ScopeType { get; set; } = "Form";
    public string? ScopeValue { get; set; }
    public TimeSpan ValidFor { get; set; } = TimeSpan.FromDays(7);
}

public sealed class DelegateApprovalStepRequest
{
    public Guid EntryId { get; set; }
    public Guid StepId { get; set; }
    /// <summary>Email of the person to whom the step is delegated.</summary>
    public string DelegateToEmail { get; set; } = string.Empty;
    public string DelegateToName { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

/// <summary>Represents a single acceptor in an AnyOf step submitted alongside an entry.</summary>
public sealed class ApproverAcceptorInput
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

/// <summary>Result of a submission returned to the caller after a successful <c>SubmitEntryWithResultAsync</c>.</summary>
public sealed class FormSubmissionResult
{
    public required EntryRecord Entry { get; init; }
    /// <summary>Computed quiz score, populated when the form definition has <c>IsQuizMode = true</c>.</summary>
    public decimal? Score { get; init; }
    /// <summary>Whether the submitter passed the quiz, or null when quiz mode is off.</summary>
    public bool? QuizPassed { get; init; }
    /// <summary>Confirmation message from <see cref="FormPublication.ConfirmationMessage"/>.</summary>
    public string ConfirmationMessage { get; init; } = string.Empty;
    /// <summary>Redirect URL from <see cref="FormPublication.ConfirmationRedirectUrl"/>. Empty when confirmation page is used instead.</summary>
    public string ConfirmationRedirectUrl { get; init; } = string.Empty;

    // ── Forwarding members for backward-compatible call sites ────────────────
    public Guid Id => Entry.Id;
    public EntryStatus Status => Entry.Status;
    public Guid FormVersionId => Entry.FormVersionId;
    public IReadOnlyDictionary<string, string?> Answers => Entry.Answers;
    public List<EntryFileRecord> Files => Entry.Files;
    public List<EntryRevisionRecord> Revisions => Entry.Revisions;
    public List<ApprovalStepRecord> ApprovalSteps => Entry.ApprovalSteps;

    /// <summary>
    /// Implicit conversion to <see cref="EntryRecord"/> for call sites that assign the result
    /// directly to an <see cref="EntryRecord"/> variable.
    /// </summary>
    public static implicit operator EntryRecord(FormSubmissionResult result) => result.Entry;
}

/// <summary>Result of <c>GetFormAnalyticsAsync</c>.</summary>
public sealed class FormAnalyticsViewModel
{
    public required FormAggregate Form { get; init; }
    public required FormAnalyticsSummary Summary { get; init; }
}

/// <summary>Request to record a form analytics event from the host UI.</summary>
public sealed class TrackFormAnalyticsRequest
{
    public Guid FormId { get; set; }
    public FormAnalyticsEvent Event { get; set; }
    /// <summary>How long (in seconds) since the submitter started the form. Only relevant for <see cref="FormAnalyticsEvent.Submission"/>.</summary>
    public double? CompletionSeconds { get; set; }
}

public enum FormAnalyticsEvent
{
    View,
    Start,
    Submission,
    Abandon
}

/// <summary>Result of a test webhook delivery triggered via <c>SendTestWebhookAsync</c>.</summary>
public sealed class WebhookTestResult
{
    /// <summary>The webhook definition the test delivery was sent to.</summary>
    public required Guid WebhookId { get; init; }
    /// <summary>The target URL the test POST was sent to.</summary>
    public required string Url { get; init; }
    /// <summary>True when the endpoint returned a 2xx status code.</summary>
    public bool Success { get; init; }
    /// <summary>HTTP status code returned by the endpoint, or <c>null</c> when the request did not complete.</summary>
    public int? HttpStatusCode { get; init; }
    /// <summary>Error message when <see cref="Success"/> is false, otherwise empty.</summary>
    public string ErrorMessage { get; init; } = string.Empty;
    /// <summary>Round-trip latency of the HTTP call.</summary>
    public TimeSpan Elapsed { get; init; }
}
