/* Manifest: the Decks tab - list, leader picker and editor. */

/* ---------- deck building ----------
   Legality (50 cards, 4-copy cap, leader colour match) and the guideline
   bands (curve/composition targets) are computed server-side in
   deck_detail() - this is display-only. See DeckRepository.cs for where the target
   numbers come from. */
let currentDeck = null;
let leaderPickMode = 'new';    // 'new' while starting a deck, 'change' while swapping leaders

function showDeckSub(id) {
  ['deckListView', 'deckLeaderPick', 'deckEditView'].forEach(v => $('#'+v).hidden = v !== id);
  // The workspace (preview panel + two card grids) wants real width on a
  // desktop screen - everywhere else in the app stays the narrow, phone-first
  // column, so only widen while the editor with the grids is actually open.
  document.querySelector('.wrap').classList.toggle('wide', id === 'deckEditView');
}

const deckListPager = makePager({
  host: $('#deckList'),
  url: cursor => '/api/decks?' + new URLSearchParams({cursor}).toString(),
  paint: (items, append) => {
    const host = $('#deckList');
    const html = items.map(d => {
      const leader = d.leader || {};
      return `<div class="deckcard" data-id="${d.id}">
        <img loading="lazy" alt="" src="/img/${encodeURIComponent(leader.card_id||'')}">
        <div class="meta">
          <h4>${esc(d.name)}</h4>
          <div class="sm">${esc(leader.name || 'No leader')}${leader.colors ? ' · ' + esc(leader.colors) : ''}</div>
        </div>
        <div class="count ${d.size_ok ? 'ok' : ''}"><b>${d.card_count}</b><span>of 50</span></div>
      </div>`;
    }).join('');
    if (append) host.insertAdjacentHTML('beforeend', html);
    else host.innerHTML = html;
    host.querySelectorAll('.deckcard').forEach(el =>
      el.onclick = () => openDeck(Number(el.dataset.id)));
  },
  empty: `<div class="empty">No decks yet. Tap "+ New deck" and pick a Leader to start.</div>`,
});

async function showDeckList() {
  showDeckSub('deckListView');
  currentDeck = null;
  await deckListPager.reset();
}

$('#btnNewDeck').onclick = () => { leaderPickMode = 'new'; openLeaderPicker(); };

/* ---- leader picker (used both for a new deck and "change leader") ---- */
function openLeaderPicker() {
  showDeckSub('deckLeaderPick');
  $('#qLeader').value = '';
  $('#fColorLeader').value = '';
  $('#fOwnedLeader').value = '';
  doLeaderSearch().catch(() => {});
  $('#qLeader').focus();
}
$('#btnLeaderPickBack').onclick = () => {
  if (leaderPickMode === 'change' && currentDeck) { showDeckSub('deckEditView'); }
  else { showDeckList(); }
};

let leaderTimer = null;
$('#qLeader').oninput = () => { clearTimeout(leaderTimer);
  leaderTimer = setTimeout(() => doLeaderSearch().catch(() => {}), 120); };
$('#fColorLeader').onchange = () => doLeaderSearch().catch(() => {});
$('#fOwnedLeader').onchange = () => doLeaderSearch().catch(() => {});

const leaderPager = makePager({
  host: $('#leaderResults'),
  url: cursor => {
    const params = new URLSearchParams({q: $('#qLeader').value.trim(), category: 'Leader', cursor});
    if ($('#fColorLeader').value) params.set('colors', $('#fColorLeader').value);
    if ($('#fOwnedLeader').value) params.set('owned', $('#fOwnedLeader').value);  // '1' owned, '0' not
    return '/api/search?' + params.toString();
  },
  paint: (items, append) => {
    const host = $('#leaderResults');
    const html = items.map(c => {
      const badge = c.qty > 0 ? `<span class="badge owned">owned ${c.qty}</span>`
                              : `<span class="badge not-owned">not owned</span>`;
      return `<div class="row leaderrow" data-id="${esc(c.card_id)}">
      <img loading="lazy" alt="" src="/img/${encodeURIComponent(c.card_id)}">
      <div class="meta">
        <div class="cid">${esc(c.card_id)}</div>
        <div class="nm">${esc(c.name)}${badge}</div>
        <div class="tags">${esc([c.colors, c.cost != null ? 'Life ' + c.cost : '',
          c.power ? 'Power ' + c.power : ''].filter(Boolean).join(' · '))}</div>
      </div>
    </div>`;
    }).join('');
    if (append) host.insertAdjacentHTML('beforeend', html);
    else host.innerHTML = html;
    host.querySelectorAll('.leaderrow').forEach(row =>
      row.onclick = () => pickLeader(row.dataset.id));
  },
  empty: `<div class="empty">No leaders match.</div>`,
});
const doLeaderSearch = () => leaderPager.reset();

