// Load test for Manifest: the public-launch target from SCALING_PLAN.md.
//
//   1,000 registered users, 100 of them active at once
//   p95 under 300 ms for normal reads, under 500 ms for writes
//   no increment lost when many requests hit the same row
//
// Run it with loadtest/run.sh, against a staging stack - never the live site:
// setup() makes USERS accounts (load-0000 ...) and every one of them logs cards.
//
// The 100 active users are split across what people actually do: browsing and
// searching (half of them), logging cards, building decks, loading card art and
// scanning. Each waits a second or three between actions, as a person does. A
// separate "contention" scenario has 20 clients add +1 to one card of one shared
// account as fast as they can; teardown() then checks the count is exact.

import http from 'k6/http';
import { check, sleep, fail } from 'k6';
import { Counter, Trend } from 'k6/metrics';
import encoding from 'k6/encoding';

const env = (name, fallback) => (__ENV[name] !== undefined && __ENV[name] !== '' ? __ENV[name] : fallback);
const BASE = env('BASE_URL', 'http://localhost:8420').replace(/\/$/, '');
const INVITE = env('INVITE', '');
const USERS = parseInt(env('USERS', '1000'), 10);
const ACTIVE = parseInt(env('ACTIVE', '100'), 10);
const DURATION = env('DURATION', '5m');
const RAMP = env('RAMP', '1m');
const PREFIX = env('USER_PREFIX', 'load');
const CONTENTION = parseInt(env('CONTENTION', '2000'), 10);
const IMAGE_CARDS = parseInt(env('IMAGE_CARDS', '24'), 10);
const PASSWORD = 'load-test-password-1';
const SHARED = `${PREFIX}-shared`;

// A rendered "OP01-016" corner, so a scan does real OCR work and logs a card.
const SCAN_PNG = encoding.b64encode(open('./scan-corner.png', 'b'));

const lostIncrements = new Counter('lost_increments');
const contentionSent = new Counter('contention_increments_ok');
const scanEndToEnd = new Trend('scan_end_to_end', true);

const share = (fraction) => Math.max(1, Math.round(ACTIVE * fraction));
const ramping = (exec, vus) => ({
  executor: 'ramping-vus',
  exec,
  startVUs: 0,
  stages: [
    { duration: RAMP, target: vus },
    { duration: DURATION, target: vus },
    { duration: '20s', target: 0 },
  ],
  gracefulRampDown: '10s',
});

