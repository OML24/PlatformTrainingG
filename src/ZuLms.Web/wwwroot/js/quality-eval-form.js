(() => {
  const root = document.getElementById('eval-form-page'); if (!root) return;
  const request = (method, path, body) => ZuLms.api.request(method, path, body);
  const questions = document.getElementById('eval-questions');
  let draftId = null;

  function message(kind, text) {
    const error = document.getElementById('form-error');
    const success = document.getElementById('form-success');
    error.classList.add('d-none'); success.classList.add('d-none');
    (kind === 'error' ? error : success).textContent = text;
    (kind === 'error' ? error : success).classList.remove('d-none');
  }

  function questionRow(question) {
    const card = document.createElement('div');
    card.className = 'card card-body';
    const header = document.createElement('div');
    header.className = 'd-flex gap-2 mb-2 align-items-center';
    const text = document.createElement('input');
    text.className = 'form-control'; text.value = question.textAr || '';
    text.placeholder = 'نص السؤال (عربي)';
    const keep = document.createElement('div');
    keep.className = 'form-check form-switch ms-2';
    const switchInput = document.createElement('input');
    switchInput.className = 'form-check-input'; switchInput.type = 'checkbox';
    switchInput.id = `keep-${question.id || Math.random().toString(36).slice(2)}`;
    switchInput.checked = true;
    const switchLabel = document.createElement('label');
    switchLabel.className = 'form-check-label'; switchLabel.htmlFor = switchInput.id;
    switchLabel.textContent = 'نفس السؤال (قابل للمقارنة)';
    keep.append(switchInput, switchLabel);
    const remove = document.createElement('button');
    remove.type = 'button'; remove.className = 'btn btn-sm btn-outline-danger';
    remove.textContent = 'حذف السؤال';
    remove.addEventListener('click', () => card.remove());
    header.append(text, keep, remove);
    card.append(header);

    const options = document.createElement('div');
    options.className = 'vstack gap-2';
    card.append(options);
    const addOption = document.createElement('button');
    addOption.type = 'button'; addOption.className = 'btn btn-sm btn-outline-secondary align-self-start';
    addOption.textContent = 'إضافة خيار';
    addOption.addEventListener('click', () => optionRow(options, null));
    card.append(addOption);
    for (const option of question.options || []) optionRow(options, option);
    return card;
  }

  function optionRow(host, option) {
    const row = document.createElement('div');
    row.className = 'row g-2';
    const labelColumn = document.createElement('div'); labelColumn.className = 'col-md-7';
    const label = document.createElement('input');
    label.className = 'form-control'; label.placeholder = 'نص الخيار (عربي)';
    label.value = option ? option.labelAr : '';
    labelColumn.append(label);
    const scoreColumn = document.createElement('div'); scoreColumn.className = 'col-md-3';
    const score = document.createElement('input');
    score.className = 'form-control'; score.type = 'number'; score.placeholder = 'الدرجة';
    score.value = option && option.score != null ? option.score : '';
    scoreColumn.append(score);
    const removeColumn = document.createElement('div'); removeColumn.className = 'col-md-2';
    const remove = document.createElement('button');
    remove.type = 'button'; remove.className = 'btn btn-sm btn-outline-danger w-100';
    remove.textContent = 'حذف';
    remove.addEventListener('click', () => row.remove());
    removeColumn.append(remove);
    row.append(labelColumn, scoreColumn, removeColumn);
    host.append(row);
  }

  function collect() {
    const payload = {
      titleAr: document.getElementById('eval-title').value.trim(),
      questions: []
    };
    for (const card of questions.querySelectorAll(':scope > .card')) {
      const text = card.querySelector('input.form-control');
      const keep = card.querySelector('.form-check-input');
      const options = [];
      for (const row of card.querySelectorAll('.row.g-2')) {
        const label = row.querySelector('input:not([type="number"])');
        const score = row.querySelector('input[type="number"]');
        if (label && label.value.trim())
          options.push({ labelAr: label.value.trim(), score: score.value ? Number(score.value) : null });
      }
      if (text.value.trim() && options.length >= 2)
        payload.questions.push({
          existingId: card.dataset.questionId ? Number(card.dataset.questionId) : null,
          keepQuestionKey: !!(keep && keep.checked), textAr: text.value.trim(), options
        });
    }
    return payload;
  }

  async function load() {
    try {
      const draft = await request('GET', '/api/v1/evaluation-form/draft');
      draftId = draft.id;
      document.getElementById('eval-title').value = draft.titleAr || '';
      document.getElementById('eval-meta').textContent =
        `مسودة النسخة رقم ${draft.versionNo} — ${draft.questions.length} سؤالاً`;
      questions.replaceChildren();
      for (const question of draft.questions || []) {
        const card = questionRow(question);
        card.dataset.questionId = question.id;
        questions.append(card);
      }
    } catch (e) { message('error', e.code || 'تعذّر جلب المسودة'); }
  }

  document.getElementById('eval-add-question').addEventListener('click', () =>
    questions.append(questionRow({ textAr: '', options: [] })));

  document.getElementById('eval-save').addEventListener('click', async () => {
    try {
      await request('PUT', `/api/v1/evaluation-form/draft/${draftId}`, collect());
      message('success', 'حُفظت المسودة'); await load();
    } catch (e) { message('error', e.code || 'تعذّر الحفظ'); }
  });

  document.getElementById('eval-publish').addEventListener('click', async () => {
    if (!await ZuLms.ui.confirm('نشر هذه النسخة؟ لن تكون قابلة للتعديل بعد النشر.')) return;
    try {
      await request('PUT', `/api/v1/evaluation-form/draft/${draftId}`, collect());
      const result = await request('POST', `/api/v1/evaluation-form/draft/${draftId}/publish`);
      message('success', `نُشرت النسخة رقم ${result.versionNo}`);
      await load();
    } catch (e) { message('error', e.code || 'تعذّر النشر'); }
  });

  load();
})();
