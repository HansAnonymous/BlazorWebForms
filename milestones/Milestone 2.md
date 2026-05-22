# Milestone 2 Checklist: Real Auth and Permissions

This checklist breaks Milestone 2 into execution-ordered tasks.

## Planning and Security Model
- [ ] Choose host auth strategy for the sample app (cookie/OIDC/Entra ID) and document assumptions.
- [ ] Define user identity contract used by core services (stable user id, display name, email).
- [ ] Finalize permission matrix for owner/manager, approver, submitter/self-viewer, and admin.

## 1) Authentication Integration
- [ ] Integrate ASP.NET Core authentication in `src/BlazorWebForms.SampleApp`.
- [ ] Add login/logout and authenticated app shell behavior.
- [ ] Flow authenticated principal into application services (no anonymous fallthrough on protected operations).
- [ ] Add configuration docs for local/dev auth setup.

## 2) Authorization Policy Definitions
- [ ] Define policy names and requirements (builder access, publish access, admin access, approver actions, self-view permissions).
- [ ] Implement `IAuthorizationHandler`/requirements where role checks are not enough.
- [ ] Add form-level policy evaluation hooks in core service layer.
- [ ] Ensure route/component guards align with backend checks (defense in depth).

## 3) Domain Authorization Enforcement
- [ ] Enforce owner/manager permissions for builder and publish operations.
- [ ] Enforce approver permissions for approval-step actions.
- [ ] Enforce submitter/self-view policy for viewing own entries when enabled by form policy.
- [ ] Add clear forbidden/not-found behavior to avoid leaking entry/form existence.

## 4) Data and Storage Support
- [ ] Add persistence model for permissions and assignment scopes in `src/BlazorWebForms.Infrastructure.SqlServer`.
- [ ] Add migration updates for authorization-related tables/columns.
- [ ] Seed or bootstrap minimal admin/manager mapping for local development.
- [ ] Ensure permission changes are auditable (who changed what and when).

## 5) Invitation Flow (Optional Module)
- [ ] Design invitation entity with token, target email, role/scope, expiry, and status.
- [ ] Add invitation create/accept/revoke endpoints/services.
- [ ] Add invitation email contract integration point (can remain provider-abstracted).
- [ ] Add anti-abuse controls (single-use token, expiration, replay protection).

## 6) UX and Error States
- [ ] Update builder/admin/forms pages to show access-denied and sign-in-required states.
- [ ] Hide unavailable actions in UI based on evaluated permissions.
- [ ] Keep server-side authorization as source of truth even when UI hides actions.
- [ ] Add helpful, non-sensitive error text for auth failures.

## 7) Tests
- [ ] Add unit tests for policy/handler logic across role combinations.
- [ ] Add integration tests for protected operations (builder/publish/admin/approve/self-view).
- [ ] Add regression tests for permission escalation attempts and forbidden access.
- [ ] Add invitation flow tests if optional module is enabled.

## 8) Done Criteria (Milestone Exit)
- [ ] Authenticated identity is used end-to-end for protected actions.
- [ ] Owner/manager, approver, and submitter/self-view rules are enforced in services and routes.
- [ ] Unauthorized access returns correct HTTP/UI behavior without data leakage.
- [ ] Optional invitation flow works when enabled.
- [ ] README/ROADMAP notes updated to reflect real auth/authorization availability.
