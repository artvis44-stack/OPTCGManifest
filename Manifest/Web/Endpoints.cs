using System.Text.Json;
using Manifest.Data;
using Manifest.Models;
using Manifest.Services;

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
        "/api/search", "/api/facets", "/api/card/<card-id>",
        "/api/collection", "/api/collection/bulk", "/api/stats", "/api/export.csv",
        "/api/decks", "/api/decks/<id>", "/api/decks/<id>/card",
        "/api/decks/<id>/cards", "/api/decks/<id>/delete",
    };

    public static void Map(WebApplication app)
    {
        MapGet(app);
        MapPost(app);
        AccessEndpoints.Map(app);

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

        // The one route that answers differently to a stranger: the app for an
        // account, the sign-in page for anyone else.
        async Task Ui(HttpContext ctx)
        {
            if (ctx.User() is null)
            {
                await ctx.Text(200, LoginPage.Html, "text/html; charset=utf-8");
                return;
            }
            await ctx.Send(200, await File.ReadAllBytesAsync(paths.Ui), "text/html; charset=utf-8");
        }

        app.MapGet("/", Ui);
        app.MapGet("/index.html", Ui);

        app.MapGet("/img/{**cardId}", async (HttpContext ctx, string cardId) =>
        {
            var got = await images.Get(cardId.Replace(".png", ""));
            if (got is null)
            {
                await ctx.Send(404, Array.Empty<byte>(), "text/plain");
                return;
            }
            await ctx.Send(200, got.Bytes, "image/png", new[]
            {
                ("Cache-Control", "public, max-age=31536000, immutable"),
                ("X-Cache", got.FromCache ? "hit" : "miss"),
            });
        });

        app.MapGet("/setup", async ctx =>
            await ctx.Text(200, SetupPage.Html, "text/html; charset=utf-8"));

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
            var card = cards.CardDetail(ctx.UserId(), cardId);
            if (card is null) { await ctx.Json(404, new { error = "not found" }); return; }
            await ctx.Json(200, new { card });
        });

        app.MapGet("/api/decks", async ctx =>
            await ctx.Json(200, new { decks = decks.List(ctx.UserId()) }));

        app.MapGet("/api/decks/{id:long}", async (HttpContext ctx, long id) =>
        {
            var deck = decks.Detail(ctx.UserId(), id);
            if (deck is null) { await ctx.Json(404, new { error = "not found" }); return; }
            await ctx.Json(200, new { deck });
        });

        app.MapGet("/api/search", async ctx =>
        {
            var q = ctx.Request.Query;
            var results = cards.Search(
                ctx.UserId(),
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

        app.MapGet("/api/collection", async ctx => await ctx.Json(200, new
        {
            cards = cards.Collection(ctx.UserId()),
            stats = cards.Stats(ctx.UserId()),
        }));

        app.MapGet("/api/stats", async ctx => await ctx.Json(200, cards.Stats(ctx.UserId())));

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
                                                          ctx.RequestAborted);
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
            await ctx.Send(200,
                System.Text.Encoding.UTF8.GetBytes(
                    CsvExport.Write(cards.Collection(ctx.UserId()))),
                "text/csv; charset=utf-8",
                new[] { ("Content-Disposition",
                         "attachment; filename=\"one-piece-manifest.csv\"") }));
    }

    static void MapPost(WebApplication app)
    {
        var cards = app.Services.GetRequiredService<CardRepository>();
        var decks = app.Services.GetRequiredService<DeckRepository>();
        var tesseract = app.Services.GetRequiredService<TesseractScanner>();
        var api = app.Services.GetRequiredService<ApiScanner>();
        var config = app.Services.GetRequiredService<AppConfig>();
        var users = app.Services.GetRequiredService<UserRepository>();
        var access = app.Services.GetRequiredService<AccessRepository>();

        MapAuth(app, users, access, config);

        app.MapPost("/api/collection", async ctx =>
        {
            var body = await Body<CollectionPost>(ctx);
            if (string.IsNullOrEmpty(body.CardId))
            {
                await ctx.Json(400, new { error = "card_id required" });
                return;
            }
            await ctx.Json(200, cards.Adjust(ctx.UserId(), body.CardId, body.Delta,
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

            await ctx.Json(200, cards.AdjustMany(ctx.UserId(), list, body.Note));
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
            var variants = body.Variants ?? new List<string>();
            var img = body.Image;

            if (variants.Count == 0 && string.IsNullOrEmpty(img))
            {
                await ctx.Json(400, new { error = "image required" });
                return;
            }

            // Local OCR first: free, offline, and usually faster.
            if (variants.Count > 0 && tesseract.Available)
            {
                var (cid, note) = tesseract.Read(variants);
                if (cid is null && config.Verbose)
                    // Only on request, and only if what arrived really is a PNG -
                    // this writes caller-supplied bytes to disk.
                    tesseract.DumpFailedScan(variants);

                if (cid is not null)
                {
                    cards.LogScan(ctx.UserId(), cid, "tesseract");
                    var card = cards.Resolve(cid);
                    await ctx.Json(200, new ScanResponse
                    {
                        Ok = true,
                        CardId = cid,
                        Card = card,
                        InCatalog = card is not null,
                        Engine = "tesseract",
                        Confidence = card is not null ? "high" : "low",
                        Note = note,
                    });
                    return;
                }
                if (AppConfig.ApiKey is null)
                {
                    await ctx.Json(200, new ScanResponse
                    {
                        Ok = false,
                        Error = "no_read",
                        Engine = "tesseract",
                        Message = note,
                    });
                    return;
                }
            }

            if (!string.IsNullOrEmpty(img))
            {
                var head = img.Length > 64 ? img[..64] : img;
                if (head.Contains(','))
                    img = img[(img.IndexOf(',') + 1)..];

                var result = await api.Read(ctx.UserId(), img, body.MediaType ?? "image/jpeg");
                result.Engine = "api";
                await ctx.Json(200, result);
                return;
            }

            await ctx.Json(200, new ScanResponse
            {
                Ok = false,
                Error = "no_engine",
                Message = "Scanning needs tesseract installed on this machine. "
                          + "See the README, or type the number instead.",
            });
        });

        app.MapPost("/api/reset", async ctx =>
        {
            cards.ResetCollection(ctx.UserId());
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
        app.MapPost("/api/auth/login", async ctx =>
        {
            var who = ctx.ClientIp(config.BehindProxy);
            if (LoginThrottle.RetryAfter(who) is { } wait)
            {
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
                LoginThrottle.Failed(who);
                // One message for both halves: saying which was wrong tells an
                // attacker which usernames exist.
                await ctx.Json(401, new { error = "That username and password do not match." });
                return;
            }

            LoginThrottle.Succeeded(who);
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
            if (LoginThrottle.RetryAfter(who) is { } wait)
            {
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
                LoginThrottle.Failed(who);
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

                LoginThrottle.Succeeded(who);
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

    static string? Blank(string? s) => string.IsNullOrEmpty(s) ? null : s;

    static async Task<T> Body<T>(HttpContext ctx) where T : new()
    {
        if (ctx.Request.ContentLength is null or 0) return new T();
        var parsed = await JsonSerializer.DeserializeAsync<T>(
            ctx.Request.Body, Manifest.Json.Options);
        return parsed ?? new T();
    }
}
