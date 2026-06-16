using BlazorWebForms.Core.Abstractions;

namespace BlazorWebForms.Infrastructure.SqlServer.Integrations;

internal sealed class EmailIntegrationRouter(
    BlazorWebFormsSqlServerOptions options,
    SmtpEmailIntegration smtp,
    SendGridEmailIntegration sendGrid,
    GraphEmailIntegration graph,
    DryRunEmailIntegration dryRun) : IEmailIntegration
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        return GetActiveIntegration().SendAsync(to, subject, body, cancellationToken);
    }

    private IEmailIntegration GetActiveIntegration()
    {
        return options.EmailProviderStrategy.Trim().ToLowerInvariant() switch
        {
            "smtp" => smtp,
            "sendgrid" => sendGrid,
            "graph" => graph,
            _ => dryRun
        };
    }
}
