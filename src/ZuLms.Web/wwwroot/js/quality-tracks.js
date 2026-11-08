(() => {
  const root = document.getElementById('tracks-page'); if (!root) return;
  const $ = name => document.getElementById(`tracks-${name}`);
  const request = (method, path, body) => ZuLms.api.request(method, path, body);
  let editingId = null;

  function message(kind, text) {
    $('error').classList.add('d-none'); $('success').classList.add('d-none');
    $(kind).textContent = text; $(kind).classList.remove('d-none');
  }

  async function load() {
    try {
      const rows = await request('GET', '/api/v1/tracks?includeArchived=true');
      const body = $('rows'); body.replaceChildren();
      for (const row of rows || []) {
        const tr = document.createElement('tr');
        const name = document.createElement('td');
        name.textContent = ZuLmsI18n.field(row, 'name');
        const sort = document.createElement('td'); sort.textContent = row.sortOrder;
        const status = document.createElement('td');
        status.textContent = row.isArchived ? 'مؤرشف' : 'فعّال';
        status.className = row.isArchived ? 'text-muted' : 'text-success';
        const actions = document.createElement('td');
        const edit = document.createElement('button');
        edit.type = 'button'; edit.className = 'btn btn-sm btn-outline-primary me-1';
        edit.textContent = 'تعديل';
        edit.addEventListener('click', () => {
          editingId = row.id;
          $('name').value = row.nameAr; $('name-en').value = row.nameEn || '';
          $('sort').value = row.sortOrder;
          $('submit').textContent = 'حفظ التعديل';
        });
        const archive = document.createElement('button');
        archive.type = 'button';
        archive.className = `btn btn-sm ${row.isArchived ? 'btn-outline-success' : 'btn-outline-danger'}`;
        archive.textContent = row.isArchived ? 'إلغاء الأرشفة' : 'أرشفة';
        archive.addEventListener('click', async () => {
          archive.disabled = true;
          try {
            await request('POST',
              `/api/v1/tracks/${row.id}/${row.isArchived ? 'unarchive' : 'archive'}`);
            await load();
          } catch (e) { message('error', e.code || 'تعذّر التنفيذ'); }
          finally { archive.disabled = false; }
        });
        actions.append(edit, archive);
        tr.append(name, sort, status, actions);
        body.append(tr);
      }
    } catch (e) { message('error', e.code || 'تعذّر التحميل'); }
  }

  $('form').addEventListener('submit', async event => {
    event.preventDefault();
    const payload = {
      nameAr: $('name').value.trim(), nameEn: $('name-en').value.trim() || null,
      sortOrder: Number($('sort').value || 0)
    };
    try {
      if (editingId) {
        await request('PUT', `/api/v1/tracks/${editingId}`, payload);
        editingId = null; $('submit').textContent = 'حفظ المحور';
      } else await request('POST', '/api/v1/tracks', payload);
      message('success', 'حُفظ المحور');
      $('form').reset(); await load();
    } catch (e) { message('error', e.code || 'تعذّر الحفظ'); }
  });

  load();
})();
