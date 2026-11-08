(() => {
  const root = document.getElementById('quality-needs-page'); if (!root) return;
  const $ = name => document.getElementById(`qneeds-${name}`);
  const request = (method, path, body) => ZuLms.api.request(method, path, body);
  let page = 1;

  function message(kind, text) {
    $('error').classList.add('d-none'); $('success').classList.add('d-none');
    $(kind).textContent = text; $(kind).classList.remove('d-none');
  }

  async function load() {
    const params = new URLSearchParams({ page: String(page) });
    const search = $('search').value.trim(); if (search) params.set('search', search);
    const status = $('status').value; if (status) params.set('status', status);
    try {
      const data = await request('GET', `/api/v1/needs?${params}`);
      const body = $('rows'); body.replaceChildren();
      for (const row of data.items || []) {
        const tr = document.createElement('tr');
        const title = document.createElement('td'); title.textContent = ZuLmsI18n.field(row, 'title');
        const center = document.createElement('td'); center.textContent = row.centerNameAr;
        const dean = document.createElement('td'); dean.textContent = row.deanNameAr;
        const status = document.createElement('td');
        status.textContent = row.status === 'Approved' ? 'معتمدة'
          : row.status === 'Returned' ? 'معادة' : 'بانتظار المراجعة';
        status.className = row.status === 'Approved' ? 'text-success'
          : row.status === 'Returned' ? 'text-danger' : 'text-warning';
        const letter = document.createElement('td');
        const link = document.createElement('a');
        link.href = `/api/v1/files/${row.letterFileId}`; link.textContent = 'الكتاب';
        letter.append(link);
        const actions = document.createElement('td');
        if (row.status === 'Submitted') {
          for (const [approve, label, className] of
            [[true, 'اعتماد', 'btn-success'], [false, 'إعادة', 'btn-outline-danger']]) {
            const button = document.createElement('button');
            button.type = 'button'; button.className = `btn btn-sm ${className} me-1`;
            button.textContent = label;
            button.addEventListener('click', async () => {
              const notes = await ZuLms.ui.prompt(approve ? 'ملاحظات (اختيارية):' : 'ملاحظات الإعادة (إلزامية):');
              if (notes === null) return;
              if (!approve && !notes.trim()) { message('error', 'ملاحظات الإعادة إلزامية'); return; }
              button.disabled = true;
              try {
                await request('POST', `/api/v1/needs/${row.id}/decision`, { approve, notes });
                message('success', 'حُفظ القرار'); await load();
              } catch (e) { message('error', e.code || 'تعذّر الحفظ'); }
              finally { button.disabled = false; }
            });
            actions.append(button);
          }
        } else actions.textContent = '—';
        tr.append(title, center, dean, status, letter, actions);
        body.append(tr);
      }
    } catch (e) { message('error', e.code || 'تعذّر التحميل'); }
  }

  $('filters').addEventListener('submit', event => { event.preventDefault(); page = 1; load(); });
  load();
})();
