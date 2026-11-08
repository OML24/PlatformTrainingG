(() => {
  const root = document.getElementById('directory-page'); if (!root) return;
  const request = (method, path) => ZuLms.api.request(method, path);
  const error = document.getElementById('directory-error');
  const cards = document.getElementById('directory-cards');
  let page = 1, pages = 1, loadVersion = 0;
  function state(message) {
    const column = document.createElement('div'); column.className = 'col-12 empty-state';
    column.setAttribute('role', 'status'); column.textContent = message;
    cards.replaceChildren(column);
  }

  async function loadFilters() {
    const select = document.getElementById('directory-center');
    const filterError = document.getElementById('directory-filter-error');
    select.disabled = true;
    filterError.classList.add('d-none');
    try {
      const faculties = await request('GET', '/api/v1/users/faculties');
      const selected = select.value;
      select.replaceChildren();
      const none = document.createElement('option');
       none.value = ''; none.textContent = root.dataset.all; select.append(none);
       for (const faculty of faculties || []) {
        const option = document.createElement('option');
        option.value = faculty.id; option.textContent = ZuLmsI18n.field(faculty, 'name');
        select.append(option);
      }
      select.value = selected;
      select.disabled = false;
     } catch {
       filterError.classList.remove('d-none');
     }
  }

  async function load() {
    const version = ++loadVersion;
    const requestedPage = page;
    const params = new URLSearchParams({ page: String(page) });
    const search = document.getElementById('directory-search').value.trim();
    if (search) params.set('search', search);
    const center = document.getElementById('directory-center').value;
    if (center) params.set('centerId', center);
    error.classList.add('d-none'); cards.setAttribute('aria-busy', 'true'); state(root.dataset.loading);
    document.getElementById('directory-prev').disabled = true;
    document.getElementById('directory-next').disabled = true;
    try {
        const data = await request('GET', `/api/v1/profiles/directory?${params}`);
        if (version !== loadVersion) return;
       pages = Math.max(1, Math.ceil(data.total / data.pageSize));
       document.getElementById('directory-prev').disabled = requestedPage <= 1;
       document.getElementById('directory-next').disabled = requestedPage >= pages;
       document.getElementById('directory-page-number').textContent = `${requestedPage} / ${pages}`;
       cards.replaceChildren();
      for (const row of data.items || []) {
         const column = document.createElement('div'); column.className = 'col-12 col-md-6 col-xl-4';
        const card = document.createElement('div'); card.className = 'card h-100';
        const body = document.createElement('div'); body.className = 'card-body';
        const name = document.createElement('h2');
         name.className = 'h6 card-title'; name.textContent = row.fullNameAr;
         const avatar = document.createElement('span'); avatar.className = 'avatar av-navy mb-3';
         avatar.setAttribute('aria-hidden', 'true');
         avatar.textContent = (row.fullNameAr || '?').split(/\s+/).filter(Boolean).slice(0, 2).map(word => word[0]).join('');
        const sub = document.createElement('p');
        sub.className = 'card-text small text-muted';
        sub.textContent = [row.centerNameAr, row.departmentNameAr].filter(Boolean).join(' — ');
         body.append(avatar, name, sub);
        if (row.empStatus !== null && row.empStatus !== undefined) {
          const status = document.createElement('p');
          status.className = 'card-text small';
          status.textContent = row.empStatusDesc ? `${row.empStatusDesc} (${row.empStatus})` : String(row.empStatus);
          body.append(status);
        }
        for (const skill of row.expertiseAr || []) {
          const badge = document.createElement('span');
          badge.className = 'badge text-bg-secondary me-1'; badge.textContent = skill;
          body.append(badge);
        }
        const link = document.createElement('a');
        link.className = 'btn btn-sm btn-outline-primary mt-2 d-block';
         link.href = `/shared/profile/${row.userId}`; link.textContent = root.dataset.open;
        body.append(link);
        card.append(body); column.append(card); cards.append(column);
       }
       if (!data.items?.length) state(root.dataset.empty);
    } catch (e) {
       if (version !== loadVersion) return;
       error.textContent = e.code || root.dataset.error;
       error.classList.remove('d-none');
       cards.replaceChildren();
       document.getElementById('directory-page-number').textContent = '';
       const retry = document.createElement('button');
       retry.type = 'button'; retry.className = 'btn btn-outline-primary';
       retry.textContent = root.dataset.retry;
       retry.addEventListener('click', load);
       cards.append(retry);
     } finally { if (version === loadVersion) cards.setAttribute('aria-busy', 'false'); }
  }

  document.getElementById('directory-filters')
     .addEventListener('submit', event => { event.preventDefault(); page = 1; load(); });
   document.getElementById('directory-prev').addEventListener('click', () => { if (page > 1) { page--; load(); } });
    document.getElementById('directory-next').addEventListener('click', () => { if (page < pages) { page++; load(); } });
  document.getElementById('directory-filter-retry').addEventListener('click', loadFilters);
  loadFilters(); load();
})();
