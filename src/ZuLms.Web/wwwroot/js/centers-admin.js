(() => {
  const root = document.getElementById('centers-page'); if (!root) return;
  const rows = document.getElementById('centers-rows');
  const error = document.getElementById('centers-error');
  const search = document.getElementById('centers-search');
  let page = 1, totalPages = 1, debounce;
  function fail(e) { error.textContent = e.message || 'تعذّر إكمال الطلب'; error.classList.remove('d-none'); }
  async function csrf() { return ZuLms.api.csrf(); }
  function request(path, method = 'GET', body) { return ZuLms.api.request(method, path, body); }
  function cell(row, content) { const td = document.createElement('td');
    if (content instanceof Node) td.append(content); else td.textContent = content ?? '—';
    row.append(td); return td; }
  function checkbox(checked, label) { const input = document.createElement('input');
    input.type = 'checkbox'; input.className = 'form-check-input'; input.checked = checked;
    input.setAttribute('aria-label', label); return input; }
  async function load() {
    const data = await request(`/api/v1/centers?page=${page}&search=${encodeURIComponent(search.value.trim())}`);
    totalPages = Math.max(1, Math.ceil(data.total / data.pageSize));
    document.getElementById('centers-page-number').textContent = `${page} / ${totalPages}`;
    document.getElementById('centers-prev').disabled = page <= 1;
    document.getElementById('centers-next').disabled = page >= totalPages;
    rows.replaceChildren();
    for (const center of data.items) {
      const row = document.createElement('tr');
      cell(row, center.sideCode); cell(row, center.centerCode); cell(row, ZuLmsI18n.field(center, 'name'));
      cell(row, center.suggestedHeadName);
      const faculty = checkbox(center.isFaculty, `كلية ${ZuLmsI18n.field(center, 'name')}`); cell(row, faculty);
      const code = document.createElement('input'); code.className = 'form-control form-control-sm';
      code.value = center.shortCode || ''; code.maxLength = 10; code.pattern = '[A-Za-z]{2,10}';
      code.setAttribute('aria-label', `الرمز المختصر ${ZuLmsI18n.field(center, 'name')}`); cell(row, code);
      const active = checkbox(center.isActive, `تفعيل ${ZuLmsI18n.field(center, 'name')}`); cell(row, active);
      if (root.dataset.canEdit !== 'true') { faculty.disabled = true; code.disabled = true; active.disabled = true; }
      if (root.dataset.canEdit !== 'true') { rows.append(row); continue; }
      const save = document.createElement('button'); save.type = 'button'; save.className = 'btn btn-sm btn-primary'; save.textContent = 'حفظ';
      save.addEventListener('click', async () => {
        save.disabled = true;
        try { await request(`/api/v1/centers/${center.id}`, 'PUT', { expectedVersion: center.version,
          isFaculty: faculty.checked, shortCode: code.value, isActive: active.checked }); await load(); }
        catch (e) { fail(e); save.disabled = false; }
      });
      cell(row, save); rows.append(row);
    }
  }
  search.addEventListener('input', () => { clearTimeout(debounce); debounce = setTimeout(() => { page = 1; load().catch(fail); }, 300); });
  document.getElementById('centers-prev').addEventListener('click', () => { page--; load().catch(fail); });
  document.getElementById('centers-next').addEventListener('click', () => { page++; load().catch(fail); });
  load().catch(fail);
})();
