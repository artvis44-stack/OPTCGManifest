/* Manifest: the Selling tab - eBay listings drafted from a card, and each one
   tracked from draft to sale. Nothing is sent to eBay: the composer writes the
   title, description and item specifics to copy into eBay's own form. */

const LISTING_STATUS = {draft: 'Draft', listed: 'Listed', sold: 'Sold', ended: 'Ended unsold'};
const CONDITIONS = ['Near Mint', 'Lightly Played', 'Moderately Played', 'Heavily Played', 'Damaged'];
const FOOTER_KEY = 'manifest.listingFooter';
const DEFAULT_FOOTER = 'Posted in a penny sleeve and toploader inside a stiffened envelope.\n'
  + 'Combined postage on multiple wins - message me before paying.';

let LISTINGS = [];
let sellFilter = 'all';

const postJSON = (path, body) => api(path, {method: 'POST',
  headers: {'content-type': 'application/json'}, body: JSON.stringify(body || {})});

// The footer is the seller's own postage and returns blurb, kept on this device.
function shopFooter() {
  try { const v = localStorage.getItem(FOOTER_KEY); return v === null ? DEFAULT_FOOTER : v; }
  catch { return DEFAULT_FOOTER; }
}
function saveFooter(v) { try { localStorage.setItem(FOOTER_KEY, v); } catch {} }

// The page is often served over plain http on the home network, where the
// Clipboard API does not exist; a selected textarea and execCommand still works.
async function copyText(text, btn) {
  let ok = false;
  try {
    if (navigator.clipboard && window.isSecureContext) { await navigator.clipboard.writeText(text); ok = true; }
  } catch {}
  if (!ok) {
    const ta = document.createElement('textarea');
    ta.value = text;
    ta.setAttribute('readonly', '');
    ta.style.cssText = 'position:fixed;top:0;left:0;opacity:0';
    document.body.appendChild(ta);
    ta.select();
    try { ok = document.execCommand('copy'); } catch {}
    ta.remove();
  }
  if (btn) {
    const was = btn.dataset.label || btn.textContent;
    btn.dataset.label = was;
    btn.textContent = ok ? 'Copied' : 'Copy failed';
    setTimeout(() => { btn.textContent = was; }, 1300);
  }
}

// A note that something worked, wherever on the page the person is.
function toast(text, err) {
  const el = document.createElement('div');
  el.className = 'toast' + (err ? ' err' : '');
  el.setAttribute('role', 'status');
  el.textContent = text;
  document.body.appendChild(el);
  setTimeout(() => el.remove(), 2600);
}

const money = v => (v || v === 0) ? fmtGBP(v) : '—';
const numOrNull = s => {
  const t = String(s ?? '').trim().replace(/^£/, '').replace(',', '.');
  if (!t) return null;
  const n = Number(t);
  return n >= 0 ? n : NaN;
};
const day = s => s ? s.slice(0, 10) : '';

function openModal(html, label) {
  const host = $('#modalHost');
  const onKey = e => { if (e.key === 'Escape') close(); };
  function close() { host.innerHTML = ''; document.removeEventListener('keydown', onKey); }
  host.innerHTML = `<div class="modal-backdrop" id="modalBackdrop">
    <div class="modal" role="dialog" aria-modal="true" aria-label="${esc(label)}">
      <div class="modal-top"><button class="modal-close" id="modalClose" aria-label="Close">&times;</button></div>
      ${html}
    </div></div>`;
  $('#modalBackdrop').onclick = e => { if (e.target.id === 'modalBackdrop') close(); };
  $('#modalClose').onclick = close;
  document.addEventListener('keydown', onKey);
  return {host: $('#modalBackdrop'), close};
}

/* ---------- the composer ----------
   Opened on a card (new listing) or on a saved listing (editing). For a new one
   the words are redrafted whenever condition, grade or quantity change, until the
   seller types into the title or description themselves. */
