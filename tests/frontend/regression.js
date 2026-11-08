(() => {
  const byId = id => document.getElementById(id);
  const tick = async () => { await Promise.resolve(); await Promise.resolve(); await Promise.resolve(); };
  const assert = (condition, message) => { if (!condition) throw new Error(message); };
  const result = [];
  const test = async (name, run) => {
    try { await run(); result.push({ name, status: 'PASS' }); }
    catch (error) { result.push({ name, status: 'FAIL', error: error.message }); }
    const item = document.createElement('li');
    const row = result.at(-1); item.textContent = `${row.status}: ${name}${row.error ? ' — ' + row.error : ''}`;
    item.className = row.status === 'PASS' ? 'text-success' : 'text-danger'; byId('results').append(item);
  };
  const directory = name => ({ total: 1, pageSize: 20, items: [{ userId: 7, fullNameAr: name, expertiseAr: [] }] });
  const summary = unread => ({ unread, latest: [{ titleAr: 'Fixture', bodyAr: 'Test', createdAt: '2026-11-01T20:59:00Z', isRead: false }] });
  const search = value => { byId('directory-search').value = value; byId('directory-filters').dispatchEvent(new Event('submit', { cancelable: true })); };
  byId('open-date-dialog').addEventListener('click', () => byId('date-dialog').showModal());
  byId('close-date-dialog').addEventListener('click', () => byId('date-dialog').close());
  byId('run').addEventListener('click', async () => {
    byId('run').disabled = true;
    await test('Optional pickers stay blank and initialize once', () => {
      ZuLmsPickers.init(document); ZuLmsPickers.init(document);
      assert(byId('optional-date').value === '', 'Blank input was populated');
      assert(document.querySelectorAll('.zu-picker-clear').length === 2, 'Missing or duplicate clear controls');
      assert(document.querySelectorAll('.daterangepicker').length === 3, 'Duplicate plugin instances');
    });
    await test('Typed calendar dates reject invalid days and accept leap days', () => {
      const input = byId('optional-date');
      input.value = '2026-02-31'; input.dispatchEvent(new Event('input'));
      assert(!input.checkValidity(), 'Invalid calendar date accepted');
      input.value = '2028-02-29'; input.dispatchEvent(new Event('input'));
      assert(input.checkValidity(), 'Leap day rejected');
    });
    await test('Optional clear resets value, validity, and emits change', () => {
      const input = byId('optional-date'); let changes = 0;
      input.addEventListener('change', () => changes++);
      input.nextElementSibling.click();
      assert(ZuLmsPickers.value(input.id) === null && input.checkValidity() && changes === 1, 'Clear contract failed');
      assert(!jQuery(input).data('daterangepicker').isShowing, 'Clear reopened the picker');
    });
    await test('Dialog picker stays in top layer and preserves minute precision', () => {
      const dialog = byId('date-dialog'); dialog.showModal();
      const picker = jQuery(byId('instant')).data('daterangepicker'); picker.show();
      assert(picker.container.closest('dialog')[0] === dialog, 'Picker outside dialog');
      assert(picker.startDate.format('YYYY-MM-DD HH:mm') === '2026-11-01 23:59', 'Start minute changed');
      assert(picker.endDate.format('YYYY-MM-DD HH:mm') === '2026-11-01 23:59', 'End minute changed');
      picker.hide(); dialog.close();
    });
    await test('Midnight values and programmatic edits survive picker reopening', () => {
      const input = byId('instant'), dialog = byId('date-dialog');
      input.value = '2026-02-31 00:00'; input.dispatchEvent(new Event('input'));
      assert(!input.checkValidity(), 'Invalid date-time accepted');
      input.value = '2026-11-02 00:00';
      dialog.showModal(); const picker = jQuery(input).data('daterangepicker'); picker.show();
      assert(input.checkValidity() && picker.startDate.format('YYYY-MM-DD HH:mm') === input.value, 'Programmatic midnight value changed');
      picker.hide(); dialog.close();
    });
    await test('Required prompt rejects whitespace; cancel returns null and restores focus', async () => {
      byId('run').disabled = false; byId('run').focus();
      const promise = ZuLms.ui.prompt('Reason', { required: true });
      const dialog = document.querySelector('.zu-question-dialog[open]');
      dialog.querySelector('textarea').value = '  ';
      dialog.querySelector('form').dispatchEvent(new Event('submit', { cancelable: true }));
      assert(dialog.open, 'Whitespace accepted');
      dialog.querySelector('button[type="button"]').click();
      assert(await promise === null && document.activeElement === byId('run'), 'Cancel/focus contract failed');
      byId('run').disabled = true;
    });
    await test('Confirm cancellation and custom labels preserve public contracts', async () => {
      const promise = ZuLms.ui.confirm('Proceed?', { label: 'Delete', danger: true });
      const dialog = document.querySelector('.zu-question-dialog[open]');
      assert(dialog.querySelector('button[type="submit"]').textContent === 'Delete', 'Custom label ignored');
      dialog.dispatchEvent(new Event('cancel', { cancelable: true }));
      assert(await promise === false, 'Escape did not resolve false');
    });
    await test('Directory filter failure remains visible after a successful list load', async () => {
      fixture.take('/users/faculties').reject(new Error('offline'));
      fixture.take('/profiles/directory').resolve(directory('Initial'));
      await tick();
      assert(!byId('directory-filter-error').classList.contains('d-none'), 'Filter error was lost');
      assert(byId('directory-center').disabled, 'Broken filter is enabled');
    });
    await test('Directory faculty retry restores the supported server filter', async () => {
      byId('directory-filter-retry').click();
      fixture.take('/users/faculties').resolve([{ id: 1, nameAr: 'Fixture faculty' }]); await tick();
      assert(!byId('directory-center').disabled && byId('directory-center').options.length === 2, 'Retry failed');
    });
    await test('Directory ignores stale success and stale failure responses', async () => {
      search('older'); const older = fixture.take('/profiles/directory');
      search('newer'); const newer = fixture.take('/profiles/directory');
      newer.resolve(directory('Newest')); await tick(); older.resolve(directory('Old')); await tick();
      assert(byId('directory-cards').textContent.includes('Newest') && !byId('directory-cards').textContent.includes('Old'), 'Stale success replaced cards');
      search('fail'); const failure = fixture.take('/profiles/directory');
      search('current'); const current = fixture.take('/profiles/directory');
      failure.reject(new Error('stale failure')); await tick();
      assert(byId('directory-cards').getAttribute('aria-busy') === 'true', 'Stale failure cleared busy state');
      current.resolve(directory('Current')); await tick();
      assert(byId('directory-error').classList.contains('d-none'), 'Stale failure shown');
    });
    await test('Directory exposes a safe read retry after failure', async () => {
      search('retry'); fixture.take('/profiles/directory').reject(new Error('offline')); await tick();
      byId('directory-cards').querySelector('button').click();
      fixture.take('/profiles/directory').resolve(directory('Recovered')); await tick();
      assert(byId('directory-cards').textContent.includes('Recovered'), 'Read retry failed');
    });
    await test('Notification initial loading and latest-request-wins', async () => {
      assert(byId('notifications-list').textContent.includes('Loading'), 'No loading state');
      const older = fixture.take('/notifications');
      byId('notifications-bell').dispatchEvent(new Event('shown.bs.dropdown'));
      fixture.take('/notifications').resolve(summary(2)); await tick(); older.resolve(summary(8)); await tick();
      assert(byId('notifications-count').textContent === '2', 'Stale unread count restored');
    });
    await test('Notification read-all prevents double writes and displays write failure', async () => {
      const button = byId('notifications-list').querySelector('button'); button.click(); button.click();
      assert(fixture.pending.filter(row => row.method === 'POST').length === 1, 'Duplicate write');
      fixture.take('/notifications/read-all', 'POST').reject(new Error('offline')); await tick();
      assert(byId('notifications-list').querySelector('[role="alert"]') && !button.disabled, 'Write failure hidden');
    });
    await test('Notification read-all succeeds only after server refresh', async () => {
      byId('notifications-list').querySelector('button').click();
      fixture.take('/notifications/read-all', 'POST').resolve(); await tick();
      fixture.take('/notifications').resolve({ unread: 0, latest: [] }); await tick();
      assert(byId('notifications-count').classList.contains('d-none') && byId('notifications-list').textContent === 'No notifications', 'Read state not refreshed');
    });
    window.frontendRegressionResults = result;
    document.title = `${result.filter(row => row.status === 'PASS').length}/${result.length} PASS — SIMULATED-UI`;
  }, { once: true });
})();
