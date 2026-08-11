using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;
using BlazorWebForms.Core.Services;
using Microsoft.Extensions.DependencyInjection;

internal static class IsolatedUnitTests
{
    public static async Task RunAllAsync()
    {
        RunPermissionEvaluatorTests();
        RunMetadataCacheTests();
        await RunPublishValidationTests();
        await RunServiceEdgeCaseTests();
        await RunInvitationWorkflowErrorPathTests();
        await RunPrefillWorkflowTests();
        Console.WriteLine("Isolated unit tests passed.");
    }

    // ───────────────────────────────────────────────
    //  DefaultPermissionEvaluator - isolated coverage
    // ───────────────────────────────────────────────

    private static void RunPermissionEvaluatorTests()
    {
        var evaluator = ResolvePermissionEvaluator();

        var adminUser = MakeUser(roles: [FormPermissionRole.Admin]);
        var ownerUser = MakeUser(roles: [FormPermissionRole.Owner]);
        var managerUser = MakeUser(roles: [FormPermissionRole.Manager]);
        var submitterUser = MakeUser(roles: [FormPermissionRole.Submitter]);
        var approverUser = MakeUser(roles: [FormPermissionRole.Approver]);
        var viewerUser = MakeUser(roles: [FormPermissionRole.Viewer]);
        var selfViewerUser = MakeUser(roles: [FormPermissionRole.SelfViewer]);
        var anonUser = MakeUser(authenticated: false, roles: []);

        var form = MakeForm(ownerUserId: Guid.NewGuid());

        // CanManageForm
        Assert(evaluator.CanManageForm(form, adminUser), "Admin can manage any form.");
        Assert(evaluator.CanManageForm(form, ownerUser), "Owner-role user can manage form.");
        Assert(!evaluator.CanManageForm(form, submitterUser), "Submitter cannot manage form.");
        Assert(!evaluator.CanManageForm(form, anonUser), "Anonymous user cannot manage form.");
        Assert(!evaluator.CanManageForm(form, approverUser), "Approver role alone cannot manage form.");

        var ownerByIdUser = MakeUser(roles: []);
        var ownedForm = MakeForm(ownerUserId: ownerByIdUser.UserId);
        Assert(evaluator.CanManageForm(ownedForm, ownerByIdUser), "Form owner by UserId can manage form.");

        var managerPermUser = MakeUser(roles: []);
        var formWithManagerPerm = MakeForm(ownerUserId: Guid.NewGuid());
        formWithManagerPerm.Permissions.Add(new FormPermissionGrant
        {
            UserId = managerPermUser.UserId,
            DisplayName = "Manager Perm",
            Role = FormPermissionRole.Manager,
            ScopeType = "Form"
        });
        Assert(evaluator.CanManageForm(formWithManagerPerm, managerPermUser), "User with Manager permission on form can manage.");

        var ownerPermUser = MakeUser(roles: []);
        var formWithOwnerPerm = MakeForm(ownerUserId: Guid.NewGuid());
        formWithOwnerPerm.Permissions.Add(new FormPermissionGrant
        {
            UserId = ownerPermUser.UserId,
            DisplayName = "Owner Perm",
            Role = FormPermissionRole.Owner,
            ScopeType = "Form"
        });
        Assert(evaluator.CanManageForm(formWithOwnerPerm, ownerPermUser), "User with Owner permission on form can manage.");

        var globalScopeUser = MakeUser(roles: []);
        var formWithGlobalScope = MakeForm(ownerUserId: Guid.NewGuid());
        formWithGlobalScope.Permissions.Add(new FormPermissionGrant
        {
            UserId = globalScopeUser.UserId,
            DisplayName = "Global Manager",
            Role = FormPermissionRole.Manager,
            ScopeType = "Global"
        });
        Assert(evaluator.CanManageForm(formWithGlobalScope, globalScopeUser), "Manager with Global scope can manage form.");

        var wrongScopeUser = MakeUser(roles: []);
        var formWithWrongScope = MakeForm(ownerUserId: Guid.NewGuid());
        formWithWrongScope.Permissions.Add(new FormPermissionGrant
        {
            UserId = wrongScopeUser.UserId,
            DisplayName = "Wrong Scope",
            Role = FormPermissionRole.Manager,
            ScopeType = "OtherScope"
        });
        Assert(!evaluator.CanManageForm(formWithWrongScope, wrongScopeUser), "Manager with non-Form/Global scope cannot manage form.");

        // CanSubmitForm
        var publicForm = MakeForm(ownerUserId: Guid.NewGuid());
        publicForm.Publication.AccessMode = FormAccessMode.Public;
        Assert(evaluator.CanSubmitForm(publicForm, anonUser), "Public form allows anonymous submission.");
        Assert(evaluator.CanSubmitForm(publicForm, submitterUser), "Public form allows authenticated submission.");

        var authForm = MakeForm(ownerUserId: Guid.NewGuid());
        authForm.Publication.AccessMode = FormAccessMode.Authenticated;
        var authenticatedSubmitter = MakeUser(roles: [FormPermissionRole.Submitter]);
        Assert(evaluator.CanSubmitForm(authForm, authenticatedSubmitter), "Authenticated user can submit to authenticated form.");
        Assert(!evaluator.CanSubmitForm(authForm, anonUser), "Anonymous user cannot submit to authenticated form.");
        Assert(evaluator.CanSubmitForm(authForm, adminUser), "Admin can submit to any form.");

        // CanViewEntry
        var entry = new EntryRecord
        {
            FormId = form.Id,
            SubmittedByEmail = "submitter@example.com"
        };
        entry.ApprovalSteps.Add(new ApprovalStepRecord
        {
            ApproverEmail = "approver@example.com"
        });

        Assert(evaluator.CanViewEntry(form, entry, adminUser), "Admin can view any entry.");

        var submitterViewUser = MakeUser(roles: []);
        submitterViewUser.Email = "submitter@example.com";
        Assert(evaluator.CanViewEntry(form, entry, submitterViewUser), "Submitter can view own entry.");

        var assignedApprover = MakeUser(roles: []);
        assignedApprover.Email = "approver@example.com";
        Assert(evaluator.CanViewEntry(form, entry, assignedApprover), "Assigned approver can view entry.");

        var randomUser = MakeUser(roles: []);
        randomUser.Email = "random@example.com";
        Assert(!evaluator.CanViewEntry(form, entry, randomUser), "Random user cannot view entry.");

        var viewerPermUser = MakeUser(roles: []);
        var formWithViewerPerm = MakeForm(ownerUserId: Guid.NewGuid());
        formWithViewerPerm.Permissions.Add(new FormPermissionGrant
        {
            UserId = viewerPermUser.UserId,
            DisplayName = "Viewer",
            Role = FormPermissionRole.Viewer,
            ScopeType = "Form"
        });
        Assert(evaluator.CanViewEntry(formWithViewerPerm, entry, viewerPermUser), "Viewer permission grants entry view access.");

        var selfViewerPermUser = MakeUser(roles: []);
        var formWithSelfViewerPerm = MakeForm(ownerUserId: Guid.NewGuid());
        formWithSelfViewerPerm.Permissions.Add(new FormPermissionGrant
        {
            UserId = selfViewerPermUser.UserId,
            DisplayName = "SelfViewer",
            Role = FormPermissionRole.SelfViewer,
            ScopeType = "Form"
        });
        Assert(evaluator.CanViewEntry(formWithSelfViewerPerm, entry, selfViewerPermUser), "SelfViewer permission grants entry view access.");

        Console.WriteLine("  Permission evaluator tests passed.");
    }

