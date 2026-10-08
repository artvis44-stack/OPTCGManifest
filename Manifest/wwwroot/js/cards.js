/* Manifest: card rendering shared by every tab - filters, the grid and list
   layouts, the pager, the card viewer, the inspector and the modal. */

/* ---------- filter dropdowns ----------
   Options come from /api/facets (distinct values straight from the catalogue)
   so a new set or rarity shows up automatically after a catalogue refresh,
   rather than needing this file edited too. Category/colour/rarity get a
   fixed, game-meaningful order up front; anything unrecognised is appended
   alphabetically so nothing new silently goes missing. */
const CATEGORY_ORDER = ['Leader', 'Character', 'Event', 'Stage'];
const COLOR_ORDER = ['Red', 'Green', 'Blue', 'Purple', 'Black', 'Yellow'];
const RARITY_ORDER = ['Common', 'Uncommon', 'Rare', 'SuperRare', 'SecretRare',
  'TreasureRare', 'Special', 'Leader', 'Promo'];
// Older catalogue snapshots stored these as shouty abbreviations (LEADER, SR,
// SEC, UC...) rather than the current scraper's full words — label lookups
// are keyed lowercase and matched case-insensitively so either vintage of
// manifest.db renders the same tidy dropdowns.
const RARITY_LABEL = {
  common: 'Common', c: 'Common', uncommon: 'Uncommon', uc: 'Uncommon',
  rare: 'Rare', r: 'Rare', superrare: 'Super Rare', sr: 'Super Rare',
  secretrare: 'Secret Rare', sec: 'Secret Rare', treasurerare: 'Treasure Rare',
  tr: 'Treasure Rare', special: 'Special', 'sp card': 'Special',
  leader: 'Leader', l: 'Leader', promo: 'Promo', p: 'Promo',
};
const titleCase = s => s.replace(/\w\S*/g,
  w => w[0].toUpperCase() + w.slice(1).toLowerCase());
function orderBy(values, order) {
  const rank = v => {
    const i = order.findIndex(o => o.toLowerCase() === v.toLowerCase());
    return i === -1 ? order.length : i;
  };
  return [...values].sort((a, b) => rank(a) - rank(b) || a.localeCompare(b));
}
function fillSelect(sel, values, labelFor) {
  values.forEach(v => {
    const o = document.createElement('option');
    o.value = v;
    o.textContent = labelFor ? (labelFor[v.toLowerCase()] || v) : titleCase(v);
    sel.appendChild(o);
  });
}
async function loadFacets() {
  const f = await api('/api/facets');
  const cats = orderBy(f.categories, CATEGORY_ORDER);
  const colors = orderBy(f.colors, COLOR_ORDER);
  const rarities = orderBy(f.rarities, RARITY_ORDER);
  ['fCategory', 'fCategoryOwn'].forEach(id => fillSelect($('#'+id), cats));
  ['fColor', 'fColorOwn', 'fColorLeader'].forEach(id => fillSelect($('#'+id), colors));
  ['fSet', 'fSetOwn', 'fSetDeck'].forEach(id => fillSelect($('#'+id), f.sets));
  ['fRarity', 'fRarityOwn', 'fRarityDeck'].forEach(id => fillSelect($('#'+id), rarities, RARITY_LABEL));
}
loadFacets().catch(() => {});

// suffix '' reads the Log tab's selects (fCategory, fColor, ...); 'Own' reads
// the My cards tab's (fCategoryOwn, fColorOwn, ...) — same four filters, two
// independent sets of controls since both panes exist in the DOM at once.
function readFilters(suffix) {
  return {
    category: $('#fCategory' + suffix).value,
    color: $('#fColor' + suffix).value,
    set: $('#fSet' + suffix).value,
    rarity: $('#fRarity' + suffix).value,
  };
}

/* ---------- tabs ---------- */
const panes = {tabLog:'paneLog', tabOwn:'paneOwn', tabDeck:'paneDeck', tabScan:'paneScan'};
Object.keys(panes).forEach(t => $('#'+t).onclick = () => {
  Object.entries(panes).forEach(([k,v]) => {
    $('#'+k).setAttribute('aria-selected', k===t);
    $('#'+v).hidden = k!==t;
  });
  document.body.dataset.tab = t;
  clearInspector();
  if (t==='tabOwn') loadOwned();
  if (t==='tabDeck') showDeckList();
  if (t==='tabLog') $('#q').focus();
  if (t!=='tabScan') stopCam();
});

/* ---------- shared row renderer ---------- */
const esc = s => String(s ?? '').replace(/[&<>"']/g, m =>
  ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[m]));