function composerHTML(c, l) {
  const cond = l ? l.condition : CONDITIONS[0];
  const graded = l && !CONDITIONS.includes(l.condition) ? l.condition : '';
  return `<form class="lst" data-lst>
    <div class="lst-head">
      <img alt="" src="/img/${encodeURIComponent(c.card_id)}">
      <div>
        <div class="cid">${esc(c.card_id)}</div>
        <h2>${esc(c.name || '')}</h2>
        <div class="sm">${esc([c.set_label, rarityLabel(c.rarity), c.variant].filter(Boolean).join(' · '))}</div>
        <div class="lst-market" data-market></div>
      </div>
    </div>

    <div class="lst-grid">
      <label>Condition<select name="condition">
        ${CONDITIONS.map(x => `<option${x === cond && !graded ? ' selected' : ''}>${x}</option>`).join('')}
      </select></label>
      <label>Graded<input name="grade" maxlength="40" autocomplete="off" placeholder="e.g. PSA 10"
             value="${esc(graded)}"></label>
      <label>Qty<input name="qty" inputmode="numeric" autocomplete="off" value="${l ? l.qty : 1}"></label>
      <label>Price £<input name="price" inputmode="decimal" autocomplete="off"
             value="${l && l.price_gbp != null ? l.price_gbp.toFixed(2) : ''}"></label>
      <label>You paid £<input name="cost" inputmode="decimal" autocomplete="off" placeholder="optional"
             value="${l && l.cost_gbp != null ? l.cost_gbp.toFixed(2) : ''}"></label>
    </div>

    <div class="lst-field">
      <div class="lst-lbl">Title <span data-count></span>
        <button type="button" class="ghost" data-copy="title">Copy</button></div>
      <textarea name="title" class="lst-title" rows="2" maxlength="80" spellcheck="true">${l ? esc(l.title) : ''}</textarea>
    </div>
    <div class="lst-field">
      <div class="lst-lbl">Description
        <button type="button" class="ghost" data-copy="description">Copy</button></div>
      <textarea name="description" spellcheck="true">${l ? esc(l.description) : ''}</textarea>
    </div>
    <details class="lst-foot">
      <summary>Your shop footer (added to new descriptions)</summary>
      <textarea name="footer" spellcheck="true">${esc(shopFooter())}</textarea>
    </details>

    <div class="lst-field">
      <div class="lst-lbl">Item specifics
        <button type="button" class="ghost" data-copy="specifics">Copy all</button></div>
      <div class="lst-specs" data-specs><span class="sm">Drafting…</span></div>
    </div>

    <div class="lst-grid">
      <label class="wide">eBay link<input name="ebay_url" inputmode="url" autocomplete="off"
             placeholder="paste once it's live (optional)" value="${l && l.ebay_url ? esc(l.ebay_url) : ''}"></label>
      <label class="wide">Note<input name="note" maxlength="1000" autocomplete="off"
             value="${l ? esc(l.note) : ''}"></label>
    </div>

    <div class="custom-btns">
      ${l ? `<button type="submit" data-save="${l.status}">Save</button>`
          : `<button type="submit" data-save="listed">Save as listed</button>
             <button type="submit" class="ghost" data-save="draft">Save draft</button>`}
      <a class="ghost lst-comps" data-comps target="_blank" rel="noopener noreferrer" hidden>Sold prices on eBay</a>
      ${l ? `<button type="button" class="ghost danger" data-delete>Delete listing</button>` : ''}
    </div>
    <div class="custom-msg" data-msg role="status"></div>
  </form>`;
}

