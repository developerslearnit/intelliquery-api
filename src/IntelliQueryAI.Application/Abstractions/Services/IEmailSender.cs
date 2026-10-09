namespace IntelliQueryAI.Application.Abstractions.Services;

/// <summary>
/// Service abstraction for delivering notification, verification, and reset emails.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Asynchronously delivers an email message.
    /// </summary>
    Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
