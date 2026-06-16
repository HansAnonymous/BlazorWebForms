using System.Text;
using BlazorWebForms.Core.Abstractions;

namespace BlazorWebForms.Infrastructure.SqlServer.Integrations;

internal sealed class PdfIntegration : IPdfIntegration
{
    public Task<byte[]> RenderAsync(string title, IReadOnlyDictionary<string, string?> fields, CancellationToken cancellationToken = default)
    {
        var lines = new List<string>
        {
            "PDF INTEGRATION OUTPUT",
            $"Title: {title}"
        };

        foreach (var field in fields.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"{field.Key}: {field.Value}");
        }

        return Task.FromResult(Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines)));
    }
}
