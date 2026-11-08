(() => {
  const root = document.getElementById('eval-results-page'); if (!root) return;
  const $ = name => document.getElementById(`results-${name}`);
  const agg = name => document.getElementById(`aggregate-${name}`);
  const request = (method, path, body) => ZuLms.api.request(method, path, body);

  function message(kind, text) {
    $('error').classList.add('d-none'); $('success').classList.add('d-none');
    $(kind).textContent = text; $(kind).classList.remove('d-none');
  }

  function render(data) {
    $('body').classList.remove('d-none');
    $('course-title').textContent = `${data.courseNo} — ${data.courseTitleAr}`;
    $('summary').textContent = `المحاضر: ${data.instructorNameAr} — نسخة النموذج ${data.versionNo} — عدد الاستجابات ${data.responses}`;
    const host = $('questions'); host.replaceChildren();
    for (const question of data.questions || []) {
      const card = document.createElement('div');
      card.className = 'card card-body';
      const title = document.createElement('h3');
      title.className = 'h6'; title.textContent = `نسخة ${data.versionNo}: ${question.textAr}`;
      card.append(title);
      if (question.averageScore != null) {
        const average = document.createElement('p');
        average.className = 'text-muted mb-2';
        average.textContent = `متوسط الدرجة: ${question.averageScore}`;
        card.append(average);
      }
      const list = document.createElement('ul');
      list.className = 'list-group list-group-flush';
      for (const option of question.options || []) {
        const item = document.createElement('li');
        item.className = 'list-group-item d-flex justify-content-between';
        const label = document.createElement('span'); label.textContent = ZuLmsI18n.field(option, 'label');
        const value = document.createElement('span');
        value.textContent = `${option.count} (${option.sharePercent}%)` +
          (option.score != null ? ` — درجة ${option.score}` : '');
        item.append(label, value); list.append(item);
      }
      card.append(list); host.append(card);
    }
  }

  $('filters').addEventListener('submit', async event => {
    event.preventDefault();
    const course = $('course').value.trim();
    if (!course) { message('error', 'أدخل معرّف الدورة'); return; }
    try {
      render(await request('GET', `/api/v1/evaluation/results/${course}`));
    } catch (e) { message('error', e.code || 'تعذّر جلب النتائج'); }
  });

  $('extend-submit').addEventListener('click', async () => {
    const course = $('course').value.trim();
    const date = $('extend').value;
    if (!course || !date) { message('error', 'أدخل معرّف الدورة والتاريخ الجديد'); return; }
    if (!await ZuLms.ui.confirm(`تمديد فترة تقييم الدورة ${course} حتى ${date}؟`)) return;
    try {
      await request('PUT', `/api/v1/evaluation/period/${course}`, { newEndDate: date });
      message('success', 'مُدّدت فترة التقييم');
    } catch (e) { message('error', e.code || 'تعذّر التمديد'); }
  });

  function addOption(select, value, label) {
    const option = document.createElement('option');
    option.value = value; option.textContent = label;
    select.append(option);
  }

  async function loadAggregateLookups() {
    try {
      const [lookups, versions] = await Promise.all([
        request('GET', '/api/v1/courses/directory/lookups?view=All'),
        request('GET', '/api/v1/evaluation/versions')
      ]);
      for (const center of lookups.faculties || [])
        addOption(agg('center'), center.id, ZuLmsI18n.field(center, 'name'));
      for (const track of lookups.tracks || [])
        addOption(agg('track'), track.id, ZuLmsI18n.field(track, 'name'));
      for (const version of versions || [])
        addOption(agg('version'), version.id,
          `نسخة ${version.versionNo} — ${ZuLmsI18n.field(version, 'title')}`);
    } catch (e) {
      agg('error').textContent = e.code || 'تعذّر تحميل المرشحات';
      agg('error').classList.remove('d-none');
    }
  }

  function renderAggregate(data) {
    agg('summary').textContent = `الدورات: ${data.courses} — الاستجابات: ${data.responses}`;
    const host = agg('questions'); host.replaceChildren();
    for (const question of data.questions || []) {
      const card = document.createElement('div'); card.className = 'card card-body';
      const title = document.createElement('h3'); title.className = 'h6';
      title.textContent = `${question.textAr} — النسخ ${question.versionNumbers.join('، ')}`;
      const summary = document.createElement('p'); summary.className = 'text-muted mb-2';
      summary.textContent = `الإجابات: ${question.answers}` +
        (question.averageScore == null ? '' : ` — متوسط الدرجة: ${question.averageScore}`);
      card.append(title, summary);
      const list = document.createElement('ul');
      list.className = 'list-group list-group-flush';
      for (const answer of question.options || []) {
        const item = document.createElement('li'); item.className = 'list-group-item';
        item.textContent = `${ZuLmsI18n.field(answer, 'label')}: ${answer.count} (${answer.sharePercent}%)`;
        list.append(item);
      }
      card.append(list); host.append(card);
    }
    if (!data.questions?.length) host.textContent = 'لا توجد نتائج لهذه المرشحات';
  }

  agg('filters').addEventListener('submit', async event => {
    event.preventDefault(); agg('error').classList.add('d-none');
    const params = new URLSearchParams();
    for (const key of ['course', 'center', 'track', 'version', 'from', 'to']) {
      const value = agg(key).value;
      if (value) params.set({ course: 'courseId', center: 'centerId',
        track: 'trackId', version: 'versionId', from: 'fromDate',
        to: 'toDate' }[key], value);
    }
    try {
      renderAggregate(await request('GET', `/api/v1/evaluation/aggregate?${params}`));
    } catch (e) {
      agg('error').textContent = e.code || 'تعذّر عرض التحليل';
      agg('error').classList.remove('d-none');
    }
  });

  loadAggregateLookups();
})();
