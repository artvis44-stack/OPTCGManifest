using System.Text.Json;
using Manifest.Data;
using Manifest.Models;
using Manifest.Services;
using Manifest.Services.Jobs;

namespace Manifest.Web;

public static class Endpoints
{
    static readonly string[] KnownPaths =
    {
        "/", "/setup", "/ca.crt", "/img/<card-id>", "/register", "/admin",
        "/access/review",
        "/api/health", "/api/health/live", "/api/health/ready",
        "/api/session", "/api/auth/login", "/api/auth/register",
        "/api/auth/logout",
        "/api/access/request", "/api/access/review", "/api/access/decide",
        "/api/access/invite", "/api/access/requests",
        "/api/access/requests/<id>/approve", "/api/access/requests/<id>/deny",
        "/api/search", "/api/facets", "/api/card/<card-id>", "/api/prints/<card-id>",
        "/api/collection", "/api/collection/bulk", "/api/collection/stats", "/api/stats",
        "/api/export.csv",
        "/api/decks", "/api/decks/<id>", "/api/decks/<id>/card",
        "/api/decks/<id>/cards", "/api/decks/<id>/print", "/api/decks/<id>/delete", "/api/scan", "/api/scan/<id>",
        "/api/binders", "/api/binders/<id>", "/api/binders/<id>/members",
        "/api/binders/<id>/members/<username>/delete", "/api/binders/<id>/delete",
        "/api/binders/visibility", "/api/binders/move",
        "/api/admin/data", "/api/admin/data/<scrape-catalog|refresh-prices>",
    };

    public static void Map(WebApplication app)
    {
        MapGet(app);
        MapPost(app);
        AccessEndpoints.Map(app);
        BinderEndpoints.Map(app);
        AdminEndpoints.Map(app);

        app.MapFallback(async ctx =>
        {
            if (HttpMethods.IsGet(ctx.Request.Method))
            {
                await ctx.Json(404, new
                {
                    error = "not found",
                    version = AppConfig.Version,
                    paths = KnownPaths,
                    hint = "If a path you expected is missing, this copy of the server "
                           + "is older than the one you are reading about.",
                });
                return;
            }
            await ctx.Json(404, new { error = "not found" });
        });
    }

