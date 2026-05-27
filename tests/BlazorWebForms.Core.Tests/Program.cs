using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;
using BlazorWebForms.Core.Services;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
var primaryNotifier = new FakeEmailNotifier();
services.AddBlazorWebFormsCore();
services.AddSingleton<IFormsRepository, FakeRepository>();
services.AddSingleton<ICurrentUserContext, FakeCurrentUserContext>();
services.AddSingleton<IEmailNotifier>(primaryNotifier);
services.AddSingleton<IFileStorage, FakeFileStorage>();

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
Assert(roundTrip.SchemaVersion == FormDefinition.CurrentSchemaVersion, "Definition round-trip preserves current schema version.");

var legacyJson = """
{
  "title": "Legacy",
  "description": "No schema version",
  "sections": []
}
""";
var legacyDefinition = serializer.Deserialize(legacyJson);
Assert(legacyDefinition.SchemaVersion == FormDefinition.CurrentSchemaVersion, "Legacy definitions are upgraded on read.");

var unsupportedSchemaBlocked = false;
try
{
    serializer.Deserialize("{\"schemaVersion\":999,\"title\":\"Future\",\"sections\":[]}");
}
catch (InvalidOperationException)
{
    unsupportedSchemaBlocked = true;
}
Assert(unsupportedSchemaBlocked, "Unsupported future schema versions are rejected.");

Assert(conditionEvaluator.IsVisible("needsHotel=yes", new Dictionary<string, string?> { ["needsHotel"] = "yes" }), "Condition evaluator matches field=value.");

var sectionRules = new VisibilityConditionDefinition
{
    Join = VisibilityJoinOperator.And,
    Rules =
    [
        new VisibilityRuleDefinition { FieldId = "department", Operator = VisibilityRuleOperator.Equals, Value = "finance" },
        new VisibilityRuleDefinition { FieldId = "comment", Operator = VisibilityRuleOperator.Contains, Value = "urgent" }
    ]
};
Assert(conditionEvaluator.IsVisible(sectionRules, new Dictionary<string, string?>
{
    ["department"] = "finance",
    ["comment"] = "needs urgent review"
}), "Structured AND rules evaluate visible when all rules pass.");

var fieldRules = new VisibilityConditionDefinition
{
    Join = VisibilityJoinOperator.Or,
    Rules =
    [
        new VisibilityRuleDefinition { FieldId = "status", Operator = VisibilityRuleOperator.NotEquals, Value = "closed" },
        new VisibilityRuleDefinition { FieldId = "notes", Operator = VisibilityRuleOperator.Empty }
    ]
};
Assert(conditionEvaluator.IsVisible(fieldRules, new Dictionary<string, string?> { ["status"] = "open" }), "Structured OR rules evaluate visible when one rule passes.");
Assert(!conditionEvaluator.IsVisible(fieldRules, new Dictionary<string, string?>
{
    ["status"] = "closed",
    ["notes"] = "has text"
}), "Structured OR rules evaluate hidden when no rules pass.");

var renderDefinition = new FormDefinition
{
    Title = "Render visibility test",
    Sections =
    [
        new FormSectionDefinition
        {
            Id = "s1",
            Title = "Visible section",
            VisibilityRules = new VisibilityConditionDefinition
            {
                Join = VisibilityJoinOperator.And,
                Rules =
                [
                    new VisibilityRuleDefinition { FieldId = "department", Operator = VisibilityRuleOperator.Equals, Value = "finance" }
                ]
            },
            Fields =
            [
                new FormFieldDefinition { Id = "department", Label = "Department" },
                new FormFieldDefinition
                {
                    Id = "managerNotes",
                    Label = "Manager notes",
                    VisibilityRules = new VisibilityConditionDefinition
                    {
                        Join = VisibilityJoinOperator.Or,
                        Rules =
                        [
                            new VisibilityRuleDefinition { FieldId = "priority", Operator = VisibilityRuleOperator.Equals, Value = "high" },
                            new VisibilityRuleDefinition { FieldId = "comment", Operator = VisibilityRuleOperator.Contains, Value = "urgent" }
                        ]
                    }
                }
            ]
        },
        new FormSectionDefinition
        {
            Id = "s2",
            Title = "Hidden section",
            VisibilityRules = new VisibilityConditionDefinition
            {
                Join = VisibilityJoinOperator.And,
                Rules =
                [
                    new VisibilityRuleDefinition { FieldId = "department", Operator = VisibilityRuleOperator.Equals, Value = "legal" }
                ]
            },
            Fields = [new FormFieldDefinition { Id = "legalOnly", Label = "Legal only" }]
        }
    ]
};

