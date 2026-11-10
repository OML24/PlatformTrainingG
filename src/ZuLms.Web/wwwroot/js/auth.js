(() => {
  // Presentation helpers (redesign): show/hide password, Caps Lock hint.
  document.querySelectorAll('[data-eye-for]').forEach(button => {
    const input = document.getElementById(button.dataset.eyeFor);
    if (!input) return;
    button.addEventListener('click', () => {
      const show = input.type === 'password';
      input.type = show ? 'text' : 'password';
      button.setAttribute('aria-pressed', String(show));
      button.setAttribute('aria-label', show ? button.dataset.labelHide : button.dataset.labelShow);
      input.focus();
    });
  });
  const caps = document.getElementById('caps-warning');
  if (caps) {
    const check = event => {
      if (typeof event.getModifierState === 'function')
        caps.classList.toggle('show', event.getModifierState('CapsLock'));
    };
    document.querySelectorAll('input[type="password"], #password').forEach(input => {
      input.addEventListener('keydown', check);
      input.addEventListener('keyup', check);
      input.addEventListener('blur', () => caps.classList.remove('show'));
    });
  }

  const form = document.querySelector('#login-form, #change-password-form, #account-password-form, #forgot-password-form');
  if (!form) return;
  const message = document.getElementById('auth-message');
  const box = form.closest('.auth-box');
  let submitting = false;
  form.addEventListener('submit', async event => {
    event.preventDefault();
    if (submitting) return;
    submitting = true;
    message.textContent = '';
    message.classList.remove('is-success');
    const submit = form.querySelector('button[type="submit"]');
    submit.disabled = true;
    submit.classList.add('is-loading');
    submit.setAttribute('aria-busy', 'true');
    let navigating = false;
    try {
      const payload = Object.fromEntries(new FormData(form).entries());
      if (form.id === 'login-form') payload.rememberMe = document.getElementById('remember-me').checked;
      const result = await ZuLms.api.post(form.dataset.api, payload, { silent401: true });
      if (form.id === 'forgot-password-form') { message.textContent = form.dataset.success; message.classList.add('is-success'); }
      else {
        if (result.sessionId) ZuLmsSession.activate(result.sessionId);
        navigating = true;
        window.location.assign(ZuLmsSession.url(result.redirectUrl));
      }
    } catch (error) {
      message.textContent = error.message || form.dataset.error;
      if (box) { box.classList.remove('is-shaking'); void box.offsetWidth; box.classList.add('is-shaking'); }
    } finally {
      submitting = false;
      // Keep the spinner while the browser navigates after a successful login.
      if (!navigating) { submit.disabled = false; submit.classList.remove('is-loading'); submit.removeAttribute('aria-busy'); }
    }
  });
})();
