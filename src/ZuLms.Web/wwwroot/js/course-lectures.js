(() => {
  const root = document.getElementById('lectures-page'); if (!root) return;
  const id = Number(root.dataset.courseId);
  const $ = name => document.getElementById(`lectures-${name}`);
  let editingId = null;
  let materialLecture = null;
  const errors = {
    FORBIDDEN: 'لا تملك صلاحية إدارة محاضرات هذه الدورة.',
    COURSE_NOT_FOUND: 'الدورة غير موجودة.',
    COURSE_NOT_APPROVED: 'لا يمكن إضافة محاضرة إلا لدورة معتمدة.',
    COURSE_FINISHED: 'لا يمكن إضافة محاضرة إلى دورة منتهية.',
    LECTURE_INVALID: 'تحقق من عنوان المحاضرة ووقت البداية والنهاية.',
    LECTURE_MODE_INVALID: 'نمط المحاضرة لا يتوافق مع نمط الدورة.',
    LECTURE_LOCATION_REQUIRED: 'مكان المحاضرة مطلوب للمحاضرة الوجاهية.',
    LECTURE_TEAMS_URL_INVALID: 'رابط Teams غير صالح؛ يجب أن يكون رابطاً كاملاً يبدأ بـ https://',
    ATT_CODE_CONFIG_INVALID: 'إعدادات كود الحضور غير صالحة؛ راجع مسؤول النظام.'
  };
  function showError(e) {
    const error = $('error');
    error.replaceChildren();
    const message = document.createElement('p');
    message.className = 'mb-0';
    message.textContent = e?.code === 'VALIDATION_FAILED'
      ? root.dataset.validationError : e?.message || errors[e?.code] || e?.code || root.dataset.error;
    error.append(message);
    let firstInvalid = null;
    if (e.fieldErrors) {
      const list = document.createElement('ul'); list.className = 'mb-0 mt-2';
      for (const [key, messages] of Object.entries(e.fieldErrors)) {
        const name = key.split('.').at(-1).toLowerCase();
        const field = [...$('form').querySelectorAll('[name]')].find(input => input.name.toLowerCase() === name);
        const label = field?.labels?.[0]?.textContent || key;
        if (field) { field.classList.add('is-invalid'); firstInvalid ||= field; }
        for (const text of Array.isArray(messages) ? messages : [messages]) {
          const item = document.createElement('li'); item.textContent = `${label}: ${text}`;
          list.append(item);
        }
      }
      error.append(list);
    }
    error.classList.remove('d-none');
    firstInvalid?.focus();
  }
  function clearFieldErrors() {
    $('form').querySelectorAll('.is-invalid').forEach(field => field.classList.remove('is-invalid'));
    $('form').querySelectorAll('.field-error').forEach(message => message.remove());
  }
  function endEdit() {
    editingId = null; $('form').reset(); clearFieldErrors(); $('title').readOnly = false;
    $('mode').disabled = false; $('notes').readOnly = false;
    $('form-title').textContent = 'إضافة محاضرة';
    $('submit').textContent = 'إضافة المحاضرة';
    $('cancel-edit').classList.add('d-none'); toggleMode();
  }
  function startEdit(row) {
    clearFieldErrors();
    $('error').classList.add('d-none');
    editingId = row.id;
    $('title').value = row.titleAr || ''; $('title').readOnly = true;
    $('mode').value = row.mode; $('mode').disabled = true;
    $('start').value = ZuLmsDates.input(row.startsAt);
    $('end').value = ZuLmsDates.input(row.endsAt);
    $('location').value = row.locationAr || '';
    $('teams').value = row.teamsUrl || '';
    $('notes').value = ''; $('notes').readOnly = true;
    $('form-title').textContent = 'تعديل موعد المحاضرة ومكانها';
    $('submit').textContent = 'حفظ التعديل';
    $('cancel-edit').classList.remove('d-none'); toggleMode();
    $('form').scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
  async function showRoster(lecture) {
    $('error').classList.add('d-none');
    const rows = await ZuLms.api.request('GET', `/api/v1/lectures/${lecture.id}/attendance/roster`);
    $('roster-title').textContent = `كشف الحضور: ${ZuLmsI18n.field(lecture, 'title', 'محاضرة')}`;
    const body = $('roster-rows'); body.replaceChildren();
    for (const row of rows) {
      const tr = document.createElement('tr');
      for (const value of [ZuLmsI18n.field(row, 'name'), row.loginName]) {
        const td = document.createElement('td'); td.textContent = value || '—'; tr.append(td);
      }
      const td = document.createElement('td');
      const checkbox = document.createElement('input');
      checkbox.type = 'checkbox'; checkbox.className = 'form-check-input';
      checkbox.checked = row.isPresent;
      checkbox.setAttribute('aria-label', `حضور ${ZuLmsI18n.field(row, 'name')}`);
      checkbox.addEventListener('change', async () => {
        const requested = checkbox.checked; checkbox.disabled = true;
        $('error').classList.add('d-none');
        try {
          await ZuLms.api.request('PUT', `/api/v1/lectures/${lecture.id}/attendance/roster/${row.participantId}`, {
            isPresent: requested, reason: $('roster-reason').value.trim() || null
          });
          $('success').textContent = 'حُفظ تعديل الحضور'; $('success').classList.remove('d-none');
        } catch (e) { checkbox.checked = !requested; showError(e); }
        finally { checkbox.disabled = false; }
      });
      td.append(checkbox); tr.append(td); body.append(tr);
    }
    $('roster-section').classList.remove('d-none');
  }
  async function showMaterials(lecture) {
    materialLecture = lecture;
    const rows = await ZuLms.api.request('GET', `/api/v1/lectures/${lecture.id}/materials`);
    $('materials-title').textContent = `مواد المحاضرة: ${ZuLmsI18n.field(lecture, 'title', 'محاضرة')}`;
    const list = $('materials-list'); list.replaceChildren();
    for (const row of rows) {
      const item = document.createElement('li');
      const link = document.createElement('a');
      link.href = `/api/v1/files/${row.fileId}`;
      link.textContent = row.title || row.name;
      item.append(link); list.append(item);
    }
    $('materials-section').classList.remove('d-none');
  }
  async function loadExam() {
    let payload;
    try { payload = await ZuLms.api.request('GET', `/api/v1/courses/${id}/exam`); }
    catch (e) {
      if (e.code === 'EXAM_NOT_ALLOWED' || e.code === 'FORBIDDEN') {
        $('exam-section').classList.add('d-none'); return;
      }
      throw e;
    }
    const exam = payload.exam;
    $('exam-form').classList.toggle('d-none', !!exam);
    $('exam-empty').classList.toggle('d-none', !!exam);
    $('exam-roster').classList.toggle('d-none', !exam);
    if (!exam) return;
    const body = $('exam-rows'); body.replaceChildren();
    for (const row of exam.participants) {
      const tr = document.createElement('tr');
      for (const value of [ZuLmsI18n.field(row, 'name'), row.loginName]) {
        const td = document.createElement('td'); td.textContent = value || '—'; tr.append(td);
      }
      const td = document.createElement('td');
      const select = document.createElement('select'); select.className = 'form-select';
      for (const [value, label] of [['', 'لم تُرصد النتيجة'], ['Passed', 'ناجح'], ['Failed', 'راسب']]) {
        const option = document.createElement('option'); option.value = value;
        option.textContent = label; select.append(option);
      }
      select.value = row.outcome || ''; td.append(select); tr.append(td);
      const action = document.createElement('td');
      const save = document.createElement('button'); save.type = 'button';
      save.className = 'btn btn-sm btn-outline-primary'; save.textContent = 'حفظ';
      save.addEventListener('click', async () => {
        if (!select.value) { showError({ code: 'اختر ناجح أو راسب صراحةً' }); return; }
        save.disabled = true; $('error').classList.add('d-none');
        try {
          await ZuLms.api.request('PUT', `/api/v1/courses/${id}/exam/participants/${row.participantId}`,
            { outcome: select.value });
          row.outcome = select.value;
          $('success').textContent = 'حُفظت نتيجة الامتحان'; $('success').classList.remove('d-none');
        } catch (e) { select.value = row.outcome || ''; showError(e); }
        finally { save.disabled = false; }
      });
      action.append(save); tr.append(action); body.append(tr);
    }
  }
  function toggleMode() {
    const online = $('mode').value === 'Online';
    $('location-wrap').classList.toggle('d-none', online);
    $('teams-wrap').classList.toggle('d-none', !online);
    $('location').required = !online; $('teams').required = online;
  }
  async function load() {
    const details = await ZuLms.api.request('GET', `/api/v1/courses/directory/${id}?view=Auto`);
    const finished = details.course.status === 'Finished';
    $('form').classList.toggle('d-none', finished);
    $('finish').classList.toggle('d-none', finished);
    const rows = await ZuLms.api.request('GET', `/api/v1/courses/${id}/lectures`);
    const body = $('rows'); body.replaceChildren();
    for (const row of rows) {
      const tr = document.createElement('tr');
      for (const text of [ZuLmsI18n.field(row, 'title', '—'),
         ({ Online: 'عن بُعد', InPerson: 'وجاهي', Blended: 'مدمج' })[row.mode] || row.mode,
         ZuLmsDates.display(row.startsAt),
         ZuLmsDates.display(row.endsAt),
        row.mode === 'Online' ? row.teamsUrl : row.locationAr]) {
        const td = document.createElement('td'); td.textContent = text || '—'; tr.append(td);
      }
      const cell = document.createElement('td');
      const button = document.createElement('button'); button.type = 'button';
      button.className = 'btn btn-sm btn-outline-primary';
      button.textContent = row.codeIsActive ? 'الكود مفعّل' : 'تفعيل كود الحضور';
      button.disabled = row.codeIsActive;
      button.addEventListener('click', async () => {
        button.disabled = true;
        try {
          const data = await ZuLms.api.request('POST', `/api/v1/lectures/${row.id}/activate-code`);
           $('code').textContent = `كود الحضور: ${data.code} — صالح حتى ${ZuLmsDates.display(data.expiresAt)}`;
          $('code').classList.remove('d-none'); await load();
        } catch (e) { showError(e); button.disabled = false; }
      });
      cell.append(button);
      if (!finished) {
        const edit = document.createElement('button');
        edit.type = 'button'; edit.className = 'btn btn-sm btn-outline-secondary ms-2';
        edit.textContent = 'تعديل الموعد/المكان';
        edit.addEventListener('click', () => startEdit(row));
        cell.append(edit);
      }
      if (!finished || details.canAdminActions) {
        const deactivate = document.createElement('button');
        deactivate.type = 'button';
        deactivate.className = 'btn btn-sm btn-outline-danger ms-2';
        deactivate.textContent = 'تعطيل';
        deactivate.addEventListener('click', async () => {
          if (!await ZuLms.ui.confirm('تعطيل هذه المحاضرة وإعادة حساب الحضور عند اللزوم؟')) return;
          const reason = await ZuLms.ui.prompt('سبب التعطيل (اختياري):');
          if (reason === null) return;
          deactivate.disabled = true;
          try {
            await ZuLms.api.request('POST', `/api/v1/lectures/${row.id}/deactivate`,
              { reason: reason.trim() || null });
            $('success').textContent = 'عُطّلت المحاضرة';
            $('success').classList.remove('d-none');
            $('roster-section').classList.add('d-none');
            await load();
          } catch (e) { showError(e); deactivate.disabled = false; }
        });
        cell.append(deactivate);
      }
      const rosterButton = document.createElement('button');
      rosterButton.type = 'button'; rosterButton.className = 'btn btn-sm btn-outline-secondary ms-2';
      rosterButton.textContent = 'كشف الحضور';
      rosterButton.addEventListener('click', async () => {
        rosterButton.disabled = true;
        try { await showRoster(row); } catch (e) { showError(e); }
        finally { rosterButton.disabled = false; }
      });
      cell.append(rosterButton); tr.append(cell); body.append(tr);
      const materialButton = document.createElement('button');
      materialButton.type = 'button';
      materialButton.className = 'btn btn-sm btn-outline-secondary ms-2';
      materialButton.textContent = 'المواد';
      materialButton.addEventListener('click', () =>
        showMaterials(row).catch(showError));
      cell.append(materialButton);
    }
  }
  $('mode').addEventListener('change', toggleMode);
  $('form').addEventListener('input', event => {
    event.target.classList.remove('is-invalid');
    event.target.closest('.col-md-3, .col-md-4, .col-md-5, .col-md-6, .col-12')
      ?.querySelectorAll('.field-error').forEach(message => message.remove());
  });
  $('cancel-edit').addEventListener('click', endEdit);
  $('materials-form').addEventListener('submit', async event => {
    event.preventDefault(); if (!materialLecture) return;
    const button = event.currentTarget.querySelector('button');
    button.disabled = true; $('error').classList.add('d-none');
    try {
      const form = new FormData(); form.append('kind', 'TrainingMaterial');
      form.append('file', $('materials-file').files[0]);
      const file = await ZuLms.api.request('POST', '/api/v1/files', form);
      await ZuLms.api.request('POST', `/api/v1/lectures/${materialLecture.id}/materials`,
        { fileId: file.id, title: $('materials-label').value.trim() || null });
      $('success').textContent = 'رُفعت المادة وربطت بالمحاضرة';
      $('success').classList.remove('d-none');
      $('materials-form').reset(); await showMaterials(materialLecture);
    } catch (e) { showError(e); }
    finally { button.disabled = false; }
  });
  $('finish').addEventListener('click', async () => {
    const count = $('rows').childElementCount;
    if (!await ZuLms.ui.confirm(`إنهاء الدورة مع ${count} محاضرة فعّالة؟ لن تتمكن من إضافة محاضرات بعد ذلك.`)) return;
    $('finish').disabled = true;
    try {
      const result = await ZuLms.api.request('POST', `/api/v1/courses/${id}/finish`);
      $('success').textContent = `انتهت الدورة، وثُبّت عدد المحاضرات: ${result.finalLecturesCount}`;
      $('success').classList.remove('d-none'); $('form').classList.add('d-none');
    } catch (e) { showError(e); $('finish').disabled = false; }
  });
  $('exam-form').addEventListener('submit', async event => {
    event.preventDefault(); $('error').classList.add('d-none');
    const button = event.currentTarget.querySelector('button'); button.disabled = true;
    try {
      await ZuLms.api.request('POST', `/api/v1/courses/${id}/exam`,
        { nameAr: $('exam-name').value.trim() });
      await loadExam();
    } catch (e) { showError(e); }
    finally { button.disabled = false; }
  });
  $('form').addEventListener('submit', async event => {
    event.preventDefault(); $('error').classList.add('d-none'); $('success').classList.add('d-none');
    clearFieldErrors(); $('submit').disabled = true;
    try {
      if ($('mode').value === 'Online' && !/^https:\/\//i.test($('teams').value.trim())) {
        $('teams').classList.add('is-invalid');
        $('teams').focus();
        showError({ code: 'LECTURE_TEAMS_URL_INVALID' });
        return;
      }
      const schedule = {
        startsAt: ZuLmsDates.localDateTime($('start').value),
        endsAt: ZuLmsDates.localDateTime($('end').value),
        locationAr: $('mode').value === 'InPerson' ? $('location').value : null,
        locationEn: null, teamsUrl: $('mode').value === 'Online' ? $('teams').value : null
      };
      if (editingId) {
        await ZuLms.api.request('PUT', `/api/v1/lectures/${editingId}`, schedule);
        $('success').textContent = 'حُفظ تعديل المحاضرة';
      } else {
        await ZuLms.api.request('POST', `/api/v1/courses/${id}/lectures`, {
          titleAr: $('title').value.trim() || null, titleEn: null,
          mode: $('mode').value, ...schedule,
          notesAr: $('notes').value.trim() || null
        });
        $('success').textContent = 'أُضيفت المحاضرة';
      }
      $('success').classList.remove('d-none'); endEdit(); await load();
    } catch (e) { showError(e); }
    finally { $('submit').disabled = false; }
  });
  toggleMode(); load().catch(showError);
  loadExam().then(() => {
    if (location.pathname.endsWith('/exam')) $('exam-section').scrollIntoView({ block: 'start' });
  }).catch(showError);
})();
