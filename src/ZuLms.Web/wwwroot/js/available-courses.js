(() => {
  'use strict';
  const root = document.getElementById('available-courses-page'); if (!root) return;
  const rows = document.getElementById('available-courses-rows');
  const error = document.getElementById('available-courses-error');
  const text = root.dataset;
  let page = 1, totalPages = 1;
  function state(message, isError = false) {
    rows.replaceChildren();
    const column = document.createElement('div'); column.className = 'col-12';
    if (!isError) {
      const note = document.createElement('p'); note.className = 'empty-state';
      note.textContent = message; note.setAttribute('role', 'status');
      column.append(note);
    }
    if (isError) {
      const retry = document.createElement('button'); retry.type = 'button';
      retry.className = 'btn btn-outline-primary'; retry.textContent = text.retry;
      retry.addEventListener('click', load); column.append(retry);
    }
    rows.append(column);
  }
  function label(host, title, value) {
    const p = document.createElement('p'); p.className = 'mb-2 small text-secondary';
    const strong = document.createElement('strong'); strong.className = 'text-body';
    strong.textContent = `${title}: `;
    const span = document.createElement('span'); span.textContent = value || '—';
    p.append(strong, span); host.append(p);
  }
  function render(course) {
    const column = document.createElement('div'); column.className = 'col-12 col-md-6 col-xl-4';
    const article = document.createElement('article'); article.className = 'card h-100';
    const body = document.createElement('div'); body.className = 'card-body d-flex flex-column';
    const number = document.createElement('small'); number.className = 'text-secondary';
    number.dir = 'ltr'; number.textContent = course.courseNo;
    const heading = document.createElement('h2'); heading.className = 'h5 mt-2';
    heading.textContent = ZuLmsI18n.field(course, 'title');
    body.append(number, heading);
    label(body, text.start, course.plannedStartDate);
    label(body, text.end, course.plannedEndDate);
    if (course.registrationDeadline) label(body, text.deadline, ZuLmsDates.display(course.registrationDeadline));
    if (course.maxParticipants != null) label(body, text.capacity, String(course.maxParticipants));
    const actions = document.createElement('div'); actions.className = 'd-flex flex-wrap gap-2 mt-auto pt-3';
    const detail = document.createElement('a'); detail.href = `/courses/${course.id}?view=Available`;
    detail.className = 'btn btn-outline-primary'; detail.textContent = text.details;
    actions.append(detail);
    const button = document.createElement('button'); button.type = 'button';
    button.className = 'btn btn-primary';
    const closed = course.registrationDeadline && new Date(course.registrationDeadline) < new Date();
    button.textContent = course.isRegistered ? text.registered : closed ? text.closed : text.register;
    button.disabled = course.isRegistered || !!closed;
    button.addEventListener('click', async () => {
      button.disabled = true; error.classList.add('d-none');
      try { await ZuLms.api.post(`/api/v1/courses/${course.id}/self-registration`); await load(); }
      catch (e) { error.textContent = e.message || text.error; error.classList.remove('d-none'); button.disabled = false; }
    });
    actions.append(button); body.append(actions); article.append(body); column.append(article);
    return column;
  }
  async function load() {
    error.classList.add('d-none'); rows.setAttribute('aria-busy', 'true');
    document.getElementById('available-courses-prev').disabled = true;
    document.getElementById('available-courses-next').disabled = true;
    state(text.loading);
    try {
      const data = await ZuLms.api.get(`/api/v1/courses/available?page=${page}`);
      totalPages = Math.max(1, Math.ceil(data.total / data.pageSize));
      document.getElementById('available-courses-page-number').textContent = `${page} / ${totalPages}`;
      document.getElementById('available-courses-prev').disabled = page <= 1;
      document.getElementById('available-courses-next').disabled = page >= totalPages;
      rows.replaceChildren(...data.items.map(render));
      if (!data.items.length) state(text.empty);
    } catch (e) {
      error.textContent = e.message || text.error;
      error.classList.remove('d-none'); state(text.error, true);
    } finally { rows.setAttribute('aria-busy', 'false'); }
  }
  document.getElementById('available-courses-prev').addEventListener('click', () => { if (page > 1) { page--; load(); } });
  document.getElementById('available-courses-next').addEventListener('click', () => { if (page < totalPages) { page++; load(); } });
  load();
})();
