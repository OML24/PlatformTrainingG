// App shell behaviour: mobile sidebar and toast notifications.
(() => {
  'use strict';

  const body = document.body;
  const sidebar = document.getElementById('app-sidebar');
  const toggle = document.getElementById('sidebar-toggle');
  const backdrop = document.getElementById('sidebar-backdrop');
  const mobile = window.matchMedia('(max-width: 991.98px)');
  const main = document.querySelector('.main-content');

  function setOpen(open) {
    if (!sidebar || !toggle) return;
    sidebar.classList.toggle('open', open);
    body.classList.toggle('sidebar-open', open);
    toggle.setAttribute('aria-expanded', String(open));
    if (main) main.inert = open && mobile.matches;
    sidebar.inert = !open && mobile.matches;
    if (open) sidebar.querySelector('a, button, select')?.focus();
    else if (mobile.matches) toggle.focus();
  }

  if (toggle) {
    toggle.addEventListener('click', () => setOpen(!sidebar.classList.contains('open')));
  }
  backdrop?.addEventListener('click', () => setOpen(false));
  document.addEventListener('keydown', event => {
    if (!sidebar?.classList.contains('open')) return;
    if (event.key === 'Escape') setOpen(false);
    if (event.key === 'Tab' && mobile.matches) {
      const items = [...sidebar.querySelectorAll('a[href], button:not(:disabled), select:not(:disabled)')].filter(node => node.getClientRects().length);
      const first = items[0], last = items.at(-1);
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
    }
  });
  if (sidebar) sidebar.inert = mobile.matches;
  mobile.addEventListener('change', () => setOpen(false));
  // Close the drawer after following a link on small screens.
  sidebar?.addEventListener('click', event => {
    if (event.target.closest('a') && window.matchMedia('(max-width: 991.98px)').matches) setOpen(false);
  });

  // Shared toast helper: ZuLms.toast(message | Node, 'success' | 'error' | 'warning' | 'info').
  // Toasts float at the top corner, stack, pause on hover and close by themselves.
  let host = document.getElementById('zu-toasts');
  if (!host) {
    host = document.createElement('div');
    host.className = 'zu-toast-host'; host.id = 'zu-toasts';
    document.body.append(host);
  }
  host.setAttribute('aria-live', 'polite');
  const icons = { success: 'ti-circle-check', error: 'ti-alert-circle', warning: 'ti-alert-triangle', info: 'ti-info-circle' };
  const MAX_TOASTS = 4;

  function toast(message, kind = 'info', timeout) {
    kind = Object.hasOwn(icons, kind) ? kind : kind === 'warn' ? 'warning' : 'info';
    const duration = timeout ?? (kind === 'error' ? 7000 : 4500);
    const node = document.createElement('div');
    node.className = `zu-toast ${kind}`;
    node.setAttribute('role', kind === 'error' ? 'alert' : 'status');
    const icon = document.createElement('i');
    icon.className = `ti ${icons[kind]} zu-toast-icon`; icon.setAttribute('aria-hidden', 'true');
    const text = document.createElement('div'); text.className = 'zu-toast-text';
    if (message instanceof Node) text.append(message); else text.textContent = String(message ?? '');
    const close = document.createElement('button'); close.type = 'button'; close.className = 'zu-toast-close';
    close.innerHTML = '<i class="ti ti-x" aria-hidden="true"></i>';
    close.setAttribute('aria-label', window.ZuLmsI18n?.t ? ZuLmsI18n.t('close') : 'إغلاق');
    const bar = document.createElement('span'); bar.className = 'zu-toast-progress';
    bar.style.animationDuration = `${duration}ms`;
    node.append(icon, text, close, bar);

    let timer = null, remaining = duration, started = 0;
    const dismiss = () => {
      if (node.classList.contains('leaving')) return;
      window.clearTimeout(timer);
      node.classList.add('leaving');
      node.addEventListener('animationend', () => node.remove(), { once: true });
      window.setTimeout(() => node.remove(), 400);
    };
    const start = () => { started = Date.now(); timer = window.setTimeout(dismiss, remaining); node.classList.remove('paused'); };
    const pause = () => { window.clearTimeout(timer); remaining -= Date.now() - started; node.classList.add('paused'); };
    close.addEventListener('click', dismiss);
    node.addEventListener('mouseenter', pause);
    node.addEventListener('mouseleave', start);
    node.addEventListener('focusin', pause);
    node.addEventListener('focusout', start);

    host.prepend(node);
    while (host.children.length > MAX_TOASTS) host.lastElementChild.remove();
    start();
    return { dismiss };
  }
  window.ZuLms = window.ZuLms || {};
  window.ZuLms.toast = toast;

  // Pages report results through hidden Bootstrap alerts (#success, #error, ...).
  // Show those messages as toasts instead of a banner at the top of the page.
  // Opt out with data-inline-alert on an alert that must stay in place.
  const FEEDBACK = '.alert-success, .alert-danger';
  const kindOf = el => el.classList.contains('alert-danger') ? 'error' : 'success';
  const visible = el => !el.classList.contains('d-none') && !el.hidden &&
    el.style.display !== 'none' && el.textContent.trim().length > 0;
  const pending = new Set();
  let scheduled = false;

  function convert(el) {
    if (!el.isConnected || el.closest('[data-inline-alert], #zu-toasts, .zu-toast-host') || !visible(el)) return;
    const content = document.createElement('div');
    for (const child of el.childNodes) content.append(child.cloneNode(true));
    content.querySelectorAll('.btn-close, [data-bs-dismiss]').forEach(x => x.remove());
    // Hide the original first, so the page keeps its normal show/hide logic.
    el.classList.add('d-none');
    toast(content, kindOf(el));
  }
  function flush() {
    scheduled = false;
    const items = [...pending]; pending.clear();
    items.forEach(convert);
  }
  function queue(el) {
    pending.add(el);
    if (!scheduled) { scheduled = true; queueMicrotask(flush); }
  }
  const observer = new MutationObserver(records => {
    for (const record of records) {
      const target = record.target.nodeType === Node.ELEMENT_NODE ? record.target : record.target.parentElement;
      if (!target) continue;
      const alert = target.closest(FEEDBACK);
      if (alert) queue(alert);
      for (const added of record.addedNodes) {
        if (added.nodeType !== Node.ELEMENT_NODE) continue;
        if (added.matches(FEEDBACK)) queue(added);
        added.querySelectorAll?.(FEEDBACK).forEach(queue);
      }
    }
  });
  observer.observe(document.body, { subtree: true, childList: true, characterData: true,
    attributes: true, attributeFilter: ['class', 'hidden', 'style'] });
})();
