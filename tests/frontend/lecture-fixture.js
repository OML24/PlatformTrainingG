(() => {
  const fixture = window.lectureFixture = { writes: [], failure: null };
  const row = { id: 99, titleAr: 'Fixture lecture', mode: 'InPerson', startsAt: '2026-11-01T06:33:00Z', endsAt: '2026-11-01T06:43:00Z', locationAr: 'Test room' };
  window.ZuLms = { api: { async request(method, path, body) {
    if (method === 'GET' && path.includes('/directory/')) return { course: { status: 'Approved' } };
    if (method === 'GET' && path.endsWith('/lectures')) return [row];
    if (method === 'GET' && path.endsWith('/exam')) return { exam: null };
    if (method === 'POST' || method === 'PUT') {
      fixture.writes.push({ method, path, body });
      if (fixture.failure) { const failure = fixture.failure; fixture.failure = null; throw failure; }
      return { id: 99 };
    }
    throw new Error(`Unexpected fixture request: ${method} ${path}`);
  } } };
})();
