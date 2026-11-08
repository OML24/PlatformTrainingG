(() => {
  'use strict';
  const root = document.getElementById('profile-page');
  if (!root) return;
  const $ = name => document.getElementById(`profile-${name}`);
  const labels = root.dataset;
  const english = document.documentElement.lang === 'en';
  const reasonLabels = {
    COURSE_NOT_FINISHED: 'الدورة لم تنتهِ بعد',
    PARTICIPANT_INACTIVE: 'التسجيل غير فعّال',
    ATTENDANCE_INSUFFICIENT: 'لم يستوفِ نسبة الحضور المطلوبة',
    EXAM_NOT_PASSED: 'لم ينجح في الامتحان أو لم تُرصد نتيجته',
    EVALUATION_MISSING: 'تقييم الدورة غير مكتمل',
    HISTORICAL_NOT_ELIGIBLE: 'غير مستحق حسب نتيجة الدورة السابقة'
  };
  function card(title, lines, href) {
    const column = document.createElement('div'); column.className = 'col-md-6';
    const card = document.createElement('article'); card.className = 'card h-100';
    const body = document.createElement('div'); body.className = 'card-body';
    const heading = document.createElement('h3');
    heading.className = 'h6 card-title'; heading.textContent = title;
    body.append(heading);
    for (const line of lines) {
      const p = document.createElement('p');
      p.className = 'card-text small mb-1'; p.textContent = line;
      body.append(p);
    }
    if (href) {
      const url = new URL(href, location.origin);
      if (['http:', 'https:'].includes(url.protocol)) {
        const link = document.createElement('a');
        link.className = 'btn btn-sm btn-outline-primary mt-3';
        link.href = url.href; link.target = '_blank'; link.rel = 'noopener noreferrer nofollow';
        link.textContent = labels.verify; body.append(link);
      }
    }
    card.append(body); column.append(card); return column;
  }
  function empty(container) {
    if (container.childElementCount) return;
    const p = document.createElement('p'); p.className = 'empty-state'; p.textContent = labels.empty;
    container.append(p);
  }
  async function load() {
    root.setAttribute('aria-busy', 'true');
    $('loading').classList.remove('d-none');
    $('error').classList.add('d-none'); $('retry').classList.add('d-none');
    try {
      const data = await ZuLms.api.get(`/api/v1/profiles/${labels.userId}`);
      $('name').textContent = english && data.fullNameEn ? data.fullNameEn : data.fullNameAr;
      $('sub').textContent = [data.centerNameAr, data.departmentNameAr, data.academicRank].filter(Boolean).join(' — ');
      $('bio').textContent = (english && data.bioEn ? data.bioEn : data.bioAr) || '';
      $('expertise').replaceChildren();
      for (const item of data.expertiseAr || []) {
        const badge = document.createElement('span'); badge.className = 'badge text-bg-light border';
        badge.textContent = item; $('expertise').append(badge);
      }
      $('certificates').replaceChildren();
      for (const cert of data.certificates || []) {
        const date = new Intl.DateTimeFormat(english ? 'en-GB' : 'ar-JO-u-nu-latn', { timeZone: 'Asia/Amman' }).format(new Date(cert.issuedAt));
        $('certificates').append(card(cert.courseTitleAr, [cert.coursePeriodText,
          `${labels.hours}: ${cert.courseHours}`, `${labels.issued}: ${date}`, cert.referenceNo], cert.verifyUrl));
      }
      empty($('certificates'));
      $('participations').replaceChildren();
      for (const course of data.courseParticipations || []) {
        const status = course.certificateReference
          ? `مكتملة — صدرت الشهادة ${course.certificateReference}`
          : course.eligible ? 'مكتملة ومستوفية لمتطلبات الشهادة'
            : course.isFinal ? 'شارك ولم يستوفِ متطلبات الشهادة' : 'مسجل / قيد التنفيذ';
        const reasons = (course.unmetReasons || [])
          .filter(x => course.isFinal || x !== 'COURSE_NOT_FINISHED')
          .map(x => reasonLabels[x] || x);
        const lines = [`#${course.courseId} — ${course.courseNo}`, status];
        if (reasons.length) lines.push(`السبب: ${reasons.join('، ')}`);
        $('participations').append(card(course.courseTitleAr, lines,
          `/courses/${course.courseId}?view=Member`));
      }
      empty($('participations'));
      $('procerts').replaceChildren();
      $('procerts-title').textContent = `${labels.proCerts} (${data.proCertCount})`;
      for (const cert of data.proCerts || []) {
        $('procerts').append(card(cert.name, [cert.issuerName, cert.categoryNameAr,
          `${labels.issued}: ${cert.issuedDate}` + (cert.isExpired ? ` — ${labels.expired}` : cert.expiryDate ? ` — ${labels.expires}: ${cert.expiryDate}` : '')], cert.verifyUrl));
      }
      empty($('procerts'));
    } catch (e) {
      $('error').textContent = labels.error + (e.code ? ` (${e.code})` : '');
      $('error').classList.remove('d-none'); $('retry').classList.remove('d-none');
    } finally { root.setAttribute('aria-busy', 'false'); $('loading').classList.add('d-none'); }
  }
  $('retry').addEventListener('click', load);
  load();
})();
