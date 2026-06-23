# BlazorWebForms.Core — Domain Models Reference

Detailed reference for all domain models used throughout the Core package.

---

## Table of Contents

1. [Form Aggregates](#form-aggregates)
2. [Entry and Submission Models](#entry-and-submission-models)
3. [Form Definition Models](#form-definition-models)
4. [Visibility and Conditions](#visibility-and-conditions)
5. [Branding](#branding)
6. [Approval Workflow](#approval-workflow)
7. [View Models](#view-models)
8. [Request/Response DTOs](#requestresponse-dtos)

---

## Form Aggregates

### FormAggregate

Root aggregate for a form. Contains metadata, definition, versions, and permissions.

```csharp
public sealed class FormAggregate
{
	public Guid Id { get; set; }
	public string Name { get; set; }
	public string Description { get; set; }
	public FormPublication Publication { get; set; }
	public FormDefinition DraftDefinition { get; set; }
	public List<FormVersionRecord> Versions { get; set; }
	public List<FormPermissionGrant> Permissions { get; set; }
	public DateTimeOffset CreatedUtc { get; set; }
	public Guid CreatedByUserId { get; set; }
	public DateTimeOffset UpdatedUtc { get; set; }
	public Guid UpdatedByUserId { get; set; }
}
```

| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Unique form identifier |
| `Name` | `string` | Display name (e.g. "Travel Request Form") |
| `Description` | `string` | Free-form description for internal use |
| `Publication` | `FormPublication` | Slug, access mode, edit mode, published state |
| `DraftDefinition` | `FormDefinition` | Unpublished form schema (user is currently editing) |
| `Versions` | `List<FormVersionRecord>` | Published version snapshots (immutable, ordered by version number) |
| `Permissions` | `List<FormPermissionGrant>` | Who can manage/submit/view this form |
| `CreatedUtc` | `DateTimeOffset` | Form creation timestamp (UTC) |
| `CreatedByUserId` | `Guid` | GUID of user who created the form |
| `UpdatedUtc` | `DateTimeOffset` | Last modification timestamp (UTC) |
| `UpdatedByUserId` | `Guid` | GUID of user who last modified the form |

**Invariants**:
- `Id` is never empty
- `Name` is never empty
- At least one version must exist if `IsPublished = true`
- First user to save a form is automatically granted `Owner` role

---

### FormPublication

Publication metadata for a form.

```csharp
public sealed class FormPublication
{
	public string Slug { get; set; }
	public FormAccessMode AccessMode { get; set; }
	public FormEditMode EditMode { get; set; }
	public bool IsPublished { get; set; }
}
```

| Property | Description |
|---|---|
| `Slug` | URL slug (e.g. `"travel-request"`). Must be unique within access mode. Used in routes like `/forms/{slug}` |
| `AccessMode` | Who can access the published form |
| `EditMode` | Whether entries can be revised after approval |
| `IsPublished` | `true` if at least one `FormVersionRecord` exists |

**Slug rules**:
- Must be 3–100 characters
- Alphanumeric, hyphens, underscores only
- Lowercase preferred
- Unique across forms with the same `AccessMode` (e.g. two public forms cannot share a slug)

---

### FormAccessMode

```csharp
public enum FormAccessMode
{
	Authenticated = 0,
	Public = 1,
	InvitationOnly = 2
}
```

| Value | Who can submit |
|---|---|
| `Authenticated` | Any authenticated user (requires sign-in) |
| `Public` | Anyone, including anonymous users (no sign-in required) |
| `InvitationOnly` | Only users who have accepted an invitation for this form |

---

### FormEditMode

```csharp
public enum FormEditMode
{
	DraftOnly = 0,
	OverwriteLatest = 1
}
```

| Value | Behavior after approval |
|---|---|
| `DraftOnly` | Entry cannot be revised; submission is final |
| `OverwriteLatest` | Entry can be revised via `ReviseEntryAsync()` after approval |

---

### FormVersionRecord

Immutable snapshot of a form definition at the time of publication.

```csharp
public sealed class FormVersionRecord
{
	public Guid Id { get; set; }
	public Guid FormId { get; set; }
	public int VersionNumber { get; set; }
	public string DefinitionJson { get; set; }
	public DateTimeOffset PublishedUtc { get; set; }
	public Guid PublishedByUserId { get; set; }
	public string PublishedByName { get; set; }
}
```

| Property | Description |
|---|---|
| `Id` | Unique version record GUID |
| `FormId` | Parent form ID |
| `VersionNumber` | Auto-incrementing version (1, 2, 3, ...) |
| `DefinitionJson` | Serialized `FormDefinition` (immutable) |
| `PublishedUtc` | Publication timestamp (UTC) |
| `PublishedByUserId` | GUID of publisher |
| `PublishedByName` | Display name of publisher (denormalized for audit trail) |

---

### FormPermissionGrant

```csharp
public sealed class FormPermissionGrant
{
	public Guid Id { get; set; }
	public Guid FormId { get; set; }
	public Guid UserId { get; set; }
	public string UserEmail { get; set; }
	public FormPermissionRole Role { get; set; }
	public DateTimeOffset GrantedUtc { get; set; }
	public Guid GrantedByUserId { get; set; }
}
```

| Property | Description |
|---|---|
| `Id` | Grant GUID |
| `FormId` | Form this permission applies to |
| `UserId` | User receiving the permission |
| `UserEmail` | User's email (denormalized for notifications) |
| `Role` | Permission role (Admin, Owner, Manager, Approver, Submitter, Viewer, SelfViewer) |
| `GrantedUtc` | When the permission was granted |
| `GrantedByUserId` | Who granted it |

**Roles**:
- `Admin` — Full control across all forms
- `Owner` — Full control on owned forms
- `Manager` — Can manage, publish, approve
- `Approver` — Can approve entries
- `Submitter` — Can submit forms
- `Viewer` — Can view all entries (read-only)
- `SelfViewer` — Can view own entries only

---

## Entry and Submission Models

### EntryRecord

Submitted form entry or draft.

```csharp
public sealed class EntryRecord
{
	public Guid Id { get; set; }
	public Guid FormId { get; set; }
	public Guid SubmittedByUserId { get; set; }
	public string SubmittedByName { get; set; }
	public string SubmittedByEmail { get; set; }
	public DateTimeOffset SubmittedUtc { get; set; }
	public EntryStatus Status { get; set; }
	public Dictionary<string, string?> Answers { get; set; }
	public List<EntryFileRecord> Files { get; set; }
	public List<ApprovalStepRecord> ApprovalSteps { get; set; }
	public List<ApprovalAuditEvent> ApprovalAuditTrail { get; set; }
	public List<RevisionRecord> Revisions { get; set; }
}
```

| Property | Description |
|---|---|
| `Id` | Entry GUID |
| `FormId` | Parent form ID |
| `SubmittedByUserId` | GUID of submitter |
| `SubmittedByName` | Submitter display name (denormalized) |
| `SubmittedByEmail` | Submitter email (denormalized) |
| `SubmittedUtc` | When entry was submitted |
| `Status` | Current approval status |
| `Answers` | Field values keyed by field id (e.g. `["name"] = "John Doe"`) |
| `Files` | Uploaded file metadata |
| `ApprovalSteps` | Approval workflow steps |
| `ApprovalAuditTrail` | History of all approval actions |
| `Revisions` | If `EditMode = OverwriteLatest`, records of revisions made to approved entries |

**Answer format**:
- Field id as key (string)
- Answer as value (string or null)
- Checkboxes stored as `"true"` or `"false"`
- Multi-select stored as comma-separated values (implementation-dependent)

---

### EntryStatus

```csharp
public enum EntryStatus
{
	Draft = 0,
	NeedsApproval = 1,
	Approved = 2,
	Rejected = 3
}
```

| Value | Meaning |
|---|---|
| `Draft` | Entry saved but not submitted; not visible to approvers |
| `NeedsApproval` | Submitted and awaiting approval |
| `Approved` | All approval steps completed |
| `Rejected` | At least one approver rejected; submitter can resubmit |

---

### EntryFileRecord

```csharp
public sealed class EntryFileRecord
{
	public Guid Id { get; set; }
	public Guid EntryId { get; set; }
	public string FieldId { get; set; }
	public string FileName { get; set; }
	public string ContentType { get; set; }
	public long Length { get; set; }
	public string Sha256Hash { get; set; }
	public string StoragePath { get; set; }
	public DateTimeOffset UploadedUtc { get; set; }
}
```

---

### RevisionRecord

```csharp
public sealed class RevisionRecord
{
	public Guid Id { get; set; }
	public Guid EntryId { get; set; }
	public Dictionary<string, string?> ChangedFields { get; set; }
	public Guid RevisedByUserId { get; set; }
	public string RevisedByName { get; set; }
	public DateTimeOffset RevisedUtc { get; set; }
}
```

Records modifications made to an approved entry (only when `EditMode = OverwriteLatest`).

---

## Form Definition Models

### FormDefinition

Serializable form schema. Used as:
1. Draft definition during editing (mutable)
2. Version snapshot after publishing (immutable)

```csharp
public sealed class FormDefinition
{
	public int SchemaVersion { get; set; }
	public string Title { get; set; }
	public string Description { get; set; }
	public string DefaultCulture { get; set; }
	public BrandingDefinition Branding { get; set; }
	public List<FormSectionDefinition> Sections { get; set; }
	public Dictionary<string, string> LocalizedTitles { get; set; }
	public Dictionary<string, string> LocalizedDescriptions { get; set; }

	public const int CurrentSchemaVersion = 2;
}
```

| Property | Description |
|---|---|
| `SchemaVersion` | Always `2` (current). Used for forward-compatibility. Legacy definitions upgraded on deserialization. |
| `Title` | Form name displayed to submitters |
| `Description` | Form description/instructions |
| `DefaultCulture` | Default culture code (e.g. `"en-US"`). Used as fallback if submitted form is in a different culture. |
| `Branding` | Logo, colors, hero text, etc. |
| `Sections` | Ordered list of form sections |
| `LocalizedTitles` | Culture-specific form titles (keyed by culture code) |
| `LocalizedDescriptions` | Culture-specific descriptions |

**Localization pattern**:
- Base property (e.g. `Title`) used if exact culture not found
- Culture fallback: exact culture → language only → `DefaultCulture` → base property
- Example: If user requests `"fr-CA"` and only `"fr"` exists, use `"fr"`; else fall back to `DefaultCulture` or base text

---

### FormSectionDefinition

```csharp
public sealed class FormSectionDefinition
{
	public string Id { get; set; }
	public string Title { get; set; }
	public string Description { get; set; }
	public VisibilityConditionDefinition? VisibilityRules { get; set; }
	public FormSectionLayoutDefinition? Layout { get; set; }
	public Dictionary<string, string> LocalizedTitles { get; set; }
	public Dictionary<string, string> LocalizedDescriptions { get; set; }
	public List<FormFieldDefinition> Fields { get; set; }
}
```

A logical grouping of form fields.

| Property | Description |
|---|---|
| `Id` | Unique section identifier within the form |
| `Title` | Section heading |
| `Description` | Optional section instructions |
| `VisibilityRules` | Conditional display (section is hidden if rules evaluate to false) |
| `Layout` | Column count and other layout hints |
| `Fields` | Ordered list of fields in this section |

---

### FormSectionLayoutDefinition

```csharp
public sealed class FormSectionLayoutDefinition
{
	public int Columns { get; set; }  // 1, 2, 3, or 4
}
```

---

### FormFieldDefinition

```csharp
public sealed class FormFieldDefinition
{
	public string Id { get; set; }
	public FormFieldKind Kind { get; set; }
	public string Label { get; set; }
	public string Placeholder { get; set; }
	public string HelpText { get; set; }
	public bool Required { get; set; }
	public bool Searchable { get; set; }
	public string? RegexPattern { get; set; }
	public string? DefaultValue { get; set; }
	public string? ValidationHint { get; set; }
	public VisibilityConditionDefinition? VisibilityRules { get; set; }
	public FormFieldLayoutDefinition? Layout { get; set; }

	// Localization
	public Dictionary<string, string> LocalizedLabels { get; set; }
	public Dictionary<string, string> LocalizedPlaceholders { get; set; }
	public Dictionary<string, string> LocalizedHelpTexts { get; set; }
	public Dictionary<string, string> LocalizedValidationHints { get; set; }

	// File field constraints
	public long? MaxFileSizeBytes { get; set; }
	public int MaxFileCount { get; set; }
	public List<string> AllowedMimeTypes { get; set; }
	public List<string> AllowedExtensions { get; set; }

	// Select/Radio options
	public List<FormFieldOption> Options { get; set; }
}
```

| Property | Description |
|---|---|
| `Id` | Unique field identifier within the form |
| `Kind` | Field type (Text, Select, File, etc.) |
| `Label` | Field label/question |
| `Placeholder` | Hint text in input |
| `HelpText` | Additional instructions below label |
| `Required` | Whether field must be filled before submit |
| `Searchable` | If `true`, field value is indexed for admin search |
| `RegexPattern` | Optional validation pattern (applied at submit time) |
| `DefaultValue` | Pre-filled value on load |
| `ValidationHint` | Error message if regex fails |
| `VisibilityRules` | Conditional display |
| `Layout` | Width hint (auto, half, third, full, etc.) |
| `LocalizedLabels`, etc. | Culture-specific text |
| `MaxFileSizeBytes` | Max file size for File fields |
| `MaxFileCount` | Max number of files (1 = single, >1 = multi-select) |
| `AllowedMimeTypes` | Whitelist of accepted MIME types |
| `AllowedExtensions` | Whitelist of accepted file extensions (with `.` prefix) |
| `Options` | Choices for Select/Radio fields |

---

### FormFieldKind

```csharp
public enum FormFieldKind
{
	Text = 0,
	TextArea = 1,
	Number = 2,
	Select = 3,
	Checkbox = 4,
	Radio = 5,
	Date = 6,
	File = 7,
	RichText = 8,
	Signature = 9
}
```

| Kind | UI rendered as | Answer format |
|---|---|---|
| `Text` | Single-line text input | String value |
| `TextArea` | Multi-line textarea | String value (may contain newlines) |
| `Number` | Numeric input | String representation of number (e.g. `"42"`, `"3.14"`) |
| `Select` | Dropdown (`<select>`) | Selected option value (string) |
| `Checkbox` | Boolean checkbox | `"true"` or `"false"` or empty |
| `Radio` | Radio button group | Selected option value (string) |
| `Date` | Date picker | ISO 8601 date string (e.g. `"2024-12-25"`) |
| `File` | File upload | File metadata (stored separately in `EntryFileRecord`) |
| `RichText` | Rich text editor (HTML) | HTML string |
| `Signature` | Signature capture (canvas) | Signature data (format implementation-dependent) |

---

### FormFieldOption

```csharp
public sealed class FormFieldOption
{
	public string Value { get; set; }
	public string Label { get; set; }
	public Dictionary<string, string> LocalizedLabels { get; set; }
}
```

Used for Select, Radio, and Checkbox groups.

| Property | Description |
|---|---|
| `Value` | The option value (submitted if selected) |
| `Label` | Display text for the option |
| `LocalizedLabels` | Culture-specific display text |

---

### FormFieldLayoutDefinition

```csharp
public sealed class FormFieldLayoutDefinition
{
	public string WidthHint { get; set; }  // "Auto", "Half", "Third", "Full", "TwoThirds", etc.
}
```

---

## Visibility and Conditions

### VisibilityConditionDefinition

Controls when sections and fields are shown or hidden based on other field values.

```csharp
public sealed class VisibilityConditionDefinition
{
	public VisibilityJoinOperator Join { get; set; }
	public List<VisibilityRuleDefinition> Rules { get; set; }
}
```

| Property | Description |
|---|---|
| `Join` | `And` — all rules must be true; `Or` — at least one must be true |
| `Rules` | List of individual visibility rules |

**Example**:

```csharp
// Show field only if employment status is "Employed" AND department is "Engineering"
var condition = new VisibilityConditionDefinition
{
	Join = VisibilityJoinOperator.And,
	Rules = new()
	{
		new VisibilityRuleDefinition
		{
			FieldId = "employment-status",
			Operator = VisibilityRuleOperator.Equals,
			Value = "Employed"
		},
		new VisibilityRuleDefinition
		{
			FieldId = "department",
			Operator = VisibilityRuleOperator.Equals,
			Value = "Engineering"
		}
	}
};
```

---

### VisibilityJoinOperator

```csharp
public enum VisibilityJoinOperator
{
	And = 0,
	Or = 1
}
```

---

### VisibilityRuleDefinition

```csharp
public sealed class VisibilityRuleDefinition
{
	public string FieldId { get; set; }
	public VisibilityRuleOperator Operator { get; set; }
	public string Value { get; set; }
}
```

| Property | Description |
|---|---|
| `FieldId` | Field to check (must exist in form) |
| `Operator` | Comparison operator |
| `Value` | Value to compare against |

---

### VisibilityRuleOperator

```csharp
public enum VisibilityRuleOperator
{
	Equals = 0,
	NotEquals = 1,
	Contains = 2,
	Empty = 3
}
```

| Operator | Meaning |
|---|---|
| `Equals` | Field answer equals `Value` exactly (case-sensitive) |
| `NotEquals` | Field answer does not equal `Value` |
| `Contains` | Field answer contains `Value` as substring (case-insensitive) |
| `Empty` | Field answer is null or empty string (ignores `Value`) |

---

## Branding

### BrandingDefinition

```csharp
public sealed class BrandingDefinition
{
	public string? LogoUrl { get; set; }
	public string? LogoFileRef { get; set; }
	public string? HeroImageUrl { get; set; }
	public string? HeroImageFileRef { get; set; }
	public string AccentColor { get; set; }
	public string SurfaceColor { get; set; }
	public string TextColor { get; set; }
	public string ButtonRadius { get; set; }
	public string HeroText { get; set; }
}
```

| Property | Description |
|---|---|
| `LogoUrl` | HTTP URL to logo image |
| `LogoFileRef` | Relative path if logo is stored in file system |
| `HeroImageUrl` | HTTP URL to hero image |
| `HeroImageFileRef` | Relative path if hero image is stored in file system |
| `AccentColor` | Primary brand color (hex or CSS color, e.g. `"#0f766e"`) |
| `SurfaceColor` | Background color (hex or CSS color) |
| `TextColor` | Text color for hero area (hex or CSS color) |
| `ButtonRadius` | CSS border-radius for buttons (e.g. `"4px"`, `"999px"`) |
| `HeroText` | Call-to-action text displayed in hero area |

---

## Approval Workflow

### ApprovalStepRecord

```csharp
public sealed class ApprovalStepRecord
{
	public Guid Id { get; set; }
	public int Order { get; set; }
	public string ApproverName { get; set; }
	public string ApproverEmail { get; set; }
	public ApprovalStepStatus Status { get; set; }
	public string? Signature { get; set; }
	public string? RejectionReason { get; set; }
	public DateTimeOffset? CompletedUtc { get; set; }
}
```

---

### ApprovalStepStatus

```csharp
public enum ApprovalStepStatus
{
	Pending = 0,
	Approved = 1,
	Rejected = 2
}
```

---

### ApprovalAuditEvent

```csharp
public sealed class ApprovalAuditEvent
{
	public ApprovalAuditAction Action { get; set; }
	public Guid ActorUserId { get; set; }
	public string ActorDisplayName { get; set; }
	public string? Signature { get; set; }
	public string? Reason { get; set; }
	public string? CorrelationId { get; set; }
	public DateTimeOffset OccurredUtc { get; set; }
}
```

Record of every approval action (approve, reject, resubmit, reminder sent, etc.).

---

### ApprovalAuditAction

```csharp
public enum ApprovalAuditAction
{
	StepApproved = 0,
	StepRejected = 1,
	EntryResubmitted = 2,
	ReminderSent = 3,
	EntryApproved = 4
}
```

---

## View Models

### DashboardViewModel

```csharp
public sealed class DashboardViewModel
{
	public List<FormAggregate> Forms { get; set; }
	public List<EntryRecord> RecentEntries { get; set; }
}
```

Used by `GetDashboardAsync()`. Contains forms managed by current user and recent submissions.

---

### BuilderState

```csharp
public sealed class BuilderState
{
	public FormAggregate Form { get; set; }
}
```

Container returned by `GetBuilderStateAsync()`.

---

### PublishedFormViewModel

```csharp
public sealed class PublishedFormViewModel
{
	public FormAggregate Form { get; set; }
	public FormVersionRecord Version { get; set; }
	public FormDefinition Definition { get; set; }
}
```

Data passed to a published form view:
- `Form` — form metadata and publication settings
- `Version` — the currently published version record
- `Definition` — deserialized form definition from the version

---

### EntryDetailViewModel

```csharp
public sealed class EntryDetailViewModel
{
	public FormAggregate Form { get; set; }
	public EntryRecord Entry { get; set; }
	public FormDefinition Definition { get; set; }
	public bool CanView { get; set; }
	public string? HistoricalRenderWarning { get; set; }
}
```

Data for entry detail page:
- `Form` — parent form
- `Entry` — submitted entry
- `Definition` — the published version's definition (for rendering)
- `CanView` — permission check result
- `HistoricalRenderWarning` — if version was deleted, this explains the situation

---

## Request/Response DTOs

### SaveDraftRequest

```csharp
public sealed class SaveDraftRequest
{
	public Guid? FormId { get; set; }
	public string Name { get; set; }
	public string Description { get; set; }
	public string Slug { get; set; }
	public FormAccessMode AccessMode { get; set; }
	public FormEditMode EditMode { get; set; }
	public FormDefinition Definition { get; set; }
}
```

Request to save a form draft. If `FormId` is null, creates a new form.

---

### SubmitEntryRequest

```csharp
public sealed class SubmitEntryRequest
{
	public Guid? DraftEntryId { get; set; }
	public Dictionary<string, string?> Answers { get; set; }
	public List<ApproverInput> Approvers { get; set; }
	public List<SubmittedFileInput> Files { get; set; }
}
```

Request to submit a form entry.

---

### SaveDraftSubmissionRequest

```csharp
public sealed class SaveDraftSubmissionRequest
{
	public Guid? DraftEntryId { get; set; }
	public Dictionary<string, string?> Answers { get; set; }
	public List<ApproverInput> Approvers { get; set; }
	public List<SubmittedFileInput> Files { get; set; }
}
```

Request to save a draft entry.

---

### ApproverInput

```csharp
public sealed class ApproverInput
{
	public string Name { get; set; }
	public string Email { get; set; }
}
```

Approver specification at submit time.

---

### SubmittedFileInput

```csharp
public sealed class SubmittedFileInput
{
	public string FieldId { get; set; }
	public StoredFile File { get; set; }
}
```

Uploaded file metadata.

---

### StoredFile

```csharp
public sealed class StoredFile
{
	public string RelativePath { get; set; }
	public string FileName { get; set; }
	public string ContentType { get; set; }
	public long Length { get; set; }
	public string Sha256 { get; set; }
}
```

File metadata returned after `StoreFileAsync()`.

---

### CreateInvitationRequest

```csharp
public sealed class CreateInvitationRequest
{
	public Guid FormId { get; set; }
	public string Email { get; set; }
	public FormPermissionRole Role { get; set; }
	public TimeSpan ValidFor { get; set; }
}
```

---

### FormInvitation

```csharp
public sealed class FormInvitation
{
	public Guid Id { get; set; }
	public Guid FormId { get; set; }
	public string Email { get; set; }
	public FormPermissionRole Role { get; set; }
	public string Token { get; set; }
	public InvitationStatus Status { get; set; }
	public DateTimeOffset CreatedUtc { get; set; }
	public DateTimeOffset ExpiresUtc { get; set; }
	public DateTimeOffset? AcceptedUtc { get; set; }
}
```

---

### InvitationStatus

```csharp
public enum InvitationStatus
{
	Pending = 0,
	Accepted = 1,
	Expired = 2,
	Revoked = 3
}
```

---

### UserProfile

```csharp
public sealed class UserProfile
{
	public Guid UserId { get; set; }
	public bool IsAuthenticated { get; set; }
	public string DisplayName { get; set; }
	public string Email { get; set; }
	public List<FormPermissionRole> Roles { get; set; }
}
```

Returned by `ICurrentUserContext.GetCurrentUser()`. Represents the currently authenticated user.

---

## Next Steps

- See [CORE_API_REFERENCE.md](./CORE_API_REFERENCE.md) for FormsApplicationService methods
- See [CORE_LOCALIZATION.md](./CORE_LOCALIZATION.md) for localization and condition evaluation
- See [CORE_EXTENDING.md](./CORE_EXTENDING.md) for implementing interfaces
