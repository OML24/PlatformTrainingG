(() => {
  const root = document.getElementById('instructor-progress-page'); if (!root) return;
  const id = Number(root.dataset.courseId);
  const error = document.getElementById('instructor-progress-error');
  const body = document.getElementById('instructor-progress-rows');
  const reasons = {
    COURSE_NOT_FINISHED: 'الدورة لم تنتهِ', PARTICIPANT_INACTIVE: 'مشارك غير فعّال',
    ATTENDANCE_INSUFFICIENT: 'الحضور غير كافٍ', EXAM_NOT_PASSED: 'الامتحان غير مجتاز',
    HISTORICAL_NOT_ELIGIBLE: 'غير مستحق في سجل الدورة السابقة',
    EVALUATION_MISSING: 'الاستبيان غير مكتمل'
  };
  function cell(tr, value) {
    const td = document.createElement('td'); td.textContent = value;
    tr.append(td);
  }
  ZuLms.api.request('GET', `/api/v1/courses/${id}/progress/roster`)
    .then(rows => {
      body.replaceChildren();
      for (const row of rows) {
        const p = row.progress;
        const tr = document.createElement('tr');
        cell(tr, `${ZuLmsI18n.field(row, 'name')} (${row.loginName})`);
        cell(tr, `${p.attendedLectures}/${p.totalLectures} — المطلوب ${p.requiredLectures}`);
        cell(tr, p.totalLectures ? `${Math.round(p.attendedLectures * 100 / p.totalLectures)}%` : '0%');
        cell(tr, !p.hasExam ? '—' : p.examOutcome === 'Passed' ? 'ناجح' : p.examOutcome === 'Failed' ? 'راسب' : 'لم تُرصد');
        cell(tr, p.surveyCompleted ? '✓' : '✗');
        cell(tr, p.impactApproved ? 'معتمد (اختياري)' : 'غير معتمد/لم يُرفع (اختياري)');
        cell(tr, !p.isFinal ? 'مبدئي' : p.eligible ? 'مستحق' :
          `غير مستحق: ${p.unmetReasons.map(x => reasons[x] || x).join('، ')}`);
        cell(tr, row.certificateStatus === 'Issued' ? row.certificateReference :
          row.certificateStatus === 'Revoked' ? 'ملغاة' : 'لا توجد');
        body.append(tr);
      }
    })
    .catch(e => { error.textContent = e.code || e.message || 'تعذّر تحميل القائمة';
      error.classList.remove('d-none'); });
})();
