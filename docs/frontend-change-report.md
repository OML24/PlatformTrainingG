# Frontend execution status — 2026-11-01

**Not complete / not accepted.** This is an interim implementation report, not a claim that F0–F9 or the full UI test plan passed.

| Phase | Files | Change and contract protection | Check |
|---|---|---|---|
| F0 | `frontend-baseline.md`, `frontend-contracts.csv`, `frontend-page-matrix.csv`, `frontend-blockers.md` | Captured source hashes and extracted Razor/JS identifiers; CSV fields marked for manual review, not fully validated contracts | Initial EN login/AR dashboard observed on isolated database; coverage incomplete |
| F1 | `prototype.css`, `site.css`, `_Layout.cshtml.css`, `_Layout.cshtml` | Scoped prototype shell/experience tokens; Paces component styling; no old `app.css` link; preserved antiforgery/meta and route protections | Release build 0 warnings/errors |
| F2 | `shell.js`, `site.js`, `_Layout.cshtml` | One `#zu-toasts` host; modal header/body/footer without changing public confirm/prompt/show return types; improved drawer focus | JS syntax verified; full browser modal regression pending |
| F3 | `role-switcher.js`, `_Layout.cshtml` | Uses validated GET `/api/v1/context`; narrows existing server-permitted links by selected context, without changing auth or routes; admin direct creation links to actual `/admin/courses/new`; deepest route alone active | Browser confirmed Admin context hides Quality creation link, shows Admin creation link and only one active item |
| F3 | `notifications.js`, `_Layout.cshtml`, `SharedResource.*.resx` | Localized notification empty/error/read-all, retained GET/POST contracts and added Amman timestamps when provided | Release build/JS syntax; notification interactions pending fixture test |
| F5 | `_DirectCourseForm.cshtml`, `direct-course.js`, `_CourseDirectory.cshtml`, `course-directory.js` | Fixed `direct-*` resolver mismatch; preserved field IDs and request payload; picker markup for existing date fields; course states and loading/error/empty rows | JS syntax/build; browser confirmed initialized empty pickers and no console errors; create action not yet end-to-end tested |
| F7 | `Profile.cshtml`, `profile.js`, `SharedResource.*.resx`, `Member/ProCerts.cshtml` | Moved inline code outside CSP; kept GET path and response keys; localized profile labels, date presentation uses Amman; date-only remains date-only | Build, JS syntax; authenticated profile fixture pending |
| F4 | `Login`, `ForgotPassword`, `ChangePassword`, `Verify`, `AccessDenied`, `Error`, `auth.css`, `verify.js` | Real local logo and Tajawal, calm P6 card, localized verify/error/denied, removed scaffold developer instructions; public verify still uses existing API and hides hidden records | EN verify browser loaded without console errors; other cases require visual/response fixtures |
| F5/F6 | `Courses/Available`, `Courses/Details`, `Instructor/Lectures`, related JS | Member cards with states using only API fields; course workspace tabs without duplicate panels; date-only picker and Amman instant display/send per converter | Build/JS syntax; details and lecture actions pending fixture tests |
| F7 | `Shared/Directory`, `directory.js` | Directory cards, initials, accessible search and server-driven pagination preserving API parameters | Build/JS syntax; role fixture pending |
| F8 | `Admin/Audit`, `Quality/Evaluations`, `Quality/ProCertStats`, `admin-audit.js` | Replaced native date inputs with local picker; audit detail column aligned to rows | Build/JS syntax; audit export/filter test pending |
| F9 | `Quality/Dean/Member Dashboard` and dashboard JS | Replaced first-page counts with server-provided totals or full `mine` lists; quality uses authorized all-courses endpoint; unavailable dean status totals no longer misrepresented | Build/JS syntax; role fixtures still needed |
| F4/F6/F8 follow-up | `verify.js`, `member-enter-code.js`, `available-courses.js`, `admin-audit.js` | Verify reads the actual public `{certificate}` response (not an `ok/data` envelope); attendance pagination invalidates stale lecture responses; available-course load shows one error with retry; audit pagination can reach beyond page 30 | Missing-reference verify returned localized not-found without console errors; success/revocation fixtures pending |

**Verification:** final `dotnet build ZuLms.sln -c Release` passed (0 warnings/errors); a preceding build while the app held its executable failed solely on Windows file lock, then passed after stopping that test process. `dotnet test ZuLms.sln -c Release --no-build`: 69 passed; 1 failed because Docker/Testcontainers unavailable (not a test assertion failure). JS syntax, RESX XML and protected-source SHA comparisons passed. Fresh compiled admin, course directory, direct course form, and profile loaded without console errors; context filter, single toast host, empty initialized pickers and absence of inline profile scripts verified. Screenshot capture after restart was unavailable because the Review window was not visible. No protected C# or backend source changed.

