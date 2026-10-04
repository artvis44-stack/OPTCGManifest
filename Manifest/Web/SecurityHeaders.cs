using System.Security.Cryptography;

namespace Manifest.Web;

public static class SecurityHeaders
{
    const string NonceKey = "csp-nonce";

    /// <summary>The headers every response carries, including a fresh CSP nonce.</summary>
    public static void Apply(HttpContext ctx)
    {
        foreach (var (k, v) in AppConfig.SecurityHeaders)
            ctx.Response.Headers[k] = v;
        ctx.Response.Headers["Content-Security-Policy"] = AppConfig.ContentSecurityPolicy(Nonce(ctx));
    }

    /// <summary>This response's nonce: random, and new for every request.</summary>
    public static string Nonce(HttpContext ctx)
    {
        if (ctx.Items.TryGetValue(NonceKey, out var n) && n is string nonce) return nonce;
        nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        ctx.Items[NonceKey] = nonce;
        return nonce;
    }
}
