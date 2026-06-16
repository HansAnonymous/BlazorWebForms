using BlazorWebForms.Core.Abstractions;

namespace BlazorWebForms.Infrastructure.SqlServer.Integrations;

internal sealed class DryRunEmailIntegration : IEmailIntegration
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(to))
        {
            throw new InvalidOperationException("Email recipient is required.");
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new InvalidOperationException("Email subject is required.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new InvalidOperationException("Email body is required.");
        }

        return Task.CompletedTask;
    }
}
