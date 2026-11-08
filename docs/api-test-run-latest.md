# ZU-LMS API test run

- Base URL: https://127.0.0.1:7166
- Run at: 2026-10-30T06:11:37.905Z
- Checks: **322**, passed: **322**, failed: **0**

## A. Anonymous / auth guards — 13/13 passed

| # | Check | Request | Status | Envelope | Result |
|---|---|---|---|---|---|
| 1 | root redirects to login | `GET /` | 302 | — | PASS |
| 2 | login page renders | `GET /login` | 200 | — | PASS |
| 3 | me requires authentication | `GET /api/v1/auth/me` | 401 | ok:false · UNAUTHORIZED | PASS |
| 4 | settings requires authentication | `GET /api/v1/settings` | 401 | ok:false · UNAUTHORIZED | PASS |
| 5 | available courses requires authentication | `GET /api/v1/courses/available` | 401 | ok:false · UNAUTHORIZED | PASS |
| 6 | hints require authentication | `GET /api/v1/hints?context=course` | 401 | ok:false · UNAUTHORIZED | PASS |
| 7 | context requires authentication | `GET /api/v1/context` | 401 | ok:false · UNAUTHORIZED | PASS |
| 8 | admin page requires authentication | `GET /admin/dashboard` | 401 | — | PASS |
| 9 | csrf token issued | `GET /api/v1/auth/csrf` | 200 | ok:true | PASS |
| 10 | login without CSRF is rejected | `POST /api/v1/auth/login (no token)` | 400 | ok:false · CSRF_INVALID | PASS |
| 11 | login with wrong password is rejected | `POST /api/v1/auth/login (wrong pwd)` | 400 | ok:false · AUTH_INVALID | PASS |
| 12 | empty login payload is rejected | `POST /api/v1/auth/login ({})` | 400 | ok:false · AUTH_INVALID | PASS |
| 13 | unknown API returns 404 envelope | `GET /api/v1/does-not-exist` | 404 | ok:false · NOT_FOUND | PASS |

## B. Admin session, pages & language — 23/23 passed

| # | Check | Request | Status | Envelope | Result |
|---|---|---|---|---|---|
| 1 | admin signs in | `POST /api/v1/auth/login (admin)` | 200 | ok:true | PASS |
| 2 | me returns the session | `GET /api/v1/auth/me` | 200 | ok:true | PASS |
| 3 | switch language to English | `PATCH /api/v1/auth/language {en}` | 200 | ok:true | PASS |
| 4 | switch language back to Arabic | `PATCH /api/v1/auth/language {ar}` | 200 | ok:true | PASS |
| 5 | invalid language rejected | `PATCH /api/v1/auth/language {xx}` | 400 | ok:false · LANGUAGE_INVALID | PASS |
| 6 | language change without CSRF rejected | `PATCH /api/v1/auth/language (no token)` | 400 | ok:false · CSRF_INVALID | PASS |
| 7 | page /admin/dashboard | `GET /admin/dashboard` | 200 | — | PASS |
| 8 | page /admin/users | `GET /admin/users` | 200 | — | PASS |
| 9 | page /admin/credentials | `GET /admin/credentials` | 200 | — | PASS |
| 10 | page /admin/settings | `GET /admin/settings` | 200 | — | PASS |
| 11 | page /admin/hints | `GET /admin/hints` | 200 | — | PASS |
| 12 | page /admin/sync | `GET /admin/sync` | 200 | — | PASS |
| 13 | page /admin/centers | `GET /admin/centers` | 200 | — | PASS |
| 14 | page /admin/password-policy | `GET /admin/password-policy` | 200 | — | PASS |
| 15 | page /account/password | `GET /account/password` | 200 | — | PASS |
| 16 | page /quality/dashboard | `GET /quality/dashboard` | 200 | — | PASS |
| 17 | page /quality/proposals | `GET /quality/proposals` | 200 | — | PASS |
| 18 | page /member/dashboard | `GET /member/dashboard` | 200 | — | PASS |
| 19 | page /instructor/courses | `GET /instructor/courses` | 200 | — | PASS |
| 20 | page /courses/available | `GET /courses/available` | 200 | — | PASS |
| 21 | page /dean/dashboard | `GET /dean/dashboard` | 403 | — | PASS |
| 22 | page /dean/propose | `GET /dean/propose` | 403 | — | PASS |
| 23 | page /no-such-page | `GET /no-such-page` | 404 | — | PASS |

## C. Admin API happy paths & errors — 79/79 passed

