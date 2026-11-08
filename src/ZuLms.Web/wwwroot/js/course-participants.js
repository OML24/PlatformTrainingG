(() => {
  const root = document.getElementById('participants-page'); if (!root) return;
  const id = Number(root.dataset.courseId);
  const $ = key => document.getElementById(`participants-${key}`);
  const request = (path, method = 'GET', body) => ZuLms.api.request(method, path, body);
  const selected = new Map();
  let searchTimer;
  function message(kind, value) {
    $('error').classList.add('d-none'); $('success').classList.add('d-none');
    const box = $(kind); box.textContent = value; box.classList.remove('d-none');
  }
  function showError(e) { message('error', e.code || e.message || 'تعذّر إكمال الطلب'); }
  function selectedCount() { $('selected-count').textContent = selected.size ? `تم اختيار ${selected.size} موظف` : 'لم يُحدد أحد'; }
  async function search() {
    const rows = await request(`/api/v1/participants/employees?search=${encodeURIComponent($('search').value)}`);
    const container = $('results'); container.replaceChildren();
    for (const row of rows) {
      const label = document.createElement('label'); label.className = 'list-group-item d-flex align-items-center gap-2';
      const input = document.createElement('input'); input.type = 'checkbox'; input.className = 'form-check-input';
      input.checked = selected.has(row.userId);
      input.addEventListener('change', () => {
        if (input.checked) selected.set(row.userId, row); else selected.delete(row.userId);
        selectedCount();
      });
      const status = row.empStatus == null ? '' :
        ` — ${row.empStatusDesc ? `${row.empStatusDesc} (${row.empStatus})` : `رمز ${row.empStatus}`}`;
      const text = document.createElement('span'); text.textContent = `${ZuLmsI18n.field(row, 'name')} (${row.loginName})${status}`;
      label.append(input, text); container.append(label);
    }
  }
  async function load() {
    const data = await request(`/api/v1/courses/${id}/participants`);
    $('course').textContent = `${data.courseNo} — ${ZuLmsI18n.field(data, 'title')} | المشاركون: ${data.participants.length}${data.maxParticipants ? ` / ${data.maxParticipants}` : ''}`;
    const body = $('rows'); body.replaceChildren();
    for (const row of data.participants) {
      const tr = document.createElement('tr');
      const status = row.empStatus == null ? '' :
        ` — ${row.empStatusDesc ? `${row.empStatusDesc} (${row.empStatus})` : `رمز ${row.empStatus}`}`;
      for (const value of [ZuLmsI18n.field(row, 'name') + status, row.loginName, row.source,
        ZuLmsDates.display(row.joinedAt),
        row.hasIssuedCertificate ? 'صادرة' : '—']) {
        const td = document.createElement('td'); td.textContent = value; tr.append(td);
      }
      const cell = document.createElement('td');
      const button = document.createElement('button'); button.type = 'button';
      button.className = 'btn btn-sm btn-outline-danger'; button.textContent = 'إخراج';
      button.addEventListener('click', () => remove(row).catch(showError));
      cell.append(button); tr.append(cell); body.append(tr);
    }
  }
  async function add() {
    if (!selected.size) { message('error', 'اختر موظفاً واحداً على الأقل'); return; }
    $('add').disabled = true;
    try {
      const path = `/api/v1/courses/${id}/participants`;
      const payload = { userIds: [...selected.keys()], confirmCapacityOverride: false };
      try { await request(path, 'POST', payload); }
      catch (e) {
        if (e.code !== 'CAPACITY_CONFIRM_REQUIRED' ||
            !await ZuLms.ui.confirm('سيتجاوز عدد المشاركين سعة الدورة. هل تريد المتابعة؟')) throw e;
        payload.confirmCapacityOverride = true;
        await request(path, 'POST', payload);
      }
      selected.clear(); selectedCount();
      message('success', 'أُضيف المشاركون');
      await Promise.all([load(), search()]);
    } catch (e) { showError(e); }
    finally { $('add').disabled = false; }
  }
  async function remove(row) {
    const reason = await ZuLms.ui.prompt(`سبب إخراج ${ZuLmsI18n.field(row, 'name')}:`);
    if (reason === null) return;
    if (!reason.trim()) { message('error', 'سبب الإخراج إلزامي'); return; }
    const path = `/api/v1/courses/${id}/participants/${row.userId}`;
    const payload = { reason: reason.trim(), confirmCertificateRevocation: false };
    try {
      try { await request(path, 'DELETE', payload); }
      catch (e) {
        if (e.code !== 'CERTIFICATE_REVOCATION_CONFIRM_REQUIRED' ||
            !await ZuLms.ui.confirm('للمشارك شهادة صادرة. إخراجه سيلغي الشهادة. هل تريد المتابعة؟')) throw e;
        payload.confirmCertificateRevocation = true;
        await request(path, 'DELETE', payload);
      }
      message('success', 'أُخرج المشارك'); await load();
    } catch (e) { showError(e); }
  }
  $('search').addEventListener('input', () => {
    clearTimeout(searchTimer); searchTimer = setTimeout(() => search().catch(showError), 250);
  });
  $('add').addEventListener('click', add);
  Promise.all([load(), search()]).catch(showError);
})();
