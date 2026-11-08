# Isolated frontend component checks

From the `zu-lms` root, run `node tests/frontend/serve.mjs`, then open:

- `http://127.0.0.1:5286/` (English/LTR)
- `http://127.0.0.1:5286/?lang=ar` (Arabic/RTL picker/dialog controls)

Click **Run component checks** once. Reload to run again. Results are also available
as `window.frontendRegressionResults`. The fixture heading and results stay in English.

The fixture uses the actual frontend JS, Moment, jQuery and daterangepicker; API
responses are controlled promises in `fixture-api.js`. No database, login secrets,
production API calls, dependency installs or changes to backend tests are needed.
Tests cover strict calendar validity, empty/clear/idempotent pickers, minute/midnight
preservation inside dialogs, confirm/prompt contracts, directory request ordering,
independent filter errors/retry, and notification read errors/double-write guards.

These are **SIMULATED-UI component checks**, not authenticated route, permission,
database round-trip, full localization or visual-acceptance tests. This server binds
only to loopback and serves the fixture directory and application public assets.

## Lecture date / validation regression

- `node --test tests/frontend/lecture-dates.test.mjs`: tests the actual date helper
  in Node, including Arabic/English, 09:33/09:43, 23:59/midnight, blanks and Amman
  edit round-trip formatting.
- With the fixture server running, open `http://127.0.0.1:5286/lectures.html`
  and click **Run lecture checks**. This test-only page extracts the actual lecture
  form markup from Razor (substituting just the fixture ID and resource strings)
  and loads the real lecture/date/picker scripts. It tests POST/PUT payloads and
  validation presentation using fake API responses, never application data.
- C# DTO serialization compatibility is covered by `LectureSerializationTests` in
  `tests/ZuLms.Web.Tests` using the existing JSON converter configuration.

## Tab-session isolation and live-permission regression

- `node --test tests/frontend/session.test.mjs`: runs the actual `session.js` in a VM with
  two simulated tabs. It verifies selector pinning across reloads, query/hash decoration,
  the `X-ZU-Session` header, cross-origin refusal, mismatch recovery and absence of token
  storage.
- `powershell -NoProfile -ExecutionPolicy Bypass -File tests/frontend/run-session-api-regression.ps1`:
  creates a fresh local PostgreSQL database, seeds two fixture accounts, starts the real
  application on loopback ports 7276/5276 and shares **one cookie jar** between simulated
  tabs. It checks identity isolation, refresh/navigation rendering, role context, immediate
  visibility of a new course, instructor lecture/attendance immediately after assignment,
  isolated refresh/logout, and first-login password change with CSRF.

The script never modifies the configured application database. It refuses to run if the
isolated ports are occupied or the database host is not local. It must run against a Debug
build (`dotnet build ZuLms.sln -c Debug`). The seeded database is retained for inspection
and can be dropped manually.

