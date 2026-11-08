(function () {
  'use strict';

  const csrfMeta = document.querySelector('meta[name="csrf"]');
  const sessionFetch = (url, options) => window.ZuLmsSession.fetch(url, options);

  class ApiError extends Error {
    constructor(code, message, status, fieldErrors) {
      super(message || code);
      this.name = 'ApiError';
      this.code = code;
      this.status = status;
      this.fieldErrors = fieldErrors || null;
    }
  }

  let csrfPromise = null;

  function csrf(force) {
    if (!force && csrfMeta && csrfMeta.content) return Promise.resolve(csrfMeta.content);
    if (force) csrfPromise = null;
    if (!csrfPromise) {
      csrfPromise = sessionFetch('/api/v1/auth/csrf', { credentials: 'same-origin', cache: 'no-store' })
        .then(response => response.ok ? response.json() : Promise.reject(new ApiError('CSRF_INVALID', '', response.status)))
        .then(payload => {
          const token = (unwrap(payload) || {}).requestToken;
          // Keep the meta tag in sync so every later call reuses the refreshed token.
          if (csrfMeta && token) csrfMeta.content = token;
          return token;
        })
        .catch(error => { csrfPromise = null; throw error; });
    }
    return csrfPromise;
  }

  let refreshPromise = null;

  // The access cookie is short lived; refresh it silently instead of dropping the user's work.
  function refreshSession() {
    if (!refreshPromise) {
      refreshPromise = csrf()
        .then(token => sessionFetch('/api/v1/auth/refresh', {
          method: 'POST',
          headers: { 'Accept': 'application/json', 'RequestVerificationToken': token },
          credentials: 'same-origin',
          cache: 'no-store'
        }))
        .then(response => response.ok ? true : Promise.reject(new ApiError('UNAUTHORIZED', '', response.status)))
        .finally(() => { refreshPromise = null; });
    }
    return refreshPromise;
  }

  function unwrap(payload) {
    return payload && typeof payload === 'object' && Object.prototype.hasOwnProperty.call(payload, 'ok')
      ? payload.data
      : payload;
  }

  function redirectToLogin() {
    const target = window.location.pathname + window.location.search;
    window.location.assign(ZuLmsSession.url('/login?returnUrl=' + encodeURIComponent(target)));
  }

  function applyFieldErrors(fieldErrors, scope) {
    if (!fieldErrors) return;
    const root = scope || document;
    root.querySelectorAll('.field-error').forEach(node => node.remove());
    Object.keys(fieldErrors).forEach(name => {
      const field = root.querySelector('[name="' + CSS.escape(name) + '"]');
      if (!field) return;
      const message = document.createElement('div');
      message.className = 'field-error invalid-feedback d-block';
      const list = fieldErrors[name];
      message.textContent = Array.isArray(list) ? list.join(' ') : String(list);
      field.classList.add('is-invalid');
      (field.closest('.mb-3, .col-md-6, .col-md-4, .col-12') || field.parentElement).append(message);
    });
  }

  async function request(method, url, body, options) {
    const settings = options || {};
    let retriedCsrf = false;
    let retriedAuth = false;

    const send = async token => {
      const headers = Object.assign({ 'Accept': 'application/json' }, settings.headers || {});
      if (method !== 'GET' && method !== 'HEAD') headers.RequestVerificationToken = token;
      if (body !== undefined && body !== null && !(body instanceof FormData)) {
        headers['Content-Type'] = 'application/json';
      }
      return sessionFetch(url, {
        method: method,
        headers: headers,
        credentials: 'same-origin',
        cache: 'no-store',
        body: body === undefined || body === null ? undefined
          : body instanceof FormData ? body : JSON.stringify(body)
      });
    };

    let token = method === 'GET' || method === 'HEAD' ? null : await csrf();
    let response = await send(token);

    // Retry once with a freshly issued antiforgery token, then once after a silent session refresh.
    while (true) {
      if (response.status !== 400 && response.status !== 401) break;
      const problem = await response.clone().json().catch(() => null);
      const code = problem && problem.code;
      if (response.status === 400 && code === 'CSRF_INVALID' && !retriedCsrf) {
        retriedCsrf = true;
        token = await csrf(true);
        response = await send(token);
        continue;
      }
      if (response.status === 401 && code !== 'REAUTH_REQUIRED' && !retriedAuth) {
        retriedAuth = true;
        try {
          await refreshSession();
        } catch {
          if (settings.silent401 !== true) redirectToLogin();
          throw new ApiError('UNAUTHORIZED', '', 401);
        }
        if (method === 'GET' || method === 'HEAD') token = null; else token = await csrf();
        response = await send(token);
        continue;
      }
      break;
    }

    if (response.status === 204) return null;

    const type = response.headers.get('content-type') || '';
    const payload = type.indexOf('json') >= 0 ? await response.json().catch(() => null) : null;

    if (response.status === 401 && (payload && payload.code) !== 'REAUTH_REQUIRED') {
      if (settings.silent401 !== true) redirectToLogin();
      throw new ApiError('UNAUTHORIZED', '', 401);
    }

    if (!response.ok) {
      const code = (payload && payload.code) || String(response.status);
      const message = (payload && payload.message) || '';
      const fieldErrors = (payload && payload.fieldErrors) || null;
      if (fieldErrors) applyFieldErrors(fieldErrors, settings.scope);
      throw new ApiError(code, message, response.status, fieldErrors);
    }
    return unwrap(payload);
  }

  function toast(message, kind) {
    // shell.js installs the shared toast UI; keep this only as a fallback for pages without it.
    if (window.ZuLms && window.ZuLms.toast && window.ZuLms.toast !== toast) return window.ZuLms.toast(message, kind);
    const host = document.getElementById('zu-toasts') || (function () {
      const node = document.createElement('div');
      node.id = 'zu-toasts';
      node.className = 'toast-container position-fixed bottom-0 end-0 p-3';
      node.setAttribute('aria-live', 'polite');
      document.body.append(node);
      return node;
    })();
    const element = document.createElement('div');
    element.className = 'toast align-items-center border-0 ' +
      (kind === 'error' ? 'text-bg-danger' : kind === 'warn' ? 'text-bg-warning' : 'text-bg-success');
    element.setAttribute('role', kind === 'error' ? 'alert' : 'status');
    const inner = document.createElement('div');
    inner.className = 'd-flex';
    const text = document.createElement('div');
    text.className = 'toast-body';
    text.textContent = message;
    const close = document.createElement('button');
    close.type = 'button';
    close.className = 'btn-close btn-close-white me-2 m-auto';
    close.setAttribute('data-bs-dismiss', 'toast');
    close.setAttribute('aria-label', 'Close');
    inner.append(text, close);
    element.append(inner);
    host.append(element);
    if (window.bootstrap && window.bootstrap.Toast) {
      const instance = new window.bootstrap.Toast(element, { delay: 4000 });
      instance.show();
      element.addEventListener('hidden.bs.toast', () => element.remove());
    } else {
      setTimeout(() => element.remove(), 4000);
    }
  }

  window.ZuLms = Object.assign(window.ZuLms || {}, {
    ApiError: ApiError,
    unwrap: unwrap,
    toast: toast,
    applyFieldErrors: applyFieldErrors,
    api: {
      csrf: csrf,
      request: request,
      get: (url, options) => request('GET', url, undefined, options),
      post: (url, body, options) => request('POST', url, body, options),
      put: (url, body, options) => request('PUT', url, body, options),
      patch: (url, body, options) => request('PATCH', url, body, options),
      del: (url, options) => request('DELETE', url, undefined, options),
      upload: (url, formData, options) => request('POST', url, formData, options)
    }
  });
})();
