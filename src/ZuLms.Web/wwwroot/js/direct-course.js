(() => {
  const root = document.getElementById('direct-course-page'); if (!root) return;
  const $ = id => document.getElementById(`${['form', 'error', 'success'].includes(id) ? 'direct' : 'd'}-${id}`);
  const value = id => $(id).value;
  const nullable = id => value(id).trim() || null;
  const number = id => nullable(id) === null ? null : Number(value(id));
  const request = (path, method = 'GET', body) => ZuLms.api.request(method, path, body);
  const error = $('error'), success = $('success');
  const historicalOnly = root.dataset.historicalOnly === 'true';
  let searchTimer;
  const historicalParticipants = new Map();
  function historicalEvaluationRequired() {
    return historicalOnly && $('historical-evaluation-required').checked;
  }
  function toggleHistoricalEvaluation() {
    if (!historicalOnly) return;
    const required = historicalEvaluationRequired();
    $('evaluation-wrap').classList.toggle('d-none', !required);
    $('evaluation').required = required;
    if (!required) $('evaluation').value = '';
  }
  function showError(e) { error.textContent = e.code || e.message || 'تعذّر إكمال الطلب'; error.classList.remove('d-none'); }
  function option(select, id, label) { const item = document.createElement('option'); item.value = id; item.textContent = label; select.append(item); }
  function toggle() {
    const choice = value('choice'), existing = choice === 'Internal' || choice === 'ExistingExternal';
    $('existing-wrap').classList.toggle('d-none', !existing);
    $('external-wrap').classList.toggle('d-none', existing);
    $('email-wrap').classList.toggle('d-none', choice !== 'NewExternalAccount');
    $('user').required = existing;
    $('name-ar').required = !existing; $('org-ar').required = !existing;
    $('email').required = choice === 'NewExternalAccount';
    if (existing) search().catch(showError);
  }
  async function search() {
    const external = value('choice') === 'ExistingExternal';
    const rows = await request(`/api/v1/courses/direct/instructors?external=${external}&search=${encodeURIComponent(value('search'))}`);
    const select = $('user'); select.replaceChildren(); option(select, '', 'اختر المحاضر');
    for (const row of rows) {
      const status = row.empStatus == null ? '' :
        ` — ${row.empStatusDesc ? `${row.empStatusDesc} (${row.empStatus})` : `رمز ${row.empStatus}`}`;
      option(select, row.userId, `${ZuLmsI18n.field(row, 'name')} (${row.loginName})${status}`);
    }
  }
  async function lookups() {
    const data = await request('/api/v1/courses/direct/lookups');
    const related = $('related'), target = $('target-center'), track = $('track');
    related.replaceChildren(); target.replaceChildren(); track.replaceChildren();
    option(related, '', 'بدون كلية'); option(target, '', 'اختر الكلية');
    for (const row of data.faculties) {
      if (Number(row.id) > 0) option(related, row.id, ZuLmsI18n.field(row, 'name'));
      option(target, row.id, ZuLmsI18n.field(row, 'name'));
    }
    for (const row of data.tracks) option(track, row.id, ZuLmsI18n.field(row, 'name'));
    if (historicalOnly && data.canCreateHistorical) {
      $('historical-notes').required = true;
      $('audience').value = 'Faculty';
      $('audience').disabled = true;
      $('target-center-wrap').classList.remove('d-none');
      $('target-center').required = true;
    }
  }
  async function loadHistoricalEmployees() {
    if (!historicalOnly) return;
    const centerId = number('target-center'), body = $('historical-rows');
    body.replaceChildren(); historicalParticipants.clear();
    if (centerId === null) {
      const tr = document.createElement('tr'); const td = document.createElement('td');
      td.colSpan = 4; td.className = 'text-muted'; td.textContent = 'اختر تصنيف المشاركين.';
      tr.append(td); body.append(tr); return;
    }
    const rows = await request(`/api/v1/courses/direct/historical-employees?centerId=${centerId}`);
    if (!rows.length) {
      const tr = document.createElement('tr'); const td = document.createElement('td');
      td.colSpan = 4; td.className = 'text-muted'; td.textContent = 'لا يوجد أعضاء فعّالون في هذا التصنيف.';
      tr.append(td); body.append(tr); return;
    }
    for (const row of rows) {
      const tr = document.createElement('tr');
      const pickCell = document.createElement('td'), pick = document.createElement('input');
      pick.type = 'checkbox'; pick.className = 'form-check-input'; pickCell.append(pick);
      const name = document.createElement('td');
      name.textContent = ZuLmsI18n.field(row, 'name') +
        (row.empStatus == null ? '' :
          ` — ${row.empStatusDesc ? `${row.empStatusDesc} (${row.empStatus})` : `رمز ${row.empStatus}`}`);
      const login = document.createElement('td'); login.textContent = row.loginName;
      const statusCell = document.createElement('td'), status = document.createElement('select');
      status.className = 'form-select form-select-sm'; status.disabled = true;
      status.append(new Option('ناجح / مستحق', 'true'),
        new Option('لم ينجح / غير مستحق', 'false'));
      statusCell.append(status);
      const sync = () => {
        status.disabled = !pick.checked;
        if (pick.checked) historicalParticipants.set(row.userId,
          { userId: row.userId, passed: status.value === 'true' });
        else historicalParticipants.delete(row.userId);
      };
      pick.addEventListener('change', sync); status.addEventListener('change', sync);
      tr.append(pickCell, name, login, statusCell); body.append(tr);
    }
  }
  async function upload(kind, file) {
    if (!file) return null;
    const form = new FormData(); form.append('kind', kind); form.append('file', file);
    const result = await ZuLms.api.upload('/api/v1/files', form);
    return result.id;
  }
  function input(letter, cv) {
    const choice = value('choice'), existing = choice === 'Internal' || choice === 'ExistingExternal';
    const deadline = nullable('deadline');
    const selectedAudience = historicalOnly && number('target-center') === 0
      ? 'Administrative' : value('audience');
    return {
      titleAr: value('title-ar'), titleEn: nullable('title-en'), trackId: number('track'),
      deliveryMode: value('mode'), objectivesAr: value('objectives-ar'),
      objectivesEn: nullable('objectives-en'), targetAudienceAr: value('target-ar'),
      targetAudienceEn: nullable('target-en'), totalHours: number('hours'),
       plannedStartDate: value('start'), plannedEndDate: value('end'),
       audienceType: selectedAudience,
       targetCenterId: selectedAudience === 'Faculty' ? number('target-center') : null,
       relatedCenterId: number('related'), registrationDeadline: deadline,
      maxParticipants: number('max'), hasExam: historicalOnly ? false : $('exam').checked,
      evaluationEndDate: historicalEvaluationRequired() || !historicalOnly
        ? value('evaluation') : null,
      historicalEvaluationRequired: historicalEvaluationRequired(),
      instructorChoice: choice,
      instructorUserId: existing ? number('user') : null,
      externalNameAr: existing ? null : value('name-ar'),
      externalNameEn: existing ? null : nullable('name-en'),
      externalOrgAr: existing ? null : value('org-ar'),
      externalOrgEn: existing ? null : nullable('org-en'),
      externalEmail: choice === 'NewExternalAccount' ? value('email') : null,
      councilLetterFileId: existing ? null : letter,
      instructorCvFileId: existing ? null : cv,
       isHistorical: historicalOnly,
       historicalNotes: historicalOnly ? nullable('historical-notes') : null,
       impactEndDate: null,
       historicalParticipants: historicalOnly ? [...historicalParticipants.values()] : null
    };
  }
  $('choice').addEventListener('change', toggle);
  if (historicalOnly)
    $('historical-evaluation-required').addEventListener('change', toggleHistoricalEvaluation);
  $('search').addEventListener('input', () => { clearTimeout(searchTimer); searchTimer = setTimeout(() => search().catch(showError), 250); });
  $('audience').addEventListener('change', () => {
    const faculty = value('audience') === 'Faculty';
    $('target-center-wrap').classList.toggle('d-none', !faculty);
    $('target-center').required = faculty;
  });
  $('target-center').addEventListener('change', () => loadHistoricalEmployees().catch(showError));
  $('form').addEventListener('submit', async event => {
    event.preventDefault(); error.classList.add('d-none'); success.classList.add('d-none');
    $('submit').disabled = true;
    try {
      if (historicalOnly && !historicalParticipants.size)
        throw { code: 'اختر عضواً واحداً على الأقل وحدد حالته' };
      if (historicalOnly) {
        const end = value('end');
        const today = new Date(); today.setHours(0, 0, 0, 0);
        const parsed = new Date(`${end}T00:00:00`);
        if (!end || Number.isNaN(parsed.valueOf()) || parsed >= today)
          throw { code: 'يجب أن يكون تاريخ انتهاء الدورة السابقة قبل تاريخ اليوم' };
        if (historicalEvaluationRequired() && !nullable('evaluation'))
          throw { code: 'حدد آخر موعد للتقييم المطلوب' };
      }
      const choice = value('choice'), existing = choice === 'Internal' || choice === 'ExistingExternal';
      const letter = existing ? null : await upload('ProposalCouncilLetter', $('letter').files[0]);
      const cv = existing ? null : await upload('InstructorCv', $('cv').files[0]);
      const result = await request('/api/v1/courses/direct', 'POST', input(letter, cv));
      success.replaceChildren();
      success.append(`اعتُمدت الدورة بنجاح (المعرّف: ${result.id}). `);
      const link = document.createElement('a');
      link.href = `/courses/${result.id}/participants`;
      link.textContent = 'إدارة المشاركين';
      success.append(link);
      success.classList.remove('d-none');
      $('form').reset(); toggle();
      if (historicalOnly) {
        toggleHistoricalEvaluation();
        $('audience').value = 'Faculty';
        historicalParticipants.clear();
        await loadHistoricalEmployees();
      }
      window.scrollTo({ top: 0, behavior: 'smooth' });
    } catch (e) { showError(e); }
    finally { $('submit').disabled = false; }
  });
  toggle(); toggleHistoricalEvaluation(); lookups().catch(showError);
})();