/* ---------- paged lists ----------
   Every long list arrives a page at a time, sorted and filtered by the server, and
   the next page is fetched as the end of the list scrolls into view. A pager owns
   one host element and a status line just after it (loading / failed + retry).
   Changing the query calls reset(); anything still in flight for the old query is
   aborted and, if it lands anyway, ignored. */
function makePager({host, url, paint, empty}) {
  const p = {items: []};
  let cursor = '', done = true, busy = false, gen = 0, ctrl = null;
  const foot = document.createElement('div');
  foot.className = 'pager';
  host.insertAdjacentElement('afterend', foot);
  const near = () => foot.offsetParent !== null &&
    foot.getBoundingClientRect().top < innerHeight + 800;
  new IntersectionObserver(es => { if (es.some(e => e.isIntersecting)) next(); },
                           {rootMargin: '800px 0px'}).observe(foot);

  function status(kind) {
    foot.dataset.state = kind;
    if (kind === 'loading') foot.textContent = 'Loading…';
    else if (kind === 'error') {
      foot.innerHTML = 'Could not load this list. <button type="button" class="ghost">Retry</button>';
      foot.querySelector('button').onclick = () => next();
    } else foot.textContent = '';
  }

  async function next() {
    if (busy || done) return;
    busy = true;
    const mine = gen, first = cursor === '';
    ctrl = new AbortController();
    status('loading');
    try {
      const d = await api(url(cursor), {signal: ctrl.signal});
      if (mine !== gen) return;
      p.items.push(...d.items);
      cursor = d.next_cursor || '';
      done = !d.next_cursor;
      if (first && !d.items.length) {
        host.className = '';
        host.innerHTML = typeof empty === 'function' ? empty() : empty;
      } else {
        paint(d.items, !first);
      }
      status('');
    } catch (e) {
      if (mine !== gen || e.name === 'AbortError') return;
      if (first) { host.className = ''; host.innerHTML = ''; }
      status('error');
      return;
    } finally {
      if (mine === gen) busy = false;
    }
    // A short page can leave the end still on screen, which the observer will
    // not report again - so keep going until it is pushed out of view.
    if (!done && near()) next();
  }

  p.reset = () => {
    gen++; if (ctrl) ctrl.abort();
    busy = false; done = false; cursor = ''; p.items = [];
    return next();
  };
  p.clear = () => {
    gen++; if (ctrl) ctrl.abort();
    busy = false; done = true; cursor = ''; p.items = [];
    host.className = ''; host.innerHTML = ''; status('');
  };
  // Repaint everything loaded so far, e.g. after switching grid/list.
  p.repaint = () => { if (p.items.length) paint(p.items, false); };
  return p;
}

// Prices come from an unofficial, free API (optcgapi.com) and are refreshed
// by running `manifest refresh-prices` on a schedule, not live per-request — a card
// with no price yet just shows nothing rather than a placeholder.
const fmtGBP = v => (v || v === 0) ? '£' + v.toFixed(2) : '';

// Each source's own price, in its own currency and in pounds; the card is shown at
// the first of them (Cardmarket, then TCGplayer, then optcgapi.com).
const PRICE_SOURCE = {cardmarket: 'Cardmarket', tcgplayer: 'TCGplayer', optcgapi: 'optcgapi',
                      manual: 'Typed in'};
const CURRENCY_SIGN = {EUR: '€', USD: '$', GBP: '£'};
function pricesHTML(prices) {
  return (prices || []).map(p => {
    const own = (CURRENCY_SIGN[p.currency] || p.currency + ' ') + p.amount.toFixed(2);
    const name = esc(PRICE_SOURCE[p.source] || p.source);
    const label = p.url
      ? `<a href="${esc(p.url)}" target="_blank" rel="noopener noreferrer">${name}</a>` : name;
    return `<div class="cv-price"><span>${label}</span><b>${esc(own)}</b>`
      + `${p.currency === 'GBP' ? '' : `<i>${esc(fmtGBP(p.gbp))}</i>`}</div>`;
  }).join('');
}

// Who put a card in a shared binder; nothing anywhere else.
const addedBy = c => c.added_by && sharedView() ? 'by ' + c.added_by : '';

