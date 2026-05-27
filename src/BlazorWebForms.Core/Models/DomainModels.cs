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
}

public sealed class FormPermissionGrant
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public FormPermissionRole Role { get; set; }
}

public sealed class FormNotificationRule
{
    public string Email { get; set; } = string.Empty;
    public bool OnSubmission { get; set; } = true;
    public bool OnApproval { get; set; } = true;
}

public sealed class EntryRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FormId { get; set; }
    public Guid FormVersionId { get; set; }
    public string SubmittedBy { get; set; } = string.Empty;
    public string SubmittedByEmail { get; set; } = string.Empty;
    public DateTimeOffset SubmittedUtc { get; set; } = DateTimeOffset.UtcNow;
    public EntryStatus Status { get; set; } = EntryStatus.Submitted;
    public Dictionary<string, string?> Answers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> SearchIndex { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<EntryFileRecord> Files { get; set; } = [];
    public List<EntryRevisionRecord> Revisions { get; set; } = [];
    public List<ApprovalStepRecord> ApprovalSteps { get; set; } = [];
}

public sealed class EntryFileRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FieldId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long Length { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public DateTimeOffset UploadedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class EntryQueryOptions
{
    public Guid? FormId { get; set; }
    public EntryStatus? Status { get; set; }
    public DateTimeOffset? SubmittedFromUtc { get; set; }
    public DateTimeOffset? SubmittedToUtc { get; set; }
    public string? Search { get; set; }
    public string? IndexedFieldId { get; set; }
    public string? IndexedFieldValue { get; set; }
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
    public string ApproverName { get; set; } = string.Empty;
    public string ApproverEmail { get; set; } = string.Empty;
    public ApprovalStepStatus Status { get; set; } = ApprovalStepStatus.Pending;
    public string? Signature { get; set; }
    public DateTimeOffset? CompletedUtc { get; set; }
}

public sealed class StoredFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long Length { get; set; }
    public string RelativePath { get; set; } = string.Empty;
}

public sealed class FileUploadRequest
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Content { get; set; } = [];
}

public sealed class UserProfile
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<FormPermissionRole> Roles { get; set; } = [];
}
