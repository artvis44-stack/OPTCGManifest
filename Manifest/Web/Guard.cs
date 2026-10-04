using Manifest.Web;

namespace Manifest;

/// <summary>
/// Host and Origin checking, plus the security headers. Written by hand rather than
/// reached for from the framework's HostFiltering/CORS middleware because the exact
/// status codes and the JSON error body with its log reference are part of what the
/// UI and the test suite expect.
/// </summary>
public sealed class Guard
{
    readonly RequestDelegate _next;
    readonly AppConfig _config;

    public Guard(RequestDelegate next, AppConfig config)
    {
        _next = next;
        _config = config;
    }

    public async Task Invoke(HttpContext ctx)
    {
        SecurityHeaders.Apply(ctx);

        if (!HostOk(ctx))
        {
            await ctx.Fail(421, "This server does not answer to that host name.",
                           $"rejected Host: '{ctx.Request.Headers.Host}'");
            return;
        }

        var writing = HttpMethods.IsPost(ctx.Request.Method);
        if (writing)
        {
            if (!OriginOk(ctx))
            {
                await ctx.Fail(403, "Cross-site requests are not accepted.",
                               $"rejected Origin: '{ctx.Request.Headers.Origin}'");
                return;
            }

            // A cross-origin <form> can only send text/plain, multipart/form-data or
            // url-encoded bodies; asking for JSON means a hostile page has to make a
            // preflighted request, which never gets an answer here.
            var ctype = (ctx.Request.ContentType ?? "").Split(';')[0].Trim().ToLowerInvariant();
            if (ctype != "application/json")
            {
                await ctx.Fail(415, "Send application/json.", $"rejected Content-Type: '{ctype}'");
                return;
            }

            if (ctx.Request.ContentLength > _config.UploadLimitBytes)
            {
                await ctx.Fail(413,
                    $"That request is too large. The limit is {_config.UploadLimitBytes / (1024 * 1024)}MB.",
                    $"{ctx.Request.ContentLength} bytes");
                return;
            }
        }

        await _next(ctx);
    }

    /// <summary>Host must be one this server actually answers to - see AllowedHosts.</summary>
    bool HostOk(HttpContext ctx)
    {
        var host = (ctx.Request.Headers.Host.ToString() ?? "").Trim();
        if (host.Length == 0) return false;

        string name;
        if (host.StartsWith('['))                            // [::1]:8420
        {
            var close = host.IndexOf(']');
            name = close >= 0 ? host[1..close] : host;
        }
        else
        {
            name = host.Count(c => c == ':') == 1 ? host[..host.LastIndexOf(':')] : host;
        }
        return _config.AllowedHosts.Contains(name);
    }

    /// <summary>
    /// A browser attaches Origin to cross-site POSTs. No Origin at all is a
    /// non-browser client (curl, the test suite) and is left alone; an Origin that
    /// isn't us is a request some other site made on the user's behalf.
    /// </summary>
    bool OriginOk(HttpContext ctx)
    {
        var origin = ctx.Request.Headers.Origin.ToString();
        if (string.IsNullOrEmpty(origin)) return true;
        if (origin == "null") return false;
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
        var name = uri.Host;
        return name.Length > 0 && _config.AllowedHosts.Contains(name);
    }
}
