(() => {
  const root = document.getElementById('dean-needs-page'); if (!root) return;
  const $ = name => document.getElementById(`needs-${name}`);
  const request = (method, path, body) => ZuLms.api.request(method, path, body);

  function message(kind, text) {
    $('error').classList.add('d-none'); $('success').classList.add('d-none');
    $(kind).textContent = text; $(kind).classList.remove('d-none');
  }
  function showError(e) { message('error', e.code || e.message || 'تعذّر إكمال الطلب'); }

  async function load() {
    try {
      const data = await request('GET', '/api/v1/needs');
      const body = $('rows'); body.replaceChildren();
      for (const row of data.items || []) {
        const tr = document.createElement('tr');
        const title = document.createElement('td'); title.textContent = ZuLmsI18n.field(row, 'title');
        const status = document.createElement('td');
        status.textContent = row.status === 'Approved' ? 'معتمدة'
          : row.status === 'Returned' ? 'معادة بملاحظات' : 'بانتظار المراجعة';
        status.className = row.status === 'Approved' ? 'text-success'
          : row.status === 'Returned' ? 'text-danger' : 'text-warning';
        const revision = document.createElement('td'); revision.textContent = row.currentRevision;
        const created = document.createElement('td');
        created.textContent = new Date(row.createdAt).toLocaleDateString('ar');
        const letter = document.createElement('td');
        const link = document.createElement('a');
        link.href = `/api/v1/files/${row.letterFileId}`; link.textContent = 'الكتاب';
        letter.append(link);
        const actions = document.createElement('td');
        if (row.status === 'Returned') {
          const resubmit = document.createElement('button');
          resubmit.type = 'button'; resubmit.className = 'btn btn-sm btn-outline-primary';
          resubmit.textContent = 'تعديل وإعادة إرسال';
          resubmit.addEventListener('click', async () => {
            const details = await request('GET', `/api/v1/needs/${row.id}`);
            $('title').value = details.titleAr || '';
            $('description').value = details.descriptionAr || '';
            root.dataset.resubmitId = String(row.id);
            message('info', `عدّل البيانات ثم أرسل؛ ملاحظات الجودة: ${details.lastReturnNotes || '—'}`);
            window.scrollTo({ top: 0, behavior: 'smooth' });
          });
          actions.append(resubmit);
        } else actions.textContent = '—';
        tr.append(title, status, revision, created, letter, actions);
        body.append(tr);
      }
    } catch (e) { showError(e); }
  }

  async function uploadLetter() {
    const file = $('letter').files[0];
    if (!file) throw new Error('اختر كتاب الاحتياج PDF');
    const form = new FormData();
    form.append('kind', 'NeedLetter'); form.append('file', file);
    const uploaded = await request('POST', '/api/v1/files', form);
    if (!uploaded || !uploaded.id) throw new Error('تعذّر رفع الكتاب');
    return uploaded.id;
  }

  function payload(letterFileId) {
    return {
      titleAr: $('title').value.trim(),
      descriptionAr: $('description').value.trim(),
      letterFileId
    };
  }

  $('form').addEventListener('submit', async event => {
    event.preventDefault();
    const button = $('submit'); button.disabled = true;
    try {
      const letterFileId = await uploadLetter();
      const resubmitId = root.dataset.resubmitId;
      const body = payload(letterFileId);
      if (resubmitId) {
        await request('POST', `/api/v1/needs/${resubmitId}/resubmit`, body);
        delete root.dataset.resubmitId;
        message('success', 'أُعيد إرسال الاحتياج إلى الجودة');
      } else {
        await request('POST', '/api/v1/needs', body);
        message('success', 'رُفع الاحتياج وأُرسل إلى الجودة');
      }
      $('form').reset(); await load();
    } catch (e) {
      message('error', e.code === 'NEED_LETTER_INVALID' ? 'الكتاب غير صالح (PDF فقط)' : e.message || e.code || 'تعذّر الإرسال');
    } finally { button.disabled = false; }
  });

  load();
})();
