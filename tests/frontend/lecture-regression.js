(() => {
  const $ = name => document.getElementById(`lectures-${name}`);
  const results = [];
  const assert = (condition, message) => { if (!condition) throw new Error(message); };
  const settle = async () => { for (let i = 0; i < 10; i++) await Promise.resolve(); };
  const set = (name, value) => { $(name).value = value; $(name).dispatchEvent(new Event('input', { bubbles: true })); };
  async function test(name, action) {
    try { await action(); results.push({ name, status: 'PASS' }); }
    catch (error) { results.push({ name, status: 'FAIL', error: error.message }); }
    const row = results.at(-1), item = document.createElement('li');
    item.textContent = `${row.status}: ${name}${row.error ? ' — ' + row.error : ''}`;
    document.getElementById('lecture-check-results').append(item);
  }
  document.getElementById('run-lecture-checks').addEventListener('click', async event => {
    event.target.disabled = true;
    await test('Create sends ISO local times with original Amman hours/minutes', async () => {
      set('title', 'مقدمة'); set('start', '2026-11-01 09:33'); set('end', '2026-11-01 09:43'); set('location', 'مدرج القدس');
      $('form').requestSubmit(); await settle();
      const write = lectureFixture.writes.at(-1);
      assert(write?.method === 'POST' && write.path === '/api/v1/courses/7/lectures', 'Create was not submitted');
      assert(write.body.startsAt === '2026-11-01T09:33:00' && write.body.endsAt === '2026-11-01T09:43:00', 'Wrong local serialization');
      assert(!$('success').classList.contains('d-none') && !$('submit').disabled, 'Success/submission state failed');
    });
    await test('Edit populates Amman values and PUT preserves 23:59/midnight', async () => {
      const edit = [...$('rows').querySelectorAll('button')].find(button => button.textContent === 'تعديل الموعد/المكان');
      edit.click();
      assert($('start').value === '2026-11-01 09:33' && $('end').value === '2026-11-01 09:43', 'Edit display shifted time');
      set('start', '2026-11-01 23:59'); set('end', '2026-11-02 00:00');
      $('form').requestSubmit(); await settle();
      const write = lectureFixture.writes.at(-1);
      assert(write.method === 'PUT' && write.path === '/api/v1/lectures/99', 'Wrong edit endpoint');
      assert(write.body.startsAt === '2026-11-01T23:59:00' && write.body.endsAt === '2026-11-02T00:00:00', 'Edit midnight shifted');
    });
    await test('Validation errors show field labels/details safely and retain input', async () => {
      set('title', 'Input retained'); set('start', '2026-11-01 09:33'); set('end', '2026-11-01 09:43'); set('location', 'Test room');
      lectureFixture.failure = { code: 'VALIDATION_FAILED', fieldErrors: { '$.startsAt': ['Invalid date <b>test</b>'], EndsAt: ['End must follow start'] } };
      $('form').requestSubmit(); await settle();
      assert($('error').textContent.includes('البداية بتوقيت عمّان: Invalid date <b>test</b>'), 'Start detail/label missing');
      assert($('error').textContent.includes('النهاية بتوقيت عمّان: End must follow start'), 'End detail/label missing');
      assert(!$('error').querySelector('b'), 'Server text was rendered as HTML');
      assert($('start').classList.contains('is-invalid') && $('end').classList.contains('is-invalid'), 'Field markers missing');
      assert($('title').value === 'Input retained' && !$('submit').disabled, 'Rejected submit lost input or stayed disabled');
    });
    await test('Correcting and retrying clears validation state', async () => {
      set('start', '2026-11-01 09:34');
      assert(!$('start').classList.contains('is-invalid'), 'Corrected field stayed invalid');
      $('form').requestSubmit(); await settle();
      assert($('error').classList.contains('d-none') && !$('form').querySelector('.is-invalid'), 'Old errors survived success');
    });
    await test('Readable server message takes precedence over technical error code', async () => {
      set('start', '2026-11-01 09:33'); set('end', '2026-11-01 09:43'); set('location', 'Test room');
      lectureFixture.failure = { code: 'LECTURE_INVALID', message: 'راجع موعد المحاضرة' };
      $('form').requestSubmit(); await settle();
      assert($('error').textContent === 'راجع موعد المحاضرة', 'Technical code hid message');
    });
    window.lectureRegressionResults = results;
    document.title = `${results.filter(row => row.status === 'PASS').length}/${results.length} lecture checks PASS — SIMULATED-UI`;
  }, { once: true });
})();
