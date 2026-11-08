(() => {
  const root = document.getElementById('role-switcher');
  if (!root) return;
  const select = document.getElementById('role-switcher-select');
  const error = document.getElementById('role-switcher-error');
  function setRoleLabel(role) {
    const label = root.dataset[role.replace(/^./, first => first.toLowerCase())] || role;
    document.querySelectorAll('[data-current-role]').forEach(node => { node.textContent = label; });
  }
  async function load() {
    const data = await ZuLms.api.get('/api/v1/context');
    select.replaceChildren();
    data.choices.forEach(choice => {
      const option = document.createElement('option');
      option.value = choice.role;
      option.textContent = root.dataset[choice.role.charAt(0).toLowerCase() + choice.role.slice(1)] ?? choice.role;
      select.append(option);
    });
    select.value = data.current ?? '';
    setRoleLabel(data.current);
    root.classList.toggle('d-none', data.choices.length < 2);
  }
  select.addEventListener('change', async () => {
    select.disabled = true;
    error.textContent = '';
    try {
      const result = await ZuLms.api.post('/api/v1/context', { role: select.value });
      window.location.assign(ZuLmsSession.url(result.redirectUrl));
    } catch {
      error.textContent = root.dataset.error;
      await load().catch(() => {});
    } finally { select.disabled = false; }
  });
  load().catch(() => { error.textContent = root.dataset.error; });
})();