| # | Check | Request | Status | Envelope | Result |
|---|---|---|---|---|---|
| 1 | list settings | `GET /api/v1/settings` | 200 | ok:true | PASS |
| 2 | update setting (unchanged value) | `PUT /api/v1/settings/ui.default_page_size` | 200 | ok:true | PASS |
| 3 | stale setting version conflicts | `PUT /api/v1/settings/ui.default_page_size (stale)` | 409 | ok:false · CONFLICT | PASS |
| 4 | unknown setting is 404 | `PUT /api/v1/settings/does.not.exist` | 404 | ok:false · SETTING_NOT_FOUND | PASS |
| 5 | unknown settings route is 404 | `GET /api/v1/settings/nope` | 404 | ok:false · NOT_FOUND | PASS |
| 6 | read password policy | `GET /api/v1/password-policy` | 200 | ok:true | PASS |
| 7 | update password policy | `PUT /api/v1/password-policy` | 200 | ok:true | PASS |
| 8 | list blocked passwords | `GET /api/v1/password-policy/blocked` | 200 | ok:true | PASS |
| 9 | add blocked password | `POST /api/v1/password-policy/blocked` | 200 | ok:true | PASS |
| 10 | duplicate blocked password conflicts | `POST /api/v1/password-policy/blocked (again)` | 409 | ok:false · BLOCKED_PASSWORD_EXISTS | PASS |
| 11 | remove blocked password | `DELETE /api/v1/password-policy/blocked/10007` | 200 | ok:true | PASS |
| 12 | removing missing blocked password is 404 | `DELETE /api/v1/password-policy/blocked/10007` | 404 | ok:false · BLOCKED_PASSWORD_NOT_FOUND | PASS |
| 13 | list centers | `GET /api/v1/centers` | 200 | ok:true | PASS |
| 14 | update center | `PUT /api/v1/centers/69` | 200 | ok:true | PASS |
| 15 | stale center version conflicts | `PUT /api/v1/centers/69 (stale)` | 409 | ok:false · CONFLICT | PASS |
| 16 | unknown center is 404 | `PUT /api/v1/centers/999999` | 404 | ok:false · CENTER_NOT_FOUND | PASS |
| 17 | list sync sides | `GET /api/v1/sync/sides` | 200 | ok:true | PASS |
| 18 | list sync runs | `GET /api/v1/sync/runs` | 200 | ok:true | PASS |
| 19 | list sync reviews | `GET /api/v1/sync/reviews` | 200 | ok:true | PASS |
| 20 | missing review is 404 | `POST /api/v1/sync/reviews/999999/resolve` | 404 | ok:false · REVIEW_NOT_FOUND | PASS |
| 21 | missing sync run is 404 | `GET /api/v1/sync/runs/999999` | 404 | ok:false · SYNC_RUN_NOT_FOUND | PASS |
| 22 | start sync run answers with an envelope | `POST /api/v1/sync/runs` | 202 | ok:true | PASS |
| 23 | list faculties | `GET /api/v1/users/faculties` | 200 | ok:true | PASS |
| 24 | credentials directory | `GET /api/v1/users/credentials-directory` | 200 | ok:true | PASS |
| 25 | weak password rejected on user create | `POST /api/v1/users/local (weak)` | 400 | ok:false · LOGIN_INVALID | PASS |
| 26 | create local user | `POST /api/v1/users/local` | 200 | ok:true | PASS |
| 27 | duplicate login conflicts | `POST /api/v1/users/local (duplicate)` | 409 | ok:false · LOGIN_EXISTS | PASS |
| 28 | fixture user local.audit exists | `GET /api/v1/users/credentials-directory?search=local.audit` | 409 | ok:true | PASS |
| 29 | fixture user 900001 exists | `GET /api/v1/users/credentials-directory?search=900001` | 409 | ok:true | PASS |
| 30 | fixture user 900002 exists | `GET /api/v1/users/credentials-directory?search=900002` | 409 | ok:true | PASS |
| 31 | fixture user 900003 exists | `GET /api/v1/users/credentials-directory?search=900003` | 409 | ok:true | PASS |
| 32 | deactivate user | `POST /api/v1/users/8297/deactivate` | 200 | ok:true | PASS |
| 33 | restore user | `POST /api/v1/users/8297/restore` | 200 | ok:true | PASS |
| 34 | unknown user is 404 | `GET /api/v1/users/999999` | 404 | ok:false · USER_NOT_FOUND | PASS |
| 35 | assign Dean role scoped to faculty | `PUT /api/v1/users/8298/roles (Dean)` | 200 | ok:true | PASS |
| 36 | assign Member role | `PUT /api/v1/users/8299/roles (Member)` | 200 | ok:true | PASS |
| 37 | assign Quality role | `PUT /api/v1/users/8300/roles (Quality)` | 200 | ok:true | PASS |
| 38 | local account cannot take an employee role | `PUT /api/v1/users/8297/roles (Dean)` | 400 | ok:false · ROLE_NOT_ASSIGNABLE | PASS |
| 39 | list user permissions | `GET /api/v1/users/8297/permissions` | 200 | ok:true | PASS |
| 40 | set permission override | `PUT /api/v1/users/8297/permissions/page.admin.users` | 200 | ok:true | PASS |
| 41 | clear permission override | `PUT /api/v1/users/8297/permissions/page.admin.users` | 200 | ok:true | PASS |
| 42 | hints by context | `GET /api/v1/hints?context=course` | 200 | ok:true | PASS |
| 43 | hints catalogue | `GET /api/v1/hints` | 200 | ok:true | PASS |
| 44 | create hint | `POST /api/v1/hints` | 201 | ok:true | PASS |
| 45 | duplicate hint conflicts | `POST /api/v1/hints (duplicate)` | 409 | ok:false · HINT_EXISTS | PASS |
| 46 | hint validation errors | `POST /api/v1/hints ({})` | 400 | ok:false · VALIDATION_FAILED | PASS |
| 47 | bad CSRF token is rejected | `POST /api/v1/hints (bad token)` | 400 | ok:false · CSRF_INVALID | PASS |
| 48 | update hint | `PUT /api/v1/hints/63` | 200 | ok:true | PASS |
| 49 | deactivate hint | `DELETE /api/v1/hints/63` | 200 | ok:true | PASS |
| 50 | deactivating missing hint is 404 | `DELETE /api/v1/hints/63` | 404 | ok:false · HINT_NOT_FOUND | PASS |
| 51 | list proposal faculties | `GET /api/v1/proposals/faculties` | 200 | ok:true | PASS |
| 52 | list pending proposals | `GET /api/v1/proposals/pending` | 200 | ok:true | PASS |
| 53 | search external instructors | `GET /api/v1/proposals/external-instructors` | 200 | ok:true | PASS |
| 54 | missing proposal is 404 | `GET /api/v1/proposals/999999` | 404 | ok:false · COURSE_NOT_FOUND | PASS |
| 55 | admin cannot propose without permission | `POST /api/v1/proposals (admin)` | 403 | ok:false · FORBIDDEN | PASS |
| 56 | direct course lookups | `GET /api/v1/courses/direct/lookups` | 200 | ok:true | PASS |
| 57 | direct course instructor search | `GET /api/v1/courses/direct/instructors` | 200 | ok:true | PASS |
| 58 | create direct course | `POST /api/v1/courses/direct` | 201 | ok:true | PASS |
| 59 | missing file is 404 | `GET /api/v1/files/999999` | 404 | ok:false · FILE_NOT_FOUND | PASS |
| 60 | upload a CV file | `POST /api/v1/files (pdf)` | 201 | ok:true | PASS |
| 61 | upload without CSRF is rejected | `POST /api/v1/files (bad token)` | 400 | ok:false · CSRF_INVALID | PASS |
| 62 | upload without kind is rejected | `POST /api/v1/files (no kind)` | 400 | ok:false · FILE_KIND_INVALID | PASS |
| 63 | download uploaded file | `GET /api/v1/files/36` | 200 | — | PASS |
| 64 | list pending cancellations | `GET /api/v1/cancellations/pending` | 200 | ok:true | PASS |
| 65 | admin cannot request cancellation | `POST /api/v1/courses/19/cancellation/request` | 403 | ok:false · FORBIDDEN | PASS |
| 66 | admin cannot cancel directly | `POST /api/v1/courses/19/cancellation/direct` | 403 | ok:false · FORBIDDEN | PASS |
| 67 | employee search | `GET /api/v1/participants/employees` | 200 | ok:true | PASS |
| 68 | list course participants | `GET /api/v1/courses/19/participants` | 200 | ok:true | PASS |
| 69 | add named participant | `POST /api/v1/courses/19/participants` | 200 | ok:true | PASS |
| 70 | remove participant | `DELETE /api/v1/courses/19/participants/8299` | 200 | ok:true | PASS |
| 71 | preview bulk initial credentials | `GET /api/v1/credentials/send-initial/preview` | 200 | ok:true | PASS |
| 72 | queue bulk initial credentials | `POST /api/v1/credentials/send-initial/bulk` | 200 | ok:true | PASS |
| 73 | reauthentication rejects wrong password | `POST /api/v1/credentials/reauthenticate (wrong)` | 400 | ok:false · AUTH_INVALID | PASS |
| 74 | reauthentication accepts the password | `POST /api/v1/credentials/reauthenticate` | 200 | ok:true | PASS |
| 75 | reveal credentials after reauth | `GET /api/v1/credentials/8299/reveal` | 200 | ok:true | PASS |
| 76 | queue initial credentials for one user | `POST /api/v1/credentials/8299/send-initial` | 400 | ok:false · INITIAL_NOT_ELIGIBLE | PASS |
| 77 | list external account requests | `GET /api/v1/external-instructor-requests` | 200 | ok:true | PASS |
| 78 | suggest external login | `GET /api/v1/external-instructor-requests/suggest-login` | 200 | ok:true | PASS |
| 79 | missing external request is 404 | `POST /api/v1/external-instructor-requests/999999/complete` | 404 | ok:false · EXTERNAL_REQUEST_NOT_FOUND | PASS |

