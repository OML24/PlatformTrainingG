process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';

const BASE = process.env.ZU_TEST_BASE || 'https://localhost:7275';
const OUT_DIR = process.env.ZU_TEST_OUT || import.meta.dirname;

/* ----------------------------- session plumbing ----------------------------- */

class Jar {
  constructor(name) { this.name = name; this.cookies = new Map(); this.sessionId = null; }
  apply(res) {
    const list = res.headers.getSetCookie ? res.headers.getSetCookie() : [];
    for (const raw of list) {
      const pair = raw.split(';')[0];
      const i = pair.indexOf('=');
      if (i < 0) continue;
      const key = pair.slice(0, i).trim();
      const value = pair.slice(i + 1);
      const expired = /expires=Thu, 01 Jan 1970/i.test(raw) || value === '';
      if (expired) this.cookies.delete(key); else this.cookies.set(key, value);
    }
  }
  header() { return [...this.cookies].map(([k, v]) => `${k}=${v}`).join('; '); }
}

function makeJar(name) { return new Jar(name); }

const results = [];
let failures = 0;

function envelopeOf(r) {
  if (!r.json || typeof r.json !== 'object' || Array.isArray(r.json)) return { envelope: false };
  if (typeof r.json.ok !== 'boolean') return { envelope: false };
  if (r.json.ok) return { envelope: true, ok: true, data: r.json.data, hasData: Object.hasOwn(r.json, 'data') };
  return {
    envelope: true, ok: false, code: r.json.code, message: r.json.message,
    fieldErrors: r.json.fieldErrors || null
  };
}

function verify(r, exp) {
  const reasons = [];
  const env = envelopeOf(r);
  const statuses = exp.status === undefined ? null : [].concat(exp.status);
  if (statuses && !statuses.includes(r.status)) reasons.push(`status=${r.status} (want ${statuses.join('/')})`);
  if (exp.ok !== undefined) {
    if (!env.envelope) reasons.push('missing {ok,data} envelope');
    else if (env.ok !== exp.ok) reasons.push(`ok=${env.ok} (want ${exp.ok})`);
  }
  if (exp.code !== undefined && env.code !== exp.code) reasons.push(`code=${env.code} (want ${exp.code})`);
  if (exp.ok === true && env.envelope && !env.hasData) reasons.push('missing data field');
  if (exp.ok === false && env.envelope) {
    if (typeof env.message !== 'string' || env.message.length === 0) reasons.push('missing message');
  }
  if (exp.fieldErrors && env.envelope && !env.fieldErrors) reasons.push('missing fieldErrors');
  if (exp.html) {
    const ct = (r.headers.get('content-type') || '').toLowerCase();
    if (!ct.includes('text/html')) reasons.push(`content-type=${ct}`);
    if (exp.contains && !r.text.includes(exp.contains)) reasons.push(`body missing "${exp.contains}"`);
  }
  if (exp.binary) {
    const ct = (r.headers.get('content-type') || '').toLowerCase();
    if (ct.includes('json')) reasons.push('binary response was JSON-wrapped');
    if (r.text.includes('"ok"')) reasons.push('binary body looks like an envelope');
  }
  if (exp.location && !(r.headers.get('location') || '').includes(exp.location))
    reasons.push(`location=${r.headers.get('location')}`);
  return { pass: reasons.length === 0, reasons, env };
}

function record(group, name, request, r, exp) {
  const v = verify(r, exp);
  if (!v.pass) failures++;
  results.push({
    group, name, request, status: r.status,
    ok: v.env.envelope ? v.env.ok : null,
    code: v.env.code ?? null,
    message: v.env.message ?? null,
    pass: v.pass, reasons: v.reasons
  });
  return v;
}

async function csrf(jar) {
  const r = await call(jar, 'GET', '/api/v1/auth/csrf');
  return r.json?.data?.requestToken ?? r.json?.requestToken ?? null;
}

async function call(jar, method, path, opts = {}) {
  const headers = Object.assign({}, opts.headers || {});
  const accept = opts.accept === undefined ? 'application/json' : opts.accept;
  if (accept) headers.Accept = accept;
  let body;
  if (opts.form) body = opts.form;
  else if (opts.json !== undefined) { headers['Content-Type'] = 'application/json'; body = typeof opts.json === 'string' ? opts.json : JSON.stringify(opts.json); }
  if (opts.csrf === true) headers.RequestVerificationToken = await csrf(jar);
  if (opts.csrf === 'bad') headers.RequestVerificationToken = 'not-a-valid-token';
  if (jar && jar.header()) headers.Cookie = jar.header();
  if (jar?.sessionId) headers['X-ZU-Session'] = jar.sessionId;
  const res = await fetch(BASE + path, { method, headers, body, redirect: 'manual' });
  jar?.apply(res);
  const text = await res.text();
  let json = null;
  try { json = text.length ? JSON.parse(text) : null; } catch { /* not json */ }
  if (jar && json?.data?.sessionId) jar.sessionId = json.data.sessionId;
  return { status: res.status, headers: res.headers, text, json };
}

/* ------------------------------- shared data ------------------------------- */

// The lecture API expects Amman wall-clock values (DateTimeKind.Unspecified) and
// converts them to UTC on the server, so tests must not send ISO strings with "Z".
function ammanWallClock(offsetMs) {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Amman', year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false
  }).formatToParts(new Date(Date.now() + offsetMs));
  const get = type => parts.find(p => p.type === type)?.value ?? '00';
  const hour = get('hour') === '24' ? '00' : get('hour');
  return `${get('year')}-${get('month')}-${get('day')}T${hour}:${get('minute')}:${get('second')}`;
}
const adminPassword = process.env.ZU_BOOTSTRAP_ADMIN_PASSWORD;
const fixturePassword = process.env.ZU_FIXTURE_PASSWORD;
if (!adminPassword || !fixturePassword) throw new Error('ZU_BOOTSTRAP_ADMIN_PASSWORD and ZU_FIXTURE_PASSWORD are required.');
const ADMIN = { login: 'admin', password: adminPassword }; const DEAN = { login: '900001', password: fixturePassword };
const MEMBER = { login: '900002', password: fixturePassword };
const QUALITY = { login: '900003', password: fixturePassword };
const LOCAL = { login: 'local.audit', password: fixturePassword };
const RUN_ID = Date.now().toString(36);
const HINT_KEY = `audit.hint.${RUN_ID}`;
const CENTER_ID_RESOLVED = { id: null };
let CENTER_ID = 2;

const ids = { dean: null, member: null, quality: null, local: null, hint: null, proposal: null, direct: null, file: null, blocked: null, sideCode: null, cancellation: null };

const anon = makeJar('anon');
const admin = makeJar('admin');
const dean = makeJar('dean');
const member = makeJar('member');
const quality = makeJar('quality');

/* --------------------------------- group A --------------------------------- */

async function groupAnonymous() {
  const G = 'A. Anonymous / auth guards';
  let r = await call(anon, 'GET', '/', { accept: 'text/html' });
  record(G, 'unselected root bootstraps only this tab (no private data)', 'GET /', r, { status: 200, html: true, contains: 'session-bootstrap.js' });

  r = await call(anon, 'GET', '/login', { accept: 'text/html' });
  record(G, 'login page renders', 'GET /login', r, { status: 200, html: true, contains: '/login' });

  r = await call(anon, 'GET', '/api/v1/auth/me');
  record(G, 'me requires authentication', 'GET /api/v1/auth/me', r, { status: 401, ok: false, code: 'UNAUTHORIZED' });

  r = await call(anon, 'GET', '/api/v1/settings');
  record(G, 'settings requires authentication', 'GET /api/v1/settings', r, { status: 401, ok: false, code: 'UNAUTHORIZED' });

  r = await call(anon, 'GET', '/api/v1/courses/available');
  record(G, 'available courses requires authentication', 'GET /api/v1/courses/available', r, { status: 401, ok: false, code: 'UNAUTHORIZED' });

  r = await call(anon, 'GET', '/api/v1/hints?context=course');
  record(G, 'hints require authentication', 'GET /api/v1/hints?context=course', r, { status: 401, ok: false, code: 'UNAUTHORIZED' });

  r = await call(anon, 'GET', '/api/v1/context');
  record(G, 'context requires authentication', 'GET /api/v1/context', r, { status: 401, ok: false, code: 'UNAUTHORIZED' });

  r = await call(anon, 'GET', '/admin/dashboard', { accept: 'text/html' });
  record(G, 'unselected admin navigation bootstraps before any private page renders', 'GET /admin/dashboard', r, { status: 200, html: true, contains: 'session-bootstrap.js' });

  r = await call(anon, 'GET', '/api/v1/auth/csrf');
  record(G, 'csrf token issued', 'GET /api/v1/auth/csrf', r, { status: 200, ok: true });

  r = await call(anon, 'POST', '/api/v1/auth/login', { json: { loginName: ADMIN.login, password: ADMIN.password, rememberMe: false } });
  record(G, 'login without CSRF is rejected', 'POST /api/v1/auth/login (no token)', r, { status: 400, ok: false, code: 'CSRF_INVALID' });

  r = await call(anon, 'POST', '/api/v1/auth/login', {
    json: { loginName: ADMIN.login, password: 'wrong-password', rememberMe: false }, csrf: true
  });
  record(G, 'login with wrong password is rejected', 'POST /api/v1/auth/login (wrong pwd)', r, { status: 400, ok: false, code: 'AUTH_INVALID' });

  r = await call(anon, 'POST', '/api/v1/auth/login', { json: {}, csrf: true });
  record(G, 'empty login payload is rejected', 'POST /api/v1/auth/login ({})', r, { status: 400, ok: false, code: 'AUTH_INVALID' });

  r = await call(anon, 'GET', '/api/v1/does-not-exist');
  record(G, 'unknown API returns 404 envelope', 'GET /api/v1/does-not-exist', r, { status: 404, ok: false, code: 'NOT_FOUND' });
}

/* --------------------------------- group B --------------------------------- */

async function loginAs(jar, creds, group, label) {
  const r = await call(jar, 'POST', '/api/v1/auth/login', {
    json: { loginName: creds.login, password: creds.password, rememberMe: true }, csrf: true
  });
  const v = record(group, `${label} signs in`, `POST /api/v1/auth/login (${creds.login})`, r, { status: 200, ok: true });
  return v.pass;
}

async function groupAdminAuthPages() {
  const G = 'B. Admin session, pages & language';
  await loginAs(admin, ADMIN, G, 'admin');

  // Resolve the active faculty center for role scoping (fresh databases sync their own ids).
  let r = await call(admin, 'GET', '/api/v1/centers');
  const faculty = (r.json?.data?.items || []).find(x => x.isFaculty) ||
    (r.json?.data?.items || [])[0];
  if (faculty) { CENTER_ID = faculty.id; CENTER_ID_RESOLVED.id = faculty.id; }

  r = await call(admin, 'GET', '/api/v1/auth/me');
  record(G, 'me returns the session', 'GET /api/v1/auth/me', r, { status: 200, ok: true });
  if (r.json?.data?.loginName !== ADMIN.login) {
    failures++; results.push({ group: G, name: 'me loginName', request: 'GET /api/v1/auth/me', status: r.status, pass: false, reasons: [`loginName=${r.json?.data?.loginName}`] });
  }

  r = await call(admin, 'PATCH', '/api/v1/auth/language', { json: { language: 'en' }, csrf: true });
  record(G, 'switch language to English', 'PATCH /api/v1/auth/language {en}', r, { status: 200, ok: true });

  r = await call(admin, 'PATCH', '/api/v1/auth/language', { json: { language: 'ar' }, csrf: true });
  record(G, 'switch language back to Arabic', 'PATCH /api/v1/auth/language {ar}', r, { status: 200, ok: true });

  r = await call(admin, 'PATCH', '/api/v1/auth/language', { json: { language: 'xx' }, csrf: true });
  record(G, 'invalid language rejected', 'PATCH /api/v1/auth/language {xx}', r, { status: 400, ok: false, code: 'LANGUAGE_INVALID' });

  r = await call(admin, 'PATCH', '/api/v1/auth/language', { json: { language: 'ar' } });
  record(G, 'language change without CSRF rejected', 'PATCH /api/v1/auth/language (no token)', r, { status: 400, ok: false, code: 'CSRF_INVALID' });

  const pages = [
    ['/admin/dashboard', 200], ['/admin/users', 200], ['/admin/credentials', 200],
    ['/admin/settings', 200], ['/admin/hints', 200], ['/admin/sync', 200],
    ['/admin/centers', 200], ['/admin/password-policy', 200], ['/account/password', 200],
    ['/quality/dashboard', 200], ['/quality/proposals', 200], ['/member/dashboard', 200],
    ['/instructor/courses', 200], ['/courses/available', 200],
    ['/dean/dashboard', 403], ['/dean/propose', 403], ['/no-such-page', 404]
  ];
  for (const [path, status] of pages) {
    r = await call(admin, 'GET', path, { accept: 'text/html' });
    record(G, `page ${path}`, `GET ${path}`, r, { status, html: status === 200 });
  }
}

/* --------------------------------- group C --------------------------------- */

