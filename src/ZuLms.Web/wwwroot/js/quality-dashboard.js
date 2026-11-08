(() => {
  const root = document.getElementById('quality-dashboard-page');
  if (!root) return;
  const error = document.getElementById('dashboard-error');
  const fail = e => { error.textContent = e.message || 'تعذّر تحميل المؤشرات'; error.classList.remove('d-none'); };
  const set = (id, text) => { const node = document.getElementById(id); if (node) node.textContent = text; };
  Promise.allSettled([
    ZuLms.api.get('/api/v1/proposals/pending?page=1'),
    ZuLms.api.get('/api/v1/proposals/faculties'),
    ZuLms.api.get('/api/v1/courses/directory?view=All&page=1')
  ]).then(([pending, faculties, courses]) => {
    if (pending.status === 'fulfilled') set('kpi-pending-proposals', String(pending.value.total));
    if (faculties.status === 'fulfilled') set('kpi-faculties', String(faculties.value.length));
    if (courses.status === 'fulfilled') set('kpi-available', String(courses.value.total));
    if ([pending, faculties, courses].some(x => x.status === 'rejected'))
      fail({ message: 'تعذّر تحميل بعض المؤشرات' });
  });
})();
