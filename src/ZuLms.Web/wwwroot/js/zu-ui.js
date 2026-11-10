// ZU-LMS UI helpers for the redesign (docs/FRONTEND-REDESIGN.md).
// Presentation only: no API calls here.
(() => {
  'use strict';
  const reduced = window.matchMedia('(prefers-reduced-motion: reduce)');
  const locale = document.documentElement.lang === 'en' ? 'en-US' : 'ar-JO-u-nu-latn';
  const fmt = (value, decimals = 0) => Number(value || 0).toLocaleString(locale,
    { minimumFractionDigits: decimals, maximumFractionDigits: decimals });

  // Animate a number inside `node` from its current value to `target`.
  function countUp(node, target, options = {}) {
    if (!node) return;
    const decimals = options.decimals ?? (Number.isInteger(Number(target)) ? 0 : 1);
    const end = Number(target) || 0;
    const start = Number(node.dataset.zuValue || 0);
    node.dataset.zuValue = String(end);
    if (reduced.matches || start === end) { node.textContent = fmt(end, decimals); return; }
    const duration = options.duration ?? 900;
    const t0 = performance.now();
    const step = now => {
      const p = Math.min(1, (now - t0) / duration);
      const eased = 1 - Math.pow(1 - p, 3);
      node.textContent = fmt(start + (end - start) * eased, decimals);
      if (p < 1) requestAnimationFrame(step);
    };
    requestAnimationFrame(step);
  }

  // Header gets a soft shadow once the page scrolls.
  const header = document.querySelector('.main-content > .header');
  if (header) {
    const onScroll = () => header.classList.toggle('is-scrolled', window.scrollY > 4);
    window.addEventListener('scroll', onScroll, { passive: true });
    onScroll();
  }

  window.ZuUi = { countUp, format: fmt, reducedMotion: () => reduced.matches };
})();
