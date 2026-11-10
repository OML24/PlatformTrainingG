(() => {
  const root = document.getElementById('admin-users-page'); if (!root) return;
  const $ = id => document.getElementById(id);
  const error = $('external-requests-error'), success = $('external-requests-success');
  const english = document.documentElement.lang === 'en';
  let page = 1, pages = 1, selected = null;
  function showError(e) { error.textContent = e.message || 'تعذّر إكمال الطلب'; error.classList.remove('d-none'); }
  async function csrf() { return ZuLms.api.csrf(); }
  function request(path, method = 'GET', body) { return ZuLms.api.request(method, path, body); }
  async function list() {
    const data = await request(`/api/v1/external-instructor-requests?page=${page}`);
    const header = root.querySelector('thead tr');
    header.replaceChildren(...(english
      ? ['Course number', 'Course title', 'Instructor', 'Organization', 'Email', 'Requested at', 'Course status', 'Request status', 'Action']
      : ['\u0631\u0642\u0645 \u0627\u0644\u062f\u0648\u0631\u0629', '\u0627\u0633\u0645 \u0627\u0644\u062f\u0648\u0631\u0629', '\u0627\u0644\u0645\u062f\u0631\u0628', '\u0627\u0644\u062c\u0647\u0629', '\u0627\u0644\u0628\u0631\u064a\u062f \u0627\u0644\u0625\u0644\u0643\u062a\u0631\u0648\u0646\u064a', '\u062a\u0627\u0631\u064a\u062e \u0627\u0644\u0637\u0644\u0628', '\u062d\u0627\u0644\u0629 \u0627\u0644\u062f\u0648\u0631\u0629', '\u062d\u0627\u0644\u0629 \u0627\u0644\u0637\u0644\u0628', '\u0627\u0644\u0625\u062c\u0631\u0627\u0621'])
      .map(text => { const cell = document.createElement('th'); cell.textContent = text; return cell; }));
    pages = Math.max(1, Math.ceil(data.total / data.pageSize));
    $('external-requests-page-number').textContent = `${page} / ${pages}`;
    $('external-requests-prev').disabled = page <= 1;
    $('external-requests-next').disabled = page >= pages;
    const rows = $('external-requests-rows'); rows.replaceChildren();
    for (const item of data.items) {
      const row = document.createElement('tr');
      const courseStatus = item.courseStatus === 'AwaitingAccount'
        ? (english ? 'Awaiting instructor account' : '\u0628\u0627\u0646\u062a\u0638\u0627\u0631 \u0625\u0646\u0634\u0627\u0621 \u062d\u0633\u0627\u0628 \u0627\u0644\u0645\u062f\u0631\u0628')
        : item.courseStatus;
      for (const text of [item.courseNo, item.courseTitleAr, item.fullNameAr,
        item.organizationAr, item.email,
        item.requestedAt ? new Date(item.requestedAt).toLocaleString(english ? 'en' : 'ar') : '',
        courseStatus, english ? 'Pending account creation' : '\u0645\u0639\u0644\u0642\u0629 \u062d\u062a\u0649 \u0625\u0646\u0634\u0627\u0621 \u0627\u0644\u062d\u0633\u0627\u0628']) {
        const td = document.createElement('td'); td.textContent = text; row.append(td);
      }
      const td = document.createElement('td');
      if (root.dataset.canCreate !== 'true') { row.append(td); rows.append(row); continue; }
      const button = document.createElement('button'); button.type = 'button';
      button.className = 'btn btn-sm btn-primary'; button.textContent = 'إنشاء الحساب';
      button.addEventListener('click', () => {
        selected = item; $('external-request-name').textContent = `${item.fullNameAr} — ${item.courseTitleAr} — ${english ? 'Pending account creation' : '\u0645\u0639\u0644\u0642\u0629 \u062d\u062a\u0649 \u0625\u0646\u0634\u0627\u0621 \u0627\u0644\u062d\u0633\u0627\u0628'}`;
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
      success.textContent = english ? 'External instructor account ' + result.loginName + ' created successfully; the course is now activated.' : '\u062a\u0645 \u0625\u0646\u0634\u0627\u0621 \u062d\u0633\u0627\u0628 \u0627\u0644\u0645\u062f\u0631\u0628 \u0628\u0646\u062c\u0627\u062d\u060c \u0648\u062a\u0645 \u062a\u0641\u0639\u064a\u0644 \u0627\u0644\u062f\u0648\u0631\u0629.';
      success.classList.remove('d-none'); await list();
    } catch (e2) { showError(e2); }
    finally { button.disabled = false; }
  });
  $('external-request-cancel').addEventListener('click', () => $('external-request-dialog').close());
  $('external-requests-prev').addEventListener('click', () => { page--; list().catch(showError); });
  $('external-requests-next').addEventListener('click', () => { page++; list().catch(showError); });
  list().catch(showError);
})();