async function openListingComposer(card, listing) {
  const c = card || {card_id: listing.card_id, name: listing.name, set_label: listing.set_label,
                     rarity: listing.rarity, variant: listing.variant};
  const {host, close} = openModal(composerHTML(c, listing), 'eBay listing');
  const form = host.querySelector('[data-lst]');
  const f = form.elements;
  const msg = form.querySelector('[data-msg]');
  let specifics = [];
  let touched = !!listing;   // a saved listing's words are the seller's own
  let drafted = null;

  const count = () => {
    const n = f.title.value.length;
    const el = form.querySelector('[data-count]');
    el.textContent = `${n}/80`;
    el.className = n > 80 ? 'over' : '';
  };
  const paintSpecs = () => {
    form.querySelector('[data-specs]').innerHTML = specifics.map((s, i) =>
      `<div class="spec"><span>${esc(s.name)}</span><b>${esc(s.value)}</b>
        <button type="button" class="ghost" data-spec="${i}">Copy</button></div>`).join('');
    form.querySelectorAll('[data-spec]').forEach(b =>
      b.onclick = () => copyText(specifics[+b.dataset.spec].value, b));
  };
  const withFooter = d => [d, f.footer.value.trim()].filter(Boolean).join('\n\n');

  async function redraft() {
    const params = new URLSearchParams({card_id: c.card_id, condition: f.condition.value,
                                        grade: f.grade.value.trim(), qty: f.qty.value || '1'});
    try {
      const {draft} = await api('/api/listings/draft?' + params);
      drafted = draft;
      specifics = draft.specifics;
      paintSpecs();
      if (!touched) {
        f.title.value = draft.title;
        f.description.value = withFooter(draft.description);
        count();
      }
      if (!listing && !f.price.value && draft.market_gbp) f.price.value = draft.market_gbp.toFixed(2);
      const prices = draft.prices && draft.prices.length ? pricesHTML(draft.prices) : '';
      form.querySelector('[data-market]').innerHTML = draft.market_gbp
        ? `<span>Market <b>${fmtGBP(draft.market_gbp)}</b></span>${prices}` : prices;
      const comps = form.querySelector('[data-comps]');
      comps.href = draft.sold_search_url;
      comps.hidden = false;
    } catch (e) {
      msg.textContent = e.message || 'Could not draft this card.';
    }
  }

  let redraftTimer = null;
  const later = () => { clearTimeout(redraftTimer); redraftTimer = setTimeout(redraft, 250); };
  f.condition.onchange = redraft;
  f.grade.oninput = later;
  f.qty.oninput = later;
  f.title.oninput = () => { touched = true; count(); };
  f.description.oninput = () => { touched = true; };
  f.footer.oninput = () => {
    saveFooter(f.footer.value);
    if (!touched && drafted) f.description.value = withFooter(drafted.description);
  };
  form.querySelectorAll('[data-copy]').forEach(b => b.onclick = () => {
    const what = b.dataset.copy;
    copyText(what === 'specifics' ? specifics.map(s => `${s.name}: ${s.value}`).join('\n') : f[what].value, b);
  });

  let saveAs = listing ? listing.status : 'draft';
  form.querySelectorAll('[data-save]').forEach(b => b.onclick = () => { saveAs = b.dataset.save; });
  form.onsubmit = async e => {
    e.preventDefault();
    const nums = {price_gbp: numOrNull(f.price.value), cost_gbp: numOrNull(f.cost.value)};
    if (Object.values(nums).some(Number.isNaN)) { msg.textContent = 'Prices have to be numbers, like 12.50.'; return; }
    const body = {
      ...(listing || {}), card_id: c.card_id, title: f.title.value, description: f.description.value,
      condition: f.grade.value.trim() || f.condition.value, qty: +f.qty.value || 1, status: saveAs,
      ebay_url: f.ebay_url.value, note: f.note.value, ...nums,
    };
    msg.textContent = 'Saving…';
    try {
      await postJSON(listing ? `/api/listings/${listing.id}` : '/api/listings', body);
      close();
      if (!listing) toast(saveAs === 'listed' ? 'Saved as listed - see the Selling tab.' : 'Draft saved - see the Selling tab.');
      if (!$('#paneSell').hidden || listing) loadSelling().catch(() => {});
    } catch (err) {
      msg.textContent = err.message || 'Could not save it.';
    }
  };
  const del = form.querySelector('[data-delete]');
  if (del) del.onclick = async () => {
    if (!confirm('Delete this listing? Its sale, if any, drops out of the totals.')) return;
    try {
      await postJSON(`/api/listings/${listing.id}/delete`);
      close();
      loadSelling().catch(() => {});
    } catch (err) { msg.textContent = err.message || 'Could not delete it.'; }
  };

  count();
  redraft();
}