var renderAnswers = new Dictionary<string, string?>
{
    ["department"] = "finance",
    ["priority"] = "low",
    ["comment"] = "urgent escalation"
};

var visibleSections = renderDefinition.Sections
    .Where(s => conditionEvaluator.IsVisible(s.VisibilityRules, renderAnswers))
    .ToList();
Assert(visibleSections.Count == 1 && visibleSections[0].Id == "s1", "Section-level conditional rendering shows only matching sections.");

var visibleFields = visibleSections[0].Fields
    .Where(f => conditionEvaluator.IsVisible(f.VisibilityRules, renderAnswers))
    .Select(f => f.Id)
    .ToList();
Assert(visibleFields.Contains("department") && visibleFields.Contains("managerNotes"), "Field-level conditional rendering shows matching fields.");

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

var invalidRegexDefinition = DemoFormFactory.CreateDefaultDefinition();
invalidRegexDefinition.Sections[0].Fields[0].RegexPattern = "[";
var invalidRegexForm = await app.SaveDraftAsync(new SaveDraftRequest
{
    Name = "Invalid regex form",
    Description = "Should fail publish",
    Slug = "invalid-regex-form",
    Definition = invalidRegexDefinition,
    AccessMode = FormAccessMode.Public
});
var invalidRegexPublishBlocked = false;
try
{
    await app.PublishAsync(invalidRegexForm.Id);
}
catch (InvalidOperationException)
{
    invalidRegexPublishBlocked = true;
}
Assert(invalidRegexPublishBlocked, "Publish blocks invalid regex patterns in draft definition.");

var brokenConditionDefinition = DemoFormFactory.CreateDefaultDefinition();
brokenConditionDefinition.Sections[0].Fields[0].VisibilityCondition = "unknownField=yes";
var brokenConditionForm = await app.SaveDraftAsync(new SaveDraftRequest
{
    Name = "Broken condition form",
    Description = "Should fail publish",
    Slug = "broken-condition-form",
    Definition = brokenConditionDefinition,
    AccessMode = FormAccessMode.Public
});
var brokenConditionPublishBlocked = false;
try
{
    await app.PublishAsync(brokenConditionForm.Id);
}
catch (InvalidOperationException)
{
    brokenConditionPublishBlocked = true;
}
Assert(brokenConditionPublishBlocked, "Publish blocks visibility conditions that reference unknown fields.");

var consistencyDefinition = DemoFormFactory.CreateDefaultDefinition();
consistencyDefinition.Sections.Reverse();
consistencyDefinition.Sections[0].VisibilityCondition = "employeeName=Jordan";
consistencyDefinition.Sections[0].Fields.Reverse();
var consistentField = consistencyDefinition.Sections[0].Fields[0];
consistentField.Placeholder = "Type notes";
consistentField.HelpText = "Include budget context.";
consistentField.RegexPattern = "^[A-Za-z0-9 ,.?!-]+$";
consistentField.DefaultValue = "Default note";
consistentField.ValidationHint = "Use letters, numbers, punctuation.";
consistentField.VisibilityCondition = "needsHotel=yes";
consistentField.Options =
[
    new FormFieldOption { Value = "alpha", Label = "Alpha" },
    new FormFieldOption { Value = "beta", Label = "Beta" }
];

var consistencyForm = await app.SaveDraftAsync(new SaveDraftRequest
{
    Name = "Consistency form",
    Description = "Draft consistency checks",
    Slug = "consistency-form",
    AccessMode = FormAccessMode.Public,
    Definition = consistencyDefinition
});

