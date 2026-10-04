using Manifest.Data;

namespace Manifest.Services.Jobs;

/// <summary>
/// Mail goes out from a background job, so a slow or unreachable SMTP server never
/// holds up the request that wanted to send something. With no worker at all
/// (MANIFEST_WORKER_MODE=disabled) it is sent in the request, as it used to be,
/// rather than queued for nobody.
/// </summary>
public sealed class Outbox(JobQueue queue, Mailer mailer, AppConfig config)
{
    /// <returns>
    /// What the caller can tell the person who triggered it: "queued" (on its way),
    /// "logged" (no SMTP - it is printed on the server console instead), or, sending
    /// directly, "sent" or "failed".
    /// </returns>
    public async Task<string> Send(string to, string subject, string text, string? html,
                                   string? correlationId)
    {
        if (config.WorkerMode == "disabled")
            return (await mailer.Send(to, subject, text, html)).ToString().ToLowerInvariant();

        queue.Enqueue(JobTypes.SendEmail, new EmailPayload(to, subject, text, html),
                      correlationId: correlationId, priority: JobPriority.Mail);
        return mailer.Configured ? "queued" : "logged";
    }
}
