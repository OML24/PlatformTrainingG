(() => {
  const root = document.getElementById('admin-needs-page'); if (!root) return;
  const request = (method, path, body) => ZuLms.api.request(method, path, body);
  const error = document.getElementById('aneeds-error');
  const success = document.getElementById('aneeds-success');

  function message(kind, text) {
    error.classList.add('d-none'); success.classList.add('d-none');
    (kind === 'error' ? error : success).textContent = text;
    (kind === 'error' ? error : success).classList.remove('d-none');
  }

  async function load() {
    try {
      const data = await request('GET', '/api/v1/needs');
      const body = document.getElementById('aneeds-rows'); body.replaceChildren();
      for (const row of data.items || []) {
        const tr = document.createElement('tr');
        const title = document.createElement('td'); title.textContent = ZuLmsI18n.field(row, 'title');
        const center = document.createElement('td'); center.textContent = row.centerNameAr;
        const dean = document.createElement('td'); dean.textContent = row.deanNameAr;
        const status = document.createElement('td');
        status.textContent = ZuLmsI18n.t(`needStatus.${row.status}`);
        status.className = row.status === 'Approved' ? 'text-success'
          : row.status === 'Returned' ? 'text-warning' : '';
        const letter = document.createElement('td');
        const link = document.createElement('a');
        link.href = `/api/v1/files/${row.letterFileId}`; link.textContent = 'الكتاب';
        letter.append(link);
        const actions = document.createElement('td');
        const deactivate = document.createElement('button');
        deactivate.type = 'button'; deactivate.className = 'btn btn-sm btn-outline-danger';
        deactivate.textContent = 'تعطيل';
        deactivate.addEventListener('click', async () => {
          const reason = await ZuLms.ui.prompt('سبب التعطيل (إلزامي):');
          if (!reason || !reason.trim()) return;
          deactivate.disabled = true;
          try {
            await request('POST', `/api/v1/needs/${row.id}/deactivate`, { reason: reason.trim() });
            message('success', 'عُطّل الاحتياج'); await load();
          } catch (e) { message('error', e.code || 'تعذّر التعطيل'); }
          finally { deactivate.disabled = false; }
        });
        actions.append(deactivate);
        tr.append(title, center, dean, status, letter, actions);
        body.append(tr);
      }
    } catch (e) { message('error', e.code || 'تعذّر التحميل'); }
  }

  load();
})();
