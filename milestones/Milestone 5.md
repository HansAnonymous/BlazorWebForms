# Milestone 5 Checklist: Files, PDF, Notifications (Real Implementations)

This checklist breaks Milestone 5 into execution-ordered tasks.

## Planning and Provider Choices
- [ ] Choose production file storage strategy (local/dev, Azure Blob, or pluggable provider).
- [ ] Choose PDF generation library/provider and document licensing/runtime implications.
- [ ] Choose email provider(s) and fallback strategy (SendGrid/SMTP/Graph/etc.).

## 1) File Upload Implementation
- [ ] Replace demo file handling with real byte storage + metadata persistence.
- [ ] Support multiple files per field and per submission where configured.
- [ ] Add per-field constraints (max size, allowed MIME/extensions, max count).
- [ ] Add secure file naming, content-type verification, and virus-scan integration point.
- [ ] Add download/auth rules and signed/temporary URL strategy where applicable.

## 2) File Metadata and Lifecycle
- [ ] Persist file metadata (storage key, original name, size, type, hash, uploaded by, timestamp).
- [ ] Link files to entry revisions for historical integrity.
- [ ] Add cleanup strategy for orphaned temp files and deleted drafts.
- [ ] Add retention policy hooks for compliance-sensitive deployments.

## 3) PDF Export Implementation
- [ ] Replace stub text exporter with real PDF renderer service.
- [ ] Define PDF template/layout rules for sections, answers, signatures, and audit context.
- [ ] Support branding and localization in exported PDF.
- [ ] Ensure historical export uses exact submitted form version and revision data.
- [ ] Add large-entry safeguards (pagination, memory bounds, timeout behavior).

## 4) Email Notification Implementation
- [ ] Replace in-memory notifier with provider-backed email sender.
- [ ] Add templated notifications for submit, approval pending, reminder, approval, rejection.
- [ ] Add retry/backoff and dead-letter/failure handling strategy.
- [ ] Add idempotency keys for event-driven sends to avoid duplicates.
- [ ] Add environment-based toggle to disable outbound email in local/dev.

## 5) Security and Compliance Baseline
- [ ] Validate file upload/download authorization for submitters, approvers, and admins.
- [ ] Add anti-abuse limits (rate limits/quota hooks) for uploads and outbound notifications.
- [ ] Ensure sensitive metadata is not leaked in logs.
- [ ] Add basic compliance notes for storage region and data retention expectations.

## 6) UX and Operational Observability
- [ ] Add user-visible upload progress and failure states.
- [ ] Add PDF export status/error messaging in UI.
- [ ] Add notification delivery status indicators where useful.
- [ ] Add structured logs/metrics for upload throughput, PDF generation, email delivery.

## 7) Tests
- [ ] Add integration tests for upload constraints and multi-file handling.
- [ ] Add storage abstraction tests (write/read/delete, metadata consistency).
- [ ] Add PDF generation snapshot/contract tests.
- [ ] Add email template and delivery workflow tests with provider mocks.
- [ ] Add end-to-end test covering submission with files, approval notification, and PDF export.

## 8) Done Criteria (Milestone Exit)
- [ ] Real file storage with metadata and constraints is in place.
- [ ] PDF export generates usable documents from real submission data.
- [ ] Email notifications are sent through a real provider with retries/idempotency.
- [ ] Security and operational baseline checks are in place.
- [ ] README/ROADMAP notes updated for real file/PDF/notification implementations.