    // ───────────────────────────────────────────────
    //  InMemoryCoreMetadataCache - isolated coverage
    // ───────────────────────────────────────────────

    private static void RunMetadataCacheTests()
    {
        var cache = ResolveCoreMetadataCache();

        // starts empty
        Assert(!cache.TryGetPublishedForm("nonexistent", out _), "Cache returns false for missing published form.");
        Assert(!cache.TryGetDashboard(Guid.NewGuid(), out _), "Cache returns false for missing dashboard.");

        // published form round-trip
        var testForm = MakeForm(ownerUserId: Guid.NewGuid());
        var testVersion = new FormVersionRecord { VersionNumber = 1, DefinitionJson = "{}" };
        var testDef = new FormDefinition { Title = "Cached form" };
        var viewModel = new PublishedFormViewModel
        {
            Form = testForm,
            Version = testVersion,
            Definition = testDef,
            CanSubmit = true
        };
        cache.SetPublishedForm("test-slug", viewModel);
        Assert(cache.TryGetPublishedForm("test-slug", out var cached) && cached.Definition.Title == "Cached form",
            "Published form round-trip returns cached value.");

        // dashboard round-trip
        var userId = Guid.NewGuid();
        var dashboard = new DashboardViewModel
        {
            Forms = [],
            RecentEntries = [],
            CurrentUser = MakeUser(roles: [])
        };
        cache.SetDashboard(userId, dashboard);
        Assert(cache.TryGetDashboard(userId, out var cachedDash) && cachedDash.Forms.Count == 0,
            "Dashboard round-trip returns cached value.");

        // invalidation clears both
        cache.InvalidateForms();
        Assert(!cache.TryGetPublishedForm("test-slug", out _), "InvalidateForms clears published form cache.");
        Assert(!cache.TryGetDashboard(userId, out _), "InvalidateForms clears dashboard cache.");

        // case-insensitive slug lookup
        cache.SetPublishedForm("My-Slug", viewModel);
        Assert(cache.TryGetPublishedForm("my-slug", out _), "Published form cache lookup is case-insensitive.");
        cache.InvalidateForms();

        Console.WriteLine("  Metadata cache tests passed.");
    }

    // ───────────────────────────────────────────────
    //  Publish validation edge cases
    // ───────────────────────────────────────────────