async function pickLeader(cardId) {
  const c = leaderPager.items.find(x => x.card_id === cardId);
  if (leaderPickMode === 'new') {
    const r = await api('/api/decks', {method: 'POST',
      headers: {'content-type': 'application/json'},
      body: JSON.stringify({name: (c && c.name ? c.name : 'New deck') + ' Deck',
                            leader_card_id: cardId})});
    currentDeck = r.deck;
  } else {
    const r = await api(`/api/decks/${currentDeck.id}`, {method: 'POST',
      headers: {'content-type': 'application/json'},
      body: JSON.stringify({leader_card_id: cardId})});
    currentDeck = r.deck;
  }
  showDeckSub('deckEditView');
  renderDeckEditor();
}

/* ---- deck editor ---- */
async function openDeck(id) {
  const r = await api('/api/decks/' + id);
  currentDeck = r.deck;
  showDeckSub('deckEditView');
  renderDeckEditor();
}
$('#btnDeckBack').onclick = showDeckList;

function renderDeckEditor() {
  previewedCard = null;
  $('#previewPanel').innerHTML =
    `<div class="empty">Hover or tap a card to open its dossier here.</div>`;
  renderDeckHead();
  renderDeckLegal();
  renderDeckGuide();
  renderDeckCards();
  $('#deckColorNote').textContent = currentDeck.leader
    ? `locked to ${currentDeck.leader.colors}` : '';
  renderBuyList();
  doDeckSearch().catch(() => {});
}

function renderDeckHead() {
  const d = currentDeck, leader = d.leader;
  $('#deckHead').innerHTML = leader ? `
    <img alt="" src="/img/${encodeURIComponent(leader.card_id)}" id="deckLeaderImg">
    <div class="meta">
      <input class="deckName" id="deckNameInput" value="${esc(d.name)}" aria-label="Deck name">
      <div class="stat">${esc(leader.name)} · ${esc(leader.colors)} · Life ${leader.life ?? '—'}</div>
      <div class="stat ${d.legal.size_ok ? '' : 'bad'}"><b>${d.total}</b> / ${d.deck_size_target} cards</div>
      <div class="rowbtns"><button class="ghost" id="btnChangeLeader">Change leader</button></div>
    </div>` : '';
  if (!leader) return;
  $('#deckLeaderImg').onclick = async () => {
    const r = await api('/api/card/' + encodeURIComponent(leader.card_id));
    if (r.card) openCardModal(r.card);
  };
  const nameInput = $('#deckNameInput');
  nameInput.onchange = async () => {
    const r = await api(`/api/decks/${d.id}`, {method: 'POST',
      headers: {'content-type': 'application/json'},
      body: JSON.stringify({name: nameInput.value})});
    currentDeck = r.deck;
  };
  $('#btnChangeLeader').onclick = () => { leaderPickMode = 'change'; openLeaderPicker(); };
}

