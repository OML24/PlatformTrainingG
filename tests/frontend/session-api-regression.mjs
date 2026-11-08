// Run only against the explicitly prepared, isolated session-test application.
// Shares ONE cookie jar between all simulated tabs to reproduce a real browser.
import assert from 'node:assert/strict';

const base = process.env.ZU_SESSION_TEST_BASE;
const password = process.env.ZU_SESSION_TEST_PASSWORD;
if (!base || !new URL(base).hostname.match(/^(localhost|127\.0\.0\.1)$/) || !password)
  throw new Error('Set ZU_SESSION_TEST_BASE (loopback only) and ZU_SESSION_TEST_PASSWORD for the isolated fixture.');
const cookies = new Map();
const checks = [];
async function call(session, method, path, body, accept = 'application/json', header = true) {
  const headers = { Accept: accept, Cookie: [...cookies].map(([k, v]) => `${k}=${v}`).join('; ') };
  if (session && header) headers['X-ZU-Session'] = session;
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (!['GET', 'HEAD'].includes(method)) {
    const token = await call(session, 'GET', '/api/v1/auth/csrf');
    assert.equal(token.status, 200);
    headers.RequestVerificationToken = token.json.data.requestToken;
    headers.Cookie = [...cookies].map(([k, v]) => `${k}=${v}`).join('; ');
  }
  const response = await fetch(base + path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body), redirect: 'manual' });
  for (const raw of response.headers.getSetCookie()) {
    const [pair] = raw.split(';'), split = pair.indexOf('=');
    const key = pair.slice(0, split), value = pair.slice(split + 1);
    if (!value || /max-age=0|expires=Thu, 01 Jan 1970/i.test(raw)) cookies.delete(key);
    else cookies.set(key, value);
  }
  const text = await response.text();
  let json; try { json = JSON.parse(text); } catch { /* HTML */ }
  return { status: response.status, headers: response.headers, text, json };
}
async function check(name, action) { await action(); checks.push({ name, status: 'PASS' }); console.log(`PASS: ${name}`); }
async function login(loginName) {
  const result = await call(null, 'POST', '/api/v1/auth/login', { loginName, password, rememberMe: false });
  assert.equal(result.status, 200, `Login failed: ${result.json?.code}`);
  assert.match(result.json.data.sessionId, /^[0-9a-f]{32}$/);
  return result.json.data.sessionId;
}
async function me(session, expected) {
  const response = await call(session, 'GET', '/api/v1/auth/me');
  assert.equal(response.status, 200);
  assert.equal(response.json.data.loginName, expected);
  return response.json.data;
}
const a = await login('session-admin-a');
const b = await login('990091');
const member = await me(b, '990091');
await check('Two logins sharing one cookie jar keep distinct account identities', async () => {
  assert.notEqual(a, b); await me(a, 'session-admin-a'); await me(b, '990091');
});
await check('Query-only page refresh renders the selected account, not the latest login', async () => {
  for (const [session, path, name, other] of [[a, '/admin/dashboard', 'Fixture administrator A', 'Fixture member B'], [b, '/member/dashboard', 'Fixture member B', 'Fixture administrator A']]) {
    const response = await call(session, 'GET', `${path}?zu_session=${session}`, undefined, 'text/html', false);
    assert.equal(response.status, 200); assert.ok(response.text.includes(name)); assert.ok(!response.text.includes(other));
    assert.match(response.headers.get('cache-control'), /no-store/);
  }
});
await check('Missing, unknown and conflicting selectors never fall back to another account', async () => {
  assert.equal((await call(null, 'GET', '/api/v1/auth/me')).status, 401);
  assert.equal((await call('cccccccccccccccccccccccccccccccc', 'GET', '/api/v1/auth/me')).status, 401);
  assert.equal((await call(a, 'GET', `/api/v1/auth/me?zu_session=${b}`)).status, 400);
});
await check('Role context remains isolated between accounts', async () => {
  assert.equal((await call(a, 'POST', '/api/v1/context', { role: 'Admin' })).status, 200);
  assert.equal((await call(b, 'GET', '/api/v1/context')).json.data.current, 'Member');
  assert.equal((await call(b, 'POST', '/api/v1/context', { role: 'Admin' })).status, 403);
});
await check('A forbidden page shows access-denied inside the same tab, not a blank document', async () => {
  const response = await call(b, 'GET', '/admin/dashboard', undefined, 'text/html');
  assert.equal(response.status, 302);
  const location = response.headers.get('location');
  assert.match(location, /^\/access-denied\?zu_session=/);
  assert.ok(location.includes(b));
});