/* ---------- moving a listing along ---------- */
function listingBody(l, changes) {
  return {card_id: l.card_id, title: l.title, description: l.description, condition: l.condition,
          qty: l.qty, status: l.status, price_gbp: l.price_gbp, cost_gbp: l.cost_gbp,
          sold_price_gbp: l.sold_price_gbp, fees_gbp: l.fees_gbp, postage_gbp: l.postage_gbp,
          ebay_url: l.ebay_url, note: l.note, ...changes};
}

function openMarkListed(l) {
  const {host, close} = openModal(`<form class="lst" data-f>
      <h2 class="lst-h">Mark as listed</h2>
      <div class="sm">${esc(l.title)}</div>
      <div class="lst-grid">
        <label class="wide">eBay link<input name="ebay_url" inputmode="url" autocomplete="off"
               placeholder="optional" value="${esc(l.ebay_url || '')}"></label>
        <label>Price £<input name="price" inputmode="decimal" autocomplete="off"
               value="${l.price_gbp != null ? l.price_gbp.toFixed(2) : ''}"></label>
      </div>
      <div class="custom-btns"><button type="submit">Listed</button></div>
      <div class="custom-msg" data-msg role="status"></div>
    </form>`, 'Mark as listed');
  const form = host.querySelector('[data-f]');
  form.onsubmit = async e => {
    e.preventDefault();
    const price = numOrNull(form.elements.price.value);
    if (Number.isNaN(price)) { form.querySelector('[data-msg]').textContent = 'The price has to be a number.'; return; }
    try {
      await postJSON(`/api/listings/${l.id}`, listingBody(l,
        {status: 'listed', ebay_url: form.elements.ebay_url.value, price_gbp: price}));
      close();
      loadSelling().catch(() => {});
    } catch (err) { form.querySelector('[data-msg]').textContent = err.message || 'Could not save it.'; }
  };
}

function openMarkSold(l) {
  const canTake = typeof binderWritable === 'function' && binderWritable();
  const b = typeof currentBinder === 'function' ? currentBinder() : null;
  const {host, close} = openModal(`<form class="lst" data-f>
      <h2 class="lst-h">Sold</h2>
      <div class="sm">${esc(l.title)}</div>
      <div class="lst-grid">
        <label>Sold for £<input name="sold" inputmode="decimal" autocomplete="off"
               value="${(l.sold_price_gbp ?? l.price_gbp) != null ? (l.sold_price_gbp ?? l.price_gbp).toFixed(2) : ''}"></label>
        <label>eBay fees £<input name="fees" inputmode="decimal" autocomplete="off" placeholder="0"
               value="${l.fees_gbp != null ? l.fees_gbp.toFixed(2) : ''}"></label>
        <label>Postage £<input name="postage" inputmode="decimal" autocomplete="off" placeholder="0"
               value="${l.postage_gbp != null ? l.postage_gbp.toFixed(2) : ''}"></label>
      </div>
      ${canTake ? `<label class="check"><input type="checkbox" name="take" checked>
        take ${l.qty} out of ${esc(b ? b.name : 'this binder')}</label>` : ''}
      <div class="custom-btns"><button type="submit">Mark sold</button></div>
      <div class="custom-msg" data-msg role="status"></div>
    </form>`, 'Mark as sold');
  const form = host.querySelector('[data-f]');
  const msg = form.querySelector('[data-msg]');
  form.onsubmit = async e => {
    e.preventDefault();
    const f = form.elements;
    const nums = {sold_price_gbp: numOrNull(f.sold.value), fees_gbp: numOrNull(f.fees.value),
                  postage_gbp: numOrNull(f.postage.value)};
    if (Object.values(nums).some(Number.isNaN)) { msg.textContent = 'Amounts have to be numbers, like 12.50.'; return; }
    try {
      await postJSON(`/api/listings/${l.id}`, listingBody(l, {status: 'sold', ...nums}));
      if (f.take && f.take.checked) {
        try {
          await postJSON('/api/collection', {card_id: l.card_id, delta: -l.qty});
          refreshTotals();
        } catch (err) {
          msg.textContent = `Marked sold, but could not take it out of the binder: ${err.message}`;
          loadSelling().catch(() => {});
          return;
        }
      }
      close();
      loadSelling().catch(() => {});
    } catch (err) { msg.textContent = err.message || 'Could not save it.'; }
  };
}

