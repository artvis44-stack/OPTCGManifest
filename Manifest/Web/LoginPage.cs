namespace Manifest.Web;

/// <summary>
/// What a stranger gets at "/". Kept apart from ui.html rather than folded into it
/// as an overlay: the app is one 100KB file that assumes it may call the API freely,
/// and handing that to someone with no account would mean auditing every call for
/// what it leaks. A separate page has nothing in it to leak.
/// </summary>
public static class LoginPage
{
    public const string Html = """
<!doctype html><meta charset=utf-8>
<meta name=viewport content="width=device-width,initial-scale=1">
<title>Manifest — sign in</title>
<style>
:root{
  --abyss:#03121A; --deep:#07202C; --panel:rgba(21,69,90,.42);
  --edge:rgba(240,232,214,.13); --edge-hi:rgba(240,232,214,.28);
  --gold:#E3A63A; --gold-hi:#F7D384; --red:#C8403A; --red-hi:#E8776C;
  --text:#EFE7D6; --dim:rgba(239,231,214,.58); --faint:rgba(239,231,214,.34);
  --mono:ui-monospace,"SF Mono",Menlo,monospace;
  --sans:system-ui,-apple-system,"Segoe UI",Roboto,sans-serif;
}
*{box-sizing:border-box}
html,body{margin:0;height:100%}
body{background:radial-gradient(120% 90% at 50% 0%,var(--deep),var(--abyss) 70%);
  color:var(--text);font-family:var(--sans);display:flex;align-items:center;
  justify-content:center;padding:24px}
.card{width:100%;max-width:390px}
.brand{display:flex;align-items:center;gap:13px;margin-bottom:26px}
.brand svg{width:52px;height:35px;flex:none}
h1{font-size:23px;margin:0;letter-spacing:.02em}
.sub{font-size:11.5px;letter-spacing:.16em;text-transform:uppercase;color:var(--faint);
  margin-top:3px}
form{background:var(--panel);border:1px solid var(--edge);border-radius:14px;
  padding:22px;backdrop-filter:blur(7px)}
h2{font-size:12px;letter-spacing:.18em;text-transform:uppercase;color:var(--gold);
  margin:0 0 16px}
label{display:block;font-size:12px;color:var(--dim);margin:14px 0 5px}
label:first-of-type{margin-top:0}
input{width:100%;padding:12px 13px;border-radius:9px;background:rgba(3,18,26,.6);
  border:1px solid var(--edge);color:var(--text);font-size:16px;font-family:var(--sans)}
input:focus{outline:none;border-color:var(--gold);
  box-shadow:0 0 0 1px rgba(227,166,58,.5)}
button{width:100%;margin-top:20px;padding:13px;border:0;border-radius:9px;
  background:var(--gold);color:#071A20;font-size:15px;font-weight:700;cursor:pointer;
  font-family:var(--sans)}
button:hover{background:var(--gold-hi)}
button:disabled{opacity:.55;cursor:default}
.msg{margin-top:15px;font-size:13.5px;line-height:1.5;padding:10px 12px;border-radius:8px;
  display:none}
.msg.bad{display:block;background:rgba(200,64,58,.16);border:1px solid rgba(200,64,58,.45);
  color:var(--red-hi)}
.msg.good{display:block;background:rgba(103,192,178,.14);
  border:1px solid rgba(103,192,178,.4);color:#8FD8CB}
.swap{margin-top:18px;text-align:center;font-size:13px;color:var(--dim)}
.swap button{width:auto;margin:0;padding:0;background:none;color:var(--gold);
  font-weight:600;font-size:13px;text-decoration:underline;cursor:pointer}
.swap button:hover{background:none;color:var(--gold-hi)}
.hint{font-size:11.5px;color:var(--faint);margin-top:6px;line-height:1.45}
.foot{margin-top:24px;text-align:center;font-size:11px;color:var(--faint);
  font-family:var(--mono)}
[hidden]{display:none!important}
</style>

<div class=card>
  <div class=brand>
    <svg viewBox="0 0 48 32" aria-hidden=true>
      <ellipse cx=24 cy=24.5 rx=22 ry=6.6 fill="#D9BA72"/>
      <ellipse cx=24 cy=23.2 rx=22 ry=6.6 fill="#F0D793"/>
      <path d="M9 24 C9 9 16 3 24 3 C32 3 39 9 39 24 Z" fill="#EFD189"/>
      <path d="M9 24 C9 9 16 3 24 3 C28 3 24 10 24 24 Z" fill="#F7E3AE"/>
      <path d="M9.4 20.4 C16 17.6 32 17.6 38.6 20.4 L38.2 24.2 C32 21.2 16 21.2 9.8 24.2 Z"
            fill="#C8403A"/>
    </svg>
    <div>
      <h1>Manifest</h1>
      <div class=sub>One Piece TCG</div>
    </div>
  </div>

  <form id=form autocomplete=on>
    <h2 id=heading>Sign in</h2>

    <label for=username>Username</label>
    <input id=username name=username autocomplete=username autocapitalize=none
           autocorrect=off spellcheck=false required>

    <label for=password>Password</label>
    <input id=password name=password type=password autocomplete=current-password required>
    <div class=hint id=passwordHint hidden>At least 10 characters. A phrase you will
      remember beats a short scramble.</div>

    <div id=inviteWrap hidden>
      <label for=invite>Invite code</label>
      <input id=invite name=invite autocomplete=off autocapitalize=none autocorrect=off
             spellcheck=false>
      <div class=hint>From whoever runs this server.</div>
    </div>

    <button id=submit type=submit>Sign in</button>
    <div class=msg id=msg role=status></div>

    <div class=swap id=swapWrap hidden>
      <span id=swapText>No account yet?</span>
      <button type=button id=swap>Create one</button>
    </div>
  </form>

  <div class=foot>your cards, your database</div>
</div>

<script>
const $ = id => document.getElementById(id);
let mode = 'login';            // or 'register'
let canRegister = false;

// The token from an approved access request arrives in the link, not from the
// person - they were mailed a URL and have nothing to type. Reading it here is
// what makes an email-only server work: with no shared code configured the server
// reports registration as closed, and the link is then the only thing saying this
// particular stranger is allowed an account. The page takes it as `invite` and the
// API takes it as `t`, the same name /api/access/review uses; bridging the two
// here keeps links that have already been mailed out working.
const linkToken = new URLSearchParams(location.search).get('invite') || '';

function show(text, ok) {
  $('msg').textContent = text;
  $('msg').className = 'msg ' + (ok ? 'good' : 'bad');
}

function setMode(next) {
  mode = next;
  const registering = mode === 'register';
  $('heading').textContent = registering ? 'Create an account' : 'Sign in';
  $('submit').textContent  = registering ? 'Create account' : 'Sign in';
  $('swapText').textContent = registering ? 'Already have one?' : 'No account yet?';
  $('swap').textContent = registering ? 'Sign in' : 'Create one';
  // Nothing to type when the invite came from the link, so the field stays out of
  // the way rather than sitting there pre-filled with 256 bits of noise.
  $('inviteWrap').hidden = !registering || linkToken !== '';
  $('passwordHint').hidden = !registering;
  $('password').setAttribute('autocomplete', registering ? 'new-password' : 'current-password');
  $('msg').className = 'msg';
}

$('swap').addEventListener('click', () => setMode(mode === 'login' ? 'register' : 'login'));

// Settled before anything async runs, so the message the invite lookup puts up is
// not wiped by a setMode that lands after it.
if (linkToken !== '') setMode('register');

// Whether accounts can be created at all is the server's business - it depends on
// whether an invite code was configured, or on holding a link - so the form asks
// rather than assumes.
fetch('/api/session', { headers: { 'Accept': 'application/json' } })
  .then(r => r.json())
  .then(s => {
    if (s.authenticated) { location.replace('/'); return; }
    // A server with no shared code reports registration closed, which is the right
    // answer for anyone who just found the page and the wrong one for someone
    // holding a link that was minted for them.
    canRegister = s.registration === 'invite' || linkToken !== '';
    $('swapWrap').hidden = !canRegister;
    // The link is a statement about one person, so the mode and the message are
    // already its business - the first-account note would only talk over it.
    if (linkToken !== '') return;
    if (canRegister && s.users === 0) {
      setMode('register');
      show('No accounts yet. The first one created becomes the owner and inherits '
           + 'any collection already in the database.', true);
    }
  })
  .catch(() => {});

// Says who the invite is for before anything is typed, and fails a spent or expired
// link here rather than after a username and a password have been chosen.
if (linkToken !== '') {
  fetch('/api/access/invite?t=' + encodeURIComponent(linkToken),
        { headers: { 'Accept': 'application/json' } })
    .then(async r => {
      const data = await r.json().catch(() => ({}));
      if (r.ok) {
        show('This invite is for ' + data.email + '. Choose a username and a '
             + 'password and the account is yours.', true);
      } else {
        show(data.error || 'That invite link is not valid any more.', false);
      }
    })
    .catch(() => {});
}

$('form').addEventListener('submit', async e => {
  e.preventDefault();
  const button = $('submit');
  button.disabled = true;
  $('msg').className = 'msg';

  const path = mode === 'register' ? '/api/auth/register' : '/api/auth/login';
  const body = {
    username: $('username').value.trim(),
    password: $('password').value,
  };
  if (mode === 'register') {
    body.invite = linkToken !== '' ? linkToken : $('invite').value.trim();
  }

  try {
    const r = await fetch(path, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
    const data = await r.json().catch(() => ({}));
    if (r.ok) { location.replace('/'); return; }
    show(data.error || ('Something went wrong (HTTP ' + r.status + ').'), false);
  } catch (err) {
    show('Could not reach the server. Is it still running?', false);
  } finally {
    button.disabled = false;
  }
});

$('username').focus();
</script>
""";
}
