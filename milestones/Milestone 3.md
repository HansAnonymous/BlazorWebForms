# Milestone 3 Checklist: Builder UX and Schema Completeness

This checklist breaks Milestone 3 into execution-ordered tasks.

## Planning and Schema Direction
- [ ] Define complete builder feature scope for V1.5 (field options, conditions, layout, localization, branding).
- [ ] Formalize `FormDefinition` JSON schema versioning strategy (version field + compatibility rules).
- [ ] Decide migration policy for older definitions (on-read transform vs. explicit migration jobs).

## 1) Field Editing Completeness
- [ ] Add field editors for placeholder, help text, regex, options list, defaults, and validation hints.
- [ ] Add reorder interactions for fields and sections.
- [ ] Add safe delete flows with confirmation and dependency checks.
- [ ] Ensure all builder edits update draft definition consistently.

## 2) Conditional Visibility Rule Builder
- [ ] Replace raw `field=value` editing with structured rule builder UI.
- [ ] Support rule groups (AND/OR) and basic operators (equals/not equals/contains/empty).
- [ ] Validate rule references against existing fields to prevent broken conditions.
- [ ] Persist and preview conditional behavior in builder.

## 3) Section-Level Conditions and Advanced Layout
- [ ] Add section-level visibility conditions.
- [ ] Add layout controls for sections/fields (columns, width hints, grouping).
- [ ] Ensure renderer supports layout metadata without breaking existing forms.
- [ ] Add fallback rendering when layout metadata is missing.

## 4) Localization Editing
- [ ] Add localization editor for labels/help/options by culture.
- [ ] Define default/fallback culture behavior for missing translations.
- [ ] Validate localization payload shape in save/publish flows.
- [ ] Ensure published snapshots contain localized content for historical accuracy.

## 5) Branding Support
- [ ] Add branding model fields (logo URL/file ref, hero image ref, palette tokens).
- [ ] Add builder UI for branding configuration.
- [ ] Apply branding tokens in published form renderer with safe defaults.
- [ ] Validate uploaded/linked assets and sanitize configurable style inputs.

## 6) Schema Versioning and Compatibility
- [ ] Add explicit schema version to `FormDefinition`.
- [ ] Implement parser/validator per schema version.
- [ ] Add forward-compatible transforms for older definitions.
- [ ] Add publish-time validation gate to block invalid schema payloads.

## 7) Tests
- [ ] Add builder component tests for edit/reorder/delete and rule authoring.
- [ ] Add serialization/deserialization tests for schema versioned definitions.
- [ ] Add conditional visibility rendering tests (field and section level).
- [ ] Add localization and branding snapshot tests.

## 8) Done Criteria (Milestone Exit)
- [ ] Builder supports complete field editing, reorder/delete, and structured condition authoring.
- [ ] Section conditions, localization, and branding are available in draft and published render paths.
- [ ] `FormDefinition` versioning is explicit and validated.
- [ ] Older definitions remain renderable through compatibility handling.
- [ ] README/ROADMAP notes updated for builder/schema completeness.
