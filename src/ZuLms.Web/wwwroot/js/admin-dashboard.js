(() => {
  const root = document.getElementById('admin-dashboard-activity');
  if (!root) return;
  const eventsNode = document.getElementById('admin-recent-events');
  const syncNode = document.getElementById('admin-last-sync');
  const error = document.getElementById('admin-activity-error');
  const date = value => value ? ZuLmsDates.display(value) : '—';
  const english = document.documentElement.lang === 'en';
  const syncStatuses = { Success: ['ناجحة', 'Success', 'ok', 'ti-circle-check'], Failed: ['فشلت', 'Failed', 'bad', 'ti-alert-circle'],
    Running: ['قيد التشغيل', 'Running', 'run', 'ti-loader-2'], Partial: ['جزئية', 'Partial', 'warn', 'ti-alert-triangle'],
    Pending: ['معلّقة', 'Pending', 'warn', 'ti-clock'] };
  const el = (tag, cls, text) => { const n = document.createElement(tag); if (cls) n.className = cls; if (text !== undefined) n.textContent = text; return n; };
  const icon = name => { const i = el('i', `ti ${name}`); i.setAttribute('aria-hidden', 'true'); return i; };
  // Pick a glyph from the Arabic summary so the timeline is scannable.
  const glyph = text => {
    const t = text || '';
    if (/حذف|إلغاء|رفض/.test(t)) return 'ti-trash';
    if (/دخول|خروج|كلمة المرور/.test(t)) return 'ti-login';
    if (/شهادة/.test(t)) return 'ti-certificate';
    if (/دورة|محاضرة/.test(t)) return 'ti-book-2';
    if (/صلاحي|دور/.test(t)) return 'ti-shield-cog';
    if (/مزامنة/.test(t)) return 'ti-refresh';
    if (/إعداد/.test(t)) return 'ti-settings';
    return 'ti-point';
  };
  Promise.all([ZuLms.api.get('/api/v1/audit?page=1'), ZuLms.api.get('/api/v1/sync/runs')])
    .then(([audit, runs]) => {
      eventsNode.replaceChildren();
      audit.items.slice(0, 10).forEach(item => {
        const row = el('li');
        const dot = el('span', 'za-dot'); dot.append(icon(glyph(item.summaryAr)));
        const body = el('div');
        const meta = el('div', 'za-ev-meta');
        const who = el('span'); who.append(icon('ti-user'), document.createTextNode(item.actorName || '—'));
        const when = el('span'); when.append(icon('ti-clock'), document.createTextNode(date(item.at)));
        meta.append(who, when);
        body.append(el('div', 'za-ev-text', item.summaryAr), meta);
        row.append(dot, body); eventsNode.append(row);
      });
      if (!audit.items.length) {
        const row = el('li', 'empty-state');
        row.style.display = 'block';
        row.append(icon('ti-activity'), el('p', '', 'لا توجد أحداث.'));
        eventsNode.append(row);
      }
      const latest = runs[0];
      if (latest) {
        syncNode.replaceChildren();
        const s = syncStatuses[latest.status] || [latest.status, latest.status, '', 'ti-info-circle'];
        const badge = el('span', `za-sync-state ${s[2]}`); badge.append(icon(s[3]), document.createTextNode(s[english ? 1 : 0]));
        const rows = el('div', 'za-sync-rows');
        const line = (label, value) => { const d = el('div'); d.append(el('span', '', label), el('strong', '', value)); return d; };
        rows.append(line(english ? 'Started' : 'البدء', date(latest.startedAt)), line(english ? 'Finished' : 'الانتهاء', date(latest.finishedAt)));
        syncNode.append(badge, rows);
      }
    }).catch(err => { error.textContent = err.message || 'تعذر تحميل نشاط الإدارة.'; error.classList.remove('d-none'); });
})();
