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
}

public sealed class SubmitEntryRequest
{
    public Guid? DraftEntryId { get; set; }
    public Dictionary<string, string?> Answers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ApproverInput> Approvers { get; set; } = [];
    public List<SubmittedFileInput> Files { get; set; } = [];
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
