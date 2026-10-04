/* Manifest: the fetch wrapper and the few globals every other script uses.
   The scripts share one global scope and load in this order:
   api, cards, collection, decks, scan, session. */

const $ = s => document.querySelector(s);
async function api(p, o) {
  try {
    const r = await fetch(p, o);
    // A session that ran out mid-use is not a broken server. Reloading lands on
    // the sign-in page, because "/" serves that to anyone without an account.
    if (r.status === 401) { location.reload(); throw new Error('signed out'); }
    const text = await r.text();
    const data = text ? JSON.parse(text) : {};
    if (!r.ok) {
      const err = new Error(data.error || ('HTTP ' + r.status));
      err.status = r.status;
      throw err;
    }
    return data;
  } catch (e) {
    if (e.name !== 'AbortError' && e.message !== 'signed out' && !e.status)
      say('Lost contact with the server. Is it still running?', true);
    throw e;
  }
}
let stream = null, pending = null, pendQty = 1, lastHit = null, USE_API = false;
let cardIndex = {};
const indexCards = list => list.forEach(c => cardIndex[c.card_id] = c);