function rowHTML(c, qty) {
  const tags = [c.set_label, c.rarity, c.variant, c.colors, addedBy(c)].filter(Boolean).join(' · ');
  const price = fmtGBP(c.price_gbp);
  return `<div class="row ${c.card_id===lastHit?'hit':''}" data-id="${esc(c.card_id)}">
    <img loading="lazy" alt="" src="/img/${encodeURIComponent(c.card_id)}">
    <div class="meta">
      <div class="cid">${esc(c.card_id)}</div>
      <div class="nm">${esc(c.name || 'Not in catalogue')}</div>
      <div class="tags">${esc(tags)}${price ? ` · <span class="price">${price}</span>` : ''}</div>
    </div>
    <div class="own ${qty?'':'zero'}">${qty||0}</div>
    <div class="btns">
      <button class="minus" data-d="-1" aria-label="One fewer">−</button>
      <button data-d="1" aria-label="One more">+</button>
    </div>
  </div>`;
}

function wire(host, after) {
  host.querySelectorAll('.row .btns button').forEach(b => b.onclick = async () => {
    const id = b.closest('.row').dataset.id;
    const r = await api('/api/collection', {method:'POST',
      headers:{'content-type':'application/json'},
      body: JSON.stringify({card_id:id, delta:+b.dataset.d})});
    lastHit = id;
    refreshTotals();
    syncCardQty(id, r.qty);
    if (after) after(id, r.qty);
  });
  host.querySelectorAll('.row').forEach(row => {
    row.onclick = e => {
      if (e.target.closest('.btns')) return;
      const c = cardIndex[row.dataset.id];
      if (c) openCard(c);
    };
  });
}

/* ---------- grid renderer ----------
   The default way to browse: the art is the row. Owned count top-left, DON cost
   and market price along the bottom, and a stepper bar that slides up on hover
   so logging a copy never needs the card opened first. */
function gridHTML(c, qty) {
  const price = fmtGBP(c.price_gbp);
  const rar = rarityShort(c.rarity);
  return `<div class="gcard ${qty ? '' : 'zero'} ${c.card_id === lastHit ? 'hit' : ''}"
       data-id="${esc(c.card_id)}" tabindex="0" role="button"
       title="${esc(c.card_id + ' — ' + (c.name || ''))}">
    <div class="art">
      <img loading="lazy" alt="${esc(c.name || c.card_id)}"
           src="/img/${encodeURIComponent(c.card_id)}">
      <span class="qty" data-own>${qty || 0}</span>
      ${price ? `<span class="money">${price}</span>` : ''}
      <div class="steps">
        <button class="minus" data-d="-1" aria-label="One fewer">&minus;</button>
        <button data-d="1" aria-label="One more">+</button>
      </div>
    </div>
    <div class="nm">${esc(c.name || 'Not in catalogue')}</div>
    <div class="no">${esc(c.card_id)}${rar ? ' · ' + esc(rar) : ''}${
      c.variant ? ' · ' + esc(c.variant) : ''}${addedBy(c) ? ' · ' + esc(addedBy(c)) : ''}</div>
  </div>`;
}

// Stepping a tile updates that tile in place rather than repainting the grid -
// a full re-render under the cursor makes the card you are clicking jump away.
function wireGrid(host, onChange) {
  host.querySelectorAll('.gcard').forEach(el => {
    const id = el.dataset.id;
    el.onclick = e => {
      if (e.target.closest('.steps')) return;
      const c = cardIndex[id];
      if (c) openCard(c);
    };
    el.onkeydown = e => {
      if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); el.click(); }
    };
    el.querySelectorAll('.steps button').forEach(b => b.onclick = async () => {
      const r = await api('/api/collection', {method:'POST',
        headers:{'content-type':'application/json'},
        body: JSON.stringify({card_id: id, delta: +b.dataset.d})});
      lastHit = id;
      if (cardIndex[id]) cardIndex[id].qty = r.qty;
      refreshTotals();
      syncCardQty(id, r.qty);
      if (onChange) onChange(id, r.qty);
    });
  });
}

/* ---------- view toggle (grid | list), remembered per tab ---------- */
const VIEW = {log: 'grid', own: 'grid'};
try { Object.assign(VIEW, JSON.parse(localStorage.getItem('manifestView') || '{}')); } catch (e) {}

function paintToggle(which) {
  const box = $(which === 'log' ? '#viewLog' : '#viewOwn');
  if (box) box.querySelectorAll('button').forEach(b =>
    b.classList.toggle('on', b.dataset.view === VIEW[which]));
}
['log', 'own'].forEach(which => {
  const box = $(which === 'log' ? '#viewLog' : '#viewOwn');
  if (!box) return;
  box.querySelectorAll('button').forEach(b => b.onclick = () => {
    VIEW[which] = b.dataset.view;
    try { localStorage.setItem('manifestView', JSON.stringify(VIEW)); } catch (e) {}
    paintToggle(which);
    if (which === 'log') logPager.repaint();
    else ownPager.repaint();
  });
  paintToggle(which);
});

