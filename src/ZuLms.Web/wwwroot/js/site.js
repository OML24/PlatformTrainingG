(() => {
  'use strict';
  let questionId = 0;

  // Browser-native prompt/confirm are unavailable in some embedded browsers.
  function ask(message, needsText, noticeOnly = false, options = {}) {
    return new Promise(resolve => {
      message = ZuLmsI18n.translate ? ZuLmsI18n.translate(message) : message;
      options = Object.assign({}, options);
      if (options.title && ZuLmsI18n.translate) options.title = ZuLmsI18n.translate(options.title);
      if (options.label && ZuLmsI18n.translate) options.label = ZuLmsI18n.translate(options.label);
      const trigger = document.activeElement;
      const dialog = document.createElement('dialog');
      dialog.className = 'zu-question-dialog';
      const form = document.createElement('form');
      const header = document.createElement('div'); header.className = 'modal-header';
      const title = document.createElement('h2'); title.className = 'h5 mb-0';
      title.id = `zu-question-title-${++questionId}`;
      title.textContent = options.title || ZuLmsI18n.t(noticeOnly ? 'close' : 'confirm');
      dialog.setAttribute('aria-labelledby', title.id);
      header.append(title);
      const body = document.createElement('div'); body.className = 'modal-body';
      const description = document.createElement('p');
      description.className = 'mb-3';
      description.textContent = message;
      body.append(description);
      let input = null;
      if (needsText) {
        input = document.createElement('textarea');
        input.className = 'form-control mb-3';
        input.rows = 3;
        input.required = !!options.required;
        input.setAttribute('aria-label', message);
        body.append(input);
      }
      const actions = document.createElement('div');
      actions.className = 'modal-footer';
      const cancel = document.createElement('button');
      cancel.type = 'button';
      cancel.className = 'btn btn-outline-secondary';
      cancel.textContent = ZuLmsI18n.t('cancel');
      const accept = document.createElement('button');
      accept.type = 'submit';
      accept.className = options.danger ? 'btn btn-danger' : 'btn btn-primary';
      accept.textContent = options.label || ZuLmsI18n.t(noticeOnly ? 'close' : 'confirm');
      if (!noticeOnly) actions.append(cancel);
      actions.append(accept);
      form.append(header, body, actions);
      dialog.append(form);
      document.body.append(dialog);
      let settled = false;
      const finish = value => {
        if (settled) return;
        settled = true;
        dialog.close();
        dialog.remove();
        if (trigger?.isConnected) trigger.focus();
        resolve(value);
      };
      cancel.addEventListener('click', () => finish(needsText ? null : false));
      dialog.addEventListener('cancel', event => {
        event.preventDefault();
        finish(needsText ? null : false);
      });
      form.addEventListener('submit', event => {
        event.preventDefault();
        if (input && options.required && !input.value.trim()) { input.focus(); return; }
        finish(needsText ? input.value : true);
      });
      dialog.showModal();
      (input || accept).focus();
    });
  }
  window.ZuLms = Object.assign(window.ZuLms || {}, {
    ui: {
      confirm: (message, options) => ask(message, false, false, options),
      prompt: (message, options) => ask(message, true, false, options),
      show: (message, options) => ask(message, false, true, options)
    }
  });

  const logout = document.getElementById('logout-button');
  if (logout) {
    logout.addEventListener('click', async () => {
      logout.disabled = true;
      try {
        await ZuLms.api.post('/api/v1/auth/logout', undefined, { silent401: true });
      } catch {
        // Session may already be gone; continue to the login screen.
      }
      window.location.assign(ZuLmsSession.url('/login'));
    });
  }

  // Bootstrap tooltips for static elements marked with data-bs-toggle="tooltip".
  if (window.bootstrap && window.bootstrap.Tooltip) {
    document.querySelectorAll('[data-bs-toggle="tooltip"]')
      .forEach(element => window.bootstrap.Tooltip.getOrCreateInstance(element));
  }

  // Keep the original action nodes, including their attached listeners.
  const content = document.querySelector('.page-content');
  if (content && window.bootstrap?.Dropdown) {
    let scheduled = false;
    function compactTableActions() {
      scheduled = false;
      content.querySelectorAll('td:not([data-actions-compact])').forEach(cell => {
        const buttons = [...cell.querySelectorAll('.btn')];
        if (buttons.length < 3) return;
        cell.dataset.actionsCompact = 'true';
        const dropdown = document.createElement('div');
        dropdown.className = 'dropdown table-action-dropdown';
        const trigger = document.createElement('button');
        trigger.type = 'button';
        trigger.className = 'btn btn-sm btn-outline-primary dropdown-toggle';
        trigger.setAttribute('data-bs-toggle', 'dropdown');
        trigger.setAttribute('aria-expanded', 'false');
        trigger.textContent = document.documentElement.lang === 'en' ? 'Actions' : 'إجراءات';
        const menu = document.createElement('div');
        menu.className = 'dropdown-menu table-action-menu';
        for (const button of buttons) {
          if (button.classList.contains('btn-danger') || button.classList.contains('btn-outline-danger'))
            button.classList.add('table-action-danger');
          menu.append(button);
        }
        dropdown.append(trigger, menu);
        cell.append(dropdown);
        window.bootstrap.Dropdown.getOrCreateInstance(trigger, { popperConfig: { strategy: 'fixed' } });
        menu.addEventListener('click', event => {
          if (event.target.closest('.btn')) window.bootstrap.Dropdown.getInstance(trigger)?.hide();
        });
      });
    }
    const observer = new MutationObserver(() => {
      if (!scheduled) {
        scheduled = true;
        queueMicrotask(compactTableActions);
      }
    });
    observer.observe(content, { childList: true, subtree: true });
    compactTableActions();
  }
})();