**Next:** finish page-by-page F5–F9 and localization, create role fixtures in the isolated database, test date round-trips and all actions, obtain before/after captures (the Review window must be visible), run full UI plan and protected-file comparison. This report distinguishes shared-component progress from full acceptance.

Latest local checks: Release build (0 warnings/errors), JS syntax, matched AR/EN resource keys, and `dotnet test --filter 'FullyQualifiedName!~PostgresSeedTests'` (69 passed). Docker-only seed integration test was excluded, not counted as passing.

## Follow-up implementation — 2026-11-01

Work remained exclusively under `newnew/zu-lms`. No protected C#, server permissions, routes, API payloads, configuration, dependencies or database records were changed.

| Area | Files | Fix / evidence |
|---|---|---|
| Shell localization / navigation | `_Layout.cshtml`, `SharedResource.ar/en.resx`, `role-switcher.js` | All sidebar groups/labels, brand, university, menu and navigation accessible labels use resources; active link gains `aria-current`. Existing enter-code/teaching/faculty-members/directory pages are reachable under their exact existing permission checks. Directory is shared across validated internal contexts, never ExternalInstructor. No profile edit action or route alias was invented. |
| Directory reliability | `Directory.cshtml`, `directory.js`, resources | Latest-request-wins for success/failure/finally; independent filter error and retry; safe list read retry; broken faculty filter disabled without preventing text searches. |
| Notifications | `notifications.js`, layout/resources | Visible initial loading, ordered GET responses, one read-all write at a time, recoverable visible write error; shared Amman timestamps. Arabic notification content remains a DTO limitation, now explicitly documented. |
| Date inputs | `pickers.js`, layout/resources | Strict calendar validity beyond the regex (rejects February 31), optional clear with input/change events and no automatic popup reopening, exact start/end synchronization after programmatic edits, 1-minute precision retained. |
| Shared dialogs | `site.js` | Unique title IDs, translated controls, optional action label, explicit trigger-focus restoration; public confirm/prompt/show contracts preserved. |
| Repeatable checks | `tests/frontend/` | Loopback-only static fixture server, no installs or backend access. Actual scripts/plugins with fake API promises; 14/14 component checks passed separately in EN/LTR and AR/RTL. These are SIMULATED-UI, not authenticated acceptance tests. |

**Verification in this follow-up:** Release build passed (0 warnings/errors). Full `dotnet test ZuLms.sln -c Release --no-build` ran: 69 passed, 1 failed at Docker/Testcontainers initialization (`npipe://./pipe/docker_engine` unavailable). It is not counted as passing. App/fixture JS syntax and RESX XML/unique/paired keys passed; all layout resource references resolve. AR fixture browser console had no errors. No application listener was present on 7275/5275, and this run did not start the app against a fallback database.

Evidence and before-source manifest: `docs/frontend-evidence/20261101-followup/`. Reproduction instructions: `tests/frontend/README.md`. A component screenshot succeeded during the first 13-case iteration; the final AR screenshot was blocked because the Review tab was not visible despite focus. No per-route screenshot claim is made.

**Still not complete / not accepted:** F0 route-wide baseline/captures, all-role fixtures, full F5–F9 UI workflows and generated-page localization, database date round-trips, missing backend metadata/filter/statistics/profile contracts and Docker integration remain open. Component checks do not substitute for `AI-UI-TEST-PLAN.ar.md`.

## Live permissions and multi-account session fix — 2026-11-01

Reported symptoms were treated as functional defects, not styling work:

- Lecture attendance returned a permission error immediately after course assignment; a
  member could not see a new course until signing out and in.
- Two accounts signed in on one computer could swap identities after navigation/refresh.

Backend and frontend were changed together. `PermissionService` now recalculates effective
permissions per request (no 30-second cross-request cache), and `/api/v1/auth/me` returns
effective roles. Authentication, refresh, role-context and language cookies are now
namespaced per tab with an opaque `zu_session` selector; login, refresh, logout, silent
refresh, redirects and client fetches all stay inside that namespace. A missing selector
fails closed (API 401, private HTML bootstrap then login) and never falls back to another
tab or the latest login. Public/bootstrap pages carry no private data.

Verification: Release build 0 warnings/errors; 121 tests passed excluding the Docker-only
`PostgresSeedTests`; 24 frontend script tests passed; 9/9 isolated real-API two-account
checks passed. Browser check on the running app kept `admin` and `900002` in separate tabs
across refresh, navigation and a member-only sign-out, with no console errors.

Details, contract change for API clients and reproduction commands:
`docs/session-isolation-and-live-permissions.md`.

