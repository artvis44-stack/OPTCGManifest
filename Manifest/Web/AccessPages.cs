namespace Manifest.Web;

/// <summary>
/// The pages the access flow needs that are not the app and not the sign-in form:
/// the one an approve link opens, and the one the owner manages requests from.
/// Written the same way LoginPage is - one file, inline style, no build step -
/// because the alternative is threading two more pages through a bundler for a tool
/// whose whole appeal is that it is a single binary and a database file.
/// </summary>
public static class AccessPages
{
    /// <summary>Shared with LoginPage, kept here so the three pages cannot drift.</summary>
    const string Style = """
<meta charset=utf-8>
<meta name=viewport content="width=device-width,initial-scale=1">
<style>
:root{
  --abyss:#03121A; --deep:#07202C; --panel:rgba(21,69,90,.42);
  --edge:rgba(240,232,214,.13); --gold:#E3A63A; --gold-hi:#F7D384;
  --red:#C8403A; --red-hi:#E8776C; --green:#67C0B2;
  --text:#EFE7D6; --dim:rgba(239,231,214,.58); --faint:rgba(239,231,214,.34);
  --mono:ui-monospace,"SF Mono",Menlo,monospace;
  --sans:system-ui,-apple-system,"Segoe UI",Roboto,sans-serif;
}
*{box-sizing:border-box}
html,body{margin:0;min-height:100%}
body{background:radial-gradient(120% 90% at 50% 0%,var(--deep),var(--abyss) 70%);
  background-attachment:fixed;color:var(--text);font-family:var(--sans);padding:24px}
.wrap{max-width:720px;margin:0 auto}
.brand{display:flex;align-items:center;gap:13px;margin:8px 0 26px}
.brand svg{width:46px;height:31px;flex:none}
h1{font-size:21px;margin:0;letter-spacing:.02em}
.sub{font-size:11px;letter-spacing:.16em;text-transform:uppercase;color:var(--faint);margin-top:3px}
.panel{background:var(--panel);border:1px solid var(--edge);border-radius:14px;
  padding:20px;backdrop-filter:blur(7px);margin-bottom:14px}
h2{font-size:12px;letter-spacing:.18em;text-transform:uppercase;color:var(--gold);margin:0 0 14px}
.who{font-size:19px;font-weight:600;word-break:break-all;margin-bottom:4px}
.meta{font-size:12px;color:var(--faint);font-family:var(--mono)}
.meta.lead{margin-top:13px;line-height:1.6}
.meta.under{margin-top:5px}
.note{margin:14px 0 0;padding:11px 13px;border-left:3px solid var(--gold);
  background:rgba(3,18,26,.45);border-radius:0 8px 8px 0;font-size:14px;
  line-height:1.55;white-space:pre-wrap;word-break:break-word}
.row{display:flex;gap:10px;margin-top:20px;flex-wrap:wrap}
button{flex:1;min-width:130px;padding:12px 14px;border:0;border-radius:9px;font-size:14.5px;
  font-weight:700;cursor:pointer;font-family:var(--sans)}
button.yes{background:var(--gold);color:#071A20}
button.yes:hover{background:var(--gold-hi)}
button.no{background:transparent;color:var(--red-hi);border:1px solid rgba(200,64,58,.5)}
button.no:hover{background:rgba(200,64,58,.14)}
button.small{flex:none;min-width:0;padding:7px 13px;font-size:12.5px;font-weight:600}
button:disabled{opacity:.45;cursor:default}
.msg{margin-top:16px;font-size:13.5px;line-height:1.55;padding:11px 13px;border-radius:8px;display:none}
.msg.bad{display:block;background:rgba(200,64,58,.16);border:1px solid rgba(200,64,58,.45);color:var(--red-hi)}
.msg.good{display:block;background:rgba(103,192,178,.14);border:1px solid rgba(103,192,178,.4);color:#8FD8CB}
.msg.warn{display:block;background:rgba(227,166,58,.13);border:1px solid rgba(227,166,58,.4);color:var(--gold-hi)}
.tag{display:inline-block;padding:2px 9px;border-radius:20px;font-size:10.5px;font-weight:700;
  letter-spacing:.1em;text-transform:uppercase;font-family:var(--sans)}
.tag.pending{background:rgba(227,166,58,.18);color:var(--gold-hi)}
.tag.approved{background:rgba(103,192,178,.16);color:#8FD8CB}
.tag.denied{background:rgba(200,64,58,.16);color:var(--red-hi)}
.tag.used{background:rgba(239,231,214,.12);color:var(--dim)}
.item{border-top:1px solid var(--edge);padding:15px 0}
.item:first-of-type{border-top:0;padding-top:0}
.item .top{display:flex;align-items:center;gap:10px;flex-wrap:wrap}
.item .addr{font-weight:600;word-break:break-all;flex:1;min-width:180px}
.empty{color:var(--faint);font-size:14px;padding:6px 0}
.foot{margin:22px 0 8px;text-align:center;font-size:11px;color:var(--faint);font-family:var(--mono)}
a{color:var(--gold)}
[hidden]{display:none!important}
</style>
""";

