(() => {
  const root = document.getElementById('member-impact-page'); if (!root) return;
  const $ = name => document.getElementById(`impact-${name}`);
  const request = (method, path, body) => ZuLms.api.request(method, path, body);

  function message(kind, text) {
    $('error').classList.add('d-none'); $('success').classList.add('d-none');
    $(kind).textContent = text; $(kind).classList.remove('d-none');
  }

  async function loadCourses() {
    const select = $('course');
    select.replaceChildren();
    const option = document.createElement('option');
    option.value = ''; option.textContent = '— اختر دورة —';
    select.append(option);
    try {
      const data = await request('GET', '/api/v1/courses/directory?view=Member');
      for (const row of (data && data.items) || []) {
        const item = document.createElement('option');
        item.value = row.id; item.textContent = `${row.courseNo} — ${ZuLmsI18n.field(row, 'title')}`;
        select.append(item);
      }
    } catch { /* no courses yet */ }
  }

  async function loadImpact() {
    const course = $('course').value;
    const body = $('rows'); body.replaceChildren();
    if (!course) return;
    try {
      const rows = await request('GET', `/api/v1/courses/${course}/impact`);
      for (const row of rows || []) {
        const tr = document.createElement('tr');
        const courseCell = document.createElement('td');
        const selected = $('course').selectedOptions[0];
        courseCell.textContent = selected ? selected.textContent : course;
        const type = document.createElement('td');
        type.textContent = row.type === 'StudyPlan' ? 'خطة دراسية'
          : row.type === 'ExamModel' ? 'نموذج اختبار' : 'نشاط صفّي';
        const status = document.createElement('td');
        status.textContent = row.status === 'Approved' ? 'معتمد'
          : row.status === 'Returned' ? 'معاد للتعديل' : 'بانتظار المراجعة';
        status.className = row.status === 'Approved' ? 'text-success'
          : row.status === 'Returned' ? 'text-danger' : 'text-warning';
        const sequence = document.createElement('td'); sequence.textContent = row.sequenceNo;
        const notes = document.createElement('td'); notes.textContent = row.notes || '—';
        const file = document.createElement('td');
        const link = document.createElement('a');
        link.href = `/api/v1/files/${row.fileId}`; link.textContent = 'الملف';
        file.append(link);
        tr.append(courseCell, type, status, sequence, notes, file);
        body.append(tr);
      }
    } catch (e) {
      if (e.code !== 'FORBIDDEN' && e.code !== 'IMPACT_NOT_OPEN')
        message('error', e.code || 'تعذّر جلب الأثر');
    }
  }

  $('form').addEventListener('submit', async event => {
    event.preventDefault();
    const course = $('course').value;
    const file = $('file').files[0];
    if (!course || !file) { message('error', 'اختر الدورة والملف'); return; }
    const button = $('submit'); button.disabled = true;
    try {
      const form = new FormData();
      form.append('kind', 'ImpactFile'); form.append('file', file);
      const uploaded = await request('POST', '/api/v1/files', form);
      await request('POST', `/api/v1/courses/${course}/impact`,
        { type: $('type').value, fileId: uploaded.id });
      message('success', 'رُفع الأثر وأُرسل للمحاضر للمراجعة');
      $('form').reset(); await loadCourses(); await loadImpact();
    } catch (e) {
      message('error', e.code === 'IMPACT_SAME_FILE' ? 'ارفع نسخة معدلة، لا الملف نفسه'
        : e.code || 'تعذّر الرفع');
    } finally { button.disabled = false; }
  });

  $('course').addEventListener('change', loadImpact);
  loadCourses(); loadImpact();
})();
