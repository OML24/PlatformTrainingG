(() => {
  'use strict';
  let id = null;
  try { id = sessionStorage.getItem('zu.tab-session'); } catch { /* No default account fallback. */ }
  if (!id || !/^[0-9a-f]{32}$/.test(id)) { location.replace('/login'); return; }
  const target = new URL(location.href);
  if (target.searchParams.get('zu_session') === id) {
    location.replace('/login?zu_session=' + id + '&returnUrl=' + encodeURIComponent(target.pathname));
    return;
  }
  target.searchParams.set('zu_session', id);
  location.replace(target.pathname + target.search + target.hash);
})();
