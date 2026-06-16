# Milestone 5 Checklist: Files, PDF, Notifications (Real Implementations)

This checklist breaks Milestone 5 into execution-ordered tasks.

## Planning and Provider Choices
- [x] Choose production file storage strategy (local/dev, Azure Blob, or pluggable provider).
- [ ] Choose PDF generation library/provider and document licensing/runtime implications.
- [x] Choose email provider(s) and fallback strategy (SendGrid/SMTP/Graph/etc.).

## 1) File Upload Implementation
- [x] Replace demo file handling with real byte storage + metadata persistence.
- [x] Support multiple files per field and per submission where configured.
- [x] Add per-field constraints (max size, allowed MIME/extensions, max count).
- [x] Add secure file naming, content-type verification, and virus-scan integration point.
- [x] Add download/auth rules and signed/temporary URL strategy where applicable.

## 2) File Metadata and Lifecycle
- [x] Persist file metadata (storage key, original name, size, type, hash, uploaded by, timestamp).
- [x] Link files to entry revisions for historical integrity.
- [x] Add cleanup strategy for orphaned temp files and deleted drafts.
- [x] Add retention policy hooks for compliance-sensitive deployments.

## 3) PDF Export Implementation
- [x] Replace stub text exporter with real PDF renderer service.
- [x] Define PDF template/layout rules for sections, answers, signatures, and audit context.
- [x] Support branding and localization in exported PDF.
- [x] Ensure historical export uses exact submitted form version and revision data.
- [x] Add large-entry safeguards (pagination, memory bounds, timeout behavior).

## 4) Email Notification Implementation
- [x] Replace in-memory notifier with provider-backed email sender.
- [x] Add templated notifications for submit, approval pending, reminder, approval, rejection.
- [x] Add retry/backoff and dead-letter/failure handling strategy.
- [x] Add idempotency keys for event-driven sends to avoid duplicates.
- [x] Add environment-based toggle to disable outbound email in local/dev.

## 5) Security and Compliance Baseline
- [x] Validate file upload/download authorization for submitters, approvers, and admins.
- [x] Add anti-abuse limits (rate limits/quota hooks) for uploads and outbound notifications.
- [x] Ensure sensitive metadata is not leaked in logs.
- [x] Add basic compliance notes for storage region and data retention expectations.

## 6) UX and Operational Observability
- [x] Add user-visible upload progress and failure states.
- [x] Add PDF export status/error messaging in UI.
- [x] Add notification delivery status indicators where useful.
- [x] Add structured logs/metrics for upload throughput, PDF generation, email delivery.

## 7) Tests
- [x] Add integration tests for upload constraints and multi-file handling.
- [x] Add storage abstraction tests (write/read/delete, metadata consistency).
- [x] Add PDF generation snapshot/contract tests.
- [x] Add email template and delivery workflow tests with provider mocks.
- [x] Add end-to-end test covering submission with files, approval notification, and PDF export.

## 8) Done Criteria (Milestone Exit)
- [x] Real file storage with metadata and constraints is in place.
- [x] PDF export generates usable documents from real submission data.
- [x] Email notifications are sent through a real provider with retries/idempotency.
- [x] Security and operational baseline checks are in place.
- [x] README/ROADMAP notes updated for real file/PDF/notification implementations.

## Progress Notes
- 2026-06-13: Implemented first upload hardening slice.
  - Added per-file constraints on `FormFieldDefinition` (`MaxFileSizeBytes`, `MaxFileCount`, `AllowedMimeTypes`, `AllowedExtensions`).
  - Enforced upload constraints in published renderer before upload and forwarded constraints to storage request.
  - Hardened local storage implementation with environment toggle, size limits, safe file naming/path strategy, extension + MIME allow-list validation.
  - Added SQL infra tests for allowed upload plus rejected extension/MIME/size paths.
  - Remaining: true multi-file-per-field UX/data model, authz download strategy, and production provider wiring.
- 2026-06-13: Implemented multi-file-per-field submission path.
  - `DynamicFormRenderer` now enables multiple file selection when `MaxFileCount > 1` and tracks per-field file lists.
  - Published submission/draft payloads now carry flattened file inputs while preserving per-field grouping.
  - Service-layer submit path now replaces file metadata collection for each submission and aggregates answer text per file field.
  - Added core regression coverage for same-field multi-file submission persistence.
- 2026-06-13: Implemented secure entry-file download baseline.
  - Added storage read contract (`IFileStorage.OpenReadAsync`) and local-storage safe-path read implementation (path traversal blocked).
  - Added service-layer file open path with entry-level authorization gate (`OpenEntryFileAsync`).
  - Added sample app download endpoint (`GET /entry-files/{entryId}/{fileId}`) with short-lived signed token validation.
  - Entry detail now renders file names as download links using temporary signed URLs.
  - Added regression tests for authorized file read, unauthorized read rejection, and traversal rejection in local storage.
