using Manifest.Data;
using Manifest.Models;
using Manifest.Services;
using Manifest.Services.Jobs;

namespace Manifest.Web;

/// <summary>
/// The request-access flow, end to end: a stranger leaves an address, the admin is
/// mailed a link to a review page, and approving mails the stranger a link that
/// makes exactly one account.
///
/// Kept apart from Endpoints because every route here is reachable without a session
/// - the whole point is that the caller has no account yet - and a file where that
/// is true of every route is easier to keep honest than a handful of exceptions
/// scattered through one where it is true of none.
/// </summary>
public static class AccessEndpoints
{
    /// <summary>
    /// Answered to every well-formed request, whatever actually happened: filed,
    /// ignored as a duplicate, ignored because there is already an account, or
    /// ignored because it was refused last week. Anything more specific would turn
    /// this form into a way to ask which addresses are known here.
    /// </summary>
    const string Acknowledgement =
        "Thanks — if that address can be given access, whoever runs this server will "
        + "be in touch by email. There is nothing else to do here.";

    public static void Map(WebApplication app)
    {
        var access = app.Services.GetRequiredService<AccessRepository>();
        var users = app.Services.GetRequiredService<UserRepository>();
        var mailer = app.Services.GetRequiredService<Mailer>();
        var outbox = app.Services.GetRequiredService<Outbox>();
        var config = app.Services.GetRequiredService<AppConfig>();
        var clock = app.Services.GetRequiredService<IClock>();

        MapPublic(app, access, outbox, config, clock);
        MapOwner(app, access, mailer, outbox, clock);
        MapPages(app, access, users);
    }

    // ---- what a stranger can reach

    static void MapPublic(WebApplication app, AccessRepository access, Outbox outbox,
                          AppConfig config, IClock clock)
    {
        var throttle = app.Services.GetRequiredService<ILoginThrottle>();
        app.MapPost("/api/access/request", async ctx =>
        {
            if (!AppConfig.RequestAccessOpen)
            {
                await ctx.Json(403, new
                {
                    error = "This server is not taking requests. Ask whoever runs it directly.",
                });
                return;
            }

            // Counted against the same allowance as a wrong password, from the same
            // address. Filing a request costs the server an outgoing email, so it is
            // worth at least as much rate limiting as a sign-in attempt.
            var who = "access:" + ctx.ClientIp(config.BehindProxy);
            if (await throttle.RetryAfter(who) is { } wait)
            {
                ctx.Response.Headers["Retry-After"] = wait.ToString();
                await ctx.Json(429, new { error = $"Too many requests. Try again in {Minutes(wait)}." });
                return;
            }

            var body = await Read<AccessRequestPost>(ctx);
            if (AccessRepository.RejectEmail(body.Email) is { } bad)
            {
                await throttle.Failed(who);
                await ctx.Json(400, new { error = bad });
                return;
            }

            await throttle.Failed(who);

            var submission = access.Submit(body.Email!, body.Note,
                                           ctx.ClientIp(config.BehindProxy));
            await Announce(submission, outbox, BaseUrl(ctx), ctx.TraceIdentifier);

            await ctx.Json(200, new { ok = true, message = Acknowledgement });
        });

        // What the review page loads to show the admin what they are deciding about.
        // Reading is safe for a link scanner to do; deciding is not, which is why it
        // is a separate POST.
        app.MapGet("/api/access/review", async ctx =>
        {
            var found = access.ByActionToken(ctx.Request.Query["t"]);
            if (found is null)
            {
                await ctx.Json(404, new
                {
                    error = "That link is not valid any more. It may have been used, "
                            + "expired, or the request may already have been decided. "
                            + "Sign in and open /admin to see where it stands.",
                });
                return;
            }
            await ctx.Json(200, new { request = AccessRequestView.Of(found, clock) });
        });

        app.MapPost("/api/access/decide", async ctx =>
        {
            var body = await Read<AccessDecisionPost>(ctx);
            var found = access.ByActionToken(body.Token);
            if (found is null)
            {
                await ctx.Json(404, new { error = "That link is not valid any more." });
                return;
            }

            var approving = string.Equals(body.Decision, "approve", StringComparison.OrdinalIgnoreCase);
            if (!approving && !string.Equals(body.Decision, "deny", StringComparison.OrdinalIgnoreCase))
            {
                await ctx.Json(400, new { error = "decision must be approve or deny" });
                return;
            }

            var outcome = approving
                ? await ApproveAndMail(access, outbox, found.Id, "email link",
                                       BaseUrl(ctx), clock, ctx.TraceIdentifier)
                : Denied(access.Deny(found.Id, "email link"), clock);

            await ctx.Json(outcome.Ok ? 200 : 409, outcome.Payload);
        });

        // Lets the sign-up page say who the invite is for before anything is typed,
        // and lets it fail early and clearly on a link that has already been spent.
        app.MapGet("/api/access/invite", async ctx =>
        {
            var found = access.ByInviteToken(ctx.Request.Query["t"]);
            if (found is null)
            {
                await ctx.Json(404, new
                {
                    error = "That invite link is not valid any more. It may have been "
                            + "used already, or it may have expired. Ask for access "
                            + "again and a fresh one will be sent.",
                });
                return;
            }
            await ctx.Json(200, new { ok = true, email = found.Email });
        });
    }

