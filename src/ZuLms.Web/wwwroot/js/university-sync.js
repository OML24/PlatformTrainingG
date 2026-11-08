(() => {
  const root = document.getElementById('sync-page');
  if (!root) return;
  const error = document.getElementById('sync-error');
  const current = document.getElementById('sync-current');
  const progress = document.getElementById('sync-progress');
  const start = document.getElementById('sync-start');
  let page = 1, totalPages = 1, activeRun = null, timer = null;
  const date = value => value ? ZuLmsDates.display(value) : '—';
  const labels = {
    SKIPPED_INVALID_ROWS: 'تم تجاوز صفوف غير صالحة',
    MissingFromSource: 'غير موجود في المصدر', UnresolvedReference: 'مرجع غير معروف',
    DuplicateLoginName: 'رقم دخول مكرر', DuplicateSourceKey: 'معرّف مصدر مكرر',
    SharedEmail: 'بريد إلكتروني مشترك', StatusChanged: 'تغيّرت الحالة',
    Success: 'اكتملت', Partial: 'اكتملت جزئياً', Failed: 'فشلت',
    Employees: 'الموظفون', Students: 'الطلبة', Centers: 'الجهات',
    Departments: 'الأقسام', Emails: 'البريد الإلكتروني',
    accountsCreated: 'حسابات أُنشئت', reviewItemsCreated: 'عناصر تحتاج مراجعة'
  };
  function readable(value) {
    if (value == null || value === '') return '—';
    const raw = String(value);
    const skipped = /^SKIPPED_INVALID_ROWS:(\d+)$/.exec(raw);
    return skipped ? `${labels.SKIPPED_INVALID_ROWS}: ${skipped[1]}` : labels[raw] || raw;
  }
  function details(value, summary = 'عرض التفاصيل') {
    const node = document.createElement('details');
    const heading = document.createElement('summary'); heading.textContent = summary;
    const content = document.createElement('pre'); content.className = 'small mt-2 mb-0 text-wrap';
    content.textContent = typeof value === 'string' ? value : JSON.stringify(value, null, 2);
    node.append(heading, content); return node;
  }
  function detailSummary(value) {
    if (!value || value === '—') return 'لا توجد تفاصيل';
    if (typeof value !== 'string') return 'عرض التفاصيل';
    if (/^[A-Z_]+:\d+$/.test(value)) return readable(value);
    try { JSON.parse(value); return 'عرض التفاصيل'; } catch { return value.length < 80 ? readable(value) : 'عرض التفاصيل'; }
  }
  function cell(row, value) { const node = document.createElement('td'); node.textContent = value ?? '—'; row.append(node); return node; }
  function fail(e) { error.textContent = e.message || 'تعذّر إكمال الطلب'; error.classList.remove('d-none'); }
  async function csrf() { return ZuLms.api.csrf(); }
  function request(path, method = 'GET', body) { return ZuLms.api.request(method, path, body); }
  async function sides() {
    const data = await request('/api/v1/sync/sides');
    const body = document.getElementById('sync-sides'); body.replaceChildren();
    for (const side of data) {
      const row = document.createElement('tr');
      cell(row, side.sideCode); cell(row, ZuLmsI18n.field(side, 'name')); cell(row, date(side.lastSeenAt));
      const slot = document.createElement('td'); const input = document.createElement('input');
      input.type = 'checkbox'; input.className = 'form-check-input'; input.checked = side.isImported;
      input.disabled = root.dataset.canRun !== 'true';
      input.setAttribute('aria-label', `استيراد ${ZuLmsI18n.field(side, 'name')}`);
      input.addEventListener('change', async () => {
        input.disabled = true;
        try { await request(`/api/v1/sync/sides/${side.sideCode}/imported`, 'PUT', { imported: input.checked }); }
        catch (e) { input.checked = !input.checked; fail(e); }
        finally { input.disabled = false; }
      });
      slot.append(input); row.append(slot); body.append(row);
    }
  }
  async function runs() {
    const data = await request('/api/v1/sync/runs');
    const body = document.getElementById('sync-runs'); body.replaceChildren();
    for (const run of data) {
      const row = document.createElement('tr');
      cell(row, run.id); cell(row, date(run.startedAt)); cell(row, date(run.finishedAt));
      cell(row, run.triggeredBy);
      cell(row, run.finishedAt ? readable(run.status) : 'جارٍ');
      const slot = document.createElement('td');
      try {
        const stats = JSON.parse(run.statsJson || '{}');
        const summary = document.createElement('div');
        summary.textContent = [
          stats.accountsCreated != null ? `حسابات أُنشئت: ${stats.accountsCreated}` : null,
          stats.reviewItemsCreated != null ? `عناصر تحتاج مراجعة: ${stats.reviewItemsCreated}` : null
        ].filter(Boolean).join('، ') || 'إحصاءات المزامنة';
        slot.append(summary);
        for (const [source, values] of Object.entries(stats.sources || {})) {
          const line = document.createElement('div');
          line.textContent = `${readable(source)}: ${values.fetched ?? values.Fetched ?? 0} صف`;
          slot.append(line);
          for (const warning of values.errors || values.Errors || []) {
            const note = document.createElement('div');
            note.className = 'text-warning-emphasis';
            note.textContent = `${readable(warning.key)}: ${readable(warning.message)}`;
            slot.append(note);
          }
        }
        slot.append(details(stats));
      } catch { slot.append(details(run.statsJson || '—')); }
      row.append(slot); body.append(row);
    }
    const ongoing = data.find(x => !x.finishedAt);
    if (ongoing && !activeRun) { activeRun = ongoing.id; poll(); }
  }
  async function reviews() {
    const reason = document.getElementById('sync-reason').value;
    const data = await request(`/api/v1/sync/reviews?page=${page}${reason ? `&reason=${encodeURIComponent(reason)}` : ''}`);
    totalPages = Math.max(1, Math.ceil(data.total / data.pageSize));
    document.getElementById('sync-page-number').textContent = `${page} / ${totalPages}`;
    document.getElementById('sync-prev').disabled = page <= 1;
    document.getElementById('sync-next').disabled = page >= totalPages;
    const body = document.getElementById('sync-reviews'); body.replaceChildren();
    for (const review of data.items) {
      const row = document.createElement('tr');
      cell(row, readable(review.reason)); cell(row, `${readable(review.entityType)} ${review.entityKey}`);
      const detailCell = document.createElement('td');
      const rawDetails = review.details || '—';
      detailCell.append(details(rawDetails, detailSummary(rawDetails)));
      row.append(detailCell);
      const slot = document.createElement('td');
      const note = document.createElement('input'); note.className = 'form-control form-control-sm mb-1';
      note.placeholder = 'ملاحظة / سبب التعطيل'; note.setAttribute('aria-label', 'ملاحظة المعالجة');
      slot.append(note);
      const actions = root.dataset.canResolve === 'true' ?
        [['Keep', 'إبقاء'], ['Disable', 'تعطيل'], ['Ignore', 'تجاهل']]
          .filter(([value]) => value !== 'Disable' || review.entityType === 'Employee') : [];
      const menu = actions.length >= 3 ? document.createElement('div') : slot;
      if (menu !== slot) {
        menu.className = 'dropdown table-action-dropdown';
        const trigger = document.createElement('button');
        trigger.type = 'button'; trigger.className = 'btn btn-sm btn-outline-primary dropdown-toggle';
        trigger.textContent = 'الإجراءات'; trigger.dataset.bsToggle = 'dropdown';
        trigger.setAttribute('aria-expanded', 'false');
        const items = document.createElement('div'); items.className = 'dropdown-menu table-action-menu';
        menu.append(trigger, items); slot.append(menu);
      }
      const destination = menu === slot ? slot : menu.lastElementChild;
      for (const [value, label] of actions) {
        if (value === 'Disable' && review.entityType !== 'Employee') continue;
        const button = document.createElement('button'); button.type = 'button';
        button.className = 'btn btn-sm btn-outline-primary me-1 mb-1'; button.textContent = label;
        button.addEventListener('click', async () => {
          if (value === 'Disable' && !note.value.trim()) { fail(new Error('سبب التعطيل إلزامي')); return; }
          const confirmation = `${root.dataset.confirmResolution} ${label}: ${review.entityType} ${review.entityKey}`;
          if (!await ZuLms.ui.confirm(confirmation)) return;
          button.disabled = true;
          try { await request(`/api/v1/sync/reviews/${review.id}/resolve`, 'POST',
            { resolution: value, notes: note.value }); await reviews(); }
          catch (e) { fail(e); button.disabled = false; }
        });
        destination.append(button);
      }
      row.append(slot); body.append(row);
    }
  }
  async function poll() {
    if (!activeRun) return;
    try {
      const run = await request(`/api/v1/sync/runs/${activeRun}`);
      current.textContent = `#${run.id} — ${run.finishedAt ? readable(run.status) : 'جارٍ'}`;
      progress.classList.toggle('d-none', !!run.finishedAt);
      if (start) start.disabled = !run.finishedAt;
      if (run.finishedAt) {
        activeRun = null; clearTimeout(timer);
        await Promise.all([runs(), sides(), reviews()]);
      } else timer = setTimeout(poll, 2000);
    } catch (e) { fail(e); timer = setTimeout(poll, 2000); }
  }
  start?.addEventListener('click', async () => {
    start.disabled = true; error.classList.add('d-none');
    try { const run = await request('/api/v1/sync/runs', 'POST'); activeRun = run.id; poll(); await runs(); }
    catch (e) { start.disabled = false; fail(e); }
  });
  document.getElementById('sync-reason').addEventListener('change', () => { page = 1; reviews().catch(fail); });
  document.getElementById('sync-prev').addEventListener('click', () => { page--; reviews().catch(fail); });
  document.getElementById('sync-next').addEventListener('click', () => { page++; reviews().catch(fail); });
  Promise.all([sides(), runs(), reviews()]).catch(fail);
})();
