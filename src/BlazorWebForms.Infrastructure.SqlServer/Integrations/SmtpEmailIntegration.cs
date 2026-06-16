using System.Net;
using System.Net.Mail;
using BlazorWebForms.Core.Abstractions;

namespace BlazorWebForms.Infrastructure.SqlServer.Integrations;

internal sealed class SmtpEmailIntegration(BlazorWebFormsSqlServerOptions options) : IEmailIntegration
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.SmtpHost))
        {
            throw new InvalidOperationException("SMTP host is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.EmailFromAddress))
        {
            throw new InvalidOperationException("SMTP from address is not configured.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(options.EmailFromAddress, options.EmailFromDisplayName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(to));

        using var client = new SmtpClient(options.SmtpHost, options.SmtpPort)
        {
            EnableSsl = options.SmtpEnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrWhiteSpace(options.SmtpUsername))
        {
            client.Credentials = new NetworkCredential(options.SmtpUsername, options.SmtpPassword ?? string.Empty);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
    }
}
