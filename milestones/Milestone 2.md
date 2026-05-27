# Milestone 2 Checklist: Real Auth and Permissions

This checklist breaks Milestone 2 into execution-ordered tasks.

## Planning and Security Model
- [x] Choose host auth strategy for the sample app (cookie/OIDC/Entra ID) and document assumptions.
- [x] Define user identity contract used by core services (stable user id, display name, email).
- [x] Finalize permission matrix for owner/manager, approver, submitter/self-viewer, and admin.

## 1) Authentication Integration
- [x] Integrate ASP.NET Core authentication in `src/BlazorWebForms.SampleApp`.
- [x] Add login/logout and authenticated app shell behavior.
- [x] Flow authenticated principal into application services (no anonymous fallthrough on protected operations).
- [x] Add configuration docs for local/dev auth setup.

## 2) Authorization Policy Definitions
- [x] Define policy names and requirements (builder access, publish access, admin access, approver actions, self-view permissions).
- [x] Implement `IAuthorizationHandler`/requirements where role checks are not enough.
- [x] Add form-level policy evaluation hooks in core service layer.
- [x] Ensure route/component guards align with backend checks (defense in depth).

## 3) Domain Authorization Enforcement
- [x] Enforce owner/manager permissions for builder and publish operations.
- [x] Enforce approver permissions for approval-step actions.
- [x] Enforce submitter/self-view policy for viewing own entries when enabled by form policy.
- [x] Add clear forbidden/not-found behavior to avoid leaking entry/form existence.

## 4) Data and Storage Support
- [x] Add persistence model for permissions and assignment scopes in `src/BlazorWebForms.Infrastructure.SqlServer`.
- [x] Add migration updates for authorization-related tables/columns.
- [x] Seed or bootstrap minimal admin/manager mapping for local development.
- [x] Ensure permission changes are auditable (who changed what and when).

## 5) Invitation Flow (Optional Module)
- [x] Design invitation entity with token, target email, role/scope, expiry, and status.
- [x] Add invitation create/accept/revoke endpoints/services.
- [x] Add invitation email contract integration point (can remain provider-abstracted).
- [x] Add anti-abuse controls (single-use token, expiration, replay protection).

## 6) UX and Error States
- [x] Update builder/admin/forms pages to show access-denied and sign-in-required states.
- [x] Hide unavailable actions in UI based on evaluated permissions.
- [x] Keep server-side authorization as source of truth even when UI hides actions.
- [x] Add helpful, non-sensitive error text for auth failures.

## 7) Tests
- [x] Add unit tests for policy/handler logic across role combinations.
- [x] Add integration tests for protected operations (builder/publish/admin/approve/self-view).
- [x] Add regression tests for permission escalation attempts and forbidden access.
- [x] Add invitation flow tests if optional module is enabled.

