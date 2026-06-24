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
    options.EnableOutboundEmail = true;
    options.EnableGraphIntegration = true;
    options.EmailProviderStrategy = "DryRun";
});

await using var provider = services.BuildServiceProvider();

var repository = provider.GetRequiredService<IFormsRepository>();
var schema = provider.GetRequiredService<SqlServerSchemaDescriptor>();
var storage = provider.GetRequiredService<IFileStorage>();
var forms = provider.GetRequiredService<FormsApplicationService>();
var telemetry = provider.GetRequiredService<IOperationalTelemetry>() as InMemoryOperationalTelemetry;

await repository.SeedAsync();

var dashboard = await forms.GetDashboardAsync();
Assert(dashboard.Forms.Count > 0, "Seed creates demo form.");
Assert(dashboard.RecentEntries.Count > 0, "Seed creates demo entry.");

var script = schema.GetCreateScript();
Assert(script.Contains("[forms].[Forms]"), "Schema descriptor includes forms table.");
Assert(script.Contains("[forms].[ApprovalSteps]"), "Schema descriptor includes approval steps table.");
Assert(script.Contains("[RejectionReason] NVARCHAR(2000) NULL"), "Schema descriptor includes approval rejection reason column.");
Assert(script.Contains("[forms].[FormInvitations]"), "Schema descriptor includes form invitations table.");
Assert(script.Contains("[ScopeType] NVARCHAR(32) NOT NULL"), "Schema descriptor includes permission scope metadata.");
Assert(script.Contains("IX_FormPermissions_FormId_ScopeType_ScopeValue"), "Schema descriptor includes permission scope index.");
Assert(script.Contains("IX_Entries_FormId_Status_SubmittedByEmail_SubmittedUtc"), "Schema descriptor includes draft lookup index.");
Assert(script.Contains("[Sha256] NVARCHAR(64) NOT NULL"), "Schema descriptor includes file hash metadata column.");
Assert(script.Contains("[RevisionNumber] INT NOT NULL"), "Schema descriptor includes file revision metadata column.");

var stored = await storage.SaveAsync(new()
{
    FileName = "demo.txt",
    ContentType = "text/plain",
    Content = "demo"u8.ToArray()
});
Assert(stored.Sha256 == Convert.ToHexString(System.Security.Cryptography.SHA256.HashData("demo"u8.ToArray())).ToLowerInvariant(), "File storage hash metadata is consistent with payload.");

Assert(File.Exists(Path.Combine(AppContext.BaseDirectory, "test-uploads", stored.RelativePath)), "File storage writes file to disk.");
await using (var opened = await storage.OpenReadAsync(stored.RelativePath))
using (var openedReader = new StreamReader(opened))
{
    var openedText = await openedReader.ReadToEndAsync();
    Assert(openedText == "demo", "File storage can open and read stored file by relative path.");
}

await AssertThrowsAsync(
    () => storage.OpenReadAsync("..\\..\\secret.txt"),
    "Path traversal read request is rejected.");

await storage.DeleteAsync(stored.RelativePath);
await AssertThrowsAsync(
    () => storage.OpenReadAsync(stored.RelativePath),
    "Deleted file is no longer readable from storage.");

var restrictedStored = await storage.SaveAsync(new FileUploadRequest
{
    FileName = "receipt.pdf",
    ContentType = "application/pdf",
    Content = "pdf"u8.ToArray(),
    AllowedExtensions = [".pdf"],
    AllowedMimeTypes = ["application/pdf"],
    MaxAllowedBytes = 10
});
Assert(restrictedStored.FileName == "receipt.pdf", "Allowed constrained upload is stored.");

await AssertThrowsAsync(
    () => storage.SaveAsync(new FileUploadRequest
    {
        FileName = "payload.exe",
        ContentType = "application/octet-stream",
        Content = "bad"u8.ToArray(),
        AllowedExtensions = [".pdf"]
    }),
    "Rejected extension raises validation error.");

await AssertThrowsAsync(
    () => storage.SaveAsync(new FileUploadRequest
    {
        FileName = "receipt.pdf",
        ContentType = "application/json",
        Content = "bad"u8.ToArray(),
        AllowedMimeTypes = ["application/pdf"]
    }),
    "Rejected MIME type raises validation error.");

