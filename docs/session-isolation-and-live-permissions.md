# Live permissions and per-tab session isolation — 2026-11-01

## Reported problems

1. An instructor could create a lecture but opening its attendance roster returned a
   permission error. Leaving the site and signing in again fixed it. A member could not
   see a newly created course until signing out and in again.
2. When two accounts were signed in on the same computer, navigating or refreshing a tab
   could switch it to the other account and show that account's data.

## Root causes

### 1. Permissions were cached across requests for 30 seconds

`PermissionService` kept a `static ConcurrentDictionary` keyed only by user id with a
30-second lifetime. The first request after a role, course-assignment or override change
therefore reused the old effective permission set. `/api/v1/auth/me` also returned the
role claims frozen in the JWT at sign-in time instead of the effective roles.

### 2. One cookie namespace was shared by every tab

Authentication, refresh, role-context and language cookies had global names
(`zu_at`, `zu_rt`, `zu_ctx`, `zu_lang`). Signing in to a second account in another tab
overwrote those cookies, so the first tab silently became the second account on its next
request. A refresh or navigation could show another user's data.

## Fixes

### Live effective permissions

- `PermissionService` now keeps only a request-scoped cache. Every HTTP request resolves
  roles, role permissions, instructor status and overrides from the database, so a new
  assignment is effective on the very next request, without sign-out or waiting.
- `/api/v1/auth/me` returns the effective role codes from the same calculation.
- Authorization policies and page/sidebar visibility already used the service; they now
  see the new state immediately.
- The 30-second `IMemoryCache` entry for account active/stamp/status remains. It does not
  cache permissions; it only avoids a database read of immutable session facts.

### Per-tab session namespace

- A tab selector is an opaque 32-character hexadecimal value (`zu_session`). It is **not**
  an authentication token: authentication still requires the HttpOnly, Secure JWT cookie
  and full JWT/user validation.
- Every session cookie is suffixed with the selector:
  `zu_at_<id>`, `zu_rt_<id>`, `zu_ctx_<id>`, `zu_lang_<id>`.
- The selector is read from the `X-ZU-Session` header or the `zu_session` query value.
  - No selector: API calls are unauthenticated; private HTML pages receive a small
    bootstrap page that resolves the tab selector before any private data is rendered.
  - Invalid or conflicting header/query values: `400 SESSION_SELECTOR_INVALID`.
  - There is no fallback to a global cookie, the last login or an `Authorization` header.
- Successful login always creates a new selector and cookie namespace, even when the login
  page was opened from an already authenticated tab.
- Refresh and logout only touch the requesting tab's namespace, so they cannot sign out or
  switch another account.
- `session.js` pins the selector in `sessionStorage`, appends it to same-origin links,
  adds the header to every same-origin `fetch`, rejects cross-origin session requests and
  refuses a history/pasted URL whose selector differs from the tab's pinned selector.
  Back-forward-cache restores force a reload so no stale page is shown.
- Server-side redirects keep the selector. Private responses are sent with
  `Cache-Control: no-cache, no-store` and `Pragma: no-cache`.
- A signed-in user who opens a page outside their permissions receives the existing
  localized `/access-denied` page instead of a blank 403 document. API responses keep
  their status/JSON envelope.

### Password policy error found while testing

The blocked-context check treated every name fragment, including single-letter initials
(e.g. `C`), as forbidden content. Any password containing that letter was rejected with
`PWD_CONTAINS_CONTEXT`. Context tokens shorter than three characters are now ignored;
longer name/login/initial-password fragments are still rejected.

## Verification

| Check | Result |
|---|---|
| `dotnet build ZuLms.sln -c Release` | 0 warnings, 0 errors |
| `dotnet test ZuLms.sln -c Debug --filter 'FullyQualifiedName!~PostgresSeedTests'` | 121 passed, 0 failed |
| `tests/frontend/session.test.mjs` + lecture date tests | 24 passed |
| Isolated real-API regression (`tests/frontend/run-session-api-regression.ps1`) | 9/9 passed |

The isolated regression starts a separate database and application instance, shares one
cookie jar between simulated tabs (as a real browser does), and verifies:

