(() => {
  const root = document.getElementById('certificate-verify-page'); if (!root) return;
  const $ = id => document.getElementById(`verify-${id}`);
  const hide = () => ['loading', 'not-found', 'result'].forEach(x => $(x).classList.add('d-none'));
  function add(details, label, value, ltr = false) {
    const dt = document.createElement('dt'); dt.className = 'col-sm-4'; dt.textContent = label;
    const dd = document.createElement('dd'); dd.className = 'col-sm-8'; dd.textContent = value || '—';
    if (ltr) dd.dir = 'ltr'; details.append(dt, dd);
  }
  async function verify(reference) {
    const value = (reference || '').trim(); if (!value) return;
    $('reference').value = value; hide(); $('loading').classList.remove('d-none');
    try {
      const data = await ZuLms.api.request('GET', `/api/v1/public/verify/${encodeURIComponent(value)}`);
      hide();
      if (!data) { $('not-found').classList.remove('d-none'); return; }
      $('status').textContent = data.revoked ? 'الشهادة ملغاة' : 'شهادة صحيحة وسارية';
      $('status').className = data.revoked ? 'text-danger' : 'text-success';
      const details = $('details'); details.replaceChildren();
      add(details, 'الرقم المرجعي', data.referenceNo, true);
      add(details, 'اسم الحاصل', data.holderNameAr);
      add(details, 'الدورة', data.courseTitleAr);
      add(details, 'فترة الدورة', data.coursePeriodText, true);
      add(details, 'الساعات', String(data.courseHours));
      add(details, 'المحاضر', data.instructorNameAr);
      add(details, 'تاريخ الإصدار', new Date(data.issuedAt).toLocaleDateString('ar'));
      if (data.revokedAt) add(details, 'تاريخ الإلغاء', new Date(data.revokedAt).toLocaleDateString('ar'));
      $('result').classList.remove('d-none');
    } catch { hide(); $('not-found').classList.remove('d-none'); }
  }
  $('form').addEventListener('submit', event => { event.preventDefault(); verify($('reference').value); });
  const initial = new URLSearchParams(location.search).get('cert');
  if (initial) verify(initial);
})();
