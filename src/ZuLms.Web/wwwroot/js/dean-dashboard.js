(() => {
  const root = document.getElementById('dean-dashboard-page');
  if (!root) return;
  const error = document.getElementById('dashboard-error');
  const fail = e => { error.textContent = e.message || 'تعذّر تحميل المؤشرات'; error.classList.remove('d-none'); };
  const set = (id, text) => { const node = document.getElementById(id); if (node) node.textContent = text; };
  ZuLms.api.get('/api/v1/proposals/mine?page=1').then(data => {
    set('kpi-my-proposals', String(data.total));
  }).catch(fail);
})();
