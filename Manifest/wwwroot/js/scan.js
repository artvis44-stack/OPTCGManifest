/* Manifest: the Scan tab - camera, photo, corner crops and the read. */

/* ---------- scan tab ---------- */
const camConstraints = {
  // height, not width, is the *larger* dimension here — a card, and a
  // phone held normally to photograph one, are both portrait.
  video: {facingMode: {ideal: 'environment'}, height: {ideal: 1920}, aspectRatio: {ideal: 4/5}},
};

async function refocus() {
  if (!stream) return;
  const v = $('#video');
  const track = stream.getVideoTracks()[0];
  const caps = track.getCapabilities ? track.getCapabilities() : {};
  if (caps.focusMode && caps.focusMode.includes('single-shot')) {
    try { await track.applyConstraints({advanced: [{focusMode: 'single-shot'}]}); return; }
    catch (e) {}
  }
  // Most phone browsers don't actually expose focus control through
  // getCapabilities() — caps.focusMode comes back empty, so the "proper"
  // API above silently does nothing. The one lever that's still available
  // everywhere is that many phones re-run their own autofocus sweep when a
  // camera session first opens, even though nothing here can trigger that
  // sweep directly — so closing and reopening the stream is the fallback.
  say('Restarting the camera to refocus…');
  stream.getTracks().forEach(t => t.stop());
  try {
    stream = await navigator.mediaDevices.getUserMedia(camConstraints);
    v.srcObject = stream; await v.play();
    say('If the number still looks blurry, tap the preview again.');
  } catch (e) {
    say('Could not restart the camera.', true);
  }
}

$('#btnCam').onclick = async () => {
  try {
    stream = await navigator.mediaDevices.getUserMedia(camConstraints);
    const v = $('#video');
    v.srcObject = stream; await v.play();
    v.hidden = false; $('#finderMsg').hidden = true; $('#btnShoot').disabled = false;

    // The number is read from close up, well inside the distance most phone
    // cameras focus at by default for a live preview — some devices lock
    // focus on whatever was in frame when the stream opened and never
    // refocus after that. Ask for continuous autofocus if the device
    // exposes it, on top of the tap-to-refocus handler below.
    const track = stream.getVideoTracks()[0];
    const caps = track.getCapabilities ? track.getCapabilities() : {};
    if (caps.focusMode && caps.focusMode.includes('continuous')) {
      track.applyConstraints({advanced: [{focusMode: 'continuous'}]}).catch(() => {});
    }
    v.onclick = () => { refocus().catch(() => {}); };

    say('If the number looks blurry, tap the preview to refocus.');
  } catch (e) {
    if (!window.isSecureContext) {
      $('#finderMsg').innerHTML =
        'Live camera needs a secure page. <a href="/setup" class="lnk">' +
        'Set that up once</a> if you want it — or just use <b>Photo</b> above, which ' +
        'scans exactly the same and already works.';
      $('#finderMsg').hidden = false;
      say('Live camera needs HTTPS. Photo works right now instead.', true);
    } else {
      say('Camera permission was refused. Allow it in your browser settings, or use Photo.', true);
    }
  }
};
function stopCam() {
  if (stream) { stream.getTracks().forEach(t => t.stop()); stream = null; }
  $('#video').onclick = null;
  $('#video').hidden = true; $('#finderMsg').hidden = false; $('#btnShoot').disabled = true;
}
$('#btnShoot').onclick = async () => {
  const v = $('#video');
  if (!v.videoWidth) return say('Camera not ready.', true);
  // Freeze on the frame you actually meant to capture — otherwise the
  // preview keeps moving under your hand while it reads, so what gets
  // analysed is never quite the frame you were looking at when you tapped.
  v.pause();
  $('#btnShoot').disabled = true;
  try {
    // The crop math has to compensate for object-fit:cover cropping the
    // native camera frame down to what's actually on screen — but that
    // only works if it knows the box's real on-screen shape. This used to
    // assume the CSS aspect-ratio:5/4 on .finder had actually taken effect,
    // which isn't true on every browser; when it doesn't, the box renders
    // at some other shape (often whatever the raw camera stream is) and
    // every crop lands off-target. Measuring the video element's actual
    // rendered box is correct regardless of whether that CSS applied.
    const box = v.getBoundingClientRect();
    const containerAspect = box.width / box.height;
    await send(shrink(v, v.videoWidth, v.videoHeight),
               cornerVariants(v, v.videoWidth, v.videoHeight, containerAspect));
  } finally {
    $('#btnShoot').disabled = false;
    if (stream) v.play().catch(() => {});
  }
};
$('#btnFile').onclick = () => $('#file').click();
$('#file').onchange = e => {
  const f = e.target.files[0]; if (!f) return;
  const rd = new FileReader();
  rd.onload = () => { const i = new Image();
    i.onload = () => send(shrink(i, i.width, i.height),
                          cornerVariants(i, i.width, i.height)); i.src = rd.result; };
  rd.readAsDataURL(f); e.target.value = '';
};
function shrink(src, w, h) {
  const s = Math.min(1, 1200 / Math.max(w, h));
  const cv = document.createElement('canvas');
  cv.width = w*s|0; cv.height = h*s|0;
  cv.getContext('2d').drawImage(src, 0, 0, cv.width, cv.height);
  return cv.toDataURL('image/jpeg', .85).split(',')[1];
}

