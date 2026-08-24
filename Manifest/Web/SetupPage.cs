namespace Manifest.Web;

public static class SetupPage
{
    public const string Html = """
<!doctype html><meta charset=utf-8>
<meta name=viewport content="width=device-width,initial-scale=1">
<title>Camera setup</title>
<style>
body{margin:0;background:#0E2A33;color:#E8E1D4;font:16px/1.6 system-ui,sans-serif}
.w{max-width:560px;margin:0 auto;padding:22px 18px 60px}
h1{font-size:22px;letter-spacing:.16em;text-transform:uppercase;margin:0 0 4px}
h2{font-size:13px;letter-spacing:.18em;text-transform:uppercase;color:#C98A2E;
   margin:30px 0 8px}
p,li{color:rgba(232,225,212,.82)}
ol{padding-left:20px}li{margin:9px 0}
a.btn{display:block;text-align:center;background:#C98A2E;color:#071A20;
  text-decoration:none;font-weight:700;padding:15px;margin:16px 0}
code{font-family:ui-monospace,monospace;background:rgba(232,225,212,.1);
  padding:2px 5px;font-size:14px;word-break:break-all}
.note{border-left:3px solid #6FB3A8;padding:2px 0 2px 12px;margin:18px 0;
  color:rgba(232,225,212,.62);font-size:14.5px}
.skip{margin-top:34px;padding-top:16px;border-top:1px solid rgba(232,225,212,.15)}
</style>
<div class=w>
<h1>Camera setup</h1>
<p>Browsers only allow camera access on a secure page. Installing this one-time
certificate makes your own machine count as secure. It is used by nothing else and
goes nowhere near the internet.</p>

<a class=btn href="/ca.crt">Download the certificate</a>

<h2>iPhone and iPad</h2>
<ol>
<li>Tap the button above, then <b>Allow</b>.</li>
<li>Open <b>Settings</b>. Near the top you will see <b>Profile Downloaded</b> — tap
it, then <b>Install</b>, top right. Enter your passcode and confirm.</li>
<li>This second step is the one everyone misses. Go to <b>Settings → General →
About → Certificate Trust Settings</b> and switch <b>Manifest Local Authority</b>
on.</li>
<li>Reopen the app over <code>https://</code> and the camera will work.</li>
</ol>

<h2>Android</h2>
<ol>
<li>Tap the button above to download it.</li>
<li>Open <b>Settings</b> and search for <b>CA certificate</b>. The path is usually
<b>Security → More security settings → Encryption &amp; credentials → Install a
certificate → CA certificate</b>.</li>
<li>Tap <b>Install anyway</b>, then pick the downloaded <code>ca.crt</code>.</li>
<li>Reopen the app over <code>https://</code>.</li>
</ol>

<div class=note>Android also has a shortcut with nothing to install: open
<code>chrome://flags/#unsafely-treat-insecure-origin-as-secure</code>, paste this
page's <code>http://</code> address into the box, set it to Enabled, and relaunch
Chrome. Firefox: in <code>about:config</code> set
<code>media.devices.insecure.enabled</code> and
<code>media.getusermedia.insecure.enabled</code> to true.</div>

<div class=skip>
<h2>Or skip all of this</h2>
<p>Run the server without <code>--https</code>. Everything works except the live
viewfinder — the <b>Photo</b> button still scans, because uploading a picture does
not need a secure page. And typing a card number is faster than scanning anyway.</p>
<a class=btn href="/">Back to the app</a>
</div>
</div>
""";

    /// <summary>
    /// The plain-http helper points its "back to the app" link at the https origin,
    /// since that host is the whole reason the phone is on this page.
    /// </summary>
    public static string For(string appUrl) => Html.Replace("href=\"/\"", $"href=\"{appUrl}\"");
}
