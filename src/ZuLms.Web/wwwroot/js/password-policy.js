(() => {
  const root = document.getElementById('policy-page');
  if (!root) return;
  const form = document.getElementById('policy-form');
  const message = document.getElementById('policy-message');
  const fields = ['allowSpaces', 'allowArabic', 'blockCommon', 'blockContext',
    'requireUpper', 'requireLower', 'requireDigit', 'requireSymbol'];
  let page = 1;
  let lastPage = 1;
  let searchTimer;
  const showError = () => {
    message.textContent = root.dataset.error;
    message.classList.remove('d-none');
  };
  const clearError = () => {
    message.textContent = '';
    message.classList.add('d-none');
  };
  async function csrf() { return ZuLms.api.csrf(); }
  function request(path, method = 'GET', body) { return ZuLms.api.request(method, path, body); }
  async function loadPolicy() {
    const value = await request('/api/v1/password-policy');
    form.elements.minLength.value = value.minLength;
    form.elements.maxLength.value = value.maxLength;
    form.elements.rotationDays.value = value.rotationDays ?? '';
    fields.forEach(key => { form.elements[key].checked = value[key]; });
  }
  async function loadBlocked() {
    const term = document.getElementById('blocked-search').value;
    const result = await request(`/api/v1/password-policy/blocked?search=${encodeURIComponent(term)}&page=${page}`);
    const rows = result.items;
    lastPage = Math.max(1, Math.ceil(result.total / result.pageSize));
    const tbody = document.getElementById('blocked-rows');
    tbody.replaceChildren();
    rows.forEach(row => {
      const tr = document.createElement('tr');
      const word = document.createElement('td');
      word.textContent = row.passwordNormalized;
      const source = document.createElement('td');
      source.textContent = row.source;
      const action = document.createElement('td');
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'btn btn-sm btn-outline-danger';
      button.textContent = root.dataset.delete;
      button.addEventListener('click', async () => {
        if (!await ZuLms.ui.confirm(root.dataset.delete)) return;
        try { clearError(); await request(`/api/v1/password-policy/blocked/${row.id}`, 'DELETE'); await loadBlocked(); }
        catch { showError(); }
      });
      action.append(button);
      tr.append(word, source, action);
      tbody.append(tr);
    });
    document.getElementById('blocked-page').textContent = String(page);
    document.getElementById('blocked-prev').disabled = page === 1;
    document.getElementById('blocked-next').disabled = page >= lastPage;
  }
  form.addEventListener('submit', async event => {
    event.preventDefault();
    const value = {
      minLength: Number(form.elements.minLength.value),
      maxLength: Number(form.elements.maxLength.value),
      rotationDays: form.elements.rotationDays.value ? Number(form.elements.rotationDays.value) : null
    };
    fields.forEach(key => { value[key] = form.elements[key].checked; });
    try { clearError(); await request('/api/v1/password-policy', 'PUT', value); }
    catch { showError(); }
  });
  document.getElementById('blocked-add-form').addEventListener('submit', async event => {
    event.preventDefault();
    const input = document.getElementById('blocked-password');
    try { clearError(); await request('/api/v1/password-policy/blocked', 'POST', { password: input.value });
      input.value = ''; page = 1; await loadBlocked(); }
    catch { showError(); }
  });
  document.getElementById('blocked-search').addEventListener('input', () => {
    clearTimeout(searchTimer);
    searchTimer = setTimeout(() => { page = 1; loadBlocked().catch(showError); }, 300);
  });
  document.getElementById('blocked-prev').addEventListener('click', () => {
    if (page > 1) { page--; loadBlocked().catch(showError); }
  });
  document.getElementById('blocked-next').addEventListener('click', () => {
    if (page < lastPage) { page++; loadBlocked().catch(showError); }
  });
  Promise.all([loadPolicy(), loadBlocked()]).catch(showError);
})();
