# BlazorWebForms.Core — API Reference

Complete technical reference for the `BlazorWebForms.Core` package. This package contains all domain models, business logic, and service contracts required by any BlazorWebForms integration.

---

## Table of Contents

1. [Overview](#overview)
2. [FormsApplicationService](#formsapplicationservice)
3. [Domain Models](#domain-models)
4. [Service Interfaces](#service-interfaces)
5. [Validation](#validation)
6. [Condition Evaluation](#condition-evaluation)
7. [Error Handling](#error-handling)

---

## Overview

### Architecture

`BlazorWebForms.Core` is the foundation layer providing:

- **Domain models**: `FormAggregate`, `EntryRecord`, `FormDefinition`, etc.
- **Serialization**: JSON (de)serialization of form definitions
- **Business logic**: `FormsApplicationService` — the single entry point for all operations
- **Abstractions**: Interfaces for extensibility (`IFormsRepository`, `IFileStorage`, `IEmailNotifier`, etc.)
- **Helpers**: Condition evaluators, permission checkers, localization resolvers, layout helpers

The package has **no dependencies on UI frameworks, databases, or external services**. It is completely framework-agnostic and can be embedded in any .NET application.

### Project structure

```
src/BlazorWebForms.Core/
├── Abstractions/
│   ├── Contracts.cs              # Core interfaces (IFormsRepository, IFileStorage, etc.)
│   └── ICoreMetadataCache.cs     # In-memory caching interface
├── Models/
│   ├── DomainModels.cs           # FormAggregate, EntryRecord, FormVersionRecord, etc.
│   ├── Enums.cs                  # FormFieldKind, FormAccessMode, EntryStatus, etc.
│   ├── FormDefinitionModels.cs   # FormDefinition, FormSectionDefinition, FormFieldDefinition, etc.
│   └── ViewModels.cs             # DashboardViewModel, PublishedFormViewModel, etc.
├── Services/
│   ├── FormsApplicationService.cs # Main business logic service (scoped)
│   ├── ConditionEvaluationHelper.cs
│   ├── FormLocalizationResolver.cs
│   ├── FormLayoutResolver.cs
│   ├── ApprovalStepGuard.cs
│   ├── VisibilityEvaluationHelper.cs
│   ├── SearchIndexBuilder.cs
│   ├── ConditionReferenceHelper.cs
│   ├── EntryFileMapper.cs
│   ├── PrefillProviders.cs
│   ├── DefaultImplementations.cs  # Default IPermissionEvaluator, IConditionEvaluator
│   ├── InMemoryCoreMetadataCache.cs
│   ├── BlazorWebFormsServiceCollectionExtensions.cs # DI registration
│   └── DemoFormFactory.cs         # Test data generation
```

---

## FormsApplicationService

The main application service. Inject this into your Blazor components or MVC controllers to access all form operations.

```csharp
public sealed class FormsApplicationService
```

**Lifetime**: Scoped (create a new instance per HTTP request/Blazor circuit).

### Form Management Methods

#### `GetDashboardAsync()`

```csharp
public async Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default)
```

**Returns**: `DashboardViewModel` containing all forms and recent entries accessible to the current user.

**Uses**: 
- `IFormsRepository.QueryFormsAsync()`
- `IFormsRepository.QueryEntriesAsync()` (recent entries)
- Caches result per user via `ICoreMetadataCache`

**Throws**: `InvalidOperationException` if user context is unavailable.

**Example**:

```csharp
var dashboard = await formsService.GetDashboardAsync();
foreach (var form in dashboard.Forms)
{
	Console.WriteLine($"{form.Name} - {form.Description}");
}
```

---

#### `GetBuilderStateAsync(Guid? formId)`

```csharp
public async Task<BuilderState> GetBuilderStateAsync(
	Guid? formId = null,
	CancellationToken cancellationToken = default)
```

**Parameters**:
- `formId` — Load existing form for editing. `null` starts a new blank form.

**Returns**: `BuilderState` with form metadata and draft definition ready for editing.

**Uses**:
- `IFormsRepository.GetFormAsync(formId)` if loading
- `IPermissionEvaluator.CanManageForm()` — throws if user lacks permission
- Creates a new `FormAggregate` if `formId` is null

**Throws**:
- `InvalidOperationException` if form not found or user lacks `Manage` permission

**Example**:

```csharp
var state = await formsService.GetBuilderStateAsync(Guid.Parse("..."));
var definition = state.Form.DraftDefinition;
Console.WriteLine($"Editing: {definition.Title}");
```

---

#### `SaveDraftAsync(SaveDraftRequest request)`

```csharp
public async Task<FormAggregate> SaveDraftAsync(
	SaveDraftRequest request,
	CancellationToken cancellationToken = default)
```

**Parameters**: `SaveDraftRequest` containing:
- `FormId` — `null` to create new, or existing GUID to update
- `Name`, `Description`, `Slug` — form metadata
- `AccessMode` — `Authenticated`, `Public`, or `InvitationOnly`
- `EditMode` — `DraftOnly` or `OverwriteLatest`
- `Definition` — the form definition model

**Returns**: Updated or created `FormAggregate`.

**Validation**: 
- Default culture must be valid via `CultureInfo.GetCultureInfo()`
- All localized cultures must be valid
- Slug must be unique (within public/authenticated forms)
- Definition passes full schema validation

**Side effects**:
- If `FormId` is null, generates a new GUID and assigns current user as `Owner`
- Updates draft definition without creating a published version
- Clears metadata cache

**Throws**:
- `InvalidOperationException` if culture is invalid, slug conflicts, or validation fails
- `InvalidOperationException` if user lacks `Manage` permission

**Example**:

```csharp
var form = await formsService.SaveDraftAsync(new SaveDraftRequest
{
	FormId = null,  // new form
	Name = "Travel Request",
	Description = "Employee travel requests",
	Slug = "travel-request",
	AccessMode = FormAccessMode.Authenticated,
	Definition = definition
});
Console.WriteLine($"Form created: {form.Id}");
```

---

#### `PublishAsync(Guid formId)`

```csharp
public async Task<FormVersionRecord> PublishAsync(
	Guid formId,
	CancellationToken cancellationToken = default)
```

**Parameters**:
- `formId` — Form to publish

**Returns**: `FormVersionRecord` (immutable snapshot of the definition).

**Validation**:
- Same as `SaveDraftAsync` (definition must pass full validation)
- Draft definition is serialized to JSON and stored

**Side effects**:
- Creates a new `FormVersionRecord` with auto-incremented `VersionNumber`
- All previous versions remain in the repository (historical records)
- Clears metadata cache so next published form view reflects new version

**Throws**:
- `InvalidOperationException` if form not found, validation fails, or user lacks `Manage` permission

**Example**:

```csharp
var version = await formsService.PublishAsync(formId);
Console.WriteLine($"Published version {version.VersionNumber}");
```

---

### Submission Lifecycle Methods

#### `SubmitEntryAsync(Guid formId, SubmitEntryRequest request)`

```csharp
public async Task<EntryRecord> SubmitEntryAsync(
	Guid formId,
	SubmitEntryRequest request,
	CancellationToken cancellationToken = default)
```

**Parameters**: `SubmitEntryRequest` containing:
- `Answers` — `Dictionary<string, string?>` (field id → answer)
- `Approvers` — `List<ApproverInput>` (optional; if provided, entry needs approval)
- `Files` — `List<SubmittedFileInput>` (uploaded files)
- `DraftEntryId` — `Guid?` (draft to finalize, if any)

**Returns**: Created `EntryRecord` with:
- `Status` = `Approved` (if no approvers) or `NeedsApproval` (if approvers provided)
- `Answers`, `Files`, `ApprovalSteps` populated

**Side effects**:
- If `DraftEntryId` is provided, deletes the draft entry
- Creates approval steps in order
- Triggers email notifications to all approvers
- Triggers email to form notification rules
- Increments submission counter for anti-abuse tracking

**Throws**:
- `InvalidOperationException` if form/version not found or user lacks `Submit` permission
- `InvalidOperationException` if validation rules fail (required fields, regex patterns, etc.)

**Example**:

```csharp
var entry = await formsService.SubmitEntryAsync(formId, new SubmitEntryRequest
{
	Answers = new() { ["name"] = "John Doe", ["email"] = "john@example.com" },
	Approvers = new() { new ApproverInput { Name = "Jane Manager", Email = "jane@example.com" } },
	Files = files
});
Console.WriteLine($"Entry submitted: {entry.Id}, status: {entry.Status}");
```

---

#### `SaveDraftSubmissionAsync(Guid formId, SaveDraftSubmissionRequest request)`

```csharp
public async Task<EntryRecord> SaveDraftSubmissionAsync(
	Guid formId,
	SaveDraftSubmissionRequest request,
	CancellationToken cancellationToken = default)
```

**Parameters**: `SaveDraftSubmissionRequest` containing:
- `Answers` — partial or complete answers
- `Files` — uploaded files
- `Approvers` — list of approvers to notify on final submit
- `DraftEntryId` — `Guid?` (update existing draft or create new)

**Returns**: Created or updated draft `EntryRecord` with `Status = Draft`.

**Side effects**:
- Does NOT trigger email notifications
- If `DraftEntryId` is null, creates new entry with `Status = Draft`
- If `DraftEntryId` exists, updates answers and files in place
- Clears cache

**Note**: Draft entries are not visible in `QueryEntriesAsync()` or dashboards by default. They are resumed only via `GetDraftSubmissionAsync()`.

**Example**:

```csharp
var draft = await formsService.SaveDraftSubmissionAsync(formId, new SaveDraftSubmissionRequest
{
	DraftEntryId = null,  // new draft
	Answers = answers,
	Files = files,
	Approvers = approvers
});
```

---

#### `GetDraftSubmissionAsync(Guid formId)`

```csharp
public async Task<EntryRecord?> GetDraftSubmissionAsync(
	Guid formId,
	CancellationToken cancellationToken = default)
```

**Parameters**:
- `formId` — Form to check for draft

**Returns**: Current user's draft entry for this form, or `null` if none exists.

**Scoping**: Returns only the current user's draft (enforced by `GetCurrentUser()`).

**Example**:

```csharp
var draft = await formsService.GetDraftSubmissionAsync(formId);
if (draft != null)
{
	Console.WriteLine($"Draft has {draft.Answers.Count} answers");
}
```

---

#### `GetLatestUserEntryAsync(Guid formId)`

```csharp
public async Task<EntryRecord?> GetLatestUserEntryAsync(
	Guid formId,
	CancellationToken cancellationToken = default)
```

**Returns**: Current user's most recent submitted (non-draft) entry, or `null`.

**Ordering**: By `SubmittedUtc` descending.

---

#### `ReviseEntryAsync(Guid entryId, Dictionary<string, string?> answers)`

```csharp
public async Task<EntryRecord> ReviseEntryAsync(
	Guid entryId,
	Dictionary<string, string?> answers,
	CancellationToken cancellationToken = default)
```

**Precondition**: Entry must have `Status = Approved` and form `EditMode = OverwriteLatest`.

**Returns**: Updated entry.

**Side effects**:
- Appends a `RevisionRecord` with timestamp and actor info
- Overwrites answers in place (does not create new entry)
- Triggers email notifications to form notification rules
- Does not trigger approver re-notification

**Throws**: `InvalidOperationException` if entry not found, status is not `Approved`, or `EditMode` does not allow revisions.

---

#### `ResubmitEntryAsync(Guid entryId, ResubmitEntryRequest request)`

```csharp
public async Task<EntryRecord> ResubmitEntryAsync(
	Guid entryId,
	ResubmitEntryRequest request,
	CancellationToken cancellationToken = default)
```

**Precondition**: Entry must have `Status = Rejected`.

**Parameters**: `ResubmitEntryRequest` containing:
- `Answers` — revised answers
- `Approvers` — list of new approvers

**Returns**: Updated entry with:
- `Status = NeedsApproval`
- Old approval steps cleared
- New approval steps created

**Side effects**:
- Resets approval workflow
- Appends resubmission audit event
- Triggers email notifications to new approvers

---

### Query Methods

#### `SearchEntriesAsync(Guid? formId, string? search)`

```csharp
public async Task<List<EntryRecord>> SearchEntriesAsync(
	Guid? formId = null,
	string? search = null,
	CancellationToken cancellationToken = default)
```

**Parameters**:
- `formId` — Filter to specific form, or `null` for all forms
- `search` — Text to search in submitter name and searchable fields

**Returns**: Up to 1000 matching entries.

**Scoping**: Returns only entries visible to current user.

**Searchability**: Only fields with `Searchable = true` are indexed.

---

#### `QueryEntriesAsync(EntryQueryOptions options)`

```csharp
public async Task<IReadOnlyList<EntryRecord>> QueryEntriesAsync(
	EntryQueryOptions options,
	CancellationToken cancellationToken = default)
```

**Parameters**: `EntryQueryOptions` containing:
- `FormId` — `Guid?` (filter to form)
- `Status` — `EntryStatus?` (filter by status)
- `SubmittedByUserId` — `Guid?` (filter by submitter)
- `Search` — `string?` (search term)
- `Sort` — `EntryQuerySort` (`SubmittedDescending`, `UpdatedDescending`)
- `Offset` — pagination offset
- `Limit` — page size (default 50, max 1000)

**Returns**: Paginated list of entries.

**Scoping**: Returns only entries visible to current user per permission rules.

**Example**:

```csharp
var options = new EntryQueryOptions
{
	FormId = formId,
	Status = EntryStatus.NeedsApproval,
	Offset = 0,
	Limit = 50
};
var entries = await formsService.QueryEntriesAsync(options);
```

---

#### `GetEntryDetailAsync(Guid entryId)`

```csharp
public async Task<EntryDetailViewModel?> GetEntryDetailAsync(
	Guid entryId,
	CancellationToken cancellationToken = default)
```

**Returns**: `EntryDetailViewModel` containing:
- `Form` — parent `FormAggregate`
- `Definition` — published form definition used for rendering
- `Entry` — full `EntryRecord` with answers, files, approval steps, audit trail
- `CanView` — whether current user can view this entry
- `HistoricalRenderWarning` — if the published version was deleted, a fallback warning

**Returns `null`** if entry not found.

**Scoping**: Checks `CanView` permission; if false, returns ViewModel with `CanView=false`.

**Example**:

```csharp
var vm = await formsService.GetEntryDetailAsync(entryId);
if (vm?.CanView == true)
{
	Console.WriteLine($"Answers: {vm.Entry.Answers.Count}");
}
```

---

### Approval Workflow Methods

#### `ApproveStepAsync(Guid entryId, Guid stepId, string signature)`

```csharp
public async Task<EntryRecord> ApproveStepAsync(
	Guid entryId,
	Guid stepId,
	string signature,
	CancellationToken cancellationToken = default)
```

**Parameters**:
- `entryId` — Entry to approve
- `stepId` — Specific approval step to mark approved
- `signature` — Text signature captured from approver

**Returns**: Updated entry.

**Side effects**:
- Marks step `Status = Approved`
- If all steps are now approved, sets entry `Status = Approved`
- Appends `ApprovalAuditEvent` to entry
- If entry now fully approved, triggers email to submitter

**Throws**: `InvalidOperationException` if step not found, already completed, or current user is not the assigned approver.

---

#### `RejectStepAsync(Guid entryId, Guid stepId, string reason)`

```csharp
public async Task<EntryRecord> RejectStepAsync(
	Guid entryId,
	Guid stepId,
	string reason,
	CancellationToken cancellationToken = default)
```

**Parameters**:
- `entryId` — Entry to reject
- `stepId` — Step to mark rejected
- `reason` — Rejection reason for submitter

**Returns**: Updated entry with `Status = Rejected`.

**Side effects**:
- Marks step `Status = Rejected`
- Sets all other approval steps to `Pending` (workflow resets)
- Sets entry `Status = Rejected`
- Appends `ApprovalAuditEvent`
- Triggers email to submitter with reason

**Note**: Submitter must call `ResubmitEntryAsync()` to restart workflow.

---

#### `SendApprovalRemindersAsync(Guid formId)`

```csharp
public async Task<ApprovalReminderResult> SendApprovalRemindersAsync(
	Guid formId,
	CancellationToken cancellationToken = default)
```

**Returns**: `ApprovalReminderResult` with counts of reminders sent and failures.

**Scope**: Sends reminders for all entries with `Status = NeedsApproval` on this form.

**Behavior**:
- For each pending approval step, sends a reminder email to the assigned approver
- Tracks idempotency to prevent duplicate reminders on retry
- Respects `IAntiAbuseGuard.CheckOutboundNotificationAllowedAsync()`

---

### File Operations Methods

#### `StoreFileAsync(FileUploadRequest request)`

```csharp
public async Task<StoredFile> StoreFileAsync(
	FileUploadRequest request,
	CancellationToken cancellationToken = default)
```

**Parameters**: `FileUploadRequest` containing:
- `FileName` — original filename
- `ContentType` — MIME type
- `Content` — byte array
- `MaxAllowedBytes` — max size enforcement
- `AllowedMimeTypes` — list of allowed MIME types
- `AllowedExtensions` — list of allowed file extensions

**Returns**: `StoredFile` with:
- `RelativePath` — path where file was saved
- `Sha256` — content hash for deduplication
- `FileName`, `ContentType`, `Length`

**Side effects**:
- Validates file constraints (size, type, extension)
- Calls `IFileStorage.SaveAsync()` to persist
- Tracks upload count for `IAntiAbuseGuard`
- Records telemetry via `IOperationalTelemetry`

**Throws**: `InvalidOperationException` if constraints violated or upload blocked by anti-abuse guard.

---

#### `OpenEntryFileAsync(Guid entryId, Guid fileId)`

```csharp
public async Task<EntryFileStream> OpenEntryFileAsync(
	Guid entryId,
	Guid fileId,
	CancellationToken cancellationToken = default)
```

**Returns**: `EntryFileStream` with:
- `Content` — `byte[]` of file content
- `ContentType` — MIME type
- `FileName` — original filename

**Security**: Enforces that current user can view the entry before opening the file.

**Throws**: `InvalidOperationException` if entry not found or user lacks permission.

---

#### `CleanupStaleDraftFilesAsync(TimeSpan draftAgeThreshold)`

```csharp
public async Task<DraftCleanupResult> CleanupStaleDraftFilesAsync(
	TimeSpan draftAgeThreshold,
	CancellationToken cancellationToken = default)
```

**Parameters**:
- `draftAgeThreshold` — Delete drafts older than this duration

**Returns**: `DraftCleanupResult` with counts of deleted entries and files.

**Side effects**:
- Finds all draft entries (status `Draft`) older than the threshold
- Deletes each draft entry
- Deletes orphaned files via `IFileStorage.DeleteAsync()`
- Records telemetry

**Recommended frequency**: Daily or weekly via scheduled job. See `DraftCleanupService` in `Infrastructure.SqlServer` for background service implementation.

---

### PDF Export Methods

#### `ExportEntryPdfAsync(Guid entryId)`

```csharp
public async Task<EntryPdfExport> ExportEntryPdfAsync(
	Guid entryId,
	CancellationToken cancellationToken = default)
```

**Returns**: `EntryPdfExport` containing:
- `Content` — `byte[]` of PDF (or text, depending on `IPdfExporter` implementation)
- `ContentType` — `"text/plain"` (built-in) or `"application/pdf"` (custom)
- `FileName` — suggested filename

**Security**: Enforces that current user can view the entry.

**Options**: Controlled by `BlazorWebFormsSqlServerOptions`:
- `PdfMaxAnswerRows` — max answer rows included (default 500)
- `PdfMaxAuditRows` — max audit events included (default 250)
- `PdfMaxBytes` — byte size guard (default 1 MB)

**Throws**: `InvalidOperationException` if entry not found, user lacks permission, or export exceeds size limits.

---

### Invitation Methods

#### `CreateInvitationAsync(CreateInvitationRequest request)`

```csharp
public async Task<FormInvitation> CreateInvitationAsync(
	CreateInvitationRequest request,
	CancellationToken cancellationToken = default)
```

**Parameters**: `CreateInvitationRequest` containing:
- `FormId` — Form to grant access to
- `Email` — Recipient email
- `Role` — `FormPermissionRole` to grant
- `ValidFor` — Duration the invitation is valid

**Returns**: `FormInvitation` with:
- `Token` — Shareable token for recipient
- `ExpiresUtc` — When the token expires
- `Status = Pending`

**Side effects**:
- Creates `InvitationRecord` in repository
- Sends email notification to recipient with acceptance link
- Respects `IAntiAbuseGuard.CheckOutboundNotificationAllowedAsync()`

**Throws**: `InvalidOperationException` if form not found, user lacks `Manage` permission, or email notification fails.

---

#### `AcceptInvitationAsync(string token)`

```csharp
public async Task<FormInvitation> AcceptInvitationAsync(
	string token,
	CancellationToken cancellationToken = default)
```

**Parameters**:
- `token` — Invitation token from email

**Returns**: Updated `FormInvitation` with `Status = Accepted`.

**Side effects**:
- Validates token signature and expiry
- Grants the specified role to current user on the form via `FormPermissionGrant`
- Marks invitation `Status = Accepted`, `AcceptedUtc = now`, `AcceptedByUserId = currentUser`
- Sends email notification to form owner
- Clears cache

**Throws**: `InvalidOperationException` if token invalid, expired, or already accepted.

---

#### `RevokeInvitationAsync(Guid invitationId)`

```csharp
public async Task<FormInvitation> RevokeInvitationAsync(
	Guid invitationId,
	CancellationToken cancellationToken = default)
```

**Returns**: Updated invitation with `Status = Revoked`.

**Side effects**:
- Marks invitation revoked
- If already accepted, does not revoke the granted permission
- Sends email to invitee
- Clears cache

**Throws**: `InvalidOperationException` if user lacks `Manage` permission.

---

#### `GetInvitationsAsync(Guid formId)`

```csharp
public async Task<List<FormInvitation>> GetInvitationsAsync(
	Guid formId,
	CancellationToken cancellationToken = default)
```

**Returns**: All invitations (pending, accepted, expired, revoked) for the form.

**Scoping**: Checks user has `Manage` permission.

---

### Initialization Method

#### `SeedAsync()`

```csharp
public async Task SeedAsync(CancellationToken cancellationToken = default)
```

**Purpose**: Ensures demo data exists. Call once at application startup.

**Behavior**:
- Checks if repository is empty
- If empty, creates a demo form with sample data
- No-op if demo form already exists

**Example** (in `Program.cs`):

```csharp
using (var scope = app.Services.CreateScope())
{
	var formsService = scope.ServiceProvider.GetRequiredService<FormsApplicationService>();
	await formsService.SeedAsync();
}
```

---

## Domain Models

All models are serializable/deserializable to JSON.

### FormAggregate

Root aggregate containing form metadata, draft definition, published versions, and permission grants.

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

| Property | Description |
|---|---|
| `Id` | Form GUID |
| `Name` | Display name of form |
| `Description` | Free-form description |
| `Publication` | Slug, access mode, edit mode, published state |
| `DraftDefinition` | Current unpublished definition |
| `Versions` | Immutable version snapshots (ordered by version number) |
| `Permissions` | Access grants per user/role |
| `CreatedUtc`, `CreatedByUserId` | Initial creator |
| `UpdatedUtc`, `UpdatedByUserId` | Last updater |

---

### FormPublication

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
| `Slug` | URL slug for published form (e.g. `"travel-request"`) — must be unique within access mode |
| `AccessMode` | `Authenticated` (signed-in users), `Public` (anyone), or `InvitationOnly` |
| `EditMode` | `DraftOnly` (no revisions after approve) or `OverwriteLatest` (allow revisions) |
| `IsPublished` | `true` if at least one version has been published |

---

### FormAccessMode

```csharp
public enum FormAccessMode
{
	Authenticated = 0,   // Only authenticated users can submit
	Public = 1,          // Anyone can submit (no auth required)
	InvitationOnly = 2   // Only invited users can submit
}
```

---

### FormEditMode

```csharp
public enum FormEditMode
{
	DraftOnly = 0,
	OverwriteLatest = 1   // Allows revisions after approval
}
```

---

### EntryRecord

Submitted or draft entry with answers, files, and approval workflow state.

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

---

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

---

### FormDefinition

The serializable form schema. Every version snapshot stores a complete `FormDefinition`.

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

---

### VisibilityConditionDefinition

Controls when sections and fields are visible.

```csharp
public sealed class VisibilityConditionDefinition
{
	public VisibilityJoinOperator Join { get; set; }  // And or Or
	public List<VisibilityRuleDefinition> Rules { get; set; }
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

| Operator | Condition |
|---|---|
| `Equals` | Field answer equals `Value` exactly |
| `NotEquals` | Field answer does not equal `Value` |
| `Contains` | Field answer contains `Value` (substring, case-insensitive) |
| `Empty` | Field answer is null or empty string |

---

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

---

## Service Interfaces

All interfaces are defined in `Abstractions/Contracts.cs` and are intended for extension.

### IFormsRepository

```csharp
public interface IFormsRepository
{
	Task<FormAggregate?> GetFormAsync(Guid formId, CancellationToken ct = default);
	Task<FormAggregate> CreateFormAsync(FormAggregate form, CancellationToken ct = default);
	Task<FormAggregate> UpdateFormAsync(FormAggregate form, CancellationToken ct = default);
	Task<FormVersionRecord> PublishVersionAsync(Guid formId, FormVersionRecord version, CancellationToken ct = default);
	Task<FormAggregate?> GetPublishedFormBySlugAsync(string slug, CancellationToken ct = default);
	Task<List<FormAggregate>> QueryFormsAsync(FormQueryOptions options, CancellationToken ct = default);
	Task<List<EntryRecord>> QueryEntriesAsync(EntryQueryOptions options, CancellationToken ct = default);
	Task<EntryRecord?> GetEntryAsync(Guid entryId, CancellationToken ct = default);
	Task<EntryRecord> CreateEntryAsync(EntryRecord entry, CancellationToken ct = default);
	Task<EntryRecord> UpdateEntryAsync(EntryRecord entry, CancellationToken ct = default);
	Task DeleteEntryAsync(Guid entryId, CancellationToken ct = default);
	// ... more methods for invitations, etc.
}
```

---

### IFileStorage

```csharp
public interface IFileStorage
{
	Task<StoredFile> SaveAsync(FileUploadRequest request, CancellationToken ct = default);
	Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default);
	Task DeleteAsync(string relativePath, CancellationToken ct = default);
}
```

---

### IEmailNotifier

```csharp
public interface IEmailNotifier
{
	Task NotifyFormSubmittedAsync(FormAggregate form, EntryRecord entry, CancellationToken ct = default);
	Task NotifyApproverAssignedAsync(FormAggregate form, EntryRecord entry, ApprovalStepRecord step, CancellationToken ct = default);
	Task NotifyEntryApprovedAsync(FormAggregate form, EntryRecord entry, CancellationToken ct = default);
	Task NotifyEntryRejectedAsync(FormAggregate form, EntryRecord entry, ApprovalStepRecord step, CancellationToken ct = default);
	// ... more methods
}
```

---

### IPermissionEvaluator

```csharp
public interface IPermissionEvaluator
{
	bool CanManageForm(FormAggregate form, UserProfile user);
	bool CanSubmitForm(FormAggregate form, UserProfile user);
	bool CanViewEntry(EntryRecord entry, FormAggregate form, UserProfile user);
	bool CanApproveEntry(EntryRecord entry, ApprovalStepRecord step, UserProfile user);
}
```

---

### IConditionEvaluator

```csharp
public interface IConditionEvaluator
{
	bool IsVisible(
		VisibilityConditionDefinition? condition,
		IReadOnlyDictionary<string, string?> answers);
}
```

---

### ICurrentUserContext

```csharp
public interface ICurrentUserContext
{
	UserProfile GetCurrentUser();
}
```

---

## Validation

All validation occurs during `SaveDraftAsync` and `PublishAsync`. Invalid inputs throw `InvalidOperationException`.

### Culture Validation

- **Default culture** must be a valid `CultureInfo` (e.g. `"en-US"`, `"fr-FR"`)
- **All localized cultures** must be valid
- Empty culture keys are rejected

**Example error**:

```
"Default culture is not a valid culture."
```

---

### Form Definition Schema Validation

- `Title` must not be empty
- `Description` can be empty but must not be null
- At least one section required
- Each section must have at least one field
- Field ids within a section must be unique
- Visibility conditions must reference fields that exist in the form

---

### Slug Validation

- Slug must be unique among published forms with the same access mode
- Slug must be URL-safe (alphanumeric, hyphens, underscores)

---

### Regex Pattern Validation

- If a field has a `RegexPattern`, it is validated when the entry is submitted
- Invalid regex patterns are caught at form save time

---

## Condition Evaluation

The `IConditionEvaluator` interface evaluates visibility conditions at render time.

```csharp
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
		}
	}
};

var answers = new Dictionary<string, string?>
{
	["employment-status"] = "Employed"
};

var visible = conditionEvaluator.IsVisible(condition, answers);  // true
```

---

## Error Handling

All business logic errors throw `InvalidOperationException` with descriptive messages. These are intended to be caught and logged by the caller.

**Common error messages**:

- `"Default culture is required."`
- `"Default culture is not a valid culture."`
- `"Form localized titles culture 'xyz' is not a valid culture."`
- `"User does not have permission to manage forms."`
- `"Entry not found."`
- `"Approval step already completed."`
- `"Current user is not assigned to this approval step."`
- `"File size exceeds allowed maximum."`
- `"File extension not allowed."`

---

## Next Steps

- See [CORE_MODELS.md](./CORE_MODELS.md) for detailed domain model reference
- See [CORE_LOCALIZATION.md](./CORE_LOCALIZATION.md) for localization and condition evaluation
- See [CORE_EXTENDING.md](./CORE_EXTENDING.md) for how to implement optional interfaces
