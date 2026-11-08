# Lecture creation/edit: VALIDATION_FAILED

## Cause and fix

`LectureInput` and `LectureScheduleInput` use `DateTime` (Amman wall-clock,
`Kind=Unspecified`). The picker displays `yyyy-MM-dd HH:mm`, but the default
System.Text.Json DateTime reader requires ISO local text, e.g.
`2026-11-01T09:33:00`. The installed `AmmanDateTimeOffsetConverter` does not apply
to these DateTime properties, so sending picker text directly fails model binding.

Both POST creation and PUT editing now serialize the picker value using the new
`ZuLmsDates.localDateTime` helper. It inserts `T` and zero seconds without using
`Date`, UTC conversion, `Z`, or any offset. The display format stays unchanged;
date-only and DateTimeOffset endpoint contracts are untouched. Date inputs now
explicitly use `dir=ltr` within the Arabic page.

The form adds names matching its existing JSON payload keys. Error presentation
now prefers readable messages, supplies a localized validation heading, shows all
returned field details as text with associated labels, marks/focuses the failing
field, retains inputs after failure, and clears stale validation markers on edits
and retries. No backend production source, DTO, permissions or database record was
changed for this fix.

## Checks

- `node --test tests/frontend/lecture-dates.test.mjs`: **18 passed**.
- `LectureSerializationTests`: 4 cases, reproducing rejection of the old format
  and acceptance of create/edit ISO local values at 09:33, 23:59 and midnight with
  `DateTimeKind.Unspecified` preserved.
- Browser fixture `tests/frontend/lectures.html`: **5/5 passed**, using actual
  lecture Razor form markup and JS with simulated API responses. Checked create
  and edit payloads, server field-error labels/details, safe text rendering,
  retained input, corrected-field/retry state and readable-message precedence.
- These browser checks are SIMULATED-UI, not a real database save or authorization
  test. No real lecture was created during automated testing.
- Final Release build: 0 warnings/errors. JS syntax and unique/paired AR/EN RESX
  keys passed. Release .NET suite excluding Docker-dependent `PostgresSeedTests`:
  **88 passed**; that excluded integration test is not counted as passing.
- Restarted this project's Release server successfully on ports 7275/5275.

Reproduction instructions are in `tests/frontend/README.md`. After restarting
the Release server, reload the real lecture page before retrying.