var consistencyBuilderState = await app.GetBuilderStateAsync(consistencyForm.Id);
var persistedSection = consistencyBuilderState.Form.DraftDefinition.Sections[0];
var persistedField = persistedSection.Fields[0];
Assert(persistedSection.VisibilityCondition == "employeeName=Jordan", "Section visibility condition persists with draft save.");
Assert(persistedField.Placeholder == "Type notes", "Field placeholder persists with draft save.");
Assert(persistedField.HelpText == "Include budget context.", "Field help text persists with draft save.");
Assert(persistedField.RegexPattern == "^[A-Za-z0-9 ,.?!-]+$", "Field regex pattern persists with draft save.");
Assert(persistedField.DefaultValue == "Default note", "Field default value persists with draft save.");
Assert(persistedField.ValidationHint == "Use letters, numbers, punctuation.", "Field validation hint persists with draft save.");
Assert(persistedField.VisibilityCondition == "needsHotel=yes", "Field visibility condition persists with draft save.");
Assert(persistedField.Options.Count == 2 && persistedField.Options[0].Value == "alpha" && persistedField.Options[1].Value == "beta", "Field options persist in order with draft save.");

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

var dashboard = await app.GetDashboardAsync();
Assert(dashboard.CurrentUser.IsAuthenticated, "Dashboard includes authenticated current user profile.");

var detail = await app.GetEntryDetailAsync(entry.Id);
Assert(detail is not null && detail.CanView, "Authorized user can view entry detail.");

// negative authorization checks
services = new ServiceCollection();
services.AddBlazorWebFormsCore();
services.AddSingleton<IFormsRepository>(repository);
services.AddSingleton<ICurrentUserContext, FakeAnonymousCurrentUserContext>();
services.AddSingleton<IEmailNotifier>(new FakeEmailNotifier());
services.AddSingleton<IFileStorage, FakeFileStorage>();

await using var anonymousProvider = services.BuildServiceProvider();
var anonymousApp = anonymousProvider.GetRequiredService<FormsApplicationService>();

var threwOnBuilder = false;
try
{
    await anonymousApp.GetBuilderStateAsync(form.Id);
}
catch (InvalidOperationException)
{
    threwOnBuilder = true;
}
Assert(threwOnBuilder, "Anonymous user cannot access builder state.");

var anonymousDetail = await anonymousApp.GetEntryDetailAsync(entry.Id);
Assert(anonymousDetail is null, "Anonymous user cannot view entry detail and receives no existence signal.");

// admin bypass checks
services = new ServiceCollection();
services.AddBlazorWebFormsCore();
services.AddSingleton<IFormsRepository>(repository);
services.AddSingleton<ICurrentUserContext, FakeAdminCurrentUserContext>();
services.AddSingleton<IEmailNotifier>(new FakeEmailNotifier());
services.AddSingleton<IFileStorage, FakeFileStorage>();

await using var adminProvider = services.BuildServiceProvider();
var adminApp = adminProvider.GetRequiredService<FormsApplicationService>();
var adminDetail = await adminApp.GetEntryDetailAsync(entry.Id);
Assert(adminDetail is not null && adminDetail.CanView, "Admin role can view entry detail.");

var approverUser = new UserProfile
{
    UserId = Guid.NewGuid(),
    IsAuthenticated = true,
    DisplayName = "Approver User",
    Email = "lead@example.com",
    Roles = [FormPermissionRole.Approver]
};
services = new ServiceCollection();
services.AddBlazorWebFormsCore();
services.AddSingleton<IFormsRepository>(repository);
services.AddSingleton<ICurrentUserContext>(new FakeFixedCurrentUserContext(approverUser));
services.AddSingleton<IEmailNotifier>(new FakeEmailNotifier());
services.AddSingleton<IFileStorage, FakeFileStorage>();

await using var approverProvider = services.BuildServiceProvider();
var approverApp = approverProvider.GetRequiredService<FormsApplicationService>();
var approvedByApprover = await approverApp.ApproveStepAsync(entry.Id, revised.ApprovalSteps[0].Id, "Lead");
Assert(approvedByApprover.Status == EntryStatus.Approved, "Approver can approve assigned step.");