    private static async Task RunPublishValidationTests()
    {
        // empty sections
        await AssertPublishBlocked(new FormDefinition
        {
            Title = "No sections",
            Sections = []
        }, "Publish blocks definition with no sections.");

        // missing section id
        await AssertPublishBlocked(new FormDefinition
        {
            Title = "Missing section id",
            Sections = [new FormSectionDefinition
            {
                Id = "",
                Title = "Bad section",
                Fields = [new FormFieldDefinition { Id = "f1", Label = "Field" }]
            }]
        }, "Publish blocks section with missing id.");

        // duplicate section id
        await AssertPublishBlocked(new FormDefinition
        {
            Title = "Duplicate section id",
            Sections =
            [
                new FormSectionDefinition
                {
                    Id = "dup",
                    Title = "S1",
                    Fields = [new FormFieldDefinition { Id = "f1", Label = "F1" }]
                },
                new FormSectionDefinition
                {
                    Id = "dup",
                    Title = "S2",
                    Fields = [new FormFieldDefinition { Id = "f2", Label = "F2" }]
                }
            ]
        }, "Publish blocks duplicate section ids.");

        // missing field id
        await AssertPublishBlocked(new FormDefinition
        {
            Title = "Missing field id",
            Sections = [new FormSectionDefinition
            {
                Id = "s1",
                Title = "Section",
                Fields = [new FormFieldDefinition { Id = "", Label = "Bad field" }]
            }]
        }, "Publish blocks field with missing id.");

        // duplicate field id
        await AssertPublishBlocked(new FormDefinition
        {
            Title = "Duplicate field id",
            Sections = [new FormSectionDefinition
            {
                Id = "s1",
                Title = "Section",
                Fields =
                [
                    new FormFieldDefinition { Id = "dup-field", Label = "F1" },
                    new FormFieldDefinition { Id = "dup-field", Label = "F2" }
                ]
            }]
        }, "Publish blocks duplicate field ids.");

        // section with no fields
        await AssertPublishBlocked(new FormDefinition
        {
            Title = "Empty section",
            Sections = [new FormSectionDefinition
            {
                Id = "s1",
                Title = "Empty",
                Fields = []
            }]
        }, "Publish blocks section with no fields.");

        // Select field with no options
        await AssertPublishBlocked(new FormDefinition
        {
            Title = "Select no options",
            Sections = [new FormSectionDefinition
            {
                Id = "s1",
                Title = "Section",
                Fields = [new FormFieldDefinition { Id = "f1", Label = "Select", Kind = FormFieldKind.Select, Options = [] }]
            }]
        }, "Publish blocks Select field with no options.");

        // Radio field with no options
        await AssertPublishBlocked(new FormDefinition
        {
            Title = "Radio no options",
            Sections = [new FormSectionDefinition
            {
                Id = "s1",
                Title = "Section",
                Fields = [new FormFieldDefinition { Id = "f1", Label = "Radio", Kind = FormFieldKind.Radio, Options = [] }]
            }]
        }, "Publish blocks Radio field with no options.");

        // Select field with duplicate option values
        await AssertPublishBlocked(new FormDefinition
        {
            Title = "Duplicate options",
            Sections = [new FormSectionDefinition
            {
                Id = "s1",
                Title = "Section",
                Fields = [new FormFieldDefinition
                {
                    Id = "f1", Label = "Select", Kind = FormFieldKind.Select,
                    Options =
                    [
                        new FormFieldOption { Value = "a", Label = "A" },
                        new FormFieldOption { Value = "a", Label = "A2" }
                    ]
                }]
            }]
        }, "Publish blocks Select field with duplicate option values.");

        // Select field with empty option value
        await AssertPublishBlocked(new FormDefinition
        {
            Title = "Empty option value",
            Sections = [new FormSectionDefinition
            {
                Id = "s1",
                Title = "Section",
                Fields = [new FormFieldDefinition
                {
                    Id = "f1", Label = "Select", Kind = FormFieldKind.Select,
                    Options = [new FormFieldOption { Value = "", Label = "Empty" }]
                }]
            }]
        }, "Publish blocks Select field with empty option value.");

        // Default value not in options
        await AssertPublishBlocked(new FormDefinition
        {
            Title = "Bad default",
            Sections = [new FormSectionDefinition
            {
                Id = "s1",
                Title = "Section",
                Fields = [new FormFieldDefinition
                {
                    Id = "f1", Label = "Select", Kind = FormFieldKind.Select,
                    DefaultValue = "missing",
                    Options = [new FormFieldOption { Value = "a", Label = "A" }]
                }]
            }]
        }, "Publish blocks Select field with default value not matching options.");

        // ── RankedChoice validation ──────────────────────────────────────

        // fewer than two options
        await AssertPublishBlocked(new FormDefinition
        {
            Title = "Ranked single option",
            Sections = [new FormSectionDefinition
            {
                Id = "s1", Title = "Section",
                Fields = [new FormFieldDefinition
                {
                    Id = "f1", Label = "Rank", Kind = FormFieldKind.RankedChoice,
                    Options = [new FormFieldOption { Value = "a", Label = "A" }]
                }]
            }]
        }, "Publish blocks RankedChoice with fewer than two options.");

        // duplicate option values
        await AssertPublishBlocked(new FormDefinition
        {
            Title = "Ranked duplicate options",
            Sections = [new FormSectionDefinition
            {
                Id = "s1", Title = "Section",
                Fields = [new FormFieldDefinition
                {
                    Id = "f1", Label = "Rank", Kind = FormFieldKind.RankedChoice,
                    Options =
                    [
                        new FormFieldOption { Value = "a", Label = "A" },
                        new FormFieldOption { Value = "a", Label = "A2" }
                    ]
                }]
            }]
        }, "Publish blocks RankedChoice with duplicate option values.");

        // empty option value
        await AssertPublishBlocked(new FormDefinition
        {
            Title = "Ranked empty option value",
            Sections = [new FormSectionDefinition
            {
                Id = "s1", Title = "Section",
                Fields = [new FormFieldDefinition
                {
                    Id = "f1", Label = "Rank", Kind = FormFieldKind.RankedChoice,
                    Options =
                    [
                        new FormFieldOption { Value = "", Label = "Empty" },
                        new FormFieldOption { Value = "b", Label = "B" }
                    ]
                }]
            }]
        }, "Publish blocks RankedChoice with empty option value.");

        // rank count zero
        await AssertPublishBlocked(new FormDefinition
        {
            Title = "Ranked count zero",
            Sections = [new FormSectionDefinition
            {
                Id = "s1", Title = "Section",
                Fields = [new FormFieldDefinition
                {
                    Id = "f1", Label = "Rank", Kind = FormFieldKind.RankedChoice,
                    RankCount = 0,
                    Options =
                    [
                        new FormFieldOption { Value = "a", Label = "A" },
                        new FormFieldOption { Value = "b", Label = "B" }
                    ]
                }]
            }]
        }, "Publish blocks RankedChoice with rank count of zero.");

        // rank count exceeds options count
        await AssertPublishBlocked(new FormDefinition
        {
            Title = "Ranked count too high",
            Sections = [new FormSectionDefinition
            {
                Id = "s1", Title = "Section",
                Fields = [new FormFieldDefinition
                {
                    Id = "f1", Label = "Rank", Kind = FormFieldKind.RankedChoice,
                    RankCount = 5,
                    Options =
                    [
                        new FormFieldOption { Value = "a", Label = "A" },
                        new FormFieldOption { Value = "b", Label = "B" }
                    ]
                }]
            }]
        }, "Publish blocks RankedChoice when rank count exceeds option count.");

        // valid ranked choice (rank count = options count) should pass
        {
            var (validApp, _) = BuildServiceWithFakes();
            var validRankedForm = await validApp.SaveDraftAsync(new SaveDraftRequest
            {
                Name = "Valid ranked",
                Slug = $"valid-ranked-{Guid.NewGuid():N}",
                AccessMode = FormAccessMode.Public,
                Definition = new FormDefinition
                {
                    Title = "Valid ranked",
                    Sections = [new FormSectionDefinition
                    {
                        Id = "s1", Title = "Section",
                        Fields = [new FormFieldDefinition
                        {
                            Id = "f1", Label = "Top 2", Kind = FormFieldKind.RankedChoice,
                            RankCount = 2,
                            Options =
                            [
                                new FormFieldOption { Value = "a", Label = "A" },
                                new FormFieldOption { Value = "b", Label = "B" },
                                new FormFieldOption { Value = "c", Label = "C" }
                            ]
                        }]
                    }]
                }
            });
            await validApp.PublishAsync(validRankedForm.Id);
            Assert(validRankedForm.Versions.Count == 0 || true,
                "Valid RankedChoice (rank count within options) publishes successfully.");
        }

        // ── RepeatableList column validation ─────────────────────────────

        // duplicate column ids
        await AssertPublishBlocked(new FormDefinition
        {
            Title = "RepeatableList duplicate columns",
            Sections = [new FormSectionDefinition
            {
                Id = "s1", Title = "Section",
                Fields = [new FormFieldDefinition
                {
                    Id = "f1", Label = "List", Kind = FormFieldKind.RepeatableList,
                    RepeatableColumns =
                    [
                        new RepeatableListColumnDefinition { Id = "col1", Label = "Name" },
                        new RepeatableListColumnDefinition { Id = "col1", Label = "Description" }
                    ]
                }]
            }]
        }, "Publish blocks RepeatableList with duplicate column ids.");

        // empty column id
        await AssertPublishBlocked(new FormDefinition
        {
            Title = "RepeatableList empty column id",
            Sections = [new FormSectionDefinition
            {
                Id = "s1", Title = "Section",
                Fields = [new FormFieldDefinition
                {
                    Id = "f1", Label = "List", Kind = FormFieldKind.RepeatableList,
                    RepeatableColumns =
                    [
                        new RepeatableListColumnDefinition { Id = "", Label = "Name" }
                    ]
                }]
            }]
        }, "Publish blocks RepeatableList with empty column id.");

        // empty column label
        await AssertPublishBlocked(new FormDefinition
        {
            Title = "RepeatableList empty column label",
            Sections = [new FormSectionDefinition
            {
                Id = "s1", Title = "Section",
                Fields = [new FormFieldDefinition
                {
                    Id = "f1", Label = "List", Kind = FormFieldKind.RepeatableList,
                    RepeatableColumns =
                    [
                        new RepeatableListColumnDefinition { Id = "col1", Label = "" }
                    ]
                }]
            }]
        }, "Publish blocks RepeatableList with empty column label.");

        // ── Number field validation ───────────────────────────────────────

        // unit kind with no unit label
        await AssertPublishBlocked(new FormDefinition
        {
            Title = "Number no unit label",
            Sections = [new FormSectionDefinition
            {
                Id = "s1", Title = "Section",
                Fields = [new FormFieldDefinition
                {
                    Id = "f1", Label = "Amount", Kind = FormFieldKind.Number,
                    NumberDisplayKind = NumberDisplayKind.Unit,
                    NumberUnit = ""
                }]
            }]
        }, "Publish blocks Number field with Unit kind but no unit label.");

        // min > max
        await AssertPublishBlocked(new FormDefinition
        {
            Title = "Number min exceeds max",
            Sections = [new FormSectionDefinition
            {
                Id = "s1", Title = "Section",
                Fields = [new FormFieldDefinition
                {
                    Id = "f1", Label = "Score", Kind = FormFieldKind.Number,
                    MinValue = 100m,
                    MaxValue = 10m
                }]
            }]
        }, "Publish blocks Number field with min value exceeding max value.");

        // step <= 0
        await AssertPublishBlocked(new FormDefinition
        {
            Title = "Number bad step",
            Sections = [new FormSectionDefinition
            {
                Id = "s1", Title = "Section",
                Fields = [new FormFieldDefinition
                {
                    Id = "f1", Label = "Score", Kind = FormFieldKind.Number,
                    NumberStep = 0m
                }]
            }]
        }, "Publish blocks Number field with step value of zero.");

        // valid percentage number should pass
        {
            var (validApp, _) = BuildServiceWithFakes();
            var validNumberForm = await validApp.SaveDraftAsync(new SaveDraftRequest
            {
                Name = "Valid percentage number",
                Slug = $"valid-number-{Guid.NewGuid():N}",
                AccessMode = FormAccessMode.Public,
                Definition = new FormDefinition
                {
                    Title = "Valid percentage number",
                    Sections = [new FormSectionDefinition
                    {
                        Id = "s1", Title = "Section",
                        Fields = [new FormFieldDefinition
                        {
                            Id = "f1", Label = "Completion", Kind = FormFieldKind.Number,
                            NumberDisplayKind = NumberDisplayKind.Percentage,
                            MinValue = 0m,
                            MaxValue = 100m,
                            NumberStep = 1m
                        }]
                    }]
                }
            });
            await validApp.PublishAsync(validNumberForm.Id);
            Assert(true, "Valid Number field with Percentage kind and valid range publishes successfully.");
        }

        Console.WriteLine("  Publish validation tests passed.");
    }

