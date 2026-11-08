(() => {
  const root = document.getElementById('dean-members-page'); if (!root) return;
  const error = document.getElementById('dm-error');

  async function load() {
    const search = document.getElementById('dm-search').value.trim();
    const params = new URLSearchParams();
    if (search) params.set('search', search);
    try {
      const members = await ZuLms.api.request('GET',
        `/api/v1/proposals/faculty-members?${params}`);
      const body = document.getElementById('dm-rows'); body.replaceChildren();
      for (const member of members || []) {
        const tr = document.createElement('tr');
        const name = document.createElement('td'); name.textContent = ZuLmsI18n.field(member, 'name');
        const department = document.createElement('td');
        department.textContent = member.departmentNameAr || '—';
        const profile = document.createElement('td');
        const link = document.createElement('a');
        link.href = `/shared/profile/${member.userId}`; link.textContent = 'فتح الملف';
        profile.append(link);
        const statistics = document.createElement('td');
        const statsLink = document.createElement('a');
        statsLink.href = `/dean/stats?userId=${member.userId}`;
        statsLink.className = 'btn btn-sm btn-outline-primary';
        statsLink.textContent = 'عرض الإحصائيات'; statistics.append(statsLink);
        tr.append(name, department, profile, statistics);
        body.append(tr);
      }
    } catch (e) {
      error.textContent = e.code || 'تعذّر تحميل الأعضاء';
      error.classList.remove('d-none');
    }
  }

  document.getElementById('dm-filters')
    .addEventListener('submit', event => { event.preventDefault(); load(); });
  load();
})();
