(() => {
  'use strict';
  const key = 'zu.tab-session';
  const valid = value => typeof value === 'string' && /^[0-9a-f]{32}$/.test(value);
  let stored = null;
  try { stored = sessionStorage.getItem(key); } catch { /* The URL remains a safe fallback. */ }
  const supplied = new URL(location.href).searchParams.get('zu_session');
  const mismatchedNavigation = valid(stored) && valid(supplied) && stored !== supplied;
  let current = valid(stored) ? stored : valid(supplied) ? supplied :
    crypto.randomUUID().replaceAll('-', '');

  function save() { try { sessionStorage.setItem(key, current); } catch { /* URL still persists across refresh. */ } }
  function url(value) {
    const target = new URL(value, location.href);
    if (target.origin !== location.origin || !['http:', 'https:'].includes(target.protocol)) return value;
    target.searchParams.set('zu_session', current);
    return target.pathname + target.search + target.hash;
  }
  function activate(id) {
    if (!valid(id)) throw new Error('SESSION_SELECTOR_INVALID');
    current = id; save();
  }
  function scopedFetch(value, options = {}) {
    if (mismatchedNavigation) return Promise.reject(new Error('SESSION_NAVIGATION_MISMATCH'));
    const target = new URL(value, location.href);
    if (target.origin !== location.origin) return Promise.reject(new Error('CROSS_ORIGIN_SESSION_REQUEST'));
    const headers = new Headers(options.headers);
    headers.set('X-ZU-Session', current);
    return fetch(value, { ...options, headers, credentials: 'same-origin', cache: 'no-store' });
  }
  function decorate(anchor) {
    const href = anchor.getAttribute('href');
    if (!href || href.startsWith('#')) return;
    let target;
    try { target = new URL(href, location.href); } catch { return; }
    if (target.origin !== location.origin || !['http:', 'https:'].includes(target.protocol) || target.pathname === '/verify') return;
    const next = url(href);
    if (next !== href) anchor.setAttribute('href', next);
  }
  function scan(root) {
    if (root.nodeType !== Node.ELEMENT_NODE) return;
    if (root.matches('a[href]')) decorate(root);
    root.querySelectorAll('a[href]').forEach(decorate);
  }
  save();
  window.ZuLmsSession = { id: () => current, url, activate, fetch: scopedFetch,
    cookieName: name => name + '_' + current };
  // A back-history entry or pasted URL must not silently switch this tab after
  // an explicit new login. Hide before body parsing and reload its pinned session.
  if (mismatchedNavigation) {
    document.documentElement.style.visibility = 'hidden';
    location.replace(url(location.href));
    return;
  }
  scan(document.documentElement);
  new MutationObserver(records => records.forEach(record => {
    if (record.type === 'attributes') { if (record.target.matches('a[href]')) decorate(record.target); }
    else record.addedNodes.forEach(scan);
  })).observe(document.documentElement, { subtree: true, childList: true, attributes: true, attributeFilter: ['href'] });
  document.addEventListener('click', event => { const anchor = event.target.closest?.('a[href]'); if (anchor) decorate(anchor); }, true);
  window.addEventListener('pageshow', event => {
    if (event.persisted) { document.body.style.visibility = 'hidden'; location.reload(); }
  });
})();
