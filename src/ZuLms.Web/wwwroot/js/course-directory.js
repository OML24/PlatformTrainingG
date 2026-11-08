(() => {
  const root = document.getElementById('course-directory-page'); if (!root) return;
  const view = root.dataset.view;
  const $ = name => document.getElementById(`directory-${name}`);
  const value = name => $(name).value;
  const english = document.documentElement.lang === 'en';
  let page = 1, pages = 1;
  const statusStyles = {
    Proposed: [['مقترحة — بانتظار الجودة', 'Proposed — awaiting Quality'], 'pending'],
    Returned: [['معادة للتعديل', 'Returned for revision'], 'returned'],
    Rejected: [['مرفوضة', 'Rejected'], 'rejected'], Approved: [['معتمدة', 'Approved'], 'approved'],
    Finished: [['منتهية', 'Finished'], 'finished'], Cancelled: [['ملغية بعد الاعتماد', 'Cancelled after approval'], 'cancelled']
  };
  function state(message, role = 'status') {
    const tr = document.createElement('tr'), td = document.createElement('td');
    td.colSpan = 9; td.textContent = message; td.setAttribute('role', role);
    tr.append(td); $('rows').replaceChildren(tr);
  }
  function option(select, id, label) {
    const node = document.createElement('option'); node.value = id;
    node.textContent = label; select.append(node);
  }
  function params() {
    const query = new URLSearchParams({ view, page: String(page) });
    for (const [field, key] of [['search', 'search'], ['status', 'status'],
      ['center', 'centerId'], ['track', 'trackId'], ['mode', 'deliveryMode'],
      ['from', 'fromDate'], ['to', 'toDate'], ['active', 'isActive'],
      ['archived', 'isArchived']]) {
      if (value(field)) query.set(key, value(field));
    }
    return query;
  }
  function link(href, label, className = 'btn btn-sm btn-outline-primary') {
    const node = document.createElement('a'); node.href = href;
    node.className = className; node.textContent = label; return node;
  }
  async function load() {
    $('error').classList.add('d-none');
    $('rows').closest('table').setAttribute('aria-busy', 'true');
    state(ZuLmsI18n.t('loading'));
    try {
      const data = await ZuLms.api.request('GET', `/api/v1/courses/directory?${params()}`);
      pages = Math.max(1, Math.ceil(data.total / data.pageSize));
      $('page-number').textContent = `${page} / ${pages}`;
      $('total').textContent = `${english ? 'Total' : 'المجموع'}: ${data.total}`;
      $('prev').disabled = page <= 1; $('next').disabled = page >= pages;
      const body = $('rows'); body.replaceChildren();
      for (const row of data.items) {
        const tr = document.createElement('tr');
        if (!row.isActive) tr.className = 'table-secondary';
        for (const [index, text] of [row.courseNo, ZuLmsI18n.field(row, 'title'), row.status,
          (english ? row.centerNameEn : row.centerNameAr) || row.centerNameAr || '—',
          (english ? row.trackNameEn : row.trackNameAr) || row.trackNameAr,
          (english ? row.instructorNameEn : row.instructorNameAr) || row.instructorNameAr,
          row.plannedStartDate, String(row.participantsCount)].entries()) {
          const td = document.createElement('td');
          if (index === 2) {
            const badge = document.createElement('span');
            const [label, style] = statusStyles[text] || [[text, text], 'rejected'];
            badge.className = `zu-status zu-status-${style}`; badge.textContent = ZuLmsI18n.localize(...label);
            td.append(badge);
          } else if (index === 0) {
            td.className = 'directory-number';
            const number = document.createElement('span'); number.textContent = text || '—';
            const id = document.createElement('small'); id.textContent = `#${row.id}`;
            td.append(number, id);
          } else {
            td.textContent = text || '—';
            if (index === 1) td.className = 'directory-title';
            if ([3, 4, 5].includes(index)) td.className = 'directory-muted';
            if (index === 6) td.className = 'directory-date';
          }
          if ([0, 6, 7].includes(index)) td.dir = 'ltr';
          tr.append(td);
        }
        const actions = document.createElement('td'); actions.className = 'directory-actions';
        const actionList = document.createElement('div'); actionList.className = 'd-flex gap-1';
        actionList.append(link(`/courses/${row.id}?view=${view}`, english ? 'Details' : 'التفاصيل'));
        if (view === 'All') actionList.append(link(`/courses/${row.id}/participants`, english ? 'Participants' : 'المشاركون'));
        if (view === 'Dean' && row.status === 'Approved')
          actionList.append(link(`/courses/${row.id}/cancel-request`, english ? 'Cancel request' : 'طلب إلغاء', 'btn btn-sm btn-outline-danger'));
        actions.append(actionList);
        tr.append(actions); body.append(tr);
      }
      if (!data.items.length) state(ZuLmsI18n.t('empty'));
    } catch (e) {
      $('error').textContent = e.code === 'FORBIDDEN'
        ? 'لا تملك صلاحية عرض هذه الدورات.' : e.message || 'تعذّر تحميل الدورات';
      $('error').classList.remove('d-none');
      state(ZuLmsI18n.t('error'), 'alert');
    } finally {
      $('rows').closest('table').setAttribute('aria-busy', 'false');
    }
  }
  async function lookups() {
    const data = await ZuLms.api.request('GET', `/api/v1/courses/directory/lookups?view=${view}`);
    for (const center of data.faculties) option($('center'), center.id, ZuLmsI18n.field(center, 'name'));
    for (const track of data.tracks) option($('track'), track.id, ZuLmsI18n.field(track, 'name'));
  }
  $('filter').addEventListener('submit', event => { event.preventDefault(); page = 1; load(); });
  $('prev').addEventListener('click', () => { if (page > 1) { page--; load(); } });
  $('next').addEventListener('click', () => { if (page < pages) { page++; load(); } });
  lookups().then(load).catch(e => {
    $('error').textContent = e.code === 'FORBIDDEN'
      ? ZuLmsI18n.t('forbidden') : e.message || 'تعذّر تحميل المرشحات';
    $('error').classList.remove('d-none');
    state(e.code === 'FORBIDDEN' ? ZuLmsI18n.t('forbidden') : ZuLmsI18n.t('error'), 'alert');
    $('rows').closest('table').setAttribute('aria-busy', 'false');
  });
})();