    static void MapGet(WebApplication app)
    {
        var paths = app.Services.GetRequiredService<AppPaths>();
        var cards = app.Services.GetRequiredService<CardRepository>();
        var decks = app.Services.GetRequiredService<DeckRepository>();
        var images = app.Services.GetRequiredService<ImageCache>();
        var tesseract = app.Services.GetRequiredService<TesseractScanner>();
        var users = app.Services.GetRequiredService<UserRepository>();
        var access = app.Services.GetRequiredService<AccessRepository>();
        var config = app.Services.GetRequiredService<AppConfig>();
        var database = app.Services.GetRequiredService<Database>();
        var assets = app.Services.GetRequiredService<WebAssets>();
        var binders = app.Services.GetRequiredService<BinderRepository>();

        // Every read below covers ?binder= (see BinderEndpoints); one the account
        // cannot see answers as if it did not exist.
        async Task<BinderScope?> Scope(HttpContext ctx)
        {
            if (BinderEndpoints.ReadScope(ctx, binders) is { } scope) return scope;
            await ctx.Json(404, new { error = "not found" });
            return null;
        }

        // The one route that answers differently to a stranger: the app for an
        // account, the sign-in page for anyone else.
        async Task Ui(HttpContext ctx)
        {
            if (ctx.User() is null)
            {
                await ctx.Html(200, LoginPage.Html);
                return;
            }
            await assets.Serve(ctx, "index.html");
        }

        app.MapGet("/", Ui);
        app.MapGet("/index.html", Ui);
        app.MapGet("/css/{**path}", (HttpContext ctx, string path) => assets.Serve(ctx, "css/" + path));
        app.MapGet("/js/{**path}", (HttpContext ctx, string path) => assets.Serve(ctx, "js/" + path));

        app.MapGet("/img/{**cardId}", async (HttpContext ctx, string cardId) =>
        {
            var got = await images.Get(cardId.Replace(".png", ""), ctx.TraceIdentifier);
            switch (got.State)
            {
                case ImageCache.State.Found:
                    await ctx.Send(200, got.Bytes!, "image/png", new[]
                    {
                        ("Cache-Control", "public, max-age=31536000, immutable"),
                    });
                    return;
                case ImageCache.State.Pending:
                    // Being fetched now. Not cached, so asking again finds it.
                    await ctx.Send(404, Array.Empty<byte>(), "text/plain", new[]
                    {
                        ("Retry-After", "2"), ("X-Image", "pending"),
                    });
                    return;
                default:
                    await ctx.Send(404, Array.Empty<byte>(), "text/plain", new[] { ("X-Image", "missing") });
                    return;
            }
        });

        app.MapGet("/setup", async ctx =>
            await ctx.Html(200, SetupPage.Html));

        async Task Ca(HttpContext ctx)
        {
            if (!File.Exists(paths.CaPem))
            {
                await ctx.Text(404, "No authority yet. Run ./make_cert.sh on the machine "
                                    + "running this.", "text/plain");
                return;
            }
            // iOS only offers to install a profile for this exact type
            await ctx.Send(200, await File.ReadAllBytesAsync(paths.CaPem),
                           "application/x-x509-ca-cert",
                           new[] { ("Content-Disposition",
                                    "attachment; filename=\"manifest-ca.crt\"") });
        }

        app.MapGet("/ca.crt", Ca);
        app.MapGet("/ca.pem", Ca);

        app.MapGet("/api/card/{**cardId}", async (HttpContext ctx, string cardId) =>
        {
            if (await Scope(ctx) is not { } scope) return;
            var card = cards.CardDetail(scope, cardId);
            if (card is null) { await ctx.Json(404, new { error = "not found" }); return; }
            await ctx.Json(200, new { card });
        });

        app.MapGet("/api/prints/{**cardId}", async (HttpContext ctx, string cardId) =>
        {
            if (await Scope(ctx) is not { } scope) return;
            await ctx.Json(200, new { prints = cards.Prints(scope, cardId) });
        });

        app.MapGet("/api/decks", async ctx =>
        {
            if (Paged(ctx) is { } cursor)
            {
                var page = decks.ListPage(ctx.UserId(), Keyset.Limit(ctx.Request.Query["limit"]), cursor);
                await ctx.Json(200, new { items = page.Items, next_cursor = page.NextCursor });
                return;
            }
            await ctx.Json(200, new { decks = decks.List(ctx.UserId()) });
        });

        app.MapGet("/api/decks/{id:long}", async (HttpContext ctx, long id) =>
        {
            var deck = decks.Detail(ctx.UserId(), id);
            if (deck is null) { await ctx.Json(404, new { error = "not found" }); return; }
            await ctx.Json(200, new { deck });
        });

        app.MapGet("/api/search", async ctx =>
        {
            var q = ctx.Request.Query;
            if (await Scope(ctx) is not { } scope) return;
            if (Paged(ctx) is { } cursor)
            {
                var page = cards.SearchPage(scope, Filter(q), Blank(q["sort"].FirstOrDefault()),
                                            Keyset.Limit(q["limit"]), cursor);
                await ctx.Json(200, new { items = page.Items, next_cursor = page.NextCursor });
                return;
            }

            var results = cards.Search(
                scope,
                q["q"].FirstOrDefault() ?? "",
                q["limit"].FirstOrDefault() ?? "40",
                (q["owned"].FirstOrDefault() ?? "0") == "1",
                Blank(q["category"].FirstOrDefault()),
                Blank(q["color"].FirstOrDefault()),
                Blank(q["rarity"].FirstOrDefault()),
                Blank(q["set"].FirstOrDefault()));
            await ctx.Json(200, new { results });
        });

        app.MapGet("/api/facets", async ctx => await ctx.Json(200, cards.Facets()));

        app.MapGet("/api/collection", async ctx =>
        {
            var q = ctx.Request.Query;
            if (await Scope(ctx) is not { } scope) return;
            if (Paged(ctx) is { } cursor)
            {
                var page = cards.CollectionPage(scope, Filter(q), Blank(q["sort"].FirstOrDefault()),
                                                Keyset.Limit(q["limit"]), cursor);
                await ctx.Json(200, new { items = page.Items, next_cursor = page.NextCursor });
                return;
            }
            await ctx.Json(200, new
            {
                cards = cards.Collection(scope),
                stats = cards.Stats(scope),
            });
        });

        async Task StatsFor(HttpContext ctx)
        {
            if (await Scope(ctx) is not { } scope) return;
            await ctx.Json(200, cards.Stats(scope));
        }
        app.MapGet("/api/stats", StatsFor);
        app.MapGet("/api/collection/stats", StatsFor);

        // Told to a stranger as readily as to an account, because the sign-in page
        // needs it before there is anyone to be: it is what decides whether the
        // "create one" link appears at all.
        app.MapGet("/api/session", async ctx =>
        {
            var user = ctx.User();
            await ctx.Json(200, new
            {
                authenticated = user is not null,
                user = user is null ? null : new SessionInfo
                {
                    Username = user.Username,
                    Email = user.Email,
                    Owner = user.IsOwner,
                    CreatedAt = user.CreatedAt,
                },
                registration = AppConfig.RegistrationOpen ? "invite" : "closed",
                // Separate from `registration`: a server can take requests while
                // having no shared code at all, which is the shape a public
                // deployment wants - nobody registers without being let in by name.
                request_access = AppConfig.RequestAccessOpen,
                // Only ever true for the owner, and only so the app can show a link
                // to /admin rather than making it something you have to know about.
                pending_requests = user?.IsOwner == true ? access.PendingCount() : 0,
                users = users.Count(),
            });
        });

        app.MapGet("/api/health/live", async ctx => await ctx.Json(200, new
        {
            ok = true,
            status = "live",
            version = AppConfig.Version,
            environment = config.EnvironmentName,
            request_id = ctx.TraceIdentifier,
        }));

        app.MapGet("/api/health/ready", async ctx =>
        {
            var (status, body) = await HealthChecks.Ready(config, database,
                app.Services.GetService<StackExchange.Redis.IConnectionMultiplexer>(),
                app.Services.GetService<IImageStore>(), ctx.RequestAborted);
            await ctx.Json(status, body);
        });

        app.MapGet("/api/health", async ctx => await ctx.Json(200, new
        {
            ok = true,
            version = AppConfig.Version,
            environment = config.EnvironmentName,
            catalog = cards.CatalogCount(),
            ocr = tesseract.Available,
            scanning = tesseract.Available || AppConfig.ApiKey is not null,
            api_scanning = AppConfig.ApiKey is not null,
            secure = ctx.IsSecure(),
            authenticated = ctx.User() is not null,
        }));

        app.MapGet("/api/export.csv", async ctx =>
        {
            if (await Scope(ctx) is not { } scope) return;
            await ctx.Send(200,
                System.Text.Encoding.UTF8.GetBytes(CsvExport.Write(cards.Collection(scope))),
                "text/csv; charset=utf-8",
                new[] { ("Content-Disposition",
                         "attachment; filename=\"one-piece-manifest.csv\"") });
        });
    }

