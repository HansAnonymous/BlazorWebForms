# Milestone 3 Checklist: Builder UX and Schema Completeness

This checklist breaks Milestone 3 into execution-ordered tasks.

## Planning and Schema Direction
- [x] Define complete builder feature scope for V1.5 (field options, conditions, layout, localization, branding).
- [x] Formalize `FormDefinition` JSON schema versioning strategy (version field + compatibility rules).
- [x] Decide migration policy for older definitions (on-read transform vs. explicit migration jobs).

## 1) Field Editing Completeness
- [x] Add field editors for placeholder, help text, regex, options list, defaults, and validation hints.
- [x] Add reorder interactions for fields and sections.
- [x] Add safe delete flows with confirmation and dependency checks.
- [x] Ensure all builder edits update draft definition consistently.

## 2) Conditional Visibility Rule Builder
- [x] Replace raw `field=value` editing with structured rule builder UI.
- [x] Support rule groups (AND/OR) and basic operators (equals/not equals/contains/empty).
- [x] Validate rule references against existing fields to prevent broken conditions.
- [x] Persist and preview conditional behavior in builder.

## 3) Section-Level Conditions and Advanced Layout
- [x] Add section-level visibility conditions.
- [x] Add layout controls for sections/fields (columns, width hints, grouping).
- [x] Ensure renderer supports layout metadata without breaking existing forms.
- [x] Add fallback rendering when layout metadata is missing.

## 4) Localization Editing
- [x] Add localization editor for labels/help/options by culture.
- [x] Define default/fallback culture behavior for missing translations.
- [x] Validate localization payload shape in save/publish flows.
- [x] Ensure published snapshots contain localized content for historical accuracy.

## 5) Branding Support
- [x] Add branding model fields (logo URL/file ref, hero image ref, palette tokens).
- [x] Add builder UI for branding configuration.
- [x] Apply branding tokens in published form renderer with safe defaults.
- [x] Validate uploaded/linked assets and sanitize configurable style inputs.

## 6) Schema Versioning and Compatibility
- [x] Add explicit schema version to `FormDefinition`.
- [x] Implement parser/validator per schema version.
- [x] Add forward-compatible transforms for older definitions.
- [x] Add publish-time validation gate to block invalid schema payloads.

## 7) Tests
- [x] Add builder component tests for edit/reorder/delete and rule authoring.
- [x] Add serialization/deserialization tests for schema versioned definitions.
- [x] Add conditional visibility rendering tests (field and section level).
- [x] Add localization and branding snapshot tests.

