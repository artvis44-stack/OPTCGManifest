using System.Text.Json;
using Manifest.Data;

namespace Manifest.Web;

/// <summary>
/// eBay listings: drafting the words for one from a card, and keeping track of each
/// one from draft to sale. Nothing here talks to eBay.
/// </summary>
public static class ListingEndpoints
{
    public static void Map(WebApplication app)
    {
        var listings = app.Services.GetRequiredService<ListingRepository>();

        // ?days= narrows the sales figures to recent ones; what is up now is always all of it.
        app.MapGet("/api/listings", async ctx =>
        {
            int? days = int.TryParse(ctx.Request.Query["days"], out var d) ? d : null;
            var (stats, top) = listings.Summary(ctx.UserId(), days);
            await ctx.Json(200, new { listings = listings.List(ctx.UserId()), stats, top });
        });

        app.MapGet("/api/listings/draft", async ctx =>
        {
            var q = ctx.Request.Query;
            var qty = int.TryParse(q["qty"], out var n) ? n : 1;
            await Answer(ctx, () => new
            {
                draft = listings.Draft(q["card_id"].FirstOrDefault() ?? "", q["condition"], q["grade"], qty),
            });
        });

        app.MapPost("/api/listings", async ctx =>
        {
            var body = await Body(ctx);
            await Answer(ctx, () => new { listing = listings.Create(ctx.UserId(), body.ToInput()) });
        });

        app.MapPost("/api/listings/{id:long}", async (HttpContext ctx, long id) =>
        {
            var body = await Body(ctx);
            await Answer(ctx, () => new { listing = listings.Update(ctx.UserId(), id, body.ToInput()) });
        });

        app.MapPost("/api/listings/{id:long}/delete", async (HttpContext ctx, long id) =>
            await Answer(ctx, () =>
            {
                listings.Delete(ctx.UserId(), id);
                return new { ok = true };
            }));
    }

    static async Task Answer(HttpContext ctx, Func<object> act)
    {
        object reply;
        try
        {
            reply = act();
        }
        catch (ListingError e)
        {
            await ctx.Json(e.Status, new { error = e.Message });
            return;
        }
        await ctx.Json(200, reply);
    }

    static async Task<ListingPost> Body(HttpContext ctx)
    {
        if (ctx.Request.ContentLength is null or 0) return new ListingPost();
        return await JsonSerializer.DeserializeAsync<ListingPost>(ctx.Request.Body, Manifest.Json.Options)
               ?? new ListingPost();
    }
}
