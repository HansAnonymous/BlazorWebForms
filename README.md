# BlazorWebForms — Run & Dev Guide

Quick guide: run sample app with SQL persistence and apply EF migrations.

Developer docs for using and iterating from another solution: [docs/USING_BlazorWebForms.md](docs/USING_BlazorWebForms.md)

Prereqs
- .NET 8/10 SDK installed
- LocalDB (SQL Server Express LocalDB) or SQL Server accessible

Install EF CLI (if not already):

```powershell
dotnet tool install --global dotnet-ef
```

Restore and build:

```powershell
dotnet restore
dotnet build -c Debug
```

Apply migrations (creates database):

```powershell
dotnet ef database update --project src/BlazorWebForms.Infrastructure.SqlServer/BlazorWebForms.Infrastructure.SqlServer.csproj --startup-project src/BlazorWebForms.SampleApp/BlazorWebForms.SampleApp.csproj
```

Run sample app:

```powershell
dotnet run --project src/BlazorWebForms.SampleApp/BlazorWebForms.SampleApp.csproj -c Debug
```

Configuration
- Development connection string: `src/BlazorWebForms.SampleApp/appsettings.Development.json` → `ConnectionStrings:BlazorWebForms`.
- Optional schema override: `BlazorWebFormsSqlServer:SchemaName` in same file.
- Storage root defaults to `<SampleApp content root>/App_Data/uploads` but can be set in `Program.cs` or via options in `AddBlazorWebFormsSqlServer`.
- Development auth: sample app now uses cookie authentication. Use the top-right "Dev login" controls in the app shell to sign in as `Manager`, `Owner`, `Approver`, or `Admin`.
- Auth assumptions: stable user id is derived from NameIdentifier/sub/oid claim (or deterministic fallback), display name from `name`/identity name, email from `email` claim.
- Unauthorized route handling: protected routes now show explicit `Sign in required` for anonymous users and `Access denied` for authenticated users without required roles.
- Invitation endpoints (optional module):
  - `POST /invitations/create` (authorized: Admin/Owner/Manager)
  - `POST /invitations/accept` (authorized: authenticated user)
  - `POST /invitations/revoke/{invitationId}` (authorized: Admin/Owner/Manager)
- Invitation notifications: invitation create/accept/revoke events now flow through `IEmailNotifier` integration points.
- Email adapter template values are provided in `src/BlazorWebForms.SampleApp/appsettings.Development.json` under `BlazorWebFormsSqlServer`.

Builder + schema completeness (Milestone 3)
- `FormDefinition` now includes explicit `SchemaVersion` with compatibility handling for legacy payloads.
- Builder supports structured conditional rules (AND/OR + equals/not-equals/contains/empty), section/field layout metadata, localization maps, and branding token configuration.
- Published renderer applies localization fallback (`requested culture` -> `language` -> `default culture` -> `base text`) and branding tokens/assets with safe defaults.
- Save/publish validation gates enforce schema integrity, condition references, localization culture/value shape, and branding asset/style sanitization.

Submissions + workflow completeness (Milestone 4)
- Per-form edit policy supports immutable revision mode and overwrite-latest mode (enforced in service layer).
- Draft submissions are supported with save/resume/finalize flow (draft-aware submit path).
- Approval workflow supports approve/reject/resubmit with guarded next-step transitions and server-side invariant enforcement.
- Approval notifications expose assignment/reminder hooks with idempotency keys; local memory notifier remains default implementation.
- Approval audit trail is persisted append-only and shown in entry detail/admin filtering views.