    static void MapPost(WebApplication app)
    {
        var cards = app.Services.GetRequiredService<CardRepository>();
        var decks = app.Services.GetRequiredService<DeckRepository>();
        var scans = app.Services.GetRequiredService<ScanService>();
        var queue = app.Services.GetRequiredService<JobQueue>();
        var config = app.Services.GetRequiredService<AppConfig>();
        var users = app.Services.GetRequiredService<UserRepository>();
        var access = app.Services.GetRequiredService<AccessRepository>();
        var binders = app.Services.GetRequiredService<BinderRepository>();

        MapAuth(app, users, access, config);

        app.MapPost("/api/collection", async ctx =>
        {
            var body = await Body<CollectionPost>(ctx);
            if (string.IsNullOrEmpty(body.CardId))
            {
                await ctx.Json(400, new { error = "card_id required" });
                return;
            }
            if (await BinderEndpoints.WriteBinder(ctx, binders) is not { } binder) return;
            await ctx.Json(200, cards.Adjust(binder, ctx.UserId(), body.CardId, body.Delta,
                                             body.Qty, body.Note));
        });

        app.MapPost("/api/collection/bulk", async ctx =>
        {
            var body = await Body<BulkCardsPost>(ctx);
            var list = DeckListParser.Parse(body.Text, body.Cards);
            if (list.Count == 0)
            {
                await ctx.Json(400, new { error = "no cards found" });
                return;
            }

            if (await BinderEndpoints.WriteBinder(ctx, binders) is not { } binder) return;
            await ctx.Json(200, cards.AdjustMany(binder, ctx.UserId(), list, body.Note));
        });

        app.MapPost("/api/decks", async ctx =>
        {
            var body = await Body<DeckPost>(ctx);
            if (string.IsNullOrEmpty(body.LeaderCardId))
            {
                await ctx.Json(400, new { error = "leader_card_id required" });
                return;
            }
            try
            {
                await ctx.Json(200, new
                {
                    deck = decks.Create(ctx.UserId(), body.Name, body.LeaderCardId),
                });
            }
            catch (RuleViolation e)
            {
                await ctx.Json(400, new { error = e.Message });
            }
        });

        app.MapPost("/api/decks/{id:long}/card", async (HttpContext ctx, long id) =>
        {
            var body = await Body<DeckCardPost>(ctx);
            if (string.IsNullOrEmpty(body.CardId))
            {
                await ctx.Json(400, new { error = "card_id required" });
                return;
            }
            DeckDetail? deck;
            try
            {
                deck = decks.SetCard(ctx.UserId(), id, body.CardId, body.Qty);
            }
            catch (RuleViolation e)
            {
                await ctx.Json(400, new { error = e.Message });
                return;
            }
            if (deck is null) { await ctx.Json(404, new { error = "not found" }); return; }
            await ctx.Json(200, new { deck });
        });

        app.MapPost("/api/decks/{id:long}/print", async (HttpContext ctx, long id) =>
        {
            var body = await Body<DeckPrintPost>(ctx);
            if (string.IsNullOrEmpty(body.From) || string.IsNullOrEmpty(body.To))
            {
                await ctx.Json(400, new { error = "from and to required" });
                return;
            }
            DeckDetail? deck;
            try
            {
                deck = decks.SwapPrint(ctx.UserId(), id, body.From, body.To);
            }
            catch (RuleViolation e)
            {
                await ctx.Json(400, new { error = e.Message });
                return;
            }
            if (deck is null) { await ctx.Json(404, new { error = "not found" }); return; }
            await ctx.Json(200, new { deck });
        });

        app.MapPost("/api/decks/{id:long}/cards", async (HttpContext ctx, long id) =>
        {
            var body = await Body<BulkCardsPost>(ctx);
            var list = DeckListParser.Parse(body.Text, body.Cards);
            if (list.Count == 0)
            {
                await ctx.Json(400, new { error = "no cards found" });
                return;
            }

            DeckDetail? deck;
            try
            {
                deck = decks.SetCards(ctx.UserId(), id, list);
            }
            catch (RuleViolation e)
            {
                await ctx.Json(400, new { error = e.Message });
                return;
            }

            if (deck is null) { await ctx.Json(404, new { error = "not found" }); return; }
            await ctx.Json(200, new { deck });
        });

        app.MapPost("/api/decks/{id:long}/delete", async (HttpContext ctx, long id) =>
        {
            decks.Delete(ctx.UserId(), id);
            await ctx.Json(200, new { ok = true });
        });

        app.MapPost("/api/decks/{id:long}", async (HttpContext ctx, long id) =>
        {
            var body = await Body<DeckPost>(ctx);
            DeckDetail? deck;
            try
            {
                deck = decks.Update(ctx.UserId(), id, body.Name, body.LeaderCardId);
            }
            catch (RuleViolation e)
            {
                await ctx.Json(400, new { error = e.Message });
                return;
            }
            if (deck is null) { await ctx.Json(404, new { error = "not found" }); return; }
            await ctx.Json(200, new { deck });
        });

        app.MapPost("/api/scan", async ctx =>
        {
            var body = await Body<ScanPost>(ctx);
            if ((body.Variants ?? new()).Count == 0 && string.IsNullOrEmpty(body.Image))
            {
                await ctx.Json(400, new { error = "image required" });
                return;
            }

            if (!config.ScansAsync)
            {
                await ctx.Json(200, await scans.Read(ctx.UserId(), body));
                return;
            }

            // Two attempts: one retry covers a worker that died mid-read, and a
            // photo that could not be read twice will not be read a third time.
            var id = queue.Enqueue(JobTypes.RunOcrScan, body, correlationId: ctx.TraceIdentifier,
                                   userId: ctx.UserId(), maxAttempts: 2, priority: JobPriority.Scan);
            await ctx.Json(202, new { scan_id = id, status = "pending" });
        });

        app.MapGet("/api/scan/{id:long}", async (HttpContext ctx, long id) =>
        {
            // Someone else's scan is reported the same as no scan at all.
            var job = queue.Get(id);
            if (job is null || job.Type != JobTypes.RunOcrScan || job.UserId != ctx.UserId())
            {
                await ctx.Json(404, new { error = "not found" });
                return;
            }
            object reply = job.Status switch
            {
                "done" => new
                {
                    status = "complete",
                    result = JsonSerializer.Deserialize<JsonElement>(job.Result ?? "{}"),
                },
                "failed" => new { status = "failed", error = "That scan could not be read." },
                _ => new { status = "pending" },
            };
            await ctx.Json(200, reply);
        });

        app.MapPost("/api/reset", async ctx =>
        {
            if (await BinderEndpoints.WriteBinder(ctx, binders) is not { } binder) return;
            cards.ResetCollection(binder);
            await ctx.Json(200, new { ok = true });
        });
    }

