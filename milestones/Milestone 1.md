# Milestone 1 Checklist: Production-Grade Persistence (SQL Server / Azure SQL)

This checklist breaks Milestone 1 into execution-ordered tasks.

## Planning and Data Decisions
- [x] Define persistence boundaries and DTO/domain mapping strategy in `src/BlazorWebForms.Core` (what stays pure domain vs. EF-specific).
- [x] Decide JSON storage approach for `FormVersion.DefinitionJson` and `Entry.AnswersJson` (plain `NVARCHAR(MAX)` first, SQL JSON functions optional later).
- [x] Confirm naming conventions, keys, and audit columns for all tables before writing migrations.

## 1) EF Core Foundation (`src/BlazorWebForms.Infrastructure.SqlServer`)
- [x] Add EF Core packages and create `BlazorWebFormsDbContext`.
- [x] Add `DbSet<>` for Forms, FormVersions, Publications, Entries, EntryRevisions, ApprovalSteps, Permissions, Notifications, FilesMetadata, SearchIndex rows.
- [x] Configure entity mappings with Fluent API (keys, required fields, lengths, relationships, cascade rules).
- [x] Add value conversions/enums mapping for statuses and workflow states.
- [x] Register DbContext and SQL provider in DI.

## 2) Schema and Migrations
- [x] Create initial migration covering:
  - [x] Forms
  - [x] Form versions + publication link
  - [x] Entries + revisions
  - [x] Approval steps
  - [x] Permissions
  - [x] Notifications
  - [x] Files metadata
  - [x] Searchable field index table
- [x] Validate migration SQL against SQL Server/Azure SQL compatibility.
- [x] Add migration execution strategy for sample app startup (dev-only auto-migrate or documented manual command).
- [x] Keep/update schema script parity if you still ship a standalone SQL script.

## 3) Repository and Service Implementation Swap
- [x] Replace in-memory repository implementation with EF-backed repositories in `src/BlazorWebForms.Infrastructure.SqlServer`.
- [x] Keep existing core interfaces unchanged where possible to minimize churn.
- [x] Update save/publish flow so publishing writes immutable `FormVersion` snapshots.
- [x] Update submission flow so each edit creates immutable `EntryRevision` rows.
- [x] Ensure approval step updates are transactional with entry status updates.

## 4) Search Indexing
- [x] Implement searchable-field extraction from submission answers into index rows.
- [x] Persist normalized key/value index records during submit/edit operations.
- [x] Add repository query methods for common admin filters (form, status, date, indexed field value).
- [x] Add SQL indexes for expected predicates (form ID, created date, status, indexed key/value).

## 5) Historical Render Path
- [x] On entry-detail load, always resolve the exact `FormVersionId` tied to that submission/revision.
- [x] Render using stored `DefinitionJson` from that version, not latest form draft/published head.
- [x] Add guardrails for missing/corrupt historical version payloads (graceful error state).

## 6) App Wiring (`src/BlazorWebForms.SampleApp`)
- [x] Add SQL Server connection string configuration.
- [x] Switch DI from in-memory persistence to EF persistence.
- [x] Add local/dev instructions for DB provisioning and migration apply.
- [x] Verify existing routes (`/admin`, `/builder`, `/forms/{slug}`, `/entries/{entryId}`) still function with SQL backend.

