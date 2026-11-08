(() => {
  const arabic = new URLSearchParams(location.search).get('lang') === 'ar';
  document.documentElement.lang = arabic ? 'ar' : 'en';
  document.documentElement.dir = arabic ? 'rtl' : 'ltr';
  if (arabic) {
    document.querySelector('link[href$="bootstrap.min.css"]').href = '/assets/lib/bootstrap/dist/css/bootstrap.rtl.min.css';
    document.body.dataset.pickerClear = 'مسح التاريخ';
    document.body.dataset.pickerInvalid = 'أدخل تاريخاً صحيحاً بالصيغة المعروضة.';
  }
  const pending = [];
  const request = (method, path) => new Promise((resolve, reject) => pending.push({ method, path, resolve, reject }));
  window.fixture = { pending, take(fragment, method = 'GET') {
    const index = pending.findIndex(row => row.method === method && row.path.includes(fragment));
    if (index < 0) throw new Error(`No pending ${method} ${fragment}`);
    return pending.splice(index, 1)[0];
  } };
  window.ZuLms = { api: { request, get: path => request('GET', path), post: path => request('POST', path) } };
})();
