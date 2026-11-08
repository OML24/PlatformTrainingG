(() => {
  const root = document.getElementById('eval-form-page'); if (!root) return;
  const $ = name => document.getElementById(`eval-form-${name}`);
  let draft = null;
  function fail(e) { $('error').textContent = e.code || e.message || 'تعذّر إكمال الطلب';
    $('error').classList.remove('d-none'); }
  function success(message) { $('success').textContent = message;
    $('success').classList.remove('d-none'); $('error').classList.add('d-none'); }
  function button(label, action, className = 'btn btn-sm btn-outline-secondary') {
    const node = document.createElement('button'); node.type = 'button';
    node.className = className; node.textContent = label;
    node.addEventListener('click', action); return node;
  }
  function textInput(value, placeholder, onChange) {
    const node = document.createElement('input'); node.type = 'text';
    node.className = 'form-control'; node.value = value || '';
    node.placeholder = placeholder; node.addEventListener('input', () => onChange(node.value));
    return node;
  }
  function move(list, index, direction) {
    const target = index + direction;
    if (target < 0 || target >= list.length) return;
    [list[index], list[target]] = [list[target], list[index]]; render();
  }
  function render() {
    const host = $('questions'); host.replaceChildren();
    draft.questions.forEach((question, index) => {
      const card = document.createElement('section');
      card.className = 'card card-body mb-3';
      const heading = document.createElement('h2'); heading.className = 'h6';
      heading.textContent = `السؤال ${index + 1}`; card.append(heading);
      card.append(textInput(question.textAr, 'نص السؤال بالعربية',
        value => { question.textAr = value; }));
      if (question.id) {
        const label = document.createElement('label'); label.className = 'mt-2';
        const check = document.createElement('input'); check.type = 'checkbox';
        check.className = 'form-check-input me-2';
        check.checked = question.keepQuestionKey;
        check.addEventListener('change', () => { question.keepQuestionKey = check.checked; });
        label.append(check, document.createTextNode('هذا نفس السؤال وقابل للمقارنة رغم تعديل الصياغة'));
        card.append(label);
      }
      const controls = document.createElement('div'); controls.className = 'd-flex gap-1 my-2';
      controls.append(button('↑', () => move(draft.questions, index, -1)),
        button('↓', () => move(draft.questions, index, 1)),
        button('حذف السؤال', () => { draft.questions.splice(index, 1); render(); },
          'btn btn-sm btn-outline-danger'));
      card.append(controls);
      const options = document.createElement('div');
      question.options.forEach((option, optionIndex) => {
        const row = document.createElement('div');
        row.className = 'd-flex gap-2 align-items-center mb-2';
        row.append(textInput(option.labelAr, 'نص الخيار',
          value => { option.labelAr = value; }));
        const score = document.createElement('input'); score.type = 'number';
        score.className = 'form-control'; score.style.maxWidth = '7rem';
        score.min = '-32768'; score.max = '32767';
        score.placeholder = 'الدرجة'; score.value = option.score ?? '';
        score.addEventListener('input', () => { option.score = score.value === ''
          ? null : Number(score.value); });
        row.append(score,
          button('↑', () => move(question.options, optionIndex, -1)),
          button('↓', () => move(question.options, optionIndex, 1)),
          button('حذف', () => { question.options.splice(optionIndex, 1); render(); },
            'btn btn-sm btn-outline-danger'));
        options.append(row);
      });
      card.append(options, button('إضافة خيار', () => {
        question.options.push({ labelAr: '', score: null }); render();
      }, 'btn btn-sm btn-outline-primary'));
      host.append(card);
    });
  }
  async function load() {
    const data = await ZuLms.api.request('GET', '/api/v1/evaluation-form/draft');
    draft = { id: data.id, titleAr: data.titleAr,
      questions: data.questions.map(q => ({ id: q.id,
        keepQuestionKey: true, textAr: q.textAr,
        options: q.options.map(o => ({ labelAr: o.labelAr, score: o.score })) })) };
    $('title').value = draft.titleAr; render();
  }
  $('title').addEventListener('input', () => { if (draft) draft.titleAr = $('title').value; });
  $('add').addEventListener('click', () => {
    if (!draft) return;
    draft.questions.push({ id: null, keepQuestionKey: false,
      textAr: '', options: [{ labelAr: '', score: null }] }); render();
  });
  $('save').addEventListener('click', async () => {
    if (!draft) return;
    $('save').disabled = true;
    try {
      await ZuLms.api.request('PUT', `/api/v1/evaluation-form/draft/${draft.id}`, {
        titleAr: draft.titleAr,
        questions: draft.questions.map(q => ({ existingId: q.id,
          keepQuestionKey: q.keepQuestionKey, textAr: q.textAr,
          options: q.options.map(o => ({ labelAr: o.labelAr, score: o.score })) }))
      });
      success('حُفظت المسودة'); await load();
    } catch (e) { fail(e); }
    finally { $('save').disabled = false; }
  });
  $('publish').addEventListener('click', async () => {
    if (!draft || !await ZuLms.ui.confirm('نشر هذه النسخة؟ لن يمكن تعديلها بعد النشر.')) return;
    $('publish').disabled = true;
    try {
      const result = await ZuLms.api.request('POST',
        `/api/v1/evaluation-form/draft/${draft.id}/publish`);
      success(`نُشرت النسخة ${result.versionNo}`); await load();
    } catch (e) { fail(e); }
    finally { $('publish').disabled = false; }
  });
  load().catch(fail);
})();