function renderDeckLegal() {
  const d = currentDeck, host = $('#deckLegal');
  const nameOfBase = baseId => {
    const c = d.cards.find(x => x.base_id === baseId);
    return c ? c.name : baseId;
  };
  const nameOfCard = cardId => {
    const c = d.cards.find(x => x.card_id === cardId);
    return c ? c.name : cardId;
  };
  if (d.legal.clean) {
    host.className = 'deckLegal ok';
    host.textContent = "Legal: exactly 50 cards, nothing over 4 copies, "
      + "everything matches your Leader's colours.";
    return;
  }
  host.className = 'deckLegal bad';
  const issues = [];
  if (!d.legal.size_ok) {
    issues.push(d.legal.short_by > 0
      ? `${d.legal.short_by} more card${d.legal.short_by === 1 ? '' : 's'} needed (${d.total}/${d.deck_size_target})`
      : `${d.legal.over_by} too many cards (${d.total}/${d.deck_size_target})`);
  }
  if (d.legal.over_limit.length)
    issues.push('Over the 4-copy limit: ' + d.legal.over_limit.map(nameOfBase).join(', '));
  if (d.legal.off_color.length)
    issues.push("Doesn't match your Leader's colours: " + d.legal.off_color.map(nameOfCard).join(', '));
  host.innerHTML = 'Not tournament-legal yet:<ul>'
    + issues.map(i => `<li>${esc(i)}</li>`).join('') + '</ul>';
}

function guidePill(label, g) {
  return `<div class="guidePill ${g.band}"><b>${g.count}</b>
    <span>${esc(label)} · target ${g.min}–${g.max}</span></div>`;
}

function renderDeckGuide() {
  const d = currentDeck, curve = d.curve;
  const costs = Array.from({length: 11}, (_, i) => i); // 0..10, 10 catching "10+"
  const max = Math.max(1, ...costs.map(c => curve[String(c)] || 0));
  const bars = costs.map(c => {
    const n = curve[String(c)] || 0;
    const h = Math.round(n / max * 100);
    return `<div class="col"><div class="n">${n || ''}</div><div class="bar" data-h="${h}"></div></div>`;
  }).join('');
  const labels = costs.map(c => `<div class="col"><div class="lbl">${c === 10 ? '10+' : c}</div></div>`).join('');
  $('#deckGuide').innerHTML = `
    <h3>Cost curve</h3>
    <div class="curve">${bars}</div>
    <div class="curveRow">${labels}</div>
    <div class="guideGrid">
      ${guidePill('Characters', d.guidelines.characters)}
      ${guidePill('Events', d.guidelines.events)}
      ${guidePill('Stages', d.guidelines.stages)}
      ${guidePill('Counter cards', d.guidelines.counters)}
      ${guidePill('Blockers', d.guidelines.blockers)}
      ${guidePill('Low cost 0-3', d.guidelines.cost_low)}
      ${guidePill('Mid cost 4-6', d.guidelines.cost_mid)}
      ${guidePill('High cost 7+', d.guidelines.cost_high)}
    </div>
    <div class="guideNote">Targets are community deckbuilding starting points, not an
      official rule — a deck outside these ranges is still perfectly legal.</div>`;
  // Set here rather than in a style="" attribute, which the page's CSP refuses.
  $('#deckGuide').querySelectorAll('.bar').forEach(b => b.style.height = b.dataset.h + '%');
}

/* ---- card tiles + the hover/tap preview panel ----
   Tapping a tile in the "add" grid adds one copy and previews it; tapping a
   tile in the "in this deck" grid removes one copy and previews it. Hovering
   (desktop only - touch has no hover) just previews, without changing
   anything, so you can look before you commit. The preview panel always
   re-fetches the full card record (effect text isn't in the cheap search
   payload) and is the single source of truth for the +/- buttons too, so
   there's only one place that needs to agree with the server about quantity. */
let previewedCard = null;
let previewHoverTimer = null;
let previewAbort = null;

function deckQtyOf(cardId) {
  const e = currentDeck.cards.find(x => x.card_id === cardId);
  return e ? e.qty : 0;
}

function tileHTML(c, ownedQty, deckQty, illegal) {
  const ownedCls = ownedQty > 0 ? '' : 'zero';
  return `<div class="ctile${illegal ? ' illegal' : ''}" data-id="${esc(c.card_id)}"
       title="${esc(c.name || c.card_id)}">
    <img loading="lazy" alt="" src="/img/${encodeURIComponent(c.card_id)}">
    <span class="pill owncount ${ownedCls}">${ownedQty}</span>
    ${deckQty ? `<span class="pill deckcount">${deckQty}</span>` : ''}
  </div>`;
}

