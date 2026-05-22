# Milestone 4 Checklist: Submissions, Edits, and Workflow

This checklist breaks Milestone 4 into execution-ordered tasks.

## Planning and Policy Decisions
- [ ] Define per-form submission edit policy model (immutable revisions vs. hard overwrite).
- [ ] Define draft submission lifecycle states and transitions.
- [ ] Define approval workflow state machine including rejection and resubmission.

## 1) Edit-After-Submit Policy
- [ ] Add per-form configuration for edit behavior in builder/admin settings.
- [ ] Enforce selected policy in submission service layer.
- [ ] For immutable mode, ensure edits always append revisions.
- [ ] For overwrite mode, ensure replacement behavior is explicit and audited.

## 2) Draft Submissions
- [ ] Add draft submission entity/state support in core + persistence.
- [ ] Add save-progress API/service method separate from final submit.
- [ ] Add resume-draft experience in form renderer for authorized submitter.
- [ ] Add expiration/cleanup policy for stale drafts (configurable or documented).

## 3) Approval Workflow Enhancements
- [ ] Add rejection action with reason capture.
- [ ] Add resubmission flow after rejection.
- [ ] Preserve workflow invariants (step ordering, allowed transitions, terminal states).
- [ ] Ensure approval actions are transactional and concurrency-safe.

## 4) Notifications and Reminders Hooks
- [ ] Add approver notification triggers for pending approvals.
- [ ] Add reminder scheduling hooks for overdue approvals.
- [ ] Add idempotency guards so retries do not duplicate notifications.
- [ ] Keep provider abstraction to support different notifier implementations.

## 5) Approval Audit Trail
- [ ] Persist full audit trail for approvals (actor, timestamp, action, signature/reason).
- [ ] Expose audit history in entry detail UI.
- [ ] Ensure audit rows are append-only and immutable.
- [ ] Add filtering/sorting for audit events in admin view.

## 6) UX and Validation
- [ ] Update submission UI for draft save/resume and final-submit confirmation.
- [ ] Add clear status badges for submitted, in review, approved, rejected, and draft.
- [ ] Surface rejection reason and next action for resubmission.
- [ ] Prevent invalid transitions from UI while still enforcing server-side checks.

## 7) Tests
- [ ] Add tests for edit policy behavior in both immutable and overwrite modes.
- [ ] Add draft lifecycle tests (create/save/resume/submit).
- [ ] Add approval workflow tests for approve/reject/resubmit transitions.
- [ ] Add notification trigger/reminder idempotency tests.
- [ ] Add audit trail integrity tests.

## 8) Done Criteria (Milestone Exit)
- [ ] Per-form edit policy works and is enforced.
- [ ] Draft submissions can be saved, resumed, and finalized.
- [ ] Rejection/resubmission and reminders are supported in workflow.
- [ ] Approval audit trail is complete and visible.
- [ ] README/ROADMAP notes updated for submissions/workflow completeness.
