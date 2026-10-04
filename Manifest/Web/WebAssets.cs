using System.Reflection;
using System.Security.Cryptography;

namespace Manifest.Web;

/// <summary>
/// The app's own page, stylesheet and scripts, from Manifest/wwwroot, compiled into
/// the binary - so a container is one file and a deploy cannot ship a page that
/// does not match its server. Each is served with an ETag and revalidated on every
/// load, so an upgrade reaches phones at once instead of after a cache expires.
///
/// MANIFEST_WEB_ROOT points at a wwwroot folder on disk instead, re-read on every
/// request, for working on the front end without rebuilding.
/// </summary>
public sealed class WebAssets
{
    public sealed record Asset(byte[] Bytes, string ContentType, string ETag);

    readonly Dictionary<string, Asset> _embedded;
    readonly string? _disk;

    public WebAssets()
    {
        _disk = Environment.GetEnvironmentVariable("MANIFEST_WEB_ROOT") is { Length: > 0 } dir
            ? Path.GetFullPath(dir)
            : null;

        var assembly = typeof(WebAssets).Assembly;
        _embedded = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith("wwwroot/", StringComparison.Ordinal))
            .ToDictionary(n => n["wwwroot/".Length..], n => Load(assembly, n), StringComparer.Ordinal);
    }

    static Asset Load(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return Make(name, buffer.ToArray());
    }

    static Asset Make(string name, byte[] bytes) => new(
        bytes,
        Path.GetExtension(name) switch
        {
            ".html" => "text/html; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".js" => "text/javascript; charset=utf-8",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            _ => "application/octet-stream",
        },
        "\"" + Convert.ToHexString(SHA256.HashData(bytes))[..20].ToLowerInvariant() + "\"");

    /// <summary>The asset at a path like "js/api.js", or null - never anything outside wwwroot.</summary>
    public Asset? Find(string path)
    {
        if (path.Contains("..", StringComparison.Ordinal) || path.Contains('\\')) return null;
        if (_disk is null) return _embedded.GetValueOrDefault(path);

        var file = Path.GetFullPath(Path.Combine(_disk, path));
        if (!file.StartsWith(_disk + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || !File.Exists(file))
            return null;
        return Make(file, File.ReadAllBytes(file));
    }

    public async Task Serve(HttpContext ctx, string path)
    {
        var asset = Find(path);
        if (asset is null)
        {
            await ctx.Send(404, Array.Empty<byte>(), "text/plain");
            return;
        }
        if (ctx.Request.Headers.IfNoneMatch == asset.ETag)
        {
            ctx.Response.StatusCode = 304;
            ctx.Response.Headers.ETag = asset.ETag;
            ctx.Response.Headers.CacheControl = "no-cache";
            return;
        }
        await ctx.Send(200, asset.Bytes, asset.ContentType, new[]
        {
            ("ETag", asset.ETag),
            ("Cache-Control", "no-cache"),
        });
    }
}