    /// <summary>
    /// Sign in, sign out, and the invite-gated way to get an account in the first
    /// place. These are POSTs the Auth middleware lets through unauthenticated, so
    /// each one does its own throttling.
    /// </summary>
    static void MapAuth(WebApplication app, UserRepository users, AccessRepository access,
                        AppConfig config)
    {
        var throttle = app.Services.GetRequiredService<ILoginThrottle>();
        app.MapPost("/api/auth/login", async ctx =>
        {
            var who = ctx.ClientIp(config.BehindProxy);
            if (await throttle.RetryAfter(who) is { } wait)
            {
                Telemetry.AuthAttempt("login", "throttled");
                ctx.Response.Headers["Retry-After"] = wait.ToString();
                await ctx.Json(429, new
                {
                    error = $"Too many attempts. Try again in {Minutes(wait)}.",
                });
                return;
            }

            var body = await Body<LoginPost>(ctx);
            var user = users.Authenticate(body.Username, body.Password);
            if (user is null)
            {
                await throttle.Failed(who);
                Telemetry.AuthAttempt("login", "refused");
                // One message for both halves: saying which was wrong tells an
                // attacker which usernames exist.
                await ctx.Json(401, new { error = "That username and password do not match." });
                return;
            }

            await throttle.Succeeded(who);
            Telemetry.AuthAttempt("login", "ok");
            ctx.SetSessionCookie(users.StartSession(user.Id));
            await ctx.Json(200, new
            {
                ok = true,
                user = new SessionInfo { Username = user.Username, Owner = user.IsOwner },
            });
        });

        // Two ways to hold an invitation, and a server may offer either, both or
        // neither. The shared code from MANIFEST_INVITE_CODE is the old one: one
        // secret, everybody types the same thing, and it stays because a private
        // server on a home network wants nothing more than that. The other is a
        // token minted for one address by an approved access request, which is what
        // makes a server on the public internet safe to leave running.
        app.MapPost("/api/auth/register", async ctx =>
        {
            var who = ctx.ClientIp(config.BehindProxy);
            if (await throttle.RetryAfter(who) is { } wait)
            {
                Telemetry.AuthAttempt("register", "throttled");
                ctx.Response.Headers["Retry-After"] = wait.ToString();
                await ctx.Json(429, new
                {
                    error = $"Too many attempts. Try again in {Minutes(wait)}.",
                });
                return;
            }

            var body = await Body<RegisterPost>(ctx);
            var offered = (body.Invite ?? "").Trim();

            var invite = access.ByInviteToken(offered);
            var shared = AppConfig.InviteCode is { } expected && SameSecret(offered, expected);

            if (invite is null && !shared)
            {
                // A wrong invite counts against the same allowance as a wrong
                // password, so neither can be guessed any faster than the other.
                await throttle.Failed(who);
                Telemetry.AuthAttempt("register", "refused");
                await ctx.Json(403, new { error = WhyNot(offered) });
                return;
            }

            var username = body.Username ?? "";
            var password = body.Password ?? "";

            // Everything that can be judged without spending the invite is judged
            // first. A mistyped password should not cost someone the only link they
            // have, and there is no way to hand it back to them once it is gone.
            if (Passwords.RejectUsername(username) is { } badName)
            {
                await ctx.Json(400, new { error = badName });
                return;
            }
            if (Passwords.RejectPassword(password) is { } badPass)
            {
                await ctx.Json(400, new { error = badPass });
                return;
            }
            if (users.ByName(username) is not null)
            {
                await ctx.Json(400, new { error = "That username is taken." });
                return;
            }

            // Spend before creating. Two browsers following the same link both get
            // this far, and only one of them can win a conditional update - whereas
            // both would win a check that ran before the account was made.
            if (invite is not null && !access.Spend(offered))
            {
                await ctx.Json(403, new
                {
                    error = "That invite link has already been used. Ask for access "
                            + "again if you need a new one.",
                });
                return;
            }

            try
            {
                var user = users.Create(username, password, invite?.Email);
                if (invite is not null) access.Attach(invite.Id, user.Id);

                await throttle.Succeeded(who);
                Telemetry.AuthAttempt("register", "ok");
                ctx.SetSessionCookie(users.StartSession(user.Id));
                await ctx.Json(200, new
                {
                    ok = true,
                    user = new SessionInfo
                    {
                        Username = user.Username,
                        Email = user.Email,
                        Owner = user.IsOwner,
                    },
                });
            }
            catch (AccountError e)
            {
                // The checks above cover every ordinary mistake, so landing here
                // means something changed underneath: the username was taken in the
                // last few milliseconds, or the address already has an account. Put
                // the invite back rather than leaving a dead link and no account.
                if (invite is not null) access.Unspend(invite.Id, offered);
                await ctx.Json(400, new { error = e.Message });
            }
        });

        app.MapPost("/api/auth/logout", async ctx =>
        {
            users.EndSession(ctx.Request.Cookies[Auth.CookieName]);
            ctx.ClearSessionCookie();
            await ctx.Json(200, new { ok = true });
        });
    }

