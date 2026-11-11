(() => {
  const root = document.getElementById('instructor-progress-page'); if (!root) return;
  const id = Number(root.dataset.courseId);
  const error = document.getElementById('instructor-progress-error');
  const body = document.getElementById('instructor-progress-rows');
  const $ = x => document.getElementById(x);
  const L = (ar, en) => (window.ZuLmsI18n?.localize ? ZuLmsI18n.localize(ar, en) : ar);
  const fmt = (v, d = 0) => (window.ZuUi ? ZuUi.format(v, d) : String(v));
  const count = (node, v, d = 0) => { if (window.ZuUi) ZuUi.countUp($(node), v, { decimals: d }); else $(node).textContent = String(v); };
  const reasons = {
    COURSE_NOT_FINISHED: 'الدورة لم تنتهِ', PARTICIPANT_INACTIVE: 'مشارك غير فعّال',
    ATTENDANCE_INSUFFICIENT: 'الحضور غير كافٍ', EXAM_NOT_PASSED: 'الامتحان غير مجتاز',
    HISTORICAL_NOT_ELIGIBLE: 'غير مستحق في سجل الدورة السابقة',
    EVALUATION_MISSING: 'الاستبيان غير مكتمل'
  };
  let roster = [], courseTitle = '';

  // One place builds the visible cell texts, so the page and the PDF always match.
  function cells(row) {
    const p = row.progress;
    return [
      `${ZuLmsI18n.field(row, 'name')} (${row.loginName})`,
      `${p.attendedLectures}/${p.totalLectures} — المطلوب ${p.requiredLectures}`,
      p.totalLectures ? `${Math.round(p.attendedLectures * 100 / p.totalLectures)}%` : '0%',
      !p.hasExam ? '—' : p.examOutcome === 'Passed' ? 'ناجح' : p.examOutcome === 'Failed' ? 'راسب' : 'لم تُرصد',
      p.surveyCompleted ? '✓' : '✗',
      p.impactApproved ? 'معتمد (اختياري)' : 'غير معتمد/لم يُرفع (اختياري)',
      !p.isFinal ? 'مبدئي' : p.eligible ? 'مستحق' : `غير مستحق: ${p.unmetReasons.map(x => reasons[x] || x).join('، ')}`,
      row.certificateStatus === 'Issued' ? row.certificateReference : row.certificateStatus === 'Revoked' ? 'ملغاة' : 'لا توجد'
    ];
  }
  function summary(rows) {
    const n = rows.length;
    const eligible = rows.filter(r => r.progress.isFinal && r.progress.eligible).length;
    const withLectures = rows.filter(r => r.progress.totalLectures > 0);
    const att = withLectures.length ? withLectures.reduce((a, r) => a + r.progress.attendedLectures / r.progress.totalLectures, 0) / withLectures.length * 100 : 0;
    const enough = rows.filter(r => r.progress.attendedLectures >= r.progress.requiredLectures).length;
    const exam = rows.filter(r => r.progress.hasExam);
    const passed = exam.filter(r => r.progress.examOutcome === 'Passed').length;
    const survey = rows.filter(r => r.progress.surveyCompleted).length;
    const final = rows.some(r => r.progress.isFinal);
    return { n, eligible, att, enough, exam: exam.length, passed, survey, final };
  }
  function renderSummary(s) {
    count('ip-total', s.n);
    $('ip-total-note').textContent = s.exam ? L(`اجتاز الامتحان ${fmt(s.passed)} من ${fmt(s.exam)}`, `${fmt(s.passed)} of ${fmt(s.exam)} passed the exam`) : L('لا يوجد امتحان لهذه الدورة', 'No exam for this course');
    count('ip-eligible', s.eligible);
    const ep = s.n ? s.eligible / s.n * 100 : 0;
    $('ip-eligible-note').textContent = s.final ? L(`${fmt(ep, 0)}% من المشاركين`, `${fmt(ep, 0)}% of participants`) : L('النتيجة مبدئية حتى انتهاء الدورة', 'Provisional until the course ends');
    count('ip-attendance', s.att, Number.isInteger(Math.round(s.att * 10) / 10) ? 0 : 1);
    $('ip-attendance-note').textContent = L(`${fmt(s.enough)} حققوا الحد المطلوب`, `${fmt(s.enough)} met the requirement`);
    count('ip-survey', s.survey);
    $('ip-survey-note').textContent = s.n ? L(`${fmt(s.survey / s.n * 100, 0)}% من المشاركين`, `${fmt(s.survey / s.n * 100, 0)}% of participants`) : '';
    requestAnimationFrame(() => {
      $('ip-eligible-bar').style.width = `${ep}%`;
      $('ip-attendance-bar').style.width = `${Math.min(100, s.att)}%`;
    });
  }

  ZuLms.api.request('GET', `/api/v1/courses/directory/${id}?view=Instructor`)
    .then(d => {
      const c = d?.course || d;
      courseTitle = [c?.courseNo, ZuLmsI18n.field(c, 'title')].filter(Boolean).join(' — ');
      if (courseTitle) $('ip-course').textContent = courseTitle;
    }).catch(() => { $('ip-course').textContent = L(`الدورة #${id}`, `Course #${id}`); });

  ZuLms.api.request('GET', `/api/v1/courses/${id}/progress/roster`)
    .then(rows => {
      roster = rows;
      body.replaceChildren();
      for (const row of rows) {
        const tr = document.createElement('tr');
        for (const value of cells(row)) { const td = document.createElement('td'); td.textContent = value; tr.append(td); }
        body.append(tr);
      }
      renderSummary(summary(rows));
      $('ip-export').disabled = false;
    })
    .catch(e => { error.textContent = e.code || e.message || 'تعذّر تحميل القائمة';
      error.classList.remove('d-none'); });

  async function exportPdf(opts) {
    const s = summary(roster);
    const rep = await ZuPdf.create({
      title: L('استحقاق المشاركين', 'Participant eligibility'),
      subtitle: L('تقرير المدرب', 'Instructor report'),
      meta: [[L('الدورة', 'Course'), courseTitle || L(`#${id}`, `#${id}`)], [L('عدد المشاركين', 'Participants'), fmt(s.n)]],
      grayscale: opts.grayscale
    });
    if (opts.sections.has('kpis')) {
      rep.section(L('ملخص الدورة', 'Course summary'));
      rep.kpis([
        { label: L('المشاركون', 'Participants'), value: fmt(s.n), note: $('ip-total-note').textContent, color: '#022c5a' },
        { label: L('المستحقون للشهادة', 'Eligible'), value: fmt(s.eligible), note: $('ip-eligible-note').textContent, color: '#0f766e' },
        { label: L('متوسط الحضور', 'Average attendance'), value: `${fmt(s.att, 0)}%`, note: $('ip-attendance-note').textContent, color: '#c9a227' },
        { label: L('أكملوا الاستبيان', 'Survey completed'), value: fmt(s.survey), note: $('ip-survey-note').textContent, color: '#0369a1' }
      ]);
    }
    if (opts.sections.has('list')) {
      rep.section(L('قائمة المشاركين', 'Participants'));
      rep.table(['المشارك', 'الحضور', 'النسبة', 'الامتحان', 'الاستبيان', 'الأثر', 'الاستحقاق', 'الشهادة'].map(h => L(h, h)),
        roster.map(cells), [2.3, 1.6, 0.9, 1, 1.1, 1.6, 2.1, 1.4]);
    }
    await rep.save(`zu-lms-course-${id}-participants-${new Date().toISOString().slice(0, 10)}.pdf`);
  }
  $('ip-export').addEventListener('click', () => {
    if (!window.ZuPdf) return;
    ZuPdf.openDialog({
      sections: [{ key: 'kpis', label: L('ملخص الدورة', 'Course summary') }, { key: 'list', label: L('قائمة المشاركين', 'Participants') }],
      onExport: exportPdf
    });
  });
})();
