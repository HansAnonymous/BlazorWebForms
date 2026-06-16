using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Infrastructure.SqlServer;

internal static class DemoEntrySeedHelper
{
    public static EntryEntity CreateDemoEntry(Guid formId, Guid formVersionId)
    {
        var entry = new EntryEntity
        {
            FormId = formId,
            FormVersionId = formVersionId,
            SubmittedBy = "Taylor Submitter",
            SubmittedByEmail = "taylor@example.com",
            Status = (int)EntryStatus.NeedsApproval,
            SubmittedUtc = DateTimeOffset.UtcNow
        };

        entry.SearchIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["employeeName"] = "Taylor Submitter",
            ["destination"] = "Phoenix"
        };

        entry.Revisions.Add(new EntryRevisionEntity
        {
            RevisionNumber = 1,
            EditedBy = "Taylor Submitter",
            EditedUtc = entry.SubmittedUtc,
            Answers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["employeeName"] = "Taylor Submitter",
                ["destination"] = "Phoenix"
            }
        });

        entry.ApprovalSteps.Add(new ApprovalStepEntity
        {
            Order = 1,
            ApproverName = "Casey Manager",
            ApproverEmail = "casey@example.com",
            Status = (int)ApprovalStepStatus.Pending
        });

        foreach (var kv in entry.SearchIndex)
        {
            entry.SearchIndexEntries.Add(new EntrySearchIndexEntity
            {
                Id = Guid.NewGuid(),
                Key = kv.Key,
                Value = kv.Value
            });
        }

        return entry;
    }
}
