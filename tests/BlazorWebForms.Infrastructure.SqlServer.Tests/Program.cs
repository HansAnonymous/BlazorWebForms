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
Assert(script.Contains("[forms].[Approvals]"), "Schema descriptor includes approvals table.");

var stored = await storage.SaveAsync(new()
{
    FileName = "demo.txt",
    ContentType = "text/plain",
    Content = "demo"u8.ToArray()
});

Assert(File.Exists(Path.Combine(AppContext.BaseDirectory, "test-uploads", stored.RelativePath)), "File storage writes file to disk.");

var searchResults = await forms.SearchEntriesAsync(null, "Phoenix");
Assert(searchResults.Count > 0, "Search finds indexed values.");

var dashboardForm = dashboard.Forms.First();
var submitted = await forms.SubmitEntryAsync(dashboardForm.Id, new()
{
    Answers = new Dictionary<string, string?>
    {
        ["employeeName"] = "Jamie",
        ["destination"] = "Austin",
        ["receipt"] = "receipt.pdf"
    },
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

var submittedDetail = await forms.GetEntryDetailAsync(submitted.Id);
Assert(submittedDetail is not null, "Can load submitted entry detail.");
Assert(submittedDetail!.Entry.Files.Count == 1, "Entry file metadata is persisted.");
Assert(submittedDetail.Entry.Files[0].FileName == "receipt.pdf", "Stored file name is preserved.");

Console.WriteLine("Infrastructure tests passed.");

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