async function setListingStatus(l, status) {
  await postJSON(`/api/listings/${l.id}`, listingBody(l, {status}));
  loadSelling().catch(() => {});
}

/* ---------- the tab ---------- */
function statTile(label, value, sub) {
  return `<div class="sell-tile"><span>${esc(label)}</span><b>${value}</b>${sub ? `<i>${sub}</i>` : ''}</div>`;
}

function paintSellStats(s, top) {
  const days = s.median_days_to_sell;
  $('#sellStats').innerHTML = [
    statTile('Listed now', s.listed, s.listed ? fmtGBP(s.listed_value_gbp) + ' asking' : ''),
    statTile('Sold', s.sold, s.sold_cards !== s.sold ? `${s.sold_cards} cards` : ''),
    statTile('Revenue', money(s.revenue_gbp), s.fees_gbp ? `${fmtGBP(s.fees_gbp)} fees + post` : ''),
    statTile('Profit', `<em class="${s.profit_gbp < 0 ? 'neg' : ''}">${money(s.profit_gbp)}</em>`, ''),
    statTile('Time to sell', days == null ? '—' : `${days}d`, 'median'),
    statTile('Sell-through', s.sell_through == null ? '—' : Math.round(s.sell_through * 100) + '%',
             s.drafts ? `${s.drafts} draft${s.drafts === 1 ? '' : 's'}` : ''),
  ].join('');

  $('#sellTop').innerHTML = top.length ? top.map((t, i) => `
    <div class="buyrow">
      <span class="rank">${i + 1}</span>
      <img loading="lazy" alt="" src="/img/${encodeURIComponent(t.card_id)}">
      <div class="meta"><div class="nm">${esc(t.name || t.card_id)}</div>
        <div class="sm">${esc(t.card_id)} · ${t.cards} sold${t.median_days_to_sell != null ? ` · ${t.median_days_to_sell}d to sell` : ''}</div></div>
      <div class="price">${fmtGBP(t.revenue_gbp)}<span class="need">${t.average_gbp != null ? fmtGBP(t.average_gbp) + ' each' : ''}</span></div>
    </div>`).join('')
    : '<div class="empty">Nothing sold in this period yet.</div>';
}

