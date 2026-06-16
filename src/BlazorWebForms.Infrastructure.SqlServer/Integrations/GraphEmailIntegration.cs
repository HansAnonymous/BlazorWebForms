using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BlazorWebForms.Core.Abstractions;

namespace BlazorWebForms.Infrastructure.SqlServer.Integrations;

internal sealed class GraphEmailIntegration(BlazorWebFormsSqlServerOptions options, IHttpClientFactory httpClientFactory) : IEmailIntegration
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.GraphAccessToken))
        {
            throw new InvalidOperationException("Graph access token is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.GraphSender))
        {
            throw new InvalidOperationException("Graph sender is not configured.");
        }

        var endpoint = options.GraphMailEndpoint.Replace("{sender}", Uri.EscapeDataString(options.GraphSender), StringComparison.OrdinalIgnoreCase);
        var payload = new
        {
            message = new
            {
                subject,
                body = new { contentType = "Text", content = body },
                toRecipients = new[] { new { emailAddress = new { address = to } } }
            },
            saveToSentItems = false
        };

        var client = httpClientFactory.CreateClient("BlazorWebForms.Email.Graph");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.GraphAccessToken);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Graph mail send failed: {(int)response.StatusCode} {response.ReasonPhrase}. {content}");
        }
    }
}
