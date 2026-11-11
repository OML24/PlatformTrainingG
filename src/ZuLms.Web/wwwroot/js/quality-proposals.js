(() => {
  const root = document.getElementById('quality-proposals-page'); if (!root) return;
  const $ = id => document.getElementById(id);
  const error = $('quality-proposals-error'), success = $('quality-proposals-success');
  let page = 1, pages = 1, selected = null, searchTimer;
  const value = id => $(id).value;
  const nullable = id => value(id).trim() || null;
  const number = id => nullable(id) === null ? null : Number(value(id));
  function showError(e) { error.textContent = e.message || 'تعذّر إكمال الطلب'; error.classList.remove('d-none'); }
  async function csrf() { return ZuLms.api.csrf(); }
  function request(path, method = 'GET', body) { return ZuLms.api.request(method, path, body); }
  function option(select, id, text) { const node = document.createElement('option'); node.value = id; node.textContent = text; select.append(node); }
  function line(name, text) {
    const dt = document.createElement('dt'); dt.className = 'col-sm-3'; dt.textContent = name;
    const dd = document.createElement('dd'); dd.className = 'col-sm-9'; dd.textContent = text ?? '—';
    $('quality-proposal-fields').append(dt, dd);
  }
  function fileLine(name, id) {
    const dt = document.createElement('dt'); dt.className = 'col-sm-3'; dt.textContent = name;
    const dd = document.createElement('dd'); dd.className = 'col-sm-9';
    const link = document.createElement('a'); link.href = `/api/v1/files/${id}`;
    link.textContent = 'تنزيل الملف'; dd.append(link);
    $('quality-proposal-fields').append(dt, dd);
  }
  function toLocal(value) {
    if (!value) return '';
    return String(value).slice(0, 16).replace('T', ' ');
  }
  async function externalAccounts() {
    const data = await request(`/api/v1/proposals/external-instructors?search=${encodeURIComponent(value('quality-external-search'))}`);
    const select = $('quality-external-account'); select.replaceChildren(); option(select, '', 'اختر المدرب');
    for (const x of data) option(select, x.userId, `${ZuLmsI18n.field(x, 'name')} (${x.loginName})`);
  }
  async function open(id) {
    selected = await request(`/api/v1/proposals/${id}`);
    const p = selected.revisions.at(-1).input;
    $('quality-proposal-title').textContent = `${selected.course.courseNo} — ${ZuLmsI18n.field(selected.course, 'title')}`;
    $('quality-proposal-fields').replaceChildren();
    line('الكلية', selected.course.centerNameAr);
    line('المحاضر', selected.instructorLoginName || p.externalNameAr || '—');
    line('الأهداف', p.objectivesAr);
    line('الساعات', String(p.totalHours));
    line('التواريخ', `${p.plannedStartDate} — ${p.plannedEndDate}`);
    line('نوع المشاركين', p.audienceType);
    if (p.externalEmail) line('بريد المدرب الخارجي', p.externalEmail);
    if (p.councilLetterFileId) fileLine('كتاب مجلس الكلية', p.councilLetterFileId);
    if (p.instructorCvFileId) fileLine('السيرة الذاتية', p.instructorCvFileId);
    const history = $('quality-proposal-revisions'); history.replaceChildren();
    for (const revision of selected.revisions) {
      const card = document.createElement('div'); card.className = 'border rounded p-2 mb-2';
      card.textContent = `النسخة ${revision.revisionNo} — ${revision.decision} — ${ZuLmsDates.display(revision.submittedAt)}${revision.decisionNotes ? ` — ${revision.decisionNotes}` : ''}`;
      history.append(card);
    }
    $('quality-evaluation-end').value = selected.suggestedEvaluationEndDate;
    $('quality-audience').value = selected.audienceType;
    $('quality-target-center').value = String(selected.targetCenterId || selected.course.proposingCenterId);
    $('quality-deadline').value = toLocal(selected.registrationDeadline);
    $('quality-max').value = selected.maxParticipants ?? '';
    $('quality-exam').checked = selected.hasExam;
    $('quality-notes').value = '';
    $('quality-external-fields').classList.toggle('d-none', selected.instructorKind === 'Internal');
    $('quality-external-decision').value = '';
    $('quality-external-account-fields').classList.add('d-none');
    $('quality-proposal-details').classList.remove('d-none');
    $('quality-proposal-details').scrollIntoView({ behavior: 'smooth' });
  }
  async function list() {
    const data = await request(`/api/v1/proposals/pending?page=${page}`);
    pages = Math.max(1, Math.ceil(data.total / data.pageSize));
    $('quality-proposals-page-number').textContent = `${page} / ${pages}`;
    $('quality-proposals-prev').disabled = page <= 1;
    $('quality-proposals-next').disabled = page >= pages;
    const body = $('quality-proposals-rows'); body.replaceChildren();
    for (const p of data.items) {
      const row = document.createElement('tr');
      for (const text of [p.courseNo, ZuLmsI18n.field(p, 'title'), ZuLmsI18n.field(p, 'centerName'), p.currentRevisionNo,
        ZuLmsDates.display(p.statusChangedAt)]) {
        const td = document.createElement('td'); td.textContent = text; row.append(td);
      }
      const td = document.createElement('td');
      const button = document.createElement('button'); button.type = 'button';
      button.className = 'btn btn-sm btn-outline-primary'; button.textContent = 'مراجعة';
      button.addEventListener('click', () => open(p.id).catch(showError));
      td.append(button); row.append(td); body.append(row);
    }
    if (!data.items.length) {
      const row = document.createElement('tr'), cell = document.createElement('td');
      cell.colSpan = 6; cell.className = 'text-center text-muted py-4';
      cell.textContent = 'لا توجد مقترحات بانتظار المراجعة';
      row.append(cell); body.append(row);
    }
  }
  async function decide(decision) {
    if (!selected) return;
    const notes = nullable('quality-notes');
    if (decision !== 'Approved' && !notes) { showError(new Error('الملاحظات إلزامية')); return; }
    const externalDecision = selected.instructorKind === 'Internal' ? null : nullable('quality-external-decision');
    if (decision === 'Approved' && selected.instructorKind !== 'Internal' && !externalDecision) {
      showError(new Error('اختر قرار حساب المدرب الخارجي')); return;
    }
    const payload = {
      decision, notes, evaluationEndDate: decision === 'Approved' ? nullable('quality-evaluation-end') : null,
      audienceType: decision === 'Approved' ? value('quality-audience') : null,
      targetCenterId: decision === 'Approved' && value('quality-audience') === 'Faculty' ? number('quality-target-center') : null,
      registrationDeadline: decision === 'Approved' && nullable('quality-deadline') ?
        value('quality-deadline') : null,
      maxParticipants: decision === 'Approved' ? number('quality-max') : null,
      hasExam: decision === 'Approved' ? $('quality-exam').checked : null,
      externalDecision: decision === 'Approved' ? externalDecision : null,
      externalInstructorUserId: decision === 'Approved' && externalDecision === 'ExistingAccount' ?
        number('quality-external-account') : null
    };
    error.classList.add('d-none');
    const buttons = [...document.querySelectorAll('[data-decision]')];
    buttons.forEach(x => { x.disabled = true; });
    try {
      await request(`/api/v1/proposals/${selected.course.id}/decision`, 'POST',
        { expectedVersion: selected.course.version, input: payload });
      success.textContent = decision === 'Approved' && externalDecision === 'RequestNewAccount'
        ? ZuLmsI18n.localize('\u062a\u0645 \u0627\u0639\u062a\u0645\u0627\u062f \u0627\u0644\u062f\u0648\u0631\u0629 \u0648\u0625\u0631\u0633\u0627\u0644 \u0637\u0644\u0628 \u0625\u0646\u0634\u0627\u0621 \u062d\u0633\u0627\u0628 \u0627\u0644\u0645\u062f\u0631\u0628 \u0644\u0644\u0623\u062f\u0645\u0646\u061b \u0633\u062a\u0628\u0642\u0649 \u0627\u0644\u062f\u0648\u0631\u0629 \u0645\u0639\u0644\u0642\u0629 \u062d\u062a\u0649 \u0625\u0646\u0634\u0627\u0621 \u0627\u0644\u062d\u0633\u0627\u0628.', 'Course approved. The account request was sent to administrators; the course remains pending until the account is created.')
        : ZuLmsI18n.localize('\u062a\u0645 \u062a\u0633\u062c\u064a\u0644 \u0642\u0631\u0627\u0631 \u0627\u0644\u0645\u0642\u062a\u0631\u062d.', 'The proposal decision was recorded.');
      success.classList.remove('d-none');
      selected = null; $('quality-proposal-details').classList.add('d-none');
      await list();
    } catch (e) { showError(e); }
    finally { buttons.forEach(x => { x.disabled = false; }); }
  }
  $('quality-audience').addEventListener('change', () => {
    $('quality-target-center').disabled = value('quality-audience') !== 'Faculty';
    $('quality-deadline').disabled = value('quality-audience') !== 'Faculty';
  });
  $('quality-external-decision').addEventListener('change', () => {
    const show = value('quality-external-decision') === 'ExistingAccount';
    $('quality-external-account-fields').classList.toggle('d-none', !show);
    if (show) externalAccounts().catch(showError);
  });
  $('quality-external-search').addEventListener('input', () => {
    clearTimeout(searchTimer); searchTimer = setTimeout(() => externalAccounts().catch(showError), 250);
  });
  document.querySelectorAll('[data-decision]').forEach(button =>
    button.addEventListener('click', () => decide(button.dataset.decision)));
  $('quality-proposals-prev').addEventListener('click', () => { page--; list().catch(showError); });
  $('quality-proposals-next').addEventListener('click', () => { page++; list().catch(showError); });
  Promise.all([list(), request('/api/v1/proposals/faculties').then(data => {
    const select = $('quality-target-center'); select.replaceChildren();
    for (const x of data) option(select, x.id, ZuLmsI18n.field(x, 'name'));
  })]).catch(showError);
})();