function listingRowHTML(l) {
  const price = l.status === 'sold'
    ? `${money(l.sold_price_gbp)}<span class="need ${l.profit_gbp < 0 ? 'neg' : ''}">${l.profit_gbp != null ? (l.profit_gbp < 0 ? '' : '+') + fmtGBP(l.profit_gbp) : ''}</span>`
    : `${money(l.price_gbp)}<span class="need">${l.market_gbp ? 'mkt ' + fmtGBP(l.market_gbp) : ''}</span>`;
  const when = l.status === 'sold' ? 'sold ' + day(l.sold_at)
    : l.status === 'listed' ? 'listed ' + day(l.listed_at)
    : l.status === 'ended' ? 'ended ' + day(l.updated_at) : 'drafted ' + day(l.created_at);
  const acts = {
    draft: `<button type="button" class="ghost" data-act="listed">Mark listed</button>`,
    listed: `<button type="button" class="ghost" data-act="sold">Sold</button>
             <button type="button" class="ghost" data-act="ended">Ended unsold</button>`,
    ended: `<button type="button" class="ghost" data-act="relist">Relist</button>`,
    sold: '',
  }[l.status];
  return `<div class="lrow" data-id="${l.id}">
    <img loading="lazy" alt="" src="/img/${encodeURIComponent(l.card_id)}">
    <div class="meta">
      <div class="nm">${esc(l.title)}</div>
      <div class="sm"><span class="pill ${l.status}">${LISTING_STATUS[l.status]}</span>
        ${esc(l.card_id)}${l.qty > 1 ? ' ×' + l.qty : ''} · ${esc(when)}
        ${l.ebay_url ? ` · <a href="${esc(l.ebay_url)}" target="_blank" rel="noopener noreferrer">eBay</a>` : ''}</div>
      <div class="lacts">
        <button type="button" class="ghost" data-act="copy">Copy title</button>
        <button type="button" class="ghost" data-act="edit">Edit</button>
        ${acts}
      </div>
    </div>
    <div class="price">${price}</div>
  </div>`;
}

function paintListings() {
  const shown = LISTINGS.filter(l => sellFilter === 'all' || l.status === sellFilter);
  document.querySelectorAll('#sellFilter button').forEach(b => {
    const n = b.dataset.f === 'all' ? LISTINGS.length : LISTINGS.filter(l => l.status === b.dataset.f).length;
    b.classList.toggle('on', b.dataset.f === sellFilter);
    b.querySelector('i').textContent = n;
  });
  const host = $('#sellList');
  host.innerHTML = shown.length ? shown.map(listingRowHTML).join('')
    : `<div class="empty">${LISTINGS.length ? 'Nothing here.'
      : 'No listings yet. Type a card number above, or open any card and tap <b>Sell on eBay</b>.'}</div>`;
  host.querySelectorAll('.lrow').forEach(row => {
    const l = LISTINGS.find(x => String(x.id) === row.dataset.id);
    row.querySelectorAll('[data-act]').forEach(b => b.onclick = () => {
      const act = b.dataset.act;
      if (act === 'copy') copyText(l.title, b);
      else if (act === 'edit') openListingComposer(null, l);
      else if (act === 'listed') openMarkListed(l);
      else if (act === 'sold') openMarkSold(l);
      else if (act === 'ended') setListingStatus(l, 'ended').catch(e => toast(e.message, true));
      else if (act === 'relist') openMarkListed(l);
    });
  });
}

async function loadSelling() {
  const days = $('#sellDays').value;
  const r = await api('/api/listings' + (days ? '?days=' + days : ''));
  LISTINGS = r.listings;
  paintSellStats(r.stats, r.top);
  paintListings();
}

$('#sellDays').onchange = () => loadSelling().catch(() => {});
document.querySelectorAll('#sellFilter button').forEach(b =>
  b.onclick = () => { sellFilter = b.dataset.f; paintListings(); });

async function startListing() {
  const v = $('#sellQ').value.trim();
  if (!v) return;
  const msg = $('#sellMsg');
  msg.className = 'status';
  msg.textContent = '';
  try {
    const {results} = await api('/api/search?q=' + encodeURIComponent(v) + '&limit=1');
    if (!results.length) { msg.className = 'status err'; msg.textContent = `Nothing in the catalogue matches ${v}.`; return; }
    $('#sellQ').value = '';
    openListingComposer(results[0]);
  } catch (e) {
    msg.className = 'status err';
    msg.textContent = e.message || 'Could not look that up.';
  }
}
$('#sellGo').onclick = startListing;
$('#sellQ').onkeydown = e => { if (e.key === 'Enter') startListing(); };
