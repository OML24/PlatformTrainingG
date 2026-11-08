(() => {
  const root = document.getElementById('verify-page'); if (!root) return;
  const input = document.getElementById('verify-reference');
  const button = document.getElementById('verify-submit');
  const message = document.getElementById('verify-message');
  const result = document.getElementById('verify-result');
  const header = document.getElementById('verify-header');
  const fields = document.getElementById('verify-fields');
  const text = root.dataset;

  function showMessage(kind, text) {
    message.className = `alert mt-3 ${kind === 'error' ? 'alert-danger' : 'alert-info'}`;
    message.textContent = text;
  }
  function hideMessage() { message.className = 'alert d-none mt-3'; }
  function hideResult() { result.classList.add('d-none'); }

  function row(label, value) {
    const dt = document.createElement('dt'); dt.className = 'col-sm-4'; dt.textContent = label;
    const dd = document.createElement('dd'); dd.className = 'col-sm-8'; dd.textContent = value ?? '—';
    return [dt, dd];
  }

  async function verify() {
    const reference = (input.value || '').trim();
    hideResult(); hideMessage();
    if (!reference) { showMessage('error', text.required); return; }
    button.disabled = true;
    try {
      const response = await fetch(`/api/v1/public/verify/${encodeURIComponent(reference)}`,
        { headers: { Accept: 'application/json' } });
      const payload = await response.json().catch(() => null);
      if (!response.ok || !payload) {
        showMessage('error', (payload && payload.message) || text.error);
        return;
      }
      const certificate = payload.certificate;
      if (!certificate) {
        showMessage('error', text.notFound);
        return;
      }
      const english = document.documentElement.lang === 'en';
      header.textContent = certificate.revoked ? text.revoked : text.valid;
      header.className = `card-header ${certificate.revoked ? 'text-bg-danger' : 'text-bg-success'}`;
      fields.replaceChildren();
      if (certificate.revoked) {
        fields.append(...row(text.revokedAt, certificate.revokedAt
          ? ZuLmsDates.displayDate(certificate.revokedAt) : '—'));
      } else {
        fields.append(
          ...row(text.holder, english && certificate.holderNameEn || certificate.holderNameAr),
          ...row(text.course, english && certificate.courseTitleEn || certificate.courseTitleAr),
          ...row(text.period, certificate.coursePeriodText),
          ...row(text.hours, String(certificate.courseHours)),
          ...row(text.instructor, certificate.instructorNameAr),
          ...row(text.issued, ZuLmsDates.displayDate(certificate.issuedAt)),
          ...row(text.reference, certificate.referenceNo));
      }
      result.classList.remove('d-none');
    } catch (e) {
      showMessage('error', text.connection);
    } finally {
      button.disabled = false;
    }
  }

  button.addEventListener('click', verify);
  input.addEventListener('keydown', e => { if (e.key === 'Enter') verify(); });
  const initial = new URLSearchParams(window.location.search).get('cert');
  if (initial) { input.value = initial; verify(); }
})();
