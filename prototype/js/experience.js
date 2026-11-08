/* Shared presentation layer. Existing demo controls and data stay in their original pages. */
(() => {
  'use strict';
  const $ = (s, root = document) => root.querySelector(s);
  const $$ = (s, root = document) => [...root.querySelectorAll(s)];
  const role = location.pathname.split('/').pop().replace('.html', '');
  let serial = 0;

  function tabs(host, groups) {
    const usable = groups.filter(g => g.nodes.length);
    if (usable.length < 2) return;
    const bar = document.createElement('div');
    bar.className = 'focus-tabs';
    bar.setAttribute('role', 'tablist');
    bar.setAttribute('aria-label', 'أقسام الصفحة');
    host.append(bar);
    const items = usable.map((group, index) => {
      const id = `focus-${++serial}`;
      const button = document.createElement('button');
      button.type = 'button';
      button.textContent = group.title;
      button.id = `${id}-tab`;
      button.setAttribute('role', 'tab');
      button.setAttribute('aria-controls', id);
      const panel = document.createElement('div');
      panel.className = 'focus-panel';
      panel.id = id;
      panel.setAttribute('role', 'tabpanel');
      panel.setAttribute('aria-labelledby', button.id);
      group.nodes.forEach(node => panel.append(node));
      bar.append(button);
      host.append(panel);
      button.addEventListener('click', () => activate(index));
      button.addEventListener('keydown', event => {
        let next = index;
        if (event.key === 'ArrowLeft') next = (index + 1) % usable.length;
        else if (event.key === 'ArrowRight') next = (index + usable.length - 1) % usable.length;
        else if (event.key === 'Home') next = 0;
        else if (event.key === 'End') next = usable.length - 1;
        else return;
        event.preventDefault();
        activate(next);
        items[next].button.focus();
      });
      return { button, panel };
    });
    function activate(index) {
      items.forEach((item, i) => {
        item.panel.hidden = i !== index;
        item.button.setAttribute('aria-selected', String(i === index));
        item.button.tabIndex = i === index ? 0 : -1;
      });
      requestAnimationFrame(() => {
        if (window.Chart) $$('canvas', items[index].panel).forEach(canvas => window.Chart.getChart(canvas)?.resize());
      });
    }
    activate(0);
  }

  // Keep explanatory policies available without placing paragraphs before every task.
  $$('.page-content .alert-c.a-info, .page-content .alert-c.a-warning').forEach(alert => {
    if (alert.textContent.trim().length < 230 || $('input,button,select,a,textarea', alert)) return;
    const details = document.createElement('details');
    details.className = 'context-help';
    const summary = document.createElement('summary');
    summary.textContent = $('h6,h5,strong', alert)?.textContent.trim() || 'تعليمات وتفاصيل إضافية';
    alert.before(details);
    details.append(summary, alert);
  });

  // Reports get their own destination instead of competing with the daily work.
  const dashboard = $('#sec-dashboard');
  if (dashboard) {
    const reportRows = [...dashboard.children].filter(node => $('canvas', node));
    if (reportRows.length) {
      const reports = document.createElement('div');
      reports.id = 'sec-insights';
      reports.style.display = 'none';
      const title = document.createElement('h2');
      title.className = 'section-title';
      title.textContent = 'التقارير والمؤشرات';
      reports.append(title, ...reportRows);
      if (role === 'quality') {
        const banner = [...dashboard.children].find(node => node.classList.contains('card'));
        if (banner) reports.insertBefore(banner, reportRows[0]);
        const evaluations = $$('.data-card', dashboard).find(card => $('h5', card)?.textContent.includes('أحدث استبيانات'));
        if (evaluations) {
          const column = evaluations.parentElement;
          reports.append(evaluations);
          column.remove();
          const actionColumn = $('.col-lg-8', dashboard);
          if (actionColumn) actionColumn.className = 'col-12';
        }
      }
      dashboard.after(reports);
      const nav = document.createElement('button');
      nav.className = 'nav-item';
      nav.innerHTML = '<i class="ti ti-chart-bar nav-icon"></i> التقارير والمؤشرات';
      nav.addEventListener('click', () => window.show('insights', nav));
      $('.sidebar-nav .nav-item').after(nav);
    }
    const intro = document.createElement('div');
    intro.className = 'workspace-intro';
    const descriptions = {
      admin: ['إدارة المنصة', 'تابع الحسابات والطلبات، وافتح التقارير عند الحاجة.'],
      quality: ['متابعة الجودة', 'ابدأ بطلبات الاعتماد والقرارات التي تحتاج مراجعتك.'],
      dean: ['برامج كليتك', 'تابع مقترحات الكلية أو اقترح دورة جديدة.'],
      instructor: ['مساحة التدريب', 'اختر دورة لإدارة محاضراتها وحضور المتدربين.'],
      member: ['رحلتك التدريبية', 'تابع دوراتك وسجّل حضورك، ثم أكمل متطلبات الشهادة.']
    };
    const [title, description] = descriptions[role] || ['أهلاً بك', 'اختر المهمة التي تريد إنجازها.'];
    const heading = document.createElement('h2');
    heading.textContent = title;
    const text = document.createElement('p');
    text.textContent = description;
    intro.append(heading, text);
    dashboard.prepend(intro);
  }

  // Split the course workspace into lecture management and the participant register.
  if (role === 'instructor') {
    const member = document.createElement('section');
    member.id = 'sec-memberrole';
    member.style.display = 'none';
    member.innerHTML = `
      <div class="workspace-intro"><h2>دوراتي كعضو هيئة تدريس</h2><p>د. خالد المحاضر — تعلّم وشارك في برامج التطوير المهني بحسابك نفسه.</p></div>
      <div class="member-personal-overview data-card mb-4"><div class="data-card-body"><strong>مساحتك كمتدرب</strong><p class="mb-0 text-muted">هنا تسجيلك وحضورك وتقييماتك وشهاداتك الشخصية. لإدارة الدورات التي تقدمها، اختر «دوراتي التدريبية» من القائمة.</p></div></div>
      <div class="personal-tab-content">
        <div data-personal="available" class="data-card"><div class="data-card-body"><h5>الكتابة العلمية والنشر الدولي</h5><p class="text-muted">د. سمر الخالدي · 6 محاضرات · أونلاين</p><button class="btn-p" id="personalEnroll">التسجيل في الدورة</button><p id="personalEnrollmentStatus" class="mt-3 mb-0" role="status"></p></div></div>
        <div data-personal="enrolled" class="data-card"><div class="data-card-body"><h5>دوراتي المسجّلة</h5><p id="personalCourseState">لا توجد دورات مسجلة في هذا المثال بعد. اختر دورة من «الدورات المتاحة».</p><div id="personalCourseDetails" hidden><h6>الكتابة العلمية والنشر الدولي</h6><p>المحاضر: د. سمر الخالدي</p><p>تبدأ الدورة في 15 نوفمبر 2026. ستظهر خانة الحضور عند فتح المحاضر للتحضير.</p><span class="badge-s bs-info">قادمة — 6 محاضرات</span><p class="mt-3 mb-0 text-muted">الحد الأدنى للحضور عند انتهاء الدورة: 5 من 6 محاضرات.</p></div></div></div>
        <div data-personal="completion" class="data-card"><div class="data-card-body"><h5>التقييم والأثر التدريبي</h5><p class="text-muted">عند انتهاء إحدى دوراتك، يظهر هنا استبيانها وخيار رفع الأثر التدريبي لمحاضرها.</p><p class="mb-0">لا توجد متطلبات مستحقة حالياً.</p></div></div>
        <div data-personal="certificates" class="data-card"><div class="data-card-body"><h5>شهاداتي الشخصية</h5><p class="text-muted">تظهر شهادتك بعد استيفاء الحضور والتقييم واعتماد الأثر التدريبي.</p><p class="mb-0">لم تصدر شهادات لهذا الحساب التجريبي بعد.</p></div></div>
      </div>`;
    $('.page-content').append(member);
    const personalHost = $('.personal-tab-content', member);
    tabs(personalHost, [
      { title: 'الدورات المتاحة', nodes: [$('[data-personal="available"]', member)] },
      { title: 'دوراتي المسجّلة', nodes: [$('[data-personal="enrolled"]', member)] },
      { title: 'التقييم والأثر', nodes: [$('[data-personal="completion"]', member)] },
      { title: 'شهاداتي', nodes: [$('[data-personal="certificates"]', member)] }
    ]);
    $('#personalEnroll').onclick = () => {
      $('#personalEnroll').disabled = true;
      $('#personalEnroll').textContent = 'تم التسجيل';
      $('#personalEnrollmentStatus').textContent = 'أُضيفت الدورة إلى دوراتك المسجّلة في هذا العرض التجريبي.';
      $('#personalCourseState').hidden = true;
      $('#personalCourseDetails').hidden = false;
    };
    const detail = $('#sec-coursedetail');
    const children = [...detail.children];
    tabs(detail, [
      { title: 'المحاضرات', nodes: children.slice(2, -1) },
      { title: 'المتدربون والحضور', nodes: children.slice(-1) }
    ]);
    const attendance = $('#sec-attendance');
    const register = $('#dualQrTableBody').closest('.data-card');
    const qrRow = $('#qr1PinDisplay').closest('.row');
    tabs(attendance, [
      { title: 'رموز الحضور', nodes: [qrRow] },
      { title: 'كشف الحضور والتعديل اليدوي', nodes: [register] }
    ]);
  }

  if (role === 'member') {
    const host = $('#sec-entercode');
    const row = [...host.children].find(node => node.classList.contains('row'));
    if (row) {
      const columns = [...row.children];
      columns.forEach(column => { column.className = 'focus-full-width'; });
      tabs(row, [
        { title: 'المحاضرات وإثبات الحضور', nodes: columns.slice(0, 1) },
        { title: 'ملخص الالتزام', nodes: columns.slice(1) }
      ]);
    }
  }

  if (role === 'dean') {
    const host = $('#sec-newcourse');
    const row = [...host.children].find(node => node.classList.contains('row'));
    if (row) {
      const columns = [...row.children];
      // Submission stays visible below the steps; all entered fields remain in the DOM.
      const actions = columns.pop();
      columns.forEach(column => { column.className = 'focus-full-width'; });
      tabs(row, columns.map((column, i) => ({
        title: ['1. بيانات الدورة', '2. المحاضر والمرفقات', '3. ترشيح المشاركين'][i], nodes: [column]
      })));
      row.append(actions);
    }
  }

  // Keep details beside each role card rather than repeating a full feature list.
  $$('.role-card-features').forEach(list => {
    const details = document.createElement('details');
    details.className = 'context-help role-help';
    const summary = document.createElement('summary');
    summary.textContent = 'استعراض الصلاحيات';
    list.before(details);
    details.append(summary, list);
  });

  // Every table scrolls inside its card on small screens.
  $$('table.custom-table').forEach(table => {
    if (table.closest('.table-responsive')) return;
    const wrapper = document.createElement('div');
    wrapper.className = 'table-responsive';
    table.before(wrapper);
    wrapper.append(table);
  });

  if (typeof window.show === 'function') {
    const originalShow = window.show;
    window.show = function(name, element) {
      if (!$('#sec-' + name)) return;
      const nav = element || $$('.sidebar-nav .nav-item').find(button =>
        (button.getAttribute('onclick') || '').includes(`show('${name}'`));
      if (name === 'insights') {
        $$('.page-content > [id^="sec-"]').forEach(section => { section.style.display = 'none'; });
        $('#sec-insights').style.display = '';
        $$('.nav-item').forEach(item => item.classList.toggle('active', item === element));
      } else {
        if ($('#sec-insights')) $('#sec-insights').style.display = 'none';
        originalShow(name, nav || null);
      }
      const titles = { coursedetail: 'تفاصيل الدورة', attendance: 'إدارة حضور المحاضرة', entercode: 'محاضرات الدورة والحضور', insights: 'التقارير والمؤشرات' };
      $('#pageTitle').textContent = titles[name] || nav?.textContent.trim() || $('.section-title', $('#sec-' + name))?.textContent.trim() || 'لوحة التحكم';
      const subtitle = $('.header-left p');
      if (subtitle) subtitle.textContent = name === 'dashboard' ? 'ملخصك اليومي والوصول إلى مهامك' : 'منصة التدريب والتطوير المهني — جامعة الزرقاء';
      document.body.classList.remove('navigation-open');
      $('.menu-toggle')?.setAttribute('aria-expanded', 'false');
      window.scrollTo({ top: 0, behavior: 'instant' });
      requestAnimationFrame(() => {
        if (window.Chart) $$('canvas', $('#sec-' + name)).forEach(canvas => window.Chart.getChart(canvas)?.resize());
      });
    };
  }

  if ($('.sidebar')) {
    const directoryHeading = document.createElement('div');
    directoryHeading.className = 'nav-section-title';
    directoryHeading.textContent = 'مجتمع الجامعة';
    const directoryLink = document.createElement('a');
    directoryLink.className = 'nav-item';
    directoryLink.href = 'members-directory.html';
    directoryLink.innerHTML = '<i class="ti ti-users nav-icon"></i> دليل الأعضاء والملفات الشخصية';
    $('.sidebar-nav').append(directoryHeading, directoryLink);
    const menu = document.createElement('button');
    menu.className = 'menu-toggle btn-s';
    menu.type = 'button';
    menu.setAttribute('aria-label', 'فتح قائمة التنقل');
    menu.setAttribute('aria-expanded', 'false');
    $('.sidebar').id = 'main-navigation';
    menu.setAttribute('aria-controls', 'main-navigation');
    menu.innerHTML = '<i class="ti ti-menu-2"></i>';
    menu.onclick = () => {
      const open = document.body.classList.toggle('navigation-open');
      menu.setAttribute('aria-expanded', String(open));
    };
    $('.header').prepend(menu);
    const shade = document.createElement('button');
    shade.className = 'navigation-shade';
    shade.setAttribute('aria-label', 'إغلاق قائمة التنقل');
    shade.onclick = () => { document.body.classList.remove('navigation-open'); menu.setAttribute('aria-expanded', 'false'); menu.focus(); };
    document.body.append(shade);
    document.addEventListener('keydown', event => { if (event.key === 'Escape') shade.onclick(); });
  }
})();
