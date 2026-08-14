using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Core.Abstractions;

public interface IFormDefinitionSerializer
{
    string Serialize(FormDefinition definition);
    FormDefinition Deserialize(string json);
}

public interface IConditionEvaluator
{
    bool IsVisible(string? expression, IReadOnlyDictionary<string, string?> answers);
    bool IsVisible(VisibilityConditionDefinition? condition, IReadOnlyDictionary<string, string?> answers);
}

public interface IFileStorage
{
    Task<StoredFile> SaveAsync(FileUploadRequest request, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
}

public interface IPdfExporter
{
    Task<byte[]> ExportEntryAsync(FormAggregate form, EntryRecord entry, CancellationToken cancellationToken = default);
}

public interface IEmailNotifier
{
    Task NotifyManagersAsync(FormAggregate form, EntryRecord entry, CancellationToken cancellationToken = default);
    Task<string?> NotifyApproverAssignedAsync(FormAggregate form, EntryRecord entry, ApprovalStepRecord step, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<string?> NotifyApproverReminderAsync(FormAggregate form, EntryRecord entry, ApprovalStepRecord step, string idempotencyKey, CancellationToken cancellationToken = default);
    Task NotifyEntryApprovedAsync(FormAggregate form, EntryRecord entry, CancellationToken cancellationToken = default);
    Task NotifyEntryRejectedAsync(FormAggregate form, EntryRecord entry, ApprovalStepRecord step, CancellationToken cancellationToken = default);
    Task NotifyInvitationCreatedAsync(FormAggregate form, FormInvitation invitation, CancellationToken cancellationToken = default);
    Task NotifyInvitationAcceptedAsync(FormAggregate form, FormInvitation invitation, CancellationToken cancellationToken = default);
    Task NotifyInvitationRevokedAsync(FormAggregate form, FormInvitation invitation, CancellationToken cancellationToken = default);
}

public interface ICurrentUserContext
{
    UserProfile GetCurrentUser();
}

public interface IEmployeePrefillProvider
{
    Task<IReadOnlyDictionary<string, string?>> GetEmployeeDataAsync(UserProfile requester, string employeeEmail, CancellationToken cancellationToken = default);
}

public interface IFormPrefillProvider
{
    string ProviderKey { get; }
    bool CanResolve(FormFieldPrefillDefinition prefill);
    Task<string?> ResolveAsync(FormPrefillRequest request, CancellationToken cancellationToken = default);
}

public interface IEmailIntegration
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}

public interface IGraphIntegration
{
    Task<GraphIntegrationResult> ExecuteAsync(string operation, IReadOnlyDictionary<string, string?> parameters, CancellationToken cancellationToken = default);
    Task<GraphIntegrationResult> SendApprovalReminderAsync(FormAggregate form, EntryRecord entry, ApprovalStepRecord step, CancellationToken cancellationToken = default);
    Task<GraphIntegrationResult> SendInvitationAsync(FormAggregate form, FormInvitation invitation, CancellationToken cancellationToken = default);
}

public interface IPdfIntegration
{
    Task<byte[]> RenderAsync(string title, IReadOnlyDictionary<string, string?> fields, CancellationToken cancellationToken = default);
}

public interface IAntiAbuseGuard
{
    Task CheckUploadAllowedAsync(UserProfile user, FileUploadRequest request, CancellationToken cancellationToken = default);
    Task CheckOutboundNotificationAllowedAsync(string channel, string recipient, CancellationToken cancellationToken = default);
}

public interface IOperationalTelemetry
{
    void TrackUpload(string source, long bytes, bool success);
    void TrackPdfExport(string source, long bytes, bool success);
    void TrackEmailDelivery(string channel, bool success);
    void TrackFailure(string area, string operation, string reason);
}

public sealed class GraphIntegrationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public Dictionary<string, string?> Data { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public interface IPermissionEvaluator
{
    bool CanManageForm(FormAggregate form, UserProfile user);
    bool CanSubmitForm(FormAggregate form, UserProfile user);
    bool CanViewEntry(FormAggregate form, EntryRecord entry, UserProfile user);
}

public interface IFormsRepository
{
    Task SeedAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FormAggregate>> GetFormsAsync(CancellationToken cancellationToken = default);
    Task<FormAggregate?> GetFormAsync(Guid formId, CancellationToken cancellationToken = default);
    Task<FormAggregate?> GetFormBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task SaveFormAsync(FormAggregate form, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EntryRecord>> GetEntriesAsync(Guid? formId, string? search, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EntryRecord>> QueryEntriesAsync(EntryQueryOptions options, CancellationToken cancellationToken = default);
    Task<EntryRecord?> GetEntryAsync(Guid entryId, CancellationToken cancellationToken = default);
    Task<EntryRecord?> GetDraftEntryAsync(Guid formId, string submittedByEmail, CancellationToken cancellationToken = default);
    Task SaveEntryAsync(EntryRecord entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EntryFileRecord>> GetOrphanedFilesAsync(DateTimeOffset olderThanUtc, CancellationToken cancellationToken = default);
    Task<int> DeleteDraftEntriesOlderThanAsync(DateTimeOffset olderThanUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FormInvitation>> GetInvitationsAsync(Guid formId, CancellationToken cancellationToken = default);
    Task<FormInvitation?> GetInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default);
    Task<FormInvitation?> GetInvitationByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task SaveInvitationAsync(FormInvitation invitation, CancellationToken cancellationToken = default);
}

public interface ICustomFieldHandler
{
    string Kind { get; }
    void ValidateDefinition(FormFieldDefinition field);
}

/// <summary>Dispatches webhook payloads to configured endpoint URLs on form lifecycle events.</summary>
public interface IWebhookDispatcher
{
    /// <summary>
    /// Dispatches a webhook for the given <paramref name="triggerEvent"/> to all matching,
    /// enabled endpoints on <paramref name="form"/>. Implementations are expected to be
    /// fire-and-forget; failures should be logged but must not propagate.
    /// </summary>
    Task DispatchAsync(
        FormAggregate form,
        WebhookTriggerEvent triggerEvent,
        EntryRecord entry,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a test delivery to a single <paramref name="webhook"/> endpoint and returns
    /// a result indicating whether the delivery succeeded. Unlike <see cref="DispatchAsync"/>
    /// this method propagates errors to the caller so they can be surfaced in the UI.
    /// </summary>
    Task<WebhookTestResult> SendTestDeliveryAsync(
        FormAggregate form,
        FormWebhookDefinition webhook,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Evaluates formula expressions that reference form field values.
/// The default implementation handles basic arithmetic (<c>+</c> <c>-</c> <c>*</c> <c>/</c>) and
/// field references (<c>{fieldId}</c>). Replace with a full expression engine via DI for advanced use.
/// </summary>
public interface IFormulaEvaluator
{
    /// <summary>
    /// Evaluates <paramref name="expression"/> against the provided <paramref name="answers"/> dictionary.
    /// Returns the result as a string, or <c>null</c> if the expression cannot be evaluated.
    /// </summary>
    string? Evaluate(string expression, IReadOnlyDictionary<string, string?> answers);
}

/// <summary>
/// Validates a CAPTCHA token supplied by the client. The default implementation is a pass-through
/// that always returns <c>true</c>. Replace with a real provider (reCAPTCHA, hCaptcha, Turnstile) via DI.
/// </summary>
public interface ICaptchaValidator
{
    Task<bool> ValidateAsync(string token, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists and retrieves form-level analytics counters. The default implementation is a no-op.
/// Replace via DI with a real store (SQL, Redis, Application Insights, etc.).
/// </summary>
public interface IFormAnalyticsStore
{
    /// <summary>Increments the view counter for a form (called when the published form page is rendered).</summary>
    Task TrackViewAsync(Guid formId, CancellationToken cancellationToken = default);
    /// <summary>Increments the start counter (called when a draft is first saved or the first field is answered).</summary>
    Task TrackStartAsync(Guid formId, CancellationToken cancellationToken = default);
    /// <summary>Increments the submission counter and records the completion duration.</summary>
    Task TrackSubmissionAsync(Guid formId, TimeSpan? completionTime, CancellationToken cancellationToken = default);
    /// <summary>Increments the abandon counter (called when a draft ages out without a corresponding submission).</summary>
    Task TrackAbandonAsync(Guid formId, CancellationToken cancellationToken = default);
    /// <summary>Returns the aggregated analytics summary for a form.</summary>
    Task<FormAnalyticsSummary> GetSummaryAsync(Guid formId, CancellationToken cancellationToken = default);
}