Files hardening progress (Milestone 5 in progress)
- File fields now support optional constraints (`MaxFileSizeBytes`, `MaxFileCount`, `AllowedMimeTypes`, `AllowedExtensions`).
- Published form renderer enforces size/extension/MIME constraints before storage and displays active constraints in UI help text.
- Published form renderer now supports multiple file selection per field when `MaxFileCount > 1`.
- Local file storage now enforces environment toggle + size limits, normalizes unsafe file names, and uses generated storage keys.
- Local file storage also validates extension and MIME allow-lists when provided.
- Entry-file downloads now use service-layer authorization checks plus short-lived signed links (`/entry-files/{entryId}/{fileId}?token=...`).
- File metadata now persists hash (`Sha256`), uploader identity (`UploadedByUserId`, `UploadedByEmail`), and revision linkage (`RevisionNumber`).
- Stale-draft cleanup flow is available via service API (`CleanupStaleDraftFilesAsync`) to remove orphaned bytes and old draft rows.
- Retention hooks are configurable via SQL options (`EnableDraftCleanup`, `DraftRetentionPeriod`).

PDF export progress (Milestone 5 in progress)
- Entry PDF export is now exposed via service API (`ExportEntryPdfAsync`) and sample endpoint `GET /entry-pdf/{entryId}`.
- Export payload includes form metadata, localized section/field answers, file metadata, approval steps, and approval audit entries.
- Export safeguards are configurable through SQL options (`PdfMaxAnswerRows`, `PdfMaxAuditRows`, `PdfMaxBytes`).
- Historical PDF export binds to the submitted `FormVersionId` context for entry rendering.

Email notification progress (Milestone 5 in progress)
- Provider-style notifier (`TemplateEmailNotifier`) is now default SQL registration with template-based message payloads.
- Workflow notifications now include submit, approver assignment/reminder, approval, rejection, and invitation lifecycle events.
- Retry/backoff and idempotency de-duplication are built into notifier flow (`EmailRetryCount`, `EmailRetryDelayMs`, idempotency key tracking).
- Outbound email can be disabled by environment via `EnableOutboundEmail` (sample app disables in non-production).
- Email provider strategy now supports `DryRun`, `Smtp`, `SendGrid`, and `Graph` through the shared integration interface (`IEmailIntegration`).
- Strategy and provider settings are configurable under `BlazorWebFormsSqlServer:*` (SMTP host/port/auth, SendGrid API key, Graph sender/token).

Security/compliance baseline progress (Milestone 5 in progress)
- Assigned approvers are authorized to view entry details/files through service-layer access checks.
- Anti-abuse quota hooks are active for uploads and outbound notifications via `IAntiAbuseGuard` (`MaxUploadsPerMinute`, `MaxNotificationsPerMinute`).
- Draft cleanup retention is configurable with `DraftRetentionPeriod` and feature toggle `EnableDraftCleanup`.
- Graph correlation IDs are captured in approval audit events for external-operation traceability.
- Notification template/log payloads avoid direct entry-id leakage in outbound body text.
- Compliance region note hook is available via `DataResidenceRegion` (set per environment).

UX/observability progress (Milestone 6 implemented)
- File upload controls now show in-flight upload state and field-level failure messages.
- Entry detail page now shows PDF generation state and status messaging.
- Admin dashboards now include per-entry notification delivery status indicators.
- Operational telemetry hooks are available for upload/PDF/email counters via `IOperationalTelemetry` (default in-memory implementation).

Accessibility remediation progress (Milestone 6 implemented)
- Dynamic form renderers (package + sample) now include stronger semantic/ARIA wiring (`aria-required`, `aria-invalid`, `aria-describedby`) with explicit label ids and grouped radio semantics.
- Upload failures now render accessible error summaries linked to field anchors and move focus to the summary after async upload failures.
- Sample shell now includes a keyboard skip link and a focusable main landmark target for faster keyboard navigation.
- Async status text for loading/PDF/file-attachment feedback now uses polite live regions where applicable.
- Shared CSS now includes focus-visible outlines and visually-hidden utility classes for accessibility support.

