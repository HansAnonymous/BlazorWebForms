using System;
using System.Collections.Generic;

namespace BlazorWebForms.Infrastructure.SqlServer;

public class FormEntity
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid OwnerUserId { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? DraftDefinitionJson { get; set; }
    public string PublicationSlug { get; set; } = string.Empty;
    public string PublicationDomain { get; set; } = string.Empty;
    public int PublicationAccessMode { get; set; }
    public bool PublicationSendSubmissionCopyToSubmitter { get; set; }
    public int PublicationEditMode { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<FormVersionEntity> Versions { get; set; } = new List<FormVersionEntity>();
    public ICollection<FormPermissionEntity> Permissions { get; set; } = new List<FormPermissionEntity>();
    public ICollection<FormNotificationEntity> Notifications { get; set; } = new List<FormNotificationEntity>();
}

public class FormVersionEntity
{
    public Guid Id { get; set; }
    public Guid FormId { get; set; }
    public FormEntity? Form { get; set; }
    public int VersionNumber { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string DefinitionJson { get; set; } = string.Empty;
}

public class FormPermissionEntity
{
    public Guid Id { get; set; }
    public Guid FormId { get; set; }
    public FormEntity? Form { get; set; }
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int Role { get; set; }
    public string ScopeType { get; set; } = "Form";
    public string? ScopeValue { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public class FormNotificationEntity
{
    public Guid Id { get; set; }
    public Guid FormId { get; set; }
    public FormEntity? Form { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool OnSubmission { get; set; } = true;
    public bool OnApproval { get; set; } = true;
}

public class EntryEntity
{
    public Guid Id { get; set; }
    public Guid FormId { get; set; }
    public FormEntity? Form { get; set; }
    public Guid FormVersionId { get; set; }
    public string SubmittedBy { get; set; } = string.Empty;
    public string SubmittedByEmail { get; set; } = string.Empty;
    public DateTimeOffset SubmittedUtc { get; set; } = DateTimeOffset.UtcNow;
    public int Status { get; set; }
    public Dictionary<string, string?> Answers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> SearchIndex { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<EntryRevisionEntity> Revisions { get; set; } = new List<EntryRevisionEntity>();
    public ICollection<ApprovalStepEntity> ApprovalSteps { get; set; } = new List<ApprovalStepEntity>();
    public ICollection<ApprovalAuditEventEntity> ApprovalAuditTrail { get; set; } = new List<ApprovalAuditEventEntity>();
    public ICollection<EntrySearchIndexEntity> SearchIndexEntries { get; set; } = new List<EntrySearchIndexEntity>();
    public ICollection<EntryFileMetadataEntity> Files { get; set; } = new List<EntryFileMetadataEntity>();
}

public class EntryRevisionEntity
{
    public Guid Id { get; set; }
    public Guid EntryId { get; set; }
    public EntryEntity? Entry { get; set; }
    public int RevisionNumber { get; set; }
    public string EditedBy { get; set; } = string.Empty;
    public DateTimeOffset EditedUtc { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string?> Answers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class ApprovalStepEntity
{
    public Guid Id { get; set; }
    public Guid EntryId { get; set; }
    public EntryEntity? Entry { get; set; }
    public int Order { get; set; }
    public string ApproverName { get; set; } = string.Empty;
    public string ApproverEmail { get; set; } = string.Empty;
    public int Status { get; set; }
    public string? Signature { get; set; }
    public string? RejectionReason { get; set; }
    public DateTimeOffset? CompletedUtc { get; set; }
}

public class ApprovalAuditEventEntity
{
    public Guid Id { get; set; }
    public Guid EntryId { get; set; }
    public EntryEntity? Entry { get; set; }
    public int Action { get; set; }
    public Guid? ApprovalStepId { get; set; }
    public Guid ActorUserId { get; set; }
    public string ActorDisplayName { get; set; } = string.Empty;
    public string? Signature { get; set; }
    public string? Reason { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset OccurredUtc { get; set; } = DateTimeOffset.UtcNow;
}

public class EntrySearchIndexEntity
{
    public Guid Id { get; set; }
    public Guid EntryId { get; set; }
    public EntryEntity? Entry { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class EntryFileMetadataEntity
{
    public Guid Id { get; set; }
    public Guid EntryId { get; set; }
    public EntryEntity? Entry { get; set; }
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

public class FormInvitationEntity
{
    public Guid Id { get; set; }
    public Guid FormId { get; set; }
    public FormEntity? Form { get; set; }
    public string Email { get; set; } = string.Empty;
    public int Role { get; set; }
    public string ScopeType { get; set; } = "Form";
    public string? ScopeValue { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresUtc { get; set; }
    public int Status { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid? UpdatedByUserId { get; set; }
    public DateTimeOffset? UpdatedUtc { get; set; }
}
