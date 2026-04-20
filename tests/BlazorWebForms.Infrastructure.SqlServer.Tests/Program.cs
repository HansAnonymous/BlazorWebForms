using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Services;
using BlazorWebForms.Infrastructure.SqlServer;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddBlazorWebFormsCore();
services.AddBlazorWebFormsSqlServer(options =>
{
    options.StorageRoot = Path.Combine(AppContext.BaseDirectory, "test-uploads");
    options.SchemaName = "forms";
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

Console.WriteLine("Infrastructure tests passed.");

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
