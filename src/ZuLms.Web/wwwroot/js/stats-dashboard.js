// Statistics dashboard v2 (admin / quality / dean). Redesign: docs/FRONTEND-REDESIGN.md
// APIs (unchanged, read-only): /api/v1/stats/lookups, /api/v1/stats/dashboard,
// /api/v1/courses/directory (latest courses table, newest first).
(() => {
  const root = document.getElementById('stats-dashboard-page');
  if (!root) return;
  const $ = id => document.getElementById(id);
  const L = (ar, en) => (window.ZuLmsI18n?.localize ? ZuLmsI18n.localize(ar, en) : ar);
  const tr = text => (window.ZuLmsI18n?.translate ? ZuLmsI18n.translate(text) : text);
  const english = document.documentElement.lang === 'en';
  const rtl = document.documentElement.dir === 'rtl';
  const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  const error = $('dashboard-error');
  const charts = {};
  // Validated categorical palette (dataviz validator: lightness/chroma/CVD pass).
  const PALETTE = ['#1f5fa8', '#c9a227', '#0d9488', '#7c3aed', '#ea580c', '#db2777', '#0891b2'];
  const OTHER = '#94a3b8';
  // Single-series bars: the peak gets the full tone, the rest a tint of it.
  const TONES = { navy: ['#022c5a', '#9db4cf'], gold: ['#a8831a', '#e3cf8f'], teal: ['#0f766e', '#8fcfc6'] };
  const contextRole = root.dataset.role.charAt(0).toUpperCase() + root.dataset.role.slice(1);
  const directoryView = root.dataset.directoryView || 'All';
  const fmt = (v, d = 0) => (window.ZuUi ? ZuUi.format(v, d) : String(v));
  const set = (id, value) => { const node = $(id); if (node) node.textContent = value; };
  const countUp = (id, value, decimals) => (window.ZuUi ? ZuUi.countUp($(id), value, { decimals }) : set(id, value));
  const state = { data: null, lookups: null, monthlyMetric: 'courses', monthlyType: 'bar', trackType: 'bar', facultyView: 'rank', statusColors: new Map() };

  if (window.Chart) {
    Chart.defaults.font.family = 'Tajawal, Arial, sans-serif';
    Chart.defaults.font.size = 12.5;
    Chart.defaults.color = '#5b6b80';
  }

  const fail = e => {
    error.textContent = e?.code === 'FORBIDDEN' ? ZuLmsI18n.t('forbidden')
      : e?.message || L('تعذّر تحميل الإحصائيات', 'Statistics could not be loaded');
    error.classList.remove('d-none');
    if (e?.code === 'FORBIDDEN') root.classList.remove('stats-loading');
  };
  const option = item => {
    const node = document.createElement('option');
    node.value = String(item.id); node.textContent = item.name; return node;
  };
  const el = (tag, cls, text) => {
    const node = document.createElement(tag);
    if (cls) node.className = cls;
    if (text !== undefined) node.textContent = text;
    return node;
  };
  const truncate = (text, n = 24) => (text && text.length > n ? text.slice(0, n - 1) + '…' : text);
  const peakIndex = values => values.reduce((best, v, i) => (v > values[best] ? i : best), 0);
  const toneColors = (values, tone) => {
    const peak = peakIndex(values);
    return values.map((_, i) => (i === peak ? TONES[tone][0] : TONES[tone][1]));
  };

  /* ---------------- chart plumbing ---------------- */
  function tooltipHandler({ chart, tooltip }) {
    const host = chart.canvas.parentNode;
    let tip = host.querySelector('.zs-tip');
    if (!tip) { tip = el('div', 'zs-tip'); host.append(tip); }
    if (tooltip.opacity === 0) { tip.style.opacity = 0; return; }
    tip.replaceChildren();
    const title = (tooltip.title || []).join(' ');
    if (title) tip.append(el('div', 'zs-tip-title', title));
    const isPie = chart.config.type === 'doughnut';
    const total = isPie ? chart.data.datasets[0].data.reduce((a, v, i) => a + (chart.getDataVisibility(i) ? v : 0), 0) : 0;
    tooltip.dataPoints.forEach(dp => {
      const row = el('div', 'zs-tip-row');
      const sw = el('span', 'zs-swatch');
      const color = dp.element?.options?.backgroundColor;
      sw.style.background = typeof color === 'string' ? color : (dp.dataset.borderColor || TONES.navy[0]);
      row.append(sw, el('span', '', isPie ? dp.label : (dp.dataset.label || dp.label)));
      row.append(el('b', '', fmt(dp.raw) + (isPie && total ? `  (${fmt(dp.raw / total * 100, 0)}%)` : '')));
      tip.append(row);
    });
    tip.style.opacity = 1;
    const w = tip.offsetWidth;
    const x = chart.canvas.offsetLeft + tooltip.caretX;
    tip.style.left = `${Math.max(w / 2 + 4, Math.min(host.clientWidth - w / 2 - 4, x))}px`;
    tip.style.top = `${chart.canvas.offsetTop + tooltip.caretY}px`;
  }
  // Values printed at the end of each bar (selective when there are many bars).
  const valueLabels = {
    id: 'zsValueLabels',
    afterDatasetsDraw(chart) {
      const meta = chart.getDatasetMeta(0);
      if (!meta || meta.hidden) return;
      const values = chart.data.datasets[0].data;
      const horizontal = chart.options.indexAxis === 'y';
      const dense = values.length > 16;
      const peak = peakIndex(values);
      const { ctx } = chart;
      ctx.save();
      ctx.font = '700 12.5px Tajawal, Arial, sans-serif';
      ctx.fillStyle = '#0f172a';
      meta.data.forEach((bar, i) => {
        const v = values[i];
        if (!v || (dense && i !== peak)) return;
        const { x, y } = bar.getProps(['x', 'y'], true);
        if (horizontal) {
          ctx.textBaseline = 'middle'; ctx.textAlign = rtl ? 'right' : 'left';
          ctx.fillText(fmt(v), x + (rtl ? -6 : 6), y);
        } else {
          ctx.textBaseline = 'bottom'; ctx.textAlign = 'center';
          ctx.fillText(fmt(v), x, y - 5);
        }
      });
      ctx.restore();
    }
  };
  const axes = horizontal => {
    const cat = { grid: { display: false }, border: { display: false }, ticks: { color: '#5b6b80', maxRotation: 0, autoSkip: true } };
    const val = { beginAtZero: true, grace: '12%', grid: { color: '#eef2f7' }, border: { display: false }, ticks: { precision: 0, color: '#8a9bb0', padding: 6 } };
    if (horizontal) {
      return {
        y: { ...cat, position: rtl ? 'right' : 'left', ticks: { ...cat.ticks, autoSkip: false, callback(v) { return truncate(this.getLabelForValue(v), 28); } } },
        x: { ...val, reverse: rtl, grid: { display: false }, ticks: { display: false } }
      };
    }
    return {
      x: { ...cat, offset: true, reverse: rtl, ticks: { ...cat.ticks, callback(v) { return truncate(this.getLabelForValue(v), 14); } } },
      y: { ...val, position: rtl ? 'right' : 'left' }
    };
  };
  const baseOptions = (extra = {}) => ({
    responsive: true, maintainAspectRatio: false,
    animation: reduced ? false : { duration: 650, easing: 'easeOutQuart' },
    interaction: { mode: 'index', intersect: false },
    plugins: { legend: { display: false }, tooltip: { enabled: false, external: tooltipHandler } },
    ...extra
  });
  const draw = (id, config) => {
    charts[id]?.destroy();
    const canvas = $(id);
    if (!canvas || !window.Chart) return null;
    charts[id] = new Chart(canvas, config);
    return charts[id];
  };
  const areaFill = color => context => {
    const { ctx, chartArea } = context.chart;
    if (!chartArea) return color + '22';
    const g = ctx.createLinearGradient(0, chartArea.top, 0, chartArea.bottom);
    g.addColorStop(0, color + '45'); g.addColorStop(1, color + '00');
    return g;
  };
  const emptyNote = (body, show) => {
    let note = body.querySelector('.zs-empty');
    if (show && !note) {
      note = el('div', 'zs-center-total zs-empty');
      const wrap = el('div');
      const icon = el('i', 'ti ti-chart-dots-3');
      icon.style.cssText = 'font-size:28px;color:#a3b1c2;display:block';
      wrap.append(icon, el('span', '', L('لا توجد بيانات للفترة المحددة', 'No data for the selected period')));
      note.append(wrap); body.append(note);
    }
    if (!show && note) note.remove();
  };

  /* ---------------- KPI cards ---------------- */
  function trend(id, values) {
    const node = $(id);
    if (!node) return;
    node.className = 'zs-trend';
    node.replaceChildren();
    if (values.length < 2) { node.classList.add('d-none'); return; }
    const last = values.at(-1), prev = values.at(-2);
    const diff = last - prev;
    const icon = el('i', `ti ${diff > 0 ? 'ti-caret-up-filled' : diff < 0 ? 'ti-caret-down-filled' : 'ti-minus'}`);
    icon.setAttribute('aria-hidden', 'true');
    const text = prev > 0 ? `${diff > 0 ? '+' : ''}${fmt(diff / prev * 100, 0)}%` : `${diff > 0 ? '+' : ''}${fmt(diff)}`;
    if (diff > 0) node.classList.add('up'); else if (diff < 0) node.classList.add('down');
    node.append(icon, document.createTextNode(text));
    node.setAttribute('aria-label', `${L('مقارنة بالشهر السابق', 'vs. previous month')}: ${text}`);
  }
  function renderKpis(data) {
    countUp('kpi-courses', data.courses, 0);
    countUp('kpi-participants', data.participants, 0);
    countUp('kpi-certificates', data.certificates, 0);
    const rate = Number(data.completionRate) || 0;
    countUp('kpi-completion', rate, Number.isInteger(rate) ? 0 : 1);
    const meter = $('zs-meter-bar');
    if (meter) requestAnimationFrame(() => { meter.style.width = `${Math.min(100, Math.max(0, rate))}%`; });
    set('kpi-finished-note', L(`${fmt(data.finishedCourses)} من ${fmt(data.courses)} دورة`, `${fmt(data.finishedCourses)} of ${fmt(data.courses)} courses`));
    set('kpi-cert-ratio', data.participants > 0
      ? L(`${fmt(data.certificates / data.participants * 100, 0)}% من المشاركات انتهت بشهادة`, `${fmt(data.certificates / data.participants * 100, 0)}% of participations earned a certificate`)
      : L('لا توجد مشاركات بعد', 'No participations yet'));
    const months = data.monthly || [];
    trend('trend-courses', months.map(x => x.courses));
    trend('trend-participants', months.map(x => x.participants));
  }

  /* ---------------- charts ---------------- */
  function drawMonthly() {
    const rows = state.data.monthly || [];
    const key = state.monthlyMetric;
    const tone = key === 'courses' ? 'navy' : 'gold';
    const label = key === 'courses' ? L('الدورات', 'Courses') : L('المشاركات', 'Participations');
    const values = rows.map(r => r[key]);
    const line = state.monthlyType === 'line';
    const color = TONES[tone][0];
    const dataset = line
      ? { label, data: values, borderColor: color, borderWidth: 2, tension: .35, fill: true, backgroundColor: areaFill(color),
          pointRadius: values.length > 18 ? 0 : 3, pointHoverRadius: 6, pointBackgroundColor: '#fff', pointBorderColor: color, pointBorderWidth: 2 }
      : { label, data: values, backgroundColor: toneColors(values, tone), hoverBackgroundColor: color, borderRadius: 6, maxBarThickness: 44, categoryPercentage: .72 };
    draw('chart-monthly', {
      type: line ? 'line' : 'bar',
      data: { labels: rows.map(r => r.label), datasets: [dataset] },
      options: baseOptions({ scales: axes(false), layout: { padding: { top: 8 } } }),
      plugins: line ? [] : [valueLabels]
    });
    emptyNote($('chart-monthly').parentNode, !rows.length);
    const total = values.reduce((a, v) => a + v, 0);
    set('zs-sub-monthly', rows.length
      ? L(`${fmt(total)} ${key === 'courses' ? 'دورة' : 'مشاركة'} خلال ${fmt(rows.length)} شهر`, `${fmt(total)} ${key === 'courses' ? 'courses' : 'participations'} across ${fmt(rows.length)} months`)
      : L('لا توجد بيانات', 'No data'));
  }

  function statusColor(label) {
    if (!state.statusColors.has(label)) {
      const i = state.statusColors.size;
      state.statusColors.set(label, i < PALETTE.length ? PALETTE[i] : OTHER);
    }
    return state.statusColors.get(label);
  }
  function drawStatus() {
    const rows = (state.data.byStatus || []).filter(r => r.value > 0);
    const colors = rows.map(r => statusColor(r.label));
    const chart = draw('chart-status', {
      type: 'doughnut',
      data: { labels: rows.map(r => r.label), datasets: [{ data: rows.map(r => r.value), backgroundColor: colors, borderColor: '#fff', borderWidth: 3, hoverOffset: 6 }] },
      options: baseOptions({ cutout: '66%', interaction: { mode: 'nearest', intersect: true }, layout: { padding: 6 } })
    });
    const totalNode = $('zs-status-total');
    const updateTotal = () => {
      const sum = rows.reduce((a, r, i) => a + (chart?.getDataVisibility(i) === false ? 0 : r.value), 0);
      totalNode.replaceChildren();
      if (!rows.length) return;
      const box = el('div');
      box.append(el('b', '', fmt(sum)), el('span', '', L('دورة', 'courses')));
      totalNode.append(box);
    };
    updateTotal();
    emptyNote($('chart-status').parentNode, !rows.length);
    const legend = $('legend-status');
    legend.replaceChildren();
    const total = rows.reduce((a, r) => a + r.value, 0);
    rows.forEach((r, i) => {
      const li = el('li');
      const b = el('button');
      b.type = 'button'; b.setAttribute('aria-pressed', 'true');
      const sw = el('span', 'zs-swatch'); sw.style.background = colors[i];
      b.append(sw, el('span', 'zs-legend-name', r.label), el('span', 'zs-legend-val', fmt(r.value)), el('span', 'zs-legend-pct', `${fmt(total ? r.value / total * 100 : 0, 0)}%`));
      b.title = L('اضغط لإخفاء/إظهار هذه الحالة', 'Click to hide or show this status');
      b.addEventListener('click', () => {
        if (!chart) return;
        chart.toggleDataVisibility(i); chart.update();
        b.setAttribute('aria-pressed', String(chart.getDataVisibility(i)));
        updateTotal();
      });
      const focus = on => {
        if (!chart) return;
        const active = on && chart.getDataVisibility(i) ? [{ datasetIndex: 0, index: i }] : [];
        chart.setActiveElements(active); chart.tooltip.setActiveElements(active, { x: 0, y: 0 }); chart.update();
      };
      b.addEventListener('mouseenter', () => focus(true)); b.addEventListener('mouseleave', () => focus(false));
      b.addEventListener('focus', () => focus(true)); b.addEventListener('blur', () => focus(false));
      li.append(b); legend.append(li);
    });
    if (!rows.length) legend.append(el('li', 'text-muted small p-2', L('لا توجد بيانات', 'No data')));
  }

  function facultyIdFor(label) {
    const norm = s => String(s || '').trim();
    return (state.lookups?.faculties || []).find(f => norm(f.name) === norm(label))?.id;
  }
  function drawFaculty() {
    const rows = [...(state.data.byFaculty || [])].sort((a, b) => b.value - a.value);
    const list = $('rank-faculty');
    list.replaceChildren();
    const canFilter = !!state.lookups?.canFilterFaculty;
    $('zs-rank-hint')?.classList.toggle('d-none', !canFilter || (rows.length < 2 && !$('stats-center').value));
    const max = Math.max(1, ...rows.map(r => r.value));
    const selectedText = $('stats-center').value ? $('stats-center').selectedOptions[0]?.textContent : null;
    if (!rows.length) list.append(el('li', 'text-muted p-3 text-center', L('لا توجد بيانات للفترة المحددة', 'No data for the selected period')));
    rows.forEach((r, i) => {
      const li = el('li');
      const b = el('button');
      b.type = 'button';
      const id = canFilter ? facultyIdFor(r.label) : undefined;
      b.disabled = id === undefined;
      if (selectedText && selectedText.trim() === String(r.label).trim()) { b.classList.add('is-selected'); b.setAttribute('aria-pressed', 'true'); }
      const bar = el('span', 'zs-rank-bar'); const fill = el('i'); bar.append(fill);
      b.append(el('span', 'zs-rank-pos', fmt(i + 1)), el('span', 'zs-rank-name', r.label), el('span', 'zs-rank-val', fmt(r.value)), bar);
      b.title = r.label;
      if (id !== undefined) {
        b.addEventListener('click', () => {
          const sel = $('stats-center');
          sel.value = sel.value === String(id) ? '' : String(id);
          load();
        });
      }
      li.append(b); list.append(li);
      requestAnimationFrame(() => { fill.style.width = `${(r.value / max) * 100}%`; });
    });
    if (state.facultyView === 'bar') {
      const body = root.querySelector('[data-panel="faculty"] [data-view-body="bar"]');
      body.style.minHeight = `${Math.max(300, rows.length * 36 + 40)}px`;
      const values = rows.map(r => r.value);
      draw('chart-faculty', {
        type: 'bar',
        data: { labels: rows.map(r => r.label), datasets: [{ label: L('الدورات', 'Courses'), data: values, backgroundColor: toneColors(values, 'navy'), hoverBackgroundColor: TONES.navy[0], borderRadius: 5, maxBarThickness: 22 }] },
        options: baseOptions({ indexAxis: 'y', scales: axes(true), layout: { padding: { [rtl ? 'left' : 'right']: 34 } }, interaction: { mode: 'nearest', axis: 'y', intersect: false } }),
        plugins: [valueLabels]
      });
      emptyNote(body, !rows.length);
    }
  }

  function drawTrack() {
    let rows = [...(state.data.byTrack || [])].sort((a, b) => b.value - a.value);
    const canvas = $('chart-track');
    if (state.trackType === 'doughnut') {
      rows = rows.filter(r => r.value > 0);
      if (rows.length > PALETTE.length) {
        const head = rows.slice(0, PALETTE.length - 1);
        const rest = rows.slice(PALETTE.length - 1).reduce((a, r) => a + r.value, 0);
        rows = [...head, { label: L('أخرى', 'Other'), value: rest, other: true }];
      }
      canvas.parentNode.style.minHeight = '340px';
      draw('chart-track', {
        type: 'doughnut',
        data: { labels: rows.map(r => r.label), datasets: [{ data: rows.map(r => r.value), backgroundColor: rows.map((r, i) => (r.other ? OTHER : PALETTE[i])), borderColor: '#fff', borderWidth: 3, hoverOffset: 6 }] },
        options: baseOptions({ cutout: '62%', interaction: { mode: 'nearest', intersect: true },
          plugins: { legend: { display: true, position: 'bottom', rtl, labels: { usePointStyle: true, pointStyle: 'circle', boxWidth: 8, padding: 12, color: '#0f172a', generateLabels: chart => Chart.overrides.doughnut.plugins.legend.labels.generateLabels(chart).map(item => ({ ...item, text: truncate(item.text, 26) })) } },
            tooltip: { enabled: false, external: tooltipHandler } } })
      });
    } else {
      canvas.parentNode.style.minHeight = `${Math.max(300, rows.length * 36 + 40)}px`;
      const values = rows.map(r => r.value);
      draw('chart-track', {
        type: 'bar',
        data: { labels: rows.map(r => r.label), datasets: [{ label: L('الدورات', 'Courses'), data: values, backgroundColor: toneColors(values, 'teal'), hoverBackgroundColor: TONES.teal[0], borderRadius: 5, maxBarThickness: 22 }] },
        options: baseOptions({ indexAxis: 'y', scales: axes(true), layout: { padding: { [rtl ? 'left' : 'right']: 34 } }, interaction: { mode: 'nearest', axis: 'y', intersect: false } }),
        plugins: [valueLabels]
      });
    }
    emptyNote(canvas.parentNode, !rows.length);
  }

  function renderCharts() {
    drawStatus(); drawMonthly(); drawFaculty(); drawTrack();
    root.querySelectorAll('.zs-panel').forEach(p => { if (p.querySelector('[data-action="table"][aria-pressed="true"]')) showTable(p, true); });
  }

  /* ---------------- latest courses ---------------- */
  const statusStyles = {
    AwaitingAccount: [['بانتظار حساب المدرب', 'Awaiting instructor account'], 'pending'],
    Proposed: [['مقترحة', 'Proposed'], 'pending'], Returned: [['معادة للتعديل', 'Returned'], 'returned'],
    Rejected: [['مرفوضة', 'Rejected'], 'rejected'], Approved: [['معتمدة', 'Approved'], 'approved'],
    Finished: [['منتهية', 'Finished'], 'finished'], Cancelled: [['ملغاة', 'Cancelled'], 'cancelled']
  };
  const initials = name => String(name || '').split(/\s+/).filter(w => w && !w.endsWith('.')).slice(0, 2).map(w => w[0]).join('') || '—';
  function recentState(message) {
    const row = el('tr'); const td = el('td', 'text-center text-muted py-4', message); td.colSpan = 7;
    row.append(td); $('zs-recent-rows').replaceChildren(row);
  }
  async function loadRecent() {
    const body = $('zs-recent-rows');
    if (!body) return;
    const query = new URLSearchParams({ view: directoryView, page: '1' });
    if ($('stats-center')?.value) query.set('centerId', $('stats-center').value);
    try {
      const data = await ZuLms.api.get(`/api/v1/courses/directory?${query}`);
      body.replaceChildren();
      state.recent = (data.items || []).slice(0, 6);
      state.recent.forEach(row => {
        const r = el('tr');
        const no = el('td'); const noSpan = el('span', 'zs-course-no', row.courseNo || `#${row.id}`); noSpan.dir = 'ltr'; no.append(noSpan);
        const title = el('td', 'zs-course-title'); const a = el('a', '', ZuLmsI18n.field(row, 'title') || '—');
        a.href = `/courses/${row.id}?view=${directoryView}`; title.append(a);
        const center = el('td', 'zs-cell-muted', (english ? row.centerNameEn : row.centerNameAr) || row.centerNameAr || '—');
        const instructorName = (english ? row.instructorNameEn : row.instructorNameAr) || row.instructorNameAr || '—';
        const instructor = el('td'); const who = el('span', 'zs-person-cell');
        const av = el('span', 'zs-avatar', initials(instructorName)); av.setAttribute('aria-hidden', 'true');
        who.append(av, el('span', '', instructorName)); instructor.append(who);
        const status = el('td'); const [label, style] = statusStyles[row.status] || [[row.status, row.status], 'rejected'];
        status.append(el('span', `zu-status zu-status-${style}`, L(label[0], label[1])));
        const count = el('td', 'zs-num', fmt(row.participantsCount ?? 0));
        const start = el('td', 'zs-cell-muted', row.plannedStartDate || '—'); start.dir = 'ltr';
        r.append(no, title, center, instructor, status, count, start);
        body.append(r);
      });
      if (!data.items?.length) recentState(L('لا توجد دورات بعد', 'No courses yet'));
    } catch (e) {
      state.recent = null;
      if (e?.code === 'FORBIDDEN') root.querySelector('.zs-recent')?.classList.add('d-none');
      else recentState(e?.message || L('تعذّر تحميل الدورات', 'Courses could not be loaded'));
    }
  }

  /* ---------------- render / load ---------------- */
  const render = data => {
    state.data = data;
    error.classList.add('d-none');
    set('stats-scope', tr(data.scopeLabel));
    renderKpis(data);
    const person = $('person-summary');
    person.classList.toggle('d-none', !data.person);
    if (data.person) {
      set('person-name', data.person.name);
      countUp('person-courses', data.person.courses, 0);
      countUp('person-finished', data.person.finishedCourses, 0);
      countUp('person-certificates', data.person.certificates, 0);
    }
    renderCharts();
    root.classList.add('zs-ready');
  };
  const params = () => {
    const values = new URLSearchParams();
    values.set('role', contextRole);
    [['centerId', 'stats-center'], ['userId', 'stats-user'], ['fromDate', 'stats-from'], ['toDate', 'stats-to']]
      .forEach(([key, id]) => { if ($(id)?.value) values.set(key, $(id).value); });
    return values.toString();
  };
  const updateFilterBadge = () => {
    const n = ['stats-center', 'stats-user'].filter(id => $(id)?.value).length + (($('stats-from').value || $('stats-to').value) && $('zs-range').value === 'custom' ? 1 : 0);
    const badge = $('zs-active-count');
    badge.textContent = fmt(n);
    badge.classList.toggle('d-none', n === 0);
  };
  let loadSeq = 0;
  const load = async () => {
    const seq = ++loadSeq;
    root.classList.add('stats-loading');
    $('zs-refresh')?.classList.add('is-spinning');
    updateFilterBadge();
    loadRecent();
    try {
      const data = await ZuLms.api.get(`/api/v1/stats/dashboard?${params()}`);
      if (seq === loadSeq) render(data);
    } catch (e) { fail(e); }
    finally {
      if (seq === loadSeq) { root.classList.remove('stats-loading'); $('zs-refresh')?.classList.remove('is-spinning'); }
    }
  };

  /* ---------------- panel tools ---------------- */
  const panelChart = panel => ({ monthly: 'chart-monthly', status: 'chart-status', faculty: 'chart-faculty', track: 'chart-track' })[panel.dataset.panel];
  function tableRows(panel) {
    const d = state.data;
    if (!d) return null;
    switch (panel.dataset.panel) {
      case 'monthly': return { head: [L('الشهر', 'Month'), L('الدورات', 'Courses'), L('المشاركات', 'Participations')], rows: (d.monthly || []).map(m => [m.label, fmt(m.courses), fmt(m.participants)]) };
      case 'status': { const t = (d.byStatus || []).reduce((a, r) => a + r.value, 0); return { head: [L('الحالة', 'Status'), L('العدد', 'Count'), '%'], rows: (d.byStatus || []).map(r => [r.label, fmt(r.value), `${fmt(t ? r.value / t * 100 : 0, 1)}%`]) }; }
      case 'track': return { head: [L('المحور', 'Track'), L('الدورات', 'Courses')], rows: [...(d.byTrack || [])].sort((a, b) => b.value - a.value).map(r => [r.label, fmt(r.value)]) };
      default: return null;
    }
  }
  function showTable(panel, on) {
    panel.querySelector('.zs-table-wrap')?.remove();
    const body = panel.querySelector('.zs-donut-wrap') || panel.querySelector('.zs-panel-body');
    body.classList.toggle('d-none', on);
    if (!on) { charts[panelChart(panel)]?.resize(); return; }
    const data = tableRows(panel);
    const wrap = el('div', 'zs-table-wrap');
    const table = el('table');
    const thead = el('thead'), hr = el('tr');
    data?.head.forEach(h => { const th = el('th', '', h); th.scope = 'col'; hr.append(th); });
    thead.append(hr);
    const tbody = el('tbody');
    (data?.rows || []).forEach(r => { const row = el('tr'); r.forEach(c => row.append(el('td', '', c))); tbody.append(row); });
    if (!data?.rows.length) { const row = el('tr'); const td = el('td', 'text-muted text-center', L('لا توجد بيانات', 'No data')); td.colSpan = data?.head.length || 1; row.append(td); tbody.append(row); }
    table.append(thead, tbody); wrap.append(table);
    body.after(wrap);
  }
  function download(panel) {
    const chart = charts[panelChart(panel)];
    if (!chart) return;
    const src = chart.canvas;
    const out = document.createElement('canvas');
    out.width = src.width; out.height = src.height;
    const ctx = out.getContext('2d');
    ctx.fillStyle = '#fff'; ctx.fillRect(0, 0, out.width, out.height); ctx.drawImage(src, 0, 0);
    const a = document.createElement('a');
    a.href = out.toDataURL('image/png');
    a.download = `zu-lms-${panel.dataset.panel}-${new Date().toISOString().slice(0, 10)}.png`;
    document.body.append(a); a.click(); a.remove();
  }
  let overlay = null;
  function expand(panel, on) {
    const btn = panel.querySelector('[data-action="expand"]');
    panel.classList.toggle('is-expanded', on);
    btn.querySelector('i').className = `ti ${on ? 'ti-minimize' : 'ti-maximize'}`;
    btn.setAttribute('aria-label', on ? L('تصغير', 'Exit full screen') : L('تكبير', 'Full screen'));
    btn.title = btn.getAttribute('aria-label');
    if (on) {
      overlay = el('div', 'zs-overlay');
      overlay.addEventListener('click', () => expand(panel, false));
      document.body.append(overlay);
      document.body.style.overflow = 'hidden';
      btn.focus();
    } else {
      overlay?.remove(); overlay = null; document.body.style.overflow = '';
    }
    requestAnimationFrame(() => Object.values(charts).forEach(c => c.resize()));
  }
  document.addEventListener('keydown', e => {
    if (e.key !== 'Escape') return;
    const open = root.querySelector('.zs-panel.is-expanded');
    if (open) expand(open, false);
  });

  root.addEventListener('click', e => {
    const tool = e.target.closest('[data-action]');
    const segBtn = e.target.closest('.zs-seg button');
    const panel = e.target.closest('.zs-panel');
    if (tool && panel) {
      const action = tool.dataset.action;
      if (action === 'download') download(panel);
      if (action === 'expand') expand(panel, !panel.classList.contains('is-expanded'));
      if (action === 'table') {
        const on = tool.getAttribute('aria-pressed') !== 'true';
        tool.setAttribute('aria-pressed', String(on));
        showTable(panel, on);
      }
      return;
    }
    if (segBtn && panel && state.data) {
      const seg = segBtn.closest('.zs-seg');
      seg.querySelectorAll('button').forEach(b => b.setAttribute('aria-pressed', String(b === segBtn)));
      const value = segBtn.dataset.value;
      if (panel.dataset.panel === 'monthly') {
        if (seg.dataset.seg === 'metric') state.monthlyMetric = value; else state.monthlyType = value;
        drawMonthly();
      } else if (panel.dataset.panel === 'track') {
        state.trackType = value; drawTrack();
      } else if (panel.dataset.panel === 'faculty') {
        state.facultyView = value;
        $('rank-faculty').classList.toggle('d-none', value !== 'rank');
        panel.querySelector('[data-view-body="bar"]').classList.toggle('d-none', value !== 'bar');
        panel.querySelector('[data-bar-only]')?.classList.toggle('d-none', value !== 'bar');
        drawFaculty();
      }
    }
  });

  /* ---------------- period + header buttons ---------------- */
  const pad = n => String(n).padStart(2, '0');
  const iso = d => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  const range = $('zs-range');
  range?.addEventListener('change', () => {
    if (range.value === 'custom') return;
    const today = new Date();
    let from = '';
    if (range.value === 'month') from = iso(new Date(today.getFullYear(), today.getMonth(), 1));
    if (range.value === 'quarter') from = iso(new Date(today.getFullYear(), today.getMonth() - 3, today.getDate()));
    if (range.value === 'year') from = iso(new Date(today.getFullYear(), 0, 1));
    $('stats-from').value = from;
    $('stats-to').value = from ? iso(today) : '';
    load();
  });
  ['stats-from', 'stats-to'].forEach(id => $(id)?.addEventListener('change', () => {
    range.value = !$('stats-from').value && !$('stats-to').value ? 'all' : 'custom';
  }));
  const filtersWrap = $('zs-filters-wrap');
  const filterToggle = $('zs-filter-toggle');
  filterToggle?.addEventListener('click', () => {
    filtersWrap.open = !filtersWrap.open;
    filterToggle.setAttribute('aria-expanded', String(filtersWrap.open));
    if (filtersWrap.open) filtersWrap.querySelector('select:not([disabled]), input')?.focus();
  });
  $('zs-refresh')?.addEventListener('click', load);
  $('zs-print')?.addEventListener('click', () => window.print());

  /* ---------------- PDF export: exactly what is on screen ---------------- */
  const inTableView = key => root.querySelector(`[data-panel="${key}"] [data-action="table"]`)?.getAttribute('aria-pressed') === 'true';
  const periodText = () => {
    if (range.value !== 'custom' && range.value !== 'all') return range.selectedOptions[0].textContent.trim();
    const f = $('stats-from').value, t = $('stats-to').value;
    if (!f && !t) return L('كل الفترات', 'All time');
    return `${f || '…'}  ←  ${t || '…'}`;
  };
  const trendNote = id => {
    const n = $(id);
    return n && !n.classList.contains('d-none') && n.textContent ? `${n.textContent} ${L('مقارنة بالشهر السابق', 'vs. previous month')}` : '';
  };
  // Draw a chart into the PDF from a high-density off-screen copy (no pixelation).
  const sharp = (chart, drawFn) => {
    const snap = window.ZuPdf?.snapshot ? ZuPdf.snapshot(chart, 3) : null;
    try { drawFn(snap?.canvas || chart.canvas); } finally { snap?.dispose(); }
  };
  async function exportPdf(opts) {
    const d = state.data;
    if (!d) throw new Error(L('لا توجد بيانات بعد، انتظر اكتمال التحميل.', 'No data yet — wait for the dashboard to load.'));
    const S = opts.sections;
    const meta = [[L('النطاق', 'Scope'), tr(d.scopeLabel)], [L('الفترة', 'Period'), periodText()]];
    if ($('stats-center').value) meta.push([L('الكلية', 'Faculty'), $('stats-center').selectedOptions[0].textContent]);
    if ($('stats-user').value) meta.push([L('الشخص', 'Person'), $('stats-user').selectedOptions[0].textContent]);
    const rep = await ZuPdf.create({ title: $('zs-title').textContent.trim(), subtitle: L('تقرير الإحصائيات', 'Statistics report'), meta, grayscale: opts.grayscale });
    const rate = Number(d.completionRate) || 0;
    if (S.has('kpis')) {
      rep.section(L('المؤشرات الرئيسية', 'Key indicators'));
      rep.kpis([
        { label: L('الدورات', 'Courses'), value: fmt(d.courses), note: trendNote('trend-courses'), color: '#022c5a' },
        { label: L('المشاركات', 'Participations'), value: fmt(d.participants), note: trendNote('trend-participants'), color: '#c9a227' },
        { label: L('الشهادات الصادرة', 'Certificates issued'), value: fmt(d.certificates), note: $('kpi-cert-ratio')?.textContent || '', color: '#0f766e' },
        { label: L('الدورات المنتهية', 'Finished courses'), value: `${fmt(rate, Number.isInteger(rate) ? 0 : 1)}%`, note: $('kpi-finished-note')?.textContent || '', color: '#0369a1' }
      ]);
      if (d.person) {
        rep.section(L(`إحصائيات ${d.person.name}`, `Statistics for ${d.person.name}`));
        rep.kpis([
          { label: L('الدورات', 'Courses'), value: fmt(d.person.courses) },
          { label: L('المنتهية', 'Finished'), value: fmt(d.person.finishedCourses) },
          { label: L('الشهادات', 'Certificates'), value: fmt(d.person.certificates) }
        ], 3);
      }
    }
    const tableOf = key => { const t = tableRows(root.querySelector(`[data-panel="${key}"]`)); rep.table(t.head, t.rows, t.head.map((_, i) => (i === 0 ? 3 : 1))); };
    if (S.has('status')) {
      rep.section(L('الدورات حسب الحالة', 'Courses by status'));
      const chart = charts['chart-status'];
      if (inTableView('status') || !chart) tableOf('status');
      else {
        const rows = (d.byStatus || []).filter(r => r.value > 0);
        const visible = rows.filter((r, i) => chart.getDataVisibility(i));
        const total = visible.reduce((a, r) => a + r.value, 0);
        const legend = visible.map(r => ({ color: statusColor(r.label), label: r.label, value: fmt(r.value), pct: `${fmt(total ? r.value / total * 100 : 0, 0)}%` }));
        legend.push({ color: '#ffffff', label: L(`المجموع: ${fmt(total)} دورة`, `Total: ${fmt(total)} courses`), value: '', pct: '' });
        sharp(chart, c => rep.chart(c, { height: 420, legend }));
      }
    }
    if (S.has('monthly')) {
      rep.section(L('التطور الشهري', 'Monthly trend'), `${state.monthlyMetric === 'courses' ? L('الدورات', 'Courses') : L('المشاركات', 'Participations')} — ${$('zs-sub-monthly')?.textContent || ''}`);
      if (inTableView('monthly') || !charts['chart-monthly']) tableOf('monthly');
      else sharp(charts['chart-monthly'], c => rep.chart(c, { height: 560 }));
    }
    if (S.has('faculty')) {
      rep.section(L('التوزيع حسب الكلية', 'By faculty'));
      const rows = [...(d.byFaculty || [])].sort((a, b) => b.value - a.value);
      if (state.facultyView === 'bar' && charts['chart-faculty']) sharp(charts['chart-faculty'], c => rep.chart(c, { height: 900 }));
      else if (rows.length) rep.bars(rows);
      else rep.note(L('لا توجد بيانات', 'No data'));
    }
    if (S.has('track')) {
      rep.section(L('التوزيع حسب المحور التدريبي', 'By training track'));
      if (inTableView('track') || !charts['chart-track']) tableOf('track');
      else sharp(charts['chart-track'], c => rep.chart(c, { height: state.trackType === 'doughnut' ? 560 : 820 }));
    }
    if (S.has('recent') && state.recent) {
      rep.section(L('أحدث الدورات', 'Latest courses'));
      rep.table([L('رقم الدورة', 'Course no.'), L('العنوان', 'Title'), L('الكلية', 'Faculty'), L('المدرب', 'Instructor'), L('الحالة', 'Status'), L('المشاركون', 'Participants'), L('البدء', 'Start')],
        state.recent.map(row => {
          const [label] = statusStyles[row.status] || [[row.status, row.status]];
          return [row.courseNo || `#${row.id}`, ZuLmsI18n.field(row, 'title') || '—', (english ? row.centerNameEn : row.centerNameAr) || row.centerNameAr || '—',
            (english ? row.instructorNameEn : row.instructorNameAr) || row.instructorNameAr || '—', L(label[0], label[1]), fmt(row.participantsCount ?? 0), row.plannedStartDate || '—'];
        }), [1.7, 2.8, 1.9, 1.8, 1.2, 1.2, 1.2]);
    }
    const stamp = new Date().toISOString().slice(0, 10);
    await rep.save(`zu-lms-stats-${root.dataset.role}-${stamp}.pdf`);
  }
  $('zs-export')?.addEventListener('click', () => {
    if (!window.ZuPdf) return;
    ZuPdf.openDialog({
      sections: [
        { key: 'kpis', label: L('المؤشرات الرئيسية', 'Key indicators') },
        { key: 'status', label: L('الدورات حسب الحالة', 'Courses by status') },
        { key: 'monthly', label: L('التطور الشهري', 'Monthly trend') },
        { key: 'faculty', label: L('التوزيع حسب الكلية', 'By faculty') },
        { key: 'track', label: L('التوزيع حسب المحور', 'By training track') },
        { key: 'recent', label: L('أحدث الدورات', 'Latest courses'), checked: !!state.recent, disabled: !state.recent }
      ],
      onExport: exportPdf
    });
  });

  /* ---------------- filters (unchanged behaviour) ---------------- */
  const init = async () => {
    try {
      const lookups = await ZuLms.api.get(`/api/v1/stats/lookups?role=${encodeURIComponent(contextRole)}`);
      state.lookups = lookups;
      lookups.faculties.forEach(x => $('stats-center').append(option(x)));
      root.querySelectorAll('[data-faculty-filter]').forEach(x =>
        x.classList.toggle('d-none', !lookups.canFilterFaculty));
      root.querySelectorAll('[data-user-filter]').forEach(x =>
        x.classList.toggle('d-none', !lookups.canFilterUser));
      await load();
    } catch (e) { fail(e); root.classList.remove('stats-loading'); }
  };
  $('stats-filters').addEventListener('submit', e => { e.preventDefault(); load(); });
  let userSearchTimer;
  $('stats-user-search').addEventListener('input', () => {
    clearTimeout(userSearchTimer);
    $('stats-user').replaceChildren(option({ id: '', name: L('جميع الأشخاص', 'All people') }));
    const term = $('stats-user-search').value.trim();
    if (term.length < 2) return;
    userSearchTimer = setTimeout(async () => {
      try {
        const data = await ZuLms.api.get(`/api/v1/stats/lookups?role=${encodeURIComponent(contextRole)}&userSearch=${encodeURIComponent(term)}`);
        if ($('stats-user-search').value.trim() !== term) return;
        data.users.forEach(x => $('stats-user').append(option(x)));
      } catch (e) { fail(e); }
    }, 250);
  });
  $('stats-reset').addEventListener('click', () => {
    ['stats-center', 'stats-user-search', 'stats-user', 'stats-from', 'stats-to'].forEach(id => { if ($(id)) $(id).value = ''; });
    range.value = 'all';
    load();
  });
  init();
})();
