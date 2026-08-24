using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Manifest.Services;

/// <summary>What became of a message. The caller decides how much to care.</summary>
public enum MailOutcome
{
    /// <summary>Handed to the SMTP server, which accepted it.</summary>
    Sent,

    /// <summary>No SMTP configured, so it went to the console instead.</summary>
    Logged,

    /// <summary>SMTP was configured and refused, timed out, or was unreachable.</summary>
    Failed,
}

/// <summary>
/// Where mail goes, read from the environment. Plain SMTP rather than one provider's
/// HTTP API on purpose: Fastmail, Gmail, Resend, Mailgun, Postmark and a box running
/// Postfix all speak it, so deploying this does not mean picking a vendor.
/// </summary>
public sealed class MailSettings
{
    public string? Host { get; init; }
    public int Port { get; init; } = 587;
    public string? User { get; init; }
    public string? Password { get; init; }
    public string? From { get; init; }
    public string FromName { get; init; } = "Manifest";
    public SecureSocketOptions Security { get; init; } = SecureSocketOptions.Auto;

    /// <summary>Where a new request is announced. Without it, nothing is announced.</summary>
    public string? AdminTo { get; init; }

    /// <summary>There is a server to talk to and an address to claim to be.</summary>
    public bool Configured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);

    public static MailSettings FromEnvironment()
    {
        var host = Env("MANIFEST_SMTP_HOST");
        var user = Env("MANIFEST_SMTP_USER");

        // 587 is STARTTLS and 465 is TLS from the first byte. Getting these the wrong
        // way round is the single commonest way SMTP fails to connect at all, so the
        // port picks the default and MANIFEST_SMTP_SECURITY overrides it only if
        // someone has a server that does something unusual.
        var port = int.TryParse(Env("MANIFEST_SMTP_PORT"), out var p) ? p : 587;
        var security = (Env("MANIFEST_SMTP_SECURITY") ?? "").Trim().ToLowerInvariant() switch
        {
            "ssl" or "tls" or "implicit" => SecureSocketOptions.SslOnConnect,
            "starttls" => SecureSocketOptions.StartTls,
            "none" or "plain" => SecureSocketOptions.None,
            _ => port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls,
        };

        return new MailSettings
        {
            Host = host,
            Port = port,
            User = user,
            Password = Env("MANIFEST_SMTP_PASSWORD"),
            // Most providers want the envelope sender to be the account that
            // authenticated, so the username is the right guess when it is an address.
            From = Env("MANIFEST_SMTP_FROM") ?? (user?.Contains('@') == true ? user : null),
            FromName = Env("MANIFEST_SMTP_FROM_NAME") ?? "Manifest",
            Security = security,
            AdminTo = Env("MANIFEST_ADMIN_EMAIL"),
        };
    }

    static string? Env(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// What is missing, in the order it stops things working, for the startup banner
    /// to say out loud. A server that silently cannot mail anyone is a server whose
    /// request form is a black hole.
    /// </summary>
    public string? WhatIsMissing()
    {
        if (string.IsNullOrWhiteSpace(AdminTo)) return "MANIFEST_ADMIN_EMAIL";
        if (string.IsNullOrWhiteSpace(Host)) return "MANIFEST_SMTP_HOST";
        if (string.IsNullOrWhiteSpace(From)) return "MANIFEST_SMTP_FROM";
        return null;
    }
}

/// <summary>
/// Sends the two messages this app has any business sending. When SMTP is not set up
/// they are written to the console instead of thrown away, so a server can be stood
/// up and the whole flow walked through - links and all - before anyone has decided
/// which mail provider to pay.
/// </summary>
public sealed class Mailer
{
    readonly MailSettings _settings;
    public Mailer(MailSettings settings) => _settings = settings;

    public MailSettings Settings => _settings;
    public bool Configured => _settings.Configured;

    /// <summary>
    /// Long enough for a slow provider's TLS handshake and greylisting pause, short
    /// enough that a dead SMTP host cannot hold a web request open indefinitely.
    /// </summary>
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    public async Task<MailOutcome> Send(string to, string subject, string body,
                                        string? htmlBody = null)
    {
        if (!_settings.Configured)
        {
            Log(to, subject, body);
            return MailOutcome.Logged;
        }

        // Configured is exactly the check that these two are present; the locals are
        // what tells the compiler so.
        var host = _settings.Host!;
        var from = _settings.From!;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { TextBody = body, HtmlBody = htmlBody }.ToMessageBody();

        using var cancel = new CancellationTokenSource(Timeout);
        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(host, _settings.Port, _settings.Security,
                                      cancel.Token);
            if (!string.IsNullOrEmpty(_settings.User))
                await client.AuthenticateAsync(_settings.User, _settings.Password ?? "",
                                               cancel.Token);
            await client.SendAsync(message, cancel.Token);
            await client.DisconnectAsync(true, cancel.Token);
            return MailOutcome.Sent;
        }
        catch (Exception e)
        {
            // Never fatal to the request that triggered it. The row is already in the
            // database, and `manifest access list` is the way through when mail is
            // broken - which is exactly when someone needs a way through.
            Console.Error.WriteLine($"  mail to {to} failed: {e.GetType().Name}: {e.Message}");
            return MailOutcome.Failed;
        }
    }

    /// <summary>
    /// The no-SMTP path. Deliberately loud and deliberately complete: whoever is
    /// watching this console is standing in for the mail server, and a truncated
    /// link would be no use to them.
    /// </summary>
    static void Log(string to, string subject, string body)
    {
        Console.WriteLine();
        Console.WriteLine("  ┌─ mail (no SMTP configured, so it is printed here) ─────────");
        Console.WriteLine($"  │ To      : {to}");
        Console.WriteLine($"  │ Subject : {subject}");
        Console.WriteLine("  │");
        foreach (var line in body.Replace("\r\n", "\n").Split('\n'))
            Console.WriteLine($"  │ {line}");
        Console.WriteLine("  └───────────────────────────────────────────────────────────");
        Console.WriteLine();
    }
}
