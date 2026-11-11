(() => {
  const root = document.getElementById('course-details-page'); if (!root) return;
  const id = Number(root.dataset.courseId);
  const pathView = location.pathname.startsWith('/admin/') ? 'All'
    : location.pathname.startsWith('/quality/') ? 'Quality'
      : location.pathname.startsWith('/instructor/') ? 'Instructor'
        : location.pathname.startsWith('/member/') ? 'Member' : 'Auto';
  const view = new URLSearchParams(location.search).get('view') || pathView;
  const $ = name => document.getElementById(`course-details-${name}`);
  let currentVersion = 0;
  let instructorSearchTimer;
  const tabSections = ['overview', 'lectures', 'progress', 'evaluation', 'evaluation-results', 'impact', 'files', 'history'];
  let currentTab = 'overview';
  if (location.pathname.endsWith('/evaluation')) currentTab = 'evaluation-results';
  const statusAr = { AwaitingAccount: '\u0628\u0627\u0646\u062a\u0638\u0627\u0631 \u0625\u0646\u0634\u0627\u0621 \u062d\u0633\u0627\u0628 \u0627\u0644\u0645\u062f\u0631\u0628', Proposed: 'مقترحة', Returned: 'معادة', Rejected: 'مرفوضة',
    Approved: 'معتمدة', Finished: 'منتهية', Cancelled: 'ملغية' };
  const modeAr = { InPerson: 'وجاهي', Online: 'عن بُعد', Blended: 'مدمج' };
  const audienceAr = { General: 'عام', Faculty: 'كلية', Administrative: 'إداري', Named: 'محددون' };
  const statusStyle = { AwaitingAccount: 'pending', Proposed: 'pending', Returned: 'returned', Rejected: 'rejected',
    Approved: 'approved', Finished: 'finished', Cancelled: 'cancelled' };
  const statusLabel = status => status === 'AwaitingAccount'
    ? ZuLmsI18n.localize(statusAr[status], 'Awaiting instructor account')
    : statusAr[status] || status;
  let actionGroup = 'navigation';
  const dateOnly = value => value ? String(value).match(/^\d{4}-\d{2}-\d{2}/)?.[0] || value : '—';
  function renderTabs() {
    const available = tabSections.filter(name => !$(name === 'overview' ? 'overview' : `${name}-section`).classList.contains('d-none'));
    if (!available.includes(currentTab)) currentTab = 'overview';
    const list = $('tabs'); list.replaceChildren();
    list.classList.toggle('d-none', available.length < 2);
    for (const name of available) {
      const panel = $(name === 'overview' ? 'overview' : `${name}-section`);
      panel.hidden = name !== currentTab;
      panel.setAttribute('role', 'tabpanel');
      panel.setAttribute('tabindex', '0');
      const button = document.createElement('button');
      button.type = 'button'; button.id = `course-tab-${name}`;
      button.setAttribute('role', 'tab'); button.setAttribute('aria-controls', panel.id);
      button.setAttribute('aria-selected', String(name === currentTab));
      button.tabIndex = name === currentTab ? 0 : -1;
      button.textContent = panel.dataset.tabLabel || panel.querySelector('h2')?.textContent || name;
      panel.setAttribute('aria-labelledby', button.id);
      button.addEventListener('click', () => { currentTab = name; renderTabs(); document.getElementById(button.id)?.focus(); });
      list.append(button);
    }
  }
  $('tabs').addEventListener('keydown', event => {
    if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
    const buttons = [...$('tabs').querySelectorAll('[role=tab]')];
    const index = buttons.indexOf(document.activeElement);
    if (index < 0) return;
    event.preventDefault();
    const next = event.key === 'Home' ? 0 : event.key === 'End' ? buttons.length - 1
      : (index + (event.key === 'ArrowRight' ? 1 : -1) + buttons.length) % buttons.length;
    buttons[next].click();
  });
  function showError(e) {
    $('error').textContent = ({ COURSE_NOT_FOUND: 'الدورة غير موجودة أو غير متاحة لك.',
      FORBIDDEN: 'لا تملك صلاحية عرض هذه الدورة.' })[e.code] || e.message || 'تعذّر إكمال الطلب';
    $('error').classList.remove('d-none');
  }
  function showSuccess(text) {
    $('success').textContent = text; $('success').classList.remove('d-none');
    $('error').classList.add('d-none');
  }
  function field(label, text) {
    const item = document.createElement('div'); item.className = 'course-detail-field';
    if (['الأهداف', 'الفئة المستهدفة', 'سبب الإلغاء', 'دورة تاريخية'].includes(label))
      item.classList.add('course-detail-field-wide');
    const dt = document.createElement('dt'); dt.textContent = label;
    const dd = document.createElement('dd'); dd.textContent = text ?? '—';
    item.append(dt, dd); $('fields').append(item);
  }
  function group(name) {
    actionGroup = name;
    let node = $('actions').querySelector(`[data-action-group="${name}"]`);
    if (!node) {
      node = document.createElement('div'); node.className = 'course-action-group';
      node.dataset.actionGroup = name; $('actions').append(node);
    }
    return node;
  }
  function link(url, label) {
    const node = document.createElement('a'); node.href = url;
    node.className = 'btn btn-outline-primary btn-sm'; node.textContent = label;
    group(actionGroup).append(node);
  }
  function actionButton(label, click, danger = false) {
    const button = document.createElement('button'); button.type = 'button';
    button.className = `btn btn-sm ${danger ? 'btn-outline-danger' : 'btn-outline-secondary'}`;
    button.textContent = label;
    button.addEventListener('click', async () => {
      button.disabled = true;
      try { await click(); } catch (e) { showError(e); }
      finally { button.disabled = false; }
    });
    group(danger ? 'danger' : actionGroup).append(button);
  }
  function compactActionGroups() {
    for (const host of $('actions').querySelectorAll('[data-action-group]')) {
      if (host.children.length < 3) continue;
      const menu = document.createElement('ul'); menu.className = 'dropdown-menu';
      for (const control of [...host.children]) {
        control.classList.remove('btn', 'btn-sm', 'btn-outline-primary',
          'btn-outline-secondary', 'btn-outline-danger');
        control.classList.add('dropdown-item');
        if (host.dataset.actionGroup === 'danger') control.classList.add('text-danger');
        const item = document.createElement('li'); item.append(control); menu.append(item);
      }
      const dropdown = document.createElement('div'); dropdown.className = 'dropdown';
      const toggle = document.createElement('button'); toggle.type = 'button';
      toggle.className = 'btn btn-outline-secondary btn-sm dropdown-toggle';
      toggle.dataset.bsToggle = 'dropdown'; toggle.setAttribute('aria-expanded', 'false');
      toggle.textContent = ({ navigation: 'عرض', editing: 'إجراءات',
        danger: 'إجراءات حساسة' })[host.dataset.actionGroup] || 'إجراءات';
      dropdown.append(toggle, menu); host.append(dropdown);
    }
  }
  async function postAction(name, body, successText) {
    await ZuLms.api.request('POST', `/api/v1/courses/${id}/admin/${name}`, body);
    showSuccess(successText); await load();
  }
  async function openEdit(data) {
    const form = $('edit-form');
    const course = data.course;
    const lookups = await ZuLms.api.request('GET',
      '/api/v1/courses/directory/lookups?view=All');
    const track = form.elements.namedItem('trackId');
    const center = form.elements.namedItem('targetCenterId');
    track.replaceChildren(); center.replaceChildren();
    for (const row of lookups.tracks) {
      const option = document.createElement('option');
      option.value = row.id; option.textContent = ZuLmsI18n.field(row, 'name'); track.append(option);
    }
    if (![...track.options].some(x => Number(x.value) === data.trackId)) {
      const option = document.createElement('option');
      option.value = data.trackId; option.textContent = course.trackNameAr;
      track.append(option);
    }
    const empty = document.createElement('option'); empty.value = '';
    empty.textContent = '—'; center.append(empty);
    for (const row of lookups.faculties) {
      const option = document.createElement('option');
      option.value = row.id; option.textContent = ZuLmsI18n.field(row, 'name'); center.append(option);
    }
    if (data.targetCenterId &&
      ![...center.options].some(x => Number(x.value) === data.targetCenterId)) {
      const option = document.createElement('option');
      option.value = data.targetCenterId;
      option.textContent = data.targetCenterNameAr || String(data.targetCenterId);
      center.append(option);
    }
    currentVersion = course.version;
    const fields = {
      titleAr: course.titleAr, titleEn: course.titleEn,
      objectivesAr: data.objectivesAr, objectivesEn: data.objectivesEn,
      totalHours: data.totalHours,
      plannedStartDate: course.plannedStartDate,
      plannedEndDate: course.plannedEndDate,
      maxParticipants: data.maxParticipants,
      trackId: data.trackId, deliveryMode: data.deliveryMode,
      audienceType: data.audienceType, targetCenterId: data.targetCenterId
    };
    for (const [name, value] of Object.entries(fields))
      form.elements.namedItem(name).value = value ?? '';
    const deadline = form.elements.namedItem('registrationDeadline');
    if (data.registrationDeadline) {
      deadline.value = ZuLmsDates.input(data.registrationDeadline);
    } else deadline.value = '';
    track.disabled = !data.canAdminActions;
    form.elements.namedItem('deliveryMode').disabled = !data.canAdminActions;
    center.required = data.audienceType === 'Faculty';
    $('edit-dialog').showModal();
    ZuLmsPickers.init($('edit-dialog'));
  }
  function instructorChoice() {
    return $('instructor-form').elements.namedItem('choice').value;
  }
  function toggleInstructorFields() {
    const choice = instructorChoice();
    const account = choice === 'Internal' || choice === 'ExistingExternal';
    const named = choice === 'ExternalNameOnly';
    $('instructor-account').classList.toggle('d-none', !account);
    $('instructor-name-only').classList.toggle('d-none', !named);
    const form = $('instructor-form');
    form.elements.namedItem('userId').required = account;
    form.elements.namedItem('nameAr').required = named;
    form.elements.namedItem('organizationAr').required = named;
  }
  async function searchInstructors(selectedId = null, selectedName = null) {
    const form = $('instructor-form');
    const choice = instructorChoice();
    if (!['Internal', 'ExistingExternal'].includes(choice)) return;
    const external = choice === 'ExistingExternal';
    const search = encodeURIComponent(form.elements.namedItem('search').value.trim());
    const rows = await ZuLms.api.request('GET',
      `/api/v1/courses/${id}/instructor/search?external=${external}&search=${search}`);
    const select = form.elements.namedItem('userId'); select.replaceChildren();
    const blank = document.createElement('option');
    blank.value = ''; blank.textContent = 'اختر المحاضر'; select.append(blank);
    for (const row of rows) {
      const option = document.createElement('option');
      option.value = row.userId;
      option.textContent = `${ZuLmsI18n.field(row, 'name')} (${row.loginName})` +
        (row.empStatus == null ? '' :
          ` — ${row.empStatusDesc ? `${row.empStatusDesc} (${row.empStatus})` : `رمز ${row.empStatus}`}`);
      select.append(option);
    }
    if (selectedId && !rows.some(x => x.userId === selectedId)) {
      const option = document.createElement('option');
      option.value = selectedId; option.textContent = selectedName || String(selectedId);
      select.append(option);
    }
    if (selectedId) select.value = String(selectedId);
  }
  async function openInstructor(data) {
    currentVersion = data.course.version;
    const form = $('instructor-form');
    const choice = data.instructorKind === 'ManagedByQuality' ? 'ManagedByQuality' :
      data.managedByQuality && data.course.instructorNameAr
      ? 'ExternalNameOnly' : data.managedByQuality ? 'ManagedByQuality' :
      data.instructorKind === 'Internal' ? 'Internal' :
      data.instructorKind === 'ExternalAccount' ? 'ExistingExternal' :
      'ExternalNameOnly';
    form.elements.namedItem('choice').value = choice;
    form.elements.namedItem('search').value = '';
    for (const [name, value] of Object.entries({
      nameAr: data.course.instructorNameAr,
      nameEn: data.instructorNameEn,
      organizationAr: data.instructorOrgAr,
      organizationEn: data.instructorOrgEn
    })) form.elements.namedItem(name).value = value ?? '';
    $('instructor-current').textContent = `المحاضر الحالي: ${data.course.instructorNameAr || 'إدارة الجودة'}`;
    toggleInstructorFields();
    if (choice === 'Internal' || choice === 'ExistingExternal')
      await searchInstructors(data.instructorUserId, data.course.instructorNameAr);
    $('instructor-dialog').showModal();
  }
  async function openDeactivation() {
    const preview = await ZuLms.api.request('GET',
      `/api/v1/courses/${id}/admin/deactivation-preview`);
    currentVersion = preview.version;
    $('deactivate-reason').value = '';
    document.querySelectorAll('input[name="certificateAction"]').forEach(x => { x.checked = false; });
    const count = preview.issuedCertificates;
    $('certificates-choice').classList.toggle('d-none', count === 0);
    $('certificates-warning').textContent = count
      ? `هذه الدورة لها ${count} شهادة. ماذا تريد؟` : '';
    $('deactivate-dialog').showModal();
  }
  function adminActions(c) {
    if (c.isActive) actionButton('تعطيل', openDeactivation, true);
    else actionButton('استعادة', async () => {
      if (!await ZuLms.ui.confirm('استعادة هذه الدورة وإعادة فحص الشهادات المخفية؟')) return;
      await postAction('restore', { expectedVersion: c.version }, 'استُعيدت الدورة');
    });
    actionButton(c.isArchived ? 'إلغاء الأرشفة' : 'أرشفة', async () => {
      if (!await ZuLms.ui.confirm(c.isArchived ? 'إلغاء أرشفة هذه الدورة؟' : 'أرشفة هذه الدورة؟')) return;
      await postAction(c.isArchived ? 'unarchive' : 'archive',
        { expectedVersion: c.version }, c.isArchived ? 'أُلغيت الأرشفة' : 'أُرشفت الدورة');
    });
    if (c.status === 'Finished') actionButton('إعادة فتح', async () => {
      if (!await ZuLms.ui.confirm('إعادة فتح الدورة ستلغي جميع شهاداتها الصادرة. هل تريد المتابعة؟')) return;
      await postAction('reopen', { expectedVersion: c.version, confirmed: true },
        'أُعيد فتح الدورة');
    }, true);
  }
  async function memberLectures() {
    const rows = await ZuLms.api.request('GET', `/api/v1/courses/${id}/lectures`);
    const host = $('lectures'); host.replaceChildren();
    for (const row of rows) {
      const card = document.createElement('div'); card.className = 'card card-body mb-2';
      const title = document.createElement('h3'); title.className = 'h6';
      title.textContent = ZuLmsI18n.field(row, 'title', 'محاضرة');
      const info = document.createElement('p'); info.className = 'mb-2';
      info.textContent = `${ZuLmsDates.display(row.startsAt)} — ${row.isPresent ? 'تم تسجيل حضورك' : 'لم يُسجل حضورك'}`;
      card.append(title, info);
      const materials = await ZuLms.api.request('GET',
        `/api/v1/lectures/${row.id}/materials`);
      if (materials.length) {
        const list = document.createElement('ul');
        for (const material of materials) {
          const item = document.createElement('li');
          const link = document.createElement('a');
          link.href = `/api/v1/files/${material.fileId}`;
          link.textContent = material.title || material.name;
          item.append(link); list.append(item);
        }
        card.append(list);
      }
      if (!row.isPresent && row.codeIsActive) {
        const group = document.createElement('div'); group.className = 'd-flex gap-2';
        const input = document.createElement('input'); input.className = 'form-control';
        input.type = 'text'; input.inputMode = 'numeric'; input.pattern = '[0-9]*';
        input.maxLength = 8; input.placeholder = 'كود الحضور';
        input.setAttribute('aria-label', `كود حضور ${ZuLmsI18n.field(row, 'title', 'المحاضرة')}`);
        const button = document.createElement('button'); button.type = 'button';
        button.className = 'btn btn-primary'; button.textContent = 'تسجيل الحضور';
        button.addEventListener('click', async () => {
          button.disabled = true;
          try {
            await ZuLms.api.request('POST', `/api/v1/lectures/${row.id}/attendance/enter-code`,
              { code: input.value });
            showSuccess('سُجل حضورك'); await memberLectures();
          } catch (e) { showError(e); button.disabled = false; }
        });
        group.append(input, button); card.append(group);
      }
      host.append(card);
    }
    $('lectures-section').classList.toggle('d-none', !rows.length);
    const progress = await ZuLms.api.request('GET', `/api/v1/courses/${id}/progress`);
    const summary = $('progress'); summary.replaceChildren();
    const lines = progress.historicalPassed === null ? [
      `الحضور: ${progress.attendedLectures} من ${progress.totalLectures} — المطلوب ${progress.requiredLectures} (${progress.thresholdPercent}%)`,
      `الامتحان: ${!progress.hasExam ? 'غير مطلوب' : progress.examOutcome === 'Passed' ? 'ناجح' : progress.examOutcome === 'Failed' ? 'راسب' : 'لم تُرصد النتيجة'}`,
      `الاستبيان: ${progress.surveyCompleted ? 'مكتمل' : 'غير مكتمل'}`,
      `الأثر التدريبي (اختياري): ${progress.impactApproved ? 'معتمد' : 'غير معتمد أو لم يُرفع'}`,
      !progress.isFinal ? 'التقدم مبدئي حتى إنهاء الدورة.' : progress.eligible
        ? 'استوفيت متطلبات الشهادة.'
        : 'مسجّل في الدورة — لم تستوفِ متطلبات الشهادة.'
    ] : [
      `حالة الدورة السابقة: ${progress.historicalPassed ? 'ناجح / مستحق للشهادة' : 'لم ينجح / غير مستحق للشهادة'}`,
      `الأثر التدريبي (اختياري): ${progress.impactApproved ? 'معتمد' : 'غير معتمد أو لم يُرفع'}`,
      progress.eligible ? 'استوفيت متطلبات الشهادة.' : progress.historicalPassed
        ? 'الدورة ظاهرة في سجلك، ولم تستكمل أحد متطلبات الشهادة الإلزامية.'
        : 'الدورة ظاهرة في سجلك بحالة غير مستحق.'
    ];
    for (const line of lines) {
      const item = document.createElement('p'); item.className = 'mb-1';
      item.textContent = line; summary.append(item);
    }
    $('progress-section').classList.remove('d-none');
    await memberImpact(progress);
  }
  async function memberImpact(progress) {
    const rows = await ZuLms.api.request('GET', `/api/v1/courses/${id}/impact`);
    const host = $('impact'); host.replaceChildren();
    const latest = rows[0];
    const status = document.createElement('p');
    status.textContent = latest ? `الطلب ${latest.sequenceNo}: ${latest.status === 'Approved' ? 'معتمد' : latest.status === 'Returned' ? 'أُعيد للتعديل' : 'بانتظار المراجعة'}${latest.notes ? ` — ${latest.notes}` : ''}` : 'لم ترفع أثراً تدريبياً بعد.';
    host.append(status);
    if (progress.impactOpen && (!latest || latest.status === 'Returned')) {
      const form = document.createElement('form'); form.className = 'row g-2';
      const type = document.createElement('select'); type.className = 'form-select';
      for (const [value, label] of [['StudyPlan', 'خطة دراسية مطورة'],
        ['ExamModel', 'نموذج اختبار'], ['ClassActivity', 'نشاط صفي']]) {
        const option = document.createElement('option');
        option.value = value; option.textContent = label; type.append(option);
      }
      const file = document.createElement('input'); file.type = 'file';
      file.className = 'form-control'; file.accept = '.pdf,.docx'; file.required = true;
      const button = document.createElement('button'); button.type = 'submit';
      button.className = 'btn btn-primary'; button.textContent = 'رفع الأثر';
      form.append(type, file, button);
      form.addEventListener('submit', async event => {
        event.preventDefault(); button.disabled = true;
        try {
          const data = new FormData(); data.append('kind', 'ImpactFile');
          data.append('file', file.files[0]);
          const uploaded = await ZuLms.api.request('POST', '/api/v1/files', data);
          await ZuLms.api.request('POST', `/api/v1/courses/${id}/impact`,
            { type: type.value, fileId: uploaded.id });
          showSuccess('رُفع الأثر التدريبي'); await memberLectures();
        } catch (e) { showError(e); button.disabled = false; }
      });
      host.append(form);
    }
    $('impact-section').classList.remove('d-none');
  }
  async function memberEvaluation() {
    let form;
    try { form = await ZuLms.api.request('GET', `/api/v1/courses/${id}/evaluation`); }
    catch (e) {
      if (['EVAL_CLOSED', 'EVAL_ALREADY_SUBMITTED', 'EVAL_FORM_MISSING',
        'EVAL_FORM_EMPTY'].includes(e.code)) {
        $('evaluation-section').classList.add('d-none'); return;
      }
      throw e;
    }
    const host = $('evaluation'); host.replaceChildren();
    const intro = document.createElement('p');
    intro.textContent = `${form.courseTitleAr} — المحاضر: ${form.instructorNameAr} — آخر موعد: ${form.endDate}`;
    host.append(intro);
    const element = document.createElement('form');
    for (const question of form.questions) {
      const fieldset = document.createElement('fieldset'); fieldset.className = 'mb-3';
      const legend = document.createElement('legend'); legend.className = 'fs-6';
      legend.textContent = question.textAr; fieldset.append(legend);
      for (const option of question.options) {
        const label = document.createElement('label'); label.className = 'd-block mb-1';
        const radio = document.createElement('input');
        radio.type = 'radio'; radio.name = `question-${question.id}`;
        radio.value = String(option.id); radio.required = true;
        radio.className = 'form-check-input me-2';
        label.append(radio, document.createTextNode(ZuLmsI18n.field(option, 'label')));
        fieldset.append(label);
      }
      element.append(fieldset);
    }
    const submit = document.createElement('button'); submit.type = 'submit';
    submit.className = 'btn btn-primary'; submit.textContent = 'إرسال التقييم';
    element.append(submit);
    element.addEventListener('submit', async event => {
      event.preventDefault(); submit.disabled = true;
      const answers = form.questions.map(q => ({ questionId: q.id,
        optionId: Number(element.querySelector(`input[name="question-${q.id}"]:checked`).value) }));
      try {
        await ZuLms.api.request('POST', `/api/v1/courses/${id}/evaluation`, { answers });
        showSuccess('أُرسل تقييم الدورة');
        $('evaluation-section').classList.add('d-none');
        await memberLectures();
      } catch (e) { showError(e); submit.disabled = false; }
    });
    host.append(element); $('evaluation-section').classList.remove('d-none');
  }
  async function instructorEvaluationResults(course) {
    const section = $('evaluation-results-section');
    if (course.status !== 'Finished') { section.classList.add('d-none'); return; }
    let results;
    try {
      results = await ZuLms.api.request('GET', `/api/v1/evaluation/results/${id}`);
    } catch (e) {
      if (e.code === 'EVAL_FORM_MISSING') {
        section.classList.add('d-none'); return;
      }
      throw e;
    }
    const host = $('evaluation-results'); host.replaceChildren();
    const summary = document.createElement('p');
    summary.textContent = `المحاضر: ${results.instructorNameAr} — نسخة النموذج ${results.versionNo} — الاستجابات: ${results.responses}`;
    host.append(summary);
    for (const question of results.questions) {
      const card = document.createElement('div'); card.className = 'border rounded p-3 mb-2';
      const title = document.createElement('h3'); title.className = 'h6';
      title.textContent = question.textAr; card.append(title);
      if (question.averageScore != null) {
        const average = document.createElement('p');
        average.textContent = `متوسط الدرجة: ${question.averageScore}`;
        card.append(average);
      }
      const list = document.createElement('ul');
      for (const option of question.options) {
        const item = document.createElement('li');
        item.textContent = `${ZuLmsI18n.field(option, 'label')}: ${option.count} (${option.sharePercent}%)`;
        list.append(item);
      }
      card.append(list); host.append(card);
    }
    section.classList.remove('d-none');
  }
  async function load() {
    try {
      const data = await ZuLms.api.request('GET', `/api/v1/courses/directory/${id}?view=${view}`);
      const c = data.course;
      const awaitingAccount = c.status === 'AwaitingAccount';
      $('fields').replaceChildren(); $('actions').replaceChildren();
      actionGroup = 'navigation';
      $('history').replaceChildren(); $('files').replaceChildren();
      $('title').textContent = ZuLmsI18n.field(c, 'title');
      const meta = $('meta'); meta.replaceChildren();
      const number = document.createElement('span'); number.dir = 'ltr';
      number.textContent = `#${c.id} · ${c.courseNo || '—'}`;
      const badge = document.createElement('span');
      badge.className = `zu-status zu-status-${statusStyle[c.status] || 'rejected'}`;
      badge.textContent = statusLabel(c.status);
      meta.append(number, badge);
      field('المعرّف', `#${c.id} · ${c.courseNo || '—'}`);
      field('الحالة', statusAr[c.status] || c.status); field('المحور', ZuLmsI18n.field(c, 'trackName'));
      field('الكلية', ZuLmsI18n.field(c, 'centerName')); field('المحاضر', ZuLmsI18n.field(c, 'instructorName') ||
        (data.managedByQuality ? 'تديرها الجودة' : '—'));
      field('نمط التنفيذ', modeAr[data.deliveryMode] || data.deliveryMode);
      field('التواريخ', `${dateOnly(c.plannedStartDate)} — ${dateOnly(c.plannedEndDate)}`);
      field('الساعات', String(data.totalHours));
      field('الأهداف', ZuLmsI18n.field(data, 'objectives'));
      field('نوع المشاركين', audienceAr[data.audienceType] || data.audienceType);
      field('كلية المشاركين', data.targetCenterNameAr);
      field('نهاية التقييم', dateOnly(data.evaluationEndDate));
      if (data.registrationDeadline)
        field('آخر موعد للتسجيل', dateOnly(data.registrationDeadline));
      field('نسبة الحضور الخاصة', data.attendanceThresholdPercentOverride == null
        ? 'النسبة العامة' : `${data.attendanceThresholdPercentOverride}%`);
      field('المشاركون', String(c.participantsCount));
      if (data.cancellationReason) field('سبب الإلغاء', data.cancellationReason);
      if (c.isHistorical) field('دورة تاريخية', data.historicalNotes || 'نعم');
      if (!c.isActive) field('حالة التفعيل', 'معطّلة');
      if (c.isArchived) field('الأرشفة', 'مؤرشفة');
      if (!awaitingAccount && (view === 'All' || data.canAdminActions))
        link(`/courses/${id}/participants`, 'إدارة المشاركين');
      if (view === 'Dean' && c.status === 'Approved')
        link(`/courses/${id}/cancel-request`, 'طلب إلغاء');
      if (!awaitingAccount && !c.isHistorical && (data.canAdminActions || view === 'Instructor'))
        link(`/instructor/courses/${id}/lectures`, 'المحاضرات');
      if (!awaitingAccount && (data.canAdminActions || view === 'Instructor'))
        link(`/instructor/courses/${id}/participants`, 'استحقاق المشاركين');
      actionGroup = 'editing';
      if (data.canEditExamRequirement && ['Approved', 'Finished'].includes(c.status))
        actionButton(data.hasExam ? 'إلغاء شرط الامتحان' : 'إضافة شرط الامتحان', async () => {
          const target = !data.hasExam;
          if (!await ZuLms.ui.confirm(target
            ? 'إضافة شرط الامتحان ستعيد فحص أهلية المشاركين وقد تُلغي شهادات صادرة. متابعة؟'
            : 'إلغاء شرط الامتحان سيعطّل نتائج الامتحان النشطة للأدمن بعد إدخال السبب. متابعة؟')) return;
          const reason = target ? null : await ZuLms.ui.prompt('سبب تعطيل النتائج النشطة (إن وجدت):');
          if (!target && reason === null) return;
          await ZuLms.api.request('PUT', `/api/v1/courses/${id}/exam-requirement`,
            { hasExam: target, disableReason: reason });
          showSuccess('تغيّر شرط الامتحان'); await load();
        });
      if (data.canEditCourse && !awaitingAccount)
        actionButton('تعديل بيانات الدورة', () => openEdit(data));
      if (data.canEditInstructor && !awaitingAccount)
        actionButton('تعديل المحاضر', () => openInstructor(data));
      if (data.canEditCourse && !awaitingAccount) actionButton('نسبة الحضور', () => {
        currentVersion = c.version;
        $('threshold-percent').value = data.attendanceThresholdPercentOverride ?? '';
        $('threshold-dialog').showModal();
      });
      if (data.canExtendEvaluation && data.evaluationEndDate && !awaitingAccount)
        actionButton('نهاية التقييم', () => {
          $('eval-period-date').value = data.evaluationEndDate;
          $('eval-period-dialog').showModal();
        });
      if (data.canForceStatus) actionButton('تغيير الحالة', () => {
        currentVersion = c.version;
        const forceTarget = $('force-target');
        const approvedOption = forceTarget.querySelector('option[value=Approved]');
        if (approvedOption) approvedOption.disabled = awaitingAccount;
        forceTarget.value = awaitingAccount ? 'Returned' : c.status === 'Approved' ? 'Finished' : 'Approved';
        $('force-reason').value = '';
        $('force-confirm').checked = false;
        $('force-dialog').showModal();
      }, true);
      actionGroup = 'danger';
      if (data.canAdminActions) adminActions(c);
      compactActionGroups();
      for (const row of data.history) {
        const item = document.createElement('p'); item.className = 'border-bottom pb-2';
        const fromStatus = statusAr[row.fromStatus] || row.fromStatus || 'بداية';
        const toStatus = statusAr[row.toStatus] || row.toStatus;
        const reason = row.reason === 'Historical course' ? 'دورة تاريخية' : row.reason;
        item.textContent = `${fromStatus} ← ${toStatus} — ${ZuLmsDates.display(row.changedAt)}${reason ? ` — ${reason}` : ''}`;
        $('history').append(item);
      }
      $('history-section').classList.toggle('d-none', !data.history.length);
      for (const file of data.files) {
        const item = document.createElement('li');
        const a = document.createElement('a'); a.href = `/api/v1/files/${file.fileId}`;
        a.textContent = `${file.purpose}: ${file.name}`; item.append(a); $('files').append(item);
      }
      $('files-section').classList.toggle('d-none', !data.files.length);
      if (view === 'Member' && !awaitingAccount) { await memberLectures(); await memberEvaluation(); }
      if (awaitingAccount) { $('lectures-section').classList.add('d-none'); $('progress-section').classList.add('d-none'); $('evaluation-section').classList.add('d-none'); }
      if (view === 'Instructor' || view === 'All' || data.canAdminActions)
        await instructorEvaluationResults(c);
      renderTabs();
    } catch (e) { showError(e); }
  }
  $('deactivate-cancel').addEventListener('click', () => $('deactivate-dialog').close());
  $('edit-cancel').addEventListener('click', () => $('edit-dialog').close());
  $('edit-form').elements.namedItem('audienceType').addEventListener('change', event => {
    const faculty = event.target.value === 'Faculty';
    const center = $('edit-form').elements.namedItem('targetCenterId');
    center.required = faculty;
    if (!faculty) center.value = '';
  });
  $('force-cancel').addEventListener('click', () => $('force-dialog').close());
  $('threshold-cancel').addEventListener('click', () => $('threshold-dialog').close());
  $('eval-period-cancel').addEventListener('click', () => $('eval-period-dialog').close());
  $('instructor-cancel').addEventListener('click', () => $('instructor-dialog').close());
  $('instructor-form').elements.namedItem('choice').addEventListener('change', () => {
    toggleInstructorFields();
    searchInstructors().catch(showError);
  });
  $('instructor-form').elements.namedItem('search').addEventListener('input', () => {
    clearTimeout(instructorSearchTimer);
    instructorSearchTimer = setTimeout(() => searchInstructors().catch(showError), 250);
  });
  $('instructor-form').addEventListener('submit', async event => {
    event.preventDefault();
    const form = event.currentTarget;
    const submit = form.querySelector('button[type="submit"]');
    submit.disabled = true;
    const value = name => form.elements.namedItem(name).value.trim();
    const choice = instructorChoice();
    const account = choice === 'Internal' || choice === 'ExistingExternal';
    const named = choice === 'ExternalNameOnly';
    try {
      await ZuLms.api.request('PUT', `/api/v1/courses/${id}/instructor`, {
        expectedVersion: currentVersion, choice,
        userId: account ? Number(value('userId')) : null,
        nameAr: named ? value('nameAr') : null,
        nameEn: named ? value('nameEn') || null : null,
        organizationAr: named ? value('organizationAr') : null,
        organizationEn: named ? value('organizationEn') || null : null
      });
      $('instructor-dialog').close();
      showSuccess('حُفظ المحاضر'); await load();
    } catch (e) { showError(e); }
    finally { submit.disabled = false; }
  });
  $('threshold-form').addEventListener('submit', async event => {
    event.preventDefault();
    const submit = event.currentTarget.querySelector('button[type="submit"]');
    submit.disabled = true;
    try {
      const raw = $('threshold-percent').value.trim();
      await ZuLms.api.request('PUT', `/api/v1/courses/${id}/attendance-threshold`, {
        expectedVersion: currentVersion, percent: raw ? Number(raw) : null
      });
      $('threshold-dialog').close();
      showSuccess('حُفظت نسبة الحضور'); await load();
    } catch (e) { showError(e); }
    finally { submit.disabled = false; }
  });
  $('eval-period-form').addEventListener('submit', async event => {
    event.preventDefault();
    const submit = event.currentTarget.querySelector('button[type="submit"]');
    submit.disabled = true;
    try {
      await ZuLms.api.request('PUT', `/api/v1/evaluation/period/${id}`, {
        newEndDate: $('eval-period-date').value
      });
      $('eval-period-dialog').close();
      showSuccess('حُفظت نهاية فترة التقييم'); await load();
    } catch (e) { showError(e); }
    finally { submit.disabled = false; }
  });
  $('force-form').addEventListener('submit', async event => {
    event.preventDefault();
    const submit = event.currentTarget.querySelector('button[type="submit"]');
    submit.disabled = true;
    try {
      await ZuLms.api.request('POST', `/api/v1/courses/${id}/force-status`, {
        expectedVersion: currentVersion,
        targetStatus: $('force-target').value,
        reason: $('force-reason').value,
        confirmed: $('force-confirm').checked
      });
      $('force-dialog').close();
      showSuccess('تغيّرت حالة الدورة'); await load();
    } catch (e) { showError(e); }
    finally { submit.disabled = false; }
  });
  $('edit-form').addEventListener('submit', async event => {
    event.preventDefault();
    const form = event.currentTarget;
    const submit = form.querySelector('button[type="submit"]');
    submit.disabled = true;
    const value = name => form.elements.namedItem(name).value.trim();
    try {
      await ZuLms.api.request('PUT', `/api/v1/courses/${id}/details`, {
        expectedVersion: currentVersion,
        titleAr: value('titleAr'), titleEn: value('titleEn') || null,
        objectivesAr: value('objectivesAr'), objectivesEn: value('objectivesEn') || null,
        totalHours: Number(value('totalHours')),
        plannedStartDate: value('plannedStartDate'),
        plannedEndDate: value('plannedEndDate'),
        maxParticipants: value('maxParticipants') ? Number(value('maxParticipants')) : null,
        trackId: Number(value('trackId')), deliveryMode: value('deliveryMode'),
        audienceType: value('audienceType'),
        targetCenterId: value('targetCenterId') ? Number(value('targetCenterId')) : null,
        registrationDeadline: value('registrationDeadline')
          ? value('registrationDeadline') : null
      });
      $('edit-dialog').close();
      showSuccess('حُفظت بيانات الدورة'); await load();
    } catch (e) { showError(e); }
    finally { submit.disabled = false; }
  });
  $('deactivate-form').addEventListener('submit', async event => {
    event.preventDefault();
    const chosen = document.querySelector('input[name="certificateAction"]:checked');
    if (!$('certificates-choice').classList.contains('d-none') && !chosen) {
      showError(new Error('اختر ما سيحدث للشهادات الصادرة')); return;
    }
    const submit = event.currentTarget.querySelector('button[type="submit"]');
    submit.disabled = true;
    try {
      await postAction('deactivate', { expectedVersion: currentVersion,
        reason: $('deactivate-reason').value, certificateAction: chosen?.value || null },
      'عُطّلت الدورة');
      $('deactivate-dialog').close();
    } catch (e) { showError(e); }
    finally { submit.disabled = false; }
  });
  load();
})();
