(() => {
  const root = document.getElementById('member-procerts-page'); if (!root) return;
  const $ = name => document.getElementById(`pc-${name}`);
  const request = (method, path, body) => ZuLms.api.request(method, path, body);
  let editingId = null;

  function message(kind, text) {
    $('error').classList.add('d-none'); $('success').classList.add('d-none');
    $(kind).textContent = text; $(kind).classList.remove('d-none');
  }

  async function loadCategories() {
    try {
      const categories = await request('GET', '/api/v1/procerts/categories');
      const select = $('category'); select.replaceChildren();
      for (const category of categories || []) {
        const option = document.createElement('option');
        option.value = category.id; option.textContent = ZuLmsI18n.field(category, 'name');
        select.append(option);
      }
    } catch { /* none */ }
  }

  function payload(fileId) {
    return {
      name: $('name').value.trim(), nameAr: $('name-ar').value.trim() || null,
      issuerName: $('issuer').value.trim(),
      issuerWebsiteUrl: $('issuer-url').value.trim() || null,
      categoryId: Number($('category').value), description: $('description').value.trim() || null,
      credentialCode: $('code').value.trim() || null, verifyUrl: $('verify').value.trim() || null,
      issuedDate: $('issued').value, expiryDate: $('expiry').value || null, fileId
    };
  }

  async function load() {
    try {
      const rows = await request('GET', '/api/v1/procerts?includeInactive=true');
      const body = $('rows'); body.replaceChildren();
      for (const row of rows.items || []) {
        const tr = document.createElement('tr');
        const name = document.createElement('td'); name.textContent = row.name;
        const issuer = document.createElement('td'); issuer.textContent = row.issuerName;
        const category = document.createElement('td'); category.textContent = row.categoryNameAr;
        const issued = document.createElement('td'); issued.textContent = row.issuedDate;
        const expiry = document.createElement('td');
        expiry.textContent = row.expiryDate || 'لا تنتهي';
        if (row.isExpired) { expiry.textContent += ' (منتهية)'; expiry.className = 'text-danger'; }
        const status = document.createElement('td');
        status.textContent = row.status === 'Approved' ? 'معتمدة'
          : row.status === 'Rejected' ? 'مرفوضة' : 'بانتظار المراجعة';
        status.className = row.status === 'Approved' ? 'text-success'
          : row.status === 'Rejected' ? 'text-danger' : 'text-warning';
        if (row.status === 'Rejected' && row.decisionNotes)
          status.title = row.decisionNotes;
        const actions = document.createElement('td');
        if (row.status === 'Rejected') {
          const edit = document.createElement('button');
          edit.type = 'button'; edit.className = 'btn btn-sm btn-outline-primary me-1';
          edit.textContent = 'تعديل وإعادة إرسال';
          edit.addEventListener('click', async () => {
            editingId = row.id;
            $('form-title').textContent = 'تعديل الشهادة المرفوضة وإعادة إرسالها';
            $('name').value = row.name; $('issuer').value = row.issuerName;
            $('category').value = row.categoryId; $('issued').value = row.issuedDate;
            $('expiry').value = row.expiryDate || ''; $('code').value = row.credentialCode || '';
            $('verify').value = row.verifyUrl || '';
            window.scrollTo({ top: 0, behavior: 'smooth' });
          });
          actions.append(edit);
        }
        if (row.status === 'Approved' && !row.isExpired) {
          const file = document.createElement('a');
          file.className = 'btn btn-sm btn-outline-secondary me-1';
          file.href = `/api/v1/procerts/${row.id}/file`; file.textContent = 'الملف';
          actions.append(file);
        }
        tr.append(name, issuer, category, issued, expiry, status, actions);
        body.append(tr);
      }
    } catch (e) { message('error', e.code || 'تعذّر التحميل'); }
  }

  $('form').addEventListener('submit', async event => {
    event.preventDefault();
    const file = $('file').files[0];
    if (!file) { message('error', 'اختر ملف الشهادة'); return; }
    const button = $('submit'); button.disabled = true;
    try {
      const form = new FormData();
      form.append('kind', 'ProfessionalCertificate'); form.append('file', file);
      const uploaded = await request('POST', '/api/v1/files', form);
      const body = payload(uploaded.id);
      if (editingId) {
        await request('POST', `/api/v1/procerts/${editingId}/resubmit`, body);
        editingId = null; $('form-title').textContent = 'إضافة شهادة مهنية';
        message('success', 'أُعيد إرسال الشهادة إلى الجودة');
      } else {
        await request('POST', '/api/v1/procerts', body);
        message('success', 'أُرسلت الشهادة إلى الجودة للمراجعة');
      }
      $('form').reset(); await loadCategories(); await load();
    } catch (e) {
      const labels = {
        PROCERT_NEEDS_EVIDENCE: 'أدخل رمز الشهادة أو رابط التحقق على الأقل',
        PROCERT_DUPLICATE: 'سجّلت هذه الشهادة مسبقاً (نفس الجهة والرمز)',
        PROCERT_DATE_INVALID: 'تحقق من تاريخي الإصدار والانتهاء',
        PROCERT_URL_INVALID: 'روابط غير صالحة (استخدم https للجهة)',
        PROCERT_FILE_INVALID: 'الملف غير صالح (PDF أو PPTX فقط)'
      };
      message('error', labels[e.code] || e.code || 'تعذّر الإرسال');
    } finally { button.disabled = false; }
  });

  loadCategories(); load();
})();
