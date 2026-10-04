using System.Net;
using System.Text.RegularExpressions;

namespace Manifest.Tests;

/// <summary>
/// The Content-Security-Policy and the static front end that makes a strict one
/// possible: no inline script, handler or style attribute in the app, and on the
/// pages the server writes itself, inline tags that carry this response's nonce.
/// </summary>
[Collection("server")]
public class ContentSecurityTests(ServerFixture server)
{
    static string Csp(HttpResponseMessage r) => r.Headers.GetValues("Content-Security-Policy").Single();

    static string NonceOf(string csp) => Regex.Match(csp, "'nonce-([^']+)'").Groups[1].Value;

    [Fact]
    public async Task ThePolicyIsStrict()
    {
        var csp = Csp(await server.Client.GetAsync("/api/health"));
        Assert.Contains("default-src 'none'", csp);
        Assert.Contains("script-src 'self' 'nonce-", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.DoesNotContain("unsafe-inline", csp);
        Assert.DoesNotContain("unsafe-eval", csp);
    }

    [Fact]
    public async Task EveryResponseGetsItsOwnNonce()
    {
        var a = NonceOf(Csp(await server.Client.GetAsync("/api/health")));
        var b = NonceOf(Csp(await server.Client.GetAsync("/api/health")));
        Assert.NotEqual("", a);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public async Task TheAppShellHasNothingInline()
    {
        var response = await server.Client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<script src=\"/js/api.js\" defer></script>", html);
        Assert.DoesNotMatch(@"<script(?![^>]*\bsrc=)", html);      // every script is a file
        Assert.DoesNotMatch(@"\son[a-z]+\s*=", html);              // no onclick= / onerror=
        Assert.DoesNotContain("style=", html);
        Assert.DoesNotContain("<style", html);
    }

    [Theory]
    [InlineData("/js/api.js", "text/javascript")]
    [InlineData("/js/session.js", "text/javascript")]
    [InlineData("/css/app.css", "text/css")]
    public async Task AssetsComeFromTheBinaryAndRevalidate(string path, string type)
    {
        var first = await server.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.StartsWith(type, first.Content.Headers.ContentType!.ToString());
        var etag = first.Headers.ETag!.Tag;

        using var again = new HttpRequestMessage(HttpMethod.Get, path);
        again.Headers.TryAddWithoutValidation("If-None-Match", etag);
        Assert.Equal(HttpStatusCode.NotModified, (await server.Client.SendAsync(again)).StatusCode);
    }

    [Theory]
    [InlineData("/js/../../catalog.json")]
    [InlineData("/js/nope.js")]
    [InlineData("/css/..%2F..%2Fmanifest.db")]
    public async Task NothingOutsideTheFrontEnd(string path) =>
        Assert.Equal(HttpStatusCode.NotFound, (await server.Client.GetAsync(path)).StatusCode);

    /// <summary>The sign-in, setup and admin pages keep inline script, nonce'd.</summary>
    [Theory]
    [InlineData("/setup")]
    public async Task ServerPagesNonceTheirOwnTags(string path)
    {
        var response = await server.NewAnonymousClient().GetAsync(path);
        var nonce = NonceOf(Csp(response));
        var html = await response.Content.ReadAsStringAsync();

        var tags = Regex.Matches(html, @"<(script|style)\b[^>]*>").Select(m => m.Value).ToList();
        Assert.NotEmpty(tags);
        Assert.All(tags, t => Assert.Contains($"nonce=\"{nonce}\"", t));
        Assert.DoesNotMatch(@"\son[a-z]+\s*=", html);
        Assert.DoesNotContain("style=", html);
    }

    [Fact]
    public async Task TheSignInPageIsNoncedToo()
    {
        var response = await server.NewAnonymousClient().GetAsync("/");
        var nonce = NonceOf(Csp(response));
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains($"<script nonce=\"{nonce}\">", html);
        Assert.Contains($"<style nonce=\"{nonce}\">", html);
    }
}