function wireTiles(host, mode) {   // mode: 'add' (+1 per tap) or 'remove' (-1 per tap)
  host.querySelectorAll('.ctile').forEach(tile => {
    const id = tile.dataset.id;
    tile.onclick = async () => {
      const cur = deckQtyOf(id);
      const next = mode === 'add' ? cur + 1 : Math.max(0, cur - 1);
      await setDeckCardQty(id, next);
      loadPreview(id).catch(() => {});
    };
    tile.onmouseenter = () => hoverPreview(id);
  });
}

function hoverPreview(cardId) {
  clearTimeout(previewHoverTimer);
  previewHoverTimer = setTimeout(() => loadPreview(cardId).catch(() => {}), 90);
}

async function loadPreview(cardId) {
  if (previewAbort) previewAbort.abort();
  previewAbort = new AbortController();
  const mine = previewAbort;
  let data;
  try {
    data = await api('/api/card/' + encodeURIComponent(cardId), {signal: mine.signal});
  } catch (e) {
    if (e.name === 'AbortError') return;
    throw e;
  }
  if (mine !== previewAbort) return;
  if (data.card) renderPreview(data.card);
}

function renderPreview(c) {
  previewedCard = c;
  const deckQty = deckQtyOf(c.card_id);
  const host = $('#previewPanel');
  host.innerHTML = cardViewHTML(c, {
    ownRow: `<div class="cv-own"><span>Owned <b>${c.qty || 0}</b></span>
             <span>In deck <b>${deckQty}</b></span></div>`,
    actions: isLeaderCard(c) ? '' : `<div class="cv-act">
        <button class="stepper minus" id="previewMinus" aria-label="One fewer">&minus;</button>
        <span class="n">${deckQty}</span>
        <button class="stepper" id="previewPlus" aria-label="One more">+</button>
        <span class="lbl">in deck</span>
      </div>`,
  });
  wireCardView(host, c);
  const bump = async d => {
    await setDeckCardQty(c.card_id, Math.max(0, deckQtyOf(c.card_id) + d));
    loadPreview(c.card_id).catch(() => {});
  };
  const minus = $('#previewMinus'), plus = $('#previewPlus');
  if (minus) minus.onclick = () => bump(-1);
  if (plus) plus.onclick = () => bump(1);
  document.querySelectorAll('.ctile').forEach(t =>
    t.classList.toggle('selected', t.dataset.id === c.card_id));
}

function renderDeckCards() {
  const d = currentDeck;
  $('#deckCardsCount').textContent = `${d.total} / ${d.deck_size_target}`;
  const host = $('#deckCardsGrid');
  if (!d.cards.length) {
    host.innerHTML = `<div class="empty">No cards yet — add some from the list on the right.</div>`;
    return;
  }
  const overBase = new Set(d.legal.over_limit);
  const offColor = new Set(d.legal.off_color);
  host.innerHTML = d.cards.map(c =>
    tileHTML(c, c.owned_qty, c.qty, overBase.has(c.base_id) || offColor.has(c.card_id))).join('');
  wireTiles(host, 'remove');
}

async function setDeckCardQty(cardId, qty) {
  const r = await api(`/api/decks/${currentDeck.id}/card`, {method: 'POST',
    headers: {'content-type': 'application/json'}, body: JSON.stringify({card_id: cardId, qty})});
  currentDeck = r.deck;
  renderDeckHead(); renderDeckLegal(); renderDeckGuide(); renderDeckCards(); renderBuyList();
  deckBrowsePager.repaint();
}

$('#btnDeckImport').onclick = async () => {
  if (!currentDeck) return;
  const text = $('#deckImportText').value.trim();
  const status = $('#deckImportStatus');
  if (!text) return;
  status.className = 'status';
  status.textContent = 'Importing...';
  try {
    const r = await api(`/api/decks/${currentDeck.id}/cards`, {method: 'POST',
      headers: {'content-type': 'application/json'}, body: JSON.stringify({text})});
    currentDeck = r.deck;
    $('#deckImportText').value = '';
    renderDeckEditor();
    status.className = 'status';
    status.textContent = `Imported ${currentDeck.total} cards.`;
  } catch (e) {
    status.className = 'status err';
    status.textContent = e.message || 'Could not import that list.';
  }
};
$('#btnDeckImportClear').onclick = () => {
  $('#deckImportText').value = '';
  $('#deckImportStatus').textContent = '';
};

