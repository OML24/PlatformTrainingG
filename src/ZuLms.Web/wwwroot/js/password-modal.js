// Opens "change my password" in a popup instead of navigating to /account/password.
(() => {
  'use strict';
  const modalEl = document.getElementById('password-modal');
  if (!modalEl || !window.bootstrap) return;
  const form = document.getElementById('password-modal-form');
  const message = document.getElementById('password-modal-message');
  const submit = document.getElementById('password-modal-submit');
  const modal = bootstrap.Modal.getOrCreateInstance(modalEl);

  document.addEventListener('click', event => {
    const trigger = event.target.closest('[data-password-modal]');
    if (!trigger || event.ctrlKey || event.metaKey || event.shiftKey) return;
    event.preventDefault();
    modal.show();
  });
  modalEl.addEventListener('shown.bs.modal', () => document.getElementById('pm-current').focus());
  modalEl.addEventListener('hidden.bs.modal', () => {
    form.reset();
    message.classList.add('d-none');
    form.querySelectorAll('input').forEach(input => { input.type = 'password'; input.classList.remove('is-invalid'); });
  });
  form.querySelectorAll('[data-pm-eye]').forEach(button => button.addEventListener('click', () => {
    const input = document.getElementById(button.dataset.pmEye);
    const show = input.type === 'password';
    input.type = show ? 'text' : 'password';
    button.querySelector('i').className = show ? 'ti ti-eye-off' : 'ti ti-eye';
  }));

  const codes = {
    AUTH_INVALID: 'كلمة المرور الحالية غير صحيحة.',
    PWD_CONFIRM_MISMATCH: 'كلمتا المرور غير متطابقتين.',
    PWD_TOO_SHORT: 'كلمة المرور الجديدة قصيرة جدًا.',
    PWD_TOO_LONG: 'كلمة المرور الجديدة طويلة جدًا.',
    PWD_NEED_DIGIT: 'يجب أن تحتوي كلمة المرور على رقم.',
    PWD_NEED_LOWER: 'يجب أن تحتوي كلمة المرور على حرف صغير.',
    PWD_NEED_UPPER: 'يجب أن تحتوي كلمة المرور على حرف كبير.',
    PWD_NEED_SYMBOL: 'يجب أن تحتوي كلمة المرور على رمز.',
    PWD_NO_ARABIC: 'لا يُسمح بالحروف العربية في كلمة المرور.',
    PWD_NO_SPACES: 'لا يُسمح بالمسافات في كلمة المرور.',
    PWD_CONTAINS_CONTEXT: 'لا يجوز أن تحتوي كلمة المرور على اسمك أو اسم الدخول.',
    PWD_BLOCKED: 'كلمة المرور هذه شائعة أو محظورة، اختر غيرها.'
  };
  function fail(text) {
    message.textContent = text;
    message.classList.remove('d-none');
  }

  form.addEventListener('submit', async event => {
    event.preventDefault();
    message.classList.add('d-none');
    const payload = Object.fromEntries(new FormData(form).entries());
    let invalid = false;
    form.querySelectorAll('input[required]').forEach(input => {
      const empty = !input.value;
      input.classList.toggle('is-invalid', empty);
      invalid ||= empty;
    });
    if (invalid) return;
    if (payload.newPassword !== payload.confirmation) {
      document.getElementById('pm-confirmation').classList.add('is-invalid');
      fail(form.dataset.mismatch);
      return;
    }
    submit.disabled = true;
    try {
      await ZuLms.api.post('/api/v1/auth/account-password', payload, { silent401: true });
      modal.hide();
      window.ZuLms.toast?.(form.dataset.success, 'success');
    } catch (error) {
      fail(codes[error.code] || (error.message && error.message !== error.code ? error.message : form.dataset.error));
    } finally {
      submit.disabled = false;
    }
  });
})();
