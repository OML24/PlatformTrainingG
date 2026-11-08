import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import vm from 'node:vm';

const source = await readFile(new URL('../../src/ZuLms.Web/wwwroot/js/session.js', import.meta.url), 'utf8');
const A = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa', B = 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb';
function tab(id, stored = null) {
  const storage = new Map(stored ? [['zu.tab-session', stored]] : []);
  const anchor = { nodeType: 1, href: '/member/courses?view=Member#progress',
    getAttribute() { return this.href; }, setAttribute(_key, value) { this.href = value; },
    matches: () => true, querySelectorAll: () => [] };
  const document = { documentElement: { nodeType: 1, style: {}, matches: () => false, querySelectorAll: () => [anchor] },
    body: { style: {} }, addEventListener() { } };
  const location = new URL('https://localhost:7275/member/dashboard' + (id ? '?zu_session=' + id : ''));
  location.replace = value => { location.redirect = value; };
  const calls = [], events = {};
  const context = { document, location, URL, Headers, Node: { ELEMENT_NODE: 1 },
    sessionStorage: { getItem: key => storage.get(key), setItem: (key, value) => storage.set(key, value) },
    crypto: { randomUUID: () => 'cccccccc-cccc-cccc-cccc-cccccccccccc' },
    MutationObserver: class { observe() { } },
    fetch: async (url, options) => { calls.push({ url, options }); return { ok: true }; },
    window: { addEventListener(name, handler) { events[name] = handler; } } };
  vm.runInNewContext(source, context);
  return { session: context.window.ZuLmsSession, storage, anchor, calls, events, location, document };
}

test('Each tab preserves its own session across reload', () => {
  const a = tab(A, A), b = tab(B, B);
  assert.equal(a.session.id(), A); assert.equal(b.session.id(), B);
  assert.equal(a.storage.get('zu.tab-session'), A); assert.equal(b.storage.get('zu.tab-session'), B);
  assert.equal(tab(A, a.storage.get('zu.tab-session')).session.id(), A);
});
test('An old history/pasted session URL cannot silently change the current tab account', async () => {
  const a = tab(B, A);
  assert.equal(a.session.id(), A);
  assert.equal(a.document.documentElement.style.visibility, 'hidden');
  assert.ok(a.location.redirect.includes('zu_session=' + A));
  await assert.rejects(a.session.fetch('/api/v1/auth/me'), /SESSION_NAVIGATION_MISMATCH/);
  assert.equal(a.calls.length, 0);
});
test('Links preserve query/hash and cannot silently switch to another session', () => {
  const a = tab(A);
  assert.equal(a.anchor.href, `/member/courses?view=Member&zu_session=${A}#progress`);
  assert.equal(a.session.url(`/member/courses?zu_session=${B}`), `/member/courses?zu_session=${A}`);
});
test('Fetch sends the tab selector in a header and retains same-origin secure-cookie transport', async () => {
  const a = tab(A), b = tab(B);
  await a.session.fetch('/api/v1/auth/me'); await b.session.fetch('/api/v1/auth/me');
  assert.equal(a.calls[0].options.headers.get('X-ZU-Session'), A);
  assert.equal(b.calls[0].options.headers.get('X-ZU-Session'), B);
  assert.equal(a.calls[0].options.credentials, 'same-origin');
  assert.equal(a.calls[0].options.cache, 'no-store');
});
test('Selectors are not sent to external origins', async () => {
  const a = tab(A);
  await assert.rejects(a.session.fetch('https://external.example/api'), /CROSS_ORIGIN_SESSION_REQUEST/);
  assert.equal(a.calls.length, 0);
  assert.equal(a.session.url('https://external.example/'), 'https://external.example/');
});
test('A successful new login only changes its own tab and persists no bearer tokens', () => {
  const a = tab(A), b = tab(B);
  a.session.activate('dddddddddddddddddddddddddddddddd');
  assert.equal(b.session.id(), B);
  assert.deepEqual([...a.storage.keys()], ['zu.tab-session']);
  assert.throws(() => a.session.activate('invalid'), /SESSION_SELECTOR_INVALID/);
});
