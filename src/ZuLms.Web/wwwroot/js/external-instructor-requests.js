(() => {
  const root = document.getElementById('admin-users-page'); if (!root) return;
  const $ = id => document.getElementById(id);
  const error = $('external-requests-error'), success = $('external-requests-success');
  let page = 1, pages = 1, selected = null;
  function showError(e) { error.textContent = e.message || 'تعذّر إكمال الطلب'; error.classList.remove('d-none'); }
  async function csrf() { return ZuLms.api.csrf(); }
  function request(path, method = 'GET', body) { return ZuLms.api.request(method, path, body); }
  async function list() {
    const data = await request(`/api/v1/external-instructor-requests?page=${page}`);
    pages = Math.max(1, Math.ceil(data.total / data.pageSize));
    $('external-requests-page-number').textContent = `${page} / ${pages}`;
    $('external-requests-prev').disabled = page <= 1;
    $('external-requests-next').disabled = page >= pages;
    const rows = $('external-requests-rows'); rows.replaceChildren();
    for (const item of data.items) {
      const row = document.createElement('tr');
      for (const text of [item.courseNo, item.fullNameAr, item.organizationAr, item.email]) {
        const td = document.createElement('td'); td.textContent = text; row.append(td);
      }
      const td = document.createElement('td');
      if (root.dataset.canCreate !== 'true') { row.append(td); rows.append(row); continue; }
      const button = document.createElement('button'); button.type = 'button';
      button.className = 'btn btn-sm btn-primary'; button.textContent = 'إنشاء الحساب';
      button.addEventListener('click', () => {
        selected = item; $('external-request-name').textContent = item.fullNameAr;
        $('external-request-form').reset(); $('external-request-dialog').showModal();
      });
      td.append(button); row.append(td); rows.append(row);
    }
    $('external-requests-empty').classList.toggle('d-none', data.items.length > 0);
  }
  $('external-request-suggest').addEventListener('click', async () => {
    try { $('external-request-login').value = (await request('/api/v1/external-instructor-requests/suggest-login')).loginName; }
    catch (e) { showError(e); }
  });
  $('external-request-form').addEventListener('submit', async e => {
    e.preventDefault(); if (!selected) return;
    error.classList.add('d-none'); success.classList.add('d-none');
    const button = $('external-request-submit'); button.disabled = true;
    try {
      const result = await request(`/api/v1/external-instructor-requests/${selected.id}/complete`,
        'POST', { loginName: $('external-request-login').value,
          password: $('external-request-password').value || null });
      $('external-request-dialog').close(); selected = null;
      success.textContent = `أُنشئ الحساب برقم دخول ${result.loginName} ووُضعت رسالة البيانات في طابور البريد.`;
      success.classList.remove('d-none'); await list();
    } catch (e2) { showError(e2); }
    finally { button.disabled = false; }
  });
  $('external-request-cancel').addEventListener('click', () => $('external-request-dialog').close());
  $('external-requests-prev').addEventListener('click', () => { page--; list().catch(showError); });
  $('external-requests-next').addEventListener('click', () => { page++; list().catch(showError); });
  list().catch(showError);
})();