    const string Brand = """
  <div class=brand>
    <svg viewBox="0 0 48 32" aria-hidden=true>
      <ellipse cx=24 cy=24.5 rx=22 ry=6.6 fill="#D9BA72"/>
      <ellipse cx=24 cy=23.2 rx=22 ry=6.6 fill="#F0D793"/>
      <path d="M9 24 C9 9 16 3 24 3 C32 3 39 9 39 24 Z" fill="#EFD189"/>
      <path d="M9 24 C9 9 16 3 24 3 C28 3 24 10 24 24 Z" fill="#F7E3AE"/>
      <path d="M9.4 20.4 C16 17.6 32 17.6 38.6 20.4 L38.2 24.2 C32 21.2 16 21.2 9.8 24.2 Z"
            fill="#C8403A"/>
    </svg>
    <div><h1>Manifest</h1><div class=sub>Access requests</div></div>
  </div>
""";

    /// <summary>
    /// What the link in the admin's mail opens. Loading it decides nothing: mail
    /// clients and corporate link scanners fetch every URL in a message before a
    /// person has seen it, and a one-click approve URL would hand access to whoever
    /// ran the scanner. The buttons here POST, which no scanner does.
    /// </summary>
    public const string Review = Style + """
<title>Manifest — review a request</title>
<div class=wrap>
""" + Brand + """
  <div class=panel id=panel>
    <h2>Someone is asking for an account</h2>
    <div id=loading class=empty>Looking that request up…</div>

    <div id=detail hidden>
      <div class=who id=email></div>
      <div class=meta id=when></div>
      <div class=note id=note hidden></div>

      <div class=row>
        <button class=yes id=approve>Approve — send them a sign-up link</button>
        <button class=no id=deny>Deny</button>
      </div>
      <div class="meta lead">
        Approving emails a link that makes one account and then stops working.<br>
        Denying is silent — they are not told either way.
      </div>
    </div>

    <div class=msg id=msg role=status></div>
  </div>
  <div class=foot>your cards, your database</div>
</div>

<script>
const $ = id => document.getElementById(id);
const token = new URLSearchParams(location.search).get('t') || '';

function show(text, kind) {
  $('msg').textContent = text;
  $('msg').className = 'msg ' + kind;
}

async function load() {
  if (!token) {
    $('loading').hidden = true;
    show('That link is missing its token. Open the link from the email exactly as it '
       + 'arrived, or sign in and use /admin instead.', 'bad');
    return;
  }
  try {
    const r = await fetch('/api/access/review?t=' + encodeURIComponent(token),
                          { headers: { 'Accept': 'application/json' } });
    const data = await r.json().catch(() => ({}));
    $('loading').hidden = true;
    if (!r.ok) { show(data.error || 'That link is not valid any more.', 'bad'); return; }

    const req = data.request;
    $('email').textContent = req.email;
    $('when').textContent = 'asked ' + req.created_at + ' UTC · request #' + req.id;
    if (req.note) { $('note').textContent = req.note; $('note').hidden = false; }
    $('detail').hidden = false;
  } catch (e) {
    $('loading').hidden = true;
    show('Could not reach the server.', 'bad');
  }
}

async function decide(decision) {
  $('approve').disabled = true;
  $('deny').disabled = true;
  try {
    const r = await fetch('/api/access/decide', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ token, decision }),
    });
    const data = await r.json().catch(() => ({}));
    if (!r.ok) {
      show(data.error || 'That did not work.', 'bad');
      $('approve').disabled = false;
      $('deny').disabled = false;
      return;
    }
    $('detail').hidden = true;
    if (data.decision === 'denied') {
      show('Denied. ' + data.email + ' has not been told, and the link in your email '
         + 'no longer works.', 'good');
    } else if (data.mail === 'sent' || data.mail === 'queued') {
      show('Approved. A sign-up link is on its way to ' + data.email + '.', 'good');
    } else if (data.mail === 'logged') {
      show('Approved. No SMTP is configured on this server, so the sign-up link was '
         + 'printed to the server console instead — copy it from there.', 'warn');
    } else {
      show('Approved, but the email could not be sent. Open /admin and press Send '
         + 'again once mail is working.', 'warn');
    }
  } catch (e) {
    show('Could not reach the server.', 'bad');
    $('approve').disabled = false;
    $('deny').disabled = false;
  }
}

$('approve').addEventListener('click', () => decide('approve'));
$('deny').addEventListener('click', () => decide('deny'));
load();
</script>
""";

