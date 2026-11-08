(() => {
  const root = document.getElementById('settings-page');
  if (!root) return;
  const groupsNode = document.getElementById('settings-groups');
  const message = document.getElementById('settings-message');
  const english = document.documentElement.lang === 'en';
  const groupNames = {
    access: ['الوصول', 'Access', 'ti-lock'], attendance: ['الحضور', 'Attendance', 'ti-calendar-check'],
    audit: ['سجل التدقيق', 'Audit', 'ti-history'], auth: ['المصادقة', 'Authentication', 'ti-shield-lock'],
    cert: ['الشهادات', 'Certificates', 'ti-certificate'], courses: ['الدورات', 'Courses', 'ti-book-2'],
    credentials: ['بيانات الدخول', 'Credentials', 'ti-key'], directory: ['الدليل', 'Directory', 'ti-address-book'],
    email: ['البريد الإلكتروني', 'Email', 'ti-mail'], evaluation: ['التقييم', 'Evaluation', 'ti-clipboard-check'],
    files: ['الملفات', 'Files', 'ti-files'], org: ['الهيكل التنظيمي', 'Organization', 'ti-building'],
    procert: ['الشهادات المهنية', 'Professional certificates', 'ti-award'], sync: ['المزامنة', 'Sync', 'ti-refresh'],
    ui: ['الواجهة', 'Interface', 'ti-layout']
  };
  const showError = target => {
    target.textContent = root.dataset.error;
    target.classList.remove('d-none');
  };
  async function csrf() { return ZuLms.api.csrf(); }
  function request(path, method = 'GET', body) { return ZuLms.api.request(method, path, body); }
  function label(setting) {
    return english && setting.labelEn ? setting.labelEn : setting.labelAr;
  }
  function description(setting) {
    return english && setting.descriptionEn ? setting.descriptionEn : setting.descriptionAr;
  }
  function inputFor(setting) {
    let input;
    if (setting.type === 'Bool') {
      input = document.createElement('select');
      for (const [value, text] of [['true', root.dataset.true], ['false', root.dataset.false]]) {
        const option = document.createElement('option');
        option.value = value;
        option.textContent = text;
        input.append(option);
      }
      input.value = setting.value?.toLowerCase() ?? 'false';
    } else if (setting.type === 'Json') {
      input = document.createElement('textarea');
      input.rows = 4;
      input.value = setting.value ?? '';
    } else {
      input = document.createElement('input');
      input.type = setting.isSecret ? 'password' : setting.type === 'Int' || setting.type === 'Decimal' ? 'number' : 'text';
      if (setting.type === 'Decimal') input.step = 'any';
      if (setting.minValue !== null) input.min = String(setting.minValue);
      if (setting.maxValue !== null) input.max = String(setting.maxValue);
      input.value = setting.isSecret ? '' : setting.value ?? '';
      if (setting.isSecret) input.autocomplete = 'new-password';
    }
    input.className = 'form-control';
    input.name = 'value';
    input.id = `setting-${setting.key.replace(/[^a-z0-9-]/gi, '-')}`;
    return input;
  }
  function render(settings) {
    groupsNode.replaceChildren();
    const nav = document.getElementById('settings-nav'); nav.replaceChildren();
    const byGroup = new Map();
    settings.forEach(setting => {
      if (!byGroup.has(setting.group)) byGroup.set(setting.group, []);
      byGroup.get(setting.group).push(setting);
    });
    for (const [group, values] of byGroup) {
      const info = groupNames[group] || [group, group, 'ti-settings'];
      const groupTitle = info[english ? 1 : 0];
      const anchor = `settings-group-${group.replace(/[^a-z0-9-]/gi, '-')}`;
      const navLink = document.createElement('a');
      navLink.href = `#${anchor}`; navLink.textContent = groupTitle;
      nav.append(navLink);
      const section = document.createElement('section');
      section.className = 'card mb-4 settings-group'; section.id = anchor;
      const header = document.createElement('div'); header.className = 'card-header d-flex align-items-center gap-2';
      const icon = document.createElement('i'); icon.className = `ti ${info[2]}`; icon.setAttribute('aria-hidden', 'true');
      const heading = document.createElement('h2'); heading.className = 'h5 mb-0'; heading.textContent = groupTitle;
      const count = document.createElement('span'); count.className = 'badge text-bg-light ms-auto';
      count.textContent = String(values.length);
      header.append(icon, heading, count);
      const body = document.createElement('div');
      body.className = 'card-body row g-3';
      values.forEach(setting => {
        const form = document.createElement('form');
        form.className = 'col-12 col-lg-6 settings-field';
        const fieldLabel = document.createElement('label');
        fieldLabel.className = 'form-label fw-semibold';
        fieldLabel.textContent = label(setting);
        const input = inputFor(setting);
        fieldLabel.htmlFor = input.id;
        form.append(fieldLabel);
        const help = document.createElement('div');
        help.className = 'form-text mb-2';
        const limit = [setting.minValue === null ? '' : `${root.dataset.min}: ${setting.minValue}`,
          setting.maxValue === null ? '' : `${root.dataset.max}: ${setting.maxValue}`]
          .filter(Boolean).join(' · ');
        const detail = description(setting);
        help.textContent = [detail?.trim() === label(setting)?.trim() ? '' : detail,
          limit, setting.isSecret ? root.dataset.secret : '']
          .filter(Boolean).join(' · ');
        const button = document.createElement('button');
        button.type = 'submit';
        button.className = 'btn btn-outline-primary btn-sm';
        button.textContent = root.dataset.save;
        if (setting.type === 'Json') {
          form.append(input, button);
        } else {
          const inputGroup = document.createElement('div'); inputGroup.className = 'input-group';
          inputGroup.append(input, button); form.append(inputGroup);
        }
        if (help.textContent) form.append(help);
        const error = document.createElement('p');
        error.className = 'alert alert-danger d-none';
        error.setAttribute('role', 'alert');
        form.append(error);
        form.addEventListener('submit', async event => {
          event.preventDefault();
          error.classList.add('d-none');
          button.disabled = true;
          try {
            const value = input.value;
            if (setting.isSecret && setting.key !== 'auth.shared_initial_password' && !value)
              return;
            if (setting.key === 'auth.shared_initial_password') {
              await request('/api/v1/credentials/shared-initial', 'PUT',
                { expectedVersion: setting.version, password: value });
            } else {
              await request(`/api/v1/settings/${encodeURIComponent(setting.key)}`, 'PUT',
                { expectedVersion: setting.version, value });
            }
            input.value = setting.isSecret ? '' : value;
            await load();
          } catch { showError(error); }
          finally { button.disabled = false; }
        });
        body.append(form);
      });
      section.append(header, body);
      groupsNode.append(section);
    }
  }
  async function load() {
    const settings = await request('/api/v1/settings');
    render(settings);
    message.classList.add('d-none');
  }
  load().catch(() => showError(message));
})();
