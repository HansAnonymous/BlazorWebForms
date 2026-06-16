# Milestone 4 Checklist: Submissions, Edits, and Workflow

This checklist breaks Milestone 4 into execution-ordered tasks.

## Planning and Policy Decisions
- [x] Define per-form submission edit policy model (immutable revisions vs. hard overwrite).
- [x] Define draft submission lifecycle states and transitions.
- [x] Define approval workflow state machine including rejection and resubmission.

## 1) Edit-After-Submit Policy
- [x] Add per-form configuration for edit behavior in builder/admin settings.
- [x] Enforce selected policy in submission service layer.
- [x] For immutable mode, ensure edits always append revisions.
- [x] For overwrite mode, ensure replacement behavior is explicit and audited.

## 2) Draft Submissions
- [x] Add draft submission entity/state support in core + persistence.
- [x] Add save-progress API/service method separate from final submit.
- [x] Add resume-draft experience in form renderer for authorized submitter.
- [x] Add expiration/cleanup policy for stale drafts (configurable or documented).

## 3) Approval Workflow Enhancements
- [x] Add rejection action with reason capture.
- [x] Add resubmission flow after rejection.
- [x] Preserve workflow invariants (step ordering, allowed transitions, terminal states).
- [x] Ensure approval actions are transactional and concurrency-safe.

## 4) Notifications and Reminders Hooks
- [x] Add approver notification triggers for pending approvals.
- [x] Add reminder scheduling hooks for overdue approvals.
- [x] Add idempotency guards so retries do not duplicate notifications.
- [x] Keep provider abstraction to support different notifier implementations.

## 5) Approval Audit Trail
- [x] Persist full audit trail for approvals (actor, timestamp, action, signature/reason).
- [x] Expose audit history in entry detail UI.
- [x] Ensure audit rows are append-only and immutable.
- [x] Add filtering/sorting for audit events in admin view.

## 6) UX and Validation
- [x] Update submission UI for draft save/resume and final-submit confirmation.
- [x] Add clear status badges for submitted, in review, approved, rejected, and draft.
- [x] Surface rejection reason and next action for resubmission.
- [x] Prevent invalid transitions from UI while still enforcing server-side checks.

## 7) Tests
- [x] Add tests for edit policy behavior in both immutable and overwrite modes.
- [x] Add draft lifecycle tests (create/save/resume/submit).
- [x] Add approval workflow tests for approve/reject/resubmit transitions.
- [x] Add notification trigger/reminder idempotency tests.
- [x] Add audit trail integrity tests.