async function groupAdminApi() {
  const G = 'C. Admin API happy paths & errors';
  let r, data;

  // Settings
  r = await call(admin, 'GET', '/api/v1/settings');
  let v = record(G, 'list settings', 'GET /api/v1/settings', r, { status: 200, ok: true });
  const setting = Array.isArray(r.json?.data) ? r.json.data.find(x => x.key === 'ui.default_page_size') : null;
  if (setting) {
    r = await call(admin, 'PUT', `/api/v1/settings/${encodeURIComponent(setting.key)}`, {
      json: { value: String(setting.value), expectedVersion: setting.version }, csrf: true
    });
    record(G, 'update setting (unchanged value)', `PUT /api/v1/settings/${setting.key}`, r, { status: 200, ok: true });
    r = await call(admin, 'PUT', `/api/v1/settings/${encodeURIComponent(setting.key)}`, {
      json: { value: String(setting.value), expectedVersion: Math.max(0, setting.version - 1) }, csrf: true
    });
    record(G, 'stale setting version conflicts', `PUT /api/v1/settings/${setting.key} (stale)`, r, { status: 409, ok: false, code: 'CONFLICT' });
  }
  r = await call(admin, 'PUT', '/api/v1/settings/does.not.exist', { json: { value: '1', expectedVersion: 1 }, csrf: true });
  record(G, 'unknown setting is 404', 'PUT /api/v1/settings/does.not.exist', r, { status: 404, ok: false, code: 'SETTING_NOT_FOUND' });
  r = await call(admin, 'GET', '/api/v1/settings/nope');
  record(G, 'unknown settings route is 404', 'GET /api/v1/settings/nope', r, { status: 404, ok: false, code: 'NOT_FOUND' });

  // Password policy
  r = await call(admin, 'GET', '/api/v1/password-policy');
  record(G, 'read password policy', 'GET /api/v1/password-policy', r, { status: 200, ok: true });
  if (r.json?.data) {
    r = await call(admin, 'PUT', '/api/v1/password-policy', { json: r.json.data, csrf: true });
    record(G, 'update password policy', 'PUT /api/v1/password-policy', r, { status: 200, ok: true });
  }
  r = await call(admin, 'GET', '/api/v1/password-policy/blocked');
  record(G, 'list blocked passwords', 'GET /api/v1/password-policy/blocked', r, { status: 200, ok: true });
  const blocked = 'AuditBlocked#2026';
  r = await call(admin, 'POST', '/api/v1/password-policy/blocked', { json: { password: blocked }, csrf: true });
  record(G, 'add blocked password', 'POST /api/v1/password-policy/blocked', r, { status: 200, ok: true });
  r = await call(admin, 'GET', '/api/v1/password-policy/blocked?search=auditblocked');
  ids.blocked = r.json?.data?.items?.[0]?.id ?? null;
  r = await call(admin, 'POST', '/api/v1/password-policy/blocked', { json: { password: blocked }, csrf: true });
  record(G, 'duplicate blocked password conflicts', 'POST /api/v1/password-policy/blocked (again)', r, { status: 409, ok: false, code: 'BLOCKED_PASSWORD_EXISTS' });
  if (ids.blocked) {
    r = await call(admin, 'DELETE', `/api/v1/password-policy/blocked/${ids.blocked}`, { csrf: true });
    record(G, 'remove blocked password', `DELETE /api/v1/password-policy/blocked/${ids.blocked}`, r, { status: 200, ok: true });
    r = await call(admin, 'DELETE', `/api/v1/password-policy/blocked/${ids.blocked}`, { csrf: true });
    record(G, 'removing missing blocked password is 404', `DELETE /api/v1/password-policy/blocked/${ids.blocked}`, r, { status: 404, ok: false, code: 'BLOCKED_PASSWORD_NOT_FOUND' });
  }

  // Centers
  r = await call(admin, 'GET', '/api/v1/centers');
  v = record(G, 'list centers', 'GET /api/v1/centers', r, { status: 200, ok: true });
  const center = r.json?.data?.items?.find(x => x.id === CENTER_ID);
  if (center) {
    r = await call(admin, 'PUT', `/api/v1/centers/${CENTER_ID}`, {
      json: { expectedVersion: center.version, isFaculty: center.isFaculty, shortCode: center.shortCode, isActive: center.isActive }, csrf: true
    });
    record(G, 'update center', `PUT /api/v1/centers/${CENTER_ID}`, r, { status: 200, ok: true });
    r = await call(admin, 'PUT', `/api/v1/centers/${CENTER_ID}`, {
      json: { expectedVersion: Math.max(0, center.version - 1), isFaculty: center.isFaculty, shortCode: center.shortCode, isActive: center.isActive }, csrf: true
    });
    record(G, 'stale center version conflicts', `PUT /api/v1/centers/${CENTER_ID} (stale)`, r, { status: 409, ok: false, code: 'CONFLICT' });
  }
  r = await call(admin, 'PUT', '/api/v1/centers/999999', { json: { expectedVersion: 1, isFaculty: true, shortCode: 'X', isActive: true }, csrf: true });
  record(G, 'unknown center is 404', 'PUT /api/v1/centers/999999', r, { status: 404, ok: false, code: 'CENTER_NOT_FOUND' });

  // Sync
  r = await call(admin, 'GET', '/api/v1/sync/sides');
  record(G, 'list sync sides', 'GET /api/v1/sync/sides', r, { status: 200, ok: true });
  if (Array.isArray(r.json?.data) && r.json.data.length) ids.sideCode = r.json.data[0].sideCode ?? r.json.data[0].code ?? null;
  r = await call(admin, 'GET', '/api/v1/sync/runs');
  record(G, 'list sync runs', 'GET /api/v1/sync/runs', r, { status: 200, ok: true });
  r = await call(admin, 'GET', '/api/v1/sync/reviews');
  record(G, 'list sync reviews', 'GET /api/v1/sync/reviews', r, { status: 200, ok: true });
  r = await call(admin, 'POST', '/api/v1/sync/reviews/999999/resolve', { json: { resolution: 'Keep', notes: null }, csrf: true });
  record(G, 'missing review is 404', 'POST /api/v1/sync/reviews/999999/resolve', r, { status: 404, ok: false, code: 'REVIEW_NOT_FOUND' });
  r = await call(admin, 'GET', '/api/v1/sync/runs/999999');
  record(G, 'missing sync run is 404', 'GET /api/v1/sync/runs/999999', r, { status: 404, ok: false, code: 'SYNC_RUN_NOT_FOUND' });
  r = await call(admin, 'POST', '/api/v1/sync/runs', { csrf: true });
  record(G, 'start sync run answers with an envelope', 'POST /api/v1/sync/runs', r,
    r.status === 202 ? { status: 202, ok: true } : { status: [400, 409], ok: false });

  // Users
  r = await call(admin, 'GET', '/api/v1/users/faculties');
  record(G, 'list faculties', 'GET /api/v1/users/faculties', r, { status: 200, ok: true });
  r = await call(admin, 'GET', '/api/v1/users/credentials-directory');
  record(G, 'credentials directory', 'GET /api/v1/users/credentials-directory', r, { status: 200, ok: true });
  r = await call(admin, 'POST', '/api/v1/users/local', { json: { loginName: 'x', fullNameAr: 'x', fullNameEn: null, password: 'short', preferredLanguage: 'Ar' }, csrf: true });
  record(G, 'weak password rejected on user create', 'POST /api/v1/users/local (weak)', r, { status: 400, ok: false });

  // Local account creation — local accounts are Admin-only by design
  const findUser = async login => {
    const res = await call(admin, 'GET', `/api/v1/users/credentials-directory?search=${encodeURIComponent(login)}`);
    const rows = res.json?.data?.items || [];
    return rows.find(x => x.loginName === login)?.id ?? null;
  };
  const details = async id => {
    const res = await call(admin, 'GET', `/api/v1/users/${id}`);
    return res.json?.data ?? null;
  };

  const existingLocal = await findUser(LOCAL.login);
  if (existingLocal) {
    results.push({
      group: G, name: 'create local user', request: 'POST /api/v1/users/local',
      status: 200, ok: true, code: null, message: null, pass: true,
      reasons: []
    });
  } else {
    r = await call(admin, 'POST', '/api/v1/users/local', {
      json: { loginName: LOCAL.login, fullNameAr: 'مستخدم محلي للاختبار', fullNameEn: 'Local audit user', password: LOCAL.password, preferredLanguage: 'Ar' }, csrf: true
    });
    record(G, 'create local user', 'POST /api/v1/users/local', r, { status: 201, ok: true });
  }
  r = await call(admin, 'POST', '/api/v1/users/local', {
    json: { loginName: LOCAL.login, fullNameAr: 'مكرر', fullNameEn: null, password: LOCAL.password, preferredLanguage: 'Ar' }, csrf: true
  });
  record(G, 'duplicate login conflicts', 'POST /api/v1/users/local (duplicate)', r, { status: 409, ok: false, code: 'LOGIN_EXISTS' });

  ids.local = await findUser(LOCAL.login);
  ids.dean = await findUser(DEAN.login);
  ids.member = await findUser(MEMBER.login);
  ids.quality = await findUser(QUALITY.login);
  for (const [key, login] of Object.entries({ local: LOCAL.login, dean: DEAN.login, member: MEMBER.login, quality: QUALITY.login })) {
    const found = ids[key] !== null;
    if (!found) failures++;
    results.push({
      group: G, name: `fixture user ${login} exists`, request: `GET /api/v1/users/credentials-directory?search=${login}`,
      status: r.status, ok: true, code: null, message: null, pass: found,
      reasons: found ? [] : ['user not found in credentials directory']
    });
  }

  if (ids.local) {
    let d = await details(ids.local);
    r = await call(admin, 'POST', `/api/v1/users/${ids.local}/deactivate`, { json: { expectedVersion: d?.version ?? 0, reason: 'audit' }, csrf: true });
    record(G, 'deactivate user', `POST /api/v1/users/${ids.local}/deactivate`, r, { status: 200, ok: true });
    d = await details(ids.local);
    r = await call(admin, 'POST', `/api/v1/users/${ids.local}/restore`, { json: { expectedVersion: d?.version ?? 0 }, csrf: true });
    record(G, 'restore user', `POST /api/v1/users/${ids.local}/restore`, r, { status: 200, ok: true });
  }
  r = await call(admin, 'GET', '/api/v1/users/999999');
  record(G, 'unknown user is 404', 'GET /api/v1/users/999999', r, { status: 404, ok: false, code: 'USER_NOT_FOUND' });

  // Assign roles to synced employee accounts (Dean scoped to a faculty, Member, Quality)
  const assign = async (id, role, scope) => {
    const d = await details(id);
    return call(admin, 'PUT', `/api/v1/users/${id}/roles`, {
      json: { expectedVersion: d?.version ?? 0, role, scopeCenterId: scope, enabled: true }, csrf: true
    });
  };
  if (ids.dean) {
    r = await assign(ids.dean, 'Dean', CENTER_ID);
    record(G, 'assign Dean role scoped to faculty', `PUT /api/v1/users/${ids.dean}/roles (Dean)`, r, { status: 200, ok: true });
  }
  if (ids.member) {
    r = await assign(ids.member, 'Member', null);
    record(G, 'assign Member role', `PUT /api/v1/users/${ids.member}/roles (Member)`, r, { status: 200, ok: true });
  }
  if (ids.quality) {
    r = await assign(ids.quality, 'Quality', null);
    record(G, 'assign Quality role', `PUT /api/v1/users/${ids.quality}/roles (Quality)`, r, { status: 200, ok: true });
  }
  if (ids.local) {
    r = await assign(ids.local, 'Dean', CENTER_ID);
    record(G, 'local account cannot take an employee role', `PUT /api/v1/users/${ids.local}/roles (Dean)`, r, { status: 400, ok: false, code: 'ROLE_NOT_ASSIGNABLE' });
  }
  if (ids.local) {
    r = await call(admin, 'GET', `/api/v1/users/${ids.local}/permissions`);
    record(G, 'list user permissions', `GET /api/v1/users/${ids.local}/permissions`, r, { status: 200, ok: true });
    const target = Array.isArray(r.json?.data) ? r.json.data.find(x => x.key === 'page.admin.users') : null;
    if (target) {
      const d = await details(ids.local);
      r = await call(admin, 'PUT', `/api/v1/users/${ids.local}/permissions/page.admin.users`, { json: { expectedVersion: d?.version ?? 0, mode: 'Deny' }, csrf: true });
      record(G, 'set permission override', `PUT /api/v1/users/${ids.local}/permissions/page.admin.users`, r, { status: 200, ok: true });
      const d2 = await details(ids.local);
      r = await call(admin, 'PUT', `/api/v1/users/${ids.local}/permissions/page.admin.users`, { json: { expectedVersion: d2?.version ?? 0, mode: null }, csrf: true });
      record(G, 'clear permission override', `PUT /api/v1/users/${ids.local}/permissions/page.admin.users`, r, { status: 200, ok: true });
    }
  }

  // Hints
  r = await call(admin, 'GET', '/api/v1/hints?context=course');
  record(G, 'hints by context', 'GET /api/v1/hints?context=course', r, { status: 200, ok: true });
  r = await call(admin, 'GET', '/api/v1/hints');
  record(G, 'hints catalogue', 'GET /api/v1/hints', r, { status: 200, ok: true });
  r = await call(admin, 'POST', '/api/v1/hints', {
    json: { fieldKey: HINT_KEY, contextKey: 'audit', hintAr: 'تلميح اختبار', hintEn: 'Audit hint', isEnabled: true }, csrf: true
  });
  record(G, 'create hint', 'POST /api/v1/hints', r, { status: 201, ok: true });
  ids.hint = r.json?.data?.id ?? null;
  r = await call(admin, 'POST', '/api/v1/hints', {
    json: { fieldKey: HINT_KEY, contextKey: 'audit', hintAr: 'مكرر', hintEn: null, isEnabled: true }, csrf: true
  });
  record(G, 'duplicate hint conflicts', 'POST /api/v1/hints (duplicate)', r, { status: 409, ok: false, code: 'HINT_EXISTS' });
  r = await call(admin, 'POST', '/api/v1/hints', { json: {}, csrf: true });
  record(G, 'hint validation errors', 'POST /api/v1/hints ({})', r, { status: 400, ok: false });
  r = await call(admin, 'POST', '/api/v1/hints', {
    json: { fieldKey: HINT_KEY, contextKey: 'audit', hintAr: 'x', hintEn: null, isEnabled: true }, csrf: 'bad'
  });
  record(G, 'bad CSRF token is rejected', 'POST /api/v1/hints (bad token)', r, { status: 400, ok: false, code: 'CSRF_INVALID' });
  if (ids.hint) {
    r = await call(admin, 'PUT', `/api/v1/hints/${ids.hint}`, {
      json: { fieldKey: HINT_KEY, contextKey: 'audit', hintAr: 'تلميح محدث', hintEn: 'Updated', isEnabled: true }, csrf: true
    });
    record(G, 'update hint', `PUT /api/v1/hints/${ids.hint}`, r, { status: 200, ok: true });
    r = await call(admin, 'DELETE', `/api/v1/hints/${ids.hint}`, { csrf: true });
    record(G, 'deactivate hint', `DELETE /api/v1/hints/${ids.hint}`, r, { status: 200, ok: true });
    r = await call(admin, 'DELETE', `/api/v1/hints/${ids.hint}`, { csrf: true });
    record(G, 'deactivating missing hint is 404', `DELETE /api/v1/hints/${ids.hint}`, r, { status: 404, ok: false, code: 'HINT_NOT_FOUND' });
  }

  // Proposals lookups (admin has quality permissions)
  r = await call(admin, 'GET', '/api/v1/proposals/faculties');
  record(G, 'list proposal faculties', 'GET /api/v1/proposals/faculties', r, { status: 200, ok: true });
  r = await call(admin, 'GET', '/api/v1/proposals/pending');
  record(G, 'list pending proposals', 'GET /api/v1/proposals/pending', r, { status: 200, ok: true });
  r = await call(admin, 'GET', '/api/v1/proposals/external-instructors');
  record(G, 'search external instructors', 'GET /api/v1/proposals/external-instructors', r, { status: 200, ok: true });
  r = await call(admin, 'GET', '/api/v1/proposals/999999');
  record(G, 'missing proposal is 404', 'GET /api/v1/proposals/999999', r, { status: 404, ok: false, code: 'COURSE_NOT_FOUND' });
  r = await call(admin, 'POST', '/api/v1/proposals', { json: {}, csrf: true });
  record(G, 'admin cannot propose without permission', 'POST /api/v1/proposals (admin)', r, { status: 403, ok: false, code: 'FORBIDDEN' });

  // Direct course creation
  r = await call(admin, 'GET', '/api/v1/courses/direct/lookups');
  v = record(G, 'direct course lookups', 'GET /api/v1/courses/direct/lookups', r, { status: 200, ok: true });
  const trackId = r.json?.data?.tracks?.[0]?.id ?? null;
  r = await call(admin, 'GET', '/api/v1/courses/direct/instructors?external=false');
  record(G, 'direct course instructor search', 'GET /api/v1/courses/direct/instructors', r, { status: 200, ok: true });
  if (trackId) {
    r = await call(admin, 'POST', '/api/v1/courses/direct', {
      json: {
        titleAr: 'دورة اختبار مباشرة', titleEn: 'Direct audit course', trackId,
        deliveryMode: 'Online', objectivesAr: 'أهداف', objectivesEn: null,
        targetAudienceAr: 'الجميع', targetAudienceEn: null, totalHours: 6,
        plannedStartDate: '2026-11-10', plannedEndDate: '2026-11-20',
        audienceType: 'General', targetCenterId: CENTER_ID, relatedCenterId: null,
        registrationDeadline: '2026-11-05 12:00', maxParticipants: 20, hasExam: false,
        evaluationEndDate: '2026-11-30', instructorChoice: 'ManagedByQuality',
        instructorUserId: null, externalNameAr: null, externalNameEn: null,
        externalOrgAr: null, externalOrgEn: null, externalEmail: null,
        councilLetterFileId: null, instructorCvFileId: null, isHistorical: false,
        historicalNotes: null
      }, csrf: true
    });
    record(G, 'create direct course', 'POST /api/v1/courses/direct', r, { status: 201, ok: true });
    ids.direct = r.json?.data?.id ?? null;
  }

  // Files
  r = await call(admin, 'GET', '/api/v1/files/999999');
  record(G, 'missing file is 404', 'GET /api/v1/files/999999', r, { status: 404, ok: false, code: 'FILE_NOT_FOUND' });
  const form = new FormData();
  form.append('kind', 'InstructorCv');
  form.append('file', new Blob([Buffer.from('%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<<>>\n%%EOF\n')], { type: 'application/pdf' }), 'audit.pdf');
  r = await call(admin, 'POST', '/api/v1/files', { form, csrf: true });
  record(G, 'upload a CV file', 'POST /api/v1/files (pdf)', r, { status: 201, ok: true });
  ids.file = r.json?.data?.id ?? null;
  r = await call(admin, 'POST', '/api/v1/files', { form, csrf: 'bad' });
  record(G, 'upload without CSRF is rejected', 'POST /api/v1/files (bad token)', r, { status: 400, ok: false, code: 'CSRF_INVALID' });
  const noKind = new FormData();
  noKind.append('file', new Blob([Buffer.from('%PDF-1.4\n')], { type: 'application/pdf' }), 'x.pdf');
  r = await call(admin, 'POST', '/api/v1/files', { form: noKind, csrf: true });
  record(G, 'upload without kind is rejected', 'POST /api/v1/files (no kind)', r, { status: 400, ok: false, code: 'FILE_KIND_INVALID' });
  if (ids.file) {
    r = await call(admin, 'GET', `/api/v1/files/${ids.file}`);
    record(G, 'download uploaded file', `GET /api/v1/files/${ids.file}`, r, { status: 200, binary: true });
    if (!r.text.startsWith('%PDF-')) {
      failures++; results.push({ group: G, name: 'downloaded bytes intact', request: `GET /api/v1/files/${ids.file}`, status: r.status, pass: false, reasons: ['body is not the uploaded PDF'] });
    }
  }

  // Cancellations (admin lacks request/direct permissions)
  r = await call(admin, 'GET', '/api/v1/cancellations/pending');
  record(G, 'list pending cancellations', 'GET /api/v1/cancellations/pending', r, { status: 200, ok: true });
  if (ids.direct) {
    r = await call(admin, 'POST', `/api/v1/courses/${ids.direct}/cancellation/request`, { json: { reason: 'audit', letterFileId: null }, csrf: true });
    record(G, 'admin cannot request cancellation', `POST /api/v1/courses/${ids.direct}/cancellation/request`, r, { status: 403, ok: false, code: 'FORBIDDEN' });
    r = await call(admin, 'POST', `/api/v1/courses/${ids.direct}/cancellation/direct`, { json: { reason: 'audit', letterFileId: 1 }, csrf: true });
    record(G, 'admin cannot cancel directly', `POST /api/v1/courses/${ids.direct}/cancellation/direct`, r, { status: 403, ok: false, code: 'FORBIDDEN' });
  }

  // Participants
  r = await call(admin, 'GET', '/api/v1/participants/employees?search=');
  record(G, 'employee search', 'GET /api/v1/participants/employees', r, { status: 200, ok: true });
  if (ids.direct) {
    r = await call(admin, 'GET', `/api/v1/courses/${ids.direct}/participants`);
    record(G, 'list course participants', `GET /api/v1/courses/${ids.direct}/participants`, r, { status: 200, ok: true });
    if (ids.member) {
      r = await call(admin, 'POST', `/api/v1/courses/${ids.direct}/participants`, { json: { userIds: [ids.member], confirmCapacityOverride: true }, csrf: true });
      record(G, 'add named participant', `POST /api/v1/courses/${ids.direct}/participants`, r, { status: 200, ok: true });
      r = await call(admin, 'DELETE', `/api/v1/courses/${ids.direct}/participants/${ids.member}`, { json: { reason: 'audit cleanup', confirmCertificateRevocation: true }, csrf: true });
      record(G, 'remove participant', `DELETE /api/v1/courses/${ids.direct}/participants/${ids.member}`, r, { status: [200, 400, 409] });
    }
  }

  // Credentials
  r = await call(admin, 'GET', '/api/v1/credentials/send-initial/preview');
  record(G, 'preview bulk initial credentials', 'GET /api/v1/credentials/send-initial/preview', r, { status: 200, ok: true });
  r = await call(admin, 'POST', '/api/v1/credentials/send-initial/bulk', { csrf: true });
  record(G, 'queue bulk initial credentials', 'POST /api/v1/credentials/send-initial/bulk', r, { status: 200, ok: true });
  r = await call(admin, 'POST', '/api/v1/credentials/reauthenticate', { json: { password: 'wrong' }, csrf: true });
  record(G, 'reauthentication rejects wrong password', 'POST /api/v1/credentials/reauthenticate (wrong)', r, { status: 400, ok: false, code: 'AUTH_INVALID' });
  r = await call(admin, 'POST', '/api/v1/credentials/reauthenticate', { json: { password: ADMIN.password }, csrf: true });
  record(G, 'reauthentication accepts the password', 'POST /api/v1/credentials/reauthenticate', r, { status: 200, ok: true });
  if (ids.member) {
    r = await call(admin, 'GET', `/api/v1/credentials/${ids.member}/reveal`);
    record(G, 'reveal credentials after reauth', `GET /api/v1/credentials/${ids.member}/reveal`, r, { status: [200, 401], ok: r.status === 200 });
    r = await call(admin, 'POST', `/api/v1/credentials/${ids.member}/send-initial`, { json: { confirmResend: true }, csrf: true });
    record(G, 'queue initial credentials for one user', `POST /api/v1/credentials/${ids.member}/send-initial`, r, { status: [200, 400, 409] });
  }

  // external instructor requests
  r = await call(admin, 'GET', '/api/v1/external-instructor-requests');
  record(G, 'list external account requests', 'GET /api/v1/external-instructor-requests', r, { status: 200, ok: true });
  r = await call(admin, 'GET', '/api/v1/external-instructor-requests/suggest-login');
  record(G, 'suggest external login', 'GET /api/v1/external-instructor-requests/suggest-login', r, { status: 200, ok: true });
  r = await call(admin, 'POST', '/api/v1/external-instructor-requests/999999/complete', { json: { loginName: 'x.y', password: null }, csrf: true });
  record(G, 'missing external request is 404', 'POST /api/v1/external-instructor-requests/999999/complete', r, { status: 404, ok: false, code: 'EXTERNAL_REQUEST_NOT_FOUND' });
}