await AssertThrowsAsync(
    () => storage.SaveAsync(new FileUploadRequest
    {
        FileName = "too-big.txt",
        ContentType = "text/plain",
        Content = new byte[32],
        MaxAllowedBytes = 8
    }),
    "Rejected size raises validation error.");

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
                RelativePath = "20260521/receipt.pdf",
                Sha256 = new string('a', 64)
            }
        },
        new SubmittedFileInput
        {
            FieldId = "receipt",
            File = new StoredFile
            {
                FileName = "invoice.pdf",
                ContentType = "application/pdf",
                Length = 777,
                RelativePath = "20260521/invoice.pdf",
                Sha256 = new string('b', 64)
            }
        }
    ]
});

Assert(submitted.Revisions.Count == 1, "Submit creates initial revision.");

var submittedDetail = await forms.GetEntryDetailAsync(submitted.Id);
Assert(submittedDetail is not null, "Can load submitted entry detail.");
Assert(submittedDetail!.Entry.Files.Count == 2, "Entry file metadata supports multi-file persistence for same field.");
Assert(submittedDetail.Entry.Files.Any(file => file.FileName == "receipt.pdf"), "Stored file name is preserved.");
Assert(submittedDetail.Entry.Files.All(file => file.Sha256.Length == 64), "Stored file hash metadata is persisted.");
Assert(submittedDetail.Entry.Files.All(file => file.UploadedByUserId != Guid.Empty), "Stored file uploader user id metadata is persisted.");
Assert(submittedDetail.Entry.Files.All(file => !string.IsNullOrWhiteSpace(file.UploadedByEmail)), "Stored file uploader email metadata is persisted.");
Assert(submittedDetail.Entry.Files.All(file => file.RevisionNumber == 1), "Stored file revision linkage metadata is persisted.");
Assert(submittedDetail.Entry.ApprovalAuditTrail.Any(a => a.Action == ApprovalAuditAction.GraphApproverAssigned && !string.IsNullOrWhiteSpace(a.CorrelationId)), "Graph correlation id is persisted for assignment audit events.");
Assert(string.IsNullOrWhiteSpace(submittedDetail.HistoricalRenderWarning), "Historical render warning is empty for valid version.");

var revised = await forms.ReviseEntryAsync(submitted.Id, new Dictionary<string, string?>
{
    ["employeeName"] = "Jamie",
    ["destination"] = "Seattle",
    ["receipt"] = "receipt.pdf"
});
Assert(revised.Revisions.Count == 2, "Edit appends revision instead of overwrite.");

var remindersSent = await forms.SendApprovalRemindersAsync(newForm.Id);
Assert(remindersSent >= 1, "Approval reminder flow sends notifications for pending steps.");

var reminderDetail = await forms.GetEntryDetailAsync(submitted.Id);
Assert(reminderDetail is not null, "Can reload entry detail after reminder dispatch.");
Assert(reminderDetail!.Entry.ApprovalAuditTrail.Any(a => a.Action == ApprovalAuditAction.GraphApproverReminder), "Reminder audit events are persisted for SQL integration.");

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

var staleStored = await forms.StoreFileAsync(new FileUploadRequest
{
    FileName = "stale.txt",
    ContentType = "text/plain",
    Content = "stale-draft"u8.ToArray()
});
var staleDraft = await forms.SaveDraftSubmissionAsync(newForm.Id, new SaveDraftSubmissionRequest
{
    Answers = new Dictionary<string, string?>
    {
        ["employeeName"] = "Jamie",
        ["destination"] = "Old"
    },
    Files =
    [
        new SubmittedFileInput
        {
            FieldId = "receipt",
            File = staleStored
        }
    ]
});
staleDraft.SubmittedUtc = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(10));
await repository.SaveEntryAsync(staleDraft);

var cleanup = await forms.CleanupStaleDraftFilesAsync(TimeSpan.FromDays(7));
Assert(cleanup.DeletedDraftEntries >= 1, "Cleanup removes stale draft entries in SQL repository.");
Assert(cleanup.DeletedFiles >= 1, "Cleanup removes stale draft files from storage in SQL repository.");
Assert(!File.Exists(Path.Combine(AppContext.BaseDirectory, "test-uploads", staleStored.RelativePath)), "Cleanup deletes stale file bytes from disk.");

