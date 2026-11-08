// Triggers one university sync run against a running app and polls it to completion.
// Usage: node tools/api-tests/sync-run.mjs [baseUrl]
process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';

const base = (process.argv[2] ?? 'https://localhost:7275').replace(/\/$/, '');
const login = process.env.ZU_BOOTSTRAP_ADMIN_LOGIN ?? 'admin';
const password = process.env.ZU_BOOTSTRAP_ADMIN_PASSWORD;
if (!password) throw new Error('ZU_BOOTSTRAP_ADMIN_PASSWORD is required.');

const jar = new Map();
let sessionId = null;

function cookieHeader() {
  return [...jar.entries()].map(([k, v]) => `${k}=${v}`).join('; ');
}

function storeCookies(res) {
  const raw = res.headers.getSetCookie?.() ?? [];
  for (const line of raw) {
    const [pair] = line.split(';');
    const idx = pair.indexOf('=');
    if (idx > 0) jar.set(pair.slice(0, idx).trim(), pair.slice(idx + 1).trim());
  }
}

async function call(path, init = {}) {
  const headers = new Headers(init.headers ?? {});
  if (jar.size) headers.set('cookie', cookieHeader());
  if (sessionId) headers.set('X-ZU-Session', sessionId);
  const res = await fetch(`${base}${path}`, { ...init, headers, redirect: 'manual' });
  storeCookies(res);
  const text = await res.text();
  let json;
  try { json = text ? JSON.parse(text) : null; } catch { json = null; }
  if (json?.data?.sessionId) sessionId = json.data.sessionId;
  return { status: res.status, json, text };
}

async function csrf() {
  const r = await call('/api/v1/auth/csrf');
  const token = r.json?.data?.token ?? r.json?.data?.requestToken ?? r.json?.token;
  if (!token) throw new Error(`no CSRF token: ${r.status} ${r.text.slice(0, 200)}`);
  return token;
}

const token = await csrf();
const sign = await call('/api/v1/auth/login', {
  method: 'POST',
  headers: { 'content-type': 'application/json', RequestVerificationToken: token },
  body: JSON.stringify({ loginName: login, password, rememberMe: false })
});
console.log('login', sign.status, sign.json?.redirectUrl ?? sign.json?.code ?? '');
if (sign.status !== 200) {
  console.log(sign.text.slice(0, 500));
  process.exit(1);
}

const token2 = await csrf();
const start = await call('/api/v1/sync/runs', {
  method: 'POST',
  headers: { RequestVerificationToken: token2 }
});
console.log('start', start.status, start.text.slice(0, 300));
if (start.status !== 202) process.exit(1);
const id = start.json?.id ?? start.json?.data?.id;

const deadline = Date.now() + 15 * 60_000;
let last = '';
for (;;) {
  await new Promise((r) => setTimeout(r, 5000));
  const run = await call(`/api/v1/sync/runs/${id}`);
  const d = run.json?.data ?? run.json ?? {};
  const line = `${d.status} ${JSON.stringify(d.stats ?? d.sources ?? {})}`;
  if (line !== last) { console.log(line.slice(0, 800)); last = line; }
  if (d.finishedAt || ['Success', 'Failed', 'Cancelled'].includes(d.status)) {
    console.log('final', JSON.stringify(d).slice(0, 2000));
    process.exit(d.status === 'Success' ? 0 : 2);
  }
  if (Date.now() > deadline) { console.log('timeout'); process.exit(3); }
}