/* The card number sits in the bottom-right corner. Crop just that, blow it up,
   flatten it to grey and stretch the contrast — then hand the server a few
   versions so its OCR gets more than one shot. All of this runs here, so
   scanning costs nothing and works with no internet. */
function cornerVariants(src, w, h, containerAspect) {
  const GX = 0.60, GY = 0.90, GW = 0.30, GH = 0.07;   // matches the dashed box

  // The live preview is shown with object-fit:cover inside a fixed-aspect
  // box, which crops the native camera frame before the user ever sees it.
  // Apply that same crop here first, or the guide box on screen and the
  // region we actually read drift apart on any camera that isn't 5:4 native
  // (almost all of them). Not needed for an uploaded photo — that has no
  // container to be cropped against.
  let vx = 0, vy = 0, vw = w, vh = h;
  if (containerAspect) {
    const nativeAspect = w / h;
    if (nativeAspect > containerAspect) { vw = h * containerAspect; vx = (w - vw) / 2; }
    else                                { vh = w / containerAspect; vy = (h - vh) / 2; }
  }

  const boxAt = (cx0, cy0, cw, ch) =>
    ({sx: cx0 + cw*GX, sy: cy0 + ch*GY, sw: cw*GW, sh: ch*GH});

  let regions;
  if (containerAspect) {
    // Live camera has a dashed box the user lines the number up against,
    // but a hand-held phone rarely hits it pixel-perfect. Try the exact box
    // first, then a second, generously padded version of the same box as a
    // safety net for a near miss, instead of demanding perfect alignment.
    const exact = boxAt(vx, vy, vw, vh);
    const pad = 0.35;
    const padded = {
      sx: exact.sx - exact.sw*pad, sy: exact.sy - exact.sh*pad,
      sw: exact.sw*(1 + 2*pad),    sh: exact.sh*(1 + 2*pad),
    };
    // 'min' and 'chroma' are complementary, not interchangeable — light
    // text needs one and dark text needs the other (see the comment where
    // they're computed below). Splitting each region's attempts across
    // both, rather than committing a region to just one, covers either
    // colour of ink within the same fixed number of tries.
    regions = [
      {...exact,  configs: [[3,null,'min'],[4,null,'chroma'],[4,150,'min'],[3,140,'chroma']]},
      {...padded, configs: [[4,null,'min'],[4,150,'chroma']]},
    ];
  } else {
    // An uploaded photo has no on-screen guide: the card might fill the
    // whole frame if it was zoomed in as instructed, or it might be a
    // normal, more distant shot of the whole card with background around
    // it. Rather than assume one, try a few plausible sizes for how much
    // of the frame the card actually fills.
    regions = [1, 0.65, 0.42].map(fill => {
      const cw = vw*fill, ch = vh*fill;
      return {...boxAt(vx + (vw-cw)/2, vy + (vh-ch)/2, cw, ch),
              configs: [[4,null,'min'],[4,150,'chroma']]};
    });
  }

  const out = [];
  for (const {sx, sy, sw, sh, configs} of regions) {
    for (const [scale, thr, channel] of configs) {
    const cv = document.createElement('canvas');
    cv.width  = Math.min(2000, sw*scale|0);
    cv.height = Math.min(700,  sh*scale|0);
    const cx = cv.getContext('2d');
    cx.imageSmoothingEnabled = true;
    cx.imageSmoothingQuality = 'high';
    cx.drawImage(src, sx, sy, sw, sh, 0, 0, cv.width, cv.height);

    const d = cx.getImageData(0, 0, cv.width, cv.height);
    const p = d.data;
    // The card's background isn't flat — it's a fine dot/halftone texture —
    // so standard luminance (.299R+.587G+.114B) is no good here: it tracks
    // that texture right along with the text, because a saturated
    // background is still bright in whichever channel matches its own hue
    // (e.g. the red channel on a red card), and the dots survive the
    // contrast stretch as noise and bury the text under it. Most cards
    // print the number in white ink, which — unlike any single-hue
    // background — is bright in all three channels at once, so the
    // *minimum* of R,G,B is high only on the text. But some cards (yellow
    // ones especially) print it in black ink instead, which is dark in
    // every channel just like that background's weak channel is, so min()
    // can't tell them apart there — what actually separates black OR white
    // ink from a saturated background in that case is that ink is neutral
    // (R≈G≈B) while a card colour never is, so 255 minus the spread
    // between the channels (its "chroma") is high for either colour of
    // ink and low across the background regardless of its hue.
    for (let i = 0; i < p.length; i += 4) {
      const r = p[i], gr = p[i+1], b = p[i+2];
      const g = channel === 'chroma'
        ? 255 - (Math.max(r,gr,b) - Math.min(r,gr,b))
        : Math.min(r, gr, b);
      p[i] = p[i+1] = p[i+2] = g;
    }
    cx.putImageData(d, 0, 0);

    // A real photo is rarely lit evenly — a flash reflection or an angled
    // shadow makes one end of the crop brighter than the other. A single
    // global threshold then does the wrong thing on whichever end is off:
    // it either crushes dim-but-legible text to black or blows bright text
    // out to white. A blurred copy of the crop subtracted back out fixes
    // that in principle (the standard "flat-field" trick), but blur is a
    // trap here in practice: CSS/canvas `filter: blur()` quality varies a
    // lot across phone GPUs and can ring at the radius this needs, and a
    // plain scaled drawImage — even at 'high' smoothing quality — isn't
    // guaranteed to average every source pixel, so a regular texture
    // (a halftone dot pattern, fabric weave) can alias straight through
    // instead of blurring away. A least-squares plane fit sidesteps all of
    // that: it has only three degrees of freedom (tilt in x, tilt in y, and
    // an overall level), so there is no texture fine enough for it to
    // pick up — it can only ever describe the kind of slow, one-sided
    // brightness trend a flash or shadow actually produces.
    const xbar = (cv.width - 1) / 2, ybar = (cv.height - 1) / 2;
    let sxz = 0, syz = 0, sxx = 0, syy = 0;
    for (let y = 0, i = 0; y < cv.height; y++) {
      const yp = y - ybar;
      for (let x = 0; x < cv.width; x++, i += 4) {
        const xp = x - xbar, z = p[i];
        sxz += xp * z; syz += yp * z;
        sxx += xp * xp; syy += yp * yp;
      }
    }
    const a = sxx ? sxz / sxx : 0, bTilt = syy ? syz / syy : 0;

    let lo = 255, hi = 0;
    for (let y = 0, i = 0; y < cv.height; y++) {
      const yp = y - ybar;
      for (let x = 0; x < cv.width; x++, i += 4) {
        const xp = x - xbar;
        const g = Math.max(0, Math.min(255, p[i] - (a*xp + bTilt*yp)));
        p[i] = g;
        if (g < lo) lo = g;
        if (g > hi) hi = g;
      }
    }
    const span = Math.max(1, hi - lo);
    for (let i = 0; i < p.length; i += 4) {
      let g = ((p[i] - lo) * 255 / span) | 0;
      if (thr !== null) g = g > thr ? 255 : 0;
      p[i] = p[i+1] = p[i+2] = g; p[i+3] = 255;
    }
    cx.putImageData(d, 0, 0);
    out.push(cv.toDataURL('image/png').split(',')[1]);
    }
  }
  return out;
}

