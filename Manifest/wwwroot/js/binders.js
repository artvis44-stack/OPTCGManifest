/* Manifest: binders - which one you are looking at and logging into, sharing
   them with someone, and moving cards between them. */

let BINDERS = [];
const ALL_LABEL = 'All I can use';

const currentBinder = () =>
  BINDERS.find(b => String(b.id) === String(BINDER)) || null;
// "added by" only says something in a binder more than one person writes to.
const sharedView = () => (currentBinder() || {}).kind === 'shared';
const writableBinders = () => BINDERS.filter(b => b.writable);
const binderWritable = () => BINDER !== 'all' && !!(currentBinder() || {}).writable;

function rememberBinder() {
  try { localStorage.setItem('manifestBinder', BINDER); } catch (e) {}
}
function rememberedBinder() {
  try { return localStorage.getItem('manifestBinder'); } catch (e) { return null; }
}

// Fetches the list and settles on a binder: the one asked for, else the one last
// picked, else the first shared binder you are in - the one you most likely log
// into - else your own. Only a binder someone picks is remembered, so being added
// to a shared binder later still lands you in it.
async function loadBinders(want) {
  const r = await api('/api/binders');
  BINDERS = r.binders;
  want = want ?? rememberedBinder();
  const known = want === 'all' || BINDERS.some(b => String(b.id) === String(want));
  if (!known) {
    const shared = BINDERS.find(b => b.kind === 'shared' && b.writable);
    want = String((shared || BINDERS.find(b => b.mine)).id);
  }
  BINDER = String(want);
  paintBinderPick();
  applyBinder();
}

function paintBinderPick() {
  const opt = (value, label) =>
    `<option value="${esc(value)}"${String(value) === BINDER ? ' selected' : ''}>${esc(label)}</option>`;
  const mine = writableBinders();
  const others = BINDERS.filter(b => !b.writable);
  let html = mine.map(b => opt(b.id, b.name)).join('');
  if (mine.length > 1) html += opt('all', ALL_LABEL + ' (look only)');
  if (others.length)
    html += `<optgroup label="Shown to you">${
      others.map(b => opt(b.id, b.name + ' (look only)')).join('')}</optgroup>`;
  $('#binderPick').innerHTML = html;
}

// Everything on the page that depends on which binder is picked.
function applyBinder() {
  const b = currentBinder();
  const name = BINDER === 'all' ? ALL_LABEL : (b ? b.name : '');
  document.body.classList.toggle('readonly', !binderWritable());
  $('#exportLink').href = '/api/export.csv?binder=' + encodeURIComponent(BINDER);
  $('#ownedWhere').textContent = BINDER === 'all'
    ? 'every binder you are in, added together'
    : `everything in ${name}`;
  $('#binderNote').textContent =
      BINDER === 'all' ? 'read-only: your binders added together'
    : !b ? ''
    : !b.writable ? 'read-only'
    : b.kind === 'shared' ? 'shared with ' + b.members.join(', ')
    : 'only you';
  $('#q').placeholder = binderWritable()
    ? `OP01-016 or Nami — logs into ${name}`
    : 'Pick a binder you can add to, to log cards';
}

async function switchBinder(id) {
  BINDER = String(id);
  applyBinder();
  clearInspector();
  refreshTotals().catch(() => {});
  if (!$('#paneOwn').hidden) loadOwned().catch(() => {});
  if (logPager.items.length) doSearch().catch(() => {});
}

$('#binderPick').onchange = e => { switchBinder(e.target.value); rememberBinder(); };
$('#btnManageBinders').onclick = () => openBinderManager();

/* ---------- moving a card from the viewer ---------- */
// Offered in the card viewer when there is somewhere else it could go.
function moveActs(c) {
  if (!binderWritable()) return '';
  const targets = writableBinders().filter(b => String(b.id) !== BINDER);
  if (!targets.length) return '';
  return `<div class="cv-move">
    <span class="lbl">move one to</span>
    <select data-move-to aria-label="Move to binder">${
      targets.map(b => `<option value="${b.id}">${esc(b.name)}</option>`).join('')}</select>
    <button type="button" class="ghost" data-move>Move</button>
  </div>`;
}

function wireMoveActs(host, c) {
  const btn = host.querySelector('[data-move]');
  if (!btn) return;
  btn.onclick = async () => {
    const to = host.querySelector('[data-move-to]').value;
    btn.disabled = true;
    try {
      const r = await api('/api/binders/move', {method: 'POST',
        headers: {'content-type': 'application/json'},
        body: JSON.stringify({card_id: c.card_id, from: +BINDER, to: +to, qty: 1})});
      c.qty = r.from_qty;
      syncCardQty(c.card_id, r.from_qty);
      refreshTotals();
      btn.textContent = 'Moved';
      setTimeout(() => { btn.textContent = 'Move'; }, 1200);
    } catch (e) {
      btn.textContent = e.message || 'Could not move';
    } finally {
      btn.disabled = false;
    }
  };
}

