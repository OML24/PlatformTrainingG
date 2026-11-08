(() => {
  const root = document.getElementById('certificate-template-page'); if (!root) return;
  const request = (method, path, body) => ZuLms.api.request(method, path, body);
  const DEFAULT_LAYOUT = JSON.stringify({
    unit: 'pt', origin: 'top-left',
    fields: {
      holder_name: { x: 146, y: 262, w: 500, font: 'Tajawal', size: 30, weight: 'bold', color: '#000000', align: 'center', dir: 'rtl', maxLines: 1, shrinkToFit: true },
      course_title: { x: 146, y: 358, w: 500, font: 'Tajawal', size: 22, weight: 'bold', color: '#000000', align: 'center', dir: 'rtl', maxLines: 2, shrinkToFit: true },
      course_period: { x: 146, y: 462, w: 500, font: 'Tajawal', size: 17, color: '#000000', align: 'center', dir: 'ltr', maxLines: 1, shrinkToFit: true },
      instructor_name: { x: 410, y: 512, w: 200, font: 'Tajawal', size: 13, color: '#000000', align: 'center', dir: 'rtl', maxLines: 1, shrinkToFit: true },
      course_hours: { x: 190, y: 512, w: 200, font: 'Tajawal', size: 13, color: '#000000', align: 'center', dir: 'ltr', maxLines: 1, shrinkToFit: true },
      issue_date: { x: 410, y: 548, w: 200, font: 'Tajawal', size: 13, color: '#000000', align: 'center', dir: 'ltr', maxLines: 1, shrinkToFit: true },
      reference_no: { x: 190, y: 548, w: 200, font: 'Tajawal', size: 12, color: '#000000', align: 'center', dir: 'ltr', maxLines: 1, shrinkToFit: true },
      qr: { x: 652, y: 470, size: 80 }
    }
  }, null, 2);

  function message(kind, text) {
    const error = document.getElementById('template-error');
    const success = document.getElementById('template-success');
    error.classList.add('d-none'); success.classList.add('d-none');
    const node = kind === 'error' ? error : success;
    node.textContent = text; node.classList.remove('d-none');
  }

  async function load() {
    const layout = document.getElementById('template-layout');
    if (!layout.value.trim()) layout.value = DEFAULT_LAYOUT;
    try {
      const template = await request('GET', '/api/v1/certificate-template');
      const info = document.getElementById('template-current-info');
      if (template && template.id) {
        info.textContent = `النسخة ${template.versionNo} — أبعاد الصفحة ${template.pageWidthPt}×${template.pageHeightPt}pt — مرفوعة ${new Date(template.createdAt).toLocaleString('ar')}`;
        layout.value = JSON.stringify(JSON.parse(template.layoutJson || '{}'), null, 2);
      } else info.textContent = 'لا يوجد قالب مرفوع بعد؛ إصدار الشهادات معلّق حتى رفعه.';
    } catch (e) {
      if (e.code !== 'CERT_TEMPLATE_MISSING') message('error', e.code || 'تعذّر جلب القالب');
    }
  }

  document.getElementById('template-form').addEventListener('submit', async event => {
    event.preventDefault();
    const file = document.getElementById('template-file').files[0];
    const layout = document.getElementById('template-layout').value.trim();
    if (!file) { message('error', 'اختر ملف القالب PDF'); return; }
    try { JSON.parse(layout); }
    catch { message('error', 'مواضع العناصر ليست JSON صالحاً'); return; }
    const form = new FormData();
    form.append('file', file);
    form.append('layoutJson', layout);
    const button = document.getElementById('template-submit');
    button.disabled = true;
    try {
      await request('POST', '/api/v1/certificate-template', form);
      message('success', 'رُفع القالب وأُصدرت الشهادات المعلّقة إن وُجد مستحقون لها');
      await load();
    } catch (e) {
      message('error', e.code === 'CERT_LAYOUT_FIELD_MISSING' ? 'مواضع العناصر ناقصة حقولاً إلزامية' : e.code || 'تعذّر رفع القالب');
    } finally { button.disabled = false; }
  });

  document.getElementById('template-preview').addEventListener('click', async () => {
    try {
      const response = await ZuLmsSession.fetch('/api/v1/certificate-template/preview', {
        method: 'POST', credentials: 'same-origin',
        headers: { RequestVerificationToken: await ZuLms.api.csrf() }
      });
      if (!response.ok) {
        const payload = await response.json().catch(() => null);
        message('error', (payload && payload.code) || 'تعذّرت المعاينة');
        return;
      }
      const blob = await response.blob();
      window.open(URL.createObjectURL(blob), '_blank');
    } catch { message('error', 'تعذّرت المعاينة'); }
  });

  load();
})();
