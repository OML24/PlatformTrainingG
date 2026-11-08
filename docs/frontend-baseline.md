# Frontend baseline — 2026-10-31

- Source SHA-256 manifest: `frontend-evidence/20261031/source-baseline.json`; excludes bin/obj/logs. The parent Git repository treats this project as untracked.
- Release build before changes: PASS, 0 warnings, 0 errors.
- Isolated runtime: `https://localhost:7275`, database `zulms_frontend_20261031`. Existing university database was not used. Startup uses existing migration/seed code without modifying it; temporary startup script is outside the repository. Credentials are not included in evidence.
- Browser: desktop embedded Chromium; measured viewport 912 × 718 CSS pixels. Screenshot pixels reflect device scaling, not viewport dimensions.
- Initial unavailable-server navigation was resolved by starting the isolated application.
- Captured login EN and admin dashboard AR before application source edits. Full per-route AR/EN and 390/1440 baseline coverage is still pending; these two captures do not satisfy full F0 acceptance.
- `frontend-contracts.csv` is an extracted source inventory, not a claim that every serializer has been manually reviewed. Rows requiring review say so explicitly.
- `frontend-page-matrix.csv` initially lists implemented Razor pages. Required-route reconciliation and page-specific evidence remain pending.

## Existing findings

1. Direct course form IDs `direct-form/direct-error/direct-success` disagree with the JavaScript `d-*` resolver.
2. Profile contains inline JavaScript; CSP blocks this implementation.
3. Sidebar includes multiple role contexts and uses highest-permission role labels.
4. Layout loads prototype base copy but not the experience refinements; scaffold scoped CSS overrides link colors.
5. Date pickers increment by five minutes and may populate optional blank fields; standalone time support is missing.
6. Toast host is duplicated between shell and API.
7. English page coverage is incomplete, including navigation and generated page text.