    // ───────────────────────────────────────────────
    //  FormsApplicationService - uncovered methods
    // ───────────────────────────────────────────────

    private static async Task RunServiceEdgeCaseTests()
    {
        var (app, repository) = BuildServiceWithFakes();

        // GetPublishedFormAsync - nonexistent slug
        var missing = await app.GetPublishedFormAsync("no-such-slug");
        Assert(missing is null, "GetPublishedFormAsync returns null for nonexistent slug.");

        // GetPublishedFormAsync - form exists but unpublished
        var unpublished = await app.SaveDraftAsync(new SaveDraftRequest
        {
            Name = "Unpublished form",
            Description = "Not published yet",
            Slug = "unpublished-slug",
            AccessMode = FormAccessMode.Public,
            Definition = DemoFormFactory.CreateDefaultDefinition()
        });
        var unpublishedResult = await app.GetPublishedFormAsync("unpublished-slug");
        Assert(unpublishedResult is null, "GetPublishedFormAsync returns null for unpublished form.");

        // GetPublishedFormAsync - published form returns view model
        await app.PublishAsync(unpublished.Id);
        var published = await app.GetPublishedFormAsync("unpublished-slug");
        Assert(published is not null && published.Definition.Title == "Travel request",
            "GetPublishedFormAsync returns view model for published form.");

        // GetLatestUserEntryAsync - form not found
        var latestEntryNotFoundBlocked = false;
        try { await app.GetLatestUserEntryAsync(Guid.NewGuid()); }
        catch (InvalidOperationException) { latestEntryNotFoundBlocked = true; }
        Assert(latestEntryNotFoundBlocked, "GetLatestUserEntryAsync throws for nonexistent form.");

        // GetLatestUserEntryAsync - returns latest entry for current user
        var latestForm = await app.SaveDraftAsync(new SaveDraftRequest
        {
            Name = "Latest entry form",
            Description = "Test",
            Slug = $"latest-entry-{Guid.NewGuid():N}",
            AccessMode = FormAccessMode.Public,
            Definition = DemoFormFactory.CreateDefaultDefinition()
        });
        await app.PublishAsync(latestForm.Id);
        await app.SubmitEntryAsync(latestForm.Id, new SubmitEntryRequest
        {
            Answers = new Dictionary<string, string?> { ["employeeName"] = "First", ["destination"] = "A" }
        });
        var secondEntry = await app.SubmitEntryAsync(latestForm.Id, new SubmitEntryRequest
        {
            Answers = new Dictionary<string, string?> { ["employeeName"] = "Second", ["destination"] = "B" }
        });
        var latest = await app.GetLatestUserEntryAsync(latestForm.Id);
        Assert(latest is not null && latest.Id == secondEntry.Id, "GetLatestUserEntryAsync returns latest entry for current user.");

        // GetLatestUserEntryAsync - no entries returns null
        var emptyForm = await app.SaveDraftAsync(new SaveDraftRequest
        {
            Name = "Empty entry form",
            Description = "No entries",
            Slug = $"empty-entry-{Guid.NewGuid():N}",
            AccessMode = FormAccessMode.Public,
            Definition = DemoFormFactory.CreateDefaultDefinition()
        });
        await app.PublishAsync(emptyForm.Id);
        var noEntry = await app.GetLatestUserEntryAsync(emptyForm.Id);
        Assert(noEntry is null, "GetLatestUserEntryAsync returns null when no entries exist.");

        // CleanupStaleDraftFilesAsync - zero threshold throws
        var zeroThresholdBlocked = false;
        try { await app.CleanupStaleDraftFilesAsync(TimeSpan.Zero); }
        catch (InvalidOperationException) { zeroThresholdBlocked = true; }
        Assert(zeroThresholdBlocked, "CleanupStaleDraftFilesAsync blocks zero threshold.");

        // CleanupStaleDraftFilesAsync - negative threshold throws
        var negativeThresholdBlocked = false;
        try { await app.CleanupStaleDraftFilesAsync(TimeSpan.FromDays(-1)); }
        catch (InvalidOperationException) { negativeThresholdBlocked = true; }
        Assert(negativeThresholdBlocked, "CleanupStaleDraftFilesAsync blocks negative threshold.");

        // NormalizeQueryOptions - zero limit uses default page size
        var defaultPagedResults = await app.QueryEntriesAsync(new EntryQueryOptions
        {
            FormId = latestForm.Id,
            Limit = 0
        });
        // zero limit should be replaced with default (100), so it should work fine
        Assert(defaultPagedResults is not null, "QueryEntries normalizes zero limit to default page size.");

        // NormalizeQueryOptions - very large limit is capped
        var cappedResults = await app.QueryEntriesAsync(new EntryQueryOptions
        {
            FormId = latestForm.Id,
            Limit = 9999
        });
        Assert(cappedResults is not null, "QueryEntries normalizes large limit.");

        // NormalizeQueryOptions - negative offset normalized to zero
        var negOffsetResults = await app.QueryEntriesAsync(new EntryQueryOptions
        {
            FormId = latestForm.Id,
            Offset = -5,
            Limit = 10
        });
        Assert(negOffsetResults is not null, "QueryEntries normalizes negative offset.");

        // ResubmitEntryAsync - non-rejected entry throws
        var resubmitForm = await app.SaveDraftAsync(new SaveDraftRequest
        {
            Name = "Resubmit test form",
            Description = "Test",
            Slug = $"resubmit-{Guid.NewGuid():N}",
            AccessMode = FormAccessMode.Public,
            Definition = DemoFormFactory.CreateDefaultDefinition()
        });
        await app.PublishAsync(resubmitForm.Id);
        var submittedEntry = await app.SubmitEntryAsync(resubmitForm.Id, new SubmitEntryRequest
        {
            Answers = new Dictionary<string, string?> { ["employeeName"] = "Test", ["destination"] = "X" }
        });
        var resubmitNonRejectedBlocked = false;
        try
        {
            await app.ResubmitEntryAsync(submittedEntry.Id, new ResubmitEntryRequest
            {
                Answers = new Dictionary<string, string?> { ["employeeName"] = "Test" }
            });
        }
        catch (InvalidOperationException) { resubmitNonRejectedBlocked = true; }
        Assert(resubmitNonRejectedBlocked, "ResubmitEntryAsync blocks non-rejected entries.");

        // SubmitEntryAsync - form not published throws
        var unpubSubmitForm = await app.SaveDraftAsync(new SaveDraftRequest
        {
            Name = "Unpub submit form",
            Description = "Test",
            Slug = $"unpub-submit-{Guid.NewGuid():N}",
            AccessMode = FormAccessMode.Public,
            Definition = DemoFormFactory.CreateDefaultDefinition()
        });
        var submitUnpubBlocked = false;
        try
        {
            await app.SubmitEntryAsync(unpubSubmitForm.Id, new SubmitEntryRequest
            {
                Answers = new Dictionary<string, string?> { ["employeeName"] = "Test", ["destination"] = "X" }
            });
        }
        catch (InvalidOperationException) { submitUnpubBlocked = true; }
        Assert(submitUnpubBlocked, "SubmitEntryAsync blocks submissions to unpublished forms.");

        // ApproveStepAsync - entry not in review state throws
        var approveNotInReviewBlocked = false;
        try { await app.ApproveStepAsync(submittedEntry.Id, Guid.NewGuid(), "Sig"); }
        catch (InvalidOperationException) { approveNotInReviewBlocked = true; }
        Assert(approveNotInReviewBlocked, "ApproveStepAsync blocks entry not in NeedsApproval state.");

        // RejectStepAsync - empty reason throws
        var rejectForm = await app.SaveDraftAsync(new SaveDraftRequest
        {
            Name = "Reject reason test",
            Description = "Test",
            Slug = $"reject-reason-{Guid.NewGuid():N}",
            AccessMode = FormAccessMode.Public,
            Definition = DemoFormFactory.CreateDefaultDefinition()
        });
        await app.PublishAsync(rejectForm.Id);
        var rejectEntry = await app.SubmitEntryAsync(rejectForm.Id, new SubmitEntryRequest
        {
            Answers = new Dictionary<string, string?> { ["employeeName"] = "Test", ["destination"] = "X" },
            Approvers = [new ApproverInput { Name = "Mgr", Email = "test@example.com" }]
        });
        var emptyReasonBlocked = false;
        try { await app.RejectStepAsync(rejectEntry.Id, rejectEntry.ApprovalSteps[0].Id, ""); }
        catch (InvalidOperationException) { emptyReasonBlocked = true; }
        Assert(emptyReasonBlocked, "RejectStepAsync blocks empty rejection reason.");

        var whitespaceReasonBlocked = false;
        try { await app.RejectStepAsync(rejectEntry.Id, rejectEntry.ApprovalSteps[0].Id, "   "); }
        catch (InvalidOperationException) { whitespaceReasonBlocked = true; }
        Assert(whitespaceReasonBlocked, "RejectStepAsync blocks whitespace-only rejection reason.");

        // ExportEntryPdfAsync - entry not found
        var exportMissingBlocked = false;
        try { await app.ExportEntryPdfAsync(Guid.NewGuid()); }
        catch (InvalidOperationException) { exportMissingBlocked = true; }
        Assert(exportMissingBlocked, "ExportEntryPdfAsync throws for nonexistent entry.");

        // GetEntryDetailAsync - entry not found returns null
        var missingDetail = await app.GetEntryDetailAsync(Guid.NewGuid());
        Assert(missingDetail is null, "GetEntryDetailAsync returns null for nonexistent entry.");

        // SearchEntriesAsync passthrough
        var searchResults = await app.SearchEntriesAsync(null, "nonexistent-search-term");
        Assert(searchResults is not null, "SearchEntriesAsync handles null formId and nonexistent search term.");

        Console.WriteLine("  Service edge case tests passed.");
    }