export const options = {
  setupTimeout: '15m',
  // k6 empties each VU's cookie jar every iteration unless told not to; a person
  // stays signed in.
  noCookiesReset: true,
  teardownTimeout: '2m',
  scenarios: {
    browse: ramping('browse', share(0.5)),
    logging: ramping('logging', share(0.25)),
    decks: ramping('decks', share(0.1)),
    images: ramping('images', share(0.1)),
    scans: ramping('scans', share(0.05)),
    contention: {
      executor: 'shared-iterations',
      exec: 'contention',
      vus: 20,
      iterations: CONTENTION,
      maxDuration: '10m',
    },
  },
  thresholds: {
    'http_req_duration{kind:read}': ['p(95)<300'],
    'http_req_duration{kind:write}': ['p(95)<500'],
    'http_req_duration{kind:image}': ['p(95)<300'],
    'http_req_failed{kind:read}': ['rate<0.01'],
    'http_req_failed{kind:write}': ['rate<0.01'],
    'http_req_failed{kind:image}': ['rate<0.01'],
    scan_end_to_end: ['p(95)<5000'],
    checks: ['rate>0.99'],
    lost_increments: ['count==0'],
  },
  summaryTrendStats: ['avg', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

const JSON_HEADERS = { 'Content-Type': 'application/json' };
const post = (path, body, tags) =>
  http.post(`${BASE}${path}`, JSON.stringify(body), { headers: JSON_HEADERS, tags });
const get = (path, tags, extra = {}) => http.get(`${BASE}${path}`, Object.assign({ tags }, extra));
const pick = (list) => list[Math.floor(Math.random() * list.length)];
const think = () => sleep(1 + Math.random() * 2);
const username = (i) => `${PREFIX}-${String(i).padStart(4, '0')}`;

function signIn(name) {
  const r = post('/api/auth/login', { username: name, password: PASSWORD }, { kind: 'auth', name: 'login' });
  if (r.status !== 200) fail(`could not sign in as ${name}: ${r.status} ${r.body}`);
}

// ---- setup: accounts, and the cards to use

export function setup() {
  if (!INVITE) fail('set INVITE to the server\'s MANIFEST_INVITE_CODE');

  const names = [SHARED];
  for (let i = 0; i < USERS; i++) names.push(username(i));
  // Ten at a time: registration hashes a password, which is slow on purpose.
  for (let i = 0; i < names.length; i += 10) {
    const batch = names.slice(i, i + 10).map((name) => ({
      method: 'POST',
      url: `${BASE}/api/auth/register`,
      body: JSON.stringify({ username: name, password: PASSWORD, invite: INVITE }),
      params: { headers: JSON_HEADERS, tags: { kind: 'setup' } },
    }));
    http.batch(batch).forEach((r, j) => {
      // 400 "taken" is an account left from an earlier run, which is fine.
      if (r.status !== 200 && !(r.status === 400 && /taken/.test(r.body))) {
        fail(`could not register ${batch[j].body}: ${r.status} ${r.body}`);
      }
    });
  }

  signIn(SHARED);
  const cards = [];
  let cursor = '';
  do {
    const r = get(`/api/search?cursor=${encodeURIComponent(cursor)}&limit=200`, { kind: 'setup' });
    if (r.status !== 200) fail(`catalogue page failed: ${r.status} ${r.body}`);
    const page = r.json();
    page.items.forEach((c) => cards.push({ id: c.card_id, leader: /leader/i.test(c.category || ''), art: !!c.image_url }));
    cursor = page.next_cursor || '';
  } while (cursor);

  const leaders = cards.filter((c) => c.leader).map((c) => c.id);
  const others = cards.filter((c) => !c.leader).map((c) => c.id);
  if (leaders.length === 0 || others.length === 0) fail('the catalogue has no leaders or no other cards');

  // Card art comes from the official card site the first time. Keep to a small
  // set, fetched once here, so the test measures this server and not theirs.
  const art = others.filter((id) => cards.find((c) => c.id === id).art).slice(0, IMAGE_CARDS);
  const deadline = Date.now() + 90 * 1000;
  let pending = art.slice();
  while (pending.length > 0 && Date.now() < deadline) {
    pending = pending.filter((id) => {
      const r = get(`/img/${id}`, { kind: 'setup' }, { responseCallback: http.expectedStatuses(200, 404) });
      return r.status === 404 && r.headers['X-Image'] === 'pending';
    });
    if (pending.length > 0) sleep(1);
  }
  const stored = art.filter((id) => get(`/img/${id}`, { kind: 'setup' },
    { responseCallback: http.expectedStatuses(200, 404) }).status === 200);
  console.log(`setup: ${names.length} accounts, ${cards.length} printings, ${stored.length}/${art.length} pictures stored`);

  // The contention target starts at zero.
  const contended = others[0];
  post('/api/collection', { card_id: contended, qty: 0 }, { kind: 'setup' });

  return { leaders, others, art: stored.length > 0 ? stored : art, contended };
}

// Each VU is one person: an account of its own, signed in on its first action.
let signedIn = false;
function asMe() {
  if (signedIn) return;
  signIn(username((__VU - 1) % USERS));
  signedIn = true;
}

// ---- what people do

const TERMS = ['nami', 'luffy', 'zoro', 'sanji', 'law', 'kid', 'ace', 'shanks', 'buggy', 'usopp',
  'robin', 'chopper', 'franky', 'brook', 'yamato', 'kaido', 'hancock', 'mihawk', 'crocodile', 'op01',
  'st10', 'eb01', 'leader', 'red', 'straw hat'];
const SORTS = ['set_label', 'colors', 'price_gbp', 'not_owned'];
const COLORS = ['Red', 'Green', 'Blue', 'Purple', 'Black', 'Yellow'];

// The next page repeats the query it continues, as the page does; the server
// refuses a cursor sent with a different sort or filter.
let lastQuery = '';
let lastCursor = '';
function searchPage(query, name) {
  const r = get(`/api/search?${query}&cursor=${encodeURIComponent(lastCursor)}`, { kind: 'read', name });
  lastQuery = query;
  lastCursor = r.status === 200 ? r.json().next_cursor || '' : '';
  return r;
}

export function browse() {
  asMe();
  const roll = Math.random();
  let r;
  if (roll < 0.35) {
    const filters = Math.random() < 0.3 ? `&colors=${pick(COLORS)}` : '';
    lastCursor = '';
    r = searchPage(`limit=60&q=${encodeURIComponent(pick(TERMS))}${filters}`, 'search');
  } else if (roll < 0.55) {
    if (!lastCursor) lastQuery = `limit=60&sort=${pick(SORTS)}`;
    r = searchPage(lastQuery, lastCursor ? 'search next page' : 'search sorted');
  } else if (roll < 0.8) {
    r = get('/api/collection?cursor=&limit=60', { kind: 'read', name: 'collection' });
  } else if (roll < 0.9) {
    r = get('/api/collection/stats', { kind: 'read', name: 'collection stats' });
  } else {
    r = get('/api/session', { kind: 'read', name: 'session' });
  }
  check(r, { 'read ok': (x) => x.status === 200 });
  think();
}

export function logging(data) {
  asMe();
  const roll = Math.random();
  let r;
  if (roll < 0.75) {
    r = post('/api/collection', { card_id: pick(data.others), delta: 1 }, { kind: 'write', name: 'log +1' });
  } else if (roll < 0.9) {
    r = post('/api/collection', { card_id: pick(data.others), delta: -1 }, { kind: 'write', name: 'log -1' });
  } else {
    const cards = [];
    for (let i = 0; i < 8; i++) cards.push({ card_id: pick(data.others), qty: 1 + Math.floor(Math.random() * 3) });
    r = post('/api/collection/bulk', { cards }, { kind: 'write', name: 'log bulk' });
  }
  check(r, { 'write ok': (x) => x.status === 200 });
  think();
}

let deckId = null;
let deck = {};
export function decks(data) {
  asMe();
  if (deckId === null) {
    const r = post('/api/decks', { name: `load ${__VU}`, leader_card_id: pick(data.leaders) }, { kind: 'write', name: 'deck create' });
    if (!check(r, { 'deck created': (x) => x.status === 200 })) { think(); return; }
    deckId = r.json().deck.id;
    deck = {};
  }

  const roll = Math.random();
  const size = Object.values(deck).reduce((a, b) => a + b, 0);
  let r;
  if (roll < 0.6) {
    // Stay inside the 50-card limit: past 40, take a card out instead.
    const ids = Object.keys(deck);
    if (size > 40 && ids.length > 0) {
      const id = pick(ids);
      r = post(`/api/decks/${deckId}/card`, { card_id: id, qty: 0 }, { kind: 'write', name: 'deck set card' });
      if (r.status === 200) delete deck[id];
    } else {
      const id = pick(data.others);
      const qty = 1 + Math.floor(Math.random() * 4);
      r = post(`/api/decks/${deckId}/card`, { card_id: id, qty }, { kind: 'write', name: 'deck set card' });
      if (r.status === 200) deck[id] = qty;
    }
    check(r, { 'write ok': (x) => x.status === 200 });
  } else if (roll < 0.85) {
    r = get(`/api/decks/${deckId}`, { kind: 'read', name: 'deck detail' });
    check(r, { 'read ok': (x) => x.status === 200 });
  } else {
    r = get('/api/decks?cursor=&limit=20', { kind: 'read', name: 'deck list' });
    check(r, { 'read ok': (x) => x.status === 200 });
  }
  think();
}

export function images(data) {
  asMe();
  // A screenful of thumbnails at once, as the browser asks for them.
  const batch = [];
  for (let i = 0; i < 6; i++) {
    batch.push({
      method: 'GET',
      url: `${BASE}/img/${pick(data.art)}`,
      params: { tags: { kind: 'image', name: 'card art' }, responseCallback: http.expectedStatuses(200, 404) },
    });
  }
  http.batch(batch).forEach((r) => check(r, { 'image answered': (x) => x.status === 200 || x.status === 404 }));
  think();
}

export function scans() {
  asMe();
  const started = Date.now();
  const r = post('/api/scan', { variants: [SCAN_PNG] }, { kind: 'write', name: 'scan submit' });
  if (!check(r, { 'scan accepted': (x) => x.status === 200 || x.status === 202 })) { think(); return; }

  let status = r.status === 200 ? 'complete' : 'pending';
  const id = r.status === 202 ? r.json().scan_id : null;
  while (status === 'pending' && Date.now() - started < 30000) {
    sleep(0.25);
    const p = get(`/api/scan/${id}`, { kind: 'poll', name: 'scan poll' });
    status = p.status === 200 ? p.json().status : 'failed';
  }
  scanEndToEnd.add(Date.now() - started);
  check(status, { 'scan finished': (s) => s === 'complete' });
  think();
}

let contentionSignedIn = false;
export function contention(data) {
  if (!contentionSignedIn) {
    signIn(SHARED);
    contentionSignedIn = true;
  }
  const r = post('/api/collection', { card_id: data.contended, delta: 1 }, { kind: 'contention', name: 'contended +1' });
  if (check(r, { 'contended write ok': (x) => x.status === 200 })) contentionSent.add(1);
}

// ---- teardown: was every increment kept?

export function teardown(data) {
  signIn(SHARED);
  const r = get(`/api/card/${data.contended}`, { kind: 'setup' });
  const qty = r.status === 200 ? r.json().card.qty : -1;
  // Every iteration either landed (200) or failed its check; a 200 that did
  // not land is a lost increment.
  console.log(`contention: ${CONTENTION} increments sent, ${qty} recorded`);
  lostIncrements.add(Math.max(0, CONTENTION - qty));
  check(qty, { 'no increment lost': (q) => q === CONTENTION });
}