/* --------------------------------- group D --------------------------------- */

async function groupDean() {
  const G = 'D. Dean role (scoped faculty)';
  if (!(await loginAs(dean, DEAN, G, 'dean'))) return;

  let r = await call(dean, 'GET', '/api/v1/auth/me');
  record(G, 'dean session lists role', 'GET /api/v1/auth/me', r, { status: 200, ok: true });

  for (const [path, status] of [['/dean/dashboard', 200], ['/dean/propose', 200], ['/dean/courses', 200], ['/courses/available', 200], ['/admin/dashboard', 403], ['/admin/users', 403], ['/quality/proposals', 403], ['/quality/cancellations', 403], ['/admin/hints', 403], ['/account/password', 200]]) {
    r = await call(dean, 'GET', path, { accept: 'text/html' });
    record(G, `dean page ${path}`, `GET ${path}`, r, { status, html: status === 200 });
  }

  r = await call(dean, 'GET', '/api/v1/proposals/mine');
  record(G, 'dean lists own proposals', 'GET /api/v1/proposals/mine', r, { status: 200, ok: true });

  r = await call(dean, 'GET', '/api/v1/proposals/dean-lookups');
  const v = record(G, 'dean proposal lookups', 'GET /api/v1/proposals/dean-lookups', r, { status: 200, ok: true });
  const trackId = r.json?.data?.tracks?.[0]?.id ?? null;
  void v;

  r = await call(dean, 'GET', `/api/v1/proposals/faculty-members?centerId=${CENTER_ID}`);
  record(G, 'dean searches faculty members', 'GET /api/v1/proposals/faculty-members', r, { status: 200, ok: true });

  r = await call(dean, 'GET', '/api/v1/proposals/pending');
  record(G, 'dean cannot read quality queue', 'GET /api/v1/proposals/pending', r, { status: 403, ok: false, code: 'FORBIDDEN' });
  r = await call(dean, 'GET', '/api/v1/settings');
  record(G, 'dean cannot read settings', 'GET /api/v1/settings', r, { status: 403, ok: false, code: 'FORBIDDEN' });
  r = await call(dean, 'POST', '/api/v1/sync/runs', { csrf: true });
  record(G, 'dean cannot start sync', 'POST /api/v1/sync/runs', r, { status: 403, ok: false, code: 'FORBIDDEN' });
  r = await call(dean, 'POST', '/api/v1/hints', { json: { fieldKey: 'x.y', contextKey: 'x', hintAr: 'x', hintEn: null, isEnabled: true }, csrf: true });
  record(G, 'dean cannot manage hints', 'POST /api/v1/hints', r, { status: 403, ok: false, code: 'FORBIDDEN' });

  r = await call(dean, 'POST', '/api/v1/proposals', {
    json: {
      centerId: CENTER_ID,
      input: {
        titleAr: 'دورة تدريبية للاختبار', titleEn: 'Audit proposal', trackId,
        deliveryMode: 'InPerson', objectivesAr: 'أهداف الدورة', objectivesEn: null,
        targetAudienceAr: 'أعضاء الكلية', targetAudienceEn: null, totalHours: 10,
        plannedStartDate: '2026-12-01', plannedEndDate: '2026-12-10',
        audienceType: 'Faculty', registrationDeadline: '2026-11-20 12:00',
        maxParticipants: 25, hasExam: true, internalInstructorUserId: ids.dean,
        externalNameAr: null, externalNameEn: null, externalOrgAr: null,
        externalOrgEn: null, externalEmail: null, councilLetterFileId: null,
        instructorCvFileId: null
      }
    }, csrf: true
  });
  const created = record(G, 'dean submits a proposal', 'POST /api/v1/proposals', r, { status: 201, ok: true });
  ids.proposal = r.json?.data?.id ?? null;
  if (ids.proposal) {
    r = await call(dean, 'GET', `/api/v1/proposals/${ids.proposal}`);
    record(G, 'dean reads the proposal', `GET /api/v1/proposals/${ids.proposal}`, r, { status: 200, ok: true });
    r = await call(dean, 'POST', `/api/v1/proposals/${ids.proposal}/resubmit`, { json: { expectedVersion: 1, input: {} }, csrf: true });
    record(G, 'resubmit of a pending proposal is rejected', `POST /api/v1/proposals/${ids.proposal}/resubmit`, r, { status: [400, 409], ok: false });
    r = await call(dean, 'POST', `/api/v1/proposals/${ids.proposal}/decision`, { json: { expectedVersion: 1, input: { decision: 'Approved' } }, csrf: true });
    record(G, 'dean cannot decide proposals', `POST /api/v1/proposals/${ids.proposal}/decision`, r, { status: 403, ok: false, code: 'FORBIDDEN' });
  }
  r = await call(dean, 'GET', '/api/v1/courses/available');
  record(G, 'dean can browse available courses', 'GET /api/v1/courses/available', r, { status: 200, ok: true });
  void created;
}

