(() => {
  const root = document.getElementById('quality-external-page'); if (!root) return;
  const search = document.getElementById('quality-external-search'); const rows = document.getElementById('quality-external-rows');
  const error = document.getElementById('quality-external-error'); let timer;
  async function load() {
    try {
      const data = await ZuLms.api.get(`/api/v1/proposals/external-instructors?search=${encodeURIComponent(search.value)}`);
      rows.replaceChildren();
      for (const item of data) { const tr = document.createElement('tr'); for (const value of [ZuLmsI18n.field(item, 'name'), item.loginName]) { const td = document.createElement('td'); td.textContent = value; tr.append(td); } rows.append(tr); }
      if (!data.length) { const tr = document.createElement('tr'); const td = document.createElement('td'); td.colSpan = 2; td.textContent = 'لا توجد نتائج'; tr.append(td); rows.append(tr); }
      error.classList.add('d-none');
    } catch (e) { error.textContent = e?.message || 'تعذّر تحميل المدربين'; error.classList.remove('d-none'); }
  }
  search.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(load, 250); }); load();
})();