var exported = await forms.ExportEntryPdfAsync(submitted.Id);
Assert(exported.ContentType == "application/pdf", "PDF export returns PDF content type in SQL flow.");
Assert(exported.Content.Length > 0, "PDF export returns bytes in SQL flow.");
var exportedText = System.Text.Encoding.UTF8.GetString(exported.Content);
Assert(exportedText.Contains("APPROVAL STEPS", StringComparison.Ordinal), "PDF export includes approval steps section.");
Assert(exportedText.Contains("FILES", StringComparison.Ordinal), "PDF export includes files section.");
Assert(exportedText.Contains($"Form version id: {submitted.FormVersionId}", StringComparison.Ordinal), "PDF export uses submitted form version context.");
Assert(exportedText.Contains("invoice.pdf", StringComparison.Ordinal), "PDF export includes additional multi-file metadata.");

Assert(telemetry is not null, "Operational telemetry service is registered.");
var metrics = telemetry!.Snapshot();
Assert(metrics.TryGetValue("upload:forms-service:success", out var uploadCount) && uploadCount > 0, "Telemetry tracks upload success counts.");
Assert(metrics.TryGetValue("pdf:forms-service:success", out var pdfCount) && pdfCount > 0, "Telemetry tracks PDF success counts.");
Assert(metrics.TryGetValue("email:email:success", out var emailCount) && emailCount > 0, "Telemetry tracks email delivery success counts.");

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

var pagedQuery = await forms.QueryEntriesAsync(new EntryQueryOptions
{
    FormId = newForm.Id,
    Limit = 1
});
Assert(pagedQuery.Count == 1, "Entry query applies limit for admin pagination scenarios.");

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

var perfForm = await forms.SaveDraftAsync(new SaveDraftRequest
{
    Name = "Perf form",
    Description = "Performance paging checks.",
    Slug = $"perf-{Guid.NewGuid():N}",
    AccessMode = FormAccessMode.Public,
    Definition = DemoFormFactory.CreateDefaultDefinition()
});
await forms.PublishAsync(perfForm.Id);

for (var i = 0; i < 120; i++)
{
    await forms.SubmitEntryAsync(perfForm.Id, new SubmitEntryRequest
    {
        Answers = new Dictionary<string, string?>
        {
            ["employeeName"] = $"Perf-{i}",
            ["destination"] = "Scale"
        }
    });
}

var perfWatch = System.Diagnostics.Stopwatch.StartNew();
var perfPage = await forms.QueryEntriesAsync(new EntryQueryOptions
{
    FormId = perfForm.Id,
    Limit = 50,
    Offset = 50
});
perfWatch.Stop();
Assert(perfPage.Count == 50, "Performance paging query returns bounded page size.");
Assert(perfWatch.ElapsedMilliseconds < 5000, "Performance paging query remains within regression threshold.");

// invitation flow smoke
var invitationToRevoke = await forms.CreateInvitationAsync(new CreateInvitationRequest
{
    FormId = newForm.Id,
    Email = "invitee-sql@example.com",
    Role = FormPermissionRole.Viewer,
    ScopeType = "Form",
    ValidFor = TimeSpan.FromDays(2)
});
Assert(invitationToRevoke.Status == InvitationStatus.Pending, "Invitation persisted in SQL starts pending.");

var storedInvitation = await repository.GetInvitationByTokenAsync(invitationToRevoke.Token);
Assert(storedInvitation is not null, "Invitation is queryable by token from SQL repository.");

var revokedInvitation = await forms.RevokeInvitationAsync(invitationToRevoke.Id);
Assert(revokedInvitation.Status == InvitationStatus.Revoked, "Invitation revoke flow updates SQL status.");

var invitationToAccept = await forms.CreateInvitationAsync(new CreateInvitationRequest
{
    FormId = newForm.Id,
    Email = "invitee-accept-sql@example.com",
    Role = FormPermissionRole.Viewer,
    ScopeType = "Form",
    ValidFor = TimeSpan.FromDays(2)
});
Assert(invitationToAccept.Status == InvitationStatus.Pending, "Second invitation is pending before accept.");

var acceptedInvitation = await forms.AcceptInvitationAsync(invitationToAccept.Token);
Assert(acceptedInvitation.Status == InvitationStatus.Accepted, "Invitation accept flow updates SQL status.");

var acceptedFromRepository = await repository.GetInvitationByTokenAsync(invitationToAccept.Token);
Assert(acceptedFromRepository?.Status == InvitationStatus.Accepted, "Accepted invitation status persists in SQL repository.");

Console.WriteLine("Infrastructure tests passed.");

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static async Task AssertThrowsAsync(Func<Task> action, string message)
{
    try
    {
        await action();
    }
    catch (InvalidOperationException)
    {
        return;
    }
    catch (FileNotFoundException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

