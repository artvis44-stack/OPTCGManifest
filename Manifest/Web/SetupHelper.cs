namespace Manifest.Web;

/// <summary>
/// A tiny plain-http listener that runs alongside the https one.
///
/// The setup page exists to get a certificate onto your phone. Serving it over https
/// only would mean the phone has to already trust the certificate in order to reach
/// the page that installs it. So this is deliberately insecure and serves nothing but
/// the certificate and the instructions.
/// </summary>
public static class SetupHelper
{
    public static WebApplication Build(AppConfig config, AppPaths paths, string appUrl)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            var address = config.Bind == "0.0.0.0"
                ? System.Net.IPAddress.Any
                : System.Net.IPAddress.Parse(config.Bind);
            options.Listen(address, config.Port + 1);
        });

        var app = builder.Build();

        app.Use(async (ctx, next) =>
        {
            foreach (var (k, v) in AppConfig.SecurityHeaders)
                ctx.Response.Headers[k] = v;
            await next();
        });

        async Task Setup(HttpContext ctx) =>
            await ctx.Text(200, SetupPage.For(appUrl), "text/html; charset=utf-8");

        app.MapGet("/", Setup);
        app.MapGet("/setup", Setup);

        async Task Ca(HttpContext ctx)
        {
            if (!File.Exists(paths.CaPem))
            {
                await ctx.Text(404, "No certificate authority yet. Run ./make_cert.sh on the "
                                    + "machine running this, then restart.",
                               "text/plain; charset=utf-8");
                return;
            }
            await ctx.Send(200, await File.ReadAllBytesAsync(paths.CaPem),
                           "application/x-x509-ca-cert",
                           new[] { ("Content-Disposition",
                                    "attachment; filename=\"manifest-ca.crt\"") });
        }

        app.MapGet("/ca.crt", Ca);
        app.MapGet("/ca.pem", Ca);

        app.MapFallback(async ctx =>
            await ctx.Text(404, "This is only the setup helper. The app is at " + appUrl,
                           "text/plain; charset=utf-8"));

        return app;
    }
}