## Progress Notes
- 2026-05-21: Added ASP.NET Core cookie authentication and authorization policy registration in `src/BlazorWebForms.SampleApp/Program.cs`.
- 2026-05-21: Added claims-based current user adapter in `src/BlazorWebForms.SampleApp/ClaimsCurrentUserContext.cs` and dev auth endpoints in `src/BlazorWebForms.SampleApp/DevAuthController.cs`.
- 2026-05-21: Added route-level guards via `[Authorize]` on admin, builder, and published form pages (`src/BlazorWebForms.SampleApp/Components/Pages/Admin.razor`, `src/BlazorWebForms.SampleApp/Components/Pages/Builder.razor`, `src/BlazorWebForms.SampleApp/Components/Pages/PublishedForm.razor`).
- 2026-05-21: Added app-shell login/logout UX and user context badge in `src/BlazorWebForms.SampleApp/Components/Layout/MainLayout.razor`.
- 2026-05-21: Tightened service-layer authorization for builder and entry detail access in `src/BlazorWebForms.Core/Services/FormsApplicationService.cs` and permission evaluator updates in `src/BlazorWebForms.Core/Services/DefaultImplementations.cs`.
- 2026-05-21: Extended core tests for authenticated/anonymous authorization flows in `tests/BlazorWebForms.Core.Tests/Program.cs`.
- 2026-05-21: Added permission scope and audit fields (`ScopeType`, `ScopeValue`, `UpdatedByUserId`, `UpdatedUtc`) to SQL permissions model and repository mapping in `src/BlazorWebForms.Infrastructure.SqlServer/Entities.cs`, `src/BlazorWebForms.Infrastructure.SqlServer/BlazorWebFormsDbContext.cs`, and `src/BlazorWebForms.Infrastructure.SqlServer/EfFormsRepository.cs`.
- 2026-05-21: Added migration `src/BlazorWebForms.Infrastructure.SqlServer/Migrations/20260521095500_AddPermissionScopeAuditFields.cs` and updated model snapshot for authorization-related storage changes.
- 2026-05-21: Seeded local development admin mapping in demo form seed and added admin permission test coverage in `tests/BlazorWebForms.Core.Tests/Program.cs`.
- 2026-05-21: Added custom authorization requirements/handlers (`RoleSetRequirement`, `SelfOrManagerRequirement`) and registered them in `src/BlazorWebForms.SampleApp/Program.cs` with implementation in `src/BlazorWebForms.SampleApp/AuthHandlers.cs`.
- 2026-05-21: Added dedicated policies (`BuilderAccess`, `PublishAccess`, `ApproverAction`, `SelfViewAccess`) in `src/BlazorWebForms.SampleApp/AuthPolicies.cs` and applied route guards to core pages.
- 2026-05-21: Added invitation domain/persistence/service flow with anti-abuse checks (duplicate pending invite guard, expiration handling, single-use status transitions) in `src/BlazorWebForms.Core/Models/DomainModels.cs`, `src/BlazorWebForms.Core/Services/FormsApplicationService.cs`, `src/BlazorWebForms.Infrastructure.SqlServer/Entities.cs`, `src/BlazorWebForms.Infrastructure.SqlServer/BlazorWebFormsDbContext.cs`, and `src/BlazorWebForms.Infrastructure.SqlServer/EfFormsRepository.cs`.
- 2026-05-21: Added invitation endpoints in `src/BlazorWebForms.SampleApp/InvitationController.cs` and policy `InvitationManage` in `src/BlazorWebForms.SampleApp/AuthPolicies.cs`.
- 2026-05-21: Added invitation SQL migration `src/BlazorWebForms.Infrastructure.SqlServer/Migrations/20260521101500_AddFormInvitations.cs` and snapshot updates.
- 2026-05-21: Added regression tests for permission escalation and invitation lifecycle scenarios in `tests/BlazorWebForms.Core.Tests/Program.cs` and invitation smoke in `tests/BlazorWebForms.Infrastructure.SqlServer.Tests/Program.cs`.
- 2026-05-21: Extended `IEmailNotifier` with invitation notifications and wired create/accept/revoke sends in `src/BlazorWebForms.Core/Services/FormsApplicationService.cs` with memory implementation in `src/BlazorWebForms.Infrastructure.SqlServer/MemoryEmailNotifier.cs`.
- 2026-05-21: Added route-level not-authorized UX via `AuthorizeRouteView` and `src/BlazorWebForms.SampleApp/Components/Pages/NotAuthorized.razor`, plus clearer auth guidance text on protected pages.
- 2026-05-21: Finalized permission matrix and tightened no-leak entry detail behavior by returning null for unauthorized reads in `src/BlazorWebForms.Core/Services/FormsApplicationService.cs`; updated entry detail UI wording in `src/BlazorWebForms.SampleApp/Components/Pages/EntryDetail.razor`.

## 8) Done Criteria (Milestone Exit)
- [x] Authenticated identity is used end-to-end for protected actions.
- [x] Owner/manager, approver, and submitter/self-view rules are enforced in services and routes.
- [x] Unauthorized access returns correct HTTP/UI behavior without data leakage.
- [x] Optional invitation flow works when enabled.
- [x] README/ROADMAP notes updated to reflect real auth/authorization availability.

### Permission Matrix (Finalized)
| Capability | Admin | Owner/Manager | Approver | Submitter/SelfViewer | Anonymous |
|---|---|---|---|---|---|
| Builder draft/edit | Allow | Allow | Deny | Deny | Deny |
| Publish form | Allow | Allow | Deny | Deny | Deny |
| Admin dashboard | Allow | Allow | Deny | Deny | Deny |
| Approve assigned step | Allow | Allow | Allow (assigned email) | Deny | Deny |
| View entry detail | Allow | Allow | Deny (unless manager/admin) | Allow (own entry / granted viewer scope) | Deny |
| Submit published form (sample auth mode) | Allow | Allow | Allow | Allow | Deny |
| Manage invitations (create/revoke) | Allow | Allow | Deny | Deny | Deny |
| Accept invitation | Allow (authenticated) | Allow (authenticated) | Allow (authenticated) | Allow (authenticated) | Deny |
