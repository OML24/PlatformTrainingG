(() => {
  const root = document.getElementById('vault-page'); if (!root) return;
  const $ = name => document.getElementById(`vault-${name}`);
  const request = (method, path, body) => ZuLms.api.request(method, path, body);

  function message(kind, text) {
    $('error').classList.add('d-none'); $('success').classList.add('d-none');
    $(kind).textContent = text; $(kind).classList.remove('d-none');
  }

  async function load() {
    const params = new URLSearchParams();
    const search = $('search').value.trim(); if (search) params.set('search', search);
    const type = $('type').value; if (type) params.set('docType', type);
    try {
      const data = await request('GET', `/api/v1/vault?${params}`);
      const body = $('rows'); body.replaceChildren();
      for (const row of data.items || []) {
        const tr = document.createElement('tr');
        const title = document.createElement('td'); title.textContent = ZuLmsI18n.field(row, 'title');
        const type = document.createElement('td'); type.textContent = typeLabel(row.docType);
        const course = document.createElement('td'); course.textContent = row.courseNo || '—';
        const center = document.createElement('td'); center.textContent = row.centerNameAr || '—';
        const uploader = document.createElement('td'); uploader.textContent = row.uploadedByNameAr;
        const date = document.createElement('td');
        date.textContent = new Date(row.uploadedAt).toLocaleDateString('ar');
        const actions = document.createElement('td');
        const link = document.createElement('a');
        link.className = 'btn btn-sm btn-outline-primary';
        link.href = `/api/v1/vault/${row.id}/file`; link.textContent = 'تنزيل';
        actions.append(link);
        tr.append(title, type, course, center, uploader, date, actions);
        body.append(tr);
      }
    } catch (e) { message('error', e.code || 'تعذّر تحميل المستودع'); }
  }

  function typeLabel(kind) {
    const labels = {
      ProposalCouncilLetter: 'كتاب مجلس الكلية', InstructorCv: 'سيرة محاضر خارجي',
      CancellationLetter: 'كتاب إلغاء', NeedLetter: 'كتاب احتياج',
      QualityMinutes: 'محضر جودة', Recommendation: 'توصية',
      ProfessionalCertificate: 'شهادة مهنية', Other: 'أخرى'
    };
    return labels[kind] || kind;
  }

  $('form').addEventListener('submit', async event => {
    event.preventDefault();
    const file = $('file').files[0];
    if (!file) { message('error', 'اختر الملف'); return; }
    const button = $('submit'); button.disabled = true;
    try {
      const form = new FormData();
      form.append('kind', $('kind').value); form.append('file', file);
      const uploaded = await request('POST', '/api/v1/files', form);
      await request('POST', '/api/v1/vault', {
        fileId: uploaded.id, docType: $('kind').value,
        titleAr: $('title').value.trim(),
        courseId: $('course').value ? Number($('course').value) : null,
        centerId: $('center').value ? Number($('center').value) : null
      });
      message('success', 'رُبطت الوثيقة في المستودع');
      $('form').reset(); await load();
    } catch (e) { message('error', e.code || 'تعذّر الربط'); }
    finally { button.disabled = false; }
  });

  $('filters').addEventListener('submit', event => { event.preventDefault(); load(); });
  load();
})();
