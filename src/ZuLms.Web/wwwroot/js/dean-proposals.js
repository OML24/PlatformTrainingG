(() => {
  const root = document.getElementById('dean-propose-page'); if (!root) return;
  const $ = id => document.getElementById(id);
  const error = $('proposal-error'), success = $('proposal-success');
  let page = 1, pages = 1, editing = null, searchTimer;
  let savedFiles = { letter: null, cv: null };
  const value = id => $(id).value;
  const nullable = id => value(id).trim() || null;
  const number = id => nullable(id) === null ? null : Number(value(id));
  function showError(e) {
    const fields = e.fieldErrors ? Object.values(e.fieldErrors).flat().join('، ') : '';
    error.textContent = fields || e.message || 'تعذّر إكمال الطلب'; error.classList.remove('d-none');
  }
  async function csrf() { return ZuLms.api.csrf(); }
  function request(path, method = 'GET', body) { return ZuLms.api.request(method, path, body); }
  async function upload(kind, file) {
    const form = new FormData(); form.append('kind', kind); form.append('file', file);
    try {
      const data = await ZuLms.api.upload('/api/v1/files', form);
      return data.id;
    } catch (error) {
      throw new Error(`${file.name}: ${error.code || error.message}`);
    }
  }
  function instructorKind() {
    return document.querySelector('input[name="proposal-instructor-kind"]:checked').value;
  }
  function toggleInstructor() {
    const external = instructorKind() === 'External';
    $('proposal-internal-fields').classList.toggle('d-none', external);
    $('proposal-external-fields').classList.toggle('d-none', !external);
    $('proposal-member').required = !external;
    for (const id of ['proposal-external-name-ar', 'proposal-external-org-ar',
      'proposal-external-email']) $(id).required = external;
    $('proposal-letter').required = external && !savedFiles.letter;
    $('proposal-cv').required = external && !savedFiles.cv;
  }
  function showSavedFile(anchorId, fileId) {
    const link = $(anchorId);
    link.classList.toggle('d-none', !fileId);
    if (fileId) link.href = `/api/v1/files/${fileId}`;
  }
  function option(select, id, text) { const node = document.createElement('option'); node.value = id; node.textContent = text; select.append(node); }
  async function lookups() {
    const data = await request('/api/v1/proposals/dean-lookups');
    const center = $('proposal-center'), track = $('proposal-track');
    center.replaceChildren(); track.replaceChildren();
    for (const x of data.centers) option(center, x.id, ZuLmsI18n.field(x, 'name'));
    for (const x of data.tracks) option(track, x.id, ZuLmsI18n.field(x, 'name'));
    await members();
  }
  async function members(selected) {
    const centerId = value('proposal-center');
    const select = $('proposal-member'); select.replaceChildren();
    if (!centerId) return;
    const data = await request(`/api/v1/proposals/faculty-members?centerId=${encodeURIComponent(centerId)}&search=${encodeURIComponent(value('proposal-member-search'))}`);
    option(select, '', 'اختر المحاضر');
    for (const x of data) option(select, x.userId, `${ZuLmsI18n.field(x, 'name')} (${x.loginName})`);
    if (selected) select.value = String(selected);
  }
  function input() {
    const deadline = nullable('proposal-deadline');
    const external = instructorKind() === 'External';
    return {
      titleAr: value('proposal-title-ar'), titleEn: nullable('proposal-title-en'),
      trackId: number('proposal-track'), deliveryMode: value('proposal-mode'),
      objectivesAr: value('proposal-objectives-ar'), objectivesEn: nullable('proposal-objectives-en'),
      targetAudienceAr: null, targetAudienceEn: null,
      totalHours: number('proposal-hours'), plannedStartDate: value('proposal-start'),
      plannedEndDate: value('proposal-end'), audienceType: value('proposal-audience'),
      registrationDeadline: deadline,
      maxParticipants: number('proposal-max'), hasExam: $('proposal-exam').checked,
      internalInstructorUserId: external ? null : number('proposal-member'),
      externalNameAr: external ? value('proposal-external-name-ar') : null,
      externalNameEn: external ? nullable('proposal-external-name-en') : null,
      externalOrgAr: external ? value('proposal-external-org-ar') : null,
      externalOrgEn: external ? nullable('proposal-external-org-en') : null,
      externalEmail: external ? value('proposal-external-email') : null,
      councilLetterFileId: external ? savedFiles.letter : null,
      instructorCvFileId: external ? savedFiles.cv : null
    };
  }
  function fill(data) {
    const p = data.revisions.at(-1).input;
    $('proposal-center').value = String(data.course.proposingCenterId);
    $('proposal-track').value = String(p.trackId);
    $('proposal-title-ar').value = p.titleAr;
    $('proposal-title-en').value = p.titleEn || '';
    $('proposal-mode').value = p.deliveryMode;
    $('proposal-objectives-ar').value = p.objectivesAr;
    $('proposal-objectives-en').value = p.objectivesEn || '';
    $('proposal-hours').value = p.totalHours;
    $('proposal-start').value = p.plannedStartDate;
    $('proposal-end').value = p.plannedEndDate;
    $('proposal-audience').value = p.audienceType;
    if (p.registrationDeadline) {
      $('proposal-deadline').value = String(p.registrationDeadline).slice(0, 16).replace('T', ' ');
    } else $('proposal-deadline').value = '';
    $('proposal-max').value = p.maxParticipants ?? '';
    $('proposal-exam').checked = p.hasExam;
    document.querySelector(`input[name="proposal-instructor-kind"][value="${p.internalInstructorUserId ? 'Internal' : 'External'}"]`).checked = true;
    savedFiles = { letter: p.councilLetterFileId, cv: p.instructorCvFileId };
    $('proposal-external-name-ar').value = p.externalNameAr || '';
    $('proposal-external-name-en').value = p.externalNameEn || '';
    $('proposal-external-org-ar').value = p.externalOrgAr || '';
    $('proposal-external-org-en').value = p.externalOrgEn || '';
    $('proposal-external-email').value = p.externalEmail || '';
    showSavedFile('proposal-letter-existing', savedFiles.letter);
    showSavedFile('proposal-cv-existing', savedFiles.cv);
    $('proposal-member-search').value = data.instructorLoginName || '';
    toggleInstructor();
    return p.internalInstructorUserId ? members(p.internalInstructorUserId) : Promise.resolve();
  }
  function reset() {
    editing = null; $('proposal-form').reset(); $('proposal-submit').textContent = 'إرسال المقترح';
    savedFiles = { letter: null, cv: null };
    showSavedFile('proposal-letter-existing', null);
    showSavedFile('proposal-cv-existing', null);
    toggleInstructor();
    $('proposal-cancel-edit').classList.add('d-none');
    members().catch(showError);
  }
  async function list() {
    const data = await request(`/api/v1/proposals/mine?page=${page}`);
    pages = Math.max(1, Math.ceil(data.total / data.pageSize));
    $('my-proposals-page').textContent = `${page} / ${pages}`;
    $('my-proposals-prev').disabled = page <= 1;
    $('my-proposals-next').disabled = page >= pages;
    const body = $('my-proposals'); body.replaceChildren();
    for (const p of data.items) {
      const row = document.createElement('tr');
      if (p.status === 'Returned') row.className = 'table-danger';
      const statusLabels = { AwaitingAccount: ZuLmsI18n.localize('\u0628\u0627\u0646\u062a\u0638\u0627\u0631 \u0625\u0646\u0634\u0627\u0621 \u062d\u0633\u0627\u0628 \u0627\u0644\u0645\u062f\u0631\u0628', 'Awaiting instructor account'), Proposed: 'مقترحة', Returned: 'أُعيدت للتعديل', Rejected: 'مرفوضة',
        Approved: 'معتمدة', Finished: 'منتهية', Cancelled: 'ملغية' };
      for (const text of [p.courseNo, ZuLmsI18n.field(p, 'title'), ZuLmsI18n.field(p, 'centerName'), statusLabels[p.status] || p.status,
        p.currentRevisionNo, p.lastReturnNotes || '—']) {
        const td = document.createElement('td'); td.textContent = text; row.append(td);
      }
      const td = document.createElement('td');
      if (p.status === 'Returned') {
        const button = document.createElement('button'); button.type = 'button';
        button.className = 'btn btn-sm btn-outline-primary'; button.textContent = 'تعديل وإعادة إرسال';
        button.addEventListener('click', async () => {
          try {
            const details = await request(`/api/v1/proposals/${p.id}`);
            editing = { id: p.id, version: p.version };
            await fill(details);
            $('proposal-submit').textContent = 'إعادة إرسال المقترح';
            $('proposal-cancel-edit').classList.remove('d-none');
            $('proposal-form').scrollIntoView({ behavior: 'smooth' });
          } catch (e) { showError(e); }
        });
        td.append(button);
      }
      if (p.status === 'Approved') {
        const link = document.createElement('a');
        link.className = 'btn btn-sm btn-outline-danger';
        link.href = `/courses/${p.id}/cancel-request`;
        link.textContent = 'طلب إلغاء'; td.append(link);
      }
      row.append(td); body.append(row);
    }
  }
  $('proposal-form').addEventListener('submit', async e => {
    e.preventDefault(); error.classList.add('d-none'); success.classList.add('d-none');
    const button = $('proposal-submit'); button.disabled = true;
    try {
      const payload = input();
      if (instructorKind() === 'Internal' && !payload.internalInstructorUserId)
        throw new Error('اختر محاضراً من أعضاء كليتك');
      if (instructorKind() === 'External') {
        const letter = $('proposal-letter').files[0];
        const cv = $('proposal-cv').files[0];
        if (!letter && !savedFiles.letter || !cv && !savedFiles.cv)
          throw new Error('كتاب مجلس الكلية والسيرة الذاتية إلزاميان');
        if (letter) {
          payload.councilLetterFileId = await upload('ProposalCouncilLetter', letter);
          savedFiles.letter = payload.councilLetterFileId;
          $('proposal-letter').value = '';
          showSavedFile('proposal-letter-existing', savedFiles.letter);
        }
        if (cv) {
          payload.instructorCvFileId = await upload('InstructorCv', cv);
          savedFiles.cv = payload.instructorCvFileId;
          $('proposal-cv').value = '';
          showSavedFile('proposal-cv-existing', savedFiles.cv);
        }
        toggleInstructor();
      }
      if (editing) {
        await request(`/api/v1/proposals/${editing.id}/resubmit`, 'POST',
          { expectedVersion: editing.version, input: payload });
      } else {
        await request('/api/v1/proposals', 'POST',
          { centerId: number('proposal-center'), input: payload });
      }
      success.textContent = '???? ??????? ?????? ??? ??????'; success.classList.remove('d-none');
      reset(); page = 1; await list();
    } catch (e2) { showError(e2); }
    finally { button.disabled = false; }
  });
  $('proposal-cancel-edit').addEventListener('click', reset);
  document.querySelectorAll('input[name="proposal-instructor-kind"]').forEach(radio =>
    radio.addEventListener('change', toggleInstructor));
  $('proposal-center').addEventListener('change', () => members().catch(showError));
  $('proposal-member-search').addEventListener('input', () => {
    clearTimeout(searchTimer); searchTimer = setTimeout(() => members().catch(showError), 250);
  });
  $('proposal-audience').addEventListener('change', () => {
    $('proposal-deadline').disabled = value('proposal-audience') !== 'Faculty';
  });
  $('my-proposals-prev').addEventListener('click', () => { page--; list().catch(showError); });
  $('my-proposals-next').addEventListener('click', () => { page++; list().catch(showError); });
  toggleInstructor();
  lookups().then(list).catch(showError);
})();
