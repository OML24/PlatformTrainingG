(() => {
  const root = document.getElementById('admin-dashboard-activity');
  if (!root) return;
  const eventsNode = document.getElementById('admin-recent-events');
  const syncNode = document.getElementById('admin-last-sync');
  const error = document.getElementById('admin-activity-error');
  const date = value => value ? ZuLmsDates.display(value) : '—';
  const english = document.documentElement.lang === 'en';
  const syncStatuses = { Success: ['ناجحة', 'Success'], Failed: ['فشلت', 'Failed'],
    Running: ['قيد التشغيل', 'Running'], Partial: ['جزئية', 'Partial'],
    Pending: ['معلّقة', 'Pending'] };
  Promise.all([ZuLms.api.get('/api/v1/audit?page=1'), ZuLms.api.get('/api/v1/sync/runs')])
    .then(([audit, runs]) => {
      eventsNode.replaceChildren();
      audit.items.slice(0, 10).forEach(item => {
        const row = document.createElement('li');
        row.className = 'list-group-item d-flex justify-content-between gap-3';
        const summary = document.createElement('span'); summary.textContent = item.summaryAr;
        const meta = document.createElement('small'); meta.className = 'text-muted text-nowrap';
        meta.textContent = `${item.actorName} · ${date(item.at)}`;
        row.append(summary, meta); eventsNode.append(row);
      });
      if (!audit.items.length) { const row = document.createElement('li'); row.className = 'list-group-item text-muted'; row.textContent = 'لا توجد أحداث.'; eventsNode.append(row); }
      const latest = runs[0];
      if (latest) {
        syncNode.replaceChildren();
        const status = document.createElement('strong'); status.className = 'd-block mb-2';
        status.textContent = (syncStatuses[latest.status] || [latest.status, latest.status])[english ? 1 : 0];
        const started = document.createElement('div'); started.textContent = `البدء: ${date(latest.startedAt)}`;
        const finished = document.createElement('div'); finished.textContent = `الانتهاء: ${date(latest.finishedAt)}`;
        syncNode.append(status, started, finished);
      }
    }).catch(err => { error.textContent = err.message || 'تعذر تحميل نشاط الإدارة.'; error.classList.remove('d-none'); });
})();
