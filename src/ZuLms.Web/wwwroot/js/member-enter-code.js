(() => {
  const root = document.getElementById('enter-code-page'); if (!root) return;
  const $ = name => document.getElementById(`ec-${name}`);
  const request = (method, path, body) => ZuLms.api.request(method, path, body);
  let page = 1, pages = 1;
  let courseRequest = 0;
  let lectureRequest = 0;

  function message(kind, text) {
    $('error').classList.add('d-none'); $('success').classList.add('d-none');
    $(kind).textContent = text; $(kind).classList.remove('d-none');
  }

  async function loadCourses() {
    const requestId = ++courseRequest;
    ++lectureRequest;
    const select = $('course'); select.replaceChildren();
    const option = document.createElement('option');
    option.value = ''; option.textContent = '— اختر دورة —'; select.append(option);
    $('lecture').replaceChildren();
    $('lecture').add(new Option('اختر دورة أولاً', ''));
    $('prev').disabled = true; $('next').disabled = true;
    try {
      const data = await request('GET', `/api/v1/courses/directory?view=Member&page=${page}`);
      if (requestId !== courseRequest) return;
      pages = Math.max(1, Math.ceil(data.total / data.pageSize));
      $('prev').disabled = page <= 1; $('next').disabled = page >= pages;
      $('page-number').textContent = `${page} / ${pages}`;
      for (const row of (data && data.items) || []) {
        const item = document.createElement('option');
        item.value = row.id; item.textContent = `${row.courseNo} — ${ZuLmsI18n.field(row, 'title')}`;
        select.append(item);
      }
      if (!data.items.length) message('error', 'لا توجد دورات مسجّلة في هذه الصفحة');
    } catch (e) { if (requestId === courseRequest) message('error', e.code || 'تعذّر تحميل الدورات'); }
  }

  async function loadLectures() {
    const requestId = ++lectureRequest;
    const course = $('course').value;
    const select = $('lecture'); select.replaceChildren();
    const option = document.createElement('option');
    option.value = ''; option.textContent = course ? '— اختر محاضرة —' : 'اختر دورة أولاً';
    select.append(option);
    if (!course) return;
    try {
      const rows = await request('GET', `/api/v1/courses/${course}/lectures`);
      if (requestId !== lectureRequest) return;
      for (const row of rows || []) {
        const item = document.createElement('option');
        item.value = row.id;
        item.textContent = `${ZuLmsI18n.field(row, 'title') || 'محاضرة'} — ${ZuLmsDates.display(row.startsAt)}`;
        select.append(item);
      }
    } catch (e) { if (requestId === lectureRequest) message('error', e.code || 'تعذّر تحميل المحاضرات'); }
  }

  $('form').addEventListener('submit', async event => {
    event.preventDefault();
    const lecture = $('lecture').value;
    const code = $('code').value.trim();
    if (!lecture || !code) { message('error', 'اختر المحاضرة وأدخل الكود'); return; }
    const button = $('submit'); button.disabled = true;
    try {
      await request('POST', `/api/v1/lectures/${lecture}/attendance/enter-code`, { code });
      message('success', 'سُجّل حضورك');
      $('code').value = '';
    } catch (e) {
      const labels = {
        ATT_LOCKED: 'محظور مؤقتاً بسبب محاولات خاطئة متكررة',
        ATT_CODE_EXPIRED: 'الكود غير مفعّل أو منتهي الصلاحية',
        ATT_CODE_WRONG: 'الكود غير صحيح',
        ATT_NOT_PARTICIPANT: 'لست مشاركاً فعّالاً في هذه الدورة'
      };
      message('error', labels[e.code] || e.code || 'تعذّر تسجيل الحضور');
    } finally { button.disabled = false; }
  });

  $('course').addEventListener('change', loadLectures);
  $('prev').addEventListener('click', () => { if (page > 1) { page--; loadCourses(); } });
  $('next').addEventListener('click', () => { if (page < pages) { page++; loadCourses(); } });
  loadCourses();
})();