    /// <summary>
    /// The owner's list. The way through when mail is broken, which is the moment it
    /// matters most - so it shows what the mail settings are as well as the requests,
    /// rather than leaving someone to guess why nothing arrived.
    /// </summary>
    public const string Admin = Style + """
<title>Manifest — access requests</title>
<div class=wrap>
""" + Brand + """
  <div class=msg id=mail role=status></div>

  <div class=panel>
    <h2>Waiting on you <span id=pendingCount class=tag></span></h2>
    <div id=pending><div class=empty>Loading…</div></div>
  </div>

  <div class=panel>
    <h2>Everything else</h2>
    <div id=decided><div class=empty>Loading…</div></div>
  </div>

  <div class=foot><a href="/">back to the app</a></div>
</div>

<script>
const $ = id => document.getElementById(id);
let busy = false;

function tag(r) {
  if (r.used) return '<span class="tag used">account made</span>';
  if (r.status === 'pending') return '<span class="tag pending">pending</span>';
  if (r.status === 'denied') return '<span class="tag denied">denied</span>';
  if (r.invite_expired) return '<span class="tag denied">link expired</span>';
  return '<span class="tag approved">approved</span>';
}

const escape = s => String(s == null ? '' : s).replace(/[&<>"']/g,
  c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

function render(r) {
  const lines = [];
  lines.push('asked ' + escape(r.created_at) + ' UTC · #' + r.id);
  if (r.decided_at) lines.push('decided ' + escape(r.decided_at) + ' by ' + escape(r.decided_by));
  if (r.status === 'approved' && !r.used && r.invite_expires_at)
    lines.push('link good until ' + escape(r.invite_expires_at) + ' UTC');

  // Approve doubles as "send another link", which is what an expired or lost invite
  // needs. Nothing is offered once an account exists: there is nothing left to send.
  const buttons = r.used ? '' :
    '<div class=row>'
    + '<button class="yes small" data-do=approve data-id=' + r.id + '>'
    + (r.status === 'approved' ? 'Send the link again' : 'Approve') + '</button>'
    + (r.status === 'denied' ? '' :
       '<button class="no small" data-do=deny data-id=' + r.id + '>Deny</button>')
    + '</div>';

  return '<div class=item>'
       + '<div class=top><span class=addr>' + escape(r.email) + '</span>' + tag(r) + '</div>'
       + '<div class="meta under">' + lines.join(' · ') + '</div>'
       + (r.note ? '<div class=note>' + escape(r.note) + '</div>' : '')
       + buttons
       + '</div>';
}

function mailNote(mail) {
  if (mail.configured) return null;
  if (mail.missing)
    return ['warn', 'No email is going out: ' + mail.missing + ' is not set. Approvals '
          + 'still work — the sign-up link is printed to the server console instead.'];
  return ['warn', 'No SMTP is configured. Approvals still work; the sign-up link is '
        + 'printed to the server console.'];
}

async function load() {
  const r = await fetch('/api/access/requests', { headers: { 'Accept': 'application/json' } });
  if (r.status === 401) { location.replace('/'); return; }
  const data = await r.json().catch(() => ({}));
  if (!r.ok) {
    $('pending').innerHTML = '<div class=empty>' + escape(data.error || 'Could not load.') + '</div>';
    $('decided').innerHTML = '';
    return;
  }

  const note = mailNote(data.mail || {});
  if (note) { $('mail').textContent = note[1]; $('mail').className = 'msg ' + note[0]; }

  const all = data.requests || [];
  const pending = all.filter(r => r.status === 'pending');
  const rest = all.filter(r => r.status !== 'pending');

  $('pendingCount').textContent = pending.length ? pending.length : '';
  $('pendingCount').className = pending.length ? 'tag pending' : 'tag';
  $('pending').innerHTML = pending.length
    ? pending.map(render).join('')
    : '<div class=empty>Nothing waiting. Requests arrive here and by email.</div>';
  $('decided').innerHTML = rest.length
    ? rest.map(render).join('')
    : '<div class=empty>Nobody has asked yet.</div>';
}

document.addEventListener('click', async e => {
  const button = e.target.closest('button[data-do]');
  if (!button || busy) return;
  busy = true;
  document.querySelectorAll('button[data-do]').forEach(b => b.disabled = true);

  try {
    const r = await fetch('/api/access/requests/' + button.dataset.id + '/'
                          + button.dataset.do, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: '{}',
    });
    const data = await r.json().catch(() => ({}));
    if (!r.ok) {
      $('mail').textContent = data.error || 'That did not work.';
      $('mail').className = 'msg bad';
    } else if (data.decision === 'denied') {
      $('mail').textContent = 'Denied ' + data.email + '. They have not been told.';
      $('mail').className = 'msg good';
    } else if (data.mail === 'sent' || data.mail === 'queued') {
      $('mail').textContent = (data.mail === 'sent' ? 'Sent' : 'Sending')
                            + ' a sign-up link to ' + data.email + '.';
      $('mail').className = 'msg good';
    } else if (data.mail === 'logged') {
      $('mail').textContent = 'Approved ' + data.email + '. No SMTP configured, so the '
                            + 'link was printed to the server console.';
      $('mail').className = 'msg warn';
    } else {
      $('mail').textContent = 'Approved ' + data.email + ', but the email failed to send. '
                            + 'Fix the SMTP settings and press Send the link again.';
      $('mail').className = 'msg warn';
    }
  } catch (err) {
    $('mail').textContent = 'Could not reach the server.';
    $('mail').className = 'msg bad';
  } finally {
    busy = false;
    await load();
  }
});

load();
</script>
""";

    public const string NotYours = Style + """
<title>Manifest — not yours</title>
<div class=wrap>
""" + Brand + """
  <div class=panel>
    <h2>Not your page</h2>
    <div class=empty>Access requests are the owner account's business — that is the
      first account made on this server. You are signed in as someone else.</div>
    <div class=row><a href="/"><button class=yes>Back to the app</button></a></div>
  </div>
</div>
""";
}
