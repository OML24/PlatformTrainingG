(() => {
  const root = document.getElementById('cancel-request-page'); if (!root) return;
  const id = Number(root.dataset.courseId);
  const $ = name => document.getElementById(`cancel-request-${name}`);
  $('form').addEventListener('submit', async event => {
    event.preventDefault(); $('error').classList.add('d-none'); $('success').classList.add('d-none');
    $('submit').disabled = true;
    try {
      let fileId = null;
      const file = $('letter').files[0];
      if (file) {
        const form = new FormData(); form.append('kind', 'CancellationLetter'); form.append('file', file);
        fileId = (await ZuLms.api.upload('/api/v1/files', form)).id;
      }
      await ZuLms.api.request('POST', `/api/v1/courses/${id}/cancellation/request`,
        { reason: $('reason').value, letterFileId: fileId });
      $('success').textContent = 'أُرسل طلب الإلغاء إلى الجودة. تستمر الدورة حتى القرار.';
      $('success').classList.remove('d-none'); $('form').classList.add('d-none');
    } catch (e) {
      $('error').textContent = e.code || e.message || 'تعذّر إرسال الطلب';
      $('error').classList.remove('d-none');
    } finally { $('submit').disabled = false; }
  });
})();