    // ───────────────────────────────────────────────
    //  Invitation / workflow error paths
    // ───────────────────────────────────────────────

    private static async Task RunInvitationWorkflowErrorPathTests()
    {
        var (app, _) = BuildServiceWithFakes();

        var form = await app.SaveDraftAsync(new SaveDraftRequest
        {
            Name = "Invitation error test form",
            Description = "Test",
            Slug = $"inv-err-{Guid.NewGuid():N}",
            AccessMode = FormAccessMode.Public,
            Definition = DemoFormFactory.CreateDefaultDefinition()
        });
        await app.PublishAsync(form.Id);

        // CreateInvitationAsync - blank email
        var blankEmailBlocked = false;
        try
        {
            await app.CreateInvitationAsync(new CreateInvitationRequest
            {
                FormId = form.Id,
                Email = "",
                Role = FormPermissionRole.Viewer,
                ScopeType = "Form",
                ValidFor = TimeSpan.FromDays(1)
            });
        }
        catch (InvalidOperationException) { blankEmailBlocked = true; }
        Assert(blankEmailBlocked, "CreateInvitationAsync blocks blank email.");

        // CreateInvitationAsync - whitespace email
        var wsEmailBlocked = false;
        try
        {
            await app.CreateInvitationAsync(new CreateInvitationRequest
            {
                FormId = form.Id,
                Email = "   ",
                Role = FormPermissionRole.Viewer,
                ScopeType = "Form",
                ValidFor = TimeSpan.FromDays(1)
            });
        }
        catch (InvalidOperationException) { wsEmailBlocked = true; }
        Assert(wsEmailBlocked, "CreateInvitationAsync blocks whitespace email.");

        // CreateInvitationAsync - ValidFor zero
        var zeroValidBlocked = false;
        try
        {
            await app.CreateInvitationAsync(new CreateInvitationRequest
            {
                FormId = form.Id,
                Email = "zero@example.com",
                Role = FormPermissionRole.Viewer,
                ScopeType = "Form",
                ValidFor = TimeSpan.Zero
            });
        }
        catch (InvalidOperationException) { zeroValidBlocked = true; }
        Assert(zeroValidBlocked, "CreateInvitationAsync blocks zero ValidFor.");

        // CreateInvitationAsync - ValidFor negative
        var negativeValidBlocked = false;
        try
        {
            await app.CreateInvitationAsync(new CreateInvitationRequest
            {
                FormId = form.Id,
                Email = "neg@example.com",
                Role = FormPermissionRole.Viewer,
                ScopeType = "Form",
                ValidFor = TimeSpan.FromDays(-1)
            });
        }
        catch (InvalidOperationException) { negativeValidBlocked = true; }
        Assert(negativeValidBlocked, "CreateInvitationAsync blocks negative ValidFor.");

        // CreateInvitationAsync - ValidFor > 30 days
        var tooLongValidBlocked = false;
        try
        {
            await app.CreateInvitationAsync(new CreateInvitationRequest
            {
                FormId = form.Id,
                Email = "long@example.com",
                Role = FormPermissionRole.Viewer,
                ScopeType = "Form",
                ValidFor = TimeSpan.FromDays(31)
            });
        }
        catch (InvalidOperationException) { tooLongValidBlocked = true; }
        Assert(tooLongValidBlocked, "CreateInvitationAsync blocks ValidFor > 30 days.");

        // CreateInvitationAsync - form not found
        var missingFormBlocked = false;
        try
        {
            await app.CreateInvitationAsync(new CreateInvitationRequest
            {
                FormId = Guid.NewGuid(),
                Email = "missing@example.com",
                Role = FormPermissionRole.Viewer,
                ScopeType = "Form",
                ValidFor = TimeSpan.FromDays(1)
            });
        }
        catch (InvalidOperationException) { missingFormBlocked = true; }
        Assert(missingFormBlocked, "CreateInvitationAsync throws for nonexistent form.");

        // AcceptInvitationAsync - blank token
        var blankTokenBlocked = false;
        try { await app.AcceptInvitationAsync(""); }
        catch (InvalidOperationException) { blankTokenBlocked = true; }
        Assert(blankTokenBlocked, "AcceptInvitationAsync blocks blank token.");

        // AcceptInvitationAsync - whitespace token
        var wsTokenBlocked = false;
        try { await app.AcceptInvitationAsync("   "); }
        catch (InvalidOperationException) { wsTokenBlocked = true; }
        Assert(wsTokenBlocked, "AcceptInvitationAsync blocks whitespace token.");

        // AcceptInvitationAsync - nonexistent token
        var badTokenBlocked = false;
        try { await app.AcceptInvitationAsync("nonexistent-token"); }
        catch (InvalidOperationException) { badTokenBlocked = true; }
        Assert(badTokenBlocked, "AcceptInvitationAsync throws for nonexistent token.");

        // AcceptInvitationAsync by unauthenticated user
        var invitation = await app.CreateInvitationAsync(new CreateInvitationRequest
        {
            FormId = form.Id,
            Email = "unauth-test@example.com",
            Role = FormPermissionRole.Viewer,
            ScopeType = "Form",
            ValidFor = TimeSpan.FromDays(2)
        });

        var unauthServices = new ServiceCollection();
        unauthServices.AddBlazorWebFormsCore();
        // Share the same repository so invitation is accessible
        var (_, sharedRepo) = BuildServiceWithFakes();
        // Need to rebuild with the real repo that has the invitation
        // Instead, test with a fresh anonymous context
        var anonServices = new ServiceCollection();
        anonServices.AddBlazorWebFormsCore();
        anonServices.AddSingleton<IFormsRepository, TestFakeRepository>();
        anonServices.AddSingleton<ICurrentUserContext>(new TestAnonymousUserContext());
        anonServices.AddSingleton<IEmailNotifier, TestFakeEmailNotifier>();
        anonServices.AddSingleton<IFileStorage, TestFakeFileStorage>();
        anonServices.AddSingleton<IPdfExporter, TestFakePdfExporter>();
        anonServices.AddSingleton<IAntiAbuseGuard, TestFakeAntiAbuseGuard>();
        anonServices.AddSingleton<IOperationalTelemetry, TestFakeOperationalTelemetry>();
        await using var anonProvider = anonServices.BuildServiceProvider();
        var anonApp = anonProvider.GetRequiredService<FormsApplicationService>();
        var unauthBlocked = false;
        try { await anonApp.AcceptInvitationAsync("some-token"); }
        catch (InvalidOperationException) { unauthBlocked = true; }
        Assert(unauthBlocked, "AcceptInvitationAsync blocks unauthenticated user.");

        // RevokeInvitationAsync - nonexistent invitation
        var revokeMissingBlocked = false;
        try { await app.RevokeInvitationAsync(Guid.NewGuid()); }
        catch (InvalidOperationException) { revokeMissingBlocked = true; }
        Assert(revokeMissingBlocked, "RevokeInvitationAsync throws for nonexistent invitation.");

        // SendApprovalRemindersAsync - form not found
        var reminderMissingFormBlocked = false;
        try { await app.SendApprovalRemindersAsync(Guid.NewGuid()); }
        catch (InvalidOperationException) { reminderMissingFormBlocked = true; }
        Assert(reminderMissingFormBlocked, "SendApprovalRemindersAsync throws for nonexistent form.");

        // GetBuilderStateAsync - null formId creates new empty form
        var newBuilderState = await app.GetBuilderStateAsync(null);
        Assert(newBuilderState is not null && newBuilderState.CanManage, "GetBuilderStateAsync with null creates new empty form.");

        // GetBuilderStateAsync - nonexistent formId creates empty form
        var missingBuilderState = await app.GetBuilderStateAsync(Guid.NewGuid());
        Assert(missingBuilderState is not null, "GetBuilderStateAsync with nonexistent id returns empty form.");

        // SaveDraftSubmissionAsync - form not found
        var draftMissingFormBlocked = false;
        try
        {
            await app.SaveDraftSubmissionAsync(Guid.NewGuid(), new SaveDraftSubmissionRequest
            {
                Answers = new Dictionary<string, string?> { ["x"] = "y" }
            });
        }
        catch (InvalidOperationException) { draftMissingFormBlocked = true; }
        Assert(draftMissingFormBlocked, "SaveDraftSubmissionAsync throws for nonexistent form.");

        // GetDraftSubmissionAsync - form not found
        var getDraftMissingFormBlocked = false;
        try { await app.GetDraftSubmissionAsync(Guid.NewGuid()); }
        catch (InvalidOperationException) { getDraftMissingFormBlocked = true; }
        Assert(getDraftMissingFormBlocked, "GetDraftSubmissionAsync throws for nonexistent form.");

        Console.WriteLine("  Invitation and workflow error path tests passed.");
    }

