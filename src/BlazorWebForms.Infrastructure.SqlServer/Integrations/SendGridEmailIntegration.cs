using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BlazorWebForms.Core.Abstractions;

namespace BlazorWebForms.Infrastructure.SqlServer.Integrations;

internal sealed class SendGridEmailIntegration(BlazorWebFormsSqlServerOptions options, IHttpClientFactory httpClientFactory) : IEmailIntegration
{
    private const string Endpoint = "https://api.sendgrid.com/v3/mail/send";

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.SendGridApiKey))
        {
            throw new InvalidOperationException("SendGrid API key is not configured.");
        }

        var payload = new
        {
            personalizations = new[] { new { to = new[] { new { email = to } } } },
            from = new { email = options.EmailFromAddress, name = options.EmailFromDisplayName },
            subject,
            content = new[] { new { type = "text/plain", value = body } }
        };

        var client = httpClientFactory.CreateClient("BlazorWebForms.Email.SendGrid");
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.SendGridApiKey);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"SendGrid send failed: {(int)response.StatusCode} {response.ReasonPhrase}. {content}");
        }
    }
}
