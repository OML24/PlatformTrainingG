(function () {
  'use strict';

  const cache = new Map();

  function contextOf(element) {
    return element.getAttribute('data-hint-context') || 'shared';
  }

  async function loadContext(context) {
    if (cache.has(context)) return cache.get(context);
    const promise = ZuLms.api.get('/api/v1/hints?context=' + encodeURIComponent(context))
      .then(rows => {
        const map = new Map();
        (rows || []).forEach(row => map.set(row.fieldKey, row));
        return map;
      })
      .catch(() => new Map());
    cache.set(context, promise);
    return promise;
  }

  function apply(element, hint) {
    if (!hint) return;
    const english = document.documentElement.lang === 'en';
    const text = english && hint.hintEn ? hint.hintEn : hint.hintAr;
    element.setAttribute('title', text);
    if (!/^(INPUT|SELECT|TEXTAREA)$/.test(element.tagName)) element.setAttribute('aria-label', text);
    if (element.dataset.hintBound === 'true') return;
    element.dataset.hintBound = 'true';
    const show = () => { if (window.bootstrap && window.bootstrap.Tooltip) window.bootstrap.Tooltip.getOrCreateInstance(element).show(); };
    element.addEventListener('mouseenter', show);
    element.addEventListener('focus', show);
  }

  async function hydrate(element) {
    const key = element.getAttribute('data-hint-key');
    if (!key) return;
    const hints = await loadContext(contextOf(element));
    apply(element, hints.get(key));
  }

  function scan(root) {
    (root || document).querySelectorAll('[data-hint-key]').forEach(element => { hydrate(element); });
  }

  document.addEventListener('DOMContentLoaded', () => scan(document));
  window.ZuLmsHints = { scan: scan, refresh: () => { cache.clear(); scan(document); } };
})();
