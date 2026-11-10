(() => {
  const search = document.getElementById('search');
  const group = document.getElementById('group');
  const cards = [...document.querySelectorAll('.call')];
  const count = document.getElementById('visible-count');
  const empty = document.getElementById('empty');
  function update() {
    const query = search.value.trim().toLocaleLowerCase();
    let visible = 0;
    for (const card of cards) {
      const matches = (!query || card.dataset.search.includes(query)) && (!group.value || card.dataset.group === group.value);
      card.hidden = !matches;
      if (matches) visible++;
    }
    count.textContent = `${visible} / ${cards.length}`;
    empty.hidden = visible !== 0;
  }
  search.addEventListener('input', update);
  group.addEventListener('change', update);
  update();
})();
