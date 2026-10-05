using System.Net;
using Manifest.Data;
using Manifest.Services;

namespace Manifest.Web;

/// <summary>
/// GET /metrics on MANIFEST_METRICS_LISTEN, for Prometheus. Its own small listener,
/// in the site and in `manifest worker` alike, rather than a route on the site:
/// the reverse proxy only forwards the site's port, so this is reachable on the
/// compose network and not from the internet, with no token to manage. It answers
/// nothing else, and has no Host check because nothing here is worth rebinding to.
/// </summary>
public static class MetricsServer
{
    public static async Task<WebApplication?> Start(AppConfig config, JobQueue queue, ILogger log)
    {
        if (config.MetricsEndpoint is not { } endpoint) return null;

        var exporter = new PrometheusExporter();
        Telemetry.ObserveQueue(queue);

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            options.Listen(endpoint);
        });
        var app = builder.Build();

        app.MapGet("/metrics", (HttpContext ctx) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            return Results.Text(exporter.Render(), "text/plain; version=0.0.4; charset=utf-8");
        });
        app.MapFallback((HttpContext ctx) => Results.NotFound());

        try
        {
            await app.StartAsync();
        }
        catch (Exception e)
        {
            // The site matters more than its numbers: say so loudly and carry on.
            log.LogError("could not serve metrics on {Endpoint}: {Error}", endpoint, e.Message);
            exporter.Dispose();
            return null;
        }
        app.Lifetime.ApplicationStopped.Register(exporter.Dispose);
        return app;
    }

    public static string Describe(IPEndPoint endpoint) => $"http://{endpoint}/metrics";
}
