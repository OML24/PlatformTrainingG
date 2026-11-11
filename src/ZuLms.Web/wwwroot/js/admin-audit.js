(() => {
  const root = document.getElementById('audit-page'); if (!root) return;
  const $ = name => document.getElementById(`audit-${name}`);
  const request = (method, path) => ZuLms.api.request(method, path);
  let page = 1;
  const error = $('error');

  async function load() {
    error.classList.add('d-none');
    const params = new URLSearchParams({ page: String(page) });
    const search = $('search').value.trim(); if (search) params.set('search', search);
    const category = $('category').value; if (category) params.set('category', category);
    const action = $('action').value.trim(); if (action) params.set('actionCode', action);
    const course = $('course').value.trim(); if (course) params.set('courseId', course);
    const success = $('success').value; if (success) params.set('success', success);
    const from = $('from').value; if (from) params.set('from', from);
    const to = $('to').value; if (to) params.set('to', to);
    $('export').href = `/api/v1/audit/export?${params}`;
    try {
      const data = await request('GET', `/api/v1/audit?${params}`);
      const body = $('rows'); body.replaceChildren();
      for (const row of data.items || []) {
        const tr = document.createElement('tr');
        const cells = [row.id, ZuLmsDates.display(row.at),
          `${row.actorName} (${row.actorLogin})`,
          row.category, row.actionCode,
          `${row.entityType ?? '—'}${row.entityId ? ' #' + row.entityId : ''}`,
          row.summaryAr, row.success ? 'ناجحة' : `فاشلة: ${failureLabel(row.failureReason)}`];
        cells.forEach((value, index) => {
          const td = document.createElement('td');
          if (index === 7) td.className = value === 'ناجحة' ? 'text-success' : 'text-danger';
          td.textContent = value; tr.append(td);
        });
        const details = document.createElement('td');
        if (row.beforeJson || row.afterJson) {
          const button = document.createElement('button');
          button.type = 'button'; button.className = 'btn btn-sm btn-outline-secondary';
          button.textContent = 'قبل/بعد';
          button.addEventListener('click', async () => {
            await ZuLms.ui.show(`قبل:\n${row.beforeJson ?? '—'}\n\nبعد:\n${row.afterJson ?? '—'}`);
          });
          details.append(button);
        }
        tr.append(details);
        body.append(tr);
      }
      const pager = $('pager'); pager.replaceChildren();
      const pages = Math.max(1, Math.ceil((data.total || 0) / (data.pageSize || 50)));
      function addPage(label, target, disabled = false) {
        const li = document.createElement('li');
        li.className = `page-item ${target === page ? 'active' : ''} ${disabled ? 'disabled' : ''}`;
        const link = document.createElement('button');
        link.type = 'button'; link.className = 'page-link'; link.textContent = label;
        link.disabled = disabled; link.setAttribute('aria-label', label);
        if (target === page) link.setAttribute('aria-current', 'page');
        link.addEventListener('click', () => { page = target; load(); });
        li.append(link); pager.append(li);
      }
      addPage('السابق', page - 1, page <= 1);
      for (let i = Math.max(1, page - 2); i <= Math.min(pages, page + 2); i++) {
        addPage(String(i), i);
      }
      addPage('التالي', page + 1, page >= pages);
    } catch (e) {
      error.textContent = e.code || 'تعذّر تحميل السجل';
      error.classList.remove('d-none');
    }
  }

  function failureLabel(code) {
    const labels = {
      AUTH_INVALID: 'بيانات الدخول غير صحيحة', AUTH_FORBIDDEN: 'لا توجد صلاحية',
      FORBIDDEN: 'لا توجد صلاحية', AUTH_LOCKED: 'الحساب مقفل',
      AUTH_INACTIVE: 'الحساب غير نشط', VALIDATION_FAILED: 'البيانات المدخلة غير صالحة'
    };
    return labels[code] || (code ? 'تعذّر إكمال العملية' : 'سبب غير محدد');
  }

  $('filters').addEventListener('submit', event => { event.preventDefault(); page = 1; load(); });
  $('category').addEventListener('change', () => { page = 1; load(); });
  root.querySelectorAll('[data-audit-preset]').forEach(button => button.addEventListener('click', () => {
    const code = button.dataset.auditPreset;
    $('action').value = code;
    $('category').value = code ? 'Attendance' : '';
    if (!code) $('search').value = '';
    root.querySelectorAll('[data-audit-preset]').forEach(other =>
      other.classList.toggle('active', !!code && other === button));
    page = 1; load();
  }));
  load();
})();