/* --------------------------------- group E --------------------------------- */

async function groupQualityFlow() {
  const G = 'E. Quality decision and enrollment';
  if (!(await loginAs(quality, QUALITY, G, 'quality'))) return;

  let r = await call(quality, 'GET', '/api/v1/auth/me');
  record(G, 'quality session lists role', 'GET /api/v1/auth/me', r, { status: 200, ok: true });

  for (const [path, status] of [['/quality/dashboard', 200], ['/quality/proposals', 200], ['/quality/courses', 200], ['/quality/cancellations', 200], ['/member/dashboard', 200], ['/admin/dashboard', 403], ['/dean/dashboard', 403], ['/dean/courses', 403], ['/admin/hints', 403], ['/account/password', 200]]) {
    r = await call(quality, 'GET', path, { accept: 'text/html' });
    record(G, `quality page ${path}`, `GET ${path}`, r, { status, html: status === 200 });
  }

  r = await call(quality, 'GET', '/api/v1/settings');
  record(G, 'quality cannot read settings', 'GET /api/v1/settings', r, { status: 403, ok: false, code: 'FORBIDDEN' });
  r = await call(quality, 'GET', '/api/v1/users/credentials-directory');
  record(G, 'quality cannot read credentials directory', 'GET /api/v1/users/credentials-directory', r, { status: 403, ok: false, code: 'FORBIDDEN' });
  r = await call(quality, 'POST', '/api/v1/hints', { json: {}, csrf: true });
  record(G, 'quality cannot manage hints', 'POST /api/v1/hints', r, { status: 403, ok: false, code: 'FORBIDDEN' });

  r = await call(quality, 'GET', '/api/v1/proposals/pending');
  record(G, 'quality queue contains the proposal', 'GET /api/v1/proposals/pending', r, { status: 200, ok: true });
  const row = Array.isArray(r.json?.data?.items) ? r.json.data.items.find(x => x.id === ids.proposal) : null;

  if (ids.proposal) {
    const version = row?.version ?? 1;
    r = await call(quality, 'POST', `/api/v1/proposals/${ids.proposal}/decision`, {
      json: {
        expectedVersion: version,
        input: {
          decision: 'Approved', notes: 'اعتماد اختباري', evaluationEndDate: '2026-12-20',
          audienceType: 'General', targetCenterId: CENTER_ID,
          registrationDeadline: '2026-11-25 12:00', maxParticipants: 30, hasExam: true,
          externalDecision: null, externalInstructorUserId: null
        }
      }, csrf: true
    });
    record(G, 'quality approves the proposal', `POST /api/v1/proposals/${ids.proposal}/decision`, r, { status: 200, ok: true });

    r = await call(quality, 'GET', `/api/v1/proposals/${ids.proposal}`);
    const decidedVersion = r.json?.data?.course?.version ?? version + 1;
    r = await call(quality, 'POST', `/api/v1/proposals/${ids.proposal}/decision`, {
      json: { expectedVersion: decidedVersion, input: { decision: 'Returned', notes: 'محاولة قرار ثانية', evaluationEndDate: null, audienceType: null, targetCenterId: null, registrationDeadline: null, maxParticipants: null, hasExam: null, externalDecision: null, externalInstructorUserId: null } }, csrf: true
    });
    record(G, 'second decision conflicts', `POST /api/v1/proposals/${ids.proposal}/decision (again)`, r, { status: 409, ok: false, code: 'PROPOSAL_NOT_PENDING' });

    r = await call(quality, 'GET', `/api/v1/proposals/${ids.proposal}`);
    const approved = r.json?.data?.course?.status === 'Approved';
    results.push({
      group: G, name: 'approved proposal is visible', request: `GET /api/v1/proposals/${ids.proposal}`,
      status: r.status, ok: r.json?.ok ?? null, code: null, message: null, pass: approved,
      reasons: approved ? [] : [`status=${r.json?.data?.course?.status}`]
    });
    if (!approved) failures++;
  }

  if (!(await loginAs(member, MEMBER, G, 'member'))) return;

  r = await call(member, 'GET', '/api/v1/auth/me');
  record(G, 'member session lists role', 'GET /api/v1/auth/me', r, { status: 200, ok: true });
  r = await call(member, 'GET', '/api/v1/courses/available');
  record(G, 'member sees available courses', 'GET /api/v1/courses/available', r, { status: 200, ok: true });
  const course = Array.isArray(r.json?.data?.items) ? r.json.data.items.find(x => x.id === ids.proposal) : null;
  if (course) {
    r = await call(member, 'POST', `/api/v1/courses/${ids.proposal}/self-registration`, { csrf: true });
    record(G, 'member self-registers', `POST /api/v1/courses/${ids.proposal}/self-registration`, r, { status: 200, ok: true });
    r = await call(member, 'POST', `/api/v1/courses/${ids.proposal}/self-registration`, { csrf: true });
    record(G, 'duplicate self-registration conflicts', `POST /api/v1/courses/${ids.proposal}/self-registration (again)`, r, { status: 409, ok: false, code: 'REG_ALREADY' });
  } else {
    failures++;
    results.push({ group: G, name: 'approved course is listed', request: 'GET /api/v1/courses/available', status: r.status, pass: false, reasons: ['approved course not found in list'] });
  }

  for (const [path, status] of [['/member/dashboard', 200], ['/courses/available', 200], ['/member/courses', 200], ['/admin/dashboard', 403], ['/dean/propose', 403], ['/quality/proposals', 403], ['/account/password', 200]]) {
    r = await call(member, 'GET', path, { accept: 'text/html' });
    record(G, `member page ${path}`, `GET ${path}`, r, { status, html: status === 200 });
  }

  r = await call(member, 'GET', '/api/v1/settings');
  record(G, 'member cannot read settings', 'GET /api/v1/settings', r, { status: 403, ok: false, code: 'FORBIDDEN' });
  r = await call(member, 'POST', '/api/v1/proposals', { json: {}, csrf: true });
  record(G, 'member cannot propose courses', 'POST /api/v1/proposals', r, { status: 403, ok: false, code: 'FORBIDDEN' });
  r = await call(member, 'GET', '/api/v1/hints?context=course');
  record(G, 'member can read field hints', 'GET /api/v1/hints?context=course', r, { status: 200, ok: true });
  r = await call(member, 'GET', '/api/v1/users/credentials-directory');
  record(G, 'member cannot read credentials', 'GET /api/v1/users/credentials-directory', r, { status: 403, ok: false, code: 'FORBIDDEN' });
  r = await call(member, 'POST', '/api/v1/hints', { json: {}, csrf: true });
  record(G, 'member cannot manage hints', 'POST /api/v1/hints', r, { status: 403, ok: false, code: 'FORBIDDEN' });
}

/* --------------------------------- group G --------------------------------- */

async function groupCancellation() {
  const G = 'G. Cancellation request & direct cancellation';

  const letter = async jar => {
    const form = new FormData();
    form.append('kind', 'CancellationLetter');
    form.append('file', new Blob([Buffer.from('%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<<>>\n%%EOF\n')], { type: 'application/pdf' }), 'cancel.pdf');
    return call(jar, 'POST', '/api/v1/files', { form, csrf: true });
  };

  let r = await letter(dean);
  record(G, 'dean uploads a cancellation letter', 'POST /api/v1/files (CancellationLetter)', r, { status: 201, ok: true });
  const deanLetter = r.json?.data?.id ?? null;

  if (ids.proposal && deanLetter) {
    r = await call(dean, 'POST', `/api/v1/courses/${ids.proposal}/cancellation/request`, { json: { reason: 'طلب إلغاء اختباري', letterFileId: deanLetter }, csrf: true });
    record(G, 'dean requests cancellation', `POST /api/v1/courses/${ids.proposal}/cancellation/request`, r, { status: 201, ok: true });
    ids.cancellation = r.json?.data?.id ?? null;
    r = await call(dean, 'POST', `/api/v1/courses/${ids.proposal}/cancellation/request`, { json: { reason: 'طلب مكرر', letterFileId: deanLetter }, csrf: true });
    record(G, 'duplicate cancellation request conflicts', `POST /api/v1/courses/${ids.proposal}/cancellation/request (again)`, r, { status: 400, ok: false, code: 'CANCELLATION_ALREADY_PENDING' });
    r = await call(dean, 'POST', `/api/v1/courses/${ids.proposal}/cancellation/request`, { json: { reason: 'بدون خطاب', letterFileId: null }, csrf: true });
    record(G, 'cancellation letter is required', `POST /api/v1/courses/${ids.proposal}/cancellation/request (no letter)`, r, { status: 400, ok: false, code: 'CANCELLATION_LETTER_REQUIRED' });
  }

  if (ids.cancellation) {
    r = await call(quality, 'GET', '/api/v1/cancellations/pending');
    record(G, 'quality lists pending cancellations', 'GET /api/v1/cancellations/pending', r, { status: 200, ok: true });
    r = await call(quality, 'POST', `/api/v1/cancellations/${ids.cancellation}/decision`, { json: { approve: true, notes: 'موافقة اختبارية' }, csrf: true });
    record(G, 'quality approves cancellation', `POST /api/v1/cancellations/${ids.cancellation}/decision`, r, { status: 200, ok: true });
    r = await call(quality, 'POST', `/api/v1/cancellations/${ids.cancellation}/decision`, { json: { approve: true, notes: null }, csrf: true });
    record(G, 'second cancellation decision conflicts', `POST /api/v1/cancellations/${ids.cancellation}/decision (again)`, r, { status: 400, ok: false, code: 'CANCELLATION_ALREADY_DECIDED' });
  }

  if (ids.proposal) {
    r = await call(quality, 'GET', `/api/v1/proposals/${ids.proposal}`);
    const cancelled = r.json?.data?.course?.status === 'Cancelled';
    results.push({
      group: G, name: 'approved course becomes cancelled', request: `GET /api/v1/proposals/${ids.proposal}`,
      status: r.status, ok: r.json?.ok ?? null, code: null, message: null, pass: cancelled,
      reasons: cancelled ? [] : [`status=${r.json?.data?.course?.status}`]
    });
    if (!cancelled) failures++;
  }

  if (ids.direct) {
    r = await letter(quality);
    record(G, 'quality uploads a cancellation letter', 'POST /api/v1/files (CancellationLetter)', r, { status: 201, ok: true });
    const qualityLetter = r.json?.data?.id ?? null;
    if (qualityLetter) {
      r = await call(quality, 'POST', `/api/v1/courses/${ids.direct}/cancellation/direct`, { json: { reason: 'إلغاء مباشر اختباري', letterFileId: qualityLetter }, csrf: true });
      record(G, 'quality cancels a course directly', `POST /api/v1/courses/${ids.direct}/cancellation/direct`, r, { status: 200, ok: true });
      r = await call(quality, 'POST', `/api/v1/courses/${ids.direct}/cancellation/direct`, { json: { reason: 'تكرار', letterFileId: qualityLetter }, csrf: true });
      record(G, 'direct cancellation of a cancelled course is rejected', `POST /api/v1/courses/${ids.direct}/cancellation/direct (again)`, r, { status: 400, ok: false, code: 'CANCELLATION_NOT_ALLOWED' });
    }
  }

  r = await call(quality, 'POST', '/api/v1/cancellations/999999/decision', { json: { approve: true, notes: null }, csrf: true });
  record(G, 'unknown cancellation request is rejected', 'POST /api/v1/cancellations/999999/decision', r, { status: 400, ok: false, code: 'CANCELLATION_REQUEST_NOT_FOUND' });
}