1. two logins in one cookie jar keep distinct identities;
2. a query-only page refresh renders the selected account and no other account's data;
3. missing, unknown and conflicting selectors never fall back to another account;
4. role context stays isolated;
5. a forbidden page returns the localized access-denied redirect inside the same tab
   instead of a blank document;
6. a member already signed in sees a newly created course on the next request;
7. a new instructor can create a lecture and open its roster on the next request, without
   a new login or JWT;
8. refresh and logout of an older session neither switch nor sign out another account;
9. first-login password change gets a valid CSRF token and stays in its own session.

Browser verification on the running application at `https://localhost:7275`:

- signed in as `admin` in one tab and `900002` in another tab of the same browser profile;
- refreshed and navigated both tabs repeatedly: each kept its own user and never rendered
  the other user's shell;
- signed out the member tab: only that tab returned to `/login`; the admin tab remained
  signed in after a refresh;
- no console errors on the sign-in, admin and member pages.

The Docker-only `PostgresSeedTests` was excluded because Docker is unavailable on this
host; it is not counted as passing.

## Contract changes for non-browser clients

Browser usage is automatic. A script or integration that signs in through the API must:

1. read `sessionId` from the login response;
2. send it as `X-ZU-Session: <sessionId>` on every subsequent request (or as
   `?zu_session=<sessionId>`);
3. keep its cookie jar, because the suffixed HttpOnly cookies are still required.

`tools/api-tests/api-test.mjs` and `tools/api-tests/sync-run.mjs` were updated accordingly.

## Limitations / deliberate choices

- JavaScript is required for normal navigation; links are decorated at runtime. The
  bootstrap page and server-side redirects still fail closed (login page) without data.
- A bookmark or pasted link cannot carry a session without its cookies; it opens the login
  page instead of borrowing another tab's account.
- Each separate login creates three session cookies plus an optional language cookie. They
  are session cookies unless "remember me" is selected and disappear when the browser
  closes; there is no fixed cap on simultaneously open tabs.
- First-login password change still requires the existing CSRF token; the allowlist for
  `/api/v1/auth/csrf` and `/img` is unchanged.

## Files changed

- `src/ZuLms.Application/Features/Permissions/PermissionService.cs`
- `src/ZuLms.Web/Security/TabSession.cs` (new)
- `src/ZuLms.Web/Security/AuthHttp.cs`
- `src/ZuLms.Web/Security/AuthEndpoints.cs`
- `src/ZuLms.Web/Security/ContextEndpoints.cs`
- `src/ZuLms.Web/Localization/RequestCultureFromUserProvider.cs`
- `src/ZuLms.Web/Program.cs`
- `src/ZuLms.Web/wwwroot/js/session.js` (new)
- `src/ZuLms.Web/wwwroot/js/session-bootstrap.js` (new)
- `src/ZuLms.Web/wwwroot/js/api.js`, `auth.js`, `site.js`, `role-switcher.js`, `i18n.js`,
  `member-certificates.js`, `certificate-template.js`
- `src/ZuLms.Web/Pages/Shared/_Layout.cshtml` and the public/account pages that include
  `session.js`
- `src/ZuLms.Domain/Rules/PasswordPolicyRules.cs`
- `tests/ZuLms.Web.Tests/TabSessionTests.cs` (new), `AuthHttpTests.cs`
- `tests/ZuLms.Domain.Tests/PolicyAndFileTests.cs`
- `tests/ZuLms.Application.Tests/OrganizationMergePostgresTests.cs` (permission freshness
  assertion)
- `tests/frontend/session.test.mjs` (new), `session-api-regression.mjs` (new),
  `run-session-api-regression.ps1` (new)
- `tools/api-tests/api-test.mjs`, `tools/api-tests/sync-run.mjs`

## Reproduce

```powershell
# build and start the real application
dotnet build ZuLms.sln -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File tools/api-tests/start-local.ps1 -Configuration Release
# open https://localhost:7275/login

# isolated two-tab session + live-permission regression (separate database and port)
dotnet build ZuLms.sln -c Debug
powershell -NoProfile -ExecutionPolicy Bypass -File tests/frontend/run-session-api-regression.ps1

# static tab-selector checks
node --test tests/frontend/session.test.mjs
```
