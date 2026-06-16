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
Milestone 1 is now implemented with EF Core + SQL persistence, migrations, queryable search indexing, historical entry rendering by submitted form version, file metadata persistence, and baseline concurrency/indexing safeguards.

### Current Auth Reality (Important)
Milestone 2 is now implemented with ASP.NET Core cookie authentication, claims-based current-user mapping, route + policy guards, service-layer authorization checks, invitation create/accept/revoke flow (with anti-abuse protections), and not-authorized UX states for sign-in-required vs access-denied behavior.

## Roadmap (What’s Next)

### Milestone 1: Production-Grade Persistence (SQL Server / Azure SQL)
- Detailed execution checklist: `milestones/Milestone 1.md`
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

### Milestone 2: Real Auth + Permissions (Implemented)
- Detailed execution checklist: `milestones/Milestone 2.md`
- Integrate with ASP.NET Core authentication/authorization (host-provided identity).
- Define and enforce authorization rules for:
  - Form owner/manager: builder, publish, admin
  - Approver: approve steps
  - Submitter/self viewer: view own submissions (opt-in by form policy)
- Implement invitation flows (email-based) as an optional module.

### Milestone 3: Builder UX + Schema Completeness
- Detailed execution checklist: `milestones/Milestone 3.md`
- Expand builder features:
  - Field editing (placeholder/help/regex/options), reorder, delete
  - Conditional visibility editor (rule builder instead of raw `field=value`)
  - Section-level conditions and advanced layout
  - Localization editing for labels/help/options
  - Branding: logo, images, theme palette
- Formalize schema versioning for `FormDefinition` JSON (forward-compatible migrations).

### Milestone 3 Reality (Implemented)
- Builder now supports complete field metadata editing, reorder/delete safeguards, and structured condition authoring.
- Section/field layout metadata is supported with renderer fallback behavior.
- Localization editing and fallback resolution are implemented for form/section/field/option content.
- Branding configuration (assets + palette/style tokens) is implemented with sanitization and safe defaults in published rendering.
- `FormDefinition` versioning is explicit and validated with compatibility handling for legacy definitions.

### Milestone 4: Submissions, Edits, and Workflow
- Detailed execution checklist: `milestones/Milestone 4.md`
- Make “edit after submit” policy configurable per form (immutable revisions vs. hard overwrite mode).
- Add draft submissions (save progress without submission).
- Add richer approval workflow:
  - Approver notifications and reminders
  - Approval audit trail (who/when/what signed)
  - Rejection and resubmission flow

### Milestone 4 Reality (Implemented)
- Edit-after-submit policy is configurable per form and enforced (`ImmutableRevisions` / `OverwriteLatest`).
- Draft submissions are supported with save/resume/finalize behavior.
- Approval workflow now includes guarded approve/reject/resubmit transitions with invariant checks.
- Notification hooks for assignment/reminders are wired with idempotency keys and provider abstraction retained.
- Approval audit trail is persisted append-only and visible in entry/admin experiences.

### Milestone 5: Files, PDF, Notifications (Real Implementations)
- Detailed execution checklist: `milestones/Milestone 5.md`
- File upload: store bytes + metadata; support multiple files; per-field constraints.
- PDF export: real PDF generation (replace stub text exporter).
- Email notifications: real email provider integration (replace in-memory notifier).

### Milestone 5 Reality (Implemented)
- Upload hardening slice completed: per-field file constraints were added to form schema and enforced in UI + storage path.
- Multi-file-per-field submit flow is implemented for published renderer + service persistence path.
- Local storage now applies safer generated file keys, normalized original names/content types, configurable max-size limits, and extension/MIME allow-lists.
- Authorized file download baseline is implemented with service authorization + short-lived signed URL endpoint in sample app.
- File metadata + lifecycle baseline is implemented: hash/uploader/revision metadata persisted and stale-draft cleanup flow added.
- Retention policy hook baseline is implemented via configurable draft-retention cleanup options/service.
- PDF export baseline is implemented with submitted-version context, structured payload sections, and configurable output safeguards.
- Email notification baseline is implemented with provider-style templates, retry/backoff, idempotency guards, and environment toggle.
- Section 5 baseline has started: approver file/detail access hardened, anti-abuse quota hooks added, and Graph correlation IDs persisted in audit trail.
- Section 5 baseline is now complete (authorization, anti-abuse hooks, metadata-safety/logging, and compliance-region hook).
- Section 6 baseline is now in progress with upload/PDF UX status messaging, admin notification indicators, and operational telemetry hooks.
- Constraint failure paths are covered in SQL infrastructure tests (invalid extension/MIME/size).
- External email provider adapters are now available (SMTP/SendGrid/Graph) behind a shared integration interface with strategy routing.

### Milestone 6: Accessibility, i18n, and Polish
- Detailed execution checklist: `milestones/Milestone 6.md`
- WCAG 2.1 AA review:
  - Labels/ARIA, keyboard navigation, focus management, error summaries
  - Color contrast and responsive behaviors
- Internationalization end-to-end:
  - Culture switching, localized validation messages, localized form content
- Performance work:
  - Large forms, virtualization strategies, caching

### Milestone 6 Reality (Implemented)
- Section 1 accessibility remediation baseline is implemented for dynamic forms and sample shell/routes.
- Renderer inputs now include linked labels/help/error semantics and ARIA invalid/required/described-by attributes.
- Upload failure handling now includes focusable error summary patterns linked to field anchors.
- Keyboard navigation is improved with skip-link support and focusable main-content landmark in sample layout.
- Async loading/status feedback in key pages now uses live-region semantics for assistive technologies.
- Section 2 visual accessibility baseline is implemented with improved control target sizes, responsive action stacking, and stronger nav/readability contrast.
- Admin table views now support keyboard-focusable horizontal scrolling containers for mobile/tablet layouts.
- Non-text iconography in sample navigation now includes textual alternatives.
- Section 3 i18n baseline is implemented with request localization, culture switching, localized sample UI strings, and fallback behavior.
- Sample app now sets culture-aware `lang`/RTL `dir` in root markup and uses culture-aware date formatting in core views.
- Section 4 performance baseline is implemented with paged admin queries, bounded query defaults, and metadata caching for high-frequency reads.
- Repository query implementations now support offset/limit to reduce large-list materialization in admin workflows.
- Section 4 regression checks are in place in executable tests for query paging behavior.
- Section 5 reliability/UX baseline is implemented with standardized loading/recoverable error states and retry actions for builder/published flows.
- Long-running operations now include timeout guardrails and user guidance for retry paths.
- Telemetry now records contextualized failure counters to improve triage of user-impacting issues.
- Section 6 test/tooling baseline is implemented with executable checks for accessibility semantics, localization fallback, responsive breakpoint tokens, and performance paging thresholds.

### Optional: UI Framework Adapters
- Add an optional MudBlazor adapter package (keep core UI dependency-light).

## Non-Goals (In the Current Scaffold)
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
