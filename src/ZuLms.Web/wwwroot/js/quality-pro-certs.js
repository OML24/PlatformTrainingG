(() => {
  const root = document.getElementById('quality-procerts-page'); if (!root) return;
  const $ = name => document.getElementById(`qpc-${name}`);
  const request = (method, path, body) => ZuLms.api.request(method, path, body);
  let statusFilter = 'Submitted';

  function message(kind, text) {
    $('error').classList.add('d-none'); $('success').classList.add('d-none');
    $(kind).textContent = text; $(kind).classList.remove('d-none');
  }

  async function loadCategories() {
    try {
      const categories = await request('GET', '/api/v1/procerts/categories');
      const select = $('category'); select.replaceChildren();
      const none = document.createElement('option');
      none.value = ''; none.textContent = 'كل التصنيفات'; select.append(none);
      for (const category of categories || []) {
        const option = document.createElement('option');
        option.value = category.id; option.textContent = ZuLmsI18n.field(category, 'name');
        select.append(option);
      }
    } catch { /* none */ }
  }

  async function load() {
    const params = new URLSearchParams();
    if (statusFilter) params.set('status', statusFilter);
    const search = $('search').value.trim(); if (search) params.set('search', search);
    const category = $('category').value; if (category) params.set('categoryId', category);
    try {
      const data = await request('GET', `/api/v1/procerts?${params}`);
      const body = $('rows'); body.replaceChildren();
      for (const row of data.items || []) {
        const tr = document.createElement('tr');
        const member = document.createElement('td'); member.textContent = row.ownerNameAr;
        const name = document.createElement('td'); name.textContent = row.name;
        const issuer = document.createElement('td'); issuer.textContent = row.issuerName;
        const issued = document.createElement('td'); issued.textContent = row.issuedDate;
        const status = document.createElement('td');
        status.textContent = row.status === 'Approved' ? 'معتمدة'
          : row.status === 'Rejected' ? 'مرفوضة' : 'بانتظار المراجعة';
        status.className = row.status === 'Approved' ? 'text-success'
          : row.status === 'Rejected' ? 'text-danger' : 'text-warning';
        const file = document.createElement('td');
        const link = document.createElement('a');
        link.href = `/api/v1/procerts/${row.id}/file`; link.textContent = 'الملف';
        file.append(link);
        const actions = document.createElement('td');
        if (row.status === 'Submitted') {
          for (const [approve, label, className] of
            [[true, 'قبول', 'btn-success'], [false, 'رفض', 'btn-outline-danger']]) {
            const button = document.createElement('button');
            button.type = 'button'; button.className = `btn btn-sm ${className} me-1`;
            button.textContent = label;
            button.addEventListener('click', async () => {
              const notes = await ZuLms.ui.prompt(approve ? 'ملاحظات (اختيارية):' : 'سبب الرفض (إلزامي):');
              if (notes === null) return;
              if (!approve && !notes.trim()) { message('error', 'سبب الرفض إلزامي'); return; }
              button.disabled = true;
              try {
                await request('POST', `/api/v1/procerts/${row.id}/decision`, { approve, notes });
                message('success', 'حُفظ القرار'); await load();
              } catch (e) { message('error', e.code || 'تعذّر الحفظ'); }
              finally { button.disabled = false; }
            });
            actions.append(button);
          }
        } else actions.textContent = '—';
        tr.append(member, name, issuer, issued, status, file, actions);
        body.append(tr);
      }
    } catch (e) { message('error', e.code || 'تعذّر التحميل'); }
  }

  root.querySelectorAll('.nav-link').forEach(link => link.addEventListener('click', event => {
    event.preventDefault();
    root.querySelectorAll('.nav-link').forEach(x => x.classList.remove('active'));
    link.classList.add('active');
    statusFilter = link.dataset.status || '';
    load();
  }));
  $('filters').addEventListener('submit', event => { event.preventDefault(); load(); });
  loadCategories(); load();
})();
