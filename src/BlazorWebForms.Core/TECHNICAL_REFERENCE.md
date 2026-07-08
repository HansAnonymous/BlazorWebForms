# BlazorWebForms.Core

[![NuGet](https://img.shields.io/nuget/v/BlazorWebForms.Core)](https://www.nuget.org/packages/BlazorWebForms.Core)
[![License: AGPL-3.0](https://img.shields.io/badge/license-AGPL--3.0-blue)](https://github.com/HansAnonymous/BlazorWebForms/blob/main/LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-purple)](https://dotnet.microsoft.com)

Core library for [BlazorWebForms](https://github.com/HansAnonymous/BlazorWebForms).

Provides the domain models, `FormsApplicationService`, and all extensibility interfaces. **No database, no UI framework, no email provider dependencies** — only `Microsoft.AspNetCore.App`.

---

## Installation

```powershell
dotnet add package BlazorWebForms.Core
```

---

## Minimal Setup

### 1. Register services

```csharp
// Program.cs
using BlazorWebForms.Core.Services;

builder.Services.AddBlazorWebFormsCore();

// Required: implement ICurrentUserContext (see below)
builder.Services.AddScoped<ICurrentUserContext, YourCurrentUserContext>();
```

### 2. Implement `ICurrentUserContext`

This is the **only required implementation**. It tells the service layer who the current user is.

```csharp
using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;

public sealed class YourCurrentUserContext(IHttpContextAccessor http) : ICurrentUserContext
{
    public UserProfile GetCurrentUser()
    {
        var principal = http.HttpContext?.User;
        if (principal?.Identity is not { IsAuthenticated: true })
            return new UserProfile { IsAuthenticated = false };

        return new UserProfile
        {
            UserId      = Guid.Parse(principal.FindFirst("sub")?.Value ?? Guid.NewGuid().ToString()),
            IsAuthenticated = true,
            DisplayName = principal.FindFirst("name")?.Value ?? "User",
            Email       = principal.FindFirst("email")?.Value ?? string.Empty,
            Roles       = [FormPermissionRole.Submitter]
        };
    }
}
```

See [CORE_EXTENDING.md](../../docs/CORE_EXTENDING.md) for cookie auth, OIDC, and Azure AD examples.

### 3. Use `FormsApplicationService`

```csharp
[Inject] private FormsApplicationService Forms { get; set; } = null!;

// Define and publish a form
var draft = await Forms.SaveDraftAsync(new SaveDraftRequest
{
    Name = "Expense Claim",
    Slug = "expense-claim",
    Definition = new FormDefinition
    {
        DefaultCulture = "en-US",
        Sections =
        [
            new FormSectionDefinition
            {
                Title = "Claim Details",
                Fields =
                [
                    new FormFieldDefinition { Id = "amount",      Label = "Amount",      Kind = FormFieldKind.Number,  Required = true },
                    new FormFieldDefinition { Id = "description", Label = "Description", Kind = FormFieldKind.TextArea, Required = true },
                    new FormFieldDefinition { Id = "receipt",     Label = "Receipt",     Kind = FormFieldKind.File }
                ]
            }
        ]
    }
});

await Forms.PublishAsync(draft.Id);

// Collect a submission with approvers
var entry = await Forms.SubmitEntryAsync(draft.Id, new SubmitEntryRequest
{
    Answers   = new() { ["amount"] = "149.50", ["description"] = "Team lunch" },
    Approvers = [new ApproverInput { Id = "E-1042", DisplayName = "Jane Smith", Email = "jane@corp.com" }]
});
```

---

## Field Types

| Kind | Description |
|---|---|
| `Text` | Single-line text input |
| `TextArea` | Multi-line text input |
| `Number` | Numeric input with optional unit, min/max, step |
| `Select` | Dropdown from a fixed option list |
| `Radio` | Single-choice radio group |
| `Checkbox` | Boolean toggle |
| `Date` | Date picker |
| `File` | File upload — count, size, MIME, and extension constraints |
| `RichText` | HTML rich-text editor |
| `Signature` | Typed signature with confirmation timestamp |
| `RepeatableList` | Repeatable row set with typed columns |
| `RankedChoice` | Drag-and-drop option ranking |
| `Custom` | Developer-defined type — register an `ICustomFieldHandler` |

See [QUESTION_TYPES.md](../../docs/QUESTION_TYPES.md) for full property reference, validation rules, and answer formats.

---

## Key APIs

### Form lifecycle

```csharp
BuilderState           state  = await Forms.GetBuilderStateAsync(formId);
FormAggregate          form   = await Forms.SaveDraftAsync(request);
FormAggregate          form   = await Forms.PublishAsync(formId);
DashboardViewModel     dash   = await Forms.GetDashboardAsync();
PublishedFormViewModel view   = await Forms.GetPublishedFormAsync(slug);
```

### Submissions

```csharp
EntryRecord                  entry  = await Forms.SubmitEntryAsync(formId, request);
EntryRecord                  draft  = await Forms.SaveDraftSubmissionAsync(formId, request);
EntryRecord                  draft  = await Forms.GetDraftSubmissionAsync(formId);
EntryRecord                  entry  = await Forms.ResubmitEntryAsync(entryId, request);
EntryDetailViewModel         detail = await Forms.GetEntryDetailAsync(entryId);
IReadOnlyList<EntryRecord>   page   = await Forms.QueryEntriesAsync(options);
```

### Approval workflow

```csharp
EntryRecord entry = await Forms.ApproveStepAsync(entryId, stepId, signature);
EntryRecord entry = await Forms.RejectStepAsync(entryId, stepId, reason);
```

### Files and export

```csharp
StoredFile        file     = await Forms.StoreFileAsync(request);
EntryFileDownload download = await Forms.OpenEntryFileAsync(entryId, fileId);
EntryPdfExport    pdf      = await Forms.ExportEntryPdfAsync(entryId);
```

### Invitations

```csharp
FormInvitation inv = await Forms.CreateInvitationAsync(request);
FormInvitation inv = await Forms.AcceptInvitationAsync(token);
await Forms.RevokeInvitationAsync(invitationId);
```

See [CORE_API_REFERENCE.md](../../docs/CORE_API_REFERENCE.md) for the complete method reference.

---

## Extensibility Interfaces

| Interface | Required | Default | Purpose |
|---|---|---|---|
| `ICurrentUserContext` | **Yes** | none | Current authenticated user |
| `IFormsRepository` | Yes (via infra) | `EfFormsRepository` | Persistence |
| `IPermissionEvaluator` | No | `DefaultPermissionEvaluator` | Role-based access control |
| `IConditionEvaluator` | No | `SimpleConditionEvaluator` | Visibility rule evaluation |
| `IFormDefinitionSerializer` | No | `JsonFormDefinitionSerializer` | Form schema serialization |
| `IFileStorage` | No | `LocalFileStorage` | Binary file persistence |
| `IEmailNotifier` | No | `MemoryEmailNotifier` | Workflow email dispatch |
| `IPdfExporter` | No | `TextPdfExporter` | Entry PDF generation |
| `IAntiAbuseGuard` | No | `NoOpAntiAbuseGuard` | Rate limiting |
| `IOperationalTelemetry` | No | `NoOpOperationalTelemetry` | Metrics and counters |
| `IEmployeePrefillProvider` | No | no-op | Employee data for prefill |
| `IFormPrefillProvider` | No | claim / employee / fixed | Field value prefill |
| `ICustomFieldHandler` | No | none | Custom `FormFieldKind.Custom` handler |

Register your own implementation before calling `AddBlazorWebFormsCore()`, or use `TryAdd*` so the built-in acts as a fallback.

---

## Custom Field Types

```csharp
// 1. Implement the interface
public sealed class RatingFieldHandler : ICustomFieldHandler
{
    public string Kind => "rating";

    public void ValidateDefinition(FormFieldDefinition field)
    {
        if (!field.Metadata.TryGetValue("maxStars", out var raw)
            || !int.TryParse(raw, out var stars)
            || stars is < 1 or > 10)
        {
            throw new InvalidOperationException(
                $"Field '{field.Label}': Metadata['maxStars'] must be an integer 1-10.");
        }
    }
}

// 2. Register it
builder.Services.AddCustomFieldHandler<RatingFieldHandler>();

// 3. Use it in a definition
new FormFieldDefinition
{
    Id         = "satisfaction",
    Kind       = FormFieldKind.Custom,
    CustomKind = "rating",
    Label      = "Satisfaction",
    Required   = true,
    Metadata   = new() { ["maxStars"] = "5" }
}
```

`ValidateDefinition` is called during `PublishAsync`. If no handler is registered for a `CustomKind`, the field passes as-is — ideal for types rendered purely by the Blazor layer.

---

## Metadata

Attach arbitrary key/value pairs to forms, sections, or fields:

```csharp
definition.Metadata["department"]  = "HR";
definition.Metadata["process-id"]  = "onboarding-v3";

section.Metadata["layout-hint"]    = "two-column";

field.Metadata["helpdesk-tag"]     = "expense-category";
```

Metadata is serialized with the form definition JSON and round-trips through storage unchanged.

---

## Localization

```csharp
var definition = new FormDefinition
{
    DefaultCulture = "en-US",
    Title          = "Travel Request",
    LocalizedTitles = new() { ["fr-FR"] = "Demande de voyage" }
};

field.Label = "Destination";
field.LocalizedLabels["fr-FR"] = "Destination";
```

Culture resolution: **requested culture → language → default culture → base text**.

---

## Visibility Conditions

```csharp
field.VisibilityRules = new VisibilityConditionDefinition
{
    Join  = VisibilityJoinOperator.And,
    Rules =
    [
        new VisibilityRuleDefinition
        {
            FieldId  = "employment-status",
            Operator = VisibilityRuleOperator.Equals,
            Value    = "employed"
        }
    ]
};
```

Operators: `Equals`, `NotEquals`, `Contains`, `Empty`. Join modes: `And`, `Or`.

---

## Domain Model Overview

```
FormAggregate
  └─ FormDefinition
       ├─ Metadata                        Dictionary<string, string>
       ├─ LocalizedTitles / Descriptions
       ├─ Branding                        logo, colors, hero text
       └─ FormSectionDefinition[]
            ├─ Metadata
            ├─ VisibilityRules
            └─ FormFieldDefinition[]
                 ├─ Kind / CustomKind
                 ├─ Metadata
                 ├─ Options / RepeatableColumns
                 └─ Prefill / VisibilityRules

EntryRecord
  ├─ Status           Draft | Submitted | NeedsApproval | Approved | Rejected
  ├─ Answers          Dictionary<string, string?>
  ├─ ApprovalSteps    ApprovalStepRecord[]  (ApproverId, ApproverName, ApproverEmail)
  ├─ ApprovalAuditTrail  ApprovalAuditEvent[]
  └─ Files            EntryFileRecord[]
```

See [CORE_MODELS.md](../../docs/CORE_MODELS.md) for the complete property reference.

---

## Package Layout

```
BlazorWebForms.Core/
├── Abstractions/
│   ├── Contracts.cs              # All service interfaces
│   └── ICoreMetadataCache.cs
├── Models/
│   ├── DomainModels.cs           # FormAggregate, EntryRecord, ApprovalStepRecord…
│   ├── Enums.cs                  # FormFieldKind, EntryStatus, FormAccessMode…
│   ├── FormDefinitionModels.cs   # FormDefinition, FormSectionDefinition…
│   └── ViewModels.cs             # Request/response objects
└── Services/
    ├── FormsApplicationService.cs # Main entry point
    ├── DefaultImplementations.cs  # IPermissionEvaluator, IConditionEvaluator
    ├── BlazorWebFormsServiceCollectionExtensions.cs
    ├── FormLocalizationResolver.cs
    └── FormLayoutResolver.cs
```

---

## Testing

```powershell
dotnet run --project tests/BlazorWebForms.Core.Tests -c Debug
```

---

## Further Reading

| Document | Description |
|---|---|
| [CORE_API_REFERENCE.md](../../docs/CORE_API_REFERENCE.md) | Complete `FormsApplicationService` API |
| [CORE_MODELS.md](../../docs/CORE_MODELS.md) | Full domain model reference |
| [CORE_EXTENDING.md](../../docs/CORE_EXTENDING.md) | Interface implementations and auth examples |
| [CORE_LOCALIZATION.md](../../docs/CORE_LOCALIZATION.md) | Localization and condition evaluation |
| [QUESTION_TYPES.md](../../docs/QUESTION_TYPES.md) | Every field type documented |
| [Repository README](../../README.md) | Project overview and sample app setup |


---

## Quick Start

### Prerequisites

- .NET 10 SDK
- (Optional) SQL Server or LocalDB

### Package Installation

```powershell
dotnet add package BlazorWebForms.Core --version 1.0.0
```

Only add additional packages if needed:
- `BlazorWebForms.Blazor` — for Blazor UI components
- `BlazorWebForms.Infrastructure.SqlServer` — for SQL persistence

### Service Registration

In your `Program.cs`:

```csharp
using BlazorWebForms.Core.Services;

var builder = WebApplicationBuilder.CreateBuilder(args);

// Register Core services
builder.Services.AddBlazorWebFormsCore();

// Implement the required ICurrentUserContext interface
builder.Services.AddScoped<ICurrentUserContext, YourCurrentUserContext>();

var app = builder.Build();
app.Run();
```

### Required: Implement ICurrentUserContext

This is the only required implementation. It tells the service who the current user is:

```csharp
using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;

public sealed class YourCurrentUserContext(IHttpContextAccessor httpContextAccessor) 
	: ICurrentUserContext
{
	public UserProfile GetCurrentUser()
	{
		var principal = httpContextAccessor.HttpContext?.User;
		if (principal?.Identity is null || !principal.Identity.IsAuthenticated)
		{
			return new UserProfile { IsAuthenticated = false };
		}

		return new UserProfile
		{
			UserId = Guid.Parse(principal.FindFirst("sub")?.Value ?? Guid.NewGuid().ToString()),
			IsAuthenticated = true,
			DisplayName = principal.FindFirst("name")?.Value ?? "User",
			Email = principal.FindFirst("email")?.Value ?? string.Empty,
			Roles = new() { FormPermissionRole.Submitter }
		};
	}
}
```

See [CORE_EXTENDING.md](../../docs/CORE_EXTENDING.md) for detailed implementation examples (cookie auth, OIDC, Azure AD).

### Using FormsApplicationService

```csharp
public sealed class MyComponent : ComponentBase
{
	[Inject]
	private FormsApplicationService FormsService { get; set; } = null!;

	private async Task SubmitForm()
	{
		var entry = await FormsService.SubmitEntryAsync(formId, new SubmitEntryRequest
		{
			Answers = new() { ["name"] = "John Doe" },
			Approvers = new() { new ApproverInput { Name = "Jane", Email = "jane@example.com" } },
			Files = new()
		});

		Console.WriteLine($"Entry created: {entry.Id}");
	}
}
```

---

## Package Contents

```
BlazorWebForms.Core/
├── Abstractions/
│   ├── Contracts.cs              # Service interfaces (IFormsRepository, IFileStorage, etc.)
│   └── ICoreMetadataCache.cs     # Caching interface
├── Models/
│   ├── DomainModels.cs           # FormAggregate, EntryRecord, etc.
│   ├── Enums.cs                  # FormFieldKind, EntryStatus, FormAccessMode, etc.
│   ├── FormDefinitionModels.cs   # FormDefinition, FormSectionDefinition, etc.
│   └── ViewModels.cs             # DashboardViewModel, PublishedFormViewModel, etc.
└── Services/
	├── FormsApplicationService.cs # Main entry point for all operations
	├── DefaultImplementations.cs  # Built-in IPermissionEvaluator, IConditionEvaluator
	├── FormLocalizationResolver.cs
	├── FormLayoutResolver.cs
	├── ConditionEvaluationHelper.cs
	├── InMemoryCoreMetadataCache.cs
	├── BlazorWebFormsServiceCollectionExtensions.cs
	└── DemoFormFactory.cs
```

---

## Architecture

### No external dependencies

BlazorWebForms.Core has **zero dependencies** on UI frameworks, databases, or external services. It is pure business logic and domain models.

### Interfaces for extensibility

The package provides interfaces for:
- Permission evaluation (`IPermissionEvaluator`)
- Condition evaluation (`IConditionEvaluator`)
- Form serialization (`IFormDefinitionSerializer`)
- And many more...

Default implementations are provided. Override them by registering your own before calling `AddBlazorWebFormsCore()`.

### Validation & error handling

All business logic errors throw `InvalidOperationException` with descriptive messages. These are intended to be caught and logged by the caller.

**Example error messages**:
- `"Default culture is not a valid culture."`
- `"User does not have permission to manage forms."`
- `"Field 'First name' is required."`

---

## Key APIs

### FormsApplicationService (scoped)

Single entry point for all operations:

```csharp
// Form management
await formsService.GetDashboardAsync();
await formsService.GetBuilderStateAsync(formId);
await formsService.SaveDraftAsync(request);
await formsService.PublishAsync(formId);

// Submissions
await formsService.SubmitEntryAsync(formId, request);
await formsService.SaveDraftSubmissionAsync(formId, request);
await formsService.GetDraftSubmissionAsync(formId);

// Queries
await formsService.QueryEntriesAsync(options);
await formsService.GetEntryDetailAsync(entryId);

// Approval workflow
await formsService.ApproveStepAsync(entryId, stepId, signature);
await formsService.RejectStepAsync(entryId, stepId, reason);

// Files
await formsService.StoreFileAsync(request);
await formsService.OpenEntryFileAsync(entryId, fileId);

// Invitations
await formsService.CreateInvitationAsync(request);
await formsService.AcceptInvitationAsync(token);
```

See [CORE_API_REFERENCE.md](../../docs/CORE_API_REFERENCE.md) for complete method reference.

### Domain Models

```csharp
// Form aggregate
FormAggregate form = await formsService.SaveDraftAsync(...);
form.Name;
form.Publication;
form.DraftDefinition;

// Entry submission
EntryRecord entry = await formsService.SubmitEntryAsync(...);
entry.Status;      // Draft, NeedsApproval, Approved, Rejected
entry.Answers;     // Dictionary<string, string?>
entry.ApprovalSteps;
entry.ApprovalAuditTrail;

// Form definition
FormDefinition definition = form.DraftDefinition;
definition.DefaultCulture;    // e.g., "en-US"
definition.Sections;          // FormSectionDefinition[]
definition.LocalizedTitles;   // Culture-specific titles
definition.Branding;          // Logo, colors, hero text, etc.
```

See [CORE_MODELS.md](../../docs/CORE_MODELS.md) for complete model reference.

---

## Localization & Validation

### Localization

Every form definition carries localization dictionaries:

```csharp
definition.Title = "Travel Request";
definition.LocalizedTitles["fr-FR"] = "Demande de voyage";

section.Title = "Trip Details";
section.LocalizedTitles["fr-FR"] = "Détails du voyage";

field.Label = "Destination";
field.LocalizedLabels["fr-FR"] = "Destination";
```

Culture resolution uses fallback: requested culture → language → default culture → base text.

### Condition Evaluation

Fields and sections can be conditionally shown/hidden based on other answers:

```csharp
field.VisibilityRules = new VisibilityConditionDefinition
{
	Join = VisibilityJoinOperator.And,
	Rules = new()
	{
		new VisibilityRuleDefinition
		{
			FieldId = "employment-status",
			Operator = VisibilityRuleOperator.Equals,
			Value = "employed"
		}
	}
};
```

Injected `IConditionEvaluator` evaluates rules at render time.

### Validation

All definitions are validated during save/publish:
- Cultures must be valid (e.g., `"en-US"`, not `"invalid-culture"`)
- Form must have at least one section
- Fields must have unique ids within a section
- Visibility conditions must reference existing fields
- Required fields must have non-null answers at submit time

---

## Testing

Run the test suite:

```powershell
# Core tests (executable test program)
dotnet run --project tests/BlazorWebForms.Core.Tests/BlazorWebForms.Core.Tests.csproj -c Debug
```

Test assertions cover:
- Form definition serialization/deserialization
- Culture validation
- Visibility condition evaluation
- Approval workflow state transitions
- Permission checks
- File uploads and constraints
- Email notifications
- Invitation lifecycle
- Large-form performance
- Localization fallback

---

## Performance Notes

- Forms are cached in-memory per user (short-lived, cleared on mutation)
- Entries are paged (default 50, max 1000 per query)
- Visibility evaluation is O(rules) — runs at render time, not expensive
- Condition/permission checks do not hit the database in the Core package

---

## Security Notes

- Cultures are validated against `CultureInfo.GetCultureInfo()` to prevent injection
- All permission checks are performed in `IPermissionEvaluator` — override for custom logic
- File uploads are constrained by size, extension, and MIME type
- All timestamps are in UTC
- Sensitive data (passwords, tokens) are never logged in validation errors

---

## Related Projects

- **BlazorWebForms.Blazor** — Razor components (FormBuilderWorkspace, PublishedFormView, FormsAdminDashboard, DynamicFormRenderer)
- **BlazorWebForms.Infrastructure.SqlServer** — EF Core persistence, email integration, PDF export, file storage
- **BlazorWebForms.SampleApp** — Full working application demonstrating Core, Blazor, and Infrastructure
