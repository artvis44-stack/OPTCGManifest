using Manifest.Data;
using Manifest.Services.Jobs;

namespace Manifest.Web;

/// <summary>
/// Keeping the card data current from the admin page instead of a console: when
/// the new-set check and the price refresh last ran, how that went, and a button
/// for each that queues the same job the schedule does. The owner's alone, like
/// the rest of /admin.
/// </summary>
public static class AdminEndpoints
{
    /// <summary>What the page calls each job, and the job behind it.</summary>
    static readonly Dictionary<string, string> Jobs = new()
    {
        ["scrape-catalog"] = JobTypes.ScrapeCatalog,
        ["refresh-prices"] = JobTypes.RefreshPrices,
    };

    public static void Map(WebApplication app)
    {
        var queue = app.Services.GetRequiredService<JobQueue>();
        var db = app.Services.GetRequiredService<Database>();
        var config = app.Services.GetRequiredService<AppConfig>();

        app.MapGet("/api/admin/data", async ctx =>
        {
            if (!IsOwner(ctx)) { await Forbid(ctx); return; }
            var (printings, priced, pricedAt) = Counts(db);
            await ctx.Json(200, new
            {
                printings,
                priced,
                prices_updated_at = pricedAt,
                // A worker is what runs these. Disabled means nothing will, and the
                // page should say so rather than show a job queued forever.
                worker = config.WorkerMode,
                jobs = Jobs.ToDictionary(j => j.Key, j => View(queue.Latest(j.Value),
                    j.Value == JobTypes.ScrapeCatalog ? config.ScrapeCatalogEvery : config.RefreshPricesEvery)),
            });
        });

        app.MapPost("/api/admin/data/{job}", async (HttpContext ctx, string job) =>
        {
            if (!IsOwner(ctx)) { await Forbid(ctx); return; }
            if (!Jobs.TryGetValue(job, out var type))
            {
                await ctx.Json(404, new { error = "not found" });
                return;
            }
            if (queue.LiveOfType(type) is { } live)
            {
                await ctx.Json(200, new { id = live, already = true });
                return;
            }
            var id = queue.Enqueue(type, new { }, dedupeKey: "manual:" + type,
                                   correlationId: ctx.TraceIdentifier, userId: ctx.UserId());
            await ctx.Json(202, new { id, already = false });
        });
    }

    static object? View(JobQueue.Recent? recent, TimeSpan? every) => new
    {
        every_hours = every?.TotalHours,
        last = recent is not { } r ? null : new
        {
            id = r.Job.Id,
            status = r.Job.Status,
            queued_at = r.CreatedAt,
            updated_at = r.UpdatedAt,
            result = r.Job.Result is { } json ? System.Text.Json.JsonDocument.Parse(json).RootElement : (object?)null,
            error = r.Job.LastError,
            attempts = r.Job.Attempts,
        },
    };

    static (long Printings, long Priced, string? PricedAt) Counts(Database db)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT (SELECT count(*) FROM catalog) AS printings,
                   (SELECT count(*) FROM prices) AS priced,
                   (SELECT max(fetched_at) FROM prices) AS priced_at
            """;
        using var r = cmd.ExecuteReader();
        r.Read();
        return (r.Long("printings"), r.Long("priced"), r.Str("priced_at"));
    }

    static bool IsOwner(HttpContext ctx) => ctx.User()?.IsOwner == true;

    static Task Forbid(HttpContext ctx) => ctx.Json(403, new
    {
        error = "Only the owner account can update the card data.",
    });
}