/* ---------- the manager ---------- */
function binderManagerHTML() {
  const me = BINDERS.find(b => b.mine);
  const shared = BINDERS.filter(b => b.kind === 'shared');
  const sharedHTML = shared.map(b => `
    <div class="bnd" data-id="${b.id}">
      <div class="bndHead">
        <b>${esc(b.name)}</b>
        <span class="sm">${b.members.map(m => esc(m)).join(' · ')}</span>
      </div>
      <div class="bndActs">
        <input data-add placeholder="Add someone by username" autocomplete="off"
               autocapitalize="none" spellcheck="false">
        <button type="button" data-addbtn>Add</button>
      </div>
      <div class="bndActs">
        <button type="button" class="ghost" data-rename>Rename</button>
        <button type="button" class="ghost" data-leave>Leave</button>
        <button type="button" class="ghost danger" data-delete>Delete binder</button>
      </div>
    </div>`).join('');
  return `<div class="binderMgr">
    <h2>Binders</h2>
    <p class="tip flush">A shared binder is one collection that everyone in it can see and
      change. Your own binder, Mine, is just yours.</p>
    ${sharedHTML || '<p class="tip">No shared binders yet.</p>'}
    <div class="bnd new">
      <div class="bndHead"><b>New shared binder</b></div>
      <div class="bndActs">
        <input id="newBinderName" placeholder="e.g. Ours" maxlength="60" autocomplete="off">
        <button type="button" id="btnNewBinder">Create</button>
      </div>
      <label class="check"><input type="checkbox" id="newBinderMove">
        move everything in Mine into it</label>
    </div>
    <label class="check"><input type="checkbox" id="mineVisible"${me && me.visible ? ' checked' : ''}>
      let the people I share a binder with look at Mine (they can't change it)</label>
    <div class="status" id="binderStatus"></div>
  </div>`;
}

function openBinderManager() {
  const host = $('#modalHost');
  const onKey = e => { if (e.key === 'Escape') close(); };
  function close() { host.innerHTML = ''; document.removeEventListener('keydown', onKey); }
  function paint(message, err) {
    host.innerHTML = `<div class="modal-backdrop" id="modalBackdrop">
      <div class="modal" role="dialog" aria-modal="true" aria-label="Binders">
        <div class="modal-top"><button class="modal-close" id="modalClose" aria-label="Close">&times;</button></div>
        ${binderManagerHTML()}
      </div></div>`;
    $('#modalBackdrop').onclick = e => { if (e.target.id === 'modalBackdrop') close(); };
    $('#modalClose').onclick = close;
    if (message) {
      $('#binderStatus').textContent = message;
      $('#binderStatus').className = 'status' + (err ? ' err' : '');
    }
    wire();
  }

  // Every change reloads the list, repaints, and keeps the picked binder if it
  // still exists - leaving or deleting the one you are on falls back to another.
  async function act(call, done) {
    try {
      await call();
      await loadBinders(BINDER);
      switchBinder(BINDER);
      paint(done);
    } catch (e) {
      paint(e.message || 'That did not work.', true);
    }
  }
  const post = (path, body) => api(path, {method: 'POST',
    headers: {'content-type': 'application/json'}, body: JSON.stringify(body || {})});
  const me = () => BINDERS.find(b => b.mine);

  function wire() {
    $('#btnNewBinder').onclick = () => {
      const name = $('#newBinderName').value.trim();
      const move_mine = $('#newBinderMove').checked;
      if (!name) return;
      act(async () => {
        const r = await post('/api/binders', {name, move_mine});
        BINDER = String(r.binder.id);
        rememberBinder();
      }, `Made ${name}. Add someone to it below.`);
    };
    $('#mineVisible').onchange = e => act(
      () => post('/api/binders/visibility', {visible: e.target.checked}),
      e.target.checked ? 'Mine is now shown to the people you share with.' : 'Mine is private again.');
    host.querySelectorAll('.bnd[data-id]').forEach(el => {
      const id = el.dataset.id;
      const b = BINDERS.find(x => String(x.id) === id);
      el.querySelector('[data-addbtn]').onclick = () => {
        const username = el.querySelector('[data-add]').value.trim();
        if (username) act(() => post(`/api/binders/${id}/members`, {username}), `Added ${username}.`);
      };
      el.querySelector('[data-add]').onkeydown = e => {
        if (e.key === 'Enter') el.querySelector('[data-addbtn]').click();
      };
      el.querySelector('[data-rename]').onclick = () => {
        const name = prompt('Rename binder', b.name);
        if (name && name.trim()) act(() => post(`/api/binders/${id}`, {name}), 'Renamed.');
      };
      el.querySelector('[data-leave]').onclick = () => {
        const alone = b.members.length <= 1;
        if (!confirm(alone
          ? `You are the only one in ${b.name}. Leaving deletes it and every card in it.`
          : `Leave ${b.name}? The others keep it and its cards.`)) return;
        const self = $('#whoName').textContent;
        act(() => post(`/api/binders/${id}/members/${encodeURIComponent(self)}/delete`), `Left ${b.name}.`);
      };
      el.querySelector('[data-delete]').onclick = () => {
        if (!confirm(`Delete ${b.name} and every card in it, for everyone in it? This cannot be undone.`))
          return;
        act(() => post(`/api/binders/${id}/delete`), `Deleted ${b.name}.`);
      };
    });
  }

  document.addEventListener('keydown', onKey);
  paint();
}
