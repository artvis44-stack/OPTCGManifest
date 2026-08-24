using Manifest.Data;
using Manifest.Models;

namespace Manifest.Web;

/// <summary>
/// Turns the session cookie into an account, and refuses the API to anyone without
/// one. Sits after Guard, so a request that fails the host or origin checks never
/// reaches a database lookup.
/// </summary>
public sealed class Auth
{
    public const string CookieName = "manifest_session";

    /// <summary>
    /// Paths that answer without an account. Everything else - including card art,
    /// which costs an upstream fetch the first time each card is asked for - needs
    /// one. /setup and ca.pem stay open because they are how a phone comes to trust
    /// this server's certificate in the first place, and the certificate authority
    /// is public information by design.
    /// </summary>
    static readonly HashSet<string> Open = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/health", "/api/health/live", "/api/health/ready",
        "/api/session", "/api/auth/login", "/api/auth/register", "/api/auth/logout",
        "/setup", "/ca.crt", "/ca.pem", "/favicon.ico",

        // The request-access flow, which exists precisely for people who have no
        // account and therefore cannot get past this middleware. Each of these
        // carries its own gate instead: a rate limit on the request form, and a
        // single-use token on the three that act on a particular request.
        "/api/access/request", "/api/access/review", "/api/access/decide",
        "/api/access/invite",
    };

    readonly RequestDelegate _next;
    readonly ISessionRepository _sessions;

    public Auth(RequestDelegate next, ISessionRepository sessions)
    {
        _next = next;
        _sessions = sessions;
    }

    public async Task Invoke(HttpContext ctx)
    {
        var token = ctx.Request.Cookies[CookieName];
        if (!string.IsNullOrEmpty(token))
        {
            var user = _sessions.ForToken(token);
            if (user is not null) ctx.Items[CookieName] = user;
        }

        var path = ctx.Request.Path.Value ?? "/";
        if (ctx.User() is null && NeedsAccount(path))
        {
            // The UI turns this into a trip back to the sign-in page, so the body
            // says which of the two it is: no session, rather than no permission.
            await ctx.Json(401, new { error = "sign in first", authenticated = false });
            return;
        }

        await _next(ctx);
    }

    /// <summary>
    /// "/" is not listed: it serves the sign-in page to a stranger and the app to
    /// an account, which the route itself decides.
    /// </summary>
    static bool NeedsAccount(string path)
    {
        if (Open.Contains(path)) return false;
        return path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/img/", StringComparison.OrdinalIgnoreCase);
    }
}

public static class AuthContext
{
    public static User? User(this HttpContext ctx) =>
        ctx.Items.TryGetValue(Auth.CookieName, out var found) ? found as User : null;

    /// <summary>
    /// The account this request belongs to. Only ever called from a route the Auth
    /// middleware has already gated, so a missing account here is a routing mistake
    /// and not something to paper over with a guest identity.
    /// </summary>
    public static long UserId(this HttpContext ctx) =>
        ctx.User()?.Id ?? throw new InvalidOperationException(
            "no account on this request - the route should be behind Auth");

    /// <summary>
    /// Secure is set whenever the connection really is TLS, including when Caddy or
    /// nginx terminated it upstream. It is left off on plain http so the app still
    /// works over a home network, where there is no certificate and no cookie to
    /// steal from outside it.
    /// </summary>
    public static bool IsSecure(this HttpContext ctx) =>
        ctx.Request.IsHttps
        || string.Equals(ctx.Request.Headers["X-Forwarded-Proto"], "https",
                         StringComparison.OrdinalIgnoreCase);

    public static void SetSessionCookie(this HttpContext ctx, string token) =>
        ctx.Response.Cookies.Append(Auth.CookieName, token, new CookieOptions
        {
            HttpOnly = true,                       // script cannot read it, so XSS cannot post it on
            Secure = ctx.IsSecure()
                     || ctx.RequestServices.GetService<AppConfig>()?.IsProduction == true,
            SameSite = SameSiteMode.Lax,           // survives following a link in, blocks cross-site POSTs
            Path = "/",
            MaxAge = ctx.RequestServices.GetRequiredService<ISessionRepository>().SessionLifetime,
            IsEssential = true,
        });

    public static void ClearSessionCookie(this HttpContext ctx) =>
        ctx.Response.Cookies.Append(Auth.CookieName, "", new CookieOptions
        {
            HttpOnly = true,
            Secure = ctx.IsSecure()
                     || ctx.RequestServices.GetService<AppConfig>()?.IsProduction == true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = TimeSpan.Zero,
            IsEssential = true,
        });

    /// <summary>
    /// Who to count failed sign-ins against. Behind a reverse proxy every request
    /// arrives from the proxy, so the forwarded address is the only useful one -
    /// but it is a header, and a header is whatever the sender says it is. Trusting
    /// it is therefore opt-in with --behind-proxy, which you only set when something
    /// in front is overwriting it.
    /// </summary>
    public static string ClientIp(this HttpContext ctx, bool trustForwarded)
    {
        if (trustForwarded)
        {
            var forwarded = ctx.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrEmpty(forwarded))
            {
                var first = forwarded.Split(',')[0].Trim();
                if (first.Length > 0) return first;
            }
        }
        return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
