using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;
using BlazorWebForms.Core.Services;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddBlazorWebFormsCore();
services.AddSingleton<IFormsRepository, FakeRepository>();
services.AddSingleton<ICurrentUserContext, FakeCurrentUserContext>();
services.AddSingleton<IEmailNotifier, FakeEmailNotifier>();

await using var provider = services.BuildServiceProvider();
var app = provider.GetRequiredService<FormsApplicationService>();
var serializer = provider.GetRequiredService<IFormDefinitionSerializer>();
var conditionEvaluator = provider.GetRequiredService<IConditionEvaluator>();
var repository = (FakeRepository)provider.GetRequiredService<IFormsRepository>();

await repository.SeedAsync();

var definition = DemoFormFactory.CreateDefaultDefinition();
var json = serializer.Serialize(definition);
var roundTrip = serializer.Deserialize(json);
Assert(roundTrip.Sections.Count == definition.Sections.Count, "Definition round-trip preserves sections.");

Assert(conditionEvaluator.IsVisible("needsHotel=yes", new Dictionary<string, string?> { ["needsHotel"] = "yes" }), "Condition evaluator matches field=value.");

var form = await app.SaveDraftAsync(new SaveDraftRequest
{
    Name = "Core test form",
    Description = "Draft",
    Slug = "core-test",
    Definition = definition,
    AccessMode = FormAccessMode.Public
});

var version = await app.PublishAsync(form.Id);
Assert(version.VersionNumber == 1, "Publishing creates version 1.");

var entry = await app.SubmitEntryAsync(form.Id, new SubmitEntryRequest
{
    Answers = new Dictionary<string, string?>
    {
        ["employeeName"] = "Jordan",
        ["destination"] = "Denver"
    },
    Approvers =
    [
        new ApproverInput { Name = "Lead", Email = "lead@example.com" }
    ]
});

Assert(entry.Status == EntryStatus.NeedsApproval, "Submission with approver enters approval state.");
Assert(entry.Revisions.Count == 1, "Initial submission creates revision 1.");

var revised = await app.ReviseEntryAsync(entry.Id, new Dictionary<string, string?>
{
    ["employeeName"] = "Jordan",
    ["destination"] = "Seattle"
});
Assert(revised.Revisions.Count == 2, "Revision appends history instead of overwrite.");

var approved = await app.ApproveStepAsync(entry.Id, revised.ApprovalSteps[0].Id, "Lead");
Assert(approved.Status == EntryStatus.Approved, "Final approval marks entry approved.");

Console.WriteLine("Core tests passed.");

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

internal sealed class FakeRepository : IFormsRepository
{
    private readonly Dictionary<Guid, FormAggregate> forms = [];
    private readonly Dictionary<Guid, EntryRecord> entries = [];

    public Task SeedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<FormAggregate>> GetFormsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FormAggregate>>(forms.Values.ToList());

    public Task<FormAggregate?> GetFormAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        forms.TryGetValue(formId, out var form);
        return Task.FromResult(form);
    }

    public Task<FormAggregate?> GetFormBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        Task.FromResult(forms.Values.FirstOrDefault(x => x.Publication.Slug == slug));

    public Task SaveFormAsync(FormAggregate form, CancellationToken cancellationToken = default)
    {
        forms[form.Id] = form;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EntryRecord>> GetEntriesAsync(Guid? formId, string? search, CancellationToken cancellationToken = default)
    {
        IEnumerable<EntryRecord> query = entries.Values;
        if (formId.HasValue)
        {
            query = query.Where(x => x.FormId == formId.Value);
        }

        return Task.FromResult<IReadOnlyList<EntryRecord>>(query.ToList());
    }

    public Task<EntryRecord?> GetEntryAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        entries.TryGetValue(entryId, out var entry);
        return Task.FromResult(entry);
    }

    public Task SaveEntryAsync(EntryRecord entry, CancellationToken cancellationToken = default)
    {
        entries[entry.Id] = entry;
        return Task.CompletedTask;
    }
}

internal sealed class FakeCurrentUserContext : ICurrentUserContext
{
    public UserProfile GetCurrentUser() =>
        new()
        {
            UserId = Guid.Parse("5f6f5928-b09b-4fe3-88d9-ee65968ea3e0"),
            DisplayName = "Test User",
            Email = "test@example.com",
            Roles = [FormPermissionRole.Owner, FormPermissionRole.Manager]
        };
}

internal sealed class FakeEmailNotifier : IEmailNotifier
{
    public Task NotifyManagersAsync(FormAggregate form, EntryRecord entry, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
