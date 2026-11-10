(() => {
  const root = document.getElementById('member-dashboard-page');
  if (!root) return;
  const error = document.getElementById('dashboard-error');
  const fail = e => { error.textContent = e.message || 'تعذّر تحميل المؤشرات'; error.classList.remove('d-none'); };
  const show = (id, value) => {
    const node = document.getElementById(id);
    if (!node) return;
    if (window.ZuUi) ZuUi.countUp(node, value, { decimals: 0 }); else node.textContent = String(value);
  };
  const today = document.getElementById('zm-today');
  if (today) {
    today.textContent = new Intl.DateTimeFormat(document.documentElement.lang === 'en' ? 'en-GB' : 'ar-JO-u-nu-latn',
      { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric', timeZone: 'Asia/Amman' }).format(new Date());
  }
  Promise.allSettled([
    ZuLms.api.get('/api/v1/courses/available?page=1'),
    ZuLms.api.get('/api/v1/courses/directory?view=Member&page=1'),
    ZuLms.api.get('/api/v1/certificates/mine')
  ]).then(([available, enrolled, certificates]) => {
    if (available.status === 'fulfilled') show('kpi-available', available.value.total);
    if (enrolled.status === 'fulfilled') show('kpi-registered', enrolled.value.total);
    if (certificates.status === 'fulfilled') show('kpi-open', certificates.value.length);
    if ([available, enrolled, certificates].some(x => x.status === 'rejected'))
      fail({ message: 'تعذّر تحميل بعض المؤشرات' });
  });
})();
