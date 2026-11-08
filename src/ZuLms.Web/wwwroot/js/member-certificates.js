(() => {
  const root = document.getElementById('member-certificates-page'); if (!root) return;
  const cards = document.getElementById('mycerts-cards');
  const empty = document.getElementById('mycerts-empty');
  const error = document.getElementById('mycerts-error');

  async function pdf(id) {
    const response = await ZuLmsSession.fetch(`/api/v1/certificates/${id}/pdf`, {
      credentials: 'same-origin', headers: { Accept: 'application/pdf' }
    });
    if (!response.ok) throw new Error('download failed');
    const bytes = new Uint8Array(await response.arrayBuffer());
    if (bytes.length < 5 || String.fromCharCode(...bytes.slice(0, 5)) !== '%PDF-')
      throw new Error('invalid pdf');
    return new Blob([bytes], { type: 'application/pdf' });
  }
  async function download(id, reference) {
    const blob = await pdf(id);
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url; link.download = `${reference || 'certificate'}.pdf`;
    document.body.append(link); link.click(); link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 5000);
  }
  async function preview(id) {
    const target = window.open('about:blank', '_blank');
    if (!target) throw new Error('preview blocked');
    target.opener = null;
    try {
      const blob = await pdf(id), url = URL.createObjectURL(blob);
      target.location.replace(url);
      setTimeout(() => URL.revokeObjectURL(url), 60000);
    } catch (error) {
      target.close();
      throw error;
    }
  }

  async function load() {
    try {
      const certificates = await ZuLms.api.request('GET', '/api/v1/certificates/mine');
      cards.replaceChildren();
      if (!certificates || !certificates.length) { empty.classList.remove('d-none'); return; }
      empty.classList.add('d-none');
      for (const certificate of certificates) {
        const column = document.createElement('div');
        column.className = 'col-md-6';
        const card = document.createElement('div');
        card.className = 'card h-100';
        const body = document.createElement('div');
        body.className = 'card-body';
        const title = document.createElement('h2');
        title.className = 'h6 card-title'; title.textContent = certificate.courseTitleAr;
        const list = document.createElement('dl');
        list.className = 'row mb-0 small';
        const row = (label, value, ltr) => {
          const dt = document.createElement('dt'); dt.className = 'col-5'; dt.textContent = label;
          const dd = document.createElement('dd'); dd.className = 'col-7';
          if (ltr) dd.dir = 'ltr';
          dd.textContent = value ?? '—';
          list.append(dt, dd);
        };
        row('معرّف الدورة', `#${certificate.courseId}`, true);
        row('رمز الدورة', certificate.courseNo);
        row('فترة الدورة', certificate.coursePeriodText);
        row('الساعات', String(certificate.courseHours));
        row('المحاضر', certificate.instructorNameAr);
        row('تاريخ الإصدار', new Date(certificate.issuedAt).toLocaleDateString('ar'));
        row('الرقم المرجعي', certificate.referenceNo, true);
        const actions = document.createElement('div');
        actions.className = 'd-flex gap-2 mt-3';
        const downloadButton = document.createElement('button');
        downloadButton.type = 'button';
        downloadButton.className = 'btn btn-sm btn-primary';
        downloadButton.textContent = 'تنزيل PDF';
        downloadButton.addEventListener('click', async () => {
          downloadButton.disabled = true;
          try { await download(certificate.id, certificate.referenceNo); }
          catch { ZuLms.toast('تعذّر تنزيل الشهادة', 'error'); }
          finally { downloadButton.disabled = false; }
        });
        const previewButton = document.createElement('button');
        previewButton.type = 'button'; previewButton.className = 'btn btn-sm btn-outline-primary';
        previewButton.textContent = 'معاينة';
        previewButton.addEventListener('click', async () => {
          previewButton.disabled = true;
          try { await preview(certificate.id); }
          catch { ZuLms.toast('ملف الشهادة غير صالح أو غير متوفر', 'error'); }
          finally { previewButton.disabled = false; }
        });
        const verifyLink = document.createElement('a');
        verifyLink.className = 'btn btn-sm btn-outline-secondary';
        verifyLink.href = certificate.verifyUrl; verifyLink.target = '_blank';
        verifyLink.rel = 'noopener noreferrer'; verifyLink.textContent = 'تحقق';
        actions.append(previewButton, downloadButton, verifyLink);
        body.append(title, list, actions);
        card.append(body); column.append(card); cards.append(column);
      }
    } catch (e) {
      error.textContent = e.code || 'تعذّر تحميل الشهادات';
      error.classList.remove('d-none');
    }
  }

  load();
})();