## D. Dean role (scoped faculty) — 24/24 passed

| # | Check | Request | Status | Envelope | Result |
|---|---|---|---|---|---|
| 1 | dean signs in | `POST /api/v1/auth/login (900001)` | 200 | ok:true | PASS |
| 2 | dean session lists role | `GET /api/v1/auth/me` | 200 | ok:true | PASS |
| 3 | dean page /dean/dashboard | `GET /dean/dashboard` | 200 | — | PASS |
| 4 | dean page /dean/propose | `GET /dean/propose` | 200 | — | PASS |
| 5 | dean page /dean/courses | `GET /dean/courses` | 200 | — | PASS |
| 6 | dean page /courses/available | `GET /courses/available` | 200 | — | PASS |
| 7 | dean page /admin/dashboard | `GET /admin/dashboard` | 403 | — | PASS |
| 8 | dean page /admin/users | `GET /admin/users` | 403 | — | PASS |
| 9 | dean page /quality/proposals | `GET /quality/proposals` | 403 | — | PASS |
| 10 | dean page /quality/cancellations | `GET /quality/cancellations` | 403 | — | PASS |
| 11 | dean page /admin/hints | `GET /admin/hints` | 403 | — | PASS |
| 12 | dean page /account/password | `GET /account/password` | 200 | — | PASS |
| 13 | dean lists own proposals | `GET /api/v1/proposals/mine` | 200 | ok:true | PASS |
| 14 | dean proposal lookups | `GET /api/v1/proposals/dean-lookups` | 200 | ok:true | PASS |
| 15 | dean searches faculty members | `GET /api/v1/proposals/faculty-members` | 200 | ok:true | PASS |
| 16 | dean cannot read quality queue | `GET /api/v1/proposals/pending` | 403 | ok:false · FORBIDDEN | PASS |
| 17 | dean cannot read settings | `GET /api/v1/settings` | 403 | ok:false · FORBIDDEN | PASS |
| 18 | dean cannot start sync | `POST /api/v1/sync/runs` | 403 | ok:false · FORBIDDEN | PASS |
| 19 | dean cannot manage hints | `POST /api/v1/hints` | 403 | ok:false · FORBIDDEN | PASS |
| 20 | dean submits a proposal | `POST /api/v1/proposals` | 201 | ok:true | PASS |
| 21 | dean reads the proposal | `GET /api/v1/proposals/20` | 200 | ok:true | PASS |
| 22 | resubmit of a pending proposal is rejected | `POST /api/v1/proposals/20/resubmit` | 400 | ok:false · VALIDATION_FAILED | PASS |
| 23 | dean cannot decide proposals | `POST /api/v1/proposals/20/decision` | 403 | ok:false · FORBIDDEN | PASS |
| 24 | dean can browse available courses | `GET /api/v1/courses/available` | 200 | ok:true | PASS |

