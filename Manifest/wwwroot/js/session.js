/* Manifest: start-up - who is signed in, what this server can do, and the
   handling every card picture shares. Loaded last, once everything it calls exists. */

// Card art still being fetched answers 404 with Retry-After, so a picture that
// fails is asked for again - a little later each time, for about half a minute,
// which covers a worker working through a screenful of new art - before its frame
// is given up on. One listener for every <img>, in place of an onerror="" on each,
// which the page's Content-Security-Policy would refuse to run.
document.addEventListener('error', e => {
  const img = e.target;
  if (!(img instanceof HTMLImageElement)) return;
  img.style.visibility = 'hidden';
  // The path, not the attribute: after the first retry src is a full URL.
  const url = new URL(img.src || '', location.href);
  const tries = Number(img.dataset.tries || 0);
  if (tries >= 6 || !/^\/img\/./.test(url.pathname)) return;
  img.dataset.tries = tries + 1;
  setTimeout(() => {
    url.searchParams.set('try', tries + 1);
    img.src = url.toString();
  }, 1500 * (tries + 1));
}, true);
document.addEventListener('load', e => {
  if (e.target instanceof HTMLImageElement) e.target.style.visibility = '';
}, true);

// Who is signed in. The app is only ever served to an account, so this is for
// showing the name and offering the way out - not for deciding what to render.
(async () => {
  const s = await api('/api/session');
  if (!s.authenticated) { location.reload(); return; }
  $('#whoName').textContent = s.user.username;
  $('#who').hidden = false;
  $('#signOut').addEventListener('click', async () => {
    await fetch('/api/auth/logout', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: '{}',
    });
    location.reload();
  });
})().catch(()=>{});

(async () => {
  const h = await api('/api/health');
  USE_API = h.api_scanning && !h.ocr;
  const how = h.ocr ? 'reads card numbers on this machine, free'
            : h.api_scanning ? 'uses the API, which costs per scan'
            : 'off — install tesseract for free local scanning';
  $('#foot').textContent =
    `${h.catalog} printings in the local catalogue. Counts live in manifest.db on the ` +
    `machine running this. Scanning ${how}.`;
  if (!window.isSecureContext && h.scanning) {
    $('#finderMsg').innerHTML =
      'Tap <b>Photo</b> above to scan a card — it works on this connection right now. ' +
      'Zoom or move in close so just the bottom-right corner fills the frame; it reads ' +
      'far more reliably that way. <b>Live camera</b> needs a secure page; <a href="/setup" ' +
      'class="lnk">set that up once</a> only if you want the live viewfinder.';
  }
  if (!h.scanning) $('#finderMsg').innerHTML =
    'Scanning needs tesseract installed on the machine running this. ' +
    'See the README. Typing a number always works.';
  // The binder decides what every count means, so it is settled before any are drawn.
  // switchBinder redraws the totals, and anything opened before this landed.
  await loadBinders().catch(()=>{});
  if (BINDER !== null) switchBinder(BINDER);
  else refreshTotals().catch(()=>{});
  $('#q').focus();
})().catch(()=>{});
