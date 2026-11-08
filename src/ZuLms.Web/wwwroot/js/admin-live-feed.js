(() => {
  const root = document.getElementById('live-feed-page'); if (!root) return;
  const list = document.getElementById('live-list');
  const toggle = document.getElementById('live-toggle');
  const category = document.getElementById('live-category');
  let lastId = 0, paused = false;

  async function poll() {
    if (paused) return;
    try {
      const rows = await ZuLms.api.request('GET',
        `/api/v1/audit/live?afterId=${lastId}`);
      for (const row of rows || []) {
        lastId = Math.max(lastId, row.id);
        if (category.value && row.category !== category.value) continue;
        const item = document.createElement('li');
        item.className = 'list-group-item d-flex justify-content-between';
        const text = document.createElement('span');
        text.textContent = `${row.summaryAr} — ${row.actorName}`;
        const time = document.createElement('small');
        time.className = 'text-muted';
        time.textContent = new Date(row.at).toLocaleTimeString('ar');
        item.append(text, time);
        list.prepend(item);
      }
      while (list.children.length > 200) list.lastElementChild.remove();
    } catch { /* ignore transient errors */ }
  }

  toggle.addEventListener('click', () => {
    paused = !paused;
    toggle.textContent = paused ? 'استئناف' : 'إيقاف مؤقت';
  });
  setInterval(poll, 5000);
  poll();
})();
