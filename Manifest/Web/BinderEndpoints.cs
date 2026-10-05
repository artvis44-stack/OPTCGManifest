using System.Text.Json;
using Manifest.Data;

namespace Manifest.Web;

/// <summary>
/// Binders, and which one a collection request is about. Every collection route
/// takes ?binder=: a binder's id, "all" for every binder the account is in added
/// together (read-only), or nothing for the account's own binder - which is what
/// every route meant before binders existed.
/// </summary>
public static class BinderEndpoints
{
    /// <summary>The binders a read covers, or null when the account cannot see that binder.</summary>
    public static BinderScope? ReadScope(HttpContext ctx, BinderRepository binders)
    {
        var raw = ctx.Request.Query["binder"].FirstOrDefault();
        var user = ctx.UserId();
        if (string.IsNullOrEmpty(raw)) return BinderScope.One(binders.Personal(user));
        if (raw == "all") return BinderScope.Usable(user);
        if (!long.TryParse(raw, out var id) || binders.Rights(user, id) == BinderRights.None)
            return null;
        return BinderScope.One(id);
    }

    /// <summary>
    /// The binder a write goes into. When it cannot be written, the reason has
    /// already been sent and this is null.
    /// </summary>
    public static async Task<long?> WriteBinder(HttpContext ctx, BinderRepository binders)
    {
        var raw = ctx.Request.Query["binder"].FirstOrDefault();
        var user = ctx.UserId();
        if (string.IsNullOrEmpty(raw)) return binders.Personal(user);
        if (raw == "all")
        {
            await ctx.Json(400, new { error = "Pick one binder to put cards in." });
            return null;
        }
        if (!long.TryParse(raw, out var id)) return await NotFound(ctx);
        switch (binders.Rights(user, id))
        {
            case BinderRights.Write:
                return id;
            case BinderRights.Read:
                await ctx.Json(403, new { error = "That binder is someone else's; you can only look." });
                return null;
            default:
                return await NotFound(ctx);
        }
    }

    static async Task<long?> NotFound(HttpContext ctx)
    {
        await ctx.Json(404, new { error = "not found" });
        return null;
    }

    public static void Map(WebApplication app)
    {
        var binders = app.Services.GetRequiredService<BinderRepository>();

        app.MapGet("/api/binders", async ctx =>
            await ctx.Json(200, new { binders = binders.List(ctx.UserId()) }));

        app.MapPost("/api/binders", async ctx =>
        {
            var body = await Body<BinderPost>(ctx);
            await Answer(ctx, () => new { binder = binders.Create(ctx.UserId(), body.Name, body.MoveMine) });
        });

        app.MapPost("/api/binders/visibility", async ctx =>
        {
            var body = await Body<VisibilityPost>(ctx);
            await Answer(ctx, () => new { binder = binders.SetVisible(ctx.UserId(), body.Visible) });
        });

        app.MapPost("/api/binders/move", async ctx =>
        {
            var body = await Body<MovePost>(ctx);
            if (string.IsNullOrEmpty(body.CardId))
            {
                await ctx.Json(400, new { error = "card_id required" });
                return;
            }
            var cid = Services.CardId.Normalise(body.CardId) ?? body.CardId.ToUpperInvariant().Trim();
            await Answer(ctx, () =>
            {
                var (from, to) = binders.Move(ctx.UserId(), cid, body.From, body.To, body.Qty ?? 1);
                return new { card_id = cid, from_qty = from, to_qty = to };
            });
        });

        app.MapPost("/api/binders/{id:long}", async (HttpContext ctx, long id) =>
        {
            var body = await Body<BinderPost>(ctx);
            await Answer(ctx, () => new { binder = binders.Rename(ctx.UserId(), id, body.Name) });
        });

        app.MapPost("/api/binders/{id:long}/members", async (HttpContext ctx, long id) =>
        {
            var body = await Body<MemberPost>(ctx);
            await Answer(ctx, () => new { binder = binders.AddMember(ctx.UserId(), id, body.Username) });
        });

        app.MapPost("/api/binders/{id:long}/members/{username}/delete",
            async (HttpContext ctx, long id, string username) =>
            await Answer(ctx, () =>
            {
                binders.RemoveMember(ctx.UserId(), id, username);
                return new { ok = true };
            }));

        app.MapPost("/api/binders/{id:long}/delete", async (HttpContext ctx, long id) =>
            await Answer(ctx, () =>
            {
                binders.Delete(ctx.UserId(), id);
                return new { ok = true };
            }));
    }

    /// <summary>A binder the account is not in reads as one that does not exist.</summary>
    static async Task Answer(HttpContext ctx, Func<object> act)
    {
        object reply;
        try
        {
            reply = act();
        }
        catch (BinderError e)
        {
            await ctx.Json(400, new { error = e.Message });
            return;
        }
        catch (KeyNotFoundException)
        {
            await ctx.Json(404, new { error = "not found" });
            return;
        }
        await ctx.Json(200, reply);
    }

    static async Task<T> Body<T>(HttpContext ctx) where T : new()
    {
        if (ctx.Request.ContentLength is null or 0) return new T();
        var parsed = await JsonSerializer.DeserializeAsync<T>(ctx.Request.Body, Manifest.Json.Options);
        return parsed ?? new T();
    }
}
