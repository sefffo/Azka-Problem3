namespace Azka.Services.Implementation.Email;

/// <summary>
/// A plain data record describing an email to send.
/// Queued by HTTP-thread services and executed by the background worker
/// using its own DI scope — avoids capturing a disposed scoped IEmailService.
/// </summary>
public sealed record EmailJobDescriptor(
    string To,
    string Subject,
    string Body,
    bool IsHtml = true);
