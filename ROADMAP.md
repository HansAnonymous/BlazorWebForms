# BlazorWebForms Roadmap

BlazorWebForms is a dynamic forms package for .NET 10 Blazor Server (Interactive Server) targeting SQL Server/Azure SQL.

This repository currently contains a working V1 scaffold: domain model + service layer, a SQL-oriented infrastructure layer (with a real schema script but demo persistence), reusable Blazor UI components, and a sample app that demonstrates the end-to-end flow.

## Status Today (Scaffold Implemented)

### What You Can Do Now
- Run the sample app and click through a minimal experience:
  - Builder: create/update a draft, add sections/fields, save, publish
  - Published form: render from the latest published version and submit an entry
  - Entries: view entry details including revision history and approval steps
  - Admin: see forms, see entries, and search by indexed fields
- Publish creates an immutable `FormVersion` snapshot (JSON) and new submissions link to that exact version.
- Submissions create immutable revision history (edits append revisions, original snapshot preserved).
- Basic sequential approval steps exist (status tracking + signature string capture).
- File uploads are supported as a demo (stored locally; entry captures filename today).
- PDF export and email notifications exist as replaceable service contracts with stub implementations.

### Where It Lives in Code
- Core contracts + domain/service layer: `src/BlazorWebForms.Core`
- Infrastructure (SQL schema script + demo repository + local file store): `src/BlazorWebForms.Infrastructure.SqlServer`
- Reusable Blazor components (package UI): `src/BlazorWebForms.Blazor`
- Sample app (consumer example): `src/BlazorWebForms.SampleApp`

### Sample App Routes
- `/` overview
- `/admin` admin dashboard (forms + entries + search)
- `/builder` create/edit builder workspace
- `/builder/{formId}` edit an existing form draft
- `/forms/{slug}` view published form and submit
- `/entries/{entryId}` view entry detail

### Current Storage Reality (Important)
The infrastructure project includes a SQL Server schema creation script, but the running sample app currently uses an in-memory repository to keep the scaffold runnable without external dependencies. Moving to EF Core + real SQL persistence is a primary next milestone.

## Roadmap (What’s Next)

### Milestone 1: Production-Grade Persistence (SQL Server / Azure SQL)
- Implement EF Core model + migrations for:
  - Forms, form versions, publications
  - Entries, entry revisions
  - Approval steps
  - Permissions, notifications
  - Files metadata
- Store `FormVersion.DefinitionJson` and `Entry.AnswersJson` in SQL (NVARCHAR(MAX) or JSON-capable column strategy).
- Add queryable search:
  - Keep “searchable fields” index strategy (store extracted key/value pairs for filtering)
  - Add indexes and query patterns for common admin searches
- Add a “historical render” path: entry detail renders using the exact form version used at submission.

### Milestone 2: Real Auth + Permissions
- Integrate with ASP.NET Core authentication/authorization (host-provided identity).
- Define and enforce authorization rules for:
  - Form owner/manager: builder, publish, admin
  - Approver: approve steps
  - Submitter/self viewer: view own submissions (opt-in by form policy)
- Implement invitation flows (email-based) as an optional module.

### Milestone 3: Builder UX + Schema Completeness
- Expand builder features:
  - Field editing (placeholder/help/regex/options), reorder, delete
  - Conditional visibility editor (rule builder instead of raw `field=value`)
  - Section-level conditions and advanced layout
  - Localization editing for labels/help/options
  - Branding: logo, images, theme palette
- Formalize schema versioning for `FormDefinition` JSON (forward-compatible migrations).

### Milestone 4: Submissions, Edits, and Workflow
- Make “edit after submit” policy configurable per form (immutable revisions vs. hard overwrite mode).
- Add draft submissions (save progress without submission).
- Add richer approval workflow:
  - Approver notifications and reminders
  - Approval audit trail (who/when/what signed)
  - Rejection and resubmission flow

### Milestone 5: Files, PDF, Notifications (Real Implementations)
- File upload: store bytes + metadata; support multiple files; per-field constraints.
- PDF export: real PDF generation (replace stub text exporter).
- Email notifications: real email provider integration (replace in-memory notifier).

### Milestone 6: Accessibility, i18n, and Polish
- WCAG 2.1 AA review:
  - Labels/ARIA, keyboard navigation, focus management, error summaries
  - Color contrast and responsive behaviors
- Internationalization end-to-end:
  - Culture switching, localized validation messages, localized form content
- Performance work:
  - Large forms, virtualization strategies, caching

### Optional: UI Framework Adapters
- Add an optional MudBlazor adapter package (keep core UI dependency-light).

## Non-Goals (In the Current Scaffold)
- Full EF Core persistence is not implemented yet.
- External e-signature compliance workflows are not implemented (current “signature” is simple capture).
- Multitenant custom domain provisioning automation is not implemented (domain is metadata today).
- Full-text search across all fields/files is not implemented (current strategy is “searchable fields” index).

## Try It Locally
Build and run:

```powershell
$env:DOTNET_CLI_HOME='D:\\Coding\\BlazorWebForms\\.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
dotnet run --project src/BlazorWebForms.SampleApp/BlazorWebForms.SampleApp.csproj -m:1 /p:BuildInParallel=false /p:RestoreDisableParallel=true
```

Quick verification tests (offline):

```powershell
dotnet run --project tests/BlazorWebForms.Core.Tests/BlazorWebForms.Core.Tests.csproj -m:1 /p:BuildInParallel=false /p:RestoreDisableParallel=true
dotnet run --project tests/BlazorWebForms.Infrastructure.SqlServer.Tests/BlazorWebForms.Infrastructure.SqlServer.Tests.csproj -m:1 /p:BuildInParallel=false /p:RestoreDisableParallel=true
```

