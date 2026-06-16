using System.Text;
using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;
using BlazorWebForms.Core.Services;

namespace BlazorWebForms.Infrastructure.SqlServer;

internal sealed class TextPdfExporter(
    BlazorWebFormsSqlServerOptions options,
    IFormDefinitionSerializer serializer,
    IIntegrationGateway integrations) : IPdfExporter
{
    public async Task<byte[]> ExportEntryAsync(FormAggregate form, EntryRecord entry, CancellationToken cancellationToken = default)
    {
        if (!options.EnableTextBasedPdfExporter)
        {
            throw new InvalidOperationException("Text-based PDF exporter is disabled.");
        }

        var version = form.Versions.FirstOrDefault(v => v.Id == entry.FormVersionId);
        var definition = version is null
            ? form.DraftDefinition
            : serializer.Deserialize(version.DefinitionJson);

        var answerRows = BuildAnswerRows(definition, entry)
            .Take(Math.Max(1, options.PdfMaxAnswerRows))
            .ToList();
        var auditRows = entry.ApprovalAuditTrail
            .OrderByDescending(audit => audit.OccurredUtc)
            .Take(Math.Max(1, options.PdfMaxAuditRows))
            .ToList();

        var builder = new StringBuilder()
            .AppendLine("BLAZOR WEB FORMS EXPORT")
            .AppendLine($"Form: {ResolveLocalizedFormTitle(form, definition)}")
            .AppendLine($"Description: {ResolveLocalizedFormDescription(form, definition)}")
            .AppendLine($"Form version id: {entry.FormVersionId}")
            .AppendLine($"Entry: {entry.Id}")
            .AppendLine($"Submitted by: {entry.SubmittedBy}")
            .AppendLine($"Submitted email: {entry.SubmittedByEmail}")
            .AppendLine($"Submitted utc: {entry.SubmittedUtc:O}")
            .AppendLine($"Status: {entry.Status}")
            .AppendLine($"Branding accent: {definition.Branding.AccentColor}")
            .AppendLine($"Branding surface: {definition.Branding.SurfaceColor}")
            .AppendLine($"Branding text: {definition.Branding.TextColor}")
            .AppendLine();

        builder.AppendLine("ANSWERS");
        foreach (var answer in answerRows)
        {
            builder.AppendLine($"{answer}");
        }

        if (entry.Files.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("FILES");
            foreach (var file in entry.Files.OrderBy(file => file.FieldId).ThenBy(file => file.FileName))
            {
                builder.AppendLine($"{file.FieldId}: {file.FileName} ({file.ContentType}, {file.Length} bytes, sha256={file.Sha256})");
            }
        }

        if (entry.ApprovalSteps.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("APPROVAL STEPS");
            foreach (var step in entry.ApprovalSteps.OrderBy(step => step.Order))
            {
                builder.AppendLine($"{step.Order}. {step.ApproverName} <{step.ApproverEmail}> - {step.Status}");
            }
        }

        if (auditRows.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("APPROVAL AUDIT");
            foreach (var audit in auditRows)
            {
                builder.AppendLine($"{audit.OccurredUtc:O} {audit.Action} by {audit.ActorDisplayName}; reason={audit.Reason}; signature={audit.Signature}");
            }
        }

        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["formName"] = ResolveLocalizedFormTitle(form, definition),
            ["entryId"] = entry.Id.ToString(),
            ["status"] = entry.Status.ToString(),
            ["content"] = builder.ToString()
        };
        var payload = await integrations.Pdf.RenderAsync($"Entry {entry.Id}", fields, cancellationToken);
        if (payload.Length > options.PdfMaxBytes)
        {
            throw new InvalidOperationException($"PDF export exceeded max configured size of {options.PdfMaxBytes} bytes.");
        }

        return payload;
    }

    private static IEnumerable<string> BuildAnswerRows(FormDefinition definition, EntryRecord entry)
    {
        foreach (var section in definition.Sections)
        {
            var sectionTitle = FormLocalizationResolver.ResolveText(section.Title, section.LocalizedTitles, definition.DefaultCulture, definition.DefaultCulture);
            foreach (var field in section.Fields)
            {
                var label = FormLocalizationResolver.ResolveText(field.Label, field.LocalizedLabels, definition.DefaultCulture, definition.DefaultCulture);
                entry.Answers.TryGetValue(field.Id, out var value);
                yield return $"[{sectionTitle}] {label} ({field.Id}): {value}";
            }
        }
    }

    private static string ResolveLocalizedFormTitle(FormAggregate form, FormDefinition definition)
    {
        return FormLocalizationResolver.ResolveText(definition.Title, definition.LocalizedTitles, definition.DefaultCulture, definition.DefaultCulture);
    }

    private static string ResolveLocalizedFormDescription(FormAggregate form, FormDefinition definition)
    {
        return FormLocalizationResolver.ResolveText(definition.Description, definition.LocalizedDescriptions, definition.DefaultCulture, definition.DefaultCulture);
    }
}
