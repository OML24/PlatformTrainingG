// ZU-LMS custom select: replaces the browser's native (blue, OS-styled) option list of every
// single-choice <select> with a themed dropdown. The native <select> stays in the DOM and
// remains the source of truth (value, options, change events, validation, form submit),
// so existing page scripts keep working unchanged. Opt out with <select data-native>.
(() => {
  'use strict';
  const SKIP = 'select[multiple], select.monthselect, select.yearselect, select[data-native]';
  const SEARCH_THRESHOLD = 8;
  const isEnglish = () => document.documentElement.lang === 'en';
  const proto = HTMLSelectElement.prototype;
  const native = {
    value: Object.getOwnPropertyDescriptor(proto, 'value'),
    selectedIndex: Object.getOwnPropertyDescriptor(proto, 'selectedIndex')
  };
  const states = new WeakMap();
  let current = null; // { select, state, menu, list, search, items, active }

  const normalize = text => String(text || '').toLowerCase()
    .replace(/[أإآ]/g, 'ا').replace(/ة/g, 'ه').replace(/ى/g, 'ي')
    .replace(/[ً-ْ]/g, '').trim();

  function shouldSkip(select) {
    if (select.matches(SKIP)) return true;
    const size = Number(select.getAttribute('size') || 0);
    return size > 1;
  }

  function enhance(select) {
    if (states.has(select) || shouldSkip(select)) return;
    // A cloned form fragment may carry a stale, handler-less button: drop it.
    const stale = select.nextElementSibling;
    if (stale && stale.classList.contains('zu-select-btn')) stale.remove();

    const button = document.createElement('button');
    button.type = 'button';
    button.setAttribute('aria-haspopup', 'listbox');
    button.setAttribute('aria-expanded', 'false');
    const label = document.createElement('span');
    label.className = 'zu-select-label';
    button.append(label);
    select.after(button);
    select.classList.add('zu-select-native');
    select.tabIndex = -1;

    const state = { button, label };
    states.set(select, state);

    state.syncClasses = () => {
      const classes = [...select.classList].filter(name => name !== 'zu-select-native' && name !== 'is-invalid');
      if (!classes.includes('form-select')) classes.unshift('zu-select-bare');
      button.className = ['zu-select-btn', ...classes,
        state.invalid ? 'is-invalid' : '', state.placeholder ? 'is-placeholder' : '', current && current.select === select ? 'is-open' : '']
        .filter(Boolean).join(' ');
      button.hidden = select.hidden;
      button.style.display = select.style.display;
      button.disabled = select.disabled;
      const labelled = select.getAttribute('aria-label') ||
        (select.labels && select.labels[0] ? select.labels[0].textContent.trim() : '');
      if (labelled) button.setAttribute('aria-label', labelled);
      if (select.title) button.title = select.title;
    };
    state.sync = () => {
      const option = select.options[native.selectedIndex.get.call(select)];
      const text = option ? option.textContent.trim() : '';
      label.textContent = text || '—';
      state.placeholder = !option || option.value === '';
      button.classList.toggle('is-placeholder', state.placeholder);
      button.disabled = select.disabled;
      if (current && current.select === select) render();
    };

    for (const prop of ['value', 'selectedIndex']) {
      Object.defineProperty(select, prop, {
        configurable: true, enumerable: true,
        get() { return native[prop].get.call(this); },
        set(v) { native[prop].set.call(this, v); state.sync(); }
      });
    }

    new MutationObserver(() => { state.syncClasses(); state.sync(); }).observe(select, {
      childList: true, subtree: true, characterData: true, attributes: true,
      attributeFilter: ['class', 'disabled', 'hidden', 'style', 'selected', 'label']
    });
    select.addEventListener('change', () => { state.invalid = false; state.syncClasses(); state.sync(); });
    select.addEventListener('focus', () => button.focus());
    select.addEventListener('invalid', () => { state.invalid = true; state.syncClasses(); });
    if (select.form) select.form.addEventListener('reset', () => setTimeout(state.sync));

    button.addEventListener('click', () => {
      if (current && current.select === select) close(); else open(select);
    });
    button.addEventListener('keydown', event => {
      if (current && current.select === select) { onMenuKey(event); return; }
      if (['ArrowDown', 'ArrowUp', 'Enter', ' '].includes(event.key)) { event.preventDefault(); open(select); }
    });

    state.syncClasses();
    state.sync();
  }

  function render() {
    const { select, list, search } = current;
    const query = search ? normalize(search.value) : '';
    const selectedIndex = native.selectedIndex.get.call(select);
    list.replaceChildren();
    current.items = [];
    let group = null;
    [...select.options].forEach((option, index) => {
      if (option.hidden) return;
      const text = option.textContent.trim();
      if (query && !normalize(text).includes(query)) return;
      const parent = option.parentElement;
      if (parent && parent.tagName === 'OPTGROUP' && parent !== group) {
        group = parent;
        const header = document.createElement('div');
        header.className = 'zu-select-group';
        header.textContent = parent.label;
        list.append(header);
      }
      const item = document.createElement('div');
      item.className = 'zu-select-option';
      item.setAttribute('role', 'option');
      item.dataset.index = String(index);
      const selected = index === selectedIndex;
      item.setAttribute('aria-selected', String(selected));
      if (selected) item.classList.add('is-selected');
      if (option.value === '') item.classList.add('is-empty');
      if (option.disabled) { item.classList.add('is-disabled'); item.setAttribute('aria-disabled', 'true'); }
      const span = document.createElement('span');
      span.textContent = text || '—';
      const check = document.createElement('i');
      check.className = 'ti ti-check zu-select-check';
      check.setAttribute('aria-hidden', 'true');
      item.append(span, check);
      item.addEventListener('mousedown', event => event.preventDefault());
      item.addEventListener('click', () => { if (!option.disabled) choose(index); });
      item.addEventListener('mousemove', () => setActive(current.items.indexOf(item), false));
      list.append(item);
      current.items.push(item);
    });
    if (!current.items.length) {
      const empty = document.createElement('div');
      empty.className = 'zu-select-empty';
      empty.textContent = isEnglish() ? 'No results' : 'لا توجد نتائج';
      list.append(empty);
    }
    const selectedItem = current.items.findIndex(item => item.classList.contains('is-selected'));
    setActive(query ? firstEnabled() : (selectedItem >= 0 ? selectedItem : firstEnabled()), true);
  }

  function firstEnabled() {
    return current.items.findIndex(item => !item.classList.contains('is-disabled'));
  }

  function setActive(index, scroll) {
    if (!current) return;
    current.items.forEach(item => item.classList.remove('is-active'));
    current.active = index;
    const item = current.items[index];
    if (!item) return;
    item.classList.add('is-active');
    if (scroll) item.scrollIntoView({ block: 'nearest' });
  }

  function move(step) {
    const items = current.items;
    if (!items.length) return;
    let index = current.active;
    for (let i = 0; i < items.length; i++) {
      index = (index + step + items.length) % items.length;
      if (!items[index].classList.contains('is-disabled')) break;
    }
    setActive(index, true);
  }

  function onMenuKey(event) {
    if (!current) return;
    switch (event.key) {
      case 'ArrowDown': event.preventDefault(); move(1); break;
      case 'ArrowUp': event.preventDefault(); move(-1); break;
      case 'Home': if (!current.search) { event.preventDefault(); setActive(firstEnabled(), true); } break;
      case 'Enter': {
        event.preventDefault();
        const item = current.items[current.active];
        if (item && !item.classList.contains('is-disabled')) choose(Number(item.dataset.index));
        break;
      }
      case ' ':
        if (!current.search) { event.preventDefault(); const item = current.items[current.active]; if (item) choose(Number(item.dataset.index)); }
        break;
      case 'Escape': event.preventDefault(); event.stopPropagation(); { const b = current.state.button; close(); b.focus(); } break;
      case 'Tab': close(); break;
    }
  }

  function choose(index) {
    const { select, state } = current;
    const changed = native.selectedIndex.get.call(select) !== index;
    native.selectedIndex.set.call(select, index);
    close();
    state.sync();
    state.button.focus();
    if (changed) {
      select.dispatchEvent(new Event('input', { bubbles: true }));
      select.dispatchEvent(new Event('change', { bubbles: true }));
    }
  }

  function position() {
    if (!current) return;
    const { menu, state } = current;
    const rect = state.button.getBoundingClientRect();
    if (!rect.width && !rect.height) { close(); return; }
    const width = Math.max(rect.width, 200);
    const rtl = getComputedStyle(state.button).direction === 'rtl';
    let left = rtl ? rect.right - width : rect.left;
    left = Math.min(Math.max(8, left), window.innerWidth - width - 8);
    menu.style.width = `${width}px`;
    menu.style.left = `${left}px`;
    const below = window.innerHeight - rect.bottom;
    const above = rect.top;
    const wanted = Math.min(menu.scrollHeight || 320, 340);
    if (below < wanted + 12 && above > below) {
      menu.style.top = '';
      menu.style.bottom = `${window.innerHeight - rect.top + 6}px`;
      menu.style.maxHeight = `${Math.min(340, Math.max(160, above - 16))}px`;
      menu.classList.add('is-up');
    } else {
      menu.style.bottom = '';
      menu.style.top = `${rect.bottom + 6}px`;
      menu.style.maxHeight = `${Math.min(340, Math.max(160, below - 16))}px`;
      menu.classList.remove('is-up');
    }
  }

  function open(select) {
    close();
    const state = states.get(select);
    if (!state || select.disabled) return;
    const menu = document.createElement('div');
    menu.className = 'zu-select-menu';
    menu.dir = getComputedStyle(state.button).direction;
    let search = null;
    if (select.options.length > SEARCH_THRESHOLD) {
      const wrap = document.createElement('div');
      wrap.className = 'zu-select-search';
      const icon = document.createElement('i');
      icon.className = 'ti ti-search';
      icon.setAttribute('aria-hidden', 'true');
      search = document.createElement('input');
      search.type = 'search';
      search.autocomplete = 'off';
      search.placeholder = isEnglish() ? 'Search…' : 'بحث…';
      search.addEventListener('input', () => { render(); });
      search.addEventListener('keydown', onMenuKey);
      wrap.append(icon, search);
      menu.append(wrap);
    }
    const list = document.createElement('div');
    list.className = 'zu-select-list';
    list.setAttribute('role', 'listbox');
    menu.append(list);
    const host = select.closest('.modal, dialog') || document.body;
    host.append(menu);
    current = { select, state, menu, list, search, items: [], active: -1 };
    state.button.setAttribute('aria-expanded', 'true');
    state.syncClasses();
    render();
    position();
    requestAnimationFrame(() => menu.classList.add('is-shown'));
    if (search) search.focus();
  }

  function close() {
    if (!current) return;
    const { menu, state } = current;
    current = null;
    menu.remove();
    state.button.setAttribute('aria-expanded', 'false');
    state.syncClasses();
  }

  document.addEventListener('mousedown', event => {
    if (!current) return;
    if (current.menu.contains(event.target) || current.state.button.contains(event.target)) return;
    close();
  }, true);
  window.addEventListener('resize', () => close());
  document.addEventListener('scroll', event => {
    if (!current || current.menu.contains(event.target)) return;
    position();
  }, true);
  document.addEventListener('hide.bs.modal', () => close());

  function scan(root) {
    if (!(root instanceof Element)) return;
    if (root.tagName === 'SELECT') { enhance(root); return; }
    root.querySelectorAll('select').forEach(enhance);
  }
  new MutationObserver(records => {
    for (const record of records) {
      record.addedNodes.forEach(scan);
      record.removedNodes.forEach(node => {
        if (!(node instanceof Element)) return;
        const selects = node.tagName === 'SELECT' ? [node] : [];
        selects.forEach(select => {
          const state = states.get(select);
          if (!state) return;
          if (current && current.select === select) close();
          if (!select.isConnected && state.button.isConnected) state.button.remove();
        });
      });
    }
  }).observe(document.body, { childList: true, subtree: true });
  scan(document.body);

  window.ZuSelect = { enhance, refresh: select => states.get(select)?.sync() };
})();
