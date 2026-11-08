# Component browser results — 2026-11-01

Classification: **SIMULATED-UI**. Actual application JS and installed local plugins;
controlled fake API promises; no authenticated application, database or authorization assertions.

Fixture: `tests/frontend/`; run instructions are in its README.

| Check | EN/LTR | AR/RTL |
|---|---|---|
| Blank optional inputs / idempotent initialization | PASS | PASS |
| Strict calendar validity / leap day | PASS | PASS |
| Clear => null, valid state, change event, popup stays closed | PASS | PASS |
| Dialog-contained picker / 23:59 precision | PASS | PASS |
| Midnight / programmatic edit revalidation on reopening | PASS | PASS |
| Required prompt whitespace rejection, cancel => null, focus restoration | PASS | PASS |
| Confirm cancel => false, custom action label | PASS | PASS |
| Independent faculty-filter error after list success | PASS | PASS |
| Faculty filter retry | PASS | PASS |
| Directory stale success/failure suppression / busy state | PASS | PASS |
| Directory safe read retry | PASS | PASS |
| Notification loading / stale unread suppression | PASS | PASS |
| Read-all duplicate-write guard / visible failure | PASS | PASS |
| Read-all success refresh / empty state | PASS | PASS |

Browser URLs: `http://127.0.0.1:5286/` and `http://127.0.0.1:5286/?lang=ar`.
The fixture headings/results stay English; AR mode switches document direction,
Bootstrap and date/dialog controls. This is not a full bilingual screen audit.
AR console error check returned no messages. Intermediate 13-case screenshot worked;
final AR screenshot failed with `Screenshot needs a visible tab` after explicit focus.
Authenticated routes, full viewport coverage, actual pointer/keyboard use of every
calendar option, server date persistence and all-role flows are still pending.
