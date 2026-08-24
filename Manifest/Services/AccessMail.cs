using System.Net;
using Manifest.Data;
using Manifest.Models;

namespace Manifest.Services;

/// <summary>
/// The two messages the access flow sends. Written as plain text with an HTML twin
/// rather than HTML alone: a text part is what makes the difference between landing
/// in a mailbox and landing in a spam folder for a domain nobody has heard of, which
/// a freshly deployed hobby server always is.
/// </summary>
public static class AccessMail
{
    public sealed record Message(string Subject, string Text, string Html);

    /// <summary>
    /// Sent to the admin when someone asks. The review link carries the action token;
    /// it opens a page that shows the request, and nothing is decided until a button
    /// on that page is pressed - so a mail client prefetching the link cannot let
    /// anyone in.
    /// </summary>
    public static Message ForAdmin(AccessRequest request, string baseUrl, string reviewToken)
    {
        var review = $"{baseUrl}/access/review?t={Uri.EscapeDataString(reviewToken)}";
        var admin = $"{baseUrl}/admin";
        var said = string.IsNullOrWhiteSpace(request.Note)
            ? "They did not leave a note."
            : $"They said:\n\n    {request.Note.Replace("\n", "\n    ")}";

        var text = $"""
            {request.Email} has asked for an account on Manifest.

            {said}

            Asked at : {request.CreatedAt} UTC
            Request  : #{request.Id}

            Review it here:

                {review}

            That page shows the request and gives you Approve and Deny. Approving
            emails them a sign-up link that works once and expires after
            {(int)AccessRepository.InviteLife.TotalDays} days. Denying is silent -
            they are not told either way.

            Every request, past and present, is at {admin}
            """;

        var html = $"""
            <p style="margin:0 0 14px"><strong>{Escape(request.Email)}</strong> has asked for
            an account on Manifest.</p>
            {NoteHtml(request.Note)}
            <p style="margin:0 0 6px;color:#555;font-size:14px">
              Asked at {Escape(request.CreatedAt)} UTC &middot; request #{request.Id}
            </p>
            <p style="margin:22px 0">
              <a href="{Escape(review)}"
                 style="background:#E3A63A;color:#071A20;text-decoration:none;font-weight:700;
                        padding:12px 20px;border-radius:8px;display:inline-block">Review this request</a>
            </p>
            <p style="margin:0 0 14px;color:#555;font-size:14px">
              That page shows the request and gives you Approve and Deny. Approving emails
              them a sign-up link that works once and expires after
              {(int)AccessRepository.InviteLife.TotalDays} days. Denying is silent — they are
              not told either way.
            </p>
            <p style="margin:0;color:#555;font-size:14px">
              Every request, past and present, is at <a href="{Escape(admin)}">{Escape(admin)}</a>
            </p>
            """;

        return new Message($"Manifest — access request from {request.Email}", text, html);
    }

    /// <summary>
    /// Sent to the applicant once the admin says yes. The address is not asked for
    /// again on the sign-up page - it is the one this went to - so the invite cannot
    /// be pointed at a different mailbox by whoever ends up holding the link.
    /// </summary>
    public static Message ForApplicant(AccessRequest request, string baseUrl, string inviteToken)
    {
        var link = $"{baseUrl}/register?invite={Uri.EscapeDataString(inviteToken)}";
        var days = (int)AccessRepository.InviteLife.TotalDays;

        var text = $"""
            You asked for an account on Manifest, and it has been approved.

            Create your account here:

                {link}

            The link works once and stops working after {days} days. You choose the
            username and the password; the account is tied to this address,
            {request.Email}.

            If you did not ask for this, there is nothing to do - no account exists
            until someone follows that link, and it will quietly expire.
            """;

        var html = $"""
            <p style="margin:0 0 14px">You asked for an account on
            <strong>Manifest</strong>, and it has been approved.</p>
            <p style="margin:22px 0">
              <a href="{Escape(link)}"
                 style="background:#E3A63A;color:#071A20;text-decoration:none;font-weight:700;
                        padding:12px 20px;border-radius:8px;display:inline-block">Create your account</a>
            </p>
            <p style="margin:0 0 14px;color:#555;font-size:14px">
              The link works once and stops working after {days} days. You choose the username
              and the password; the account is tied to this address,
              <strong>{Escape(request.Email)}</strong>.
            </p>
            <p style="margin:0;color:#555;font-size:14px">
              If you did not ask for this, there is nothing to do — no account exists until
              someone follows that link, and it will quietly expire.
            </p>
            """;

        return new Message("Manifest — your access request was approved", text, html);
    }

    static string NoteHtml(string note) =>
        string.IsNullOrWhiteSpace(note)
            ? "<p style=\"margin:0 0 14px;color:#555;font-size:14px\">They did not leave a note.</p>"
            : $"""
               <blockquote style="margin:0 0 14px;padding:10px 14px;border-left:3px solid #E3A63A;
                                  background:#faf6ee;color:#333;white-space:pre-wrap">{Escape(note)}</blockquote>
               """;

    /// <summary>
    /// The note and the address are typed by a stranger and end up in the admin's
    /// mail client, which renders HTML. Escaping is the whole defence.
    /// </summary>
    static string Escape(string s) => WebUtility.HtmlEncode(s);
}
