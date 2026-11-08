(() => {
  const form = document.querySelector('#login-form, #change-password-form, #account-password-form, #forgot-password-form');
  if (!form) return;
  const message = document.getElementById('auth-message');
  let submitting = false;
  form.addEventListener('submit', async event => {
    event.preventDefault();
    if (submitting) return;
    submitting = true;
    message.textContent = '';
    const submit = form.querySelector('button[type="submit"]');
    submit.disabled = true;
    try {
      const payload = Object.fromEntries(new FormData(form).entries());
      if (form.id === 'login-form') payload.rememberMe = document.getElementById('remember-me').checked;
      const result = await ZuLms.api.post(form.dataset.api, payload, { silent401: true });
      if (form.id === 'forgot-password-form') message.textContent = form.dataset.success;
      else {
        if (result.sessionId) ZuLmsSession.activate(result.sessionId);
        window.location.assign(ZuLmsSession.url(result.redirectUrl));
      }
    } catch (error) {
      message.textContent = error.message || form.dataset.error;
    } finally { submitting = false; submit.disabled = false; }
  });
})();