Visual accessibility/responsiveness progress (Milestone 6 implemented)
- Navigation and interactive controls now use larger minimum touch-target sizing across app shell/forms/actions (44px baseline).
- Contrast for key nav states and link hover/readability was tightened in sidebar/theme styles.
- Admin entries tables now render in horizontal scroll regions with keyboard-focusable wrappers for smaller viewports.
- Mobile action groups now stack full-width buttons for better spacing and tap reliability on input-heavy routes.
- Non-text decorative elements now have companion text alternatives where needed in shell/navigation.

Internationalization progress (Milestone 6 implemented)
- Request localization is enabled in the sample pipeline with supported cultures (`en-US`, `fr-FR`, `es-ES`, `ar-SA`) and query/cookie/Accept-Language providers.
- App shell now exposes culture switching and persists culture via request-localization cookie endpoint (`/culture/set`).
- Core sample UI strings are localized through a shared app localizer with deterministic fallback (`specific culture` -> `language` -> `en` -> `key`).
- Published/sample dynamic renderer now resolves localized section/field/option content consistently at render time.
- App root now emits culture-aware `lang` and RTL-aware `dir`; key date rendering now formats using current culture.

Performance/scalability progress (Milestone 6 implemented)
- Entry query options now support server-side paging primitives (`Offset`, `Limit`) across in-memory and SQL repository implementations.
- Admin entry dashboards (sample + package) now use paged loading with next/previous controls to avoid loading large datasets in one render.
- Core service now normalizes query limits (default/bounded) to reduce unbounded entry fetches in common admin paths.
- Added short-lived metadata caching for frequently-read views (dashboard and published-form-by-slug) with invalidation on form/entry/invitation mutations.
- Added regression assertions in both core and SQL executable tests to validate paging-limit behavior.

Reliability/UX refinement progress (Milestone 6 implemented)
- Builder and published-form sample views now include standardized loading, recoverable-error, and retry states for data-load failures.
- Long-running save/publish/submit actions now include timeout guardrails and explicit retry guidance messaging.
- Published-form submit flow now supports retrying the most recent failed submission attempt without re-entering form data.
- Status messaging is now consistently exposed through live/status-friendly patterns in key interactive routes.
- Operational telemetry now captures failure-context counters (`area`, `operation`, `reason`) in addition to success/failure aggregates.

Tests/tooling progress (Milestone 6 implemented)
- Added automated accessibility guard checks in executable tests for key semantics (`aria-describedby`, radiogroup role, skip-link presence).
- Added localization fallback regression checks in executable tests to verify missing-localization fallback behavior.
- Added performance regression checks for large-form/paged-query scenarios (bounded result windows with timing threshold assertions).
- Added responsive-layout guard checks in executable tests for mobile breakpoint/media-query presence and stacked-action behavior tokens.
- Existing core and SQL executable test commands remain the single-command regression path for milestone validation.

Signed download token configuration
- Configure `BlazorWebForms:FileDownloadTokenSecret` for non-development environments.
- Default in-app fallback secret is for local development only and should be overridden before production use.

Migrations
- Migration files live in `src/BlazorWebForms.Infrastructure.SqlServer/Migrations/`.
- Generate and review SQL script before apply:

```powershell
dotnet ef migrations script --project src/BlazorWebForms.Infrastructure.SqlServer/BlazorWebForms.Infrastructure.SqlServer.csproj --startup-project src/BlazorWebForms.SampleApp/BlazorWebForms.SampleApp.csproj
```

- SQL compatibility note: current migrations use SQL Server/Azure SQL compatible types (`uniqueidentifier`, `nvarchar`, `datetimeoffset`, `rowversion`) and are validated in local integration runs.

Tests

```powershell
dotnet run --project tests/BlazorWebForms.Infrastructure.SqlServer.Tests/BlazorWebForms.Infrastructure.SqlServer.Tests.csproj -c Debug
```

Notes
- Sample app `Program.cs` binds `ConnectionStrings:BlazorWebForms` automatically when present.
- If using a remote SQL Server, update connection string accordingly and ensure firewall access.