// Renders a card list into `host` in whichever layout that tab is set to -
// replacing what is there, or after it when a pager brings the next page.
function paintCards(host, list, which, after, append) {
  host.className = VIEW[which] === 'grid' ? 'cardgrid' : '';
  const html = list.map(c =>
    VIEW[which] === 'grid' ? gridHTML(c, c.qty) : rowHTML(c, c.qty)).join('');
  if (append) host.insertAdjacentHTML('beforeend', html);
  else host.innerHTML = html;
  indexCards(list);
  if (VIEW[which] === 'grid') wireGrid(host, after.onStep);
  else wire(host, after.onRow);
  if (inspected) markSelected(inspected.card_id);
}

/* ---------- the card viewer ----------
   One component, three mounts: the desktop inspector beside the list, the
   phone-sized modal, and the deck editor's preview column. Art on one side,
   dossier on the other - the Duel Network reading layout. */
const COLOR_CLASS = {red:'c-red', green:'c-green', blue:'c-blue',
  purple:'c-purple', black:'c-black', yellow:'c-yellow'};
const HOLO_RARITY = /secret|super|treasure|special|promo|sr|sec|tr|sp/i;
const RARITY_SHORT = {common:'C', c:'C', uncommon:'UC', uc:'UC', rare:'R', r:'R',
  superrare:'SR', sr:'SR', secretrare:'SEC', sec:'SEC', treasurerare:'TR', tr:'TR',
  special:'SP', 'sp card':'SP', leader:'L', l:'L', promo:'P', p:'P'};
const rarityLabel = v => v ? (RARITY_LABEL[String(v).toLowerCase()] || v) : '';
const rarityShort = v => v ? (RARITY_SHORT[String(v).toLowerCase()] || String(v).slice(0,3).toUpperCase()) : '';
const isLeaderCard = c => (c.category || '').toUpperCase() === 'LEADER';
const hasVal = v => v !== null && v !== undefined && v !== '';
const splitColors = v => String(v || '').split(/[\/,;+]|\s{1,}/).map(x => x.trim()).filter(Boolean);

// The collection stepper, shared by the inspector and the modal. Quantities are
// tagged data-qty rather than given ids so several mounts can show the same card
// at once without colliding.
const collectionActs = c => `<div class="cv-act">
    <button class="stepper minus" data-step="-1" aria-label="One fewer">&minus;</button>
    <span class="n" data-qty>${c.qty || 0}</span>
    <button class="stepper" data-step="1" aria-label="One more">+</button>
    <span class="lbl">owned</span>
  </div>${moveActs(c)}<div data-print-acts></div>${customActs(c)}`;

function cardViewHTML(c, o) {
  o = o || {};
  const leader = isLeaderCard(c);
  const pips = splitColors(c.colors).map(x =>
    `<span class="pip ${COLOR_CLASS[x.toLowerCase()] || ''}"><i></i>${esc(x)}</span>`).join('');
  const st = [];
  // A Leader isn't played from hand, so it has no DON cost - the site's own
  // markup puts the Leader's Life value in that same slot, and the scraper
  // (and this app's catalog.cost column) carries it through as-is.
  if (hasVal(c.cost)) st.push([leader ? 'life' : 'cost', c.cost, leader ? 'life' : 'don']);
  if (c.power) st.push(['power', c.power, '']);
  if (c.counter) st.push(['counter', '+' + c.counter, '']);
  const price = fmtGBP(c.price_gbp);
  if (price) st.push(['market', price, 'money']);
  const typeline = typeLine(c);
  const meta = [c.set_label, rarityLabel(c.rarity), c.variant].filter(Boolean);
  const holo = HOLO_RARITY.test(c.rarity || '') ? ' holo' : '';
  return `<div class="cv${o.two ? ' two' : ''}">
    <div class="cv-art">
      <div class="cv-frame${holo}">
        <img alt="${esc(c.name || c.card_id)}" src="/img/${encodeURIComponent(c.card_id)}">
      </div>
      <button type="button" class="cv-expand" data-zoom>View full art</button>
    </div>
    <div class="cv-body">
      <div class="cv-plate">
        <div class="cid">${esc(c.card_id)}</div>
        <h3>${esc(c.name || 'Not in catalogue')}</h3>
      </div>
      ${pips ? `<div class="cv-pips">${pips}</div>` : ''}
      ${st.length ? `<div class="cv-stats">${st.map(([k, v, cls]) =>
        `<div class="plaque ${cls}"><b>${esc(v)}</b><span>${k}</span></div>`).join('')}</div>` : ''}
      <div class="cv-prices" data-prices${c.prices && c.prices.length ? '' : ' hidden'}>${pricesHTML(c.prices)}</div>
      <div class="cv-type" data-typeline${typeline ? '' : ' hidden'}>${esc(typeline)}</div>
      <div class="effect" data-effect${c.effect ? '' : ' hidden'}>${esc(c.effect || '')}</div>
      <div class="effect trigger" data-trigger${c.trigger ? '' : ' hidden'}>${esc(c.trigger || '')}</div>
      ${meta.length ? `<div class="cv-meta">${meta.map(t => `<span>${esc(t)}</span>`).join('')}</div>` : ''}
      ${o.ownRow || ''}
      ${o.actions || ''}
    </div>
  </div>`;
}

