using System.Text.Json;
using Manifest.Data;
using Manifest.Tools;

namespace Manifest.Services.Jobs;

/// <summary>
/// A message to send. The access-flow mails carry a single-use link, so the payload
/// is blanked as soon as the job is finished with - sent, or out of attempts - and
/// the token is in the database in the clear only while the mail is in the queue.
/// </summary>
public sealed record EmailPayload(string To, string Subject, string Text, string? Html);

public sealed class SendEmailHandler(Mailer mailer) : IJobHandler
{
    public string Type => JobTypes.SendEmail;
    public bool ForgetPayload => true;

    public async Task<object?> Run(Job job, CancellationToken cancel)
    {
        var mail = JsonSerializer.Deserialize<EmailPayload>(job.Payload, Json.Options)!;
        var outcome = await mailer.Send(mail.To, mail.Subject, mail.Text, mail.Html);
        // A refusal is worth retrying - greylisting, a provider having a bad minute.
        if (outcome == MailOutcome.Failed)
            throw new InvalidOperationException($"the SMTP server did not take the mail to {mail.To}");
        return new { outcome = outcome.ToString().ToLowerInvariant() };
    }
}

public sealed class PurgeSessionsHandler(ISessionRepository sessions) : IJobHandler
{
    public string Type => JobTypes.PurgeExpiredSessions;
    public Task<object?> Run(Job job, CancellationToken cancel) =>
        Task.FromResult<object?>(new { purged = sessions.PurgeExpiredSessions() });
}

public sealed class PurgeAccessTokensHandler(IAccessRepository access) : IJobHandler
{
    public string Type => JobTypes.PurgeExpiredAccessTokens;
    public Task<object?> Run(Job job, CancellationToken cancel) =>
        Task.FromResult<object?>(new { purged = access.PurgeExpiredTokens() });
}

public sealed class PurgeJobsHandler(JobQueue queue) : IJobHandler
{
    public string Type => JobTypes.PurgeFinishedJobs;
    public Task<object?> Run(Job job, CancellationToken cancel) =>
        Task.FromResult<object?>(new { purged = queue.Purge() });
}

/// <summary>Market prices into the database - the same as `manifest refresh-prices`.</summary>
public sealed class RefreshPricesHandler(AppPaths paths, Database db) : IJobHandler
{
    public string Type => JobTypes.RefreshPrices;

    public async Task<object?> Run(Job job, CancellationToken cancel)
    {
        if (await PriceRefresh.Run(paths, db) != 0)
            throw new InvalidOperationException("the price refresh failed; see the worker log");
        return null;
    }
}

/// <summary>
/// A fresh catalog.json from the dataset, then reseeded into the database. The
/// reseed takes the same lock as startup seeding, so a container starting at the
/// same moment waits rather than reading a half-filled catalogue.
/// </summary>
public sealed class RefreshCatalogHandler(AppPaths paths, Database db) : IJobHandler
{
    public string Type => JobTypes.RefreshCatalog;

    public async Task<object?> Run(Job job, CancellationToken cancel)
    {
        if (await CatalogRefresh.Run(paths) != 0)
            throw new InvalidOperationException("the catalogue refresh failed; see the worker log");
        if (db.Initialise(forceReseed: true) is { } error)
            throw new InvalidOperationException(error);
        return null;
    }
}