/* --------------------------------- group F --------------------------------- */

async function groupContextAndLogout() {
  const G = 'F. Context switch, logout, CSRF lifecycle';
  let r = await call(admin, 'GET', '/api/v1/context');
  record(G, 'context lists admin choices', 'GET /api/v1/context', r, { status: 200, ok: true });
  r = await call(admin, 'POST', '/api/v1/context', { json: { role: 'Admin' }, csrf: true });
  record(G, 'context switch to Admin', 'POST /api/v1/context {Admin}', r, { status: 200, ok: true });

  r = await call(admin, 'POST', '/api/v1/auth/logout', { csrf: true });
  record(G, 'logout succeeds', 'POST /api/v1/auth/logout', r, { status: 200, ok: true });
  r = await call(admin, 'GET', '/api/v1/auth/me');
  record(G, 'session is gone after logout', 'GET /api/v1/auth/me', r, { status: 401, ok: false, code: 'UNAUTHORIZED' });

  r = await call(admin, 'POST', '/api/v1/auth/refresh', { csrf: true });
  record(G, 'refresh without a token is 401', 'POST /api/v1/auth/refresh', r, { status: 401, ok: false, code: 'UNAUTHORIZED' });
}

/* --------------------------------- group H --------------------------------- */

async function groupRemaining() {
  const G = 'H. Directory, lifecycle, user edit, credentials, recovery';
  let r;

  // --- course directory ---
  r = await call(admin, 'GET', '/api/v1/courses/directory/lookups?view=All');
  record(G, 'directory lookups', 'GET /api/v1/courses/directory/lookups', r, { status: 200, ok: true });
  r = await call(admin, 'GET', '/api/v1/courses/directory?view=All');
  record(G, 'directory list', 'GET /api/v1/courses/directory', r, { status: 200, ok: true });
  r = await call(admin, 'GET', '/api/v1/courses/directory?view=All&fromDate=2026-12-31&toDate=2026-01-01');
  record(G, 'directory rejects an inverted date range', 'GET /api/v1/courses/directory (bad dates)', r, { status: 400, ok: false, code: 'QUERY_INVALID' });
  r = await call(admin, 'GET', '/api/v1/courses/directory/999999?view=All');
  record(G, 'unknown directory course is 404', 'GET /api/v1/courses/directory/999999', r, { status: 404, ok: false, code: 'COURSE_NOT_FOUND' });
  r = await call(dean, 'GET', '/api/v1/courses/directory?view=Dean');
  record(G, 'dean directory view', 'GET /api/v1/courses/directory?view=Dean', r, { status: 200, ok: true });

  // --- fresh direct course for lifecycle tests ---
  r = await call(admin, 'GET', '/api/v1/courses/direct/lookups');
  const trackId = r.json?.data?.tracks?.[0]?.id ?? null;
  let courseId = null;
  if (trackId) {
    r = await call(admin, 'POST', '/api/v1/courses/direct', {
      json: {
        titleAr: `دورة دورة حياة ${RUN_ID}`, titleEn: 'Lifecycle audit course', trackId,
        deliveryMode: 'Online', objectivesAr: 'أهداف', objectivesEn: null,
        targetAudienceAr: 'الجميع', targetAudienceEn: null, totalHours: 4,
        plannedStartDate: '2027-01-10', plannedEndDate: '2027-01-20',
        audienceType: 'General', targetCenterId: CENTER_ID, relatedCenterId: null,
        registrationDeadline: '2027-01-05 12:00', maxParticipants: 10, hasExam: false,
        evaluationEndDate: '2027-01-31', instructorChoice: 'ManagedByQuality',
        instructorUserId: null, externalNameAr: null, externalNameEn: null,
        externalOrgAr: null, externalOrgEn: null, externalEmail: null,
        councilLetterFileId: null, instructorCvFileId: null, isHistorical: false,
        historicalNotes: null
      }, csrf: true
    });
    courseId = r.json?.data?.id ?? null;
    if (!courseId) {
      failures++;
      results.push({ group: G, name: 'create lifecycle course', request: 'POST /api/v1/courses/direct', status: r.status, pass: false, reasons: ['no course id returned'] });
    }
  }

  const version = async id => {
    const res = await call(admin, 'GET', `/api/v1/courses/directory/${id}?view=All`);
    return res.json?.data?.course?.version ?? null;
  };

  if (courseId) {
    r = await call(admin, 'GET', `/api/v1/courses/directory/${courseId}?view=All`);
    record(G, 'directory course details', `GET /api/v1/courses/directory/${courseId}`, r, { status: 200, ok: true });
    const courseDetails = r.json?.data;
    const edit = {
      expectedVersion: courseDetails?.course?.version ?? 0,
      titleAr: courseDetails?.course?.titleAr ?? 'دورة اختبار',
      titleEn: courseDetails?.course?.titleEn ?? null,
      objectivesAr: 'أهداف محدثة عبر HTTP',
      objectivesEn: courseDetails?.objectivesEn ?? null,
      targetAudienceAr: courseDetails?.targetAudienceAr ?? 'الجميع',
      targetAudienceEn: courseDetails?.targetAudienceEn ?? null,
      totalHours: courseDetails?.totalHours ?? 4,
      plannedStartDate: courseDetails?.course?.plannedStartDate ?? '2027-01-10',
      plannedEndDate: courseDetails?.course?.plannedEndDate ?? '2027-01-20',
      maxParticipants: courseDetails?.maxParticipants ?? 10,
      trackId: courseDetails?.trackId ?? trackId,
      deliveryMode: courseDetails?.deliveryMode ?? 'Online',
      audienceType: courseDetails?.audienceType ?? 'General',
      targetCenterId: courseDetails?.targetCenterId ?? null,
      registrationDeadline: '2027-01-04T09:00:00Z'
    };
    r = await call(admin, 'PUT', `/api/v1/courses/${courseId}/details`, { json: edit, csrf: true });
    record(G, 'admin edits course details', `PUT /api/v1/courses/${courseId}/details`, r, { status: 200, ok: true });
    r = await call(admin, 'PUT', `/api/v1/courses/${courseId}/details`, { json: edit, csrf: true });
    record(G, 'stale course detail edit rejected', `PUT /api/v1/courses/${courseId}/details (stale)`, r, { status: 400, ok: false, code: 'CONFLICT' });
    r = await call(quality, 'PUT', `/api/v1/courses/${courseId}/attendance-threshold`, {
      json: { expectedVersion: await version(courseId), percent: 85 }, csrf: true
    });
    record(G, 'quality edits attendance threshold', `PUT /api/v1/courses/${courseId}/attendance-threshold`, r, { status: 200, ok: true });
    r = await call(admin, 'PUT', `/api/v1/courses/${courseId}/attendance-threshold`, {
      json: { expectedVersion: await version(courseId), percent: 101 }, csrf: true
    });
    record(G, 'invalid attendance threshold rejected', `PUT /api/v1/courses/${courseId}/attendance-threshold (101)`, r, { status: 400, ok: false, code: 'THRESHOLD_INVALID' });
    r = await call(quality, 'GET', `/api/v1/courses/${courseId}/instructor/search?external=false&search=${QUALITY.login}`);
    record(G, 'quality searches course instructors', `GET /api/v1/courses/${courseId}/instructor/search`, r, { status: 200, ok: true });
    if (ids.quality) {
      r = await call(quality, 'PUT', `/api/v1/courses/${courseId}/instructor`, {
        json: { expectedVersion: await version(courseId), choice: 'Internal',
          userId: ids.quality, nameAr: null, nameEn: null,
          organizationAr: null, organizationEn: null }, csrf: true
      });
      record(G, 'quality assigns registered instructor', `PUT /api/v1/courses/${courseId}/instructor`, r, { status: 200, ok: true });
    }
    r = await call(admin, 'POST', `/api/v1/courses/${courseId}/force-status`, {
      json: { expectedVersion: await version(courseId), targetStatus: 'Rejected',
        reason: '', confirmed: true }, csrf: true
    });
    record(G, 'forced status requires a reason', `POST /api/v1/courses/${courseId}/force-status (no reason)`, r, { status: 400, ok: false, code: 'REASON_REQUIRED' });
    r = await call(dean, 'POST', `/api/v1/courses/${courseId}/force-status`, {
      json: { expectedVersion: await version(courseId), targetStatus: 'Rejected',
        reason: 'محاولة عميد', confirmed: true }, csrf: true
    });
    record(G, 'dean cannot force course status', `POST /api/v1/courses/${courseId}/force-status (dean)`, r, { status: 403, ok: false, code: 'FORBIDDEN' });
    r = await call(admin, 'POST', `/api/v1/courses/${courseId}/force-status`, {
      json: { expectedVersion: await version(courseId), targetStatus: 'Rejected',
        reason: 'تصحيح اختبار HTTP', confirmed: true }, csrf: true
    });
    record(G, 'admin forces rejected status', `POST /api/v1/courses/${courseId}/force-status (Rejected)`, r, { status: 200, ok: true });
    r = await call(admin, 'POST', `/api/v1/courses/${courseId}/force-status`, {
      json: { expectedVersion: await version(courseId), targetStatus: 'Approved',
        reason: 'استعادة اختبار HTTP', confirmed: true }, csrf: true
    });
    record(G, 'admin restores approved status', `POST /api/v1/courses/${courseId}/force-status (Approved)`, r, { status: 200, ok: true });
    r = await call(admin, 'GET', `/courses/${courseId}`, { accept: 'text/html' });
    record(G, 'course detail edit page renders', `GET /courses/${courseId}`, r, { status: 200, html: true, contains: 'course-details-instructor-dialog' });
    r = await call(admin, 'GET', `/api/v1/courses/${courseId}/admin/deactivation-preview`);
    record(G, 'deactivation preview', `GET /api/v1/courses/${courseId}/admin/deactivation-preview`, r, { status: 200, ok: true });
    r = await call(admin, 'POST', `/api/v1/courses/${courseId}/admin/deactivate`, { json: { expectedVersion: await version(courseId), reason: '   ', certificateAction: null }, csrf: true });
    record(G, 'deactivation requires a reason', `POST /api/v1/courses/${courseId}/admin/deactivate (no reason)`, r, { status: 400, ok: false, code: 'REASON_REQUIRED' });
    r = await call(admin, 'POST', `/api/v1/courses/${courseId}/admin/deactivate`, { json: { expectedVersion: await version(courseId), reason: 'اختبار تعطيل', certificateAction: null }, csrf: true });
    record(G, 'admin deactivates a course', `POST /api/v1/courses/${courseId}/admin/deactivate`, r, { status: 200, ok: true });
    r = await call(admin, 'POST', `/api/v1/courses/${courseId}/admin/deactivate`, { json: { expectedVersion: await version(courseId), reason: 'مرة أخرى', certificateAction: null }, csrf: true });
    record(G, 'double deactivation is rejected', `POST /api/v1/courses/${courseId}/admin/deactivate (again)`, r, { status: 400, ok: false, code: 'COURSE_ALREADY_DISABLED' });
    r = await call(admin, 'POST', `/api/v1/courses/${courseId}/admin/restore`, { json: { expectedVersion: await version(courseId) }, csrf: true });
    record(G, 'admin restores a course', `POST /api/v1/courses/${courseId}/admin/restore`, r, { status: 200, ok: true });
    r = await call(admin, 'POST', `/api/v1/courses/${courseId}/admin/restore`, { json: { expectedVersion: await version(courseId) }, csrf: true });
    record(G, 'double restore is rejected', `POST /api/v1/courses/${courseId}/admin/restore (again)`, r, { status: 400, ok: false, code: 'COURSE_ALREADY_ACTIVE' });
    r = await call(admin, 'POST', `/api/v1/courses/${courseId}/admin/archive`, { json: { expectedVersion: await version(courseId) }, csrf: true });
    record(G, 'admin archives a course', `POST /api/v1/courses/${courseId}/admin/archive`, r, { status: 200, ok: true });
    r = await call(admin, 'POST', `/api/v1/courses/${courseId}/admin/archive`, { json: { expectedVersion: await version(courseId) }, csrf: true });
    record(G, 'double archive is rejected', `POST /api/v1/courses/${courseId}/admin/archive (again)`, r, { status: 400, ok: false, code: 'COURSE_ALREADY_ARCHIVED' });
    r = await call(admin, 'POST', `/api/v1/courses/${courseId}/admin/unarchive`, { json: { expectedVersion: await version(courseId) }, csrf: true });
    record(G, 'admin unarchives a course', `POST /api/v1/courses/${courseId}/admin/unarchive`, r, { status: 200, ok: true });
    r = await call(admin, 'POST', `/api/v1/courses/${courseId}/admin/reopen`, { json: { expectedVersion: await version(courseId), confirmed: false }, csrf: true });
    record(G, 'reopen requires confirmation', `POST /api/v1/courses/${courseId}/admin/reopen (unconfirmed)`, r, { status: 400, ok: false, code: 'CONFIRM_REQUIRED' });
    r = await call(admin, 'POST', `/api/v1/courses/${courseId}/admin/reopen`, { json: { expectedVersion: await version(courseId), confirmed: true }, csrf: true });
    record(G, 'reopen of an unfinished course is rejected', `POST /api/v1/courses/${courseId}/admin/reopen`, r, { status: 400, ok: false, code: 'REOPEN_NOT_ALLOWED' });
    r = await call(admin, 'GET', '/api/v1/courses/999999/admin/deactivation-preview');
    record(G, 'preview of an unknown course is rejected', 'GET /api/v1/courses/999999/admin/deactivation-preview', r, { status: 400, ok: false, code: 'COURSE_NOT_FOUND' });
    r = await call(dean, 'POST', `/api/v1/courses/${courseId}/admin/deactivate`, { json: { expectedVersion: await version(courseId), reason: 'محاولة عميد', certificateAction: null }, csrf: true });
    record(G, 'dean cannot deactivate courses', `POST /api/v1/courses/${courseId}/admin/deactivate (dean)`, r, { status: 403, ok: false, code: 'FORBIDDEN' });
  }

  // --- sync side import selection ---
  r = await call(admin, 'GET', '/api/v1/sync/sides');
  const side = Array.isArray(r.json?.data) ? r.json.data[0] : null;
  if (side) {
    r = await call(admin, 'PUT', `/api/v1/sync/sides/${side.sideCode}/imported`, { json: { imported: !side.isImported }, csrf: true });
    record(G, 'toggle side import selection', `PUT /api/v1/sync/sides/${side.sideCode}/imported`, r, { status: 200, ok: true });
    r = await call(admin, 'PUT', `/api/v1/sync/sides/${side.sideCode}/imported`, { json: { imported: side.isImported }, csrf: true });
    record(G, 'restore side import selection', `PUT /api/v1/sync/sides/${side.sideCode}/imported (restore)`, r, { status: 200, ok: true });
  }
  r = await call(admin, 'PUT', '/api/v1/sync/sides/999999/imported', { json: { imported: true }, csrf: true });
  record(G, 'unknown sync side is 404', 'PUT /api/v1/sync/sides/999999/imported', r, { status: 404, ok: false, code: 'SIDE_NOT_FOUND' });

  // --- user edit ---
  const details = async id => (await call(admin, 'GET', `/api/v1/users/${id}`)).json?.data ?? null;
  if (ids.local) {
    let d = await details(ids.local);
    r = await call(admin, 'PUT', `/api/v1/users/${ids.local}`, {
      json: { expectedVersion: d?.version ?? 0, fullNameEn: `Local Audit ${RUN_ID}`, emailOverride: null, preferredLanguage: 'Ar', accessOverrideAllow: false, accessOverrideReason: null }, csrf: true
    });
    record(G, 'edit user', `PUT /api/v1/users/${ids.local}`, r, { status: 200, ok: true });
    r = await call(admin, 'PUT', `/api/v1/users/${ids.local}`, {
      json: { expectedVersion: d?.version ?? 0, fullNameEn: null, emailOverride: null, preferredLanguage: 'Ar', accessOverrideAllow: false, accessOverrideReason: null }, csrf: true
    });
    record(G, 'stale user edit conflicts', `PUT /api/v1/users/${ids.local} (stale)`, r, { status: 409, ok: false, code: 'CONFLICT' });
    d = await details(ids.local);
    r = await call(admin, 'PUT', `/api/v1/users/${ids.local}`, {
      json: { expectedVersion: d?.version ?? 0, fullNameEn: null, emailOverride: 'not-an-email', preferredLanguage: 'Ar', accessOverrideAllow: false, accessOverrideReason: null }, csrf: true
    });
    record(G, 'invalid email is rejected', `PUT /api/v1/users/${ids.local} (bad email)`, r, { status: 400, ok: false, code: 'EMAIL_INVALID' });
    d = await details(ids.local);
    r = await call(admin, 'PUT', `/api/v1/users/${ids.local}`, {
      json: { expectedVersion: d?.version ?? 0, fullNameEn: null, emailOverride: null, preferredLanguage: 'Ar', accessOverrideAllow: true, accessOverrideReason: null }, csrf: true
    });
    record(G, 'access override requires a reason', `PUT /api/v1/users/${ids.local} (no reason)`, r, { status: 400, ok: false, code: 'REASON_REQUIRED' });
    r = await call(admin, 'PUT', '/api/v1/users/999999', {
      json: { expectedVersion: 1, fullNameEn: null, emailOverride: null, preferredLanguage: 'Ar', accessOverrideAllow: false, accessOverrideReason: null }, csrf: true
    });
    record(G, 'editing an unknown user is 404', 'PUT /api/v1/users/999999', r, { status: 404, ok: false, code: 'USER_NOT_FOUND' });
  }

  // --- credentials administration ---
  if (ids.local) {
    const d = await details(ids.local);
    r = await call(admin, 'PUT', `/api/v1/credentials/${ids.local}/password`, {
      json: { expectedVersion: Math.max(0, (d?.version ?? 1) - 1), newPassword: 'Xx#12345678', requireChangeAtNextLogin: false }, csrf: true
    });
    record(G, 'stale credential version conflicts', `PUT /api/v1/credentials/${ids.local}/password (stale)`, r, { status: 409, ok: false, code: 'CONFLICT' });
    r = await call(admin, 'PUT', `/api/v1/credentials/${ids.local}/password`, {
      json: { expectedVersion: d?.version ?? 0, newPassword: 'short', requireChangeAtNextLogin: false }, csrf: true
    });
    record(G, 'weak password is rejected by policy', `PUT /api/v1/credentials/${ids.local}/password (weak)`, r, { status: 400, ok: false, code: 'PWD_TOO_SHORT' });
    r = await call(admin, 'PUT', `/api/v1/credentials/${ids.local}/password`, {
      json: { expectedVersion: d?.version ?? 0, newPassword: null, requireChangeAtNextLogin: false }, csrf: true
    });
    record(G, 'null password is a validation error, not a server error', `PUT /api/v1/credentials/${ids.local}/password (null)`, r, { status: 400, ok: false, code: 'VALIDATION_FAILED' });
    r = await call(admin, 'PUT', '/api/v1/credentials/999999/password', {
      json: { expectedVersion: 1, newPassword: 'Xx#12345678', requireChangeAtNextLogin: false }, csrf: true
    });
    record(G, 'credential change for unknown user is 404', 'PUT /api/v1/credentials/999999/password', r, { status: 404, ok: false, code: 'USER_NOT_FOUND' });
  }
  r = await call(admin, 'GET', '/api/v1/settings');
  const shared = Array.isArray(r.json?.data) ? r.json.data.find(x => x.key === 'auth.shared_initial_password') : null;
  if (shared) {
    r = await call(admin, 'PUT', '/api/v1/credentials/shared-initial', { json: { expectedVersion: Math.max(0, shared.version - 1), password: '' }, csrf: true });
    record(G, 'stale shared-password version conflicts', 'PUT /api/v1/credentials/shared-initial (stale)', r, { status: 409, ok: false, code: 'CONFLICT' });
    r = await call(admin, 'PUT', '/api/v1/credentials/shared-initial', { json: { expectedVersion: shared.version, password: '' }, csrf: true });
    record(G, 'clear shared initial password', 'PUT /api/v1/credentials/shared-initial', r, { status: 200, ok: true });
  }
  if (ids.local) {
    r = await call(admin, 'POST', `/api/v1/credentials/${ids.local}/resolve-uncertain`, { json: { received: true }, csrf: true });
    record(G, 'resolving a certain delivery is rejected', `POST /api/v1/credentials/${ids.local}/resolve-uncertain`, r, { status: 400, ok: false, code: 'NOT_UNCERTAIN' });
  }
  r = await call(admin, 'POST', '/api/v1/credentials/999999/resolve-uncertain', { json: { received: true }, csrf: true });
  record(G, 'resolving for unknown user is 404', 'POST /api/v1/credentials/999999/resolve-uncertain', r, { status: 404, ok: false, code: 'USER_NOT_FOUND' });

  // --- auth recovery & self-service password ---
  r = await call(anon, 'POST', '/api/v1/auth/forgot-password', { json: { loginName: '900001', email: 'nobody@example.com' }, csrf: true });
  record(G, 'forgot password answers generically', 'POST /api/v1/auth/forgot-password', r, { status: 200, ok: true });
  r = await call(anon, 'POST', '/api/v1/auth/forgot-password', { json: {}, csrf: true });
  record(G, 'empty forgot-password payload is generic', 'POST /api/v1/auth/forgot-password ({})', r, { status: 200, ok: true });
  r = await call(anon, 'POST', '/api/v1/auth/forgot-password', { json: { loginName: '900001', email: 'a@b.co' }, csrf: 'bad' });
  record(G, 'forgot password enforces CSRF', 'POST /api/v1/auth/forgot-password (bad token)', r, { status: 400, ok: false, code: 'CSRF_INVALID' });
  r = await call(anon, 'POST', '/api/v1/auth/account-password', { json: { currentPassword: 'x', newPassword: 'Yy#12345678', confirmation: 'Yy#12345678' }, csrf: true });
  record(G, 'anonymous password change is 401', 'POST /api/v1/auth/account-password', r, { status: 401, ok: false, code: 'UNAUTHORIZED' });
  r = await call(member, 'POST', '/api/v1/auth/account-password', { json: { currentPassword: 'definitely-wrong', newPassword: 'Yy#12345678', confirmation: 'Yy#12345678' }, csrf: true });
  record(G, 'wrong current password is rejected', 'POST /api/v1/auth/account-password (wrong)', r, { status: 400, ok: false, code: 'AUTH_INVALID' });
  r = await call(member, 'POST', '/api/v1/auth/account-password', { json: {}, csrf: true });
  record(G, 'empty password payload is rejected, not a server error', 'POST /api/v1/auth/account-password ({})', r, { status: 400, ok: false, code: 'AUTH_INVALID' });
  r = await call(member, 'POST', '/api/v1/auth/change-password', { json: { newPassword: 'Yy#12345678', confirmation: 'Yy#12345678' }, csrf: true });
  record(G, 'forced-change route refuses normal sessions', 'POST /api/v1/auth/change-password', r, { status: 403, ok: false, code: 'FORBIDDEN' });
}

