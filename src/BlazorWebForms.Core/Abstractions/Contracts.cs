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
