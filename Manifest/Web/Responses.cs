using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Manifest.Web;

public static class Responses
{
    public static async Task Json(this HttpContext ctx, int code, object body)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(body, Manifest.Json.Options);
        await ctx.Send(code, bytes, "application/json");
    }

    public static Task Text(this HttpContext ctx, int code, string body, string contentType) =>
        ctx.Send(code, Encoding.UTF8.GetBytes(body), contentType);

    public static async Task Send(this HttpContext ctx, int code, byte[] body, string contentType,
                                  (string Key, string Value)[]? extra = null)
    {
        ctx.Response.StatusCode = code;
        ctx.Response.ContentType = contentType;
        ctx.Response.ContentLength = body.Length;
        ctx.Response.Headers["Cache-Control"] = "no-store";
        foreach (var (k, v) in extra ?? Array.Empty<(string, string)>())
            ctx.Response.Headers[k] = v;
        await ctx.Response.Body.WriteAsync(body);
    }

    /// <summary>
    /// Client-visible message stays generic; the real reason goes to the console with
    /// a reference so a report can be matched to a log line.
    /// </summary>
    public static Task Fail(this HttpContext ctx, int code, string message, string? detail = null)
    {
        var reference = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
        Console.Error.WriteLine(
            $"  [{reference}] {ctx.Request.Method} {ctx.Request.Path}{ctx.Request.QueryString}"
            + $" -> {code}: {detail ?? message}");
        return ctx.Json(code, new { error = message, @ref = reference });
    }
}
