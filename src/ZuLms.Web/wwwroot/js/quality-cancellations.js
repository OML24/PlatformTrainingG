(() => {
  const root = document.getElementById('quality-cancellations-page'); if (!root) return;
  const $ = name => document.getElementById(`cancellations-${name}`);
  const request = (method, path, body) => ZuLms.api.request(method, path, body);
  function message(kind, text) {
    $('error').classList.add('d-none'); $('success').classList.add('d-none');
    $(kind).textContent = text; $(kind).classList.remove('d-none');
  }
  function showError(e) { message('error', e.code || e.message || 'تعذّر إكمال الطلب'); }
  async function load() {
    const rows = await request('GET', '/api/v1/cancellations/pending');
    const body = $('rows'); body.replaceChildren();
    for (const row of rows) {
      const tr = document.createElement('tr');
      for (const text of [`${row.courseNo} — ${ZuLmsI18n.field(row, 'title')}`, row.requestedByNameAr,
        row.reason, ZuLmsDates.display(row.requestedAt)]) {
        const td = document.createElement('td'); td.textContent = text; tr.append(td);
      }
      const fileCell = document.createElement('td');
      if (row.letterFileId) {
        const link = document.createElement('a'); link.href = `/api/v1/files/${row.letterFileId}`;
        link.textContent = 'تنزيل الكتاب'; fileCell.append(link);
      } else fileCell.textContent = '—';
      tr.append(fileCell);
      const actions = document.createElement('td');
      for (const [approve, label, className] of [[true, 'موافقة', 'btn-success'], [false, 'رفض', 'btn-outline-danger']]) {
        const button = document.createElement('button'); button.type = 'button';
        button.className = `btn btn-sm ${className} me-1`; button.textContent = label;
        button.addEventListener('click', async () => {
          const notes = await ZuLms.ui.prompt(approve ? 'ملاحظات القرار (اختياري):' : 'سبب الرفض (إلزامي):');
          if (notes === null) return;
          if (!approve && !notes.trim()) { message('error', 'سبب الرفض إلزامي'); return; }
          if (approve && !await ZuLms.ui.confirm(`إلغاء الدورة ${row.courseNo} نهائياً وإلغاء شهاداتها الصادرة؟`)) return;
          button.disabled = true;
          try {
            await request('POST', `/api/v1/cancellations/${row.id}/decision`, { approve, notes });
            message('success', 'حُفظ القرار'); await load();
          } catch (e) { showError(e); }
          finally { button.disabled = false; }
        });
        actions.append(button);
      }
      tr.append(actions); body.append(tr);
    }
  }
  load().catch(showError);
})();