/* --------------------------------- group I --------------------------------- */

function buildOnePagePdf() {
  // Minimal valid single-page PDF (A4 portrait) with a correct xref table.
  const objects = [
    '<< /Type /Catalog /Pages 2 0 R >>',
    '<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
    '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << >> >>',
    '<< /Length 44 >>\nstream\nBT /F1 12 Tf 72 720 Td (ZU certificate template) Tj ET\nendstream'
  ];
  const chunks = ['%PDF-1.4\n'];
  const offsets = [0];
  let position = 0;
  for (let i = 0; i < objects.length; i++) {
    offsets.push(position);
    const body = `${i + 1} 0 obj\n${objects[i]}\nendobj\n`;
    chunks.push(body);
    position += body.length;
  }
  const startxref = position;
  let xref = `xref\n0 ${objects.length + 1}\n0000000000 65535 f \n`;
  for (let i = 1; i <= objects.length; i++)
    xref += `${String(offsets[i]).padStart(10, '0')} 00000 n \n`;
  chunks.push(xref);
  chunks.push(`trailer\n<< /Size ${objects.length + 1} /Root 1 0 R >>\nstartxref\n${startxref}\n%%EOF\n`);
  return Buffer.from(chunks.join(''), 'latin1');
}

function pdfField(x, y, w, h, size = 20) {
  return { x, y, w, h, font: 'Tajawal', size, weight: 'bold', color: '#022C5A',
    align: 'center', dir: 'rtl', maxLines: 1, shrinkToFit: true };
}

