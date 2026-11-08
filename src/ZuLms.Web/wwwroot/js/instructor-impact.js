(() => {
  const root = document.getElementById('instructor-impact-page'); if (!root) return;
  const $ = name => document.getElementById(`iimpact-${name}`);
  const request = (method, path, body) => ZuLms.api.request(method, path, body);
  const pathCourseId = new URLSearchParams(location.search).get('courseId');

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
      const data = await request('GET', '/api/v1/impact/review-courses');
      for (const row of data || []) {
        const item = document.createElement('option');
        const destination = row.destination === 'Dean' ? 'العميد' : 'الجودة';
        item.value = row.id;
        item.textContent = `#${row.id} — ${row.courseNo} — ${ZuLmsI18n.field(row, 'title')} (${destination}: ${row.pendingCount})`;
        select.append(item);
      }
      if (!data?.length) {
        option.textContent = 'لا توجد طلبات قياس أثر بانتظار المراجعة';
        option.disabled = true;
        $('rows').replaceChildren(emptyRow('لا توجد طلبات بانتظار المراجعة.'));
      } else if (pathCourseId) {
        select.value = pathCourseId;
        if (select.value) await load();
      }
    } catch (e) { message('error', e.code || 'تعذّر جلب دورات المراجعة'); }
  }

  function emptyRow(text) {
    const tr = document.createElement('tr');
    const td = document.createElement('td');
    td.colSpan = 5; td.className = 'text-center text-muted'; td.textContent = text;
    tr.append(td); return tr;
  }

  async function load() {
    const course = $('course').value;
    const body = $('rows'); body.replaceChildren();
    if (!course) { message('error', 'اختر دورة أولاً'); return; }
    try {
      const rows = await request('GET', `/api/v1/courses/${course}/impact/pending`);
      for (const row of rows || []) {
        const tr = document.createElement('tr');
        const name = document.createElement('td'); name.textContent = ZuLmsI18n.field(row, 'name');
        const type = document.createElement('td');
        type.textContent = row.type === 'StudyPlan' ? 'خطة دراسية'
          : row.type === 'ExamModel' ? 'نموذج اختبار' : 'نشاط صفّي';
        const sequence = document.createElement('td'); sequence.textContent = row.sequenceNo;
        const file = document.createElement('td');
        const link = document.createElement('a');
        link.href = `/api/v1/files/${row.fileId}`; link.textContent = 'الملف';
        file.append(link);
        const actions = document.createElement('td');
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
              await request('POST', `/api/v1/courses/${course}/impact/${row.id}/review`,
                { approve, notes });
              message('success', 'حُفظ القرار'); await load();
            } catch (e) { message('error', e.code || 'تعذّر الحفظ'); }
            finally { button.disabled = false; }
          });
          actions.append(button);
        }
        tr.append(name, type, sequence, file, actions);
        body.append(tr);
      }
      if (!rows?.length) body.append(emptyRow('لا توجد طلبات بانتظار المراجعة لهذه الدورة.'));
    } catch (e) { message('error', e.code || 'تعذّر جلب الطلبات'); }
  }

  $('load').addEventListener('click', load);
  loadCourses();
})();