async function send(b64, variants) {
  say('Reading…'); $('#hit').innerHTML = '';
  const payload = {variants};
  if (USE_API) payload.image = b64;      // only sent when the API is the engine
  let r = await api('/api/scan', {method:'POST',
    headers:{'content-type':'application/json'}, body: JSON.stringify(payload)});
  // A server reading scans in the background answers with a ticket instead of a
  // reading; ask after it until the worker has got to it.
  if (r.scan_id) r = await awaitScan(r.scan_id);
  if (!r) return say('That took too long. Try again, or type the number.', true);
  if (!r.ok) return say(r.message || 'No read.', true);
  say('');
  pending = r; pendQty = 1;
  const c = r.card || {card_id:r.card_id, name:'Not in catalogue'};
  // An alt art prints the same number as its base card, so the number alone
  // reads as the base printing; the print strip below lets you say which it is.
  $('#hit').innerHTML = `<div class="card-hit">
    <img alt="" id="hitImg" src="/img/${encodeURIComponent(pending.card_id)}">
    <div>
      <span class="cid" id="hitCid">${esc(pending.card_id)}</span>
      <h3>${esc(c.name || 'Not in catalogue')}</h3>
      <div class="sm" id="hitSub">${esc([c.set_label,c.rarity,c.variant].filter(Boolean).join(' · '))}</div>
      <div id="hitPrints"></div>
      ${(r.confidence==='low'||r.note) ? `<div class="warn">${esc(r.note||'Low confidence — check the number.')}</div>`:''}
      ${!r.in_catalog ? `<div class="warn">Not in the catalogue. It will still be counted under this number.</div>`:''}
      <div class="act">
        <button class="stepper" id="mns">−</button>
        <span class="n" id="pn">1</span>
        <button class="stepper" id="pls">+</button>
        <button id="logit">Log it</button>
      </div>
    </div></div>`;
  if (r.card) showScanPrints(pending);
  $('#mns').onclick = () => { pendQty = Math.max(1,pendQty-1); $('#pn').textContent = pendQty; };
  $('#pls').onclick = () => { pendQty++; $('#pn').textContent = pendQty; };
  $('#logit').onclick = async () => {
    const res = await api('/api/collection', {method:'POST',
      headers:{'content-type':'application/json'},
      body: JSON.stringify({card_id: pending.card_id, delta: pendQty})});
    $('#hit').innerHTML = '';
    say(`${res.card_id} logged — ${res.qty} total. Next card.`);
    refreshTotals();
  };
}
function showScanPrints(hit) {
  printsOf(hit.card_id).then(prints => {
    const slot = $('#hitPrints');
    if (!slot || pending !== hit) return;
    slot.innerHTML = printStripHTML(prints, hit.card_id);
    wirePrintStrip(slot, id => {
      const p = prints.find(x => x.card_id === id);
      if (!p) return;
      hit.card_id = id;
      $('#hitImg').src = '/img/' + encodeURIComponent(id);
      $('#hitCid').textContent = id;
      $('#hitSub').textContent = [p.set_label, p.rarity, p.variant].filter(Boolean).join(' · ');
    });
  }).catch(() => {});
}
async function awaitScan(id) {
  const until = Date.now() + 45000;
  for (let wait = 300; Date.now() < until; wait = Math.min(wait * 1.5, 1500)) {
    await new Promise(done => setTimeout(done, wait));
    const s = await api('/api/scan/' + id);
    if (s.status === 'complete') return s.result;
    if (s.status === 'failed') return {ok: false, message: s.error};
  }
  return null;
}
function say(m, err) {
  const s = $('#status'); if (!s) return;
  s.textContent = m; s.className = 'status' + (err ? ' err' : '');
}