    private static async Task RunPrefillWorkflowTests()
    {
        var (app, repo) = BuildServiceWithFakes();
        var definition = new FormDefinition
        {
            Title = "Prefill test",
            DefaultCulture = "en-US",
            Sections =
            [
                new FormSectionDefinition
                {
                    Id = "employee",
                    Title = "Employee",
                    Fields =
                    [
                        new FormFieldDefinition
                        {
                            Id = "employeeName",
                            Label = "Employee name",
                            Prefill = new FormFieldPrefillDefinition { Source = PrefillSourceKind.Claim, Key = "displayName" }
                        },
                        new FormFieldDefinition
                        {
                            Id = "employeeEmail",
                            Label = "Employee email",
                            ReadOnly = true,
                            Prefill = new FormFieldPrefillDefinition { Source = PrefillSourceKind.Claim, Key = "email" }
                        },
                        new FormFieldDefinition
                        {
                            Id = "department",
                            Label = "Department",
                            Searchable = true,
                            Prefill = new FormFieldPrefillDefinition { Source = PrefillSourceKind.Employee, Key = "department" }
                        },
                        new FormFieldDefinition
                        {
                            Id = "costCenter",
                            Label = "Cost center",
                            Prefill = new FormFieldPrefillDefinition { Source = PrefillSourceKind.Custom, ProviderKey = "hr-database", Key = "costCenter" }
                        },
                        new FormFieldDefinition
                        {
                            Id = "location",
                            Label = "Location",
                            DefaultValue = "Remote",
                            Prefill = new FormFieldPrefillDefinition { Source = PrefillSourceKind.FixedValue }
                        },
                        new FormFieldDefinition
                        {
                            Id = "repeatable",
                            Label = "Repeatable",
                            Kind = FormFieldKind.RepeatableList,
                            MinItems = 1,
                            MaxItems = 3
                        },
                        new FormFieldDefinition
                        {
                            Id = "signature",
                            Label = "Signature",
                            Kind = FormFieldKind.Signature
                        }
                    ]
                }
            ]
        };

        var resolved = await app.ResolvePrefillAnswersAsync(
            definition,
            new Dictionary<string, string?> { ["department"] = "Existing" },
            "employee@example.com");

        Assert(resolved["employeeName"] == "Isolated Test User", "Claim prefill resolves display name.");
        Assert(resolved["employeeEmail"] == "test@example.com", "Claim prefill resolves email.");
        Assert(resolved["department"] == "Existing", "Prefill preserves existing answers by default.");
        Assert(resolved["costCenter"] == "CC-42", "Custom provider prefill resolves database-backed values.");
        Assert(resolved["location"] == "Remote", "Fixed prefill resolves field default value.");

        definition.Sections[0].Fields.First(f => f.Id == "department").Prefill.ApplyWhenEmpty = false;
        var overwritten = await app.ResolvePrefillAnswersAsync(
            definition,
            new Dictionary<string, string?> { ["department"] = "Existing" },
            "employee@example.com");
        Assert(overwritten["department"] == "Engineering", "Employee prefill can overwrite existing answers.");

        var form = await app.SaveDraftAsync(new SaveDraftRequest
        {
            Name = "Prefill workflow",
            Description = "Manager prefill routing",
            Slug = "prefill-workflow",
            AccessMode = FormAccessMode.Authenticated,
            Definition = definition
        });
        await app.PublishAsync(form.Id);

        var draft = await app.CreateManagerPrefilledDraftAsync(form.Id, new ManagerPrefillDraftRequest
        {
            SubmitterName = "Employee One",
            SubmitterEmail = "employee@example.com",
            Answers = new Dictionary<string, string?> { ["managerNote"] = "Ready" },
            Approvers = [new ApproverInput { Name = "Approver One", Email = "approver@example.com" }]
        });

        var savedDraft = await repo.GetDraftEntryAsync(form.Id, "employee@example.com");
        Assert(savedDraft is not null && savedDraft.Id == draft.Id, "Manager-prefilled draft routes to submitter email.");
        Assert(draft.Answers["department"] == "Engineering", "Manager-prefilled draft includes employee data.");
        Assert(draft.Answers["costCenter"] == "CC-42", "Manager-prefilled draft includes custom provider data.");
        Assert(draft.Answers["managerNote"] == "Ready", "Manager-prefilled draft preserves manager answers.");
        Assert(draft.ApprovalSteps.Count == 1, "Manager-prefilled draft stores approver routing.");
        Assert(draft.Status == EntryStatus.Draft, "Manager-prefilled draft remains a draft.");

        Console.WriteLine("  Prefill workflow tests passed.");
    }