/* ---- add-cards browser, locked to the leader's colour(s) ---- */
let deckTimer = null;
$('#qDeck').oninput = () => { clearTimeout(deckTimer);
  deckTimer = setTimeout(() => doDeckSearch().catch(() => {}), 120); };
['fCategoryDeck', 'fSetDeck', 'fRarityDeck', 'fOwnedDeck'].forEach(id =>
  $('#' + id).onchange = () => doDeckSearch().catch(() => {}));

// Locked to the Leader's colours - any of them, for a dual-colour Leader - and
// never offering Leaders, all decided by the server so every page is already
// the right cards rather than a page with most of them filtered away here.
const deckBrowsePager = makePager({
  host: $('#deckBrowseGrid'),
  url: cursor => {
    const params = new URLSearchParams({
      q: $('#qDeck').value.trim(), sort: $('#sortDeckBrowse').value,
      colors: currentDeck.leader.colors || '', exclude_category: 'Leader', cursor});
    if ($('#fCategoryDeck').value) params.set('category', $('#fCategoryDeck').value);
    if ($('#fSetDeck').value) params.set('set', $('#fSetDeck').value);
    if ($('#fRarityDeck').value) params.set('rarity', $('#fRarityDeck').value);
    if ($('#fOwnedDeck').value) params.set('owned', $('#fOwnedDeck').value);
    return '/api/search?' + params.toString();
  },
  paint: (items, append) => {
    const host = $('#deckBrowseGrid');
    const html = items.map(c => tileHTML(c, c.qty, deckQtyOf(c.card_id), false)).join('');
    if (append) host.insertAdjacentHTML('beforeend', html);
    else host.innerHTML = html;
    wireTiles(host, 'add');
  },
  empty: `<div class="empty">Nothing matches, in your Leader's colours.</div>`,
});

async function doDeckSearch() {
  if (!currentDeck || !currentDeck.leader) { deckBrowsePager.clear(); return; }
  await deckBrowsePager.reset();
}
$('#sortDeckBrowse').onchange = () => doDeckSearch().catch(() => {});

function renderBuyList() {
  const d = currentDeck;
  const cardTotal = d.buy_list.reduce((s, b) => s + b.need, 0);
  $('#buyTotal').textContent = d.buy_list.length
    ? `${cardTotal} card${cardTotal === 1 ? '' : 's'} · ${fmtGBP(d.buy_total_gbp) || 'price unknown'}`
    : '';
  const host = $('#buyList');
  if (!d.buy_list.length) {
    host.innerHTML = `<div class="empty">${d.cards.length
      ? 'You own enough of everything in this deck.'
      : 'Add cards above to see what you still need to buy.'}</div>`;
    return;
  }
  host.innerHTML = d.buy_list.map(b => `<div class="buyrow">
    <img loading="lazy" alt="" src="/img/${encodeURIComponent(b.card_id)}">
    <div class="meta">
      <div class="nm">${esc(b.name || b.card_id)}</div>
      <div class="sm">${esc([b.card_id, b.set_label, b.rarity].filter(Boolean).join(' · '))}</div>
    </div>
    <div class="price">${b.subtotal_gbp != null ? fmtGBP(b.subtotal_gbp) : '—'}
      <span class="need">need ${b.need}</span></div>
  </div>`).join('');
}

$('#btnDeckDelete').onclick = async () => {
  if (!currentDeck) return;
  if (!confirm(`Delete "${currentDeck.name}"? This can't be undone.`)) return;
  await api(`/api/decks/${currentDeck.id}/delete`, {method: 'POST',
    headers: {'content-type': 'application/json'}, body: '{}'});
  currentDeck = null;
  showDeckList();
};
