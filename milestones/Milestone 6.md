# Milestone 6 Checklist: Accessibility, i18n, and Polish

This checklist breaks Milestone 6 into execution-ordered tasks.

## Planning and Quality Targets
- [x] Define accessibility acceptance criteria aligned to WCAG 2.1 AA.
- [x] Define supported cultures/locales for initial i18n rollout.
- [x] Define performance budgets for large forms and admin pages.

## 1) Accessibility Audit and Remediation
- [x] Perform page/component audit for forms, builder, admin, and entry detail flows.
- [x] Ensure proper label association, ARIA attributes, and semantic structure.
- [x] Ensure full keyboard support for authoring, submission, and approval workflows.
- [x] Add focus management for route changes, dialogs, validation errors, and async actions.
- [x] Add accessible error summary patterns linked to invalid fields.

## 2) Visual Accessibility and Responsiveness
- [x] Verify color contrast across themes/branding options meets AA.
- [x] Validate responsive behavior on mobile/tablet/desktop for core routes.
- [x] Improve touch targets and spacing for mobile input-heavy forms.
- [x] Ensure non-text indicators (icons/status) have textual alternatives.

## 3) Internationalization End-to-End
- [x] Add culture switching in app shell and request pipeline.
- [x] Localize validation messages and system UI strings.
- [x] Ensure form content localization is applied consistently at render time.
- [x] Add fallback behavior when translation keys/content are missing.
- [x] Validate date/number formatting and RTL readiness where applicable.

## 4) Performance and Scalability Polish
- [x] Measure baseline performance for large forms and entry/admin lists.
- [x] Add virtualization/pagination strategies where data sets are large.
- [x] Add caching strategy for frequently-read metadata (published form versions, lookup tables).
- [x] Reduce avoidable re-renders and heavy allocations in Blazor components.
- [x] Add loading skeletons or progressive rendering for slow operations.

## 5) Reliability and UX Refinement
- [x] Standardize empty states, loading states, and recoverable error states.
- [x] Improve consistency of status badges/messages across workflows.
- [x] Add guardrails for long-running operations (timeouts, cancellation, retry guidance).
- [x] Ensure logging/telemetry captures user-impacting failures with actionable context.

## 6) Tests and Tooling
- [x] Add automated accessibility checks (component/page-level where feasible).
- [x] Add localization tests for key cultures and fallback behavior.
- [x] Add performance regression checks for large form scenarios.
- [x] Add responsive layout checks for major breakpoints.

## 7) Done Criteria (Milestone Exit)
- [x] Core experiences meet WCAG 2.1 AA baseline.
- [x] End-to-end localization works for selected cultures.
- [x] Performance targets are met for large-form and admin scenarios.
- [x] UX states are consistent and resilient under normal failure modes.
- [x] README/ROADMAP notes updated to reflect accessibility/i18n/polish completion.