/* ---------- print picker ----------
   Alt arts and reprints carry the same card number as the base card - it is the
   art that differs - so the deck builder and the scan result both show one card
   and let you choose which printing you mean. Which printings a number has never
   changes while the page is open, so each family is fetched once. */
const printFamilies = new Map();
function printsOf(cardId) {
  const base = String(cardId || '').split('_')[0];
  if (!printFamilies.has(base))
    printFamilies.set(base, api('/api/prints/' + encodeURIComponent(base))
      .then(d => d.prints || [])
      .catch(e => { printFamilies.delete(base); throw e; }));
  return printFamilies.get(base);
}

const printLabel = p => p.variant || 'Base';

// badge(p) adds a corner marker per printing, e.g. how many of it are in the deck.
function printStripHTML(prints, selectedId, badge) {
  if (!prints || prints.length < 2) return '';
  return `<div class="prints">
    <div class="prints-h">Prints <span>${prints.length}</span></div>
    <div class="prints-row">${prints.map(p => {
      const sub = [p.set_label, rarityShort(p.rarity)].filter(Boolean).join(' · ');
      const price = fmtGBP(p.price_gbp);
      return `<button type="button" class="print${p.card_id === selectedId ? ' on' : ''}"
          data-print="${esc(p.card_id)}" title="${esc(p.card_id + ' · ' + printLabel(p))}"
          aria-pressed="${p.card_id === selectedId}">
        <img loading="lazy" alt="" src="/img/${encodeURIComponent(p.card_id)}">
        <span class="print-lbl">${esc(printLabel(p))}</span>
        ${sub ? `<span class="print-sub">${esc(sub)}</span>` : ''}
        ${price ? `<span class="print-price">${esc(price)}</span>` : ''}
        ${badge ? badge(p) : ''}
      </button>`;
    }).join('')}</div>
  </div>`;
}

function wirePrintStrip(host, onPick) {
  const row = host.querySelector('.prints-row');
  const on = host.querySelector('.print.on');
  // Keep the chosen printing in view when the strip is wider than the screen.
  if (row && on) row.scrollLeft = on.offsetLeft - row.offsetLeft - 8;
  host.querySelectorAll('[data-print]').forEach(b => b.onclick = e => {
    e.stopPropagation();
    host.querySelectorAll('.print').forEach(x => {
      x.classList.toggle('on', x === b);
      x.setAttribute('aria-pressed', String(x === b));
    });
    onPick(b.dataset.print);
  });
}

// Pointer tilt + specular sweep. The CSS custom properties live on .cv and are
// read by .cv-frame, so the whole viewer stays in one transform context.
function wireCardView(host, c) {
  const cv = host.querySelector('.cv');
  const frame = host.querySelector('.cv-frame');
  if (!cv || !frame) return;
  const img = frame.querySelector('img');
  const zoom = e => { if (e) e.stopPropagation(); openImageZoom(img && img.src, c.name || c.card_id); };
  frame.onclick = zoom;
  const btn = host.querySelector('[data-zoom]');
  if (btn) btn.onclick = zoom;
  frame.onpointermove = e => {
    const r = frame.getBoundingClientRect();
    const px = (e.clientX - r.left) / r.width, py = (e.clientY - r.top) / r.height;
    cv.style.setProperty('--tilt-y', ((px - .5) * 12).toFixed(2) + 'deg');
    cv.style.setProperty('--tilt-x', ((.5 - py) * 14).toFixed(2) + 'deg');
    cv.style.setProperty('--sx', (px * 100).toFixed(1) + '%');
    cv.style.setProperty('--sy', (py * 100).toFixed(1) + '%');
  };
  frame.onpointerleave = () => {
    cv.style.setProperty('--tilt-x', '0deg');
    cv.style.setProperty('--tilt-y', '0deg');
  };
}