## E. Quality decision and enrollment — 36/36 passed

| # | Check | Request | Status | Envelope | Result |
|---|---|---|---|---|---|
| 1 | quality signs in | `POST /api/v1/auth/login (900003)` | 200 | ok:true | PASS |
| 2 | quality session lists role | `GET /api/v1/auth/me` | 200 | ok:true | PASS |
| 3 | quality page /quality/dashboard | `GET /quality/dashboard` | 200 | — | PASS |
| 4 | quality page /quality/proposals | `GET /quality/proposals` | 200 | — | PASS |
| 5 | quality page /quality/courses | `GET /quality/courses` | 200 | — | PASS |
| 6 | quality page /quality/cancellations | `GET /quality/cancellations` | 200 | — | PASS |
| 7 | quality page /member/dashboard | `GET /member/dashboard` | 200 | — | PASS |
| 8 | quality page /admin/dashboard | `GET /admin/dashboard` | 403 | — | PASS |
| 9 | quality page /dean/dashboard | `GET /dean/dashboard` | 403 | — | PASS |
| 10 | quality page /dean/courses | `GET /dean/courses` | 403 | — | PASS |
| 11 | quality page /admin/hints | `GET /admin/hints` | 403 | — | PASS |
| 12 | quality page /account/password | `GET /account/password` | 200 | — | PASS |
| 13 | quality cannot read settings | `GET /api/v1/settings` | 403 | ok:false · FORBIDDEN | PASS |
| 14 | quality cannot read credentials directory | `GET /api/v1/users/credentials-directory` | 403 | ok:false · FORBIDDEN | PASS |
| 15 | quality cannot manage hints | `POST /api/v1/hints` | 403 | ok:false · FORBIDDEN | PASS |
| 16 | quality queue contains the proposal | `GET /api/v1/proposals/pending` | 200 | ok:true | PASS |
| 17 | quality approves the proposal | `POST /api/v1/proposals/20/decision` | 200 | ok:true | PASS |
| 18 | second decision conflicts | `POST /api/v1/proposals/20/decision (again)` | 409 | ok:false · PROPOSAL_NOT_PENDING | PASS |
| 19 | approved proposal is visible | `GET /api/v1/proposals/20` | 200 | ok:true | PASS |
| 20 | member signs in | `POST /api/v1/auth/login (900002)` | 200 | ok:true | PASS |
| 21 | member session lists role | `GET /api/v1/auth/me` | 200 | ok:true | PASS |
| 22 | member sees available courses | `GET /api/v1/courses/available` | 200 | ok:true | PASS |
| 23 | member self-registers | `POST /api/v1/courses/20/self-registration` | 200 | ok:true | PASS |
| 24 | duplicate self-registration conflicts | `POST /api/v1/courses/20/self-registration (again)` | 409 | ok:false · REG_ALREADY | PASS |
| 25 | member page /member/dashboard | `GET /member/dashboard` | 200 | — | PASS |
| 26 | member page /courses/available | `GET /courses/available` | 200 | — | PASS |
| 27 | member page /member/courses | `GET /member/courses` | 200 | — | PASS |
| 28 | member page /admin/dashboard | `GET /admin/dashboard` | 403 | — | PASS |
| 29 | member page /dean/propose | `GET /dean/propose` | 403 | — | PASS |
| 30 | member page /quality/proposals | `GET /quality/proposals` | 403 | — | PASS |
| 31 | member page /account/password | `GET /account/password` | 200 | — | PASS |
| 32 | member cannot read settings | `GET /api/v1/settings` | 403 | ok:false · FORBIDDEN | PASS |
| 33 | member cannot propose courses | `POST /api/v1/proposals` | 403 | ok:false · FORBIDDEN | PASS |
| 34 | member can read field hints | `GET /api/v1/hints?context=course` | 200 | ok:true | PASS |
| 35 | member cannot read credentials | `GET /api/v1/users/credentials-directory` | 403 | ok:false · FORBIDDEN | PASS |
| 36 | member cannot manage hints | `POST /api/v1/hints` | 403 | ok:false · FORBIDDEN | PASS |