## Progress Notes
- 2026-05-26: Added explicit `SchemaVersion` to `FormDefinition` in `src/BlazorWebForms.Core/Models/FormDefinitionModels.cs` with current version constant.
- 2026-05-26: Implemented schema-aware deserialize/serialize flow in `src/BlazorWebForms.Core/Services/DefaultImplementations.cs` with on-read upgrade for legacy payloads and rejection for unsupported future versions.
- 2026-05-26: Added core regression coverage for schema round-trip, legacy upgrade, and future-version rejection in `tests/BlazorWebForms.Core.Tests/Program.cs`.
- 2026-05-26: Added publish-time definition validation in `src/BlazorWebForms.Core/Services/FormsApplicationService.cs` (schema version checks, duplicate ids, option integrity, regex validity, and condition-reference validation).
- 2026-05-26: Expanded builder UX in `src/BlazorWebForms.Blazor/Components/FormBuilderWorkspace.razor` with field editors (placeholder/help/regex/default/hint/options), section/field reorder, section-level condition editing, and delete confirmations.
- 2026-05-26: Added dependency-aware delete guards in `src/BlazorWebForms.Blazor/Components/FormBuilderWorkspace.razor` to block deleting fields/sections referenced by visibility conditions until references are removed.
- 2026-05-26: Added draft consistency regression coverage in `tests/BlazorWebForms.Core.Tests/Program.cs` to verify section/field edits (reorder-dependent structure, visibility conditions, placeholder/help/regex/default/hint/options) persist through save/load builder state.
- 2026-05-26: Added structured visibility rule models/evaluator support (`VisibilityConditionDefinition`, AND/OR joins, equals/not-equals/contains/empty operators) and integrated renderer checks in `src/BlazorWebForms.Blazor/Components/DynamicFormRenderer.razor`.
- 2026-05-26: Added builder rule authoring UX in `src/BlazorWebForms.Blazor/Components/FormBuilderWorkspace.razor` for section/field rule lists (`fieldId|operator|value`) and join selection.
- 2026-05-26: Added conditional rendering tests (field + section level) in `tests/BlazorWebForms.Core.Tests/Program.cs`.
- 2026-05-26: Added builder-side visibility diagnostics panel in `src/BlazorWebForms.Blazor/Components/FormBuilderWorkspace.razor` and persistence/snapshot assertions for structured rules in `tests/BlazorWebForms.Core.Tests/Program.cs`.
- 2026-05-26: Added layout metadata models (`FormSectionLayoutDefinition`, `FormFieldLayoutDefinition`) and builder controls for section columns/group and field width/group in `src/BlazorWebForms.Blazor/Components/FormBuilderWorkspace.razor`.
- 2026-05-26: Updated renderer layout behavior in `src/BlazorWebForms.Blazor/Components/DynamicFormRenderer.razor` and CSS grid classes in `src/BlazorWebForms.Blazor/wwwroot/blazorwebforms.css` with safe fallback handling via `src/BlazorWebForms.Core/Services/FormLayoutResolver.cs`.
- 2026-05-26: Added conditional + layout fallback coverage in `tests/BlazorWebForms.Core.Tests/Program.cs`.
- 2026-05-26: Added localization editing in builder (`culture|text` mappings for form/section/field/option), default culture support, and localized rendering fallbacks via `src/BlazorWebForms.Core/Services/FormLocalizationResolver.cs`.
- 2026-05-26: Added localization payload validation in `src/BlazorWebForms.Core/Services/FormsApplicationService.cs` and localization persistence/snapshot tests in `tests/BlazorWebForms.Core.Tests/Program.cs`.
- 2026-05-26: Added branding model fields and sanitization/validation in `src/BlazorWebForms.Core/Models/FormDefinitionModels.cs` and `src/BlazorWebForms.Core/Services/FormsApplicationService.cs` (asset URL/file-ref checks, color token normalization, radius token constraints).
- 2026-05-26: Added builder branding editors in `src/BlazorWebForms.Blazor/Components/FormBuilderWorkspace.razor` and applied branding tokens/assets with safe defaults in `src/BlazorWebForms.Blazor/Components/PublishedFormView.razor` + `src/BlazorWebForms.Blazor/wwwroot/blazorwebforms.css`.
- 2026-05-26: Added branding persistence/validation snapshot tests in `tests/BlazorWebForms.Core.Tests/Program.cs`.

## 8) Done Criteria (Milestone Exit)
- [x] Builder supports complete field editing, reorder/delete, and structured condition authoring.
- [x] Section conditions, localization, and branding are available in draft and published render paths.
- [x] `FormDefinition` versioning is explicit and validated.
- [x] Older definitions remain renderable through compatibility handling.
- [x] README/ROADMAP notes updated for builder/schema completeness.

## Milestone Exit Summary
- Builder now supports complete V1.5 editing coverage in draft flow: field metadata/options, reorder/delete safeguards, structured conditional rules, layout metadata, localization payloads, and branding tokens/assets.
- Published renderer consumes structured conditions, layout metadata, localization fallback, and branding tokens with safe defaults/fallback behavior.
- Schema versioning is explicit (`SchemaVersion`) with compatibility handling for legacy payloads and validation gates for unsupported/invalid definitions.
- Regression coverage in `tests/BlazorWebForms.Core.Tests/Program.cs` validates edit persistence, rule behavior, layout fallback, localization/branding persistence, and publish-time validation safeguards.