function wireCollectionActs(host, c) {
  host.querySelectorAll('.cv-act [data-step]').forEach(b => b.onclick = async () => {
    const r = await api('/api/collection', {method: 'POST',
      headers: {'content-type': 'application/json'},
      body: JSON.stringify({card_id: c.card_id, delta: +b.dataset.step})});
    c.qty = r.qty;
    lastHit = c.card_id;
    refreshTotals();
    syncCardQty(c.card_id, r.qty);
  });
  wireMoveActs(host, c);
  wirePrintActs(host, c);
  wireCustomActs(host, c);
}

/* ---------- prints added by hand ----------
   For a print no card source lists - a promo that came with a book - anyone can
   add it as a further printing of the card they are looking at: its own name,
   set, rarity, photo and price, and the card's text. One added by hand can be
   taken out again by whoever added it, or the owner. */
const isCustomPrint = id => /_c\d+$/.test(id || '');

const customActs = c => `<div class="cv-custom">
    <button type="button" class="ghost" data-custom-open>Add a print that isn't listed</button>
    ${isCustomPrint(c.card_id) ? `<button type="button" class="ghost danger" data-custom-remove>Remove this print</button>` : ''}
    <form class="custom-form" data-custom-form hidden>
      <div class="custom-h">New print of <b>${esc(c.base_id || c.card_id)}</b> ${esc(c.name || '')}</div>
      <label>Print name<input name="variant" required maxlength="60" autocomplete="off"
             placeholder="e.g. CHOPPER's book promo"></label>
      <label>Set<input name="set_label" maxlength="60" autocomplete="off" value="Unnumbered Promos"></label>
      <div class="custom-row">
        <label>Rarity<input name="rarity" maxlength="20" autocomplete="off" value="${esc(c.rarity || '')}"></label>
        <label>Price £<input name="price" inputmode="decimal" autocomplete="off" placeholder="optional"></label>
      </div>
      <label>Photo<input name="photo" type="file" accept="image/*"></label>
      <div class="custom-btns">
        <button type="submit" data-custom-save>Add print</button>
        <button type="button" class="ghost" data-custom-cancel>Cancel</button>
      </div>
      <div class="custom-msg" data-custom-msg role="status"></div>
    </form>
  </div>`;

// A phone photo is several megabytes; the card viewer shows it a few hundred
// pixels wide. Shrunk to 1100px on the long edge and sent as JPEG.
function shrinkPhoto(file) {
  return new Promise((resolve, reject) => {
    const url = URL.createObjectURL(file);
    const img = new Image();
    img.onload = () => {
      const scale = Math.min(1, 1100 / Math.max(img.naturalWidth, img.naturalHeight));
      const cv = document.createElement('canvas');
      cv.width = Math.round(img.naturalWidth * scale);
      cv.height = Math.round(img.naturalHeight * scale);
      cv.getContext('2d').drawImage(img, 0, 0, cv.width, cv.height);
      URL.revokeObjectURL(url);
      resolve(cv.toDataURL('image/jpeg', .88).split(',')[1]);
    };
    img.onerror = () => { URL.revokeObjectURL(url); reject(new Error('That file is not a picture this browser can read.')); };
    img.src = url;
  });
}

// Show a card where the viewer it came from was: the inspector or the modal.
function reopenCard(host, card) {
  if (host.id === 'inspector') showInspector(card);
  else openCardModal(card);
}