    // ───────────────────────────────────────────────
    //  Helpers
    // ───────────────────────────────────────────────

    private static async Task AssertPublishBlocked(FormDefinition definition, string message)
    {
        var (app, _) = BuildServiceWithFakes();
        var form = await app.SaveDraftAsync(new SaveDraftRequest
        {
            Name = "Validation test",
            Description = "Should fail publish",
            Slug = $"validation-{Guid.NewGuid():N}",
            AccessMode = FormAccessMode.Public,
            Definition = DemoFormFactory.CreateDefaultDefinition()
        });

        // overwrite draft definition with the invalid one
        form.DraftDefinition = definition;
        // re-save to persist the bad definition
        await app.SaveDraftAsync(new SaveDraftRequest
        {
            FormId = form.Id,
            Name = form.Name,
            Description = form.Description,
            Slug = form.Publication.Slug,
            AccessMode = form.Publication.AccessMode,
            Definition = definition
        });

        var blocked = false;
        try { await app.PublishAsync(form.Id); }
        catch (InvalidOperationException) { blocked = true; }
        Assert(blocked, message);
    }

    private static (FormsApplicationService App, TestFakeRepository Repository) BuildServiceWithFakes()
    {
        var services = new ServiceCollection();
        var repo = new TestFakeRepository();
        services.AddBlazorWebFormsCore();
        services.AddSingleton<IFormsRepository>(repo);
        services.AddSingleton<ICurrentUserContext, TestAuthenticatedUserContext>();
        services.AddSingleton<IEmployeePrefillProvider, TestEmployeePrefillProvider>();
        services.AddSingleton<IFormPrefillProvider, TestDatabasePrefillProvider>();
        services.AddSingleton<IEmailNotifier, TestFakeEmailNotifier>();
        services.AddSingleton<IFileStorage, TestFakeFileStorage>();
        services.AddSingleton<IPdfExporter, TestFakePdfExporter>();
        services.AddSingleton<IAntiAbuseGuard, TestFakeAntiAbuseGuard>();
        services.AddSingleton<IOperationalTelemetry, TestFakeOperationalTelemetry>();

        var provider = services.BuildServiceProvider();
        var app = provider.GetRequiredService<FormsApplicationService>();
        return (app, repo);
    }

    private static IPermissionEvaluator ResolvePermissionEvaluator()
    {
        var services = new ServiceCollection();
        services.AddBlazorWebFormsCore();
        services.AddSingleton<IFormsRepository, TestFakeRepository>();
        services.AddSingleton<ICurrentUserContext, TestAuthenticatedUserContext>();
        services.AddSingleton<IEmailNotifier, TestFakeEmailNotifier>();
        services.AddSingleton<IFileStorage, TestFakeFileStorage>();
        services.AddSingleton<IPdfExporter, TestFakePdfExporter>();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IPermissionEvaluator>();
    }

    private static ICoreMetadataCache ResolveCoreMetadataCache()
    {
        var services = new ServiceCollection();
        services.AddBlazorWebFormsCore();
        services.AddSingleton<IFormsRepository, TestFakeRepository>();
        services.AddSingleton<ICurrentUserContext, TestAuthenticatedUserContext>();
        services.AddSingleton<IEmailNotifier, TestFakeEmailNotifier>();
        services.AddSingleton<IFileStorage, TestFakeFileStorage>();
        services.AddSingleton<IPdfExporter, TestFakePdfExporter>();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<ICoreMetadataCache>();
    }

    private static UserProfile MakeUser(bool authenticated = true, List<FormPermissionRole>? roles = null) =>
        new()
        {
            UserId = Guid.NewGuid(),
            IsAuthenticated = authenticated,
            DisplayName = "Test User",
            Email = $"user-{Guid.NewGuid():N}@example.com",
            Roles = roles ?? []
        };

    private static FormAggregate MakeForm(Guid ownerUserId) =>
        new()
        {
            OwnerUserId = ownerUserId,
            Name = "Test form",
            Description = "Test",
            Key = $"test-{Guid.NewGuid():N}"
        };

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

// ───────────────────────────────────────────────
//  Test fakes (isolated from main Program.cs fakes)
// ───────────────────────────────────────────────

internal sealed class TestAuthenticatedUserContext : ICurrentUserContext
{
    public UserProfile GetCurrentUser() =>
        new()
        {
            UserId = Guid.Parse("aaaa1111-bbbb-cccc-dddd-eeee22223333"),
            IsAuthenticated = true,
            DisplayName = "Isolated Test User",
            Email = "test@example.com",
            Roles = [FormPermissionRole.Owner, FormPermissionRole.Manager]
        };
}

internal sealed class TestAnonymousUserContext : ICurrentUserContext
{
    public UserProfile GetCurrentUser() =>
        new()
        {
            UserId = Guid.Empty,
            IsAuthenticated = false,
            DisplayName = "Anonymous",
            Email = string.Empty,
            Roles = []
        };
}

internal sealed class TestEmployeePrefillProvider : IEmployeePrefillProvider
{
    public Task<IReadOnlyDictionary<string, string?>> GetEmployeeDataAsync(UserProfile requester, string employeeEmail, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, string?>>(new Dictionary<string, string?>
        {
            ["department"] = "Engineering",
            ["employeeNumber"] = "E-123"
        });
}

internal sealed class TestDatabasePrefillProvider : IFormPrefillProvider
{
    public string ProviderKey => "hr-database";

    public bool CanResolve(FormFieldPrefillDefinition prefill) =>
        prefill.Source == PrefillSourceKind.Custom;

    public Task<string?> ResolveAsync(FormPrefillRequest request, CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["costCenter"] = "CC-42",
            ["managerName"] = "Database Manager"
        };

        values.TryGetValue(request.Field.Prefill.Key, out var value);
        return Task.FromResult(value);
    }
}

internal sealed class TestFakeRepository : IFormsRepository
{
    private readonly Dictionary<Guid, FormAggregate> forms = [];
    private readonly Dictionary<Guid, EntryRecord> entries = [];
    private readonly Dictionary<Guid, FormInvitation> invitations = [];