## G. Cancellation request & direct cancellation — 12/12 passed

| # | Check | Request | Status | Envelope | Result |
|---|---|---|---|---|---|
| 1 | dean uploads a cancellation letter | `POST /api/v1/files (CancellationLetter)` | 201 | ok:true | PASS |
| 2 | dean requests cancellation | `POST /api/v1/courses/20/cancellation/request` | 201 | ok:true | PASS |
| 3 | duplicate cancellation request conflicts | `POST /api/v1/courses/20/cancellation/request (again)` | 400 | ok:false · CANCELLATION_ALREADY_PENDING | PASS |
| 4 | cancellation letter is required | `POST /api/v1/courses/20/cancellation/request (no letter)` | 400 | ok:false · CANCELLATION_LETTER_REQUIRED | PASS |
| 5 | quality lists pending cancellations | `GET /api/v1/cancellations/pending` | 200 | ok:true | PASS |
| 6 | quality approves cancellation | `POST /api/v1/cancellations/4/decision` | 200 | ok:true | PASS |
| 7 | second cancellation decision conflicts | `POST /api/v1/cancellations/4/decision (again)` | 400 | ok:false · CANCELLATION_ALREADY_DECIDED | PASS |
| 8 | approved course becomes cancelled | `GET /api/v1/proposals/20` | 200 | ok:true | PASS |
| 9 | quality uploads a cancellation letter | `POST /api/v1/files (CancellationLetter)` | 201 | ok:true | PASS |
| 10 | quality cancels a course directly | `POST /api/v1/courses/19/cancellation/direct` | 200 | ok:true | PASS |
| 11 | direct cancellation of a cancelled course is rejected | `POST /api/v1/courses/19/cancellation/direct (again)` | 400 | ok:false · CANCELLATION_NOT_ALLOWED | PASS |
| 12 | unknown cancellation request is rejected | `POST /api/v1/cancellations/999999/decision` | 400 | ok:false · CANCELLATION_REQUEST_NOT_FOUND | PASS |

## H. Directory, lifecycle, user edit, credentials, recovery — 53/53 passed