function wireCustomActs(host, c) {
  const form = host.querySelector('[data-custom-form]');
  const open = host.querySelector('[data-custom-open]');
  if (!form || !open) return;
  const msg = form.querySelector('[data-custom-msg]');
  open.onclick = () => {
    form.hidden = !form.hidden;
    if (!form.hidden) { form.scrollIntoView({block: 'nearest'}); form.elements.variant.focus(); }
  };
  form.querySelector('[data-custom-cancel]').onclick = () => { form.hidden = true; msg.textContent = ''; };
  form.onsubmit = async e => {
    e.preventDefault();
    const f = form.elements;
    const priceText = f.price.value.trim().replace(/^£/, '').replace(',', '.');
    const price = priceText ? Number(priceText) : null;
    if (priceText && !(price >= 0)) { msg.textContent = 'The price has to be a number, like 12.50.'; return; }
    const save = form.querySelector('[data-custom-save]');
    save.disabled = true;
    msg.textContent = 'Adding…';
    try {
      const file = f.photo.files[0];
      const image = file ? await shrinkPhoto(file) : null;
      const r = await api('/api/prints/custom', {method: 'POST',
        headers: {'content-type': 'application/json'},
        body: JSON.stringify({card_id: c.card_id, variant: f.variant.value, set_label: f.set_label.value,
                              rarity: f.rarity.value, price_gbp: price, image})});
      if (typeof doSearch === 'function') doSearch().catch(() => {});
      reopenCard(host, r.card);
    } catch (err) {
      msg.textContent = err.message || 'Could not add it';
      save.disabled = false;
    }
  };

  const remove = host.querySelector('[data-custom-remove]');
  if (remove) remove.onclick = async () => {
    if (!confirm(`Remove ${c.card_id}${c.variant ? ' (' + c.variant + ')' : ''} from the catalogue?`)) return;
    remove.disabled = true;
    try {
      await api('/api/prints/custom/delete', {method: 'POST',
        headers: {'content-type': 'application/json'},
        body: JSON.stringify({card_id: c.card_id})});
      if (typeof doSearch === 'function') doSearch().catch(() => {});
      if (host.id === 'inspector') clearInspector();
      else $('#modalHost').innerHTML = '';
    } catch (err) {
      remove.disabled = false;
      remove.textContent = err.message || 'Could not remove it';
    }
  };
}

// "This one is really the alt art": the owned card's other printings, each with
// how many of it this binder holds, and a way to re-file copies onto one of them.
function wirePrintActs(host, c) {
  const slot = host.querySelector('[data-print-acts]');
  if (!slot || !binderWritable() || !(c.qty > 0)) return;
  const binder = BINDER === null ? '' : '?binder=' + encodeURIComponent(BINDER);
  api('/api/prints/' + encodeURIComponent(c.card_id) + binder).then(d => {
    const prints = d.prints || [];
    if (!document.body.contains(slot) || prints.length < 2) return;
    slot.innerHTML = printStripHTML(prints, c.card_id,
        p => p.qty ? `<span class="print-own" title="owned">${p.qty}</span>` : '')
      + `<div class="print-change" hidden></div>`;
    const bar = slot.querySelector('.print-change');
    wirePrintStrip(slot, id => {
      if (id === c.card_id) { bar.hidden = true; return; }
      const p = prints.find(x => x.card_id === id);
      const n = c.qty || 0;
      bar.hidden = false;
      bar.innerHTML = `<span class="lbl">Change to ${esc(printLabel(p))}${
          p.set_label ? ' · ' + esc(p.set_label) : ''}</span>
        <button type="button" class="ghost" data-change="1">Change 1</button>
        ${n > 1 ? `<button type="button" class="ghost" data-change="${n}">Change all ${n}</button>` : ''}`;
      bar.scrollIntoView({block: 'nearest'});
      bar.querySelectorAll('[data-change]').forEach(b => b.onclick = async () => {
        bar.querySelectorAll('button').forEach(x => x.disabled = true);
        try {
          const r = await api('/api/collection/print', {method: 'POST',
            headers: {'content-type': 'application/json'},
            body: JSON.stringify({from: c.card_id, to: id, qty: +b.dataset.change})});
          syncCardQty(c.card_id, r.from_qty);
          syncCardQty(id, r.to_qty);
          refreshTotals();
          if (!$('#paneOwn').hidden) loadOwned().catch(() => {});
          // Show the print the copies went to, where the old one was showing.
          const next = (await api('/api/card/' + encodeURIComponent(id))).card;
          if (!next) return;
          if (host.id === 'inspector') showInspector(next);
          else openCardModal(next);
        } catch (e) {
          bar.querySelector('.lbl').textContent = e.message || 'Could not change it';
          bar.querySelectorAll('button').forEach(x => x.disabled = false);
        }
      });
    });
  }).catch(() => {});
}

const typeLine = c => [c.category, c.attributes, c.types].filter(Boolean).join('  ·  ');

