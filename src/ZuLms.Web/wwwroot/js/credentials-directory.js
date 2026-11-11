(() => {
  const root = document.getElementById('credentials-page');
  if (!root) return;
  const rowsNode = document.getElementById('credentials-rows');
  const message = document.getElementById('credentials-message');
  let page = 1;
  let lastPage = 1;
  let searchTimer;
  let selectedAccount = null;
  let revealUserId = null;
  let faculties = [];
  let managedCenters = [];
  let userPermissions = [];
  const pendingPermissions = new Map();
  const pathUserId = location.pathname.match(/^\/admin\/users\/(\d+)\/?$/)?.[1];
  const showError = (error) => {
    const text = (error && error.message) || root.dataset.error;
    const dialog = root.querySelector('dialog[open]');
    if (dialog) {
      let alert = dialog.querySelector('.dialog-error');
      if (!alert) {
        alert = document.createElement('p');
        alert.className = 'dialog-error alert alert-danger';
        alert.setAttribute('role', 'alert');
        dialog.prepend(alert);
      }
      alert.textContent = text;
    } else {
      message.textContent = text;
      message.classList.remove('d-none');
    }
  };
  const clearError = () => {
    message.textContent = '';
    message.classList.add('d-none');
    root.querySelectorAll('.dialog-error').forEach(element => element.remove());
  };
  async function csrf() { return ZuLms.api.csrf(); }
  function request(path, method = 'GET', body) { return ZuLms.api.request(method, path, body); }
  function textCell(value) {
    const cell = document.createElement('td');
    cell.textContent = value === null || value === undefined || value === '' ? '—' : String(value);
    return cell;
  }
  function actionButton(label, handler) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'btn btn-sm btn-outline-primary me-1 mb-1';
    button.textContent = label;
    button.addEventListener('click', async () => {
      button.disabled = true;
      try { clearError(); await handler(); await load(); }
      catch (error) { showError(error); }
      finally { button.disabled = false; }
    });
    return button;
  }
  async function load() {
    const search = document.getElementById('credentials-search').value;
    const filter = document.getElementById('credentials-filter').value;
    const params = new URLSearchParams({ search, filter, page: String(page) });
    const accountType = document.getElementById('credentials-type-filter').value;
    const role = document.getElementById('credentials-role-filter').value;
    const centerId = document.getElementById('credentials-faculty-filter').value;
    const isActive = document.getElementById('credentials-active-filter').value;
    if (accountType) params.set('accountType', accountType);
    if (role) params.set('role', role);
    if (centerId) params.set('centerId', centerId);
    if (isActive) params.set('isActive', isActive);
    const result = await request(`/api/v1/users/credentials-directory?${params}`);
    lastPage = Math.max(1, Math.ceil(result.total / result.pageSize));
    rowsNode.replaceChildren();
    result.items.forEach(row => {
      const tr = document.createElement('tr');
      const types = { Employee: root.dataset.typeEmployee,
        ExternalInstructor: root.dataset.typeExternalInstructor, Local: root.dataset.typeLocal };
      const statuses = { NotSent: root.dataset.statusNotSent, Sent: root.dataset.statusSent,
        Failed: root.dataset.statusFailed, Uncertain: root.dataset.statusUncertain };
      tr.append(textCell(row.fullNameAr), textCell(row.loginName), textCell(types[row.accountType]),
        textCell(row.facultyNameAr),
        textCell(row.empStatusDesc ? `${row.empStatusDesc}${row.empStatus == null ? '' : ` (${row.empStatus})`}` : row.empStatus),
        textCell(row.isActive ? root.dataset.yes : root.dataset.no),
        textCell(row.firstLoginCompleted ? root.dataset.yes : root.dataset.no),
        textCell(row.effectiveEmail), textCell(statuses[row.emailStatus]),
        textCell(row.lastLoginAt ? ZuLmsDates.display(row.lastLoginAt) : null));
      const actions = document.createElement('td');
      actions.append(actionButton(root.dataset.reveal, async () => {
        revealUserId = row.id;
        revealDialog.showModal();
        await loadReveal();
      }));
      actions.append(actionButton(root.dataset.change, async () => {
        selectedAccount = await request(`/api/v1/users/${row.id}`);
        changeForm.reset();
        document.getElementById('change-required').disabled = selectedAccount.accountType !== 'Employee';
        changeDialog.showModal();
      }));
      actions.append(actionButton(root.dataset.edit, async () => {
        selectedAccount = await request(`/api/v1/users/${row.id}`);
        editForm.elements.fullNameEn.value = selectedAccount.fullNameEn ?? '';
        editForm.elements.emailOverride.value = selectedAccount.emailOverride ?? '';
        editForm.elements.preferredLanguage.value = selectedAccount.preferredLanguage;
        editForm.elements.accessOverrideAllow.checked = selectedAccount.accessOverrideAllow;
        editForm.elements.accessOverrideAllow.disabled = selectedAccount.accountType !== 'Employee';
        editForm.elements.accessOverrideReason.value = selectedAccount.accessOverrideReason ?? '';
        editDialog.showModal();
      }));
      actions.append(actionButton(root.dataset.roles, async () => {
        selectedAccount = await request(`/api/v1/users/${row.id}`);
        [faculties, managedCenters] = await Promise.all([request('/api/v1/users/faculties'),
          request('/api/v1/users/centers')]);
        renderRoles();
        rolesDialog.showModal();
      }));
      actions.append(actionButton(root.dataset.permissions, async () => {
        selectedAccount = await request(`/api/v1/users/${row.id}`);
        userPermissions = await request(`/api/v1/users/${row.id}/permissions`);
        pendingPermissions.clear();
        document.getElementById('permissions-search').value = '';
        renderPermissions();
        permissionsDialog.showModal();
      }));
      actions.append(actionButton(row.isActive ? root.dataset.disable : root.dataset.restore,
        async () => {
          if (row.isActive) {
            const reason = await ZuLms.ui.prompt(root.dataset.disableReason);
            if (!reason || !reason.trim()) return;
            await request(`/api/v1/users/${row.id}/deactivate`, 'POST',
              { expectedVersion: row.version, reason: reason.trim() });
          } else if (await ZuLms.ui.confirm(root.dataset.restoreConfirm)) {
            await request(`/api/v1/users/${row.id}/restore`, 'POST',
              { expectedVersion: row.version });
          }
        }));
      if (row.accountType === 'Employee' && row.isActive && !row.firstLoginCompleted &&
        row.effectiveEmail && !row.initialEmailPending) {
        if (row.emailStatus === 'Uncertain') {
          actions.append(actionButton(root.dataset.markSent, async () => {
            if (await ZuLms.ui.confirm(root.dataset.confirmSent))
              await request(`/api/v1/credentials/${row.id}/resolve-uncertain`, 'POST', { received: true });
          }));
          actions.append(actionButton(root.dataset.markFailed, async () => {
            if (await ZuLms.ui.confirm(root.dataset.confirmFailed))
              await request(`/api/v1/credentials/${row.id}/resolve-uncertain`, 'POST', { received: false });
          }));
        } else {
          actions.append(actionButton(row.emailStatus === 'Sent' ? root.dataset.resend : root.dataset.send,
            async () => {
              const confirmResend = row.emailStatus === 'Sent';
              if (confirmResend && !await ZuLms.ui.confirm(root.dataset.confirmResend)) return;
              await request(`/api/v1/credentials/${row.id}/send-initial`, 'POST', { confirmResend });
            }));
        }
      }
      tr.append(actions);
      rowsNode.append(tr);
    });
    document.getElementById('credentials-page-number').textContent = `${page} / ${lastPage}`;
    document.getElementById('credentials-prev').disabled = page === 1;
    document.getElementById('credentials-next').disabled = page >= lastPage;
  }
  document.getElementById('credentials-search').addEventListener('input', () => {
    clearTimeout(searchTimer);
    searchTimer = setTimeout(() => { page = 1; load().catch(showError); }, 300);
  });
  document.getElementById('credentials-filter').addEventListener('change', () => {
    page = 1; load().catch(showError);
  });
  ['credentials-type-filter', 'credentials-role-filter', 'credentials-faculty-filter',
    'credentials-active-filter'].forEach(id => document.getElementById(id).addEventListener('change', () => {
      page = 1; load().catch(showError);
    }));
  document.getElementById('credentials-prev').addEventListener('click', () => {
    if (page > 1) { page--; load().catch(showError); }
  });
  document.getElementById('credentials-next').addEventListener('click', () => {
    if (page < lastPage) { page++; load().catch(showError); }
  });
  document.getElementById('bulk-send').addEventListener('click', async () => {
    try {
      clearError();
      const summary = await request('/api/v1/credentials/send-initial/preview');
      const text = `${root.dataset.bulkConfirm}\n${root.dataset.eligible}: ${summary.eligible}\n` +
        `${root.dataset.sent}: ${summary.sent}\n${root.dataset.uncertain}: ${summary.uncertain}\n` +
        `${root.dataset.noEmail}: ${summary.noEmail}\n${root.dataset.pending}: ${summary.pending}\n` +
        `${root.dataset.ineligible}: ${summary.ineligible}`;
      if (summary.eligible > 0 && await ZuLms.ui.confirm(text)) {
        await request('/api/v1/credentials/send-initial/bulk', 'POST');
        await load();
      }
    } catch (error) { showError(error); }
  });
  const localDialog = document.getElementById('local-dialog');
  const localForm = document.getElementById('local-form');
  const editDialog = document.getElementById('edit-dialog');
  const editForm = document.getElementById('edit-form');
  const changeDialog = document.getElementById('change-dialog');
  const changeForm = document.getElementById('change-form');
  const revealDialog = document.getElementById('reveal-dialog');
  const reauthForm = document.getElementById('reauth-form');
  const revealedValues = document.getElementById('revealed-values');
  const rolesDialog = document.getElementById('roles-dialog');
  const rolesForm = document.getElementById('roles-form');
  const permissionsDialog = document.getElementById('permissions-dialog');
  document.getElementById('local-open').addEventListener('click', () => localDialog.showModal());
  document.getElementById('local-cancel').addEventListener('click', () => localDialog.close());
  localForm.addEventListener('submit', async event => {
    event.preventDefault();
    const data = Object.fromEntries(new FormData(localForm).entries());
    if (!data.fullNameEn) data.fullNameEn = null;
    try {
      clearError();
      await request('/api/v1/users/local', 'POST', data);
      localForm.reset(); localDialog.close(); page = 1; await load();
    } catch (error) { showError(error); }
  });
  document.getElementById('edit-cancel').addEventListener('click', () => editDialog.close());
  editForm.addEventListener('submit', async event => {
    event.preventDefault();
    if (!selectedAccount) return;
    const value = {
      expectedVersion: selectedAccount.version,
      fullNameEn: editForm.elements.fullNameEn.value || null,
      emailOverride: editForm.elements.emailOverride.value || null,
      preferredLanguage: editForm.elements.preferredLanguage.value,
      accessOverrideAllow: editForm.elements.accessOverrideAllow.checked,
      accessOverrideReason: editForm.elements.accessOverrideReason.value || null
    };
    try {
      clearError();
      await request(`/api/v1/users/${selectedAccount.id}`, 'PUT', value);
      editDialog.close(); await load();
    } catch (error) { showError(error); }
  });
  document.getElementById('change-cancel').addEventListener('click', () => changeDialog.close());
  changeForm.addEventListener('submit', async event => {
    event.preventDefault();
    if (!selectedAccount) return;
    const value = {
      expectedVersion: selectedAccount.version,
      newPassword: changeForm.elements.newPassword.value,
      requireChangeAtNextLogin: changeForm.elements.requireChangeAtNextLogin.checked
    };
    try {
      clearError();
      await request(`/api/v1/credentials/${selectedAccount.id}/password`, 'PUT', value);
      changeForm.reset(); changeDialog.close(); await load();
    } catch (error) { showError(error); }
  });
  async function loadReveal() {
    if (!revealUserId) return;
    try {
      const values = await request(`/api/v1/credentials/${revealUserId}/reveal`);
      document.getElementById('revealed-initial').value = values.initialPassword ?? '';
      document.getElementById('revealed-current').value = values.currentPassword ?? '';
      reauthForm.classList.add('d-none');
      revealedValues.classList.remove('d-none');
    } catch (error) {
      if (error.code === 'REAUTH_REQUIRED') {
        reauthForm.classList.remove('d-none');
        revealedValues.classList.add('d-none');
      } else { revealDialog.close(); throw error; }
    }
  }
  reauthForm.addEventListener('submit', async event => {
    event.preventDefault();
    try {
      clearError();
      const password = document.getElementById('reauth-password').value;
      await request('/api/v1/credentials/reauthenticate', 'POST', { password });
      document.getElementById('reauth-password').value = '';
      await loadReveal();
    } catch (error) { showError(error); }
  });
  function clearReveal() {
    revealUserId = null;
    document.getElementById('reauth-password').value = '';
    document.getElementById('revealed-initial').value = '';
    document.getElementById('revealed-current').value = '';
    revealedValues.classList.add('d-none');
    reauthForm.classList.add('d-none');
  }
  document.getElementById('reveal-close').addEventListener('click', () => { clearReveal(); revealDialog.close(); });
  revealDialog.addEventListener('close', clearReveal);
  document.querySelectorAll('.copy-credential').forEach(button => {
    button.addEventListener('click', async () => {
      const value = document.getElementById(button.dataset.target).value;
      try { await navigator.clipboard.writeText(value); }
      catch (error) { showError(error); }
    });
  });
  function roleLabel(code) {
    const labels = { Admin: root.dataset.roleAdmin, Quality: root.dataset.roleQuality,
      Dean: root.dataset.roleDean, Member: root.dataset.roleMember,
      CenterManager: root.dataset.roleCenterManager,
      Instructor: root.dataset.roleInstructor,
      ExternalInstructor: root.dataset.roleExternalInstructor };
    return labels[code] ?? code;
  }
  function renderRoles() {
    const list = document.getElementById('assigned-roles');
    list.replaceChildren();
    selectedAccount.roles.forEach(assignment => {
      const item = document.createElement('li');
      item.className = 'list-group-item d-flex justify-content-between align-items-center gap-2';
      const faculty = assignment.role === 'CenterManager'
        ? managedCenters.find(x => x.id === assignment.scopeCenterId)
        : faculties.find(x => x.id === assignment.scopeCenterId);
      const label = document.createElement('span');
      label.textContent = roleLabel(assignment.role) + (faculty ? ` — ${ZuLmsI18n.field(faculty, 'name')}` : '');
      item.append(label);
      if (['Admin', 'Quality', 'Dean', 'CenterManager', 'Member'].includes(assignment.role)) {
        const remove = actionButton(root.dataset.removeRole, async () => {
          await request(`/api/v1/users/${selectedAccount.id}/roles`, 'PUT',
            { expectedVersion: selectedAccount.version, role: assignment.role,
              scopeCenterId: assignment.scopeCenterId, enabled: false });
          selectedAccount = await request(`/api/v1/users/${selectedAccount.id}`);
          renderRoles();
        });
        item.append(remove);
      }
      list.append(item);
    });
    const code = rolesForm.elements.role;
    for (const option of code.options) {
      option.disabled = selectedAccount.accountType === 'Local' && option.value !== 'Admin' ||
        selectedAccount.accountType === 'ExternalInstructor' && ['Member', 'CenterManager'].includes(option.value);
    }
    if (code.selectedOptions[0].disabled) code.value = 'Admin';
    renderScopeOptions();
  }
  // Dean -> faculty list; center manager -> administrative centers with their current manager.
  function renderScopeOptions() {
    const role = rolesForm.elements.role.value;
    const manager = role === 'CenterManager';
    const scoped = role === 'Dean' || manager;
    const scopeSelect = rolesForm.elements.scopeCenterId;
    const label = document.getElementById('role-scope-label');
    label.textContent = manager ? label.dataset.center : label.dataset.faculty;
    scopeSelect.replaceChildren();
    const blank = document.createElement('option');
    blank.value = '';
    scopeSelect.append(blank);
    const rows = manager ? managedCenters : faculties.filter(faculty => Number(faculty.id) > 0);
    rows.forEach(row => {
      const option = document.createElement('option');
      option.value = String(row.id);
      option.textContent = ZuLmsI18n.field(row, 'name') +
        (manager && row.managerNameAr ? ` — المدير الحالي: ${row.managerNameAr}` : '');
      scopeSelect.append(option);
    });
    scopeSelect.disabled = !scoped;
    scopeSelect.required = scoped;
    updateScopeHint();
  }
  function updateScopeHint() {
    const hint = document.getElementById('role-scope-hint');
    hint.textContent = '';
    if (rolesForm.elements.role.value !== 'CenterManager') return;
    const center = managedCenters.find(x => String(x.id) === rolesForm.elements.scopeCenterId.value);
    if (center && center.managerNameAr && center.managerUserId !== selectedAccount?.id)
      hint.textContent = `سيحلّ محل المدير الحالي (${center.managerNameAr}) — لكل مركز مدير واحد فقط.`;
  }
  rolesForm.elements.role.addEventListener('change', renderScopeOptions);
  rolesForm.elements.scopeCenterId.addEventListener('change', updateScopeHint);
  rolesForm.addEventListener('submit', async event => {
    event.preventDefault();
    if (!selectedAccount) return;
    const role = rolesForm.elements.role.value;
    const scopeCenterId = role === 'Dean' || role === 'CenterManager'
      ? Number(rolesForm.elements.scopeCenterId.value) : null;
    if (role === 'CenterManager') {
      const center = managedCenters.find(x => x.id === scopeCenterId);
      if (center && center.managerNameAr && center.managerUserId !== selectedAccount.id &&
        !await ZuLms.ui.confirm(`سيتم استبدال المدير الحالي (${center.managerNameAr}) لـ ${center.nameAr}. متابعة؟`)) return;
    }
    try {
      clearError();
      await request(`/api/v1/users/${selectedAccount.id}/roles`, 'PUT',
        { expectedVersion: selectedAccount.version, role, scopeCenterId, enabled: true });
      selectedAccount = await request(`/api/v1/users/${selectedAccount.id}`);
      managedCenters = await request('/api/v1/users/centers');
      renderRoles();
      await load();
    } catch (error) { showError(error); }
  });
  document.getElementById('roles-close').addEventListener('click', () => rolesDialog.close());
  function renderPermissions() {
    const term = document.getElementById('permissions-search').value.trim().toLowerCase();
    const tbody = document.getElementById('permissions-rows');
    tbody.replaceChildren();
    userPermissions.filter(item => !term || `${item.key} ${ZuLmsI18n.field(item, 'name')} ${item.nameEn ?? ''}`
      .toLowerCase().includes(term)).forEach(item => {
      const row = document.createElement('tr');
      const name = textCell(ZuLmsI18n.field(item, 'name'));
      const key = document.createElement('small');
      key.className = 'd-block text-muted';
      key.textContent = item.key;
      name.append(key);
      const proposedMode = pendingPermissions.has(item.key) ? pendingPermissions.get(item.key) : item.mode;
      const proposedEffective = proposedMode === 'Allow' ? true : proposedMode === 'Deny' ? false : item.inheritedEffective;
      const effective = textCell(proposedEffective ? root.dataset.yes : root.dataset.no);
      const sources = document.createElement('small');
      sources.className = 'd-block text-muted';
      sources.textContent = item.roleSources?.length ? item.roleSources.map(source =>
        `${roleLabel(source.role)}${source.scopeCenterNameAr ? ` — ${source.scopeCenterNameAr}` : ''}`)
        .join('، ') : root.dataset.inherit;
      effective.append(sources);
      const modeCell = document.createElement('td');
      const select = document.createElement('select');
      select.className = 'form-select form-select-sm';
      for (const [value, label] of [['', root.dataset.inherit], ['Allow', root.dataset.allow],
        ['Deny', root.dataset.deny]]) {
        const option = document.createElement('option');
        option.value = value;
        option.textContent = label;
        select.append(option);
      }
      select.value = proposedMode ?? '';
      select.addEventListener('change', () => {
        const value = select.value || null;
        if (value === item.mode) pendingPermissions.delete(item.key);
        else pendingPermissions.set(item.key, value);
        renderPermissions();
      });
      modeCell.append(select);
      row.append(name, effective, modeCell);
      tbody.append(row);
    });
    const changed = pendingPermissions.size > 0;
    document.getElementById('permissions-preview').disabled = !changed;
    document.getElementById('permissions-cancel-changes').disabled = !changed;
  }
  document.getElementById('permissions-search').addEventListener('input', renderPermissions);
  document.getElementById('permissions-cancel-changes').addEventListener('click', () => {
    pendingPermissions.clear(); renderPermissions();
  });
  const permissionsPreviewDialog = document.getElementById('permissions-preview-dialog');
  document.getElementById('permissions-preview').addEventListener('click', () => {
    const list = document.getElementById('permissions-preview-list');
    list.replaceChildren();
    pendingPermissions.forEach((mode, key) => {
      const item = userPermissions.find(x => x.key === key);
      const row = document.createElement('li'); row.className = 'list-group-item';
      row.textContent = `${ZuLmsI18n.field(item, 'name', key)} (${key}): ${mode === 'Allow' ? root.dataset.allow : mode === 'Deny' ? root.dataset.deny : root.dataset.inherit}`;
      list.append(row);
    });
    permissionsPreviewDialog.showModal();
  });
  document.getElementById('permissions-preview-cancel').addEventListener('click', () => permissionsPreviewDialog.close());
  document.getElementById('permissions-apply').addEventListener('click', async () => {
    const button = document.getElementById('permissions-apply'); button.disabled = true;
    try {
      for (const [key, mode] of pendingPermissions) {
        await request(`/api/v1/users/${selectedAccount.id}/permissions/${encodeURIComponent(key)}`,
          'PUT', { expectedVersion: selectedAccount.version, mode });
        selectedAccount = await request(`/api/v1/users/${selectedAccount.id}`);
      }
      pendingPermissions.clear();
      userPermissions = await request(`/api/v1/users/${selectedAccount.id}/permissions`);
      permissionsPreviewDialog.close(); renderPermissions(); await load();
    } catch (error) { showError(error); }
    finally { button.disabled = false; }
  });
  document.getElementById('permissions-close').addEventListener('click', () => {
    pendingPermissions.clear(); permissionsDialog.close();
  });
  async function start() {
    faculties = await request('/api/v1/users/faculties');
    const facultyFilter = document.getElementById('credentials-faculty-filter');
    faculties.forEach(faculty => { const option = document.createElement('option'); option.value = String(faculty.id); option.textContent = ZuLmsI18n.field(faculty, 'name'); facultyFilter.append(option); });
    if (!pathUserId) { await load(); return; }
    selectedAccount = await request(`/api/v1/users/${pathUserId}`);
    document.getElementById('credentials-search').value = selectedAccount.loginName;
    await load();
    userPermissions = await request(`/api/v1/users/${pathUserId}/permissions`);
    document.getElementById('permissions-search').value = '';
    renderPermissions();
    permissionsDialog.showModal();
  }
  start().catch(showError);
})();

