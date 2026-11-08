import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import vm from 'node:vm';

const source = await readFile(new URL('../../src/ZuLms.Web/wwwroot/js/dates.js', import.meta.url), 'utf8');
function dates(language = 'ar') {
  const context = { document: { documentElement: { lang: language } }, window: {} };
  vm.runInNewContext(source, context);
  return context.window.ZuLmsDates;
}

for (const language of ['ar', 'en']) {
  for (const [input, expected] of [
    ['2026-11-01 09:33', '2026-11-01T09:33:00'],
    ['2026-11-01 09:43', '2026-11-01T09:43:00'],
    ['2026-11-01 23:59', '2026-11-01T23:59:00'],
    ['2026-11-02 00:00', '2026-11-02T00:00:00'],
    [' 2026-11-02 00:01 ', '2026-11-02T00:01:00'],
    ['', null], ['  ', null], [null, null]
  ]) {
    test(`${language}: local picker ${JSON.stringify(input)} => ${expected}`, () => {
      assert.equal(dates(language).localDateTime(input), expected);
    });
  }
  test(`${language}: editing an Amman instant preserves wall-clock minute in local DTO`, () => {
    const helper = dates(language);
    assert.equal(helper.localDateTime(helper.input('2026-11-01T06:33:00Z')), '2026-11-01T09:33:00');
  });
}
