(() => {
  const root = document.getElementById('stats-dashboard-page');
  if (!root) return;
  const $ = id => document.getElementById(id);
  const error = $('dashboard-error');
  const charts = {};
  const colors = ['#022c5a', '#d4a72c', '#0f766e', '#7c3aed', '#dc2626', '#0284c7', '#64748b'];
  const contextRole = root.dataset.role.charAt(0).toUpperCase() + root.dataset.role.slice(1);
  Chart.defaults.font.family = 'Tajawal, Arial, sans-serif';
  Chart.defaults.color = '#475569';

  const fail = e => {
    error.textContent = e?.code === 'FORBIDDEN' ? ZuLmsI18n.t('forbidden')
      : e?.message || 'تعذّر تحميل الإحصائيات';
    error.classList.remove('d-none');
    if (e?.code === 'FORBIDDEN') root.classList.remove('stats-loading');
  };
  const set = (id, value) => { const node = $(id); if (node) node.textContent = value; };
  const option = item => {
    const node = document.createElement('option');
    node.value = String(item.id); node.textContent = item.name; return node;
  };
  const draw = (id, type, labels, datasets, extra = {}) => {
    charts[id]?.destroy();
    charts[id] = new Chart($(id), {
      type, data: { labels, datasets },
      options: { responsive: true, maintainAspectRatio: false,
        plugins: { legend: { position: 'bottom' } },
        scales: type === 'doughnut' ? {} : { y: { beginAtZero: true, ticks: { precision: 0 } } },
        ...extra }
    });
  };
  const emptyLabel = rows => rows.length ? rows : [{ label: 'لا توجد بيانات', value: 0 }];
  const params = () => {
    const values = new URLSearchParams();
    values.set('role', contextRole);
    [['centerId', 'stats-center'], ['userId', 'stats-user'], ['fromDate', 'stats-from'], ['toDate', 'stats-to']]
      .forEach(([key, id]) => { if ($(id)?.value) values.set(key, $(id).value); });
    return values.toString();
  };
  const render = data => {
    error.classList.add('d-none');
    set('stats-scope', ZuLmsI18n.translate(data.scopeLabel)); set('kpi-courses', data.courses);
    set('kpi-participants', data.participants); set('kpi-certificates', data.certificates);
    set('kpi-completion', data.completionRate);
    const person = $('person-summary');
    person.classList.toggle('d-none', !data.person);
    if (data.person) {
      set('person-name', data.person.name); set('person-courses', data.person.courses);
      set('person-finished', data.person.finishedCourses); set('person-certificates', data.person.certificates);
    }
    const monthly = data.monthly.length ? data.monthly : [{ label: 'لا توجد بيانات', courses: 0, participants: 0 }];
    draw('chart-monthly', 'line', monthly.map(x => x.label), [
      { label: 'الدورات', data: monthly.map(x => x.courses), borderColor: colors[0], backgroundColor: '#022c5a22', fill: true, tension: .3 },
      { label: 'المشاركات', data: monthly.map(x => x.participants), borderColor: colors[1], backgroundColor: '#d4a72c22', fill: true, tension: .3 }
    ]);
    const status = emptyLabel(data.byStatus);
    draw('chart-status', 'doughnut', status.map(x => x.label), [{ data: status.map(x => x.value), backgroundColor: colors, borderWidth: 2 }]);
    const faculties = emptyLabel(data.byFaculty);
    draw('chart-faculty', 'bar', faculties.map(x => x.label), [{ label: 'الدورات', data: faculties.map(x => x.value), backgroundColor: colors[2] }], { indexAxis: 'y' });
    const tracks = emptyLabel(data.byTrack);
    draw('chart-track', 'bar', tracks.map(x => x.label), [{ label: 'الدورات', data: tracks.map(x => x.value), backgroundColor: colors[0] }]);
  };
  const load = async () => {
    root.classList.add('stats-loading');
    try { render(await ZuLms.api.get(`/api/v1/stats/dashboard?${params()}`)); }
    catch (e) { fail(e); }
    finally { root.classList.remove('stats-loading'); }
  };
  const init = async () => {
    try {
      const lookups = await ZuLms.api.get(`/api/v1/stats/lookups?role=${encodeURIComponent(contextRole)}`);
      lookups.faculties.forEach(x => $('stats-center').append(option(x)));
      root.querySelectorAll('[data-faculty-filter]').forEach(x =>
        x.classList.toggle('d-none', !lookups.canFilterFaculty));
      root.querySelectorAll('[data-user-filter]').forEach(x =>
        x.classList.toggle('d-none', !lookups.canFilterUser));
      await load();
    } catch (e) { fail(e); }
  };
  $('stats-filters').addEventListener('submit', e => { e.preventDefault(); load(); });
  let userSearchTimer;
  $('stats-user-search').addEventListener('input', () => {
    clearTimeout(userSearchTimer);
    $('stats-user').replaceChildren(option({ id: '', name: 'جميع الأشخاص' }));
    const term = $('stats-user-search').value.trim();
    if (term.length < 2) return;
    userSearchTimer = setTimeout(async () => {
      try {
        const data = await ZuLms.api.get(`/api/v1/stats/lookups?role=${encodeURIComponent(contextRole)}&userSearch=${encodeURIComponent(term)}`);
        if ($('stats-user-search').value.trim() !== term) return;
        data.users.forEach(x => $('stats-user').append(option(x)));
      } catch (e) { fail(e); }
    }, 250);
  });
  $('stats-reset').addEventListener('click', () => {
    ['stats-center', 'stats-user-search', 'stats-user', 'stats-from', 'stats-to'].forEach(id => { if ($(id)) $(id).value = ''; });
    load();
  });
  init();
})();
