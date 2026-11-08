(() => {
  const root = document.getElementById('roles-page'); if (!root) return;
  const $ = id => document.getElementById(`roles-${id}`);
  let matrix;
  let selected = new Set();
  const english = document.documentElement.lang === 'en';
  const groupNames = {
    attendance: ['الحضور', 'Attendance'], audit: ['التدقيق', 'Audit'], cancellation: ['الإلغاء', 'Cancellation'],
    centers: ['الكليات والمراكز', 'Centers'], cert_template: ['قوالب الشهادات', 'Certificate templates'],
    certificates: ['الشهادات', 'Certificates'], course: ['الدورات', 'Courses'], credentials: ['بيانات الدخول', 'Credentials'],
    evalform: ['نماذج التقييم', 'Evaluation forms'], evaluation: ['التقييم', 'Evaluation'], exam: ['الامتحانات', 'Exams'],
    extinstr: ['المحاضرون الخارجيون', 'External instructors'], faculty: ['الكليات', 'Faculties'],
    hints: ['الإرشادات', 'Hints'], impact: ['الأثر التدريبي', 'Impact'], lecture: ['المحاضرات', 'Lectures'],
    material: ['المواد', 'Materials'], need: ['الاحتياجات', 'Needs'], page: ['الصفحات', 'Pages'],
    participants: ['المشاركون', 'Participants'], password_policy: ['سياسة كلمة المرور', 'Password policy'],
    procert: ['الشهادات المهنية', 'Professional certificates'], profile: ['الملفات الشخصية', 'Profiles'],
    proposal: ['المقترحات', 'Proposals'], roles: ['الأدوار', 'Roles'], settings: ['الإعدادات', 'Settings'],
    stats: ['الإحصائيات', 'Statistics'], sync: ['المزامنة', 'Sync'], tracks: ['المحاور', 'Tracks'],
    user: ['المستخدمون', 'Users'], vault: ['المحفوظات', 'Vault']
  };
  const scopeNames = {
    Global: ['على مستوى الجامعة', 'University-wide', 'primary'],
    OwnFaculty: ['كليته', 'Own faculty', 'info'],
    InstructedCourse: ['دوراته كمحاضر', 'Instructed courses', 'success'],
    Self: ['نفسه فقط', 'Self only', 'secondary']
  };
  const updateCount = () => {
    $('selected-count').textContent = `${english ? 'Selected permissions' : 'الصلاحيات المحددة'}: ${selected.size}`;
  };
  const fail = e => { $('error').textContent = e?.message || e?.code || 'تعذّر إكمال الطلب'; $('error').classList.remove('d-none'); };
  const render = () => {
    if (!matrix) return;
    const role = matrix.roles.find(x => x.code === $('role').value);
    if (!role) return;
    const term = $('search').value.trim().toLowerCase();
    $('rows').replaceChildren();
    const items = matrix.permissions.filter(x => !term || `${x.key} ${ZuLmsI18n.field(x, 'name')} ${x.nameEn || ''}`.toLowerCase().includes(term));
    const groups = new Map();
    items.forEach(item => {
      const prefix = item.key.split('.')[0];
      if (!groups.has(prefix)) groups.set(prefix, []);
      groups.get(prefix).push(item);
    });
    for (const [prefix, permissions] of groups) {
      const header = document.createElement('tr'); header.className = 'roles-group-row';
      const title = document.createElement('th'); title.colSpan = 3;
      title.textContent = (groupNames[prefix] || [prefix, prefix])[english ? 1 : 0];
      header.append(title); $('rows').append(header);
      permissions.forEach(item => {
      const tr = document.createElement('tr');
      const toggleCell = document.createElement('td');
      const toggle = document.createElement('input'); toggle.type = 'checkbox'; toggle.className = 'form-check-input role-permission';
      toggle.value = item.key; toggle.checked = selected.has(item.key);
      toggle.addEventListener('change', () => { toggle.checked ? selected.add(item.key) : selected.delete(item.key); updateCount(); });
      toggleCell.append(toggle);
      const nameCell = document.createElement('td');
      const name = document.createElement('strong');
      name.textContent = english ? item.nameEn || item.key : (item.nameAr || item.key).split('—').pop().trim();
      const key = document.createElement('small'); key.className = 'roles-permission-key'; key.textContent = item.key;
      nameCell.append(name, key);
      const scopeCell = document.createElement('td');
      const scope = scopeNames[item.scope] || [item.scope, item.scope, 'secondary'];
      const badge = document.createElement('span'); badge.className = `badge text-bg-${scope[2]}`;
      badge.textContent = scope[english ? 1 : 0]; scopeCell.append(badge);
      tr.append(toggleCell, nameCell, scopeCell); $('rows').append(tr);
      });
    }
    updateCount();
  };
  async function init() {
    matrix = await ZuLms.api.get('/api/v1/roles');
    for (const role of matrix.roles) { const option = document.createElement('option'); option.value = role.code; option.textContent = ZuLmsI18n.field(role, 'name'); $('role').append(option); }
    selected = new Set(matrix.roles[0]?.permissionKeys || []);
    render();
  }
  $('role').addEventListener('change', () => {
    selected = new Set(matrix.roles.find(x => x.code === $('role').value)?.permissionKeys || []);
    render();
  });
  $('search').addEventListener('input', render);
  $('form').addEventListener('submit', async e => {
    e.preventDefault(); $('save').disabled = true; $('error').classList.add('d-none'); $('success').classList.add('d-none');
    try {
      const keys = [...selected];
      await ZuLms.api.put(`/api/v1/roles/${encodeURIComponent($('role').value)}/permissions`, { keys });
      matrix = await ZuLms.api.get('/api/v1/roles');
      selected = new Set(matrix.roles.find(x => x.code === $('role').value)?.permissionKeys || []);
      render(); $('success').textContent = 'حُفظت صلاحيات الدور'; $('success').classList.remove('d-none');
    } catch (error) { fail(error); } finally { $('save').disabled = false; }
  });
  init().catch(fail);
})();
