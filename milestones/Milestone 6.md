# Milestone 6 Checklist: Accessibility, i18n, and Polish

This checklist breaks Milestone 6 into execution-ordered tasks.

## Planning and Quality Targets
- [ ] Define accessibility acceptance criteria aligned to WCAG 2.1 AA.
- [ ] Define supported cultures/locales for initial i18n rollout.
- [ ] Define performance budgets for large forms and admin pages.

## 1) Accessibility Audit and Remediation
- [ ] Perform page/component audit for forms, builder, admin, and entry detail flows.
- [ ] Ensure proper label association, ARIA attributes, and semantic structure.
- [ ] Ensure full keyboard support for authoring, submission, and approval workflows.
- [ ] Add focus management for route changes, dialogs, validation errors, and async actions.
- [ ] Add accessible error summary patterns linked to invalid fields.

## 2) Visual Accessibility and Responsiveness
- [ ] Verify color contrast across themes/branding options meets AA.
- [ ] Validate responsive behavior on mobile/tablet/desktop for core routes.
- [ ] Improve touch targets and spacing for mobile input-heavy forms.
- [ ] Ensure non-text indicators (icons/status) have textual alternatives.

## 3) Internationalization End-to-End
- [ ] Add culture switching in app shell and request pipeline.
- [ ] Localize validation messages and system UI strings.
- [ ] Ensure form content localization is applied consistently at render time.
- [ ] Add fallback behavior when translation keys/content are missing.
- [ ] Validate date/number formatting and RTL readiness where applicable.

## 4) Performance and Scalability Polish
- [ ] Measure baseline performance for large forms and entry/admin lists.
- [ ] Add virtualization/pagination strategies where data sets are large.
- [ ] Add caching strategy for frequently-read metadata (published form versions, lookup tables).
- [ ] Reduce avoidable re-renders and heavy allocations in Blazor components.
- [ ] Add loading skeletons or progressive rendering for slow operations.

## 5) Reliability and UX Refinement
- [ ] Standardize empty states, loading states, and recoverable error states.
- [ ] Improve consistency of status badges/messages across workflows.
- [ ] Add guardrails for long-running operations (timeouts, cancellation, retry guidance).
- [ ] Ensure logging/telemetry captures user-impacting failures with actionable context.

## 6) Tests and Tooling
- [ ] Add automated accessibility checks (component/page-level where feasible).
- [ ] Add localization tests for key cultures and fallback behavior.
- [ ] Add performance regression checks for large form scenarios.
- [ ] Add responsive layout checks for major breakpoints.

## 7) Done Criteria (Milestone Exit)
- [ ] Core experiences meet WCAG 2.1 AA baseline.
- [ ] End-to-end localization works for selected cultures.
- [ ] Performance targets are met for large-form and admin scenarios.
- [ ] UX states are consistent and resilient under normal failure modes.
- [ ] README/ROADMAP notes updated to reflect accessibility/i18n/polish completion.