async function groupPhaseNew() {
  const G = 'I. Certificates, needs, pro-certs, vault, audit, notifications';
  let r;

  // Health endpoint (not enveloped, no authentication).
  r = await call(anon, 'GET', '/health', { accept: 'application/json' });
  const healthy = r.status === 200 && r.json?.status === 'healthy';
  results.push({ group: G, name: 'health endpoint reports healthy', request: 'GET /health',
    status: r.status, ok: null, code: null, message: null, pass: healthy,
    reasons: healthy ? [] : [`status=${r.status}`, JSON.stringify(r.json ?? null).slice(0, 120)] });
  if (!healthy) failures++;

  // Public verification of an unknown reference reveals nothing.
  r = await call(anon, 'GET', '/api/v1/public/verify/DOES-NOT-EXIST');
  record(G, 'unknown reference verifies as null', 'GET /api/v1/public/verify/{unknown}', r, { status: 200, ok: true });
  if (r.json?.data?.certificate !== null) {
    failures++;
    results.push({ group: G, name: 'unknown certificate hides all details', request: 'GET /api/v1/public/verify/{unknown}',
      status: r.status, ok: true, code: null, message: null, pass: false, reasons: ['certificate is not null'] });
  }

  // Certificate template upload (valid one-page PDF + layout).
  const layout = {
    unit: 'pt', origin: 'top-left',
    fields: {
      holder_name: pdfField(60, 300, 475, 40, 28),
      course_title: pdfField(60, 352, 475, 34, 22),
      course_period: pdfField(60, 396, 475, 24, 16),
      course_hours: pdfField(60, 424, 475, 24, 16),
      instructor_name: pdfField(60, 452, 475, 24, 16),
      issue_date: pdfField(60, 480, 475, 24, 16),
      reference_no: pdfField(60, 508, 475, 22, 14),
      qr: { x: 690, y: 470, size: 90 }
    }
  };
  const templateForm = new FormData();
  templateForm.append('file', new Blob([buildOnePagePdf()], { type: 'application/pdf' }), 'template.pdf');
  templateForm.append('layoutJson', JSON.stringify(layout));
  r = await call(admin, 'POST', '/api/v1/certificate-template', { form: templateForm, csrf: true });
  record(G, 'upload certificate template', 'POST /api/v1/certificate-template', r, { status: 200, ok: true });
  r = await call(admin, 'GET', '/api/v1/certificate-template');
  record(G, 'current template is version 1+', 'GET /api/v1/certificate-template', r, { status: 200, ok: true });
  r = await call(admin, 'POST', '/api/v1/certificate-template/preview', { csrf: true });
  const previewOk = r.status === 200 && r.text.startsWith('%PDF-');
  results.push({ group: G, name: 'template preview renders a PDF', request: 'POST /api/v1/certificate-template/preview',
    status: r.status, ok: null, code: null, message: null, pass: previewOk,
    reasons: previewOk ? [] : [`status=${r.status}`, `content-type=${r.headers.get('content-type')}`] });
  if (!previewOk) failures++;

  // Full issuance path: direct course → register → lecture → attendance → finish → survey → impact → certificate.
  r = await call(admin, 'GET', '/api/v1/courses/direct/lookups');
  const trackId = r.json?.data?.tracks?.[0]?.id ?? null;
  const certCourseId = trackId ? (await call(admin, 'POST', '/api/v1/courses/direct', {
    json: {
      titleAr: `دورة إصدار شهادة ${RUN_ID}`, titleEn: null, trackId,
      deliveryMode: 'Online', objectivesAr: 'أهداف الإصدار', objectivesEn: null,
      targetAudienceAr: 'الجميع', targetAudienceEn: null, totalHours: 5,
      plannedStartDate: '2027-02-01', plannedEndDate: '2027-02-10',
      audienceType: 'General', targetCenterId: CENTER_ID, relatedCenterId: null,
      registrationDeadline: '2027-01-25 12:00', maxParticipants: 15, hasExam: false,
      evaluationEndDate: '2027-03-15', instructorChoice: 'ManagedByQuality',
      instructorUserId: null, externalNameAr: null, externalNameEn: null,
      externalOrgAr: null, externalOrgEn: null, externalEmail: null,
      councilLetterFileId: null, instructorCvFileId: null, isHistorical: false,
      historicalNotes: null
    }, csrf: true
  })).json?.data?.id ?? null : null;
  if (!certCourseId) {
    failures++;
    results.push({ group: G, name: 'create certificate course', request: 'POST /api/v1/courses/direct',
      status: r.status, pass: false, reasons: ['no course id'] });
  }

  let certificate = null;
  if (certCourseId) {
    r = await call(member, 'POST', `/api/v1/courses/${certCourseId}/self-registration`, { csrf: true });
    record(G, 'member registers in the certificate course', `POST /api/v1/courses/${certCourseId}/self-registration`, r, { status: 200, ok: true });

    const startsAt = ammanWallClock(3600_000);
    const endsAt = ammanWallClock(7200_000);
    r = await call(admin, 'POST', `/api/v1/courses/${certCourseId}/lectures`, {
      json: { titleAr: 'محاضرة الإصدار', titleEn: null, mode: 'Online', startsAt, endsAt,
        locationAr: null, locationEn: null, teamsUrl: 'https://teams.microsoft.com/l/zu', notesAr: null },
      csrf: true
    });
    record(G, 'admin creates a lecture', `POST /api/v1/courses/${certCourseId}/lectures`, r, { status: 201, ok: true });
    const lectureId = r.json?.data?.id ?? null;

    let participantId = null;
    if (ids.member) {
      r = await call(admin, 'GET', `/api/v1/courses/${certCourseId}/participants`);
      record(G, 'participants list works', `GET /api/v1/courses/${certCourseId}/participants`, r, { status: 200, ok: true });
    }
    if (lectureId) {
      // The attendance roster is the endpoint that exposes participant ids.
      r = await call(admin, 'GET', `/api/v1/lectures/${lectureId}/attendance/roster`);
      record(G, 'attendance roster lists participants', `GET /api/v1/lectures/${lectureId}/attendance/roster`, r, { status: 200, ok: true });
      const roster = Array.isArray(r.json?.data) ? r.json.data : [];
      participantId = roster.find(x => x.userId === ids.member)?.participantId ?? null;
    }
    if (lectureId && participantId) {
      r = await call(admin, 'PUT', `/api/v1/lectures/${lectureId}/attendance/roster/${participantId}`,
        { json: { isPresent: true, reason: 'تسجيل اختباري' }, csrf: true });
      record(G, 'manual attendance marks the member present', `PUT /api/v1/lectures/${lectureId}/attendance/roster/${participantId}`, r, { status: 200, ok: true });
    }

    r = await call(admin, 'POST', `/api/v1/courses/${certCourseId}/finish`, { csrf: true });
    record(G, 'course finish succeeds', `POST /api/v1/courses/${certCourseId}/finish`, r, { status: 200, ok: true });

    r = await call(member, 'GET', `/api/v1/courses/${certCourseId}/evaluation`);
    record(G, 'evaluation form is served', `GET /api/v1/courses/${certCourseId}/evaluation`, r, { status: 200, ok: true });
    const questions = Array.isArray(r.json?.data?.questions) ? r.json.data.questions : [];
    const answers = questions.map(q => ({ questionId: q.id, optionId: (q.options || [])[0]?.id }));
    if (answers.length) {
      r = await call(member, 'POST', `/api/v1/courses/${certCourseId}/evaluation`, { json: { answers }, csrf: true });
      record(G, 'member submits the evaluation', `POST /api/v1/courses/${certCourseId}/evaluation`, r, { status: 200, ok: true });
    }

    const impactForm = new FormData();
    impactForm.append('kind', 'ImpactFile');
    impactForm.append('file', new Blob([Buffer.from('%PDF-1.4\n%zu impact\n%%EOF\n')], { type: 'application/pdf' }), 'impact.pdf');
    r = await call(member, 'POST', '/api/v1/files', { form: impactForm, csrf: true });
    record(G, 'member uploads an impact file', 'POST /api/v1/files (ImpactFile)', r, { status: 201, ok: true });
    const impactFileId = r.json?.data?.id ?? null;
    let submissionId = null;
    if (impactFileId) {
      r = await call(member, 'POST', `/api/v1/courses/${certCourseId}/impact`, { json: { type: 'StudyPlan', fileId: impactFileId }, csrf: true });
      record(G, 'member submits the impact', `POST /api/v1/courses/${certCourseId}/impact`, r, { status: 201, ok: true });
      submissionId = r.json?.data?.id ?? null;
      r = await call(member, 'GET', `/api/v1/courses/${certCourseId}/impact`);
      record(G, 'member lists own impact submissions', `GET /api/v1/courses/${certCourseId}/impact`, r, { status: 200, ok: true });
    }
    if (submissionId) {
      r = await call(admin, 'GET', `/api/v1/courses/${certCourseId}/impact/pending`);
      record(G, 'impact pending queue works', `GET /api/v1/courses/${certCourseId}/impact/pending`, r, { status: 200, ok: true });
      r = await call(admin, 'POST', `/api/v1/courses/${certCourseId}/impact/${submissionId}/review`, { json: { approve: true, notes: 'أثر جيد' }, csrf: true });
      record(G, 'impact review approves', `POST /api/v1/courses/${certCourseId}/impact/${submissionId}/review`, r, { status: 200, ok: true });
    }

    r = await call(member, 'GET', '/api/v1/certificates/mine');
    record(G, 'member lists own certificates', 'GET /api/v1/certificates/mine', r, { status: 200, ok: true });
    certificate = (r.json?.data || []).find(x => x.courseId === certCourseId) ?? null;
    const issued = !!certificate;
    results.push({ group: G, name: 'certificate issued automatically', request: 'GET /api/v1/certificates/mine',
      status: r.status, ok: true, code: null, message: null, pass: issued,
      reasons: issued ? [] : ['no certificate for the finished course'] });
    if (!issued) failures++;
  }

  let reference = certificate?.referenceNo ?? null;
  if (reference) {
    r = await call(anon, 'GET', `/api/v1/public/verify/${encodeURIComponent(reference)}`);
    record(G, 'public verify finds the certificate', `GET /api/v1/public/verify/{ref}`, r, { status: 200, ok: true });
    const card = r.json?.data?.certificate;
    const memberRecord = await call(admin, 'GET', `/api/v1/users/${ids.member}`);
    const expectedHolder = memberRecord.json?.data?.fullNameAr ?? null;
    const holderOk = !!card && !!expectedHolder && card.holderNameAr === expectedHolder;
    results.push({ group: G, name: 'verification shows the holder', request: 'GET /api/v1/public/verify/{ref}',
      status: r.status, ok: true, code: null, message: null, pass: holderOk,
      reasons: holderOk ? [] : [`holder=${card?.holderNameAr} expected=${expectedHolder}`] });
    if (!holderOk) failures++;

    r = await call(member, 'GET', `/api/v1/certificates/${certificate.id}/pdf`);
    const pdfOk = r.status === 200 && r.text.startsWith('%PDF-');
    results.push({ group: G, name: 'member downloads the certificate PDF', request: `GET /api/v1/certificates/${certificate.id}/pdf`,
      status: r.status, ok: null, code: null, message: null, pass: pdfOk,
      reasons: pdfOk ? [] : [`status=${r.status}`, `content-type=${r.headers.get('content-type')}`] });
    if (!pdfOk) failures++;

    r = await call(member, 'GET', `/api/v1/certificates/${certificate.id + 999}/pdf`);
    record(G, 'unknown certificate pdf is 404', `GET /api/v1/certificates/{unknown}/pdf`, r, { status: 404, ok: false, code: 'CERT_NOT_FOUND' });

    r = await call(admin, 'GET', `/api/v1/certificates?search=${encodeURIComponent(reference)}`);
    record(G, 'quality lists certificates', 'GET /api/v1/certificates?search=', r, { status: 200, ok: true });

    r = await call(admin, 'POST', `/api/v1/certificates/${certificate.id}/revoke`, { json: { reason: 'اختبار الإلغاء اليدوي' }, csrf: true });
    record(G, 'admin revokes the certificate', `POST /api/v1/certificates/${certificate.id}/revoke`, r, { status: 200, ok: true });
    r = await call(anon, 'GET', `/api/v1/public/verify/${encodeURIComponent(reference)}`);
    const revoked = r.json?.data?.certificate?.revoked === true;
    results.push({ group: G, name: 'revoked certificate verifies as revoked', request: 'GET /api/v1/public/verify/{ref}',
      status: r.status, ok: true, code: null, message: null, pass: revoked,
      reasons: revoked ? [] : ['certificate is not marked revoked'] });
    if (!revoked) failures++;
  }

  if (certCourseId) {
    r = await call(admin, 'GET', `/api/v1/evaluation/results/${certCourseId}`);
    const hasResponses = (r.json?.data?.responses ?? 0) >= 1;
    results.push({ group: G, name: 'evaluation results include the response', request: `GET /api/v1/evaluation/results/${certCourseId}`,
      status: r.status, ok: r.json?.ok ?? null, code: null, message: null, pass: hasResponses,
      reasons: hasResponses ? [] : [`responses=${r.json?.data?.responses}`, `code=${r.json?.code}`] });
    if (!hasResponses) failures++;
    r = await call(admin, 'PUT', `/api/v1/evaluation/period/${certCourseId}`, { json: { newEndDate: '2027-12-31' }, csrf: true });
    record(G, 'evaluation period extension works', `PUT /api/v1/evaluation/period/${certCourseId}`, r, { status: 200, ok: true });
  }

  // Training needs: dean submits → quality approves.
  const needLetter = new FormData();
  needLetter.append('kind', 'NeedLetter');
  needLetter.append('file', new Blob([Buffer.from('%PDF-1.4\n%zu need letter\n%%EOF\n')], { type: 'application/pdf' }), 'need.pdf');
  r = await call(dean, 'POST', '/api/v1/files', { form: needLetter, csrf: true });
  record(G, 'dean uploads the need letter', 'POST /api/v1/files (NeedLetter)', r, { status: 201, ok: true });
  const needLetterId = r.json?.data?.id ?? null;
  let needId = null;
  if (needLetterId) {
    r = await call(dean, 'POST', '/api/v1/needs', {
      json: { titleAr: `احتياج اختباري ${RUN_ID}`, titleEn: null, descriptionAr: 'وصف الاحتياج',
        descriptionEn: null, trackId: null, expectedParticipants: 12,
        preferredTimeframeAr: 'الفصل الأول', requestedInstructorUserId: null,
        requestedInstructorNoteAr: null, letterFileId: needLetterId }, csrf: true
    });
    record(G, 'dean submits a training need', 'POST /api/v1/needs', r, { status: 201, ok: true });
    needId = r.json?.data?.id ?? null;
  }
  if (needId) {
    r = await call(quality, 'GET', '/api/v1/needs?status=Submitted');
    record(G, 'quality lists pending needs', 'GET /api/v1/needs?status=Submitted', r, { status: 200, ok: true });
    r = await call(quality, 'POST', `/api/v1/needs/${needId}/decision`, { json: { approve: true, notes: 'معتمد' }, csrf: true });
    record(G, 'quality approves the need', `POST /api/v1/needs/${needId}/decision`, r, { status: 200, ok: true });
    r = await call(dean, 'GET', '/api/v1/needs');
    const approved = (r.json?.data?.items || []).some(x => x.id === needId && x.status === 'Approved');
    results.push({ group: G, name: 'dean sees the approved need', request: 'GET /api/v1/needs',
      status: r.status, ok: r.json?.ok ?? null, code: null, message: null, pass: approved,
      reasons: approved ? [] : ['need not approved/visible'] });
    if (!approved) failures++;
    r = await call(quality, 'POST', `/api/v1/needs/${needId}/decision`, { json: { approve: true, notes: null }, csrf: true });
    record(G, 'second need decision is rejected', `POST /api/v1/needs/${needId}/decision (again)`, r, { status: 400, ok: false, code: 'NEED_NOT_PENDING' });
  }

  // Professional certifications.
  r = await call(member, 'GET', '/api/v1/procerts/categories');
  record(G, 'pro-cert categories are listed', 'GET /api/v1/procerts/categories', r, { status: 200, ok: true });
  const categoryId = (r.json?.data || [])[0]?.id ?? null;
  let proCertId = null;
  if (categoryId) {
    const proForm = new FormData();
    proForm.append('kind', 'ProfessionalCertificate');
    proForm.append('file', new Blob([Buffer.from('%PDF-1.4\n%zu pro cert\n%%EOF\n')], { type: 'application/pdf' }), 'procert.pdf');
    r = await call(member, 'POST', '/api/v1/files', { form: proForm, csrf: true });
    record(G, 'member uploads the pro-cert file', 'POST /api/v1/files (ProfessionalCertificate)', r, { status: 201, ok: true });
    const proFileId = r.json?.data?.id ?? null;
    if (proFileId) {
      r = await call(member, 'POST', '/api/v1/procerts', {
        json: { name: `Microsoft Certified ${RUN_ID}`, nameAr: null,
          issuerName: 'Microsoft', issuerWebsiteUrl: 'https://www.microsoft.com',
          categoryId, description: null, credentialCode: `ZU-${RUN_ID}`,
          verifyUrl: null, issuedDate: '2026-09-01', expiryDate: '2029-09-01',
          fileId: proFileId }, csrf: true
      });
      record(G, 'member submits a professional certificate', 'POST /api/v1/procerts', r, { status: 201, ok: true });
      proCertId = r.json?.data?.id ?? null;
      r = await call(member, 'POST', '/api/v1/procerts', {
        json: { name: 'duplicate', nameAr: null, issuerName: 'Microsoft',
          issuerWebsiteUrl: null, categoryId, description: null,
          credentialCode: `ZU-${RUN_ID}`, verifyUrl: null,
          issuedDate: '2026-09-01', expiryDate: null, fileId: proFileId }, csrf: true
      });
      record(G, 'duplicate pro-cert is rejected', 'POST /api/v1/procerts (duplicate)', r, { status: 400, ok: false, code: 'PROCERT_DUPLICATE' });
    }
  }
  if (proCertId) {
    r = await call(member, 'GET', '/api/v1/procerts');
    record(G, 'member lists own pro-certs', 'GET /api/v1/procerts', r, { status: 200, ok: true });
    r = await call(quality, 'POST', `/api/v1/procerts/${proCertId}/decision`, { json: { approve: true, notes: null }, csrf: true });
    record(G, 'quality approves the pro-cert', `POST /api/v1/procerts/${proCertId}/decision`, r, { status: 200, ok: true });
    if (ids.member) {
      r = await call(admin, 'GET', `/api/v1/profiles/${ids.member}`);
      const inProfile = (r.json?.data?.proCertCount ?? 0) >= 1;
      results.push({ group: G, name: 'approved pro-cert appears in the profile', request: `GET /api/v1/profiles/${ids.member}`,
        status: r.status, ok: r.json?.ok ?? null, code: null, message: null, pass: inProfile,
        reasons: inProfile ? [] : [`proCertCount=${r.json?.data?.proCertCount}`] });
      if (!inProfile) failures++;
    }
    r = await call(admin, 'GET', '/api/v1/procerts/stats');
    record(G, 'pro-cert statistics are served', 'GET /api/v1/procerts/stats', r, { status: 200, ok: true });
    r = await call(admin, 'GET', '/api/v1/procerts/stats/export');
    const csvOk = r.status === 200 && (r.headers.get('content-type') || '').includes('text/csv');
    results.push({ group: G, name: 'pro-cert statistics export CSV', request: 'GET /api/v1/procerts/stats/export',
      status: r.status, ok: null, code: null, message: null, pass: csvOk,
      reasons: csvOk ? [] : [`status=${r.status}`, `content-type=${r.headers.get('content-type')}`] });
    if (!csvOk) failures++;
  }

  // Vault.
  const minutesForm = new FormData();
  minutesForm.append('kind', 'QualityMinutes');
  minutesForm.append('file', new Blob([Buffer.from('%PDF-1.4\n%zu minutes\n%%EOF\n')], { type: 'application/pdf' }), 'minutes.pdf');
  r = await call(quality, 'POST', '/api/v1/files', { form: minutesForm, csrf: true });
  record(G, 'quality uploads minutes file', 'POST /api/v1/files (QualityMinutes)', r, { status: 201, ok: true });
  const minutesId = r.json?.data?.id ?? null;
  if (minutesId) {
    r = await call(quality, 'POST', '/api/v1/vault', { json: { fileId: minutesId, docType: 'QualityMinutes', titleAr: `محضر اختباري ${RUN_ID}`, courseId: null, centerId: null }, csrf: true });
    record(G, 'quality links a vault document', 'POST /api/v1/vault', r, { status: 200, ok: true });
    r = await call(quality, 'GET', `/api/v1/vault?search=${encodeURIComponent(`محضر اختباري ${RUN_ID}`)}`);
    record(G, 'vault lists the document', 'GET /api/v1/vault', r, { status: 200, ok: true });
  }

  // Audit log pages/APIs.
  r = await call(admin, 'GET', '/api/v1/audit');
  record(G, 'audit log is listed', 'GET /api/v1/audit', r, { status: 200, ok: true });
  r = await call(admin, 'GET', '/api/v1/audit/live?afterId=0');
  record(G, 'live feed returns rows', 'GET /api/v1/audit/live?afterId=0', r, { status: 200, ok: true });
  r = await call(admin, 'GET', '/api/v1/audit/export');
  const auditCsv = r.status === 200 && (r.headers.get('content-type') || '').includes('text/csv');
  results.push({ group: G, name: 'audit CSV export', request: 'GET /api/v1/audit/export',
    status: r.status, ok: null, code: null, message: null, pass: auditCsv,
    reasons: auditCsv ? [] : [`status=${r.status}`] });
  if (!auditCsv) failures++;
  r = await call(quality, 'GET', '/api/v1/audit');
  record(G, 'quality cannot read the audit log', 'GET /api/v1/audit (quality)', r, { status: 403, ok: false, code: 'FORBIDDEN' });

  // Notifications.
  r = await call(member, 'GET', '/api/v1/notifications');
  record(G, 'notifications are listed', 'GET /api/v1/notifications', r, { status: 200, ok: true });
  const unread = r.json?.data?.unread ?? 0;
  const hasUnread = unread >= 1;
  results.push({ group: G, name: 'member has unread notifications', request: 'GET /api/v1/notifications',
    status: r.status, ok: true, code: null, message: null, pass: hasUnread,
    reasons: hasUnread ? [] : [`unread=${unread}`] });
  if (!hasUnread) failures++;
  r = await call(member, 'POST', '/api/v1/notifications/read-all', { csrf: true });
  record(G, 'mark all notifications read', 'POST /api/v1/notifications/read-all', r, { status: 200, ok: true });

  // Tracks.
  r = await call(quality, 'GET', '/api/v1/tracks');
  record(G, 'tracks are listed', 'GET /api/v1/tracks', r, { status: 200, ok: true });
  r = await call(quality, 'POST', '/api/v1/tracks', { json: { nameAr: `محور اختباري ${RUN_ID}`, nameEn: null, sortOrder: 99 }, csrf: true });
  record(G, 'quality creates a track', 'POST /api/v1/tracks', r, { status: 200, ok: true });

  // Profiles directory.
  r = await call(admin, 'GET', '/api/v1/profiles/directory');
  record(G, 'members directory is served', 'GET /api/v1/profiles/directory', r, { status: 200, ok: true });

  // New pages render.
  for (const [path, status] of [
    ['/admin/audit', 200], ['/admin/live-feed', 200], ['/admin/needs', 200],
    ['/admin/certificate-template', 200], ['/vault', 200], ['/quality/needs', 200],
    ['/quality/eval-form', 200], ['/quality/evaluations', 200], ['/quality/tracks', 200],
    ['/quality/certificates', 200], ['/quality/pro-certs', 200], ['/quality/pro-cert-stats', 200],
    ['/member/pro-certs', 200], ['/member/impact', 200], ['/member/teaching', 200],
    ['/member/enter-code', 200], ['/dean/members', 403], ['/shared/directory', 200],
    ['/dean/needs', 403], ['/verify', 200]
  ]) {
    r = await call(admin, 'GET', path, { accept: 'text/html' });
    record(G, `page ${path}`, `GET ${path}`, r, { status, html: status === 200 });
  }
}

