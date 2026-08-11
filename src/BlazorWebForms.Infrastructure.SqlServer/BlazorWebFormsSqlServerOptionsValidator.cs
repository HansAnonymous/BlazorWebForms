using Microsoft.Extensions.Options;

namespace BlazorWebForms.Infrastructure.SqlServer;

internal sealed class BlazorWebFormsSqlServerOptionsValidator : IValidateOptions<BlazorWebFormsSqlServerOptions>
{
    public ValidateOptionsResult Validate(string? name, BlazorWebFormsSqlServerOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            failures.Add("ConnectionString is required.");
        }

        if (options.EnableLocalFileStorage && string.IsNullOrWhiteSpace(options.StorageRoot))
        {
            failures.Add("StorageRoot is required when local file storage is enabled.");
        }

        if (options.DefaultMaxUploadBytes <= 0)
        {
            failures.Add("DefaultMaxUploadBytes must be greater than 0.");
        }

        if (options.DefaultMaxFilesPerField <= 0)
        {
            failures.Add("DefaultMaxFilesPerField must be greater than 0.");
        }

        if (options.EmailRetryCount <= 0)
        {
            failures.Add("EmailRetryCount must be greater than 0.");
        }

        if (options.EmailRetryDelayMs < 0)
        {
            failures.Add("EmailRetryDelayMs must be 0 or greater.");
        }

        if (options.MaxUploadsPerMinute <= 0)
        {
            failures.Add("MaxUploadsPerMinute must be greater than 0.");
        }

        if (options.MaxNotificationsPerMinute <= 0)
        {
            failures.Add("MaxNotificationsPerMinute must be greater than 0.");
        }

        if (options.EnableOutboundEmail && string.IsNullOrWhiteSpace(options.EmailFromAddress))
        {
            failures.Add("EmailFromAddress is required when outbound email is enabled.");
        }

        var strategy = options.EmailProviderStrategy?.Trim() ?? string.Empty;
        if (strategy.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.SmtpHost))
            {
                failures.Add("SmtpHost is required when EmailProviderStrategy is 'Smtp'.");
            }

            if (options.SmtpPort <= 0)
            {
                failures.Add("SmtpPort must be greater than 0 when EmailProviderStrategy is 'Smtp'.");
            }
        }

        if (strategy.Equals("SendGrid", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(options.SendGridApiKey))
        {
            failures.Add("SendGridApiKey is required when EmailProviderStrategy is 'SendGrid'.");
        }

        if (options.EnableGraphIntegration)
        {
            if (string.IsNullOrWhiteSpace(options.GraphSender))
            {
                failures.Add("GraphSender is required when graph integration is enabled.");
            }

            if (string.IsNullOrWhiteSpace(options.GraphAccessToken))
            {
                failures.Add("GraphAccessToken is required when graph integration is enabled.");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