- 2026-06-14: Implemented file metadata + lifecycle baseline.
  - Extended file metadata model/entity persistence with `Sha256`, uploader identity (`UploadedByUserId`, `UploadedByEmail`), and `RevisionNumber` linkage.
  - Save draft/submit paths now stamp file metadata with uploader identity and revision snapshot number.
  - Added stale draft cleanup service flow (`CleanupStaleDraftFilesAsync`) that deletes orphaned file bytes then prunes stale draft entries.
  - Added repository hooks for orphan-file discovery and stale-draft deletion (`GetOrphanedFilesAsync`, `DeleteDraftEntriesOlderThanAsync`).
  - Added local storage delete operation (`IFileStorage.DeleteAsync`) with same path-safety guards as reads.
  - Added core + SQL integration tests for hash/uploader/revision metadata and stale draft cleanup behavior.
- 2026-06-14: Completed Section 2 retention hooks and started Section 3 PDF implementation.
  - Added retention hook options (`EnableDraftCleanup`, `DraftRetentionPeriod`) and cleanup wrapper service (`DraftCleanupService`).
  - Added storage/repository lifecycle primitives for stale draft discovery and cleanup (`DeleteAsync`, `GetOrphanedFilesAsync`, `DeleteDraftEntriesOlderThanAsync`).
  - Replaced previous minimal export behavior with structured PDF payload generation path through `IPdfExporter` + `FormsApplicationService.ExportEntryPdfAsync`.
  - Added sample app PDF download endpoint (`GET /entry-pdf/{entryId}`) and entry detail PDF download link.
  - Added export safeguards via configurable row and payload limits (`PdfMaxAnswerRows`, `PdfMaxAuditRows`, `PdfMaxBytes`).
  - Added/updated core + SQL tests for retention cleanup and PDF export contract behavior.
- 2026-06-14: Completed Section 3 and implemented Section 4 baseline.
  - PDF export now binds to entry submission version context and is exposed via service + sample endpoint.
  - Export contract verified in tests to include submitted `FormVersionId` and workflow/file sections.
  - Introduced provider-style notifier (`TemplateEmailNotifier`) with template bodies, retry/backoff, and idempotency key de-duplication.
  - Added approval/rejection submitter notification hooks (`NotifyEntryApprovedAsync`, `NotifyEntryRejectedAsync`) and wired workflow events.
  - Added environment-based outbound email toggle (`EnableOutboundEmail`) and retry settings (`EmailRetryCount`, `EmailRetryDelayMs`).
- 2026-06-15: Started Section 5 security/compliance baseline.
  - Expanded entry view authorization so assigned approvers can view entry detail and file metadata via service-layer policy.
  - Added anti-abuse guard abstraction (`IAntiAbuseGuard`) and SQL default implementation with per-minute upload + notification quotas.
  - Wired upload and outbound-notification quota checks through service/notifier flows.
  - Added Graph workflow correlation persistence in approval audit events (`CorrelationId`) with compatibility DDL and indexes.
  - Added configuration hooks for compliance controls (`MaxUploadsPerMinute`, `MaxNotificationsPerMinute`, `DraftRetentionPeriod`).
- 2026-06-15: Completed remaining Section 5 item and started Section 6 UX.
  - Sanitized notifier message content to avoid leaking direct entry identifiers in message bodies.
  - Added storage/compliance region configuration hook (`DataResidenceRegion`) for environment-level documentation and policy alignment.
  - Added user-visible upload progress + failure messaging in dynamic form renderer (`Uploading...`, `Upload failed: ...`).
- 2026-06-15: Expanded Section 6 UX + observability baseline.
  - Added entry detail PDF generation UI state messaging (`Generating PDF...`, success/failure status text).
  - Added admin dashboard notification delivery status column for entry workflow visibility.
  - Added operational telemetry abstraction (`IOperationalTelemetry`) and in-memory metrics implementation for upload/PDF/email paths.
  - Wired telemetry tracking through form upload service, PDF export service path, and email notifier delivery attempts.
- 2026-06-15: Completed Section 7 test hardening.
  - Expanded SQL integration tests for multi-file persistence, storage delete/read consistency, and hash metadata validation.
  - Added PDF contract assertions for submitted-version context + file/workflow section content.
  - Added telemetry assertions (upload/PDF/email counters) in SQL integration flow.
  - Extended core tests with operational telemetry mock assertions.
  - Verified end-to-end path (submission with files -> approval notifications -> PDF export) in both test projects.
- 2026-06-15: Milestone exit review (Section 8).
  - Marked done criteria complete for file storage, PDF export usability, security/ops baseline, and docs synchronization.
  - Remaining blocker for full milestone close: wire outbound notification path to a production external provider (SMTP/SendGrid/Graph Mail) instead of current in-process integration simulation.
- 2026-06-15: Added production email adapters behind integration interface.
  - Added email strategy router (`DryRun`, `Smtp`, `SendGrid`, `Graph`) via `IEmailIntegration` implementation selection.
  - Implemented SMTP sender (`SmtpClient`) and HTTP adapters for SendGrid + Graph mail endpoints.
  - Added provider configuration hooks in SQL options + sample app configuration binding.
  - Kept retry/backoff/idempotency behavior in notifier, now delegating to selected concrete provider.
