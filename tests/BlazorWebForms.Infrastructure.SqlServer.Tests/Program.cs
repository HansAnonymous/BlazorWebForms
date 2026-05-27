using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;
using BlazorWebForms.Core.Services;
using BlazorWebForms.Infrastructure.SqlServer;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddBlazorWebFormsCore();
services.AddBlazorWebFormsSqlServer(options =>
{
    options.StorageRoot = Path.Combine(AppContext.BaseDirectory, "test-uploads");
    options.SchemaName = "forms";
    options.ConnectionString = $"Server=(localdb)\\MSSQLLocalDB;Database=BlazorWebFormsTests_{Guid.NewGuid():N};Trusted_Connection=True;MultipleActiveResultSets=True;";
});

await using var provider = services.BuildServiceProvider();

var repository = provider.GetRequiredService<IFormsRepository>();
var schema = provider.GetRequiredService<SqlServerSchemaDescriptor>();
var storage = provider.GetRequiredService<IFileStorage>();
var forms = provider.GetRequiredService<FormsApplicationService>();

await repository.SeedAsync();

var dashboard = await forms.GetDashboardAsync();
Assert(dashboard.Forms.Count > 0, "Seed creates demo form.");
Assert(dashboard.RecentEntries.Count > 0, "Seed creates demo entry.");

var script = schema.GetCreateScript();
Assert(script.Contains("[forms].[Forms]"), "Schema descriptor includes forms table.");
Assert(script.Contains("[forms].[ApprovalSteps]"), "Schema descriptor includes approval steps table.");

var stored = await storage.SaveAsync(new()
{
    FileName = "demo.txt",
    ContentType = "text/plain",
    Content = "demo"u8.ToArray()
});

Assert(File.Exists(Path.Combine(AppContext.BaseDirectory, "test-uploads", stored.RelativePath)), "File storage writes file to disk.");

var searchResults = await forms.SearchEntriesAsync(null, "Phoenix");
Assert(searchResults.Count > 0, "Search finds indexed values.");

var newForm = await forms.SaveDraftAsync(new SaveDraftRequest
{
    Name = "Infra SQL test form",
    Description = "Checks SQL persistence behavior.",
    Slug = $"infra-sql-{Guid.NewGuid():N}",
    AccessMode = FormAccessMode.Public,
    Definition = DemoFormFactory.CreateDefaultDefinition()
});

var version1 = await forms.PublishAsync(newForm.Id);
Assert(version1.VersionNumber == 1, "Publish creates immutable version 1.");

var submitted = await forms.SubmitEntryAsync(newForm.Id, new()
{
    Answers = new Dictionary<string, string?>
    {
        ["employeeName"] = "Jamie",
        ["destination"] = "Austin",
        ["receipt"] = "receipt.pdf"
    },
    Approvers =
    [
        new ApproverInput
        {
            Name = "Manager",
            Email = "manager@example.com"
        }
    ],
    Files =
    [
        new SubmittedFileInput
        {
            FieldId = "receipt",
            File = new StoredFile
            {
                FileName = "receipt.pdf",
                ContentType = "application/pdf",
                Length = 1234,
                RelativePath = "20260521/receipt.pdf"
            }
        }
    ]
});

Assert(submitted.Revisions.Count == 1, "Submit creates initial revision.");

var submittedDetail = await forms.GetEntryDetailAsync(submitted.Id);
Assert(submittedDetail is not null, "Can load submitted entry detail.");
Assert(submittedDetail!.Entry.Files.Count == 1, "Entry file metadata is persisted.");
Assert(submittedDetail.Entry.Files[0].FileName == "receipt.pdf", "Stored file name is preserved.");
Assert(string.IsNullOrWhiteSpace(submittedDetail.HistoricalRenderWarning), "Historical render warning is empty for valid version.");

var revised = await forms.ReviseEntryAsync(submitted.Id, new Dictionary<string, string?>
{
    ["employeeName"] = "Jamie",
    ["destination"] = "Seattle",
    ["receipt"] = "receipt.pdf"
});
Assert(revised.Revisions.Count == 2, "Edit appends revision instead of overwrite.");

var approved = await forms.ApproveStepAsync(submitted.Id, revised.ApprovalSteps[0].Id, "Manager Sign");
Assert(approved.Status == EntryStatus.Approved, "Approval progression updates status.");

var version2Definition = DemoFormFactory.CreateDefaultDefinition();
version2Definition.Sections[0].Title = "Changed title after submission";
await forms.SaveDraftAsync(new SaveDraftRequest
{
    FormId = newForm.Id,
    Name = newForm.Name,
    Description = newForm.Description,
    Slug = newForm.Publication.Slug,
    AccessMode = newForm.Publication.AccessMode,
    Definition = version2Definition
});
var version2 = await forms.PublishAsync(newForm.Id);
Assert(version2.VersionNumber == 2, "Publish increments version numbers.");

var historicalDetail = await forms.GetEntryDetailAsync(submitted.Id);
Assert(historicalDetail is not null, "Can reload historical entry detail.");
Assert(historicalDetail!.Definition.Sections[0].Title != "Changed title after submission", "Historical render uses submitted form version.");

var queryByStatus = await forms.QueryEntriesAsync(new EntryQueryOptions
{
    FormId = newForm.Id,
    Status = EntryStatus.Approved
});
Assert(queryByStatus.Any(e => e.Id == submitted.Id), "Entry query filters by form and status.");

var queryByIndex = await forms.QueryEntriesAsync(new EntryQueryOptions
{
    IndexedFieldId = "destination",
    IndexedFieldValue = "Seattle"
});
Assert(queryByIndex.Any(e => e.Id == submitted.Id), "Entry query filters by indexed field key/value.");

var parallelSubmissionTasks = Enumerable.Range(0, 5)
    .Select(async i =>
    {
        await using var scope = provider.CreateAsyncScope();
        var scopedForms = scope.ServiceProvider.GetRequiredService<FormsApplicationService>();
        return await scopedForms.SubmitEntryAsync(newForm.Id, new SubmitEntryRequest
        {
            Answers = new Dictionary<string, string?>
            {
                ["employeeName"] = $"Parallel-{i}",
                ["destination"] = "Portland"
            }
        });
    })
    .ToList();

await Task.WhenAll(parallelSubmissionTasks);

var parallelResults = parallelSubmissionTasks.Select(t => t.Result).ToList();
Assert(parallelResults.Select(e => e.Id).Distinct().Count() == 5, "Parallel submissions persist distinct entries.");

var parallelEntries = await forms.QueryEntriesAsync(new EntryQueryOptions
{
    FormId = newForm.Id,
    Search = "Parallel-"
});
Assert(parallelEntries.Count >= 5, "Parallel submission entries are queryable.");

// invitation flow smoke
var invitation = await forms.CreateInvitationAsync(new CreateInvitationRequest
{
    FormId = newForm.Id,
    Email = "invitee-sql@example.com",
    Role = FormPermissionRole.Viewer,
    ScopeType = "Form",
    ValidFor = TimeSpan.FromDays(2)
});
Assert(invitation.Status == InvitationStatus.Pending, "Invitation persisted in SQL starts pending.");

var storedInvitation = await repository.GetInvitationByTokenAsync(invitation.Token);
Assert(storedInvitation is not null, "Invitation is queryable by token from SQL repository.");

Console.WriteLine("Infrastructure tests passed.");

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
