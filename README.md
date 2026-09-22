# BlazorWebForms

**Structured form collection for Blazor applications.**  
Define forms as data, collect submissions, run approval submission workflows, and export results.

[![License: AGPL-3.0](https://img.shields.io/badge/license-AGPL--3.0-blue)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-purple)](https://dotnet.microsoft.com)
[![NuGet](https://img.shields.io/nuget/v/BlazorWebForms.Core)](https://www.nuget.org/packages/BlazorWebForms.Core)
[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/HansAnonymous/BlazorWebForms)

---

## What is BlazorWebForms?

BlazorWebForms is a library that lets you define forms as structured data (`FormDefinition`), render them dynamically, and collect submissions with full workflow support.

---

## Packages

| Package | Purpose                                                                                              |
|---|------------------------------------------------------------------------------------------------------|
| [`BlazorWebForms.Core`](src/BlazorWebForms.Core/) | Domain models, `FormsApplicationService`, all interfaces                                             |
| [`BlazorWebForms.Infrastructure.SqlServer`](src/BlazorWebForms.Infrastructure.SqlServer/) | EF Core persistence (SQL Server/Azure SQL), file storage, email (SMTP, SendGrid, Graph), data export |

---

## Architecture

```
Your Blazor App
      │
      ▼
FormsApplicationService          ← single entry point for all operations
      │
      ├── IFormsRepository       ← persistence (implemented by Infrastructure.SqlServer)
      ├── IFileStorage           ← file storage (local disk or bring your own)
      ├── IEmailNotifier         ← email (SMTP / SendGrid / Graph / no-op)
      ├── IPermissionEvaluator   ← role-based access (override for custom rules)
      ├── ICurrentUserContext    ← who is logged in (you implement this)
      └── ICustomFieldHandler[]  ← optional: register your own field types
```

Core has **no dependency** on any database, email provider, or UI framework. Every integration point is an interface you can swap.

---

## Quick Start

### 1. Install packages

```powershell
dotnet add package BlazorWebForms.Core
dotnet add package BlazorWebForms.Infrastructure.SqlServer
```

### 2. Register services

```csharp
// Program.cs
builder.Services.AddBlazorWebFormsCore();

builder.Services.AddBlazorWebFormsSqlServer(opt =>
{
    opt.ConnectionString = builder.Configuration.GetConnectionString("BlazorWebForms")!;
    opt.StorageRoot = Path.Combine(builder.Environment.ContentRootPath, "uploads");
});

// Required: tell the service who the current user is
builder.Services.AddScoped<ICurrentUserContext, YourCurrentUserContext>();
```

### 3. Apply database schema

On first run, call `SeedAsync` to create tables automatically:

```csharp
await app.Services.GetRequiredService<FormsApplicationService>().SeedAsync();
```

Or generate a migration script for review before applying:

```powershell
dotnet ef migrations script \
  --project src/BlazorWebForms.Infrastructure.SqlServer \
  --startup-project src/BlazorWebForms.SampleApp
```

### 4. Create and publish a form

```csharp
var draft = await formsService.SaveDraftAsync(new SaveDraftRequest
{
    Name = "Travel Request",
    Slug = "travel-request",
    Definition = new FormDefinition
    {
        DefaultCulture = "en-US",
        Sections =
        [
            new FormSectionDefinition
            {
                Title = "Trip Details",
                Fields =
                [
                    new FormFieldDefinition { Id = "destination", Label = "Destination", Kind = FormFieldKind.Text, Required = true },
                    new FormFieldDefinition { Id = "depart-date", Label = "Departure date", Kind = FormFieldKind.Date, Required = true },
                    new FormFieldDefinition { Id = "reason",      Label = "Reason",         Kind = FormFieldKind.TextArea }
                ]
            }
        ]
    }
});

await formsService.PublishAsync(draft.Id);
```

### 5. Accept a submission

```csharp
var entry = await formsService.SubmitEntryAsync(formId, new SubmitEntryRequest
{
    Answers = new() { ["destination"] = "Paris", ["depart-date"] = "2026-09-01" },
    Approvers = [new ApproverInput { Id = "E-1042", DisplayName = "Jane Smith", Email = "jane@example.com" }]
});
```

---

## Feature Overview

### Forms
- **15 built-in field types**: Text, TextArea, Number, Select, Radio, Checkbox, Date, File, RichText, Signature, RepeatableList, RankedChoice, MatrixSingle, MatrixMulti, and `Custom` (bring your own)
- **Visibility conditions**: Show/hide sections and fields using AND/OR rules against other field values
- **Localization**: Per-field/section/option localized labels with culture fallback chain
- **Branding**: Logo, hero image, accent color, button radius, surface color per form
- **Metadata bags**: Attach arbitrary `Dictionary<string, string>` to forms, sections, and fields
- **Schema versioning**: Definitions carry a `SchemaVersion` for forward-compatibility handling

### Submissions
- **Draft flow**: save, resume, and finalize multi-session drafts
- **Immutable revisions or overwrite-latest**: Configurable per form
- **File uploads**: Per-field constraints (size, MIME type, extension, file count)
- **Search indexing**: Mark fields as `Searchable`; text index is maintained on save

### Approvals
- **Sequential multi-step approval**: Assigned approvers, ordered
- **Approve / reject / resubmit**: Full state machine with server-side invariant enforcement
- **Audit trail**: Every action is appended to an immutable `ApprovalAuditEvent` log
- **Approver identity**: `Id` (employee ID), `DisplayName`, and `Email` on each step
- **Email notifications**: Assignment, reminder, approval, rejection, and resubmit events

### Infrastructure (SqlServer package)
- **EF Core 10**: Persistence with SQL Server / Azure SQL
- **Idempotent schema bootstrap**: `SeedAsync` applies column additions without a full migration runner
- **Local file storage**: Safe filename normalization, SHA-256 hash, signed short-lived download links
- **Text PDF export**: Structured entry export with configurable row/size limits
- **Email strategies**: `DryRun` (default), `Smtp`, `SendGrid`, `Graph` (Microsoft 365)
- **Anti-abuse guards**: Per-minute upload and notification rate limits
- **Draft cleanup**: Background removal of stale drafts and orphaned files (configurable retention)
- **Operational telemetry**: Upload/PDF/email counters via `IOperationalTelemetry`

### Extensibility
- Register custom field types with `AddCustomFieldHandler<T>()`
- Override any default implementation (`IPermissionEvaluator`, `IConditionEvaluator`, `IFileStorage`, …)
- Plug in prefill providers (`IFormPrefillProvider`) for claim-based, employee-based, or custom data

---

## Documentation

| Document | Description                                                |
|---|------------------------------------------------------------|
| [docs/USING_BlazorWebForms.md](docs/USING_BlazorWebForms.md) | Integration guide: NuGet, local feed, ProjectReference     |
| [docs/CORE_MODELS.md](docs/CORE_MODELS.md) | Complete domain model reference                            |
| [docs/CORE_API_REFERENCE.md](docs/CORE_API_REFERENCE.md) | Full `FormsApplicationService` API                         |
| [docs/CORE_EXTENDING.md](docs/CORE_EXTENDING.md) | Implementing interfaces, auth examples, custom field types |
| [docs/CORE_LOCALIZATION.md](docs/CORE_LOCALIZATION.md) | Localization, culture resolution, condition evaluation     |
| [docs/QUESTION_TYPES.md](docs/QUESTION_TYPES.md) | Every field kind with properties, validation, and examples |
| [src/BlazorWebForms.Core/README.md](src/BlazorWebForms.Core/README.md) | Core package reference                                     |
| [src/BlazorWebForms.Infrastructure.SqlServer/README.md](src/BlazorWebForms.Infrastructure.SqlServer/README.md) | SQL Server package reference                               |

---

## Running Tests

```powershell
# Core isolated unit tests
dotnet run --project tests/BlazorWebForms.Core.Tests -c Debug

# SQL Server integration tests (requires LocalDB)
dotnet run --project tests/BlazorWebForms.Infrastructure.SqlServer.Tests -c Debug
```

---

## License

[AGPL-3.0-only](LICENSE). Commercial licensing inquiries: open an issue.

