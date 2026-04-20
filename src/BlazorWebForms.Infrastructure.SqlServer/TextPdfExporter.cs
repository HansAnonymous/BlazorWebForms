using System.Text;
using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Infrastructure.SqlServer;

internal sealed class TextPdfExporter : IPdfExporter
{
    public Task<byte[]> ExportEntryAsync(FormAggregate form, EntryRecord entry, CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder()
            .AppendLine($"Form: {form.Name}")
            .AppendLine($"Entry: {entry.Id}")
            .AppendLine($"Submitted by: {entry.SubmittedBy}")
            .AppendLine($"Status: {entry.Status}")
            .AppendLine();

        foreach (var answer in entry.Answers.OrderBy(x => x.Key))
        {
            builder.AppendLine($"{answer.Key}: {answer.Value}");
        }

        return Task.FromResult(Encoding.UTF8.GetBytes(builder.ToString()));
    }
}
