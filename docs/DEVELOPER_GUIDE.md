# BlazorWebForms — Developer Guide

Complete reference for integrating, configuring, and extending BlazorWebForms in your own application.

---

## Table of contents

1. [Architecture overview](#1-architecture-overview)
2. [Prerequisites](#2-prerequisites)
3. [Package installation](#3-package-installation)
4. [Service registration](#4-service-registration)
5. [Implementing ICurrentUserContext](#5-implementing-icurrentusercontext)
6. [Authentication and authorization](#6-authentication-and-authorization)
7. [Blazor components](#7-blazor-components)
8. [MVC controllers](#8-mvc-controllers)
9. [Form definition model](#9-form-definition-model)
10. [FormsApplicationService — full API reference](#10-formsapplicationservice--full-api-reference)
11. [Approval workflow](#11-approval-workflow)
12. [File uploads and downloads](#12-file-uploads-and-downloads)
13. [PDF export](#13-pdf-export)
14. [Invitations](#14-invitations)
15. [Email notifications](#15-email-notifications)
16. [Implementing optional interfaces](#16-implementing-optional-interfaces)
17. [SQL Server options reference](#17-sql-server-options-reference)
18. [Configuration file reference](#18-configuration-file-reference)
19. [Database migrations](#19-database-migrations)
20. [CSS and styling](#20-css-and-styling)
21. [Permissions model](#21-permissions-model)
22. [Localization](#22-localization)
23. [Security notes](#23-security-notes)
24. [Building a custom UI (e.g. MudBlazor)](#24-building-a-custom-ui-eg-mudblazor)
25. [/entries route (Entry detail)](#25-entries-route-entry-detail)

---

## 1. Architecture overview

BlazorWebForms is split into three packages with a strict dependency direction:

```
BlazorWebForms.Core
  └─ BlazorWebForms.Blazor            (UI components, depends on Core)
  └─ BlazorWebForms.Infrastructure.SqlServer  (EF Core persistence, depends on Core)
```

| Package | Responsibility |
|---|---|
| `BlazorWebForms.Core` | Domain models, interfaces (contracts), `FormsApplicationService`, form definition serialization, condition evaluation, permission evaluation, in-memory metadata cache |
| `BlazorWebForms.Blazor` | Razor components: `FormBuilderWorkspace`, `PublishedFormView`, `FormsAdminDashboard`, `DynamicFormRenderer` |
| `BlazorWebForms.Infrastructure.SqlServer` | EF Core `DbContext`, `EfFormsRepository`, `LocalFileStorage`, email integration routing, PDF export, anti-abuse guard, draft cleanup, telemetry |

Your application hosts all three and supplies one required implementation: **`ICurrentUserContext`**.

---

## 2. Prerequisites

- .NET 10 SDK
- SQL Server or LocalDB (for `Infrastructure.SqlServer`)
- EF Core CLI (for migrations)

```powershell
dotnet tool install --global dotnet-ef
```

---

## 3. Package installation

```powershell
dotnet add package BlazorWebForms.Core --version 1.0.0
dotnet add package BlazorWebForms.Blazor --version 1.0.0
dotnet add package BlazorWebForms.Infrastructure.SqlServer --version 1.0.0
```

Only add `BlazorWebForms.Blazor` to projects that host Blazor components. Only add `BlazorWebForms.Infrastructure.SqlServer` to projects that own the database connection.

---

## 4. Service registration

Call both extension methods in `Program.cs`. The SQL Server registration wraps the Core registration's optional services with production-grade implementations.

```csharp
using BlazorWebForms.Core.Services;
using BlazorWebForms.Infrastructure.SqlServer;

var builder = WebApplication.CreateBuilder(args);

// Required for Blazor Server
builder.Services.AddRazorComponents()
	.AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddServerSideBlazor();
builder.Services.AddControllersWithViews(); // required for file/PDF/invitation controllers

// Register BlazorWebForms core services (serialization, condition evaluator,
// permission evaluator, in-memory cache, FormsApplicationService)
builder.Services.AddBlazorWebFormsCore();

// Register SQL Server infrastructure
// (DbContext, repository, file storage, email, PDF, anti-abuse, telemetry)
builder.Services.AddBlazorWebFormsSqlServer(options =>
{
	// Connection string (overridden below from configuration)
	options.ConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=MyApp;";

	// File storage root on disk
	options.StorageRoot = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "uploads");

	// Only send real email in production
	options.EnableOutboundEmail = builder.Environment.IsProduction();
	options.EmailProviderStrategy = builder.Environment.IsProduction() ? "Smtp" : "DryRun";

	// Pull remaining settings from appsettings.json (see section 18)
	var conn = builder.Configuration.GetConnectionString("BlazorWebForms");
	if (!string.IsNullOrWhiteSpace(conn))
		options.ConnectionString = conn;

	var schema = builder.Configuration["BlazorWebFormsSqlServer:SchemaName"];
	if (!string.IsNullOrWhiteSpace(schema))
		options.SchemaName = schema;
});

// Your ICurrentUserContext implementation (see section 5)
builder.Services.AddScoped<ICurrentUserContext, ClaimsCurrentUserContext>();

var app = builder.Build();

// Seed the database (creates the demo form if the repository is empty)
using (var scope = app.Services.CreateScope())
{
	var formsService = scope.ServiceProvider.GetRequiredService<FormsApplicationService>();
	await formsService.SeedAsync();
}

app.Run();
```

### What each registration provides

`AddBlazorWebFormsCore()` registers:
- `IFormDefinitionSerializer` — JSON serialization of `FormDefinition`
- `IConditionEvaluator` — evaluates visibility conditions at render time
- `IPermissionEvaluator` — checks `CanManageForm`, `CanSubmitForm`, `CanViewEntry`
- `ICoreMetadataCache` — short-lived in-memory cache for dashboard and published form lookups
- `IAntiAbuseGuard` (no-op, replaced by SQL Server registration)
- `IOperationalTelemetry` (no-op, replaced by SQL Server registration)
- `FormsApplicationService` (scoped)

`AddBlazorWebFormsSqlServer()` additionally registers:
- `BlazorWebFormsDbContext` (EF Core, scoped)
- `IFormsRepository` → `EfFormsRepository` (scoped)
- `IFileStorage` → `LocalFileStorage`
- `IEmailIntegration` → `EmailIntegrationRouter` (routes to DryRun/SMTP/SendGrid/Graph)
- `IGraphIntegration` → `GraphIntegration`
- `IPdfIntegration` → `PdfIntegration`
- `IPdfExporter` → `TextPdfExporter`
- `IEmailNotifier` → `TemplateEmailNotifier`
- `IAntiAbuseGuard` → `DefaultAntiAbuseGuard`
- `IOperationalTelemetry` → `InMemoryOperationalTelemetry`
- `DraftCleanupService` (scoped)
- `ICurrentUserContext` → `DemoCurrentUserContext` (replace with your own, see section 5)

---

## 5. Implementing ICurrentUserContext

This is the **one required implementation** your application must provide. It tells the service layer who the currently authenticated user is.

```csharp
using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

public sealed class ClaimsCurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
	public UserProfile GetCurrentUser()
	{
		var principal = httpContextAccessor.HttpContext?.User;
		if (principal?.Identity is null || !principal.Identity.IsAuthenticated)
		{
			return new UserProfile { IsAuthenticated = false, DisplayName = "Anonymous" };
		}

		// Resolve a stable GUID user id from the identity token
		var rawId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
				 ?? principal.FindFirst("sub")?.Value
				 ?? principal.FindFirst("oid")?.Value;

		var userId = Guid.TryParse(rawId, out var parsed)
			? parsed
			: DeterministicGuid(rawId ?? principal.Identity.Name ?? "anonymous");

		return new UserProfile
		{
			UserId       = userId,
			IsAuthenticated = true,
			DisplayName  = principal.FindFirst(ClaimTypes.Name)?.Value
						?? principal.Identity.Name
						?? "Authenticated User",
			Email        = principal.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
			Roles        = ResolveRoles(principal)
		};
	}

	// Map string roles from claims to the FormPermissionRole enum
	private static List<FormPermissionRole> ResolveRoles(ClaimsPrincipal principal)
	{
		var claims = principal.FindAll(ClaimTypes.Role)
			.Concat(principal.FindAll("role"))
			.Select(c => c.Value)
			.Distinct(StringComparer.OrdinalIgnoreCase);

		var mapped = new List<FormPermissionRole>();
		foreach (var role in claims)
		{
			if (Enum.TryParse<FormPermissionRole>(role, ignoreCase: true, out var r))
				mapped.Add(r);
		}
		return mapped;
	}

	// Deterministic fallback when the id claim is not a GUID
	private static Guid DeterministicGuid(string value)
	{
		var bytes = System.Security.Cryptography.MD5.HashData(
			System.Text.Encoding.UTF8.GetBytes(value));
		return new Guid(bytes);
	}
}
```

Register it **after** `AddBlazorWebFormsSqlServer` so it overrides the built-in `DemoCurrentUserContext`:

```csharp
builder.Services.AddScoped<ICurrentUserContext, ClaimsCurrentUserContext>();
```

### UserProfile properties

| Property | Description |
|---|---|
| `UserId` | Stable `Guid` identifier. Used for ownership and permission checks. |
| `IsAuthenticated` | Drives access-mode enforcement. |
| `DisplayName` | Shown in UI and stored on entries. |
| `Email` | Stored on entries; used for email notifications. |
| `Roles` | List of `FormPermissionRole` values for the current request. |

---

## 6. Authentication and authorization

BlazorWebForms does not own your authentication stack. Use any ASP.NET Core authentication scheme. The sample app uses cookie auth; production apps typically use OIDC or Microsoft Entra.

### Recommended authorization policies

```csharp
builder.Services.AddAuthorization(options =>
{
	// Any signed-in user
	options.AddPolicy("Authenticated", p => p.RequireAuthenticatedUser());

	// Admin dashboard and builder — restrict to elevated roles
	options.AddPolicy("AdminConsole",    p => p.RequireRole("Admin", "Owner", "Manager"));
	options.AddPolicy("BuilderAccess",   p => p.RequireRole("Admin", "Owner", "Manager"));
	options.AddPolicy("PublishAccess",   p => p.RequireRole("Admin", "Owner", "Manager"));

	// Approval actions — approvers and admins
	options.AddPolicy("ApproverAction",  p => p.RequireRole("Admin", "Owner", "Manager", "Approver"));

	// Entry viewing — the submitter themselves or a manager
	// Implement with a custom IAuthorizationHandler if needed (see SelfOrManagerRequirement in sample)
	options.AddPolicy("SelfViewAccess",  p => p.RequireAuthenticatedUser());

	// Invitation management
	options.AddPolicy("InvitationManage", p => p.RequireRole("Admin", "Owner", "Manager"));
});
```

### Role mapping

The `FormPermissionRole` enum maps directly to role strings:

| Enum value | Role string | Typical capabilities |
|---|---|---|
| `Admin` | `"Admin"` | Everything |
| `Owner` | `"Owner"` | Manage, publish, approve |
| `Manager` | `"Manager"` | Manage, publish, approve |
| `Approver` | `"Approver"` | Approve and reject entries |
| `Submitter` | `"Submitter"` | Submit forms only |
| `Viewer` | `"Viewer"` | Read-only across all entries |
| `SelfViewer` | `"SelfViewer"` | Read-only on own entries |

---

## 7. Blazor components

Reference the component namespace in your `_Imports.razor`:

```razor
@using BlazorWebForms.Blazor.Components
```

Include the package stylesheet in your Blazor host page:

```html
<link rel="stylesheet" href="_content/BlazorWebForms.Blazor/blazorwebforms.css" />
```

### FormBuilderWorkspace

A full form-builder UI. Lets managers create and edit form definitions, configure sections, fields, branding, localization, and publish.

```razor
@page "/builder"
@page "/builder/{FormId:guid}"
@attribute [Authorize(Policy = "BuilderAccess")]

<FormBuilderWorkspace FormId="FormId" />

@code {
	[Parameter] public Guid? FormId { get; set; }
}
```

| Parameter | Type | Required | Description |
|---|---|---|---|
| `FormId` | `Guid?` | No | If omitted, the builder starts a new empty form. If provided, loads the existing form for editing. |

The component internally calls:
- `FormsApplicationService.GetBuilderStateAsync` on load
- `FormsApplicationService.SaveDraftAsync` on save
- `FormsApplicationService.PublishAsync` on publish

### PublishedFormView

Renders a published form to an end-user and handles draft resume, submission, and approval setup.

```razor
@page "/forms/{Slug}"
@attribute [Authorize(Policy = "SelfViewAccess")]

<PublishedFormView Slug="Slug" />

@code {
	[Parameter, EditorRequired] public required string Slug { get; set; }
}
```

| Parameter | Type | Required | Description |
|---|---|---|---|
| `Slug` | `string` | Yes | The URL slug configured in the form's publication settings. |

The component internally calls:
- `FormsApplicationService.GetPublishedFormAsync` to resolve the form
- `FormsApplicationService.GetDraftSubmissionAsync` to resume any saved draft
- `FormsApplicationService.StoreFileAsync` for file uploads
- `FormsApplicationService.SaveDraftSubmissionAsync` when the user saves a draft
- `FormsApplicationService.SubmitEntryAsync` on final submit

### FormsAdminDashboard

Admin overview showing all forms and a paginated, filterable entry table.

```razor
@page "/admin"
@attribute [Authorize(Policy = "AdminConsole")]

<FormsAdminDashboard />
```

No parameters. The component internally calls:
- `FormsApplicationService.GetDashboardAsync` for forms and recent entries
- `FormsApplicationService.QueryEntriesAsync` for the paginated entry table

### DynamicFormRenderer

Low-level renderer. Used inside `PublishedFormView` but can be embedded directly for custom rendering scenarios.

```razor
<DynamicFormRenderer
	Definition="myFormDefinition"
	Answers="answers"
	AnswersChanged="OnAnswersChanged"
	FilesChanged="OnFilesChanged" />
```

| Parameter | Type | Required | Description |
|---|---|---|---|
| `Definition` | `FormDefinition` | Yes | The deserialized form definition. |
| `Answers` | `Dictionary<string, string?>` | Yes | Two-way bound answers keyed by field id. |
| `AnswersChanged` | `EventCallback<Dictionary<string, string?>>` | No | Fires when any answer changes. |
| `FilesChanged` | `EventCallback<List<FileUploadInput>>` | No | Fires when file selections change. |

---

## 8. MVC controllers

These must be added to your application. They are not shipped in the packages because they require your authorization policies. Add `AddControllersWithViews()` and `MapControllers()` to your pipeline.

### File download controller

```csharp
using BlazorWebForms.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("entry-files")]
public sealed class EntryFilesController(
	FormsApplicationService formsService,
	EntryFileDownloadTokenService tokenService) : ControllerBase
{
	[Authorize(Policy = "SelfViewAccess")]
	[HttpGet("{entryId:guid}/{fileId:guid}")]
	public async Task<IActionResult> Download(
		Guid entryId, Guid fileId,
		[FromQuery] string? token,
		CancellationToken cancellationToken)
	{
		if (!tokenService.TryValidate(token ?? string.Empty, entryId, fileId))
			return Forbid();

		try
		{
			var file = await formsService.OpenEntryFileAsync(entryId, fileId, cancellationToken);
			return File(file.Content, file.ContentType, file.FileName);
		}
		catch (InvalidOperationException) { return Forbid(); }
		catch (FileNotFoundException) { return NotFound(); }
	}
}
```

Register the token service:

```csharp
builder.Services.AddSingleton<EntryFileDownloadTokenService>();
```

The token service reads `BlazorWebForms:FileDownloadTokenSecret` from configuration. Override in production (see section 23).

### PDF export controller

```csharp
[ApiController]
[Route("entry-pdf")]
public sealed class EntryPdfController(FormsApplicationService formsService) : ControllerBase
{
	[Authorize(Policy = "SelfViewAccess")]
	[HttpGet("{entryId:guid}")]
	public async Task<IActionResult> Download(Guid entryId, CancellationToken cancellationToken)
	{
		try
		{
			var exported = await formsService.ExportEntryPdfAsync(entryId, cancellationToken);
			return File(exported.Content, exported.ContentType, exported.FileName);
		}
		catch (InvalidOperationException) { return Forbid(); }
	}
}
```

### Invitation controller

```csharp
using BlazorWebForms.Core.Models;
using BlazorWebForms.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("invitations")]
public sealed class InvitationController(FormsApplicationService formsService) : ControllerBase
{
	[Authorize(Policy = "InvitationManage")]
	[HttpPost("create")]
	public async Task<IActionResult> Create(
		[FromBody] CreateInvitationRequest request,
		CancellationToken cancellationToken)
	{
		var invitation = await formsService.CreateInvitationAsync(request, cancellationToken);
		return Ok(invitation);
	}

	[Authorize(Policy = "Authenticated")]
	[HttpPost("accept")]
	public async Task<IActionResult> Accept(
		[FromForm] string token,
		CancellationToken cancellationToken)
	{
		var invitation = await formsService.AcceptInvitationAsync(token, cancellationToken);
		return Ok(invitation);
	}

	[Authorize(Policy = "InvitationManage")]
	[HttpPost("revoke/{invitationId:guid}")]
	public async Task<IActionResult> Revoke(
		[FromRoute] Guid invitationId,
		CancellationToken cancellationToken)
	{
		var invitation = await formsService.RevokeInvitationAsync(invitationId, cancellationToken);
		return Ok(invitation);
	}
}
```

---

## 9. Form definition model

The `FormDefinition` class is the schema for a form. It is serialized to JSON and stored in the database as a version snapshot.

### FormDefinition

```csharp
public sealed class FormDefinition
{
	public int SchemaVersion { get; set; }        // always = FormDefinition.CurrentSchemaVersion (2)
	public string Title { get; set; }
	public string Description { get; set; }
	public string DefaultCulture { get; set; }    // e.g. "en-US"
	public BrandingDefinition Branding { get; set; }
	public List<FormSectionDefinition> Sections { get; set; }
	public Dictionary<string, string> LocalizedTitles { get; set; }       // culture -> text
	public Dictionary<string, string> LocalizedDescriptions { get; set; } // culture -> text
}
```

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
	public bool Searchable { get; set; }           // indexes this field's value for admin search
	public string? RegexPattern { get; set; }
	public string? DefaultValue { get; set; }
	public string? ValidationHint { get; set; }
	public VisibilityConditionDefinition? VisibilityRules { get; set; }
	public FormFieldLayoutDefinition? Layout { get; set; }
	public Dictionary<string, string> LocalizedLabels { get; set; }
	// ... LocalizedPlaceholders, LocalizedHelpTexts, LocalizedValidationHints

	// File-field constraints (only used when Kind == File)
	public long? MaxFileSizeBytes { get; set; }
	public int MaxFileCount { get; set; }          // default 1; > 1 enables multi-select
	public List<string> AllowedMimeTypes { get; set; }
	public List<string> AllowedExtensions { get; set; }

	// Select/Radio options
	public List<FormFieldOption> Options { get; set; }
}
```

### FormFieldKind values

| Value | Rendered as |
|---|---|
| `Text` | Single-line text input |
| `TextArea` | Multi-line textarea |
| `Number` | Numeric input |
| `Select` | Dropdown (`<select>`) |
| `Checkbox` | Boolean checkbox |
| `Radio` | Radio button group |
| `Date` | Date picker |
| `File` | File upload (single or multiple) |
| `RichText` | Rich text editor |
| `Signature` | Signature capture |

### Visibility conditions

Sections and fields support structured visibility rules. All rules in a group are combined with either AND or OR.

```csharp
var condition = new VisibilityConditionDefinition
{
	Join = VisibilityJoinOperator.And,
	Rules =
	[
		new VisibilityRuleDefinition
		{
			FieldId  = "field-employment-status",
			Operator = VisibilityRuleOperator.Equals,
			Value    = "Employed"
		}
	]
};
```

| Operator | Description |
|---|---|
| `Equals` | Field answer exactly equals `Value` |
| `NotEquals` | Field answer does not equal `Value` |
| `Contains` | Field answer contains `Value` (case-insensitive) |
| `Empty` | Field answer is null or empty string |

### Branding

```csharp
var branding = new BrandingDefinition
{
	LogoUrl      = "https://example.com/logo.png",  // or use LogoFileRef for stored files
	HeroImageUrl = "https://example.com/hero.jpg",
	AccentColor  = "#0f766e",
	SurfaceColor = "#ffffff",
	TextColor    = "#124040",
	ButtonRadius = "999px",
	HeroText     = "Submit your request below."
};
```

---

## 10. FormsApplicationService — full API reference

`FormsApplicationService` is the single entry point for all business logic. It is registered as **scoped**. Inject it into your Blazor components and controllers.

### Form management

| Method | Description |
|---|---|
| `SeedAsync()` | Ensures a demo form exists. Call once at startup. |
| `GetDashboardAsync()` | Returns `DashboardViewModel` with all forms and recent entries. Cached per user. |
| `GetBuilderStateAsync(Guid? formId)` | Returns `BuilderState`. Throws if user lacks manage permission. |
| `SaveDraftAsync(SaveDraftRequest)` | Saves form metadata and draft definition. Creates a new form if `FormId` is null. |
| `PublishAsync(Guid formId)` | Validates definition and creates a new `FormVersionRecord`. |
| `GetPublishedFormAsync(string slug)` | Returns `PublishedFormViewModel?` by slug. Cached. |

### Submission lifecycle

| Method | Description |
|---|---|
| `SubmitEntryAsync(Guid formId, SubmitEntryRequest)` | Creates or replaces an `EntryRecord`. Triggers manager notification email. |
| `SaveDraftSubmissionAsync(Guid formId, SaveDraftSubmissionRequest)` | Saves a draft entry (status `Draft`). Does not trigger email. |
| `GetDraftSubmissionAsync(Guid formId)` | Returns the current user's unfinished draft for a form, or null. |
| `GetLatestUserEntryAsync(Guid formId)` | Returns the current user's most recent submitted or approved entry. |
| `ReviseEntryAsync(Guid entryId, Dictionary<string, string?> answers)` | Appends a revision. Only allowed when `EditMode` is `OverwriteLatest`. |
| `ResubmitEntryAsync(Guid entryId, ResubmitEntryRequest)` | Restarts the approval workflow after a rejection. |

### Entry queries

| Method | Description |
|---|---|
| `SearchEntriesAsync(Guid? formId, string? search)` | Simple search across submitter and indexed field values. |
| `QueryEntriesAsync(EntryQueryOptions options)` | Paginated, filterable query. Use for admin dashboards. |
| `GetEntryDetailAsync(Guid entryId)` | Returns `EntryDetailViewModel?` with form definition, entry, and approval audit trail. |

### Approval workflow

| Method | Description |
|---|---|
| `ApproveStepAsync(Guid entryId, Guid stepId, string signature)` | Marks a step approved and advances the workflow. |
| `RejectStepAsync(Guid entryId, Guid stepId, string reason)` | Marks a step rejected and sets entry status to `Rejected`. |
| `SendApprovalRemindersAsync(Guid formId)` | Sends reminder emails for all pending approval steps on a form. |

### File operations

| Method | Description |
|---|---|
| `StoreFileAsync(FileUploadRequest)` | Validates and persists a file to storage. Returns `StoredFile`. |
| `StoreFileAsync(FileUploadInput)` | Overload that accepts a `FileUploadInput` (field id + request). |
| `OpenEntryFileAsync(Guid entryId, Guid fileId)` | Opens a file stream for download. Enforces access control. |
| `CleanupStaleDraftFilesAsync(TimeSpan draftAgeThreshold)` | Deletes draft entries and their orphaned files older than the threshold. |

### PDF

| Method | Description |
|---|---|
| `ExportEntryPdfAsync(Guid entryId)` | Exports an entry to PDF bytes wrapped in `EntryPdfExport`. |

### Invitations

| Method | Description |
|---|---|
| `CreateInvitationAsync(CreateInvitationRequest)` | Creates a time-limited invitation token and sends notification email. |
| `AcceptInvitationAsync(string token)` | Validates the token, grants the role, and marks the invitation accepted. |
| `RevokeInvitationAsync(Guid invitationId)` | Marks an invitation revoked. |
| `GetInvitationsAsync(Guid formId)` | Lists all invitations for a form. |

---

## 11. Approval workflow

Entries can require approval from one or more named approvers, collected at submission time.

### Typical flow

```
Submitter fills form → SubmitEntryAsync
	→ entry.Status = NeedsApproval
	→ emails sent to each ApproverInput

Approver → ApproveStepAsync(entryId, stepId, signature)
	→ if all steps approved: entry.Status = Approved

Approver → RejectStepAsync(entryId, stepId, reason)
	→ entry.Status = Rejected

Submitter → ResubmitEntryAsync(entryId, request)
	→ resets approval steps, entry.Status = NeedsApproval
```

### Supplying approvers at submit time

```csharp
var request = new SubmitEntryRequest
{
	Answers = new Dictionary<string, string?>
	{
		["field-name"] = "Jane Smith",
		["field-department"] = "Engineering"
	},
	Approvers =
	[
		new ApproverInput { Name = "John Manager", Email = "john@example.com" },
		new ApproverInput { Name = "Jane Director", Email = "jane@example.com" }
	]
};

var entry = await formsService.SubmitEntryAsync(formId, request, ct);
```

### ApprovalStepRecord

| Property | Description |
|---|---|
| `Id` | Step GUID |
| `Order` | Zero-based ordering |
| `ApproverName` | Display name of the approver |
| `ApproverEmail` | Email to notify |
| `Status` | `Pending`, `Approved`, or `Rejected` |
| `Signature` | Text signature captured at approval |
| `RejectionReason` | Populated on reject |
| `CompletedUtc` | When the step was completed |

### Approval audit trail

Every approval action appends an `ApprovalAuditEvent` to the entry:

```csharp
public sealed class ApprovalAuditEvent
{
	public ApprovalAuditAction Action { get; set; } // StepApproved, StepRejected, ...
	public Guid ActorUserId { get; set; }
	public string ActorDisplayName { get; set; }
	public string? Signature { get; set; }
	public string? Reason { get; set; }
	public string? CorrelationId { get; set; }     // for Graph integration traceability
	public DateTimeOffset OccurredUtc { get; set; }
}
```

---

## 12. File uploads and downloads

### Uploading a file

```csharp
var request = new FileUploadRequest
{
	FileName    = "report.pdf",
	ContentType = "application/pdf",
	Content     = fileBytes,

	// Optional constraints (enforced by the service)
	MaxAllowedBytes  = 5 * 1024 * 1024,            // 5 MB
	AllowedMimeTypes = ["application/pdf"],
	AllowedExtensions = [".pdf"]
};

StoredFile stored = await formsService.StoreFileAsync(request, cancellationToken);
```

The returned `StoredFile` contains `RelativePath`, `Sha256`, `FileName`, `ContentType`, and `Length`. Pass it in `SubmitEntryRequest.Files` as a `SubmittedFileInput`.

### File constraints on a field definition

Set these on `FormFieldDefinition` to have the renderer display constraints and enforce them before calling `StoreFileAsync`:

```csharp
var fileField = new FormFieldDefinition
{
	Id              = "field-attachment",
	Kind            = FormFieldKind.File,
	Label           = "Attachment",
	MaxFileSizeBytes = 10 * 1024 * 1024,   // 10 MB
	MaxFileCount    = 3,                   // multi-select
	AllowedMimeTypes = ["application/pdf", "image/png"],
	AllowedExtensions = [".pdf", ".png"]
};
```

### Signed download tokens

File downloads require a short-lived signed token. Use `EntryFileDownloadTokenService` (registered as singleton) to create tokens to embed in download links:

```csharp
// Inject EntryFileDownloadTokenService into your component or controller
var token = tokenService.Create(entryId, fileId, validFor: TimeSpan.FromMinutes(15));
var url = $"/entry-files/{entryId}/{fileId}?token={token}";
```

Configure the signing secret in production:

```json
{
  "BlazorWebForms": {
	"FileDownloadTokenSecret": "your-strong-secret-here"
  }
}
```

### Draft cleanup

Orphaned draft files are removed by `CleanupStaleDraftFilesAsync`:

```csharp
var result = await formsService.CleanupStaleDraftFilesAsync(
	draftAgeThreshold: TimeSpan.FromDays(30), cancellationToken);

Console.WriteLine($"Deleted {result.DeletedDraftEntries} entries, {result.DeletedFiles} files");
```

Call this from a background service or scheduled job. The SQL registration also provides `DraftCleanupService` (scoped) which respects `EnableDraftCleanup` and `DraftRetentionPeriod` from options.

---

## 13. PDF export

The built-in exporter (`TextPdfExporter`) produces a plain-text structured export suitable for review. Replace it with a custom `IPdfExporter` for full PDF rendering (see section 16).

Call via the controller endpoint (section 8) or directly:

```csharp
EntryPdfExport export = await formsService.ExportEntryPdfAsync(entryId, cancellationToken);
// export.Content  → byte[]
// export.ContentType → "text/plain" (built-in) or "application/pdf" (custom)
// export.FileName → "entry-{id}.txt" or similar
```

Control export limits via options:

```csharp
options.PdfMaxAnswerRows = 500;   // max answer rows included
options.PdfMaxAuditRows  = 250;   // max audit events included
options.PdfMaxBytes      = 1_000_000; // size guard
```

---

## 14. Invitations

Invitations grant a specific email address a role on a form for a limited time.

### Creating an invitation

```csharp
var request = new CreateInvitationRequest
{
	FormId   = formId,
	Email    = "colleague@example.com",
	Role     = FormPermissionRole.Approver,
	ValidFor = TimeSpan.FromDays(7)
};

FormInvitation invitation = await formsService.CreateInvitationAsync(request, ct);
// invitation.Token is the value to share with the recipient
```

### Accepting an invitation

```csharp
// Called by the recipient (authenticated)
FormInvitation accepted = await formsService.AcceptInvitationAsync(invitationToken, ct);
```

### InvitationStatus values

| Value | Description |
|---|---|
| `Pending` | Awaiting acceptance |
| `Accepted` | Token was used successfully |
| `Revoked` | Manually revoked by a manager |
| `Expired` | `ExpiresUtc` has passed |

---

## 15. Email notifications

### Email provider strategies

Configure `EmailProviderStrategy` to one of:

| Strategy | Behaviour |
|---|---|
| `DryRun` | Logs email content; nothing is sent. Default for non-production. |
| `Smtp` | Sends via SMTP. Requires `SmtpHost`, `SmtpPort`, `SmtpUsername`, `SmtpPassword`. |
| `SendGrid` | Sends via SendGrid HTTP API. Requires `SendGridApiKey`. |
| `Graph` | Sends via Microsoft Graph `/sendMail`. Requires `GraphSender`, `GraphAccessToken`. |

### Events that trigger notification emails

| Trigger | Recipients |
|---|---|
| Form submitted | `FormNotificationRule` emails on the form |
| Approver assigned | Named approver's email |
| Approver reminder | Named approver's email (via `SendApprovalRemindersAsync`) |
| Entry approved | Submitter's email |
| Entry rejected | Submitter's email |
| Invitation created | Invitee's email |
| Invitation accepted | Form owner's notification emails |
| Invitation revoked | Invitee's email |

### Retry behavior

The `TemplateEmailNotifier` retries failed sends with linear backoff:

```csharp
options.EmailRetryCount   = 3;
options.EmailRetryDelayMs = 200;
```

Duplicate sends are suppressed by idempotency key tracking. A notification with the same key will not be sent twice within the application lifetime.

### Disabling all outbound email

```csharp
options.EnableOutboundEmail = false;
```

---

## 16. Implementing optional interfaces

All optional interfaces use `TryAddSingleton` / `TryAddScoped`, so you can replace any of them by registering your implementation **before** calling `AddBlazorWebFormsSqlServer`.

### Custom email integration

```csharp
public sealed class MySmtpEmailIntegration : IEmailIntegration
{
	public async Task SendAsync(string to, string subject, string body, CancellationToken ct)
	{
		// Your SMTP/sendgrid/graph implementation
	}
}

// Register before AddBlazorWebFormsSqlServer
builder.Services.AddSingleton<IEmailIntegration, MySmtpEmailIntegration>();
```

### Custom PDF exporter

```csharp
public sealed class MyPdfExporter : IPdfExporter
{
	public async Task<byte[]> ExportEntryAsync(
		FormAggregate form, EntryRecord entry, CancellationToken ct)
	{
		// Generate and return PDF bytes
		return pdfBytes;
	}
}

builder.Services.AddSingleton<IPdfExporter, MyPdfExporter>();
```

### Custom file storage (e.g. Azure Blob)

```csharp
public sealed class AzureBlobFileStorage : IFileStorage
{
	public async Task<StoredFile> SaveAsync(FileUploadRequest request, CancellationToken ct)
	{
		// Upload to Azure Blob Storage
		return new StoredFile { RelativePath = blobName, ... };
	}

	public Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct)
	{
		// Download from Azure Blob
	}

	public Task DeleteAsync(string relativePath, CancellationToken ct)
	{
		// Delete from Azure Blob
	}
}

builder.Services.AddSingleton<IFileStorage, AzureBlobFileStorage>();
```

### Custom anti-abuse guard

```csharp
public sealed class RateLimitedAntiAbuseGuard : IAntiAbuseGuard
{
	public async Task CheckUploadAllowedAsync(
		UserProfile user, FileUploadRequest request, CancellationToken ct)
	{
		// Throw InvalidOperationException to block the upload
	}

	public async Task CheckOutboundNotificationAllowedAsync(
		string channel, string recipient, CancellationToken ct)
	{
		// Throw to block the notification
	}
}

builder.Services.AddSingleton<IAntiAbuseGuard, RateLimitedAntiAbuseGuard>();
```

### Custom operational telemetry (e.g. Application Insights)

```csharp
public sealed class AppInsightsTelemetry : IOperationalTelemetry
{
	public void TrackUpload(string source, long bytes, bool success) { ... }
	public void TrackPdfExport(string source, long bytes, bool success) { ... }
	public void TrackEmailDelivery(string channel, bool success) { ... }
	public void TrackFailure(string area, string operation, string reason) { ... }
}

builder.Services.AddSingleton<IOperationalTelemetry, AppInsightsTelemetry>();
```

---

## 17. SQL Server options reference

All properties on `BlazorWebFormsSqlServerOptions`:

| Property | Default | Description |
|---|---|---|
| `ConnectionString` | LocalDB `BlazorWebForms` | EF Core connection string |
| `StorageRoot` | `<BaseDir>/App_Data/uploads` | Local file storage root |
| `SchemaName` | `bwf` | SQL schema for all BlazorWebForms tables |
| `EnableLocalFileStorage` | `true` | Enables `LocalFileStorage` |
| `DefaultMaxUploadBytes` | `10485760` (10 MB) | Default per-file size limit |
| `DefaultMaxFilesPerField` | `1` | Default max file count per field |
| `EnableDraftCleanup` | `true` | Enables `DraftCleanupService` |
| `DraftRetentionPeriod` | `30 days` | Age threshold for stale draft removal |
| `RequireAdminForDraftCleanup` | `true` | Restricts cleanup trigger to admins |
| `EnableTextBasedPdfExporter` | `true` | Registers `TextPdfExporter` as `IPdfExporter` |
| `PdfMaxAnswerRows` | `500` | Maximum answer rows in PDF |
| `PdfMaxAuditRows` | `250` | Maximum audit events in PDF |
| `PdfMaxBytes` | `1000000` | PDF byte size guard |
| `EnableOutboundEmail` | `false` | Master switch for outbound email |
| `EmailRetryCount` | `3` | Retry attempts per notification |
| `EmailRetryDelayMs` | `200` | Milliseconds between retries |
| `EnableGraphIntegration` | `false` | Activates Microsoft Graph integration |
| `MaxUploadsPerMinute` | `30` | Upload rate limit per user |
| `MaxNotificationsPerMinute` | `120` | Notification rate limit |
| `DataResidenceRegion` | `local-dev` | Compliance tag stored in audit events |
| `EmailProviderStrategy` | `DryRun` | `DryRun`, `Smtp`, `SendGrid`, or `Graph` |
| `EmailFromAddress` | `noreply@example.com` | From address for outbound email |
| `EmailFromDisplayName` | `BlazorWebForms` | From display name |
| `SmtpHost` | `null` | SMTP server hostname |
| `SmtpPort` | `587` | SMTP port |
| `SmtpEnableSsl` | `true` | TLS/SSL for SMTP |
| `SmtpUsername` | `null` | SMTP auth username |
| `SmtpPassword` | `null` | SMTP auth password |
| `SendGridApiKey` | `null` | SendGrid API key |
| `GraphMailEndpoint` | Graph v1.0 sendMail | Microsoft Graph endpoint |
| `GraphSender` | `null` | Sender UPN for Graph |
| `GraphAccessToken` | `null` | OAuth2 token for Graph |

---

## 18. Configuration file reference

`appsettings.json` / `appsettings.Production.json`:

```json
{
  "ConnectionStrings": {
	"BlazorWebForms": "Server=myserver;Database=MyApp;User Id=myuser;Password=mypassword;"
  },
  "BlazorWebForms": {
	"FileDownloadTokenSecret": "your-strong-64-char-secret-here"
  },
  "BlazorWebFormsSqlServer": {
	"SchemaName": "bwf",
	"EnableOutboundEmail": true,
	"EnableGraphIntegration": false,
	"EmailProviderStrategy": "Smtp",
	"EmailFromAddress": "noreply@yourcompany.com",
	"EmailFromDisplayName": "YourApp",
	"SmtpHost": "smtp.yourcompany.com",
	"SmtpPort": 587,
	"SmtpEnableSsl": true,
	"SmtpUsername": "smtp-user",
	"SmtpPassword": "smtp-password",
	"SendGridApiKey": "",
	"GraphSender": "",
	"GraphAccessToken": "",
	"GraphMailEndpoint": "https://graph.microsoft.com/v1.0/users/{sender}/sendMail"
  }
}
```

`appsettings.Development.json` — override for local development:

```json
{
  "ConnectionStrings": {
	"BlazorWebForms": "Server=(localdb)\\MSSQLLocalDB;Database=MyApp;Trusted_Connection=True;"
  },
  "BlazorWebFormsSqlServer": {
	"EnableOutboundEmail": false,
	"EmailProviderStrategy": "DryRun"
  }
}
```

---

## 19. Database migrations

Migrations are included in `BlazorWebForms.Infrastructure.SqlServer`. Apply them using the EF Core CLI, pointing to your startup project for configuration:

```powershell
# Apply all pending migrations
dotnet ef database update \
  --project src/BlazorWebForms.Infrastructure.SqlServer/BlazorWebForms.Infrastructure.SqlServer.csproj \
  --startup-project src/YourApp/YourApp.csproj

# Generate SQL script to review before apply
dotnet ef migrations script \
  --project src/BlazorWebForms.Infrastructure.SqlServer/BlazorWebForms.Infrastructure.SqlServer.csproj \
  --startup-project src/YourApp/YourApp.csproj \
  --output migrations.sql
```

Your startup project must register `BlazorWebFormsSqlServer` with a valid connection string so EF can locate the `DbContext`.

All tables are created under the schema configured by `SchemaName` (default `bwf`). SQL Server-compatible types are used throughout: `uniqueidentifier`, `nvarchar`, `datetimeoffset`, `rowversion`.

---

## 20. CSS and styling

Include the bundled stylesheet in your Blazor host page:

```html
<link rel="stylesheet" href="_content/BlazorWebForms.Blazor/blazorwebforms.css" />
```

Key CSS classes used by components. Override in your own stylesheet to customize appearance:

| Class | Usage |
|---|---|
| `.bwf-builder` | Builder workspace root |
| `.bwf-panel` | Card/panel container |
| `.bwf-grid` | Responsive two-column grid |
| `.bwf-input` | Text/select/date inputs |
| `.bwf-textarea` | Textarea inputs |
| `.bwf-button` | Default button |
| `.bwf-button-primary` | Primary action button |
| `.bwf-link-button` | Anchor styled as button |
| `.bwf-admin` | Admin dashboard root |
| `.bwf-admin-card` | Form card in dashboard |
| `.bwf-card-grid` | Grid of admin cards |
| `.bwf-table` | Entry table |
| `.bwf-table-wrap` | Horizontal-scrollable table wrapper |
| `.bwf-hero` | Published form header area |
| `.bwf-brand-logo` | Logo image in hero |
| `.bwf-public-form` | Published form article root |
| `.bwf-badge` | Status badge |
| `.bwf-status` | Status message text |
| `.bwf-help` | Help/hint text |
| `.bwf-kicker` | Small secondary label above headings |
| `.bwf-actions` | Action button group |
| `.bwf-meta` | Meta information row |

Branding colors (`AccentColor`, `SurfaceColor`, `TextColor`) are applied inline at render time on the hero element. You can inherit them in your overrides with CSS custom properties.

---

## 21. Permissions model

`IPermissionEvaluator` is called throughout the service layer. The default implementation maps `FormPermissionRole` values to actions:

| Check | Roles allowed |
|---|---|
| `CanManageForm` | `Admin`, `Owner`, `Manager` |
| `CanSubmitForm` | `Admin`, `Owner`, `Manager`, `Submitter`, authenticated users for public forms |
| `CanViewEntry` | `Admin`, `Owner`, `Manager`, `Viewer`, the submitter themselves (`SelfViewer`), assigned approvers |

Permissions are stored as `FormPermissionGrant` records on `FormAggregate.Permissions`. The first user to save a form is automatically granted `Owner`.

To extend or override the permission logic, register your own `IPermissionEvaluator` before `AddBlazorWebFormsCore`:

```csharp
builder.Services.AddSingleton<IPermissionEvaluator, MyPermissionEvaluator>();
builder.Services.AddBlazorWebFormsCore();
```

---

## 22. Localization

### Form content localization

Every `FormDefinition`, `FormSectionDefinition`, and `FormFieldDefinition` carries localization dictionaries keyed by culture string (e.g. `"fr-FR"`):

```csharp
definition.LocalizedTitles["fr-FR"]      = "Mon formulaire";
definition.LocalizedDescriptions["fr-FR"] = "Description en français";

section.LocalizedTitles["fr-FR"]         = "Section principale";

field.LocalizedLabels["fr-FR"]           = "Nom complet";
field.LocalizedPlaceholders["fr-FR"]     = "Entrez votre nom";
```

The renderer resolves text with this fallback chain:

```
requested culture → language only → DefaultCulture → base text property
```

### Application UI localization

To localize the shell and navigation strings in your application, register `IStringLocalizer` or a custom localizer and configure `RequestLocalizationOptions`:

```csharp
builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
	var cultures = new[] { "en-US", "fr-FR", "es-ES" }
		.Select(CultureInfo.GetCultureInfo).ToList();
	options.DefaultRequestCulture = new RequestCulture("en-US");
	options.SupportedCultures     = cultures;
	options.SupportedUICultures   = cultures;
	options.RequestCultureProviders =
	[
		new QueryStringRequestCultureProvider(),
		new CookieRequestCultureProvider(),
		new AcceptLanguageHeaderRequestCultureProvider()
	];
});

// ...
app.UseRequestLocalization(
	app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);
```

Persist the user's culture selection:

```csharp
app.MapGet("/culture/set", (HttpContext ctx, string culture, string? returnUrl) =>
{
	ctx.Response.Cookies.Append(
		CookieRequestCultureProvider.DefaultCookieName,
		CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
		new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });
	return Results.LocalRedirect(returnUrl ?? "/");
});
```

---

## 23. Security notes

| Topic | Recommendation |
|---|---|
| `FileDownloadTokenSecret` | Set a strong, randomly generated secret in production. The built-in fallback is for local development only. |
| `DraftRetentionPeriod` | Tune to your compliance requirements. Shorter periods reduce exposure of draft data. |
| `EnableOutboundEmail` | Keep `false` in non-production to avoid sending real emails during development. |
| HTTPS | Always run behind HTTPS in production. Use `UseHttpsRedirection()` and `UseHsts()`. |
| Anti-forgery | `UseAntiforgery()` is required for Blazor form submissions. Do not remove it. |
| Connection string | Store in environment variables or a secrets manager (e.g. Azure Key Vault), never in source-controlled `appsettings.json`. |
| EF migrations | Review the generated SQL script with `migrations script` before applying in production. |
| AGPL-3.0 | This package is licensed under AGPL-3.0-only. Applications that distribute or run this software as a network service must make their source code available under the same license. |

---

## 24. Building a custom UI (e.g. MudBlazor)

The `BlazorWebForms.Blazor` package is entirely optional. The service layer (`BlazorWebForms.Core`) and the persistence layer (`BlazorWebForms.Infrastructure.SqlServer`) are UI-agnostic. You can build every user-facing component yourself using any Blazor UI library.

### Package setup

Install only the two non-UI packages:

```powershell
dotnet add package BlazorWebForms.Core --version 1.0.0
dotnet add package BlazorWebForms.Infrastructure.SqlServer --version 1.0.0
```

Do **not** install `BlazorWebForms.Blazor`. Do not link `blazorwebforms.css`. The `bwf-*` CSS classes are internal to that package and irrelevant to your app.

Install MudBlazor normally:

```powershell
dotnet add package MudBlazor
```

Then follow MudBlazor's setup (add services, add providers, add the stylesheet/scripts). The BlazorWebForms service registration does not conflict with MudBlazor.

### Service registration (unchanged)

```csharp
builder.Services.AddBlazorWebFormsCore();
builder.Services.AddBlazorWebFormsSqlServer(options => { ... });
builder.Services.AddScoped<ICurrentUserContext, ClaimsCurrentUserContext>();
```

No changes needed here. The DI registrations are the same regardless of UI library.

### Core helpers available from BlazorWebForms.Core

Three static/injectable utilities from `BlazorWebForms.Core.Services` do the non-trivial rendering work. Use them directly in your own components:

| Helper | Kind | What it does |
|---|---|---|
| `FormLocalizationResolver.ResolveText(...)` | `static` | Resolves the correct localized string for a field/section with culture fallback |
| `FormLayoutResolver.ResolveSectionColumns(section)` | `static` | Returns the configured column count (1-4) for a section |
| `FormLayoutResolver.ResolveFieldWidthHint(field)` | `static` | Returns `"Auto"`, `"Half"`, `"Full"`, `"Third"`, or `"TwoThirds"` |
| `IConditionEvaluator` | injected | Evaluates `VisibilityConditionDefinition` against the current answers dictionary |

---

### Replacing DynamicFormRenderer

This is the core rendering component. The logic you must replicate:

1. **Section/field visibility** — delegate to `IConditionEvaluator`
2. **Localized text** — delegate to `FormLocalizationResolver.ResolveText`
3. **File uploads** — call `FormsService.StoreFileAsync`, track results, surface errors
4. **Answer tracking** — maintain `Dictionary<string, string?>` and fire a callback on change

```razor
@* MyDynamicFormRenderer.razor *@
@using BlazorWebForms.Core.Abstractions
@using BlazorWebForms.Core.Models
@using BlazorWebForms.Core.Services
@using MudBlazor
@using System.Globalization
@inject FormsApplicationService FormsService
@inject IConditionEvaluator ConditionEvaluator

@foreach (var section in Definition.Sections.Where(IsSectionVisible))
{
    <MudText Typo="Typo.h6">@Localize(section.Title, section.LocalizedTitles)</MudText>
    <MudText Typo="Typo.body2">@Localize(section.Description, section.LocalizedDescriptions)</MudText>

    @foreach (var field in section.Fields.Where(IsFieldVisible))
    {
        <div style="@GetWidthStyle(field)">
            @switch (field.Kind)
            {
                case FormFieldKind.Text:
                case FormFieldKind.Signature:
                    <MudTextField Label="@FieldLabel(field)"
                                  Value="@GetValue(field.Id)"
                                  ValueChanged="@(v => UpdateValue(field.Id, v))"
                                  Required="@field.Required"
                                  HelperText="@FieldHelp(field)"
                                  Placeholder="@FieldPlaceholder(field)" />
                    break;

                case FormFieldKind.Number:
                    <MudNumericField Label="@FieldLabel(field)"
                                     Value="@ParseDecimal(GetValue(field.Id))"
                                     ValueChanged="@(v => UpdateValue(field.Id, v?.ToString()))"
                                     Required="@field.Required"
                                     HelperText="@FieldHelp(field)" />
                    break;

                case FormFieldKind.TextArea:
                case FormFieldKind.RichText:
                    <MudTextField Label="@FieldLabel(field)"
                                  Value="@GetValue(field.Id)"
                                  ValueChanged="@(v => UpdateValue(field.Id, v))"
                                  Lines="4"
                                  Required="@field.Required"
                                  HelperText="@FieldHelp(field)" />
                    break;

                case FormFieldKind.Checkbox:
                    <MudCheckBox Label="@FieldLabel(field)"
                                 Checked="@IsChecked(field.Id)"
                                 CheckedChanged="@(v => UpdateValue(field.Id, v.ToString()))" />
                    break;

                case FormFieldKind.Select:
                    <MudSelect Label="@FieldLabel(field)"
                               Value="@GetValue(field.Id)"
                               ValueChanged="@(v => UpdateValue(field.Id, v))"
                               Required="@field.Required"
                               HelperText="@FieldHelp(field)">
                        @foreach (var opt in field.Options)
                        {
                            <MudSelectItem Value="@opt.Value">@Localize(opt.Label, opt.LocalizedLabels)</MudSelectItem>
                        }
                    </MudSelect>
                    break;

                case FormFieldKind.Radio:
                    <MudRadioGroup Value="@GetValue(field.Id)"
                                   ValueChanged="@(v => UpdateValue(field.Id, v))">
                        @foreach (var opt in field.Options)
                        {
                            <MudRadio Value="@opt.Value">@Localize(opt.Label, opt.LocalizedLabels)</MudRadio>
                        }
                    </MudRadioGroup>
                    break;

                case FormFieldKind.Date:
                    <MudDatePicker Label="@FieldLabel(field)"
                                   Date="@ParseDate(GetValue(field.Id))"
                                   DateChanged="@(v => UpdateValue(field.Id, v?.ToString("yyyy-MM-dd")))"
                                   Required="@field.Required"
                                   HelperText="@FieldHelp(field)" />
                    break;

                case FormFieldKind.File:
                    <MudFileUpload T="IBrowserFile" FilesChanged="@(f => OnFileChanged(field.Id, f))"
                                   Accept="@string.Join(",", field.AllowedExtensions)">
                        <ButtonTemplate>
                            <MudButton HtmlTag="label" Variant="Variant.Filled" Color="Color.Primary"
                                       StartIcon="@Icons.Material.Filled.CloudUpload" for="@context.Id">
                                @FieldLabel(field)
                            </MudButton>
                        </ButtonTemplate>
                    </MudFileUpload>
                    @if (uploadErrorsByField.TryGetValue(field.Id, out var err))
                    {
                        <MudAlert Severity="Severity.Error">@err</MudAlert>
                    }
                    @if (uploadedNamesByField.TryGetValue(field.Id, out var name))
                    {
                        <MudText Typo="Typo.caption">Uploaded: @name</MudText>
                    }
                    break;
            }
        </div>
    }
}

@code {
    [Parameter, EditorRequired] public required FormDefinition Definition { get; set; }
    [Parameter, EditorRequired] public required Dictionary<string, string?> Answers { get; set; }
    [Parameter] public string? Culture { get; set; }
    [Parameter] public bool ReadOnly { get; set; }
    [Parameter] public EventCallback<Dictionary<string, string?>> AnswersChanged { get; set; }
    [Parameter] public EventCallback<List<SubmittedFileInput>> FilesChanged { get; set; }

    private readonly Dictionary<string, List<SubmittedFileInput>> uploadedFilesByField = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> uploadErrorsByField = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> uploadedNamesByField = new(StringComparer.OrdinalIgnoreCase);

    private string EffectiveCulture => string.IsNullOrWhiteSpace(Culture)
        ? CultureInfo.CurrentUICulture.Name : Culture!;

    // --- Visibility ---
    private bool IsSectionVisible(FormSectionDefinition section) =>
        ConditionEvaluator.IsVisible(section.VisibilityRules, Answers);

    private bool IsFieldVisible(FormFieldDefinition field) =>
        ConditionEvaluator.IsVisible(field.VisibilityRules, Answers);

    // --- Localization ---
    private string Localize(string baseText, Dictionary<string, string> map) =>
        FormLocalizationResolver.ResolveText(baseText, map, EffectiveCulture, Definition.DefaultCulture);

    private string FieldLabel(FormFieldDefinition f) => Localize(f.Label, f.LocalizedLabels);
    private string FieldHelp(FormFieldDefinition f) => Localize(f.HelpText, f.LocalizedHelpTexts);
    private string FieldPlaceholder(FormFieldDefinition f) => Localize(f.Placeholder, f.LocalizedPlaceholders);

    // --- Layout ---
    private static string GetWidthStyle(FormFieldDefinition field) =>
        FormLayoutResolver.ResolveFieldWidthHint(field) switch
        {
            "Half"      => "width:50%",
            "Third"     => "width:33%",
            "TwoThirds" => "width:66%",
            "Full"      => "width:100%",
            _           => "width:auto"
        };

    // --- Answer tracking ---
    private string? GetValue(string id) => Answers.TryGetValue(id, out var v) ? v : null;
    private bool IsChecked(string id) => string.Equals(GetValue(id), "true", StringComparison.OrdinalIgnoreCase);

    private async Task UpdateValue(string id, string? value)
    {
        Answers[id] = value;
        await AnswersChanged.InvokeAsync(Answers);
    }

    // --- Type converters ---
    private static decimal? ParseDecimal(string? v) =>
        decimal.TryParse(v, out var d) ? d : null;

    private static DateTime? ParseDate(string? v) =>
        DateTime.TryParse(v, out var dt) ? dt : null;

    // --- File uploads ---
    private async Task OnFileChanged(string fieldId, IBrowserFile file)
    {
        uploadErrorsByField.Remove(fieldId);
        var field = Definition.Sections.SelectMany(s => s.Fields)
            .FirstOrDefault(f => f.Id == fieldId);

        try
        {
            var maxBytes = field?.MaxFileSizeBytes ?? 10 * 1024 * 1024L;
            await using var stream = file.OpenReadStream(maxAllowedSize: maxBytes);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);

            var stored = await FormsService.StoreFileAsync(new FileUploadRequest
            {
                FileName         = file.Name,
                ContentType      = file.ContentType,
                Content          = memory.ToArray(),
                MaxAllowedBytes  = maxBytes,
                AllowedExtensions = field?.AllowedExtensions ?? [],
                AllowedMimeTypes  = field?.AllowedMimeTypes  ?? []
            });

            var entry = new SubmittedFileInput { FieldId = fieldId, File = stored };
            uploadedFilesByField[fieldId] = [entry];
            uploadedNamesByField[fieldId] = stored.FileName;

            var flat = uploadedFilesByField.Values.SelectMany(v => v).ToList();
            await FilesChanged.InvokeAsync(flat);

            Answers[fieldId] = stored.FileName;
            await AnswersChanged.InvokeAsync(Answers);
        }
        catch (Exception ex)
        {
            uploadErrorsByField[fieldId] = ex.Message;
        }
    }
}
```

---

### Replacing PublishedFormView

Call the service directly to load, draft, and submit. Feed the answers into your own `MyDynamicFormRenderer`.

```razor
@* MyPublishedFormPage.razor *@
@page "/forms/{Slug}"
@attribute [Authorize]
@using BlazorWebForms.Core.Models
@using BlazorWebForms.Core.Services
@using MudBlazor
@inject FormsApplicationService FormsService
@inject NavigationManager Nav

@if (ViewModel is null)
{
    <MudProgressCircular Indeterminate="true" />
}
else
{
    <MudText Typo="Typo.h4">@ViewModel.Definition.Title</MudText>

    <MyDynamicFormRenderer Definition="ViewModel.Definition"
                           Answers="answers"
                           AnswersChanged="a => answers = a"
                           FilesChanged="f => files = f" />

    @foreach (var approver in approvers)
    {
        <MudTextField Label="Approver name"  @bind-Value="approver.Name" />
        <MudTextField Label="Approver email" @bind-Value="approver.Email" />
    }
    <MudButton OnClick="AddApprover">Add approver</MudButton>

    @if (!string.IsNullOrEmpty(statusMessage))
    {
        <MudAlert Severity="Severity.Info">@statusMessage</MudAlert>
    }

    <MudButton OnClick="SaveDraftAsync" Variant="Variant.Outlined">Save draft</MudButton>
    <MudButton OnClick="SubmitAsync" Variant="Variant.Filled" Color="Color.Primary"
               Disabled="isSubmitting">
        @(isSubmitting ? "Submitting..." : "Submit")
    </MudButton>
}

@code {
    [Parameter, EditorRequired] public required string Slug { get; set; }

    private PublishedFormViewModel? ViewModel { get; set; }
    private Dictionary<string, string?> answers = new(StringComparer.OrdinalIgnoreCase);
    private List<SubmittedFileInput> files = [];
    private List<ApproverInput> approvers = [new()];
    private Guid? draftEntryId;
    private string statusMessage = string.Empty;
    private bool isSubmitting;

    protected override async Task OnParametersSetAsync()
    {
        ViewModel = await FormsService.GetPublishedFormAsync(Slug);
        if (ViewModel is null) return;

        // Resume any existing draft
        var draft = await FormsService.GetDraftSubmissionAsync(ViewModel.Form.Id);
        if (draft is not null)
        {
            draftEntryId = draft.Id;
            answers = new Dictionary<string, string?>(draft.Answers, StringComparer.OrdinalIgnoreCase);
            approvers = draft.ApprovalSteps
                .OrderBy(s => s.Order)
                .Select(s => new ApproverInput { Name = s.ApproverName, Email = s.ApproverEmail })
                .ToList();
            if (approvers.Count == 0) approvers.Add(new());
            statusMessage = $"Draft loaded.";
        }
    }

    private void AddApprover() => approvers.Add(new ApproverInput());

    private async Task SaveDraftAsync()
    {
        if (ViewModel is null) return;
        var draft = await FormsService.SaveDraftSubmissionAsync(ViewModel.Form.Id, new SaveDraftSubmissionRequest
        {
            DraftEntryId = draftEntryId,
            Answers      = new(answers, StringComparer.OrdinalIgnoreCase),
            Approvers    = approvers.Where(a => !string.IsNullOrWhiteSpace(a.Email)).ToList(),
            Files        = files
        });
        draftEntryId  = draft.Id;
        statusMessage = "Draft saved.";
    }

    private async Task SubmitAsync()
    {
        if (ViewModel is null || isSubmitting) return;
        isSubmitting = true;
        try
        {
            var entry = await FormsService.SubmitEntryAsync(ViewModel.Form.Id, new SubmitEntryRequest
            {
                DraftEntryId = draftEntryId,
                Answers      = new(answers, StringComparer.OrdinalIgnoreCase),
                Approvers    = approvers.Where(a => !string.IsNullOrWhiteSpace(a.Email)).ToList(),
                Files        = files
            });
            Nav.NavigateTo($"/entries/{entry.Id}");
        }
        catch (InvalidOperationException ex)
        {
            statusMessage = ex.Message;
        }
        finally { isSubmitting = false; }
    }
}
```

---

### Replacing FormsAdminDashboard

```razor
@* MyAdminPage.razor *@
@page "/admin"
@attribute [Authorize(Roles = "Admin,Owner,Manager")]
@using BlazorWebForms.Core.Models
@using BlazorWebForms.Core.Services
@using MudBlazor
@inject FormsApplicationService FormsService

@if (dashboard is null)
{
    <MudProgressCircular Indeterminate="true" />
}
else
{
    <MudText Typo="Typo.h4">Forms</MudText>
    <MudGrid>
        @foreach (var form in dashboard.Forms)
        {
            <MudItem xs="12" sm="6" md="4">
                <MudCard>
                    <MudCardContent>
                        <MudText Typo="Typo.h6">@form.Name</MudText>
                        <MudText Typo="Typo.body2">@form.Description</MudText>
                        <MudText Typo="Typo.caption">Versions: @form.Versions.Count</MudText>
                    </MudCardContent>
                    <MudCardActions>
                        <MudButton Href="@($"/builder/{form.Id}")" Variant="Variant.Text">Builder</MudButton>
                        <MudButton Href="@($"/forms/{form.Publication.Slug}")" Variant="Variant.Text">Published</MudButton>
                    </MudCardActions>
                </MudCard>
            </MudItem>
        }
    </MudGrid>

    <MudText Typo="Typo.h4" Class="mt-4">Entries</MudText>
    <MudTextField @bind-Value="search" Label="Search" Immediate="true"
                  DebounceInterval="300" OnDebounceIntervalElapsed="LoadEntriesAsync" />

    <MudTable Items="entries" Dense="true" Hover="true">
        <HeaderContent>
            <MudTh>Submitter</MudTh>
            <MudTh>Status</MudTh>
            <MudTh>Submitted</MudTh>
        </HeaderContent>
        <RowTemplate>
            <MudTd><MudLink Href="@($"/entries/{context.Id}")">@context.SubmittedBy</MudLink></MudTd>
            <MudTd><MudChip>@context.Status</MudChip></MudTd>
            <MudTd>@context.SubmittedUtc.LocalDateTime</MudTd>
        </RowTemplate>
    </MudTable>

    <MudPagination Count="totalPages" Selected="currentPage" SelectedChanged="OnPageChanged" />
}

@code {
    private DashboardViewModel? dashboard;
    private IReadOnlyList<EntryRecord> entries = [];
    private string search = string.Empty;
    private int currentPage = 1;
    private int totalPages = 1;
    private const int PageSize = 50;

    protected override async Task OnInitializedAsync()
    {
        dashboard = await FormsService.GetDashboardAsync();
        await LoadEntriesAsync();
    }

    private async Task LoadEntriesAsync()
    {
        currentPage = 1;
        await FetchPageAsync();
    }

    private async Task OnPageChanged(int page)
    {
        currentPage = page;
        await FetchPageAsync();
    }

    private async Task FetchPageAsync()
    {
        var options = new EntryQueryOptions
        {
            Search = string.IsNullOrWhiteSpace(search) ? null : search,
            Offset = (currentPage - 1) * PageSize,
            Limit  = PageSize
        };
        entries    = await FormsService.QueryEntriesAsync(options);
        totalPages = entries.Count == PageSize ? currentPage + 1 : currentPage;
    }
}
```

---

### Replacing FormBuilderWorkspace

The builder is the most complex component. For a custom UI you own the full form definition object and call save/publish when ready.

```razor
@* MyBuilderPage.razor *@
@page "/builder"
@page "/builder/{FormId:guid}"
@attribute [Authorize(Roles = "Admin,Owner,Manager")]
@using BlazorWebForms.Core.Models
@using BlazorWebForms.Core.Services
@using MudBlazor
@inject FormsApplicationService FormsService

@if (state is null)
{
    <MudProgressCircular Indeterminate="true" />
}
else
{
    <MudTextField Label="Form name"        @bind-Value="state.Form.Name" />
    <MudTextField Label="Slug"             @bind-Value="state.Form.Publication.Slug" />
    <MudTextField Label="Description"      @bind-Value="state.Form.Description" Lines="3" />
    <MudSelect Label="Access mode"         @bind-Value="state.Form.Publication.AccessMode">
        <MudSelectItem Value="FormAccessMode.Authenticated">Authenticated</MudSelectItem>
        <MudSelectItem Value="FormAccessMode.Public">Public</MudSelectItem>
    </MudSelect>

    @* Render your own section/field editors here *@

    <MudButton OnClick="SaveAsync"    Variant="Variant.Outlined">Save draft</MudButton>
    <MudButton OnClick="PublishAsync" Variant="Variant.Filled" Color="Color.Primary">Publish</MudButton>

    @if (!string.IsNullOrEmpty(statusMessage))
    {
        <MudAlert Severity="Severity.Info">@statusMessage</MudAlert>
    }
}

@code {
    [Parameter] public Guid? FormId { get; set; }

    private BuilderState? state;
    private string statusMessage = string.Empty;

    protected override async Task OnParametersSetAsync()
    {
        state = await FormsService.GetBuilderStateAsync(FormId);
    }

    private async Task SaveAsync()
    {
        if (state is null) return;
        await FormsService.SaveDraftAsync(new SaveDraftRequest
        {
            FormId      = state.Form.Id,
            Name        = state.Form.Name,
            Description = state.Form.Description,
            Slug        = state.Form.Publication.Slug,
            AccessMode  = state.Form.Publication.AccessMode,
            EditMode    = state.Form.Publication.EditMode,
            Definition  = state.Form.DraftDefinition
        });
        statusMessage = "Draft saved.";
    }

    private async Task PublishAsync()
    {
        if (state is null) return;
        var version = await FormsService.PublishAsync(state.Form.Id);
        statusMessage = $"Published as version {version.VersionNumber}.";
    }
}
```

---

### Summary of what comes from the package vs. what you own

| Concern | Comes from package | You implement |
|---|---|---|
| Service calls (load, submit, save, approve) | `FormsApplicationService` in `Core` | — |
| Domain models | `Core.Models` | — |
| Visibility evaluation | `IConditionEvaluator` in `Core` | — |
| Localization resolution | `FormLocalizationResolver` in `Core` | — |
| Layout hints | `FormLayoutResolver` in `Core` | Map hints to your UI system's sizing |
| File storage, email, PDF, EF | `Infrastructure.SqlServer` | — |
| HTML/component markup | — | Your components using MudBlazor (or any library) |
| CSS and styling | — | Your stylesheet / MudBlazor theme |
| Auth policies | — | Your `AddAuthorization` setup |
| Controllers (files, PDF, invitations) | — | Your controllers (see section 8) |

---

## 25. /entries route (Entry detail)

The `/entries/{entryId}` route is the entry-detail screen linked from the admin list (for example: `<MudLink Href="@($"/entries/{context.Id}")">`).

This page is where users can:
- View submitted answers in read-only mode
- View file metadata and download files
- View approval steps and approval audit trail
- Trigger PDF export

### Route and authorization

```razor
@page "/entries/{EntryId:guid}"
@attribute [Authorize(Policy = "SelfViewAccess")]
```

This matches the sample security model: submitter/self-viewer, assigned approvers, managers/owners/admins.

### Minimum page flow

1. Load entry detail view model:

```csharp
ViewModel = await FormsService.GetEntryDetailAsync(EntryId);
```

2. If `ViewModel is null` → show not found/forbidden message.
3. If `!ViewModel.CanView` → show access denied message.
4. Render answers read-only (using your own UI or `DynamicFormRenderer ReadOnly="true"`).
5. Render file links with signed download token.
6. Optionally render approval steps/audit and PDF action.

### Data contract used by the page

`GetEntryDetailAsync` returns `EntryDetailViewModel`:

| Property | Description |
|---|---|
| `Form` | Parent form aggregate |
| `Entry` | Entry data (answers, files, status, approval steps, audit trail) |
| `Definition` | Published form definition used for rendering |
| `CanView` | Service-layer permission result |
| `HistoricalRenderWarning` | Optional warning when historical render fallback is used |

### File download links from /entries

Use the token service to create short-lived signed URLs:

```csharp
var token = DownloadTokenService.Create(entryId, fileId, TimeSpan.FromMinutes(10));
var url = $"/entry-files/{entryId}/{fileId}?token={Uri.EscapeDataString(token)}";
```

Endpoint consumed:

```http
GET /entry-files/{entryId}/{fileId}?token=...
```

### PDF action from /entries

Use the PDF endpoint directly from the page:

```csharp
var pdfUrl = $"/entry-pdf/{entryId}";
NavigationManager.NavigateTo(pdfUrl, forceLoad: true);
```

Endpoint consumed:

```http
GET /entry-pdf/{entryId}
```

### MudBlazor example for /entries

```razor
@page "/entries/{EntryId:guid}"
@attribute [Authorize]
@using BlazorWebForms.Core.Models
@using BlazorWebForms.Core.Services
@using MudBlazor
@inject FormsApplicationService FormsService
@inject EntryFileDownloadTokenService TokenService
@inject NavigationManager Nav

@if (vm is null)
{
    <MudProgressCircular Indeterminate="true" />
}
else if (!vm.CanView)
{
    <MudAlert Severity="Severity.Error">Access denied.</MudAlert>
}
else
{
    <MudText Typo="Typo.h5">@vm.Form.Name</MudText>
    <MudChip>@vm.Entry.Status</MudChip>

    @foreach (var pair in vm.Entry.Answers)
    {
        <MudText><strong>@pair.Key:</strong> @pair.Value</MudText>
    }

    @if (vm.Entry.Files.Count > 0)
    {
        <MudDivider Class="my-4" />
        <MudText Typo="Typo.h6">Files</MudText>
        @foreach (var f in vm.Entry.Files)
        {
            var token = TokenService.Create(EntryId, f.Id, TimeSpan.FromMinutes(10));
            var url = $"/entry-files/{EntryId}/{f.Id}?token={Uri.EscapeDataString(token)}";
            <MudLink Href="@url">@f.FileName</MudLink>
            <MudText Typo="Typo.caption">@f.ContentType · @f.Length bytes</MudText>
        }
    }

    @if (vm.Entry.ApprovalSteps.Count > 0)
    {
        <MudDivider Class="my-4" />
        <MudText Typo="Typo.h6">Approval steps</MudText>
        @foreach (var step in vm.Entry.ApprovalSteps.OrderBy(s => s.Order))
        {
            <MudText>@step.ApproverName (@step.ApproverEmail) - @step.Status</MudText>
        }
    }

    @if (vm.Entry.ApprovalAuditTrail.Count > 0)
    {
        <MudDivider Class="my-4" />
        <MudText Typo="Typo.h6">Approval audit trail</MudText>
        @foreach (var a in vm.Entry.ApprovalAuditTrail.OrderByDescending(a => a.OccurredUtc))
        {
            <MudText>@a.Action - @a.ActorDisplayName - @a.OccurredUtc.LocalDateTime</MudText>
        }
    }

    <MudButton OnClick="DownloadPdf">Download PDF</MudButton>
}

@code {
    [Parameter] public Guid EntryId { get; set; }
    private EntryDetailViewModel? vm;

    protected override async Task OnParametersSetAsync()
        => vm = await FormsService.GetEntryDetailAsync(EntryId);

    private void DownloadPdf() => Nav.NavigateTo($"/entry-pdf/{EntryId}", forceLoad: true);
}
```

### Optional: approval actions on /entries

If you want approvers to act directly on this route, add buttons and call:

- `FormsService.ApproveStepAsync(entryId, stepId, signature)`
- `FormsService.RejectStepAsync(entryId, stepId, reason)`

Then reload the entry detail and show updated `Entry.Status`, `ApprovalSteps`, and `ApprovalAuditTrail`.

