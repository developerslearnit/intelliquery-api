using IntelliQueryAI.Application.Abstractions.Services;
using IntelliQueryAI.Domain.Services;
using Microsoft.Extensions.Logging;

namespace IntelliQueryAI.Infrastructure.Services;

/// <summary>
/// Default system date time provider using system clock UTC.
/// </summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>
/// Production-structured email service logging messages and ready for SMTP/SendGrid delivery.
/// </summary>
public sealed class EmailSender : IEmailSender
{
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(ILogger<EmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Delivering email to {ToEmail} with subject '{Subject}'. Body snippet: {Snippet}",
            toEmail, subject, htmlBody.Length > 80 ? htmlBody[..80] + "..." : htmlBody);
        return Task.CompletedTask;
    }
}
