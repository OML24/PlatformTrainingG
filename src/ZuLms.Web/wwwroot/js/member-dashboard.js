(() => {
  const root = document.getElementById('member-dashboard-page');
  if (!root) return;
  const error = document.getElementById('dashboard-error');
  const fail = e => { error.textContent = e.message || 'تعذّر تحميل المؤشرات'; error.classList.remove('d-none'); };
  const set = (id, text) => { const node = document.getElementById(id); if (node) node.textContent = text; };
  Promise.allSettled([
    ZuLms.api.get('/api/v1/courses/available?page=1'),
    ZuLms.api.get('/api/v1/courses/directory?view=Member&page=1'),
    ZuLms.api.get('/api/v1/certificates/mine')
  ]).then(([available, enrolled, certificates]) => {
    if (available.status === 'fulfilled') set('kpi-available', String(available.value.total));
    if (enrolled.status === 'fulfilled') set('kpi-registered', String(enrolled.value.total));
    if (certificates.status === 'fulfilled') set('kpi-open', String(certificates.value.length));
    if ([available, enrolled, certificates].some(x => x.status === 'rejected'))
      fail({ message: 'تعذّر تحميل بعض المؤشرات' });
  });
})();