    static string Minutes(int seconds) =>
        seconds < 90 ? $"{seconds} seconds" : $"{Math.Ceiling(seconds / 60.0)} minutes";

    /// <summary>
    /// Why an invite was refused, in terms of what the person can do next. A blank
    /// one is somebody who found the form; a wrong one is nearly always a link that
    /// has already been spent or has run out, because nobody guesses 256 bits.
    /// </summary>
    static string WhyNot(string offered)
    {
        if (offered.Length > 0)
            return "That invite is not valid. A sign-up link works once and expires "
                   + "after a week"
                   + (AppConfig.RequestAccessOpen
                       ? " - ask for access again and a fresh one will be sent."
                       : " - ask whoever runs this server for another.");

        if (AppConfig.RequestAccessOpen)
            return "New accounts here are by invitation. Use \"Ask for access\" on the "
                   + "sign-in page and you will get a link by email.";

        return "This server is not taking new accounts. Ask whoever runs it.";
    }

    /// <summary>
    /// Compares in time that does not depend on how much of the secret is right,
    /// so the invite code cannot be recovered a character at a time. Both sides are
    /// hashed first because the fixed-time comparison needs equal lengths, and the
    /// length of the real code is itself something worth not leaking.
    /// </summary>
    static bool SameSecret(string? given, string expected)
    {
        var a = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes((given ?? "").Trim()));
        var b = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(expected));
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b);
    }

    /// <summary>
    /// The cursor when the caller asked for the paged shape, which it does by sending
    /// a cursor at all - empty for the first page. Callers that send none get the
    /// old unpaged response, so a phone with yesterday's page cached keeps working.
    /// </summary>
    static string? Paged(HttpContext ctx) =>
        ctx.Request.Query.TryGetValue("cursor", out var c) ? c.ToString() : null;

    /// <summary>
    /// owned=1 is owned only and owned=0 not owned only; colors is a comma list any
    /// one of which may match. Only read in the paged shape: the old one treated
    /// owned=0 as "no filter", and still does.
    /// </summary>
    static CardFilter Filter(IQueryCollection q) => new()
    {
        Q = q["q"].FirstOrDefault(),
        Category = Blank(q["category"].FirstOrDefault()),
        ExcludeCategory = Blank(q["exclude_category"].FirstOrDefault()),
        Colors = (q["colors"].FirstOrDefault() ?? q["color"].FirstOrDefault() ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        Rarity = Blank(q["rarity"].FirstOrDefault()),
        SetLabel = Blank(q["set"].FirstOrDefault()),
        Owned = q["owned"].FirstOrDefault() switch { "1" => true, "0" => false, _ => null },
        GroupPrints = q["group"].FirstOrDefault() == "prints",
    };

    static string? Blank(string? s) => string.IsNullOrEmpty(s) ? null : s;

    static async Task<T> Body<T>(HttpContext ctx) where T : new()
    {
        if (ctx.Request.ContentLength is null or 0) return new T();
        var parsed = await JsonSerializer.DeserializeAsync<T>(
            ctx.Request.Body, Manifest.Json.Options);
        return parsed ?? new T();
    }
}