| # | Check | Request | Status | Envelope | Result |
|---|---|---|---|---|---|
| 1 | directory lookups | `GET /api/v1/courses/directory/lookups` | 200 | ok:true | PASS |
| 2 | directory list | `GET /api/v1/courses/directory` | 200 | ok:true | PASS |
| 3 | directory rejects an inverted date range | `GET /api/v1/courses/directory (bad dates)` | 400 | ok:false · QUERY_INVALID | PASS |
| 4 | unknown directory course is 404 | `GET /api/v1/courses/directory/999999` | 404 | ok:false · COURSE_NOT_FOUND | PASS |
| 5 | dean directory view | `GET /api/v1/courses/directory?view=Dean` | 200 | ok:true | PASS |
| 6 | directory course details | `GET /api/v1/courses/directory/21` | 200 | ok:true | PASS |
| 7 | admin edits course details | `PUT /api/v1/courses/21/details` | 200 | ok:true | PASS |
| 8 | stale course detail edit rejected | `PUT /api/v1/courses/21/details (stale)` | 400 | ok:false · CONFLICT | PASS |
| 9 | quality edits attendance threshold | `PUT /api/v1/courses/21/attendance-threshold` | 200 | ok:true | PASS |
| 10 | invalid attendance threshold rejected | `PUT /api/v1/courses/21/attendance-threshold (101)` | 400 | ok:false · THRESHOLD_INVALID | PASS |
| 11 | quality searches course instructors | `GET /api/v1/courses/21/instructor/search` | 200 | ok:true | PASS |
| 12 | quality assigns registered instructor | `PUT /api/v1/courses/21/instructor` | 200 | ok:true | PASS |
| 13 | forced status requires a reason | `POST /api/v1/courses/21/force-status (no reason)` | 400 | ok:false · REASON_REQUIRED | PASS |
| 14 | dean cannot force course status | `POST /api/v1/courses/21/force-status (dean)` | 403 | ok:false · FORBIDDEN | PASS |
| 15 | admin forces rejected status | `POST /api/v1/courses/21/force-status (Rejected)` | 200 | ok:true | PASS |
| 16 | admin restores approved status | `POST /api/v1/courses/21/force-status (Approved)` | 200 | ok:true | PASS |
| 17 | course detail edit page renders | `GET /courses/21` | 200 | — | PASS |
| 18 | deactivation preview | `GET /api/v1/courses/21/admin/deactivation-preview` | 200 | ok:true | PASS |
| 19 | deactivation requires a reason | `POST /api/v1/courses/21/admin/deactivate (no reason)` | 400 | ok:false · REASON_REQUIRED | PASS |
| 20 | admin deactivates a course | `POST /api/v1/courses/21/admin/deactivate` | 200 | ok:true | PASS |
| 21 | double deactivation is rejected | `POST /api/v1/courses/21/admin/deactivate (again)` | 400 | ok:false · COURSE_ALREADY_DISABLED | PASS |
| 22 | admin restores a course | `POST /api/v1/courses/21/admin/restore` | 200 | ok:true | PASS |
| 23 | double restore is rejected | `POST /api/v1/courses/21/admin/restore (again)` | 400 | ok:false · COURSE_ALREADY_ACTIVE | PASS |
| 24 | admin archives a course | `POST /api/v1/courses/21/admin/archive` | 200 | ok:true | PASS |
| 25 | double archive is rejected | `POST /api/v1/courses/21/admin/archive (again)` | 400 | ok:false · COURSE_ALREADY_ARCHIVED | PASS |
| 26 | admin unarchives a course | `POST /api/v1/courses/21/admin/unarchive` | 200 | ok:true | PASS |
| 27 | reopen requires confirmation | `POST /api/v1/courses/21/admin/reopen (unconfirmed)` | 400 | ok:false · CONFIRM_REQUIRED | PASS |
| 28 | reopen of an unfinished course is rejected | `POST /api/v1/courses/21/admin/reopen` | 400 | ok:false · REOPEN_NOT_ALLOWED | PASS |
| 29 | preview of an unknown course is rejected | `GET /api/v1/courses/999999/admin/deactivation-preview` | 400 | ok:false · COURSE_NOT_FOUND | PASS |
| 30 | dean cannot deactivate courses | `POST /api/v1/courses/21/admin/deactivate (dean)` | 403 | ok:false · FORBIDDEN | PASS |
| 31 | toggle side import selection | `PUT /api/v1/sync/sides/319/imported` | 200 | ok:true | PASS |
| 32 | restore side import selection | `PUT /api/v1/sync/sides/319/imported (restore)` | 200 | ok:true | PASS |
| 33 | unknown sync side is 404 | `PUT /api/v1/sync/sides/999999/imported` | 404 | ok:false · SIDE_NOT_FOUND | PASS |
| 34 | edit user | `PUT /api/v1/users/8297` | 200 | ok:true | PASS |
| 35 | stale user edit conflicts | `PUT /api/v1/users/8297 (stale)` | 409 | ok:false · CONFLICT | PASS |
| 36 | invalid email is rejected | `PUT /api/v1/users/8297 (bad email)` | 400 | ok:false · EMAIL_INVALID | PASS |
| 37 | access override requires a reason | `PUT /api/v1/users/8297 (no reason)` | 400 | ok:false · REASON_REQUIRED | PASS |
| 38 | editing an unknown user is 404 | `PUT /api/v1/users/999999` | 404 | ok:false · USER_NOT_FOUND | PASS |
| 39 | stale credential version conflicts | `PUT /api/v1/credentials/8297/password (stale)` | 409 | ok:false · CONFLICT | PASS |
| 40 | weak password is rejected by policy | `PUT /api/v1/credentials/8297/password (weak)` | 400 | ok:false · PWD_TOO_SHORT | PASS |
| 41 | null password is a validation error, not a server error | `PUT /api/v1/credentials/8297/password (null)` | 400 | ok:false · VALIDATION_FAILED | PASS |
| 42 | credential change for unknown user is 404 | `PUT /api/v1/credentials/999999/password` | 404 | ok:false · USER_NOT_FOUND | PASS |
| 43 | stale shared-password version conflicts | `PUT /api/v1/credentials/shared-initial (stale)` | 409 | ok:false · CONFLICT | PASS |
| 44 | clear shared initial password | `PUT /api/v1/credentials/shared-initial` | 200 | ok:true | PASS |
| 45 | resolving a certain delivery is rejected | `POST /api/v1/credentials/8297/resolve-uncertain` | 400 | ok:false · NOT_UNCERTAIN | PASS |
| 46 | resolving for unknown user is 404 | `POST /api/v1/credentials/999999/resolve-uncertain` | 404 | ok:false · USER_NOT_FOUND | PASS |
| 47 | forgot password answers generically | `POST /api/v1/auth/forgot-password` | 200 | ok:true | PASS |
| 48 | empty forgot-password payload is generic | `POST /api/v1/auth/forgot-password ({})` | 200 | ok:true | PASS |
| 49 | forgot password enforces CSRF | `POST /api/v1/auth/forgot-password (bad token)` | 400 | ok:false · CSRF_INVALID | PASS |
| 50 | anonymous password change is 401 | `POST /api/v1/auth/account-password` | 401 | ok:false · UNAUTHORIZED | PASS |
| 51 | wrong current password is rejected | `POST /api/v1/auth/account-password (wrong)` | 400 | ok:false · AUTH_INVALID | PASS |
| 52 | empty password payload is rejected, not a server error | `POST /api/v1/auth/account-password ({})` | 400 | ok:false · AUTH_INVALID | PASS |
| 53 | forced-change route refuses normal sessions | `POST /api/v1/auth/change-password` | 403 | ok:false · FORBIDDEN | PASS |

