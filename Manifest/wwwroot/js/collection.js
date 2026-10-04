/* Manifest: the Log cards and My cards tabs, and the running totals. */

/* ---------- log tab ---------- */
let timer = null;
$('#q').oninput = () => { clearTimeout(timer); timer = setTimeout(() => doSearch().catch(()=>{}), 120); };
$('#q').onkeydown = e => { if (e.key === 'Enter') quickAdd(); };
$('#qGo').onclick = quickAdd;

// The pager aborts a search still in flight when a newer one starts, and drops
// its answer if it lands anyway - otherwise a slow early response could arrive
// after a later one and paint stale results over what was typed since.
const logPager = makePager({
  host: $('#results'),
  url: cursor => {
    const fl = readFilters('');
    const params = new URLSearchParams({q: $('#q').value.trim(), sort: $('#sortResults').value,
                                       cursor});
    if (fl.category) params.set('category', fl.category);
    if (fl.color) params.set('colors', fl.color);
    if (fl.set) params.set('set', fl.set);
    if (fl.rarity) params.set('rarity', fl.rarity);
    return '/api/search?' + params.toString();
  },
  paint: (items, append) => {
    paintCards($('#results'), items, 'log', {}, append);
    if (!append) lastHit = null;
  },
  empty: `<div class="empty">Nothing matches that. Newer sets may not be in
    the catalogue yet &mdash; you can still log the number and it will count.</div>`,
});

async function doSearch() {
  const fl = readFilters('');
  if (!$('#q').value.trim() && !(fl.category || fl.color || fl.set || fl.rarity)) {
    logPager.clear();
    return;
  }
  await logPager.reset();
}
['fCategory', 'fColor', 'fSet', 'fRarity', 'sortResults'].forEach(id =>
  $('#' + id).onchange = () => doSearch().catch(() => {}));

async function quickAdd() {
  const v = $('#q').value.trim();
  if (!v) return;
  const {results} = await api('/api/search?q=' + encodeURIComponent(v) + '&limit=2');
  // A bare card number that resolves to exactly one printing logs straight away.
  if (results.length && /^[A-Za-z]{2,3}\d{2}-\d{3}/.test(v.replace(/\s/g,''))) {
    const r = await api('/api/collection', {method:'POST',
      headers:{'content-type':'application/json'},
      body: JSON.stringify({card_id: results[0].card_id, delta: 1})});
    lastHit = r.card_id;
    $('#q').value = '';
    logPager.clear();
    flash(`${r.card_id} — ${r.name || 'logged'} ×${r.qty}`);
    refreshTotals();
    return;
  }
  doSearch();
}

function flash(msg) {
  const host = $('#results');
  host.innerHTML = `<div class="empty good">${esc(msg)}</div>`;
  setTimeout(() => { if (host.firstChild && !$('#q').value) host.innerHTML = ''; }, 2200);
}

$('#btnBulkLog').onclick = async () => {
  const text = $('#bulkLogText').value.trim();
  const status = $('#bulkLogStatus');
  if (!text) return;
  status.className = 'status';
  status.textContent = 'Logging...';
  try {
    const r = await api('/api/collection/bulk', {method: 'POST',
      headers: {'content-type': 'application/json'}, body: JSON.stringify({text})});
    $('#bulkLogText').value = '';
    status.textContent = `Logged ${r.added} cards across ${r.unique} numbers.`;
    refreshTotals();
    if (logPager.items.length) doSearch().catch(() => {});
    if (ownPager.items.length) loadOwned().catch(() => {});
  } catch (e) {
    status.className = 'status err';
    status.textContent = e.message || 'Could not log that list.';
  }
};
$('#btnBulkLogClear').onclick = () => {
  $('#bulkLogText').value = '';
  $('#bulkLogStatus').textContent = '';
};

/* ---------- owned tab ---------- */
let ownedUnique = 0;   // from the stats, so "nothing matches" and "nothing yet" differ
const ownPager = makePager({
  host: $('#owned'),
  url: cursor => {
    const fl = readFilters('Own');
    const params = new URLSearchParams({q: $('#filter').value.trim(), sort: $('#sortOwned').value,
                                       cursor});
    if (fl.category) params.set('category', fl.category);
    if (fl.color) params.set('colors', fl.color);
    if (fl.set) params.set('set', fl.set);
    if (fl.rarity) params.set('rarity', fl.rarity);
    return '/api/collection?' + params.toString();
  },
  // A tile updates in place when stepped; the set breakdown is refreshed in the
  // background so it stays honest without the list jumping.
  paint: (items, append) => paintCards($('#owned'), items, 'own', {
    onRow: () => refreshSetBreak().catch(() => {}),
    onStep: () => refreshSetBreak().catch(() => {}),
  }, append),
  empty: () => `<div class="empty">${ownedUnique
    ? 'Nothing matches that filter.'
    : 'Nothing logged yet. Head to Log cards and type a number.'}</div>`,
});

async function loadOwned() {
  await refreshSetBreak();
  await ownPager.reset();
}
function paintSetBreak(sets) {
  $('#setBreak').innerHTML = sets.map(s =>
    `${esc(s.s || '—')} <span>${s.n}</span> cards · ${s.uniq}/${s.total} of the set`
  ).join('<br>');
}
async function refreshSetBreak() {
  const s = await api('/api/collection/stats');
  ownedUnique = s.unique;
  paintSetBreak(s.sets);
}
let ownTimer = null;
$('#filter').oninput = () => { clearTimeout(ownTimer);
  ownTimer = setTimeout(() => ownPager.reset().catch(() => {}), 150); };
['sortOwned', 'fCategoryOwn', 'fColorOwn', 'fSetOwn', 'fRarityOwn'].forEach(id =>
  $('#' + id).onchange = () => ownPager.reset().catch(() => {}));

$('#btnReset').onclick = async () => {
  if (!confirm('Delete every logged card? This cannot be undone.')) return;
  await api('/api/reset', {method:'POST',
    headers:{'content-type':'application/json'}, body: '{}'});
  loadOwned(); refreshTotals();
};

/* ---------- totals ---------- */
async function refreshTotals() {
  const s = await api('/api/stats');
  $('#tTotal').textContent = s.total;
  $('#tUniq').textContent = s.unique + ' unique';
  $('#tValue').textContent = s.value_gbp ? fmtGBP(s.value_gbp) + ' market' : '';
}
