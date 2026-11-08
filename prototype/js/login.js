/* Browser-only authentication demonstration; production authentication belongs on the server. */
(() => {
  const accounts = { admin: '1001', quality: '1002', dean: '1003', instructor: '1004', member: '1005' };
  let pending = null;
  const get = id => document.getElementById(id);
  const message = text => { get('loginMessage').textContent = text; };
  const key = id => `zu-demo-password-v1-${id}`;
  async function digest(password) {
    const data = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(password));
    return [...new Uint8Array(data)].map(byte => byte.toString(16).padStart(2, '0')).join('');
  }
  window.cancelPasswordChange = () => {
    pending = null;
    get('changePasswordForm').hidden = true;
    get('loginForm').hidden = false;
    get('demoAccounts').hidden = false;
    get('changePasswordForm').reset();
    message('');
  };
  window.quickFill = email => {
    cancelPasswordChange();
    const id = accounts[email.split('@')[0]];
    get('loginEmployee').value = id;
    try {
      const changed = localStorage.getItem(key(id));
      get('loginPass').value = changed ? '' : 'Demo1234';
      message(changed ? 'أدخل كلمة المرور الجديدة التي عينتها لهذا الحساب التجريبي.' : 'حساب تجريبي: سيُطلب تغيير كلمة المرور عند الدخول.');
    } catch { message('تعذر الوصول إلى التخزين المحلي للعرض التجريبي.'); }
  };
  window.handleLogin = async event => {
    event.preventDefault();
    const id = get('loginEmployee').value.trim();
    const role = Object.keys(accounts).find(role => accounts[role] === id);
    if (!role) return message('الرقم الوظيفي أو كلمة المرور غير صحيحة.');
    try {
      const saved = localStorage.getItem(key(id));
      const password = get('loginPass').value;
      if (saved ? await digest(password) !== saved : password !== 'Demo1234') return message('الرقم الوظيفي أو كلمة المرور غير صحيحة.');
      if (saved) { location.href = `${role}.html`; return; }
      pending = { id, role };
      get('loginForm').hidden = true;
      get('demoAccounts').hidden = true;
      get('changePasswordForm').hidden = false;
      get('loginPass').value = '';
      message('يجب تغيير كلمة المرور المؤقتة لإكمال الدخول.');
      get('newPassword').focus();
    } catch { message('تعذر إكمال الدخول التجريبي. تحقق من إتاحة التخزين المحلي للمتصفح.'); }
  };
  window.changeFirstPassword = async event => {
    event.preventDefault();
    if (!pending) return;
    const password = get('newPassword').value;
    if (password.length < 8) return message('أدخل كلمة مرور من 8 أحرف على الأقل.');
    if (password === 'Demo1234') return message('اختر كلمة مرور مختلفة عن كلمة المرور المؤقتة.');
    if (password !== get('confirmPassword').value) return message('كلمتا المرور غير متطابقتين.');
    const account = pending;
    try {
      localStorage.setItem(key(account.id), await digest(password));
      pending = null;
      location.href = `${account.role}.html`;
    } catch { message('تعذر حفظ كلمة المرور التجريبية. لم يكتمل تغييرها، حاول مجدداً.'); }
  };
})();