## Progress Notes
- 2026-05-21: Removed in-memory repository fallback from SQL DI registration (`src/BlazorWebForms.Infrastructure.SqlServer/ServiceCollectionExtensions.cs`) so normal runtime path uses EF repository.
- 2026-05-21: Updated seed startup path to use migrations safely on new databases while remaining compatible with existing pre-migration local databases (`src/BlazorWebForms.Infrastructure.SqlServer/EfFormsRepository.cs`).
- 2026-05-21: Moved search filtering into SQL query for server-side filtering via `EntrySearchIndex` and submitter fields (`src/BlazorWebForms.Infrastructure.SqlServer/EfFormsRepository.cs`).
- 2026-05-21: Verified current progress with both test executables: `tests/BlazorWebForms.Core.Tests` and `tests/BlazorWebForms.Infrastructure.SqlServer.Tests`.
- 2026-05-21: Added files metadata persistence end-to-end (`EntryFiles` entity/mapping/migration, plus submission metadata mapping) in `src/BlazorWebForms.Infrastructure.SqlServer/Entities.cs`, `src/BlazorWebForms.Infrastructure.SqlServer/BlazorWebFormsDbContext.cs`, `src/BlazorWebForms.Infrastructure.SqlServer/Migrations/20260521090000_AddEntryFilesMetadata.cs`, and `src/BlazorWebForms.Infrastructure.SqlServer/EfFormsRepository.cs`.
- 2026-05-21: Added transaction boundaries around form and entry persistence to cover publish/submit/approve write paths in `src/BlazorWebForms.Infrastructure.SqlServer/EfFormsRepository.cs`.
- 2026-05-21: Added historical-render guardrails with warning fallback for missing/invalid version JSON in `src/BlazorWebForms.Core/Services/FormsApplicationService.cs` and surfaced warning in `src/BlazorWebForms.SampleApp/Components/Pages/EntryDetail.razor`.
- 2026-05-21: Extended test coverage for file metadata persistence and migration-backed DB flow in `tests/BlazorWebForms.Infrastructure.SqlServer.Tests/Program.cs`.
- 2026-05-21: Added rowversion concurrency tokens and baseline admin/query indexes in EF model plus migration `src/BlazorWebForms.Infrastructure.SqlServer/Migrations/20260521093000_AddConcurrencyAndAdminIndexes.cs`.
- 2026-05-21: Documented migration SQL script review and SQL compatibility note in `README.md`.
- 2026-05-21: Added advanced entry query options (form/status/date/indexed field filters) and wired admin dashboards to use server-side query APIs in `src/BlazorWebForms.Core/Models/DomainModels.cs`, `src/BlazorWebForms.Core/Abstractions/Contracts.cs`, `src/BlazorWebForms.Core/Services/FormsApplicationService.cs`, `src/BlazorWebForms.Infrastructure.SqlServer/EfFormsRepository.cs`, `src/BlazorWebForms.SampleApp/Components/Forms/FormsAdminDashboard.razor`, and `src/BlazorWebForms.Blazor/Components/FormsAdminDashboard.razor`.
- 2026-05-21: Extended infrastructure tests to cover publish/submission/revision/approval/historical-render/query scenarios and parallel submission behavior in `tests/BlazorWebForms.Infrastructure.SqlServer.Tests/Program.cs`.
- 2026-05-21: Updated SQL schema descriptor for parity with EF schema (tables, constraints, and indexes) in `src/BlazorWebForms.Infrastructure.SqlServer/SqlServerSchemaDescriptor.cs`.

## 7) Tests
- [x] Add integration tests for DbContext mappings and migration smoke test.
- [x] Add persistence tests:
  - [x] Publish creates immutable form version snapshot
  - [x] Submit creates entry + revision
  - [x] Edit appends revision, does not overwrite original
  - [x] Approval step progression persists correctly
- [x] Add search tests for indexed fields and admin filters.
- [x] Add historical-render test to ensure entry uses exact submitted form version.
- [x] Keep/extend `tests/BlazorWebForms.Infrastructure.SqlServer.Tests`.

## 8) Data Safety and Performance Baseline
- [x] Add transaction boundaries for multi-write operations (publish, submit, approve).
- [x] Add optimistic concurrency tokens where needed (rowversion/timestamp).
- [x] Add baseline indexes and run simple query-plan sanity checks.
- [x] Validate expected behavior under parallel submissions/edits.

## 9) Done Criteria (Milestone Exit)
- [x] No runtime dependency on in-memory repository in normal sample app path.
- [x] Fresh database can be created from migrations only.
- [x] End-to-end flows pass on SQL: builder, publish, submit, revisions, approvals, admin search.
- [x] Historical rendering verified for old entries after form changes.
- [x] README/ROADMAP notes updated to reflect "real SQL persistence implemented".