    // ---- what the owner can reach, signed in

    static void MapOwner(WebApplication app, AccessRepository access, Mailer mailer, Outbox outbox,
                         IClock clock)
    {
        app.MapGet("/api/access/requests", async ctx =>
        {
            if (!IsOwner(ctx)) { await Forbid(ctx); return; }
            var status = Blank(ctx.Request.Query["status"]);
            await ctx.Json(200, new
            {
                requests = access.List(status).Select(r => AccessRequestView.Of(r, clock)),
                pending = access.PendingCount(),
                mail = new
                {
                    configured = mailer.Configured,
                    to = AppConfig.AdminEmail,
                    missing = mailer.Settings.WhatIsMissing(),
                },
            });
        });

        // Also the way to re-send a link that expired: approving something already
        // approved mints a fresh token and mails it again.
        app.MapPost("/api/access/requests/{id:long}/approve", async (HttpContext ctx, long id) =>
        {
            if (!IsOwner(ctx)) { await Forbid(ctx); return; }
            var outcome = await ApproveAndMail(access, outbox, id, ctx.User()!.Username,
                                               BaseUrl(ctx), clock, ctx.TraceIdentifier);
            await ctx.Json(outcome.Ok ? 200 : 409, outcome.Payload);
        });

        app.MapPost("/api/access/requests/{id:long}/deny", async (HttpContext ctx, long id) =>
        {
            if (!IsOwner(ctx)) { await Forbid(ctx); return; }
            var outcome = Denied(access.Deny(id, ctx.User()!.Username), clock);
            await ctx.Json(outcome.Ok ? 200 : 409, outcome.Payload);
        });
    }

    // ---- the two pages that are not the app

    static void MapPages(WebApplication app, AccessRepository access, UserRepository users)
    {
        // The sign-up form an invite link opens. It is the sign-in page in another
        // mode rather than a page of its own, so there is one place where a username
        // and a password are typed and one place to get that right.
        app.MapGet("/register", async ctx =>
            await ctx.Html(200, LoginPage.Html));

        app.MapGet("/access/review", async ctx =>
            await ctx.Html(200, AccessPages.Review));

        app.MapGet("/admin", async ctx =>
        {
            // Not the API's 401-and-let-the-UI-redirect: this is a page someone
            // followed a link to, so it should look like the sign-in page rather
            // than a JSON error.
            if (ctx.User() is null)
            {
                await ctx.Html(200, LoginPage.Html);
                return;
            }
            if (!IsOwner(ctx))
            {
                await ctx.Html(403, AccessPages.NotYours);
                return;
            }
            await ctx.Html(200, AccessPages.Admin);
        });
    }

    // ---- the shared middle

    sealed record Acted(bool Ok, object Payload);

