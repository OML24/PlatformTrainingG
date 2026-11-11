// ZU-LMS PDF reports (docs/FRONTEND-REDESIGN.md). No external library:
// pages are drawn on A4 canvases (so Arabic shaping/RTL comes from the browser),
// encoded as JPEG and wrapped in a minimal PDF 1.4 file.
//   const rep = await ZuPdf.create({ title, subtitle, meta:[[label,value]], grayscale });
//   rep.section(..); rep.kpis(..); rep.chart(..); rep.bars(..); rep.table(..);
//   await rep.save('file.pdf');
// ZuPdf.openDialog({ title, sections:[{key,label,checked}], onExport(opts) }) shows the options dialog.
(() => {
  'use strict';
  const W = 1240, H = 1754, M = 72;           // layout units (A4 @150dpi), margin
  const SCALE = 2;                             // drawn at 2x → 2480×3508 px = A4 @300dpi (print quality)
  const CW = W - 2 * M;                        // content width
  const FOOT = 70;                             // footer band
  const FONT = 'Tajawal, Arial, sans-serif';
  const C = { ink: '#0f172a', navy: '#022c5a', muted: '#5b6b80', line: '#e3e8ef', soft: '#f1f4f9', gold: '#c9a227', paper: '#ffffff' };
  const lang = () => (document.documentElement.lang === 'en' ? 'en' : 'ar');
  const L = (ar, en) => (window.ZuLmsI18n?.localize ? ZuLmsI18n.localize(ar, en) : (lang() === 'en' ? en : ar));

  function loadImage(src) {
    return new Promise(resolve => {
      const img = new Image();
      img.onload = () => resolve(img); img.onerror = () => resolve(null);
      img.src = src;
    });
  }

  class Report {
    constructor(opts) {
      this.o = opts; this.rtl = document.documentElement.dir === 'rtl';
      this.pages = []; this.y = 0;
    }
    async init() {
      try { await Promise.all([document.fonts.load(`400 20px ${FONT}`), document.fonts.load(`700 20px ${FONT}`), document.fonts.load(`800 20px ${FONT}`)]); } catch { /* fallback font */ }
      this.logo = await loadImage('/img/logo.png');
      this.newPage(true);
      return this;
    }
    /* ---------- geometry helpers (logical start/end so RTL mirrors) ---------- */
    x(offset, width = 0) { return this.rtl ? W - M - offset - width : M + offset; }
    text(str, offset, y, { size = 22, weight = 400, color = C.ink, align = 'start', maxWidth, width = 0 } = {}) {
      const ctx = this.ctx;
      ctx.font = `${weight} ${size}px ${FONT}`; ctx.fillStyle = color;
      ctx.direction = this.rtl ? 'rtl' : 'ltr'; ctx.textBaseline = 'alphabetic';
      let s = String(str ?? '');
      if (maxWidth) s = this.fit(s, maxWidth);
      let px;
      if (align === 'center') { ctx.textAlign = 'center'; px = this.x(offset, width) + width / 2; }
      else if (align === 'end') { ctx.textAlign = this.rtl ? 'left' : 'right'; px = this.rtl ? this.x(offset, width) : this.x(offset, width) + width; }
      else { ctx.textAlign = this.rtl ? 'right' : 'left'; px = this.rtl ? W - M - offset : M + offset; }
      ctx.fillText(s, px, y);
    }
    fit(s, maxWidth) {
      const ctx = this.ctx;
      if (ctx.measureText(s).width <= maxWidth) return s;
      while (s.length > 1 && ctx.measureText(s + '…').width > maxWidth) s = s.slice(0, -1);
      return s + '…';
    }
    rect(offset, y, w, h, { fill, stroke, radius = 14, lineWidth = 2 } = {}) {
      const ctx = this.ctx, px = this.x(offset, w);
      ctx.beginPath();
      ctx.roundRect ? ctx.roundRect(px, y, w, h, radius) : ctx.rect(px, y, w, h);
      if (fill) { ctx.fillStyle = fill; ctx.fill(); }
      if (stroke) { ctx.strokeStyle = stroke; ctx.lineWidth = lineWidth; ctx.stroke(); }
    }
    /* ---------- pages ---------- */
    newPage(first = false) {
      const canvas = document.createElement('canvas');
      canvas.width = W * SCALE; canvas.height = H * SCALE;
      this.ctx = canvas.getContext('2d');
      this.ctx.setTransform(SCALE, 0, 0, SCALE, 0, 0);
      this.ctx.imageSmoothingQuality = 'high';
      this.ctx.fillStyle = C.paper; this.ctx.fillRect(0, 0, W, H);
      this.pages.push(canvas);
      this.y = M;
      if (first) this.header(); else this.runningHeader();
    }
    ensure(h) { if (this.y + h > H - M - FOOT && this.y > M + 90) this.newPage(); }
    header() {
      const o = this.o;
      if (this.logo) {
        const s = 92;
        this.ctx.drawImage(this.logo, this.x(0, s), this.y, s, s);
      }
      const t = 118;
      this.text(L('جامعة الزرقاء', 'Zarqa University'), t, this.y + 34, { size: 24, weight: 800, color: C.navy });
      this.text(L('منصة التدريب والتطوير المهني', 'Training & Professional Development'), t, this.y + 70, { size: 20, color: C.muted });
      const now = new Intl.DateTimeFormat(lang() === 'en' ? 'en-GB' : 'ar-JO-u-nu-latn', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Asia/Amman' }).format(new Date());
      this.text(now, 0, this.y + 34, { size: 19, color: C.muted, align: 'end', width: CW });
      this.y += 120;
      this.ctx.fillStyle = C.gold; this.ctx.fillRect(this.x(0, CW), this.y, CW, 4);
      this.y += 64;
      this.text(o.title, 0, this.y, { size: 46, weight: 800, color: C.ink, maxWidth: CW });
      this.y += 18;
      if (o.subtitle) { this.y += 34; this.text(o.subtitle, 0, this.y, { size: 22, color: C.muted, maxWidth: CW }); }
      if (o.meta?.length) {
        this.y += 30;
        const rowH = 44, cols = 2, colW = (CW - 16) / cols;
        const rows = Math.ceil(o.meta.length / cols);
        this.rect(0, this.y, CW, rows * rowH + 24, { fill: C.soft, radius: 16 });
        o.meta.forEach(([k, v], i) => {
          const col = i % cols, row = Math.floor(i / cols);
          const off = 24 + col * colW, yy = this.y + 44 + row * rowH;
          this.text(`${k}:`, off, yy, { size: 20, weight: 700, color: C.muted });
          this.ctx.font = `700 20px ${FONT}`;
          const kw = this.ctx.measureText(`${k}: `).width;
          this.text(v, off + kw + 6, yy, { size: 20, weight: 700, color: C.ink, maxWidth: colW - kw - 40 });
        });
        this.y += rows * rowH + 24;
      }
      this.y += 40;
    }
    runningHeader() {
      this.text(this.o.title, 0, this.y + 22, { size: 20, weight: 700, color: C.muted, maxWidth: CW * 0.7 });
      this.text(L('جامعة الزرقاء', 'Zarqa University'), 0, this.y + 22, { size: 20, color: C.muted, align: 'end', width: CW });
      this.y += 40;
      this.ctx.fillStyle = C.line; this.ctx.fillRect(this.x(0, CW), this.y, CW, 2);
      this.y += 40;
    }
    footers() {
      const n = this.pages.length;
      this.pages.forEach((canvas, i) => {
        this.ctx = canvas.getContext('2d');
        const y = H - M + 10;
        this.ctx.fillStyle = C.line; this.ctx.fillRect(this.x(0, CW), y - 40, CW, 2);
        this.text(L(`صفحة ${i + 1} من ${n}`, `Page ${i + 1} of ${n}`), 0, y, { size: 18, color: C.muted });
        this.text(L('منصة التدريب — جامعة الزرقاء', 'Training portal — Zarqa University'), 0, y, { size: 18, color: C.muted, align: 'end', width: CW });
      });
    }
    /* ---------- blocks ---------- */
    section(title, sub) { this.flushSection(); this.pending = { title, sub }; }
    // Reserve room for a block plus any pending section header, then draw the header.
    place(h) {
      const head = this.pending ? (this.pending.sub ? 112 : 82) : 0;
      this.ensure(h + head);
      if (this.pending) { const p = this.pending; this.pending = null; this.drawSection(p.title, p.sub); }
    }
    flushSection() { if (this.pending) this.place(0); }
    drawSection(title, sub) {
      this.ctx.fillStyle = C.navy; this.ctx.fillRect(this.x(0, 6), this.y - 4, 6, 34);
      this.text(title, 20, this.y + 24, { size: 30, weight: 800, color: C.ink, maxWidth: CW - 20 });
      this.y += 38;
      if (sub) { this.text(sub, 20, this.y + 20, { size: 20, color: C.muted, maxWidth: CW - 20 }); this.y += 30; }
      this.y += 22;
    }
    kpis(items, perRow = 4) {
      const gap = 18, w = (CW - gap * (perRow - 1)) / perRow, h = 170;
      for (let i = 0; i < items.length; i += perRow) {
        this.place(h + gap);
        items.slice(i, i + perRow).forEach((it, j) => {
          const off = j * (w + gap);
          this.rect(off, this.y, w, h, { fill: '#fff', stroke: C.line });
          this.ctx.fillStyle = it.color || C.navy; this.rect(off, this.y, w, 6, { fill: it.color || C.navy, radius: 3 });
          this.text(it.label, off + 22, this.y + 48, { size: 21, weight: 700, color: C.muted, maxWidth: w - 44 });
          this.text(it.value, off + 22, this.y + 108, { size: 50, weight: 800, color: C.ink, maxWidth: w - 44 });
          if (it.note) this.text(it.note, off + 22, this.y + 148, { size: 17, color: C.muted, maxWidth: w - 44 });
        });
        this.y += h + gap;
      }
      this.y += 20;
    }
    // A chart canvas, optionally with a legend list beside it.
    chart(source, { height = 520, legend } = {}) {
      if (!source || !source.width || !source.height) return;
      const boxW = legend ? CW * 0.48 : CW;
      const ratio = source.height / source.width;
      let w = boxW, h = boxW * ratio;
      if (h > height) { h = height; w = h / ratio; }
      const legendH = legend ? legend.length * 50 + 10 : 0;
      const blockH = Math.max(h, legendH) + 30;
      this.place(blockH);
      this.rect(0, this.y, CW, blockH, { stroke: C.line, radius: 16 });
      const imgOff = legend ? 20 : (CW - w) / 2;
      this.ctx.drawImage(source, this.x(imgOff, w), this.y + 15 + (blockH - 30 - h) / 2, w, h);
      if (legend) {
        const off = boxW + 50, lw = CW - off - 30;
        let yy = this.y + 15 + (blockH - 30 - legendH) / 2 + 36;
        legend.forEach(r => {
          this.ctx.fillStyle = r.color; this.ctx.beginPath();
          const cx = this.rtl ? W - M - off - 10 : M + off + 10;
          this.ctx.arc(cx, yy - 8, 9, 0, Math.PI * 2); this.ctx.fill();
          this.text(r.label, off + 32, yy, { size: 22, color: C.ink, maxWidth: lw - 220 });
          this.text(r.value, off, yy, { size: 20, color: C.muted, align: 'end', width: lw - 90 });
          this.text(r.pct || '', off, yy, { size: 22, weight: 800, color: C.ink, align: 'end', width: lw });
          yy += 50;
        });
      }
      this.y += blockH + 30;
    }
    bars(rows, { color = C.navy, tint = '#8fa9c8' } = {}) {
      const max = Math.max(1, ...rows.map(r => r.value));
      const rowH = 58;
      rows.forEach((r, i) => {
        this.place(i === 0 ? rowH * Math.min(rows.length, 3) : rowH);
        const top = this.y;
        this.rect(0, top + 4, 40, 40, { fill: i === 0 ? C.gold : C.soft, radius: 10 });
        this.text(String(i + 1), 0, top + 32, { size: 20, weight: 800, color: i === 0 ? C.ink : C.muted, align: 'center', width: 40 });
        this.text(r.label, 56, top + 22, { size: 21, weight: 700, color: C.ink, maxWidth: CW - 200 });
        this.text(String(r.value), 0, top + 22, { size: 21, weight: 800, color: C.ink, align: 'end', width: CW });
        this.rect(56, top + 34, CW - 56, 10, { fill: C.soft, radius: 5 });
        const bw = Math.max(6, (CW - 56) * r.value / max);
        this.rect(56, top + 34, bw, 10, { fill: i === 0 ? color : tint, radius: 5 });
        this.y += rowH;
      });
      this.y += 24;
    }
    // Wrap text into at most `max` lines that fit `width` (last line gets an ellipsis if needed).
    lines(str, width, font, max = 2) {
      const ctx = this.ctx; ctx.font = font;
      const words = String(str ?? '').split(/\s+/).filter(Boolean);
      const out = []; let cur = '';
      for (const w of words) {
        const test = cur ? `${cur} ${w}` : w;
        if (ctx.measureText(test).width <= width || !cur) cur = test;
        else { out.push(cur); cur = w; }
      }
      if (cur) out.push(cur);
      if (out.length > max) { const rest = out.slice(max - 1).join(' '); out.length = max - 1; out.push(rest); }
      return out.map(l => this.fit(l, width));
    }
    table(head, rows, widths, { size = 18 } = {}) {
      const total = widths.reduce((a, b) => a + b, 0);
      const cols = widths.map(w => (w / total) * CW);
      const lh = size + 8, pad = 16;
      const cellLines = (r, bold) => r.map((c, i) => this.lines(c, cols[i] - 20, `${bold || i === 0 ? 700 : 400} ${size}px ${FONT}`));
      const drawRow = (r, { header = false } = {}) => {
        const ls = cellLines(r, header);
        const hgt = Math.max(...ls.map(x => x.length)) * lh + pad * 2 - 8;
        if (header) this.rect(0, this.y, CW, hgt, { fill: C.soft, radius: 10 });
        let off = 0;
        ls.forEach((cl, i) => {
          cl.forEach((line, k) => this.text(line, off + 10, this.y + pad + size - 2 + k * lh,
            { size, weight: header || i === 0 ? 700 : 400, color: header ? C.muted : C.ink }));
          off += cols[i];
        });
        this.y += hgt;
        if (!header) { this.ctx.fillStyle = C.line; this.ctx.fillRect(this.x(0, CW), this.y - 1, CW, 1); }
        return hgt;
      };
      const headH = Math.max(...cellLines(head, true).map(x => x.length)) * lh + pad * 2;
      this.place(headH + lh * 3 * Math.min(rows.length, 3));
      drawRow(head, { header: true });
      if (!rows.length) { this.text(L('لا توجد بيانات', 'No data'), 0, this.y + 36, { size: 20, color: C.muted, align: 'center', width: CW }); this.y += 52; }
      rows.forEach(r => {
        const need = Math.max(...cellLines(r).map(x => x.length)) * lh + pad * 2;
        if (this.y + need > H - M - FOOT) { this.newPage(); drawRow(head, { header: true }); }
        drawRow(r);
      });
      this.y += 30;
    }
    note(textStr) { this.place(50); this.text(textStr, 0, this.y + 24, { size: 20, color: C.muted, maxWidth: CW }); this.y += 50; }
    /* ---------- output ---------- */
    async save(filename) {
      this.flushSection();
      this.footers();
      const pages = [];
      for (const canvas of this.pages) {
        let out = canvas;
        if (this.o.grayscale) {
          const PW = canvas.width, PH = canvas.height;
          out = document.createElement('canvas'); out.width = PW; out.height = PH;
          const g = out.getContext('2d'); g.drawImage(canvas, 0, 0);
          const img = g.getImageData(0, 0, PW, PH), d = img.data;
          for (let i = 0; i < d.length; i += 4) { const v = 0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2]; d[i] = d[i + 1] = d[i + 2] = v; }
          g.putImageData(img, 0, 0);
        }
        const blob = await new Promise(r => out.toBlob(r, 'image/jpeg', 0.95));
        pages.push(new Uint8Array(await blob.arrayBuffer()));
      }
      const pdf = buildPdf(pages, W * SCALE, H * SCALE);
      const url = URL.createObjectURL(pdf);
      const a = document.createElement('a');
      a.href = url; a.download = filename; document.body.append(a); a.click(); a.remove();
      setTimeout(() => URL.revokeObjectURL(url), 4000);
    }
  }

  // Minimal PDF 1.4: one full-page JPEG per A4 page.
  function buildPdf(jpegs, w, h) {
    const enc = new TextEncoder();
    const parts = []; let len = 0; const offs = [];
    const push = d => { const b = typeof d === 'string' ? enc.encode(d) : d; parts.push(b); len += b.length; };
    const obj = (id, write) => { offs[id] = len; push(`${id} 0 obj\n`); write(); push('\nendobj\n'); };
    push('%PDF-1.4\n%âãÏÓ\n');
    const n = jpegs.length, size = 3 + 3 * n;
    obj(1, () => push('<< /Type /Catalog /Pages 2 0 R >>'));
    obj(2, () => push(`<< /Type /Pages /Kids [${jpegs.map((_, i) => `${3 + 3 * i} 0 R`).join(' ')}] /Count ${n} >>`));
    jpegs.forEach((jpeg, i) => {
      const id = 3 + 3 * i;
      obj(id, () => push(`<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595.28 841.89] /Resources << /XObject << /Im${i} ${id + 2} 0 R >> /ProcSet [/PDF /ImageC] >> /Contents ${id + 1} 0 R >>`));
      const cs = `q 595.28 0 0 841.89 0 0 cm /Im${i} Do Q`;
      obj(id + 1, () => push(`<< /Length ${cs.length} >>\nstream\n${cs}\nendstream`));
      obj(id + 2, () => { push(`<< /Type /XObject /Subtype /Image /Width ${w} /Height ${h} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length ${jpeg.length} >>\nstream\n`); push(jpeg); push('\nendstream'); });
    });
    const xref = len;
    push(`xref\n0 ${size}\n0000000000 65535 f \n`);
    for (let id = 1; id < size; id++) push(`${String(offs[id]).padStart(10, '0')} 00000 n \n`);
    push(`trailer\n<< /Size ${size} /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF\n`);
    return new Blob(parts, { type: 'application/pdf' });
  }

  /* ---------- options dialog ---------- */
  function openDialog({ title, sections = [], onExport }) {
    const dlg = document.createElement('dialog');
    dlg.className = 'zu-question-dialog zu-pdf-dialog';
    dlg.setAttribute('aria-labelledby', 'zu-pdf-title');
    const form = document.createElement('form');
    form.method = 'dialog';
    const secs = sections.map(s => `<label class="zu-pdf-check"><input type="checkbox" name="sec" value="${s.key}" ${s.checked === false ? '' : 'checked'} ${s.disabled ? 'disabled' : ''}><span>${s.label}</span></label>`).join('');
    form.innerHTML = `
      <div class="modal-header"><h2 class="h5 m-0" id="zu-pdf-title"><i class="ti ti-file-type-pdf" aria-hidden="true"></i> ${title || L('تنزيل التقرير PDF', 'Download PDF report')}</h2></div>
      <div class="modal-body">
        <p class="text-muted small mb-3">${L('يُنزَّل التقرير كما يظهر الآن على الشاشة: نفس الفلاتر ونفس شكل كل رسم.', 'The report matches what is on screen now: same filters and the same view for every chart.')}</p>
        ${sections.length ? `<fieldset class="mb-3"><legend class="form-label fs-6">${L('الأقسام المضمّنة', 'Sections to include')}</legend><div class="zu-pdf-grid">${secs}</div></fieldset>` : ''}
        <fieldset><legend class="form-label fs-6">${L('الألوان', 'Colours')}</legend>
          <div class="zu-pdf-seg">
            <label><input type="radio" name="color" value="color" checked><span><i class="ti ti-palette" aria-hidden="true"></i> ${L('ملوّن', 'Colour')}</span></label>
            <label><input type="radio" name="color" value="gray"><span><i class="ti ti-contrast" aria-hidden="true"></i> ${L('أبيض وأسود', 'Black & white')}</span></label>
          </div>
        </fieldset>
        <p class="zu-pdf-status small mt-3 mb-0" role="status" aria-live="polite"></p>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-outline-secondary" data-cancel>${L('إلغاء', 'Cancel')}</button>
        <button type="submit" class="btn btn-primary" data-go><i class="ti ti-download" aria-hidden="true"></i> ${L('تنزيل PDF', 'Download PDF')}</button>
      </div>`;
    dlg.append(form);
    document.body.append(dlg);
    const status = form.querySelector('.zu-pdf-status');
    const go = form.querySelector('[data-go]');
    const close = () => { dlg.close(); dlg.remove(); };
    form.querySelector('[data-cancel]').addEventListener('click', close);
    dlg.addEventListener('cancel', e => { e.preventDefault(); close(); });
    form.addEventListener('submit', async e => {
      e.preventDefault();
      const fd = new FormData(form);
      const opts = { sections: new Set(fd.getAll('sec')), grayscale: fd.get('color') === 'gray' };
      if (sections.length && !opts.sections.size) { status.textContent = L('اختر قسماً واحداً على الأقل.', 'Choose at least one section.'); return; }
      go.disabled = true; status.textContent = L('جارٍ تجهيز الملف…', 'Preparing the file…');
      try { await onExport(opts); close(); window.ZuLms?.toast?.(L('تم تنزيل التقرير.', 'Report downloaded.'), 'success'); }
      catch (err) { status.textContent = err?.message || L('تعذّر إنشاء الملف.', 'The file could not be created.'); go.disabled = false; }
    });
    dlg.showModal();
    go.focus();
  }

  // Re-render a Chart.js chart off-screen at `ratio`× pixel density so it stays sharp in the PDF
  // (the on-screen canvas is only as dense as the user's display). Returns { canvas, dispose }.
  function snapshot(chart, ratio = 3) {
    if (!chart || !window.Chart) return null;
    const w = Math.max(1, Math.round(chart.width)), h = Math.max(1, Math.round(chart.height));
    const host = document.createElement('div');
    host.style.cssText = `position:fixed;left:-100000px;top:0;width:${w}px;height:${h}px;pointer-events:none;`;
    const canvas = document.createElement('canvas');
    host.append(canvas); document.body.append(host);
    const src = chart.config;
    const data = { labels: [...(chart.data.labels || [])], datasets: chart.data.datasets.map(d => ({ ...d, data: [...d.data] })) };
    const options = { ...src.options, devicePixelRatio: ratio, responsive: false, maintainAspectRatio: false, animation: false,
      plugins: { ...(src.options.plugins || {}), tooltip: { enabled: false } } };
    // Same layout size as on screen; Chart.js multiplies the backing store by devicePixelRatio.
    canvas.width = w; canvas.height = h; canvas.style.width = `${w}px`; canvas.style.height = `${h}px`;
    let copy;
    try {
      copy = new Chart(canvas, { type: src.type, data, options, plugins: src.plugins || [] });
      if (src.type === 'doughnut' || src.type === 'pie') {
        data.labels.forEach((_, i) => { if (!chart.getDataVisibility(i)) copy.toggleDataVisibility(i); });
      }
      copy.update('none');
    } catch { host.remove(); return { canvas: chart.canvas, dispose() {} }; }
    return { canvas, dispose() { copy.destroy(); host.remove(); } };
  }

  window.ZuPdf = { create: opts => new Report(opts).init(), openDialog, snapshot, colors: C };
})();
