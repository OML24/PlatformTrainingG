# API Test Harness (`tools/api-tests`)

End-to-end HTTP verification suite covering every implemented `/api/v1/*` route and Razor page guard across phases 01–12.

## Files

- `run-app.ps1` — launches `ZuLms.Web` in the foreground on `https://localhost:7275`. Set database credentials, JWT and encryption keys, and the bootstrap password in environment variables first. Using `localhost` keeps this project's browser cookies separate from apps opened on `127.0.0.1`.
- `start-local.ps1` — starts the built Release application in a hidden, independent process and checks `/health`; if this project's healthy instance already owns port 7275, it reuses it. Use this for interactive testing so closing a shell does not stop the site.
- `api-test.mjs` — Node.js 20+ test runner (**322 checks across 9 groups A–I**). Writes `api-test-results.json` and `api-test-report.md` to `ZU_TEST_OUT` (defaults to this folder).
- `seed-fixtures.mjs` — idempotent seeder for the three employee fixtures (`900001` Dean, `900002` Member, `900003` Quality) plus their employee rows. It writes the app's own PBKDF2 and AES-GCM formats using the active `ZU_ENC_KEYS` key, and scopes the fixtures to the first active faculty using the same ordering as `GET /api/v1/centers` (`side_code, name_ar`).
- `sync-run.mjs` — triggers one university sync (`POST /api/v1/sync/runs`) and polls until `finishedAt` is set, printing per-source stats. Exits `0` only on `Success`.

## Required database fixtures

The application restricts employee roles (`Dean`, `Member`, `Quality`) to synced `AccountType.Employee` records (`UserService.AssignRoleAsync`); `AccountType.Local` accounts may only hold `Admin`. Run `seed-fixtures.mjs` once against a fresh isolated database to create the three employee fixtures (password from `ZU_FIXTURE_PASSWORD`, `emp_status = 13` which matches `access.eligible_emp_status_codes`) and to attach them to an active faculty center.

The bootstrap administrator is seeded automatically by `SeedRunner` when the corresponding `ZU_BOOTSTRAP_ADMIN_*` variables are set, and the local test account (`local.audit`) is created idempotently by the harness itself.

The sync and certificate groups need real data. For a full run, sync once against the university sources before executing the suite, and upload a one-page certificate template (the harness does this itself in group I).

## Running

```powershell
# Terminal 1 — set all required secret environment variables, then start the web app
powershell -NoProfile -ExecutionPolicy Bypass -File tools\api-tests\run-app.ps1

# Terminal 2 — seed fixtures (idempotent) and run the suite
node tools\api-tests\seed-fixtures.mjs
$env:ZU_TEST_OUT = "$env:TEMP\zulms-api-tests"
node tools\api-tests\api-test.mjs

# Optional — verify the real university sync reaches Success
node tools\api-tests\sync-run.mjs
```

### Environment variables

| Variable | Default | Purpose |
|---|---|---|
| `ZU_TEST_BASE` | `https://localhost:7275` | Base URL of the running app (HTTPS required: auth cookies are `Secure`). |
| `ZU_TEST_CONFIGURATION` | `Debug` | Set to `Release` to launch a previously built Release app with `run-app.ps1`. |
| `ZU_TEST_OUT` | this folder | Where `api-test-results.json` / `api-test-report.md` are written. |
| `ZU_TEST_DB` | `zu_lms` | Database targeted by `seed-fixtures.mjs`. |
| `PGHOST` / `PGPORT` / `PGUSER` / `PGPASSWORD` | `127.0.0.1` / `5432` / `postgres` / required | PostgreSQL connection for `seed-fixtures.mjs`. |
| `PG_BIN` | `C:\Program Files\PostgreSQL\18\bin` | Folder containing `psql.exe`. |
| `ZU_BOOTSTRAP_ADMIN_LOGIN` / `_PASSWORD` | `admin` / required | Admin credentials used by the suite and by `sync-run.mjs`. |
| `ZU_FIXTURE_PASSWORD` | required | Password for isolated role fixtures; shared with `seed-fixtures.mjs` and `api-test.mjs`. |
| `ZU_DB_CONNECTION`, `ZU_JWT_KEY`, `ZU_ENC_KEYS` | required | Database connection and application keys used by `run-app.ps1`; fixture encryption uses the same `ZU_ENC_KEYS`. |

## Groups

| Group | Scope | Checks |
|---|---|---:|
| A | Anonymous guards, CSRF issuance, login rejection paths, 404 envelope | 13 |
| B | Admin session, `/auth/me`, language switch, page guards for all roles | 23 |
| C | Admin API happy paths and error cases | 79 |
| D | Dean role scoped to a real faculty | 24 |
| E | Quality decision, self-registration, enrollment | 36 |
| G | Cancellation request and direct cancellation | 12 |
| H | Directory, lifecycle, course edit, user edit, credentials, recovery | 53 |
| I | Certificates, evaluation, impact, needs, pro-certs, vault, audit, notifications, new pages | 77 |
| F | Context switch, logout, CSRF lifecycle | 5 |

## Notes

- The harness sends lecture times as Amman wall-clock values (`DateTimeKind.Unspecified`) because the API converts them to UTC server-side.
- `/dean/members` and `/dean/needs` are restricted to the `Dean` role by the `03` permission matrix, so `403` for `admin` is expected and asserted.
- `.NET` tests use `ZU_TEST_PG`; set it to an isolated PostgreSQL database connection supplied through the environment before running tests.