    /// <summary>
    /// Approves and mails, wherever the decision came from. Both callers - the link
    /// in the admin's mail and the button on the admin page - land here, so there is
    /// one description of what approving means.
    /// </summary>
    static async Task<Acted> ApproveAndMail(AccessRepository access, Outbox outbox,
                                              long id, string decidedBy, string baseUrl,
                                              IClock clock, string? correlationId)
    {
        var approved = access.Approve(id, decidedBy);
        if (approved is not ({ } request, { } token))
            return new Acted(false, new
            {
                error = "That request cannot be approved - it has already been used to "
                        + "make an account.",
            });

        var mail = AccessMail.ForApplicant(request, baseUrl, token);
        var sent = await outbox.Send(request.Email, mail.Subject, mail.Text, mail.Html, correlationId);

        // The approval stands even if the mail bounced: the row is the decision, the
        // mail is only how it travels. The admin page says which happened so a failed
        // send can be retried rather than silently believed.
        return new Acted(true, new
        {
            ok = true,
            decision = "approved",
            email = request.Email,
            mail = sent,
            request = AccessRequestView.Of(request, clock),
        });
    }

    /// <summary>
    /// Denying sends nothing. Telling someone they were refused confirms the address
    /// reached a real server and invites an argument the admin did not sign up for;
    /// a request that is never answered is the quieter no.
    /// </summary>
    static Acted Denied(AccessRequest? denied, IClock clock) =>
        denied is null
            ? new Acted(false, new
            {
                error = "That request cannot be denied - it has already been used to "
                        + "make an account.",
            })
            : new Acted(true, new
            {
                ok = true,
                decision = "denied",
                email = denied.Email,
                mail = "none",
                request = AccessRequestView.Of(denied, clock),
            });

    /// <summary>
    /// Mails the admin about a new request, or re-mails an applicant whose invite is
    /// still live and who has asked again - which nearly always means the first mail
    /// went to spam.
    /// </summary>
    static async Task Announce(AccessRepository.Submission submission, Outbox outbox,
                               string baseUrl, string? correlationId)
    {
        switch (submission)
        {
            case { Outcome: AccessRepository.Outcome.Filed, Request: { } request,
                   ActionToken: { } token }:
            {
                var to = AppConfig.AdminEmail;
                if (to is null) return;
                var mail = AccessMail.ForAdmin(request, baseUrl, token);
                await outbox.Send(to, mail.Subject, mail.Text, mail.Html, correlationId);
                return;
            }
            case { Outcome: AccessRepository.Outcome.InviteResent, Request: { } request,
                   InviteToken: { } token }:
            {
                var mail = AccessMail.ForApplicant(request, baseUrl, token);
                await outbox.Send(request.Email, mail.Subject, mail.Text, mail.Html, correlationId);
                return;
            }
        }
    }

    /// <summary>
    /// What to put in a link that has to work from someone's mailbox. The configured
    /// public URL wins; without one the request's own host is used, which is safe
    /// only because Guard has already refused any Host this server does not answer
    /// to - otherwise a stranger could post a request and choose where the admin's
    /// approve link pointed.
    /// </summary>
    public static string BaseUrl(HttpContext ctx)
    {
        if (AppConfig.PublicUrl is { } configured) return configured;
        var scheme = ctx.IsSecure() ? "https" : "http";
        return $"{scheme}://{ctx.Request.Host}";
    }

    static bool IsOwner(HttpContext ctx) => ctx.User()?.IsOwner == true;

    static Task Forbid(HttpContext ctx) => ctx.Json(403, new
    {
        error = "Only the owner account can see or decide access requests.",
    });

    static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    static string Minutes(int seconds) =>
        seconds < 90 ? $"{seconds} seconds" : $"{Math.Ceiling(seconds / 60.0)} minutes";

    static async Task<T> Read<T>(HttpContext ctx) where T : new()
    {
        if (ctx.Request.ContentLength is null or 0) return new T();
        var parsed = await System.Text.Json.JsonSerializer.DeserializeAsync<T>(
            ctx.Request.Body, Manifest.Json.Options);
        return parsed ?? new T();
    }
}
