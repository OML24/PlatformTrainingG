# First-login password change: CSRF_INVALID

## Cause

The password-change-only identity (`pwd_change=true`) was blocked by
`ForcedPasswordChangeMiddleware` from GET `/api/v1/auth/csrf`. The change-password
page has no rendered CSRF meta token, so `api.js` must fetch a token for the current
identity before posting the new password. The rejected token fetch was surfaced
as `CSRF_INVALID`; the password-change operation had not run.

The same allowlist also blocked `/img/logo.png`, redirecting the image request to
the password-change page and producing the broken logo shown in the report.

## Narrow fix

For the user's subsequent explicit bug-fix request, changed the server middleware
in `src/ZuLms.Web/Security/AuthHttp.cs` to allow these two exact paths. This is a
separate security bug fix, not part of the earlier frontend-only source comparison.
No CSRF validation was removed or bypassed, no password policy or permission was
changed, and no account password or database record was edited for testing.

## Verification

- Added `ForcedPasswordChangeMiddlewareTests`: 15 regression cases.
- Before fix: CSRF endpoint and logo dependency cases failed; 12 cases passed.
- After fix: all 15 regression cases pass, including real antiforgery token
  generation/validation with ephemeral keys and rejection of a forged token.
- Limited-session API restrictions (including `/auth/me`, `/auth/refresh` and a
  path nested below `/auth/csrf`) and dashboard redirection remain tested.
- Release build: 0 warnings, 0 errors.
- Release solution tests excluding `PostgresSeedTests`: **84 passed**.
  Docker-dependent seed integration was excluded, not counted as passing.
- Restarted this project's Release server at `https://localhost:7275/login`.

No real member password was changed by the agent. The user should reload the page
and retry; if the limited session has expired, sign in again using the existing
temporary password.