## I. Certificates, needs, pro-certs, vault, audit, notifications — 77/77 passed

| # | Check | Request | Status | Envelope | Result |
|---|---|---|---|---|---|
| 1 | health endpoint reports healthy | `GET /health` | 200 | — | PASS |
| 2 | unknown reference verifies as null | `GET /api/v1/public/verify/{unknown}` | 200 | ok:true | PASS |
| 3 | upload certificate template | `POST /api/v1/certificate-template` | 200 | ok:true | PASS |
| 4 | current template is version 1+ | `GET /api/v1/certificate-template` | 200 | ok:true | PASS |
| 5 | template preview renders a PDF | `POST /api/v1/certificate-template/preview` | 200 | — | PASS |
| 6 | member registers in the certificate course | `POST /api/v1/courses/22/self-registration` | 200 | ok:true | PASS |
| 7 | admin creates a lecture | `POST /api/v1/courses/22/lectures` | 201 | ok:true | PASS |
| 8 | participants list works | `GET /api/v1/courses/22/participants` | 200 | ok:true | PASS |
| 9 | attendance roster lists participants | `GET /api/v1/lectures/4/attendance/roster` | 200 | ok:true | PASS |
| 10 | manual attendance marks the member present | `PUT /api/v1/lectures/4/attendance/roster/14` | 200 | ok:true | PASS |
| 11 | course finish succeeds | `POST /api/v1/courses/22/finish` | 200 | ok:true | PASS |
| 12 | evaluation form is served | `GET /api/v1/courses/22/evaluation` | 200 | ok:true | PASS |
| 13 | member submits the evaluation | `POST /api/v1/courses/22/evaluation` | 200 | ok:true | PASS |
| 14 | member uploads an impact file | `POST /api/v1/files (ImpactFile)` | 201 | ok:true | PASS |
| 15 | member submits the impact | `POST /api/v1/courses/22/impact` | 201 | ok:true | PASS |
| 16 | member lists own impact submissions | `GET /api/v1/courses/22/impact` | 200 | ok:true | PASS |
| 17 | impact pending queue works | `GET /api/v1/courses/22/impact/pending` | 200 | ok:true | PASS |
| 18 | impact review approves | `POST /api/v1/courses/22/impact/3/review` | 200 | ok:true | PASS |
| 19 | member lists own certificates | `GET /api/v1/certificates/mine` | 200 | ok:true | PASS |
| 20 | certificate issued automatically | `GET /api/v1/certificates/mine` | 200 | ok:true | PASS |
| 21 | public verify finds the certificate | `GET /api/v1/public/verify/{ref}` | 200 | ok:true | PASS |
| 22 | verification shows the holder | `GET /api/v1/public/verify/{ref}` | 200 | ok:true | PASS |
| 23 | member downloads the certificate PDF | `GET /api/v1/certificates/5/pdf` | 200 | — | PASS |
| 24 | unknown certificate pdf is 404 | `GET /api/v1/certificates/{unknown}/pdf` | 404 | ok:false · CERT_NOT_FOUND | PASS |
| 25 | quality lists certificates | `GET /api/v1/certificates?search=` | 200 | ok:true | PASS |
| 26 | admin revokes the certificate | `POST /api/v1/certificates/5/revoke` | 200 | ok:true | PASS |
| 27 | revoked certificate verifies as revoked | `GET /api/v1/public/verify/{ref}` | 200 | ok:true | PASS |
| 28 | evaluation results include the response | `GET /api/v1/evaluation/results/22` | 200 | ok:true | PASS |
| 29 | evaluation period extension works | `PUT /api/v1/evaluation/period/22` | 200 | ok:true | PASS |
| 30 | dean uploads the need letter | `POST /api/v1/files (NeedLetter)` | 201 | ok:true | PASS |
| 31 | dean submits a training need | `POST /api/v1/needs` | 201 | ok:true | PASS |
| 32 | quality lists pending needs | `GET /api/v1/needs?status=Submitted` | 200 | ok:true | PASS |
| 33 | quality approves the need | `POST /api/v1/needs/5/decision` | 200 | ok:true | PASS |
| 34 | dean sees the approved need | `GET /api/v1/needs` | 200 | ok:true | PASS |
| 35 | second need decision is rejected | `POST /api/v1/needs/5/decision (again)` | 400 | ok:false · NEED_NOT_PENDING | PASS |
| 36 | pro-cert categories are listed | `GET /api/v1/procerts/categories` | 200 | ok:true | PASS |
| 37 | member uploads the pro-cert file | `POST /api/v1/files (ProfessionalCertificate)` | 201 | ok:true | PASS |
| 38 | member submits a professional certificate | `POST /api/v1/procerts` | 201 | ok:true | PASS |
| 39 | duplicate pro-cert is rejected | `POST /api/v1/procerts (duplicate)` | 400 | ok:false · PROCERT_DUPLICATE | PASS |
| 40 | member lists own pro-certs | `GET /api/v1/procerts` | 200 | ok:true | PASS |
| 41 | quality approves the pro-cert | `POST /api/v1/procerts/5/decision` | 200 | ok:true | PASS |
| 42 | approved pro-cert appears in the profile | `GET /api/v1/profiles/8299` | 200 | ok:true | PASS |
| 43 | pro-cert statistics are served | `GET /api/v1/procerts/stats` | 200 | ok:true | PASS |
| 44 | pro-cert statistics export CSV | `GET /api/v1/procerts/stats/export` | 200 | — | PASS |
| 45 | quality uploads minutes file | `POST /api/v1/files (QualityMinutes)` | 201 | ok:true | PASS |
| 46 | quality links a vault document | `POST /api/v1/vault` | 200 | ok:true | PASS |
| 47 | vault lists the document | `GET /api/v1/vault` | 200 | ok:true | PASS |
| 48 | audit log is listed | `GET /api/v1/audit` | 200 | ok:true | PASS |
| 49 | live feed returns rows | `GET /api/v1/audit/live?afterId=0` | 200 | ok:true | PASS |
| 50 | audit CSV export | `GET /api/v1/audit/export` | 200 | — | PASS |
| 51 | quality cannot read the audit log | `GET /api/v1/audit (quality)` | 403 | ok:false · FORBIDDEN | PASS |
| 52 | notifications are listed | `GET /api/v1/notifications` | 200 | ok:true | PASS |
| 53 | member has unread notifications | `GET /api/v1/notifications` | 200 | ok:true | PASS |
| 54 | mark all notifications read | `POST /api/v1/notifications/read-all` | 200 | ok:true | PASS |
| 55 | tracks are listed | `GET /api/v1/tracks` | 200 | ok:true | PASS |
| 56 | quality creates a track | `POST /api/v1/tracks` | 200 | ok:true | PASS |
| 57 | members directory is served | `GET /api/v1/profiles/directory` | 200 | ok:true | PASS |
| 58 | page /admin/audit | `GET /admin/audit` | 200 | — | PASS |
| 59 | page /admin/live-feed | `GET /admin/live-feed` | 200 | — | PASS |
| 60 | page /admin/needs | `GET /admin/needs` | 200 | — | PASS |
| 61 | page /admin/certificate-template | `GET /admin/certificate-template` | 200 | — | PASS |
| 62 | page /vault | `GET /vault` | 200 | — | PASS |
| 63 | page /quality/needs | `GET /quality/needs` | 200 | — | PASS |
| 64 | page /quality/eval-form | `GET /quality/eval-form` | 200 | — | PASS |
| 65 | page /quality/evaluations | `GET /quality/evaluations` | 200 | — | PASS |
| 66 | page /quality/tracks | `GET /quality/tracks` | 200 | — | PASS |
| 67 | page /quality/certificates | `GET /quality/certificates` | 200 | — | PASS |
| 68 | page /quality/pro-certs | `GET /quality/pro-certs` | 200 | — | PASS |
| 69 | page /quality/pro-cert-stats | `GET /quality/pro-cert-stats` | 200 | — | PASS |
| 70 | page /member/pro-certs | `GET /member/pro-certs` | 200 | — | PASS |
| 71 | page /member/impact | `GET /member/impact` | 200 | — | PASS |
| 72 | page /member/teaching | `GET /member/teaching` | 200 | — | PASS |
| 73 | page /member/enter-code | `GET /member/enter-code` | 200 | — | PASS |
| 74 | page /dean/members | `GET /dean/members` | 403 | — | PASS |
| 75 | page /shared/directory | `GET /shared/directory` | 200 | — | PASS |
| 76 | page /dean/needs | `GET /dean/needs` | 403 | — | PASS |
| 77 | page /verify | `GET /verify` | 200 | — | PASS |

## F. Context switch, logout, CSRF lifecycle — 5/5 passed

| # | Check | Request | Status | Envelope | Result |
|---|---|---|---|---|---|
| 1 | context lists admin choices | `GET /api/v1/context` | 200 | ok:true | PASS |
| 2 | context switch to Admin | `POST /api/v1/context {Admin}` | 200 | ok:true | PASS |
| 3 | logout succeeds | `POST /api/v1/auth/logout` | 200 | ok:true | PASS |
| 4 | session is gone after logout | `GET /api/v1/auth/me` | 401 | ok:false · UNAUTHORIZED | PASS |
| 5 | refresh without a token is 401 | `POST /api/v1/auth/refresh` | 401 | ok:false · UNAUTHORIZED | PASS |

