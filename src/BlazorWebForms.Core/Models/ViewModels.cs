namespace BlazorWebForms.Core.Models;

public sealed class DashboardViewModel
{
    public required IReadOnlyList<FormAggregate> Forms { get; init; }
    public required IReadOnlyList<EntryRecord> RecentEntries { get; init; }
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
    public FormDefinition Definition { get; set; } = new();
    public List<string> NotificationEmails { get; set; } = [];
}

public sealed class SubmitEntryRequest
{
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

public sealed class ApproverInput
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
