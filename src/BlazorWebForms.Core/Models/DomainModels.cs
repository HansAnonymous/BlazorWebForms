namespace BlazorWebForms.Core.Models;

public sealed class FormAggregate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = $"form-{Guid.NewGuid():N}";
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid OwnerUserId { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public FormDefinition DraftDefinition { get; set; } = new();
    public List<FormVersionRecord> Versions { get; set; } = [];
    public FormPublication Publication { get; set; } = new();
    public List<FormPermissionGrant> Permissions { get; set; } = [];
    public List<FormNotificationRule> Notifications { get; set; } = [];
    public List<FormWebhookDefinition> Webhooks { get; set; } = [];
}

public sealed class FormVersionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int VersionNumber { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string DefinitionJson { get; set; } = string.Empty;
}

public sealed class FormPublication
{
    public string Slug { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public FormAccessMode AccessMode { get; set; } = FormAccessMode.Authenticated;
    public bool SendSubmissionCopyToSubmitter { get; set; } = true;
    public SubmissionEditMode EditMode { get; set; } = SubmissionEditMode.ImmutableRevisions;

    // ── Scheduling ───────────────────────────────────────────────────────────
    /// <summary>UTC timestamp before which the form is not yet accepting submissions. Null means open immediately after publishing.</summary>
    public DateTimeOffset? OpenUtc { get; set; }
    /// <summary>UTC timestamp after which the form stops accepting submissions. Null means never closes.</summary>
    public DateTimeOffset? CloseUtc { get; set; }
    /// <summary>Message shown to users who attempt to access the form before <see cref="OpenUtc"/>.</summary>
    public string NotYetOpenMessage { get; set; } = string.Empty;
    /// <summary>Message shown to users who attempt to access the form after <see cref="CloseUtc"/>.</summary>
    public string ClosedMessage { get; set; } = string.Empty;

    // ── Submission caps ───────────────────────────────────────────────────────
    /// <summary>Maximum number of non-draft submissions accepted. Once reached, the form stops accepting new submissions. Null means unlimited.</summary>
    public int? MaxSubmissions { get; set; }
    /// <summary>Message shown when the submission cap is reached.</summary>
    public string CapReachedMessage { get; set; } = string.Empty;

    // ── Confirmation page ─────────────────────────────────────────────────────
    /// <summary>Markdown or plain text shown to the submitter after a successful submission.</summary>
    public string ConfirmationMessage { get; set; } = string.Empty;
    /// <summary>When set, the host UI redirects the submitter to this URL after submission instead of showing the confirmation message.</summary>
    public string ConfirmationRedirectUrl { get; set; } = string.Empty;

    // ── Access control ────────────────────────────────────────────────────────
    /// <summary>
    /// HMAC-SHA256 hash of the access password (hex string). When non-empty, submitters must
    /// supply the matching password via <see cref="ViewModels.SubmitEntryRequest.AccessPassword"/> before submitting.
    /// </summary>
    public string AccessPasswordHash { get; set; } = string.Empty;
    /// <summary>Whether a CAPTCHA token must be validated on submission.</summary>
    public bool RequireCaptcha { get; set; }

    // ── Draft auto-save ────────────────────────────────────────────────────────
    /// <summary>When > 0, the host UI automatically saves a draft at this interval (in seconds). 0 or null disables auto-save.</summary>
    public int? AutoSaveIntervalSeconds { get; set; }
}

public sealed class FormPermissionGrant
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public FormPermissionRole Role { get; set; }
    public string ScopeType { get; set; } = "Form";
    public string? ScopeValue { get; set; }
}

public sealed class FormNotificationRule
{
    public string Email { get; set; } = string.Empty;
    public bool OnSubmission { get; set; } = true;
    public bool OnApproval { get; set; } = true;
}

public sealed class FormWebhookDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>The URL that will receive POST payloads.</summary>
    public string Url { get; set; } = string.Empty;
    /// <summary>Optional HMAC-SHA256 secret used to sign the payload. Leave empty to skip signing.</summary>
    public string Secret { get; set; } = string.Empty;
    /// <summary>Events that should trigger this webhook.</summary>
    public List<WebhookTriggerEvent> TriggerEvents { get; set; } = [WebhookTriggerEvent.EntrySubmitted];
    /// <summary>Optional HTTP headers to include in every request (e.g. Authorization).</summary>
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool IsEnabled { get; set; } = true;
}

