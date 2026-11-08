(() => {
  const root = document.getElementById('procstats-page'); if (!root) return;
  const $ = name => document.getElementById(`ps-${name}`);
  const request = (method, path) => ZuLms.api.request(method, path);
  const error = $('error');

  async function loadFilters() {
    try {
      const categories = await request('GET', '/api/v1/procerts/categories');
      const category = $('category'); category.replaceChildren();
      const none = document.createElement('option');
      none.value = ''; none.textContent = 'كل التصنيفات'; category.append(none);
      for (const item of categories || []) {
        const option = document.createElement('option');
        option.value = item.id; option.textContent = ZuLmsI18n.field(item, 'name'); category.append(option);
      }
    } catch { /* none */ }
    try {
      const faculties = await request('GET', '/api/v1/users/faculties');
      const faculty = $('faculty'); faculty.replaceChildren();
      const none = document.createElement('option');
      none.value = ''; none.textContent = 'كل الكليات'; faculty.append(none);
      for (const item of faculties || []) {
        const option = document.createElement('option');
        option.value = item.id; option.textContent = ZuLmsI18n.field(item, 'name'); faculty.append(option);
      }
    } catch { /* none */ }
  }

  function fillTable(host, rows, columns) {
    const body = $(host); body.replaceChildren();
    for (const row of rows || []) {
      const tr = document.createElement('tr');
      for (const column of columns) {
        const td = document.createElement('td'); td.textContent = column(row);
        tr.append(td);
      }
      body.append(tr);
    }
  }

  function card(label, value) {
    const column = document.createElement('div'); column.className = 'col-md-3';
    const card = document.createElement('div'); card.className = 'card card-body text-center';
    const title = document.createElement('div'); title.className = 'text-muted small';
    title.textContent = label;
    const number = document.createElement('div'); number.className = 'h4 mb-0';
    number.textContent = value;
    card.append(title, number); column.append(card);
    return column;
  }

  async function load() {
    const params = new URLSearchParams({ includeExpired: 'true' });
    const faculty = $('faculty').value; if (faculty) params.set('facultyId', faculty);
    const category = $('category').value; if (category) params.set('categoryId', category);
    const issuer = $('issuer').value.trim(); if (issuer) params.set('issuer', issuer);
    const from = $('from').value; if (from) params.set('from', from);
    try {
      const data = await request('GET', `/api/v1/procerts/stats?${params}`);
      $('export').href = `/api/v1/procerts/stats/export?${params}`;
      const totals = $('totals'); totals.replaceChildren();
      totals.append(
        card('الشهادات المعتمدة', data.totals.certs),
        card('الأعضاء أصحاب الشهادات', data.totals.members),
        card('المتوسط لكل عضو', data.totals.avgPerMember),
        card('نسبة التغطية', `${data.totals.coveragePercent}%`));
      fillTable('top', data.topMembers, [r => r.name, r => r.facultyName, r => r.count]);
      fillTable('faculties', data.byFaculty,
        [r => r.facultyName, r => r.certs, r => r.members, r => r.avgPerMember]);
      fillTable('categories', data.byCategory,
        [r => r.name, r => r.certs, r => r.members]);
      fillTable('issuers', data.topIssuers,
        [r => r.issuer, r => r.certs, r => r.members]);
    } catch (e) {
      error.textContent = e.code || 'تعذّر جلب الإحصاءات';
      error.classList.remove('d-none');
    }
  }

  $('filters').addEventListener('submit', event => { event.preventDefault(); load(); });
  loadFilters(); load();
})();