// The list endpoints leave out effect text, Trigger text and attributes to keep
// search fast; fetch them once the card is actually open and slot them into the
// placeholders the viewer already drew.
function fillEffect(c, host) {
  if ('trigger' in c) return;
  api('/api/card/' + encodeURIComponent(c.card_id)).then(d => {
    const card = d && d.card;
    if (!card || !document.body.contains(host)) return;
    c.effect = card.effect;
    c.trigger = card.trigger;
    c.attributes = card.attributes;
    c.prices = card.prices;
    const fill = (sel, text) => host.querySelectorAll(sel).forEach(el => {
      el.textContent = text || '';
      el.hidden = !text;
    });
    fill('[data-effect]', c.effect);
    fill('[data-trigger]', c.trigger);
    fill('[data-typeline]', typeLine(c));
    host.querySelectorAll('[data-prices]').forEach(el => {
      el.innerHTML = pricesHTML(c.prices);
      el.hidden = !(c.prices && c.prices.length);
    });
  }).catch(() => {});
}

/* ---------- the inspector (desktop, beside the list) ---------- */
let inspected = null;
const inspectorLive = () => {
  const el = $('#inspector');
  return !!el && el.offsetParent !== null;
};

function showInspector(c) {
  const host = $('#inspector');
  if (!host) return;
  inspected = c;
  host.innerHTML = cardViewHTML(c, {actions: collectionActs(c)});
  wireCardView(host, c);
  wireCollectionActs(host, c);
  markSelected(c.card_id);
  fillEffect(c, host);
}

function clearInspector() {
  const host = $('#inspector');
  if (!host) return;
  inspected = null;
  host.innerHTML = `<div class="idle">
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.2"
         stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
      <rect x="4.5" y="2.5" width="15" height="19" rx="2"/>
      <path d="M8 7h8M8 11h8M8 15h5"/>
    </svg>
    <div>Pick a card to open its dossier.</div>
  </div>`;
}

function markSelected(id) {
  document.querySelectorAll('.gcard, .row').forEach(el =>
    el.classList.toggle('selected', el.dataset.id === id));
}

// One entry point for "the user picked this card": the inspector when there's
// room for it beside the list, the modal when there isn't.
function openCard(c) {
  if (inspectorLive()) showInspector(c);
  else openCardModal(c);
}

/* ---------- card detail modal (phones, and the deck tab) ---------- */
function cardModalHTML(c) {
  return `<div class="modal-backdrop" id="modalBackdrop">
    <div class="modal" role="dialog" aria-modal="true" aria-label="${esc(c.name || c.card_id)}">
      <div class="modal-top"><button class="modal-close" id="modalClose" aria-label="Close">&times;</button></div>
      ${cardViewHTML(c, {two: true, actions: collectionActs(c)})}
    </div>
  </div>`;
}

function openCardModal(c) {
  const host = $('#modalHost');
  host.innerHTML = cardModalHTML(c);
  const backdrop = $('#modalBackdrop');
  const onKey = e => { if (e.key === 'Escape') close(); };
  function close() { host.innerHTML = ''; document.removeEventListener('keydown', onKey); }
  backdrop.onclick = e => { if (e.target === backdrop) close(); };
  $('#modalClose').onclick = close;
  document.addEventListener('keydown', onKey);
  wireCardView(backdrop, c);
  wireCollectionActs(backdrop, c);
  fillEffect(c, backdrop);
}

function openImageZoom(src, label) {
  if (!src) return;
  const z = document.createElement('div');
  z.className = 'zoom-backdrop';
  z.innerHTML = `<img src="${src}" alt="${esc(label || '')}">`;
  z.onclick = () => z.remove();
  document.addEventListener('keydown', function onKey(e) {
    if (e.key === 'Escape') { z.remove(); document.removeEventListener('keydown', onKey); }
  });
  document.body.appendChild(z);
}

function syncCardQty(id, qty) {
  const sel = `[data-id="${CSS.escape(id)}"]`;
  document.querySelectorAll(`.row${sel} .own`).forEach(el => {
    el.textContent = qty;
    el.classList.toggle('zero', !qty);
  });
  document.querySelectorAll(`.gcard${sel}`).forEach(el => {
    el.classList.toggle('zero', !qty);
    const n = el.querySelector('[data-own]');
    if (n) n.textContent = qty;
  });
  // any card viewer currently showing this card - inspector, modal, or both
  if (inspected && inspected.card_id === id) inspected.qty = qty;
  if (cardIndex[id]) cardIndex[id].qty = qty;
  document.querySelectorAll('.cv-act [data-qty]').forEach(el => {
    const view = el.closest('.modal, #inspector');
    if (!view) return;
    const img = view.querySelector('.cv-frame img');
    if (img && decodeURIComponent(img.getAttribute('src').split('/img/')[1] || '') === id)
      el.textContent = qty;
  });
  const o = ownPager.items.find(c => c.card_id === id);
  if (o) o.qty = qty;
}
const syncRowQty = syncCardQty;   // old name, kept for anything still calling it
