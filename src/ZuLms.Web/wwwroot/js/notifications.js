(() => {
  const bell = document.getElementById('notifications-bell');
  if (!bell || !window.ZuLms || !window.ZuLms.api) return;
  const badge = document.getElementById('notifications-count');
  const list = document.getElementById('notifications-list');
  const widget = document.getElementById('notifications-widget');
  let loaded = false, version = 0, marking = false;
  let lastSeenId = null, visitMarked = false;

  // --- Sidebar badges: show how many unread notifications belong to each menu item. ---
  const COURSE_PAGES = ['/admin/courses', '/quality/courses', '/dean/courses', '/instructor/courses', '/member/courses'];
  const pathOf = url => {
    try { return new URL(url, location.origin).pathname.replace(/\/+$/, '') || '/'; } catch { return null; }
  };
  const navItems = () => [...document.querySelectorAll('#app-sidebar .sidebar-nav a.nav-item[href]')]
    .filter(a => !a.hidden && !a.closest('[hidden]'));

  function navFor(link, items) {
    const path = pathOf(link);
    if (!path) return null;
    let best = null, bestLength = -1;
    for (const item of items) {
      const href = pathOf(item.getAttribute('href'));
      if (!href || href === '/') continue;
      if ((path === href || path.startsWith(href + '/')) && href.length > bestLength) { best = item; bestLength = href.length; }
    }
    if (best) return best;
    if (path.startsWith('/courses/')) {
      let view = null;
      try { view = new URL(link, location.origin).searchParams.get('view'); } catch { /* ignore */ }
      const order = view === 'Member' ? ['/member/courses', ...COURSE_PAGES] : COURSE_PAGES;
      for (const href of order) {
        const match = items.find(item => pathOf(item.getAttribute('href')) === href);
        if (match) return match;
      }
    }
    const last = path.split('/').filter(Boolean).pop();
    return last ? items.find(item => pathOf(item.getAttribute('href'))?.endsWith('/' + last)) || null : null;
  }

  function renderNavBadges(groups) {
    const items = navItems();
    const counts = new Map();
    for (const group of groups || []) {
      const item = navFor(group.linkUrl, items);
      if (item) counts.set(item, (counts.get(item) || 0) + group.count);
    }
    for (const item of document.querySelectorAll('#app-sidebar .sidebar-nav a.nav-item')) {
      const count = counts.get(item) || 0;
      let badge = item.querySelector('.nav-badge');
      item.classList.toggle('has-badge', count > 0 && !item.classList.contains('active'));
      if (!count) { badge?.remove(); continue; }
      if (!badge) {
        badge = document.createElement('span'); badge.className = 'nav-badge';
        item.append(badge);
      }
      badge.textContent = count > 99 ? '99+' : String(count);
      badge.setAttribute('aria-label', `${count} ${widget.dataset.unreadLabel || 'غير مقروء'}`);
    }
    return items;
  }

  // Opening the page a notification points to (or its menu page) marks it read.
  async function markVisited(groups, items) {
    if (visitMarked || !groups?.length) return false;
    visitMarked = true;
    const here = pathOf(location.href);
    const active = items.find(item => item.classList.contains('active')) ||
      items.find(item => pathOf(item.getAttribute('href')) === here);
    const links = groups.map(g => g.linkUrl).filter(link => {
      const path = pathOf(link);
      if (path === here) return true;
      return active && pathOf(active.getAttribute('href')) === here && navFor(link, items) === active;
    });
    if (!links.length) return false;
    try { await ZuLms.api.post('/api/v1/notifications/read-links', { links }); return true; }
    catch { return false; }
  }

  // A notification that arrives while the page is open also pops up as a toast.
  function announce(summary) {
    const rows = summary.latest || [];
    const newest = rows.reduce((max, row) => Math.max(max, row.id || 0), 0);
    if (lastSeenId !== null && window.ZuLms.toast) {
      rows.filter(row => !row.isRead && row.id > lastSeenId).slice(0, 3).reverse().forEach(row => {
        const content = document.createElement(row.linkUrl ? 'a' : 'span');
        if (row.linkUrl) { content.href = row.linkUrl; content.className = 'text-decoration-none'; }
        const title = document.createElement('strong'); title.className = 'd-block';
        title.textContent = ZuLmsI18n.field(row, 'title');
        const body = document.createElement('span'); body.textContent = ZuLmsI18n.field(row, 'body');
        content.append(title, body);
        window.ZuLms.toast(content, 'info', 6000);
      });
    }
    lastSeenId = Math.max(lastSeenId ?? 0, newest);
  }

  function message(text, isError = false) {
    const item = document.createElement('li');
    item.className = `dropdown-item small ${isError ? 'text-danger' : 'text-muted'}`;
    item.setAttribute('role', isError ? 'alert' : 'status');
    item.textContent = text;
    return item;
  }

  function render(summary) {
    if (!summary) return;
    const navList = renderNavBadges(summary.unreadByLink);
    announce(summary);
    markVisited(summary.unreadByLink, navList).then(changed => { if (changed) refresh(); });
    if (summary.unread > 0) {
      badge.textContent = summary.unread > 99 ? '99+' : String(summary.unread);
      badge.classList.remove('d-none');
    } else badge.classList.add('d-none');
    list.replaceChildren();
    if (!summary.latest || !summary.latest.length) {
      const empty = document.createElement('li');
      empty.className = 'dropdown-item text-muted small'; empty.textContent = widget.dataset.empty;
      list.append(empty);
      return;
    }
    for (const row of summary.latest) {
      const item = document.createElement('li');
      item.className = 'dropdown-item small py-1';
      if (!row.isRead) item.classList.add('fw-bold');
      const anchor = row.linkUrl ? document.createElement('a') : document.createElement('span');
      if (row.linkUrl) { anchor.href = row.linkUrl; anchor.className = 'text-decoration-none'; }
      const date = document.createElement('small'); date.className = 'd-block text-muted';
      if (row.createdAt && !Number.isNaN(Date.parse(row.createdAt))) date.textContent = ZuLmsDates.display(row.createdAt);
      anchor.textContent = `${ZuLmsI18n.field(row, 'title')} — ${ZuLmsI18n.field(row, 'body')}`;
      item.append(anchor);
      if (date.textContent) item.append(date);
      list.append(item);
    }
    const markAll = document.createElement('li');
    markAll.className = 'dropdown-item text-center small border-top mt-1';
    const button = document.createElement('button');
    button.type = 'button'; button.className = 'btn btn-sm btn-link';
    button.textContent = widget.dataset.readAll;
    button.addEventListener('click', async () => {
      if (marking) return;
      marking = true;
      const writeVersion = ++version; // Earlier GETs must not restore stale unread state.
      button.disabled = true;
      list.setAttribute('aria-busy', 'true');
      list.querySelector('[role="alert"]')?.remove();
      try {
        await ZuLms.api.post('/api/v1/notifications/read-all');
        marking = false;
        await refresh();
      } catch {
        list.append(message(widget.dataset.readError, true));
      } finally {
        marking = false; button.disabled = false;
        if (version === writeVersion) list.setAttribute('aria-busy', 'false');
      }
    });
    markAll.append(button); list.append(markAll);
  }

  async function refresh() {
    if (marking) return;
    const current = ++version;
    if (!loaded) list.replaceChildren(message(widget.dataset.loading));
    list.setAttribute('aria-busy', 'true');
    try {
      const summary = await ZuLms.api.get('/api/v1/notifications');
      if (current !== version) return;
      render(summary);
      loaded = true;
    } catch {
      if (current !== version) return;
      list.replaceChildren(message(widget.dataset.error, true));
    } finally { if (current === version) list.setAttribute('aria-busy', 'false'); }
  }

  bell.addEventListener('shown.bs.dropdown', refresh);
  refresh();
  setInterval(refresh, 30000);
})();