public sealed class EntryRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FormId { get; set; }
    public Guid FormVersionId { get; set; }
    public string SubmittedBy { get; set; } = string.Empty;
    public string SubmittedByEmail { get; set; } = string.Empty;
    public DateTimeOffset SubmittedUtc { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>UTC timestamp when the submitter first opened the form (used for completion-time analytics).</summary>
    public DateTimeOffset? StartedUtc { get; set; }
    public EntryStatus Status { get; set; } = EntryStatus.Submitted;
    public Dictionary<string, string?> Answers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> SearchIndex { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<EntryFileRecord> Files { get; set; } = [];
    public List<EntryRevisionRecord> Revisions { get; set; } = [];
    public List<ApprovalStepRecord> ApprovalSteps { get; set; } = [];
    public List<ApprovalAuditEvent> ApprovalAuditTrail { get; set; } = [];
    /// <summary>Computed quiz/scoring total. Populated at submission time when the form definition has <c>IsQuizMode = true</c>.</summary>
    public decimal? Score { get; set; }
    /// <summary>Whether the submitter passed the quiz (based on <c>FormDefinition.PassScore</c>). Null when quiz mode is off or no pass score is configured.</summary>
    public bool? QuizPassed { get; set; }
}

public sealed class EntryFileRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FieldId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long Length { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public Guid UploadedByUserId { get; set; }
    public string UploadedByEmail { get; set; } = string.Empty;
    public int RevisionNumber { get; set; }
    public DateTimeOffset UploadedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class EntryQueryOptions
{
    public Guid? FormId { get; set; }
    public EntryStatus? Status { get; set; }
    public IReadOnlyList<EntryStatus>? Statuses { get; set; }
    public DateTimeOffset? SubmittedFromUtc { get; set; }
    public DateTimeOffset? SubmittedToUtc { get; set; }
    public string? Search { get; set; }
    public string? IndexedFieldId { get; set; }
    public string? IndexedFieldValue { get; set; }
    public int Offset { get; set; }
    public int Limit { get; set; }
}

public sealed class EntryRevisionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int RevisionNumber { get; set; }
    public string EditedBy { get; set; } = string.Empty;
    public DateTimeOffset EditedUtc { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string?> Answers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ApprovalStepRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Order { get; set; }
    /// <summary>Primary approver identifier. Used when <see cref="AcceptorMode"/> is <see cref="ApprovalStepAcceptorMode.Single"/>.</summary>
    public string ApproverId { get; set; } = string.Empty;
    public string ApproverName { get; set; } = string.Empty;
    public string ApproverEmail { get; set; } = string.Empty;
    /// <summary>Controls whether a single designated approver or any acceptor in <see cref="Acceptors"/> may act on this step.</summary>
    public ApprovalStepAcceptorMode AcceptorMode { get; set; } = ApprovalStepAcceptorMode.Single;
    /// <summary>Pool of eligible acceptors when <see cref="AcceptorMode"/> is <see cref="ApprovalStepAcceptorMode.AnyOf"/>.</summary>
    public List<ApprovalAcceptor> Acceptors { get; set; } = [];
    /// <summary>Optional instructions shown to the approver(s) for this step.</summary>
    public string Instructions { get; set; } = string.Empty;
    public ApprovalStepStatus Status { get; set; } = ApprovalStepStatus.Pending;
    public string? Signature { get; set; }
    public string? RejectionReason { get; set; }
    public DateTimeOffset? CompletedUtc { get; set; }
    /// <summary>Set when this step has been delegated to another person.</summary>
    public string? DelegatedToEmail { get; set; }
    public string? DelegatedToName { get; set; }
    public DateTimeOffset? DelegatedUtc { get; set; }
}

public sealed class ApprovalAcceptor
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public sealed class ApprovalAuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ApprovalAuditAction Action { get; set; }
    public Guid? ApprovalStepId { get; set; }
    public Guid ActorUserId { get; set; }
    public string ActorDisplayName { get; set; } = string.Empty;
    public string? Signature { get; set; }
    public string? Reason { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset OccurredUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class StoredFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long Length { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
}

public sealed class FileUploadRequest
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Content { get; set; } = [];
    public long? MaxAllowedBytes { get; set; }
    public List<string> AllowedMimeTypes { get; set; } = [];
    public List<string> AllowedExtensions { get; set; } = [];
}

public sealed class UserProfile
{
    public Guid UserId { get; set; }
    public bool IsAuthenticated { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<FormPermissionRole> Roles { get; set; } = [];
}

public sealed class FormInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FormId { get; set; }
    public string Email { get; set; } = string.Empty;
    public FormPermissionRole Role { get; set; }
    public string ScopeType { get; set; } = "Form";
    public string? ScopeValue { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresUtc { get; set; }
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid? UpdatedByUserId { get; set; }
    public DateTimeOffset? UpdatedUtc { get; set; }
}

/// <summary>Aggregated analytics counters for a single form.</summary>
public sealed class FormAnalyticsSummary
{
    public Guid FormId { get; set; }
    /// <summary>Number of times the published form page was viewed.</summary>
    public long ViewCount { get; set; }
    /// <summary>Number of times a submitter began filling in the form (first field interaction or draft save).</summary>
    public long StartCount { get; set; }
    /// <summary>Number of successful (non-draft) submissions.</summary>
    public long SubmissionCount { get; set; }
    /// <summary>Number of sessions where a start was tracked but no submission followed.</summary>
    public long AbandonCount { get; set; }
    /// <summary>Completion rate: <c>SubmissionCount / StartCount</c>. Zero when no starts recorded.</summary>
    public double CompletionRate => StartCount == 0 ? 0d : (double)SubmissionCount / StartCount;
    /// <summary>Average number of seconds between form start and submission, across completed sessions.</summary>
    public double AverageCompletionSeconds { get; set; }
    public DateTimeOffset? LastUpdatedUtc { get; set; }
}