const lookup = await call(a, 'GET', '/api/v1/courses/direct/lookups');
const tomorrow = new Date(Date.now() + 86400000).toISOString().slice(0, 10);
const input = { titleAr: 'Session fixture course', trackId: lookup.json.data.tracks[0].id,
  deliveryMode: 'InPerson', objectivesAr: 'Fixture objectives', targetAudienceAr: 'Fixture staff', totalHours: 2,
  plannedStartDate: tomorrow, plannedEndDate: tomorrow, audienceType: 'General', evaluationEndDate: tomorrow,
  hasExam: false, instructorChoice: 'ManagedByQuality', isHistorical: false };
await check('Already logged-in member sees a new direct course immediately on the next request', async () => {
  await call(b, 'GET', '/api/v1/courses/available');
  const created = await call(a, 'POST', '/api/v1/courses/direct', input);
  assert.equal(created.status, 201, created.json?.code);
  const available = await call(b, 'GET', '/api/v1/courses/available');
  assert.ok(available.json.data.items.some(item => item.id === created.json.data.id));
});
await check('New instructor can create a lecture and open its roster without a new login/JWT', async () => {
  assert.ok(!(await me(b, '990091')).roles.includes('Instructor'));
  const created = await call(a, 'POST', '/api/v1/courses/direct', { ...input, titleAr: 'Instructor fixture course', instructorChoice: 'Internal', instructorUserId: member.userId });
  assert.equal(created.status, 201, created.json?.code);
  const course = created.json.data.id;
  const lecture = await call(b, 'POST', `/api/v1/courses/${course}/lectures`, { titleAr: 'Fixture lecture', mode: 'InPerson', startsAt: `${tomorrow}T09:33:00`, endsAt: `${tomorrow}T09:43:00`, locationAr: 'Fixture room' });
  assert.equal(lecture.status, 201, lecture.json?.code);
  const roster = await call(b, 'GET', `/api/v1/lectures/${lecture.json.data.id}/attendance/roster`);
  assert.equal(roster.status, 200, roster.json?.code);
  assert.ok((await me(b, '990091')).roles.includes('Instructor'));
});
await check('Refresh and logout of an older session do not switch or sign out another account', async () => {
  const newerA = await login('session-admin-a');
  assert.equal((await call(a, 'POST', '/api/v1/auth/refresh')).status, 200);
  await me(b, '990091'); await me(newerA, 'session-admin-a');
  assert.equal((await call(a, 'POST', '/api/v1/auth/logout')).status, 200);
  assert.equal((await call(a, 'GET', '/api/v1/auth/me')).status, 401);
  await me(b, '990091'); await me(newerA, 'session-admin-a');
});
await check('First-login password change obtains valid CSRF and stays in its own isolated session', async () => {
  const c = await login('990092');
  const changed = await call(c, 'POST', '/api/v1/auth/change-password', {
    newPassword: 'New#' + crypto.randomUUID().replaceAll('-', '') + 'aA9!',
    confirmation: ''
  });
  assert.equal(changed.status, 400); // Password validation, never CSRF_INVALID.
  assert.notEqual(changed.json.code, 'CSRF_INVALID');
  const next = 'New#' + crypto.randomUUID().replaceAll('-', '') + 'aA9!';
  const success = await call(c, 'POST', '/api/v1/auth/change-password', { newPassword: next, confirmation: next });
  assert.equal(success.status, 200, success.json?.code);
  assert.ok(success.json.data.redirectUrl.includes('zu_session=' + c));
  await me(c, '990092'); await me(b, '990091');
});
console.log(JSON.stringify({ classification: 'ISOLATED-REAL-API', passed: checks.length, checks }));