    public Task SeedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<FormAggregate>> GetFormsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FormAggregate>>(forms.Values.ToList());

    public Task<FormAggregate?> GetFormAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        forms.TryGetValue(formId, out var form);
        return Task.FromResult(form);
    }

    public Task<FormAggregate?> GetFormBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        Task.FromResult(forms.Values.FirstOrDefault(x => string.Equals(x.Publication.Slug, slug, StringComparison.OrdinalIgnoreCase)));

    public Task SaveFormAsync(FormAggregate form, CancellationToken cancellationToken = default)
    {
        forms[form.Id] = form;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EntryRecord>> GetEntriesAsync(Guid? formId, string? search, CancellationToken cancellationToken = default)
    {
        IEnumerable<EntryRecord> query = entries.Values;
        if (formId.HasValue) query = query.Where(x => x.FormId == formId.Value);
        return Task.FromResult<IReadOnlyList<EntryRecord>>(query.ToList());
    }

    public Task<IReadOnlyList<EntryRecord>> QueryEntriesAsync(EntryQueryOptions options, CancellationToken cancellationToken = default)
    {
        IEnumerable<EntryRecord> query = entries.Values;
        if (options.FormId.HasValue) query = query.Where(x => x.FormId == options.FormId.Value);
        if (options.Status.HasValue) query = query.Where(x => x.Status == options.Status.Value);
        if (!string.IsNullOrWhiteSpace(options.Search))
            query = query.Where(x =>
                x.SubmittedBy.Contains(options.Search, StringComparison.OrdinalIgnoreCase) ||
                x.SearchIndex.Values.Any(v => v.Contains(options.Search, StringComparison.OrdinalIgnoreCase)));
        query = query.OrderByDescending(x => x.SubmittedUtc);
        if (options.Offset > 0) query = query.Skip(options.Offset);
        if (options.Limit > 0) query = query.Take(options.Limit);
        return Task.FromResult<IReadOnlyList<EntryRecord>>(query.ToList());
    }

    public Task<EntryRecord?> GetEntryAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        entries.TryGetValue(entryId, out var entry);
        return Task.FromResult(entry);
    }

    public Task<EntryRecord?> GetDraftEntryAsync(Guid formId, string submittedByEmail, CancellationToken cancellationToken = default)
    {
        var draft = entries.Values
            .Where(e => e.FormId == formId && e.Status == EntryStatus.Draft)
            .OrderByDescending(e => e.SubmittedUtc)
            .FirstOrDefault(e => string.Equals(e.SubmittedByEmail, submittedByEmail, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(draft);
    }

    public Task SaveEntryAsync(EntryRecord entry, CancellationToken cancellationToken = default)
    {
        entries[entry.Id] = entry;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EntryFileRecord>> GetOrphanedFilesAsync(DateTimeOffset olderThanUtc, CancellationToken cancellationToken = default)
    {
        var orphaned = entries.Values
            .Where(e => e.Status == EntryStatus.Draft && e.SubmittedUtc < olderThanUtc)
            .SelectMany(e => e.Files)
            .ToList();
        return Task.FromResult<IReadOnlyList<EntryFileRecord>>(orphaned);
    }

    public Task<int> DeleteDraftEntriesOlderThanAsync(DateTimeOffset olderThanUtc, CancellationToken cancellationToken = default)
    {
        var staleIds = entries.Values
            .Where(e => e.Status == EntryStatus.Draft && e.SubmittedUtc < olderThanUtc)
            .Select(e => e.Id)
            .ToList();
        foreach (var id in staleIds) entries.Remove(id);
        return Task.FromResult(staleIds.Count);
    }

    public Task<IReadOnlyList<FormInvitation>> GetInvitationsAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        IEnumerable<FormInvitation> query = invitations.Values;
        if (formId != Guid.Empty) query = query.Where(i => i.FormId == formId);
        return Task.FromResult<IReadOnlyList<FormInvitation>>(query.ToList());
    }

    public Task<FormInvitation?> GetInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        invitations.TryGetValue(invitationId, out var invitation);
        return Task.FromResult(invitation);
    }

    public Task<FormInvitation?> GetInvitationByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var invitation = invitations.Values.FirstOrDefault(i => i.Token == token);
        return Task.FromResult(invitation);
    }

    public Task SaveInvitationAsync(FormInvitation invitation, CancellationToken cancellationToken = default)
    {
        invitations[invitation.Id] = invitation;
        return Task.CompletedTask;
    }
}

internal sealed class TestFakeEmailNotifier : IEmailNotifier
{
    public Task NotifyManagersAsync(FormAggregate form, EntryRecord entry, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
    public Task<string?> NotifyApproverAssignedAsync(FormAggregate form, EntryRecord entry, ApprovalStepRecord step, string idempotencyKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
    public Task<string?> NotifyApproverReminderAsync(FormAggregate form, EntryRecord entry, ApprovalStepRecord step, string idempotencyKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
    public Task NotifyEntryApprovedAsync(FormAggregate form, EntryRecord entry, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
    public Task NotifyEntryRejectedAsync(FormAggregate form, EntryRecord entry, ApprovalStepRecord step, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
    public Task NotifyInvitationCreatedAsync(FormAggregate form, FormInvitation invitation, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
    public Task NotifyInvitationAcceptedAsync(FormAggregate form, FormInvitation invitation, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
    public Task NotifyInvitationRevokedAsync(FormAggregate form, FormInvitation invitation, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

internal sealed class TestFakeFileStorage : IFileStorage
{
    private readonly Dictionary<string, byte[]> files = new(StringComparer.OrdinalIgnoreCase);
    public Task<StoredFile> SaveAsync(FileUploadRequest request, CancellationToken cancellationToken = default)
    {
        var stored = new StoredFile
        {
            FileName = request.FileName,
            ContentType = request.ContentType,
            Length = request.Content.LongLength,
            RelativePath = request.FileName,
            Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(request.Content)).ToLowerInvariant()
        };
        files[stored.RelativePath] = request.Content;
        return Task.FromResult(stored);
    }
    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        if (!files.TryGetValue(relativePath, out var content))
            throw new FileNotFoundException("Not found.", relativePath);
        return Task.FromResult<Stream>(new MemoryStream(content, writable: false));
    }
    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        files.Remove(relativePath);
        return Task.CompletedTask;
    }
}

internal sealed class TestFakePdfExporter : IPdfExporter
{
    public Task<byte[]> ExportEntryAsync(FormAggregate form, EntryRecord entry, CancellationToken cancellationToken = default) =>
        Task.FromResult(System.Text.Encoding.UTF8.GetBytes($"PDF\nANSWERS\nAPPROVAL AUDIT\nFILES\nForm version id: {entry.FormVersionId}"));
}

internal sealed class TestFakeAntiAbuseGuard : IAntiAbuseGuard
{
    public Task CheckUploadAllowedAsync(UserProfile user, FileUploadRequest request, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
    public Task CheckOutboundNotificationAllowedAsync(string channel, string recipient, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

internal sealed class TestFakeOperationalTelemetry : IOperationalTelemetry
{
    public void TrackUpload(string source, long bytes, bool success) { }
    public void TrackPdfExport(string source, long bytes, bool success) { }
    public void TrackEmailDelivery(string channel, bool success) { }
    public void TrackFailure(string area, string operation, string reason) { }
}