## Progress Notes
- 2026-05-27: Added `SubmissionEditMode` (`ImmutableRevisions`, `OverwriteLatest`) and persisted edit policy on `FormPublication`.
- 2026-05-27: Added builder policy selector in `src/BlazorWebForms.Blazor/Components/FormBuilderWorkspace.razor` and wired through `SaveDraftRequest`.
- 2026-05-27: Implemented service enforcement in `src/BlazorWebForms.Core/Services/FormsApplicationService.cs`; overwrite mode now keeps only latest revision snapshot for explicit replacement behavior.
- 2026-05-27: Updated SQL/in-memory persistence mappings in `src/BlazorWebForms.Infrastructure.SqlServer/Entities.cs`, `src/BlazorWebForms.Infrastructure.SqlServer/EfFormsRepository.cs`, `src/BlazorWebForms.Infrastructure.SqlServer/InMemorySqlFormsRepository.cs`, and `src/BlazorWebForms.Infrastructure.SqlServer/SqlServerSchemaDescriptor.cs` including legacy-schema compatibility for `PublicationEditMode`.
- 2026-05-27: Added edit policy regression coverage in `tests/BlazorWebForms.Core.Tests/Program.cs` for both immutable and overwrite modes.
- 2026-05-27: Added draft lifecycle support in core/repositories (`SaveDraftSubmissionAsync`, `GetDraftSubmissionAsync`, draft-aware submit via `SubmitEntryRequest.DraftEntryId`) and renderer resume/save flow in `src/BlazorWebForms.Blazor/Components/PublishedFormView.razor`.
- 2026-05-27: Draft lifecycle policy for stale drafts is currently documented as "latest user draft per form is reused/updated"; explicit expiration cleanup is deferred to a later hardening slice.
- 2026-05-27: Added draft lifecycle regression coverage in `tests/BlazorWebForms.Core.Tests/Program.cs` (create/save/resume/update/finalize).
- 2026-05-27: Added rejection/resubmission actions in `src/BlazorWebForms.Core/Services/FormsApplicationService.cs` with workflow guards (next-step-only transitions, terminal-state checks, reason requirement) and invariant enforcement.
- 2026-05-27: Extended approval persistence model with rejection reason in `src/BlazorWebForms.Core/Models/DomainModels.cs`, `src/BlazorWebForms.Infrastructure.SqlServer/Entities.cs`, `src/BlazorWebForms.Infrastructure.SqlServer/BlazorWebFormsDbContext.cs`, and legacy compatibility SQL in `src/BlazorWebForms.Infrastructure.SqlServer/EfFormsRepository.cs`.
- 2026-05-27: Added approve/reject/resubmit transition regression coverage in `tests/BlazorWebForms.Core.Tests/Program.cs`.
- 2026-05-27: Extended notifier abstraction with approval-assigned/reminder hooks and idempotency keys in `src/BlazorWebForms.Core/Abstractions/Contracts.cs`; wired notifications in submission/resubmission flows and reminder hook `SendApprovalRemindersAsync` in `src/BlazorWebForms.Core/Services/FormsApplicationService.cs`.
- 2026-05-27: Added idempotency behavior in notifier implementations (`src/BlazorWebForms.Infrastructure.SqlServer/MemoryEmailNotifier.cs`, `tests/BlazorWebForms.Core.Tests/Program.cs`) and regression tests for assignment/reminder notifications.
- 2026-05-27: Added approval audit trail model/persistence (`ApprovalAuditEvent`) and append-only writes in workflow transitions (approve/reject/resubmit) in `src/BlazorWebForms.Core/Services/FormsApplicationService.cs`.
- 2026-05-27: Added audit trail persistence mapping and compatibility SQL in `src/BlazorWebForms.Infrastructure.SqlServer/Entities.cs`, `src/BlazorWebForms.Infrastructure.SqlServer/BlazorWebFormsDbContext.cs`, and `src/BlazorWebForms.Infrastructure.SqlServer/EfFormsRepository.cs`.
- 2026-05-27: Exposed audit history in entry detail UI and admin filtering/sorting in `src/BlazorWebForms.SampleApp/Components/Pages/EntryDetail.razor` and `src/BlazorWebForms.Blazor/Components/FormsAdminDashboard.razor`.
- 2026-05-27: Added audit trail integrity regression coverage in `tests/BlazorWebForms.Core.Tests/Program.cs`.
- 2026-05-27: Updated submission UX in `src/BlazorWebForms.Blazor/Components/PublishedFormView.razor` with latest-submission status context, rejection reason + resubmission guidance, and guarded submit action text/state.
- 2026-05-27: Added status badge styles in `src/BlazorWebForms.Blazor/wwwroot/blazorwebforms.css` and surfaced draft/rejected counters on home dashboard in `src/BlazorWebForms.SampleApp/Components/Pages/Home.razor`.

## 8) Done Criteria (Milestone Exit)
- [x] Per-form edit policy works and is enforced.
- [x] Draft submissions can be saved, resumed, and finalized.
- [x] Rejection/resubmission and reminders are supported in workflow.
- [x] Approval audit trail is complete and visible.
- [x] README/ROADMAP notes updated for submissions/workflow completeness.

## Milestone Exit Summary
- Workflow now supports configurable edit-after-submit policy, draft save/resume/finalize lifecycle, guarded approval transitions, rejection/resubmission, reminders, and append-only audit history.
- Notification hooks are provider-abstracted with idempotency keys for assignment/reminder events.
- UI now surfaces workflow status clearly (draft/review/approved/rejected), rejection guidance, and audit history/filtering.
- Regression coverage validates policy enforcement, lifecycle transitions, notification idempotency hooks, and audit integrity across core/infrastructure paths.
