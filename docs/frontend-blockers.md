# Frontend gaps and acceptance blockers

These entries are not completed frontend requirements.

| Requirement | Current contract / finding | Action |
|---|---|---|
| Full F0 screenshots | Only initial login and admin dashboard captured; full role fixtures and viewport coverage pending | Complete baseline before page rewrites |
| Required routes | Available is `/courses/available`, enrolled is `/member/courses`, details is `/courses/{id}`, impact review is `/impact/review` | Use actual routes; no aliases added |
| Dedicated role/statistics routes | Not in current Program.cs route inventory | Reconcile approved route matrix and record backend requirements |
| Profile editing | Existing Razor only loads `/api/v1/profiles/{userId}` | Inspect owner-edit contracts before adding controls |
| Registration instant | `AmmanDateTimeOffsetConverter` accepts `yyyy-MM-dd HH:mm` as Amman wall-clock; direct creation and details editing now send this contract format | Verify round-trip with isolated course fixture; no endpoint/DTO change |
| UI acceptance | Build success does not prove browser behavior | Execute AI-UI-TEST-PLAN.ar.md with isolated role fixtures |
| Postgres seed integration test | `PostgresSeedTests` requires Docker/Testcontainers; Docker pipe unavailable on this host | Enable Docker in test environment, rerun suite; no backend change |
| Date input verification | Native date/time inputs remain removed. Follow-up adds strict calendar validation, optional clear buttons, and synchronizes both picker dates on reopening. Real plugin component checks passed EN/LTR and AR/RTL for blank/clear/leap-day/invalid-date/23:59/00:00/dialog placement | SIMULATED-UI only; authenticated create/edit/save/refresh round-trip and actual dialog pointer interactions still need isolated role fixtures |
| Available-course metadata | `AvailableCourseDto` has title, dates, max capacity, deadline and registered flag, but no instructor, track, mode, or current participant count | Backend must expose fields before member cards can show those details or a reliable full state; no fabricated values |
| Available-course search and filters | GET `/api/v1/courses/available` accepts `page` only | Backend must support search/track/mode filters if the prototype toolbar is required; do not filter a single page and present it as a complete result |
| Directory search | Profiles directory endpoint filters `search` and `centerId` only; no department/expertise-specific filter keys | Backend contract needed if distinct filters are mandatory |
| Directory English metadata | `DirectoryRowDto` exposes Arabic full name, center, department, and expertise only | Bilingual field values need corresponding DTO fields; English UI uses Arabic source names until supplied |
| Dean dashboard counts by status | `/api/v1/proposals/mine` exposes paged `items` and `total`, but no per-status totals or status filter; previous frontend counted first page and mislabeled it as totals | Added honest unavailable indicators; backend aggregation required for returned/approved counts |
| Member enter-code current lecture cards | Existing `GET /api/v1/courses/{id}/lectures` lists lectures but does not expose which attendance code is currently active to a member, nor the server-configured code length | Keep explicit course/lecture selection and text code with pagination; backend needs a member-safe active lecture/code metadata contract for the requested cards |
| Navigation localization | Sidebar groups/links, brand, header, menu/navigation ARIA now use paired resources. Added missing links to existing enter-code, teaching, faculty-members and directory pages using their existing server permission keys; context narrowing retained | Layout resource references and Release compilation checked; authenticated AR/EN shell browser acceptance and remaining generated page strings still pending |
| Profile owner editing | PUT `/api/v1/profiles/me` takes `showInDirectory` (required bool), but GET `/api/v1/profiles/{userId}` DTO omits the current value. Saving other edits would silently force visibility to false or true; owner ID/permission also requires secure determination | Backend must expose current visibility (and ownership/permission to render action), or provide a safe patch contract before implementing edit UI |
| Browser captures after restart | Follow-up captured an intermediate component-fixture screenshot; final AR screenshot still rejected despite focus because the tab was not visible | Restore visible Review pane; fixture captures do not replace before/after images of authenticated application routes |

## Follow-up fixes — 2026-11-01

- **Multi-account tabs and live permissions are fixed, not just styled.** Permissions are
  recalculated per request, and each tab has its own cookie namespace selected by
  `zu_session`; no tab can fall back to another account. See
  `docs/session-isolation-and-live-permissions.md`. Browser acceptance covered two
  accounts plus isolated sign-out; the remaining route/role matrix and screenshot blockers
  above are unchanged.
- Directory ignores stale success/failure responses; only the newest request controls cards, pagination and busy state. Faculty-filter failure has its own visible message/retry and cannot be erased by successful card loading. Failed list reads have a retry action. API parameters remain `search`, `centerId`, `page`.
- Notifications now have a loading state, ordered refreshes, guarded read-all writes and a visible write-failure message instead of swallowing the exception. Timestamp display uses the shared Amman/Latin-digit formatter.
- Shared prompts preserve cancellation return values, restore trigger focus, accept optional action labels and use unique accessible dialog titles.
- **Newly confirmed backend localization gap:** `NotificationRowDto` in `Application/Features/Notifications/NotificationService.cs` exposes only `TitleAr`/`BodyAr`, even though the stored entity has English fields. Frontend controls are translated; notification content must keep its Arabic source until the DTO exposes translations. No backend source was changed.
- `tests/frontend/` supplies reproducible SIMULATED-UI checks with controlled API promises and actual frontend/plugin assets. It does not remove the full-role, route, database, screenshot or Docker acceptance blockers above.
