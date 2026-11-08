(() => {
  'use strict';
  const root = document.getElementById('hints-page');
  if (!root) return;
  const $ = id => document.getElementById(id);
  const error = $('hints-error');
  const success = $('hints-success');
  const rowsHost = $('hints-rows');
  const pageLabel = $('hints-page-number');
  let page = 1, pages = 1, editingId = null, searchTimer;

  const text = key => root.dataset[key] || '';
  const showError = e => { error.textContent = e.message || text('error'); error.classList.remove('d-none'); };
  const showSuccess = message => {
    success.textContent = message;
    success.classList.remove('d-none');
    setTimeout(() => success.classList.add('d-none'), 3500);
  };

  function resetForm() {
    editingId = null;
    $('hint-form').reset();
    $('hint-enabled').checked = true;
    $('hint-form-title').textContent = text('new');
    $('hint-cancel').classList.add('d-none');
    $('hint-field-key').readOnly = false;
  }

  function fillForm(row) {
    editingId = row.id;
    $('hint-field-key').value = row.fieldKey;
    $('hint-context-key').value = row.contextKey;
    $('hint-ar').value = row.hintAr;
    $('hint-en').value = row.hintEn || '';
    $('hint-enabled').checked = !!row.isEnabled;
    $('hint-cancel').classList.remove('d-none');
    $('hint-field-key').focus();
  }

  function render(data) {
    rowsHost.replaceChildren();
    const rows = data.items || [];
    if (rows.length === 0) {
      const tr = document.createElement('tr');
      const td = document.createElement('td');
      td.colSpan = 5;
      td.className = 'text-muted';
      td.textContent = text('empty');
      tr.append(td);
      rowsHost.append(tr);
    }
    rows.forEach(row => {
      const tr = document.createElement('tr');
      const cells = [row.fieldKey, row.contextKey, row.hintAr];
      cells.forEach(value => {
        const td = document.createElement('td');
        td.textContent = value || '';
        tr.append(td);
      });
      const enabled = document.createElement('td');
      enabled.textContent = row.isEnabled ? '✓' : '—';
      tr.append(enabled);
      const actions = document.createElement('td');
      actions.className = 'table-actions';
      const edit = document.createElement('button');
      edit.type = 'button';
      edit.className = 'btn btn-sm btn-outline-primary';
      edit.textContent = '✎';
      edit.addEventListener('click', () => fillForm(row));
      const remove = document.createElement('button');
      remove.type = 'button';
      remove.className = 'btn btn-sm btn-outline-danger';
      remove.textContent = text('confirm');
      remove.addEventListener('click', () => deactivate(row.id));
      actions.append(edit, remove);
      tr.append(actions);
      rowsHost.append(tr);
    });
    pages = Math.max(1, Math.ceil((data.total || 0) / (data.pageSize || 20)));
    pageLabel.textContent = `${data.page || page} / ${pages}`;
  }

  async function load() {
    error.classList.add('d-none');
    try {
      const query = '?page=' + page + '&q=' + encodeURIComponent($('hints-search').value.trim());
      render(await ZuLms.api.get('/api/v1/hints' + query));
    } catch (e) {
      showError(e);
    }
  }

  async function deactivate(id) {
    try {
      await ZuLms.api.del('/api/v1/hints/' + id);
      showSuccess(text('deactivated'));
      if (editingId === id) resetForm();
      await load();
    } catch (e) {
      showError(e);
    }
  }

  document.getElementById('hint-form').addEventListener('submit', async event => {
    event.preventDefault();
    error.classList.add('d-none');
    const payload = {
      fieldKey: $('hint-field-key').value.trim(),
      contextKey: $('hint-context-key').value.trim(),
      hintAr: $('hint-ar').value.trim(),
      hintEn: $('hint-en').value.trim() || null,
      isEnabled: $('hint-enabled').checked
    };
    const submit = $('hint-save');
    submit.disabled = true;
    try {
      if (editingId === null) await ZuLms.api.post('/api/v1/hints', payload);
      else await ZuLms.api.put('/api/v1/hints/' + editingId, payload);
      showSuccess(text('saved'));
      resetForm();
      await load();
    } catch (e) {
      showError(e);
      if (e.fieldErrors) ZuLms.applyFieldErrors(e.fieldErrors, $('hint-form'));
    } finally {
      submit.disabled = false;
    }
  });

  $('hint-cancel').addEventListener('click', resetForm);
  $('hints-refresh').addEventListener('click', load);
  $('hints-prev').addEventListener('click', () => { if (page > 1) { page--; load(); } });
  $('hints-next').addEventListener('click', () => { if (page < pages) { page++; load(); } });
  $('hints-search').addEventListener('input', () => {
    clearTimeout(searchTimer);
    searchTimer = setTimeout(() => { page = 1; load(); }, 250);
  });

  resetForm();
  load();
})();