// permission escalation regression: submitter cannot publish
var submitterUser = new UserProfile
{
    UserId = Guid.NewGuid(),
    IsAuthenticated = true,
    DisplayName = "Submitter",
    Email = "submitter@example.com",
    Roles = [FormPermissionRole.Submitter]
};
services = new ServiceCollection();
services.AddBlazorWebFormsCore();
services.AddSingleton<IFormsRepository>(repository);
services.AddSingleton<ICurrentUserContext>(new FakeFixedCurrentUserContext(submitterUser));
services.AddSingleton<IEmailNotifier>(new FakeEmailNotifier());
services.AddSingleton<IFileStorage, FakeFileStorage>();

await using var submitterProvider = services.BuildServiceProvider();
var submitterApp = submitterProvider.GetRequiredService<FormsApplicationService>();
var blockedPublish = false;
try
{
    await submitterApp.PublishAsync(form.Id);
}
catch (InvalidOperationException)
{
    blockedPublish = true;
}
Assert(blockedPublish, "Submitter role cannot publish forms.");

// invitation flow tests
var invitation = await app.CreateInvitationAsync(new CreateInvitationRequest
{
    FormId = form.Id,
    Email = "invitee@example.com",
    Role = FormPermissionRole.Viewer,
    ScopeType = "Form",
    ValidFor = TimeSpan.FromDays(2)
});
Assert(invitation.Status == InvitationStatus.Pending, "Invitation starts pending.");
Assert(primaryNotifier.CreatedInvitationIds.Contains(invitation.Id), "Invitation create sends notifier event.");

var duplicateBlocked = false;
try
{
    await app.CreateInvitationAsync(new CreateInvitationRequest
    {
        FormId = form.Id,
        Email = "invitee@example.com",
        Role = FormPermissionRole.Viewer,
        ScopeType = "Form",
        ValidFor = TimeSpan.FromDays(2)
    });
}
catch (InvalidOperationException)
{
    duplicateBlocked = true;
}
Assert(duplicateBlocked, "Duplicate pending invitation is blocked.");

var inviteeUser = new UserProfile
{
    UserId = Guid.NewGuid(),
    IsAuthenticated = true,
    DisplayName = "Invitee",
    Email = "invitee@example.com",
    Roles = [FormPermissionRole.Submitter]
};
services = new ServiceCollection();
var inviteeNotifier = new FakeEmailNotifier();
services.AddBlazorWebFormsCore();
services.AddSingleton<IFormsRepository>(repository);
services.AddSingleton<ICurrentUserContext>(new FakeFixedCurrentUserContext(inviteeUser));
services.AddSingleton<IEmailNotifier>(inviteeNotifier);
services.AddSingleton<IFileStorage, FakeFileStorage>();

await using var inviteeProvider = services.BuildServiceProvider();
var inviteeApp = inviteeProvider.GetRequiredService<FormsApplicationService>();
var accepted = await inviteeApp.AcceptInvitationAsync(invitation.Token);
Assert(accepted.Status == InvitationStatus.Accepted, "Invitation can be accepted by authenticated user.");
Assert(inviteeNotifier.AcceptedInvitationIds.Contains(invitation.Id), "Invitation accept sends notifier event.");

var formAfterInviteAccept = await app.GetBuilderStateAsync(form.Id);
Assert(formAfterInviteAccept.Form.Permissions.Any(p => p.UserId == inviteeUser.UserId), "Accepted invitation grants form permission.");

var revokedInvitation = await app.CreateInvitationAsync(new CreateInvitationRequest
{
    FormId = form.Id,
    Email = "revoke@example.com",
    Role = FormPermissionRole.Approver,
    ScopeType = "Form",
    ValidFor = TimeSpan.FromDays(2)
});
var revoked = await app.RevokeInvitationAsync(revokedInvitation.Id);
Assert(revoked.Status == InvitationStatus.Revoked, "Invitation can be revoked.");
Assert(primaryNotifier.RevokedInvitationIds.Contains(revokedInvitation.Id), "Invitation revoke sends notifier event.");

