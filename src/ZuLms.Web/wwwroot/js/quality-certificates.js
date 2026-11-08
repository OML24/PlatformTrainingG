(() => {
  const root = document.getElementById('quality-certificates-page'); if (!root) return;
  const $ = name => document.getElementById(`certs-${name}`);
  const request = (method, path, body) => ZuLms.api.request(method, path, body);
  let page = 1;

  function message(kind, text) {
    $('error').classList.add('d-none'); $('success').classList.add('d-none');
    $(kind).textContent = text; $(kind).classList.remove('d-none');
  }

  async function load() {
    const params = new URLSearchParams({ page: String(page) });
    const search = $('search').value.trim(); if (search) params.set('search', search);
    const course = $('course').value.trim(); if (course) params.set('courseId', course);
    const status = $('status').value; if (status) params.set('status', status);
    try {
      const data = await request('GET', `/api/v1/certificates?${params}`);
      const body = $('rows'); body.replaceChildren();
      for (const row of data.items || []) {
        const tr = document.createElement('tr');
        const ref = document.createElement('td'); ref.dir = 'ltr'; ref.textContent = row.referenceNo;
        const courseCell = document.createElement('td');
        courseCell.textContent = `#${row.courseId} — ${row.courseNo} — ${row.courseTitleAr}`;
        const holder = document.createElement('td'); holder.textContent = row.holderNameAr;
        const status = document.createElement('td');
        status.textContent = row.status === 'Issued' ? 'سارية' : 'ملغاة';
        status.className = row.status === 'Issued' ? 'text-success' : 'text-danger';
        const issued = document.createElement('td');
        issued.textContent = new Date(row.issuedAt).toLocaleDateString('ar');
        const actions = document.createElement('td');
        if (row.status === 'Issued') {
          const revoke = document.createElement('button');
          revoke.type = 'button'; revoke.className = 'btn btn-sm btn-outline-danger';
          revoke.textContent = 'إلغاء';
          revoke.addEventListener('click', async () => {
            const reason = await ZuLms.ui.prompt('سبب إلغاء الشهادة (إلزامي):');
            if (!reason || !reason.trim()) return;
            revoke.disabled = true;
            try {
              await request('POST', `/api/v1/certificates/${row.id}/revoke`, { reason: reason.trim() });
              message('success', 'أُلغيت الشهادة'); await load();
            } catch (e) { message('error', e.code || 'تعذّر الإلغاء'); }
            finally { revoke.disabled = false; }
          });
          actions.append(revoke);
        } else actions.textContent = '—';
        tr.append(ref, courseCell, holder, status, issued, actions);
        body.append(tr);
      }
      const pager = $('pager'); pager.replaceChildren();
      const total = data.total || 0, size = data.pageSize || 20;
      const pages = Math.max(1, Math.ceil(total / size));
      for (let i = 1; i <= pages; i++) {
        const li = document.createElement('li');
        li.className = `page-item ${i === page ? 'active' : ''}`;
        const link = document.createElement('a');
        link.className = 'page-link'; link.href = '#'; link.textContent = i;
        link.addEventListener('click', event => {
          event.preventDefault(); page = i; load();
        });
        li.append(link); pager.append(li);
      }
    } catch (e) { message('error', e.code || 'تعذّر تحميل الشهادات'); }
  }

  $('filters').addEventListener('submit', event => { event.preventDefault(); page = 1; load(); });

  if ($('force-issue')) $('force-issue').addEventListener('click', async () => {
    const courseId = Number($('force-course').value), employeeCode = Number($('force-user').value);
    if (!courseId || !employeeCode) { message('error', 'أدخل معرّف الدورة والرقم الوظيفي الجامعي'); return; }
    const evaluationExempt = $('force-evaluation').value === 'exempt';
    const wording = evaluationExempt ? 'مع إعفائه من التقييم' : 'مع اشتراط أن يكون قد قيّم الدورة';
    if (!await ZuLms.ui.confirm(`سيتم تسجيل صاحب الرقم الوظيفي ${employeeCode} في الدورة ${courseId} واستيفاء الحضور والامتحان والأثر ${wording}. متابعة؟`)) return;
    $('force-issue').disabled = true;
    try {
      const data = await request('POST', '/api/v1/certificates/force-issue',
        { courseId, employeeCode, evaluationExempt });
      message('success', data.issued
        ? `أُصدرت الشهادة الاستثنائية رقم ${data.referenceNo}`
        : 'سُجّل العضو كمستوفٍ للحضور والامتحان. ستصدر الشهادة تلقائياً بعد إكمال تقييم الدورة. قياس الأثر اختياري.');
      await load();
    } catch (e) {
      const errors = {
        USER_NOT_FOUND: 'لم يوجد موظف فعّال بهذا الرقم الوظيفي الجامعي.',
        EMPLOYEE_CODE_AMBIGUOUS: 'الرقم الوظيفي مرتبط بأكثر من سجل؛ راجع بيانات الموظفين.'
      };
      message('error', errors[e.code] || e.code || 'تعذّر الإصدار الاستثنائي');
    } finally { $('force-issue').disabled = false; }
  });

  $('issue-all').addEventListener('click', async () => {
    if (!await ZuLms.ui.confirm('إصدار الشهادات لكل المستحقين بلا شهادة في جميع الدورات؟')) return;
    try {
      const data = await request('POST', '/api/v1/certificates/issue-pending');
      message('success', `أُصدرت ${data.issued} شهادة`);
      await load();
    } catch (e) { message('error', e.code || 'تعذّر الإصدار'); }
  });

  $('issue-course').addEventListener('click', async () => {
    const course = $('course').value.trim();
    if (!course) { message('error', 'أدخل معرّف الدورة أولاً'); return; }
    if (!await ZuLms.ui.confirm(`إصدار الشهادات المعلّقة للدورة ${course}؟`)) return;
    try {
      const data = await request('POST', `/api/v1/certificates/issue-pending?courseId=${encodeURIComponent(course)}`);
      message('success', `أُصدرت ${data.issued} شهادة`);
      await load();
    } catch (e) { message('error', e.code || 'تعذّر الإصدار'); }
  });

  load();
})();