/* ---------------------------------- run ----------------------------------- */

async function main() {
  const steps = [
    ['A', groupAnonymous], ['B', groupAdminAuthPages], ['C', groupAdminApi],
    ['D', groupDean], ['E', groupQualityFlow], ['G', groupCancellation],
    ['H', groupRemaining], ['I', groupPhaseNew], ['F', groupContextAndLogout]
  ];
  for (const [label, fn] of steps) {
    process.stdout.write(`\n[${label}] ${fn.name} ... `);
    try { await fn(); process.stdout.write('done'); }
    catch (e) { failures++; results.push({ group: label, name: fn.name, request: '-', status: 0, pass: false, reasons: [String(e && e.stack || e)] }); process.stdout.write('ERROR'); }
  }

  const total = results.length;
  const passed = results.filter(x => x.pass).length;
  const fs = await import('node:fs');
  fs.writeFileSync(`${OUT_DIR}/api-test-results.json`, JSON.stringify({ base: BASE, when: new Date().toISOString(), total, passed, failed: total - passed, results }, null, 2));

  const groups = [...new Set(results.map(x => x.group))];
  let md = `# ZU-LMS API test run\n\n- Base URL: ${BASE}\n- Run at: ${new Date().toISOString()}\n- Checks: **${total}**, passed: **${passed}**, failed: **${total - passed}**\n\n`;
  for (const g of groups) {
    const rows = results.filter(x => x.group === g);
    const bad = rows.filter(x => !x.pass).length;
    md += `## ${g} — ${rows.length - bad}/${rows.length} passed\n\n`;
    md += '| # | Check | Request | Status | Envelope | Result |\n|---|---|---|---|---|---|\n';
    rows.forEach((x, i) => {
      const env = x.ok === null ? '—' : x.ok ? 'ok:true' : `ok:false · ${x.code ?? '?'}`;
      md += `| ${i + 1} | ${x.name.replace(/\|/g, '\\|')} | \`${x.request.replace(/\|/g, '\\|')}\` | ${x.status} | ${env} | ${x.pass ? 'PASS' : 'FAIL — ' + x.reasons.join('; ').replace(/\|/g, '\\|')} |\n`;
    });
    md += '\n';
  }
  fs.writeFileSync(`${OUT_DIR}/api-test-report.md`, md);

  console.log(`\n\nTotal ${total}, passed ${passed}, failed ${total - passed}`);
  const failed = results.filter(x => !x.pass);
  if (failed.length) {
    console.log('\nFailures:');
    for (const f of failed) console.log(` - [${f.group}] ${f.name} :: ${f.request} :: ${f.status} :: ${f.reasons.join('; ')}`);
  }
  process.exitCode = failed.length ? 1 : 0;
}

main();