var expiredInvitation = await app.CreateInvitationAsync(new CreateInvitationRequest
{
    FormId = form.Id,
    Email = "expired@example.com",
    Role = FormPermissionRole.Viewer,
    ScopeType = "Form",
    ValidFor = TimeSpan.FromSeconds(1)
});
await Task.Delay(1100);
var expiredCaught = false;
try
{
    await inviteeApp.AcceptInvitationAsync(expiredInvitation.Token);
}
catch (InvalidOperationException)
{
    expiredCaught = true;
}
Assert(expiredCaught, "Expired invitation cannot be accepted.");

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

    public Task<IReadOnlyList<EntryRecord>> QueryEntriesAsync(EntryQueryOptions options, CancellationToken cancellationToken = default)
    {
        IEnumerable<EntryRecord> query = entries.Values;

        if (options.FormId.HasValue)
        {
            query = query.Where(x => x.FormId == options.FormId.Value);
        }

        if (options.Status.HasValue)
        {
            query = query.Where(x => x.Status == options.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(options.Search))
        {
            query = query.Where(x =>
                x.SubmittedBy.Contains(options.Search, StringComparison.OrdinalIgnoreCase) ||
                x.SearchIndex.Values.Any(v => v.Contains(options.Search, StringComparison.OrdinalIgnoreCase)));
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

    public Task<IReadOnlyList<FormInvitation>> GetInvitationsAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        IEnumerable<FormInvitation> query = invitations.Values;
        if (formId != Guid.Empty)
        {
            query = query.Where(i => i.FormId == formId);
        }

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

internal sealed class FakeCurrentUserContext : ICurrentUserContext
{
    public UserProfile GetCurrentUser() =>
        new()
        {
            UserId = Guid.Parse("5f6f5928-b09b-4fe3-88d9-ee65968ea3e0"),
            IsAuthenticated = true,
            DisplayName = "Test User",
            Email = "test@example.com",
            Roles = [FormPermissionRole.Owner, FormPermissionRole.Manager]
        };
}

internal sealed class FakeAnonymousCurrentUserContext : ICurrentUserContext
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

internal sealed class FakeAdminCurrentUserContext : ICurrentUserContext
{
    public UserProfile GetCurrentUser() =>
        new()
        {
            UserId = Guid.Parse("f10b8799-9cd8-4a04-bf92-fd5c63be6c7d"),
            IsAuthenticated = true,
            DisplayName = "Admin User",
            Email = "admin@example.com",
            Roles = [FormPermissionRole.Admin]
        };
}

internal sealed class FakeFixedCurrentUserContext(UserProfile user) : ICurrentUserContext
{
    public UserProfile GetCurrentUser() => user;
}

internal sealed class FakeEmailNotifier : IEmailNotifier
{
    public List<Guid> CreatedInvitationIds { get; } = [];
    public List<Guid> AcceptedInvitationIds { get; } = [];
    public List<Guid> RevokedInvitationIds { get; } = [];

    public Task NotifyManagersAsync(FormAggregate form, EntryRecord entry, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task NotifyInvitationCreatedAsync(FormAggregate form, FormInvitation invitation, CancellationToken cancellationToken = default)
    {
        CreatedInvitationIds.Add(invitation.Id);
        return Task.CompletedTask;
    }

    public Task NotifyInvitationAcceptedAsync(FormAggregate form, FormInvitation invitation, CancellationToken cancellationToken = default)
    {
        AcceptedInvitationIds.Add(invitation.Id);
        return Task.CompletedTask;
    }

    public Task NotifyInvitationRevokedAsync(FormAggregate form, FormInvitation invitation, CancellationToken cancellationToken = default)
    {
        RevokedInvitationIds.Add(invitation.Id);
        return Task.CompletedTask;
    }
}

internal sealed class FakeFileStorage : IFileStorage
{
    public Task<StoredFile> SaveAsync(FileUploadRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new StoredFile
        {
            FileName = request.FileName,
            ContentType = request.ContentType,
            Length = request.Content.LongLength,
            RelativePath = request.FileName
        });
    }
}
