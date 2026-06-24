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
services.AddSingleton<IPdfExporter, FakePdfExporter>();
services.AddSingleton<IAntiAbuseGuard, FakeAntiAbuseGuard>();
services.AddSingleton<IOperationalTelemetry, FakeOperationalTelemetry>();

await using var provider = services.BuildServiceProvider();
var app = provider.GetRequiredService<FormsApplicationService>();
var serializer = provider.GetRequiredService<IFormDefinitionSerializer>();
var conditionEvaluator = provider.GetRequiredService<IConditionEvaluator>();
var repository = (FakeRepository)provider.GetRequiredService<IFormsRepository>();

await repository.SeedAsync();

// Run isolated unit tests for modules with least coverage
await IsolatedUnitTests.RunAllAsync();

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
            Layout = new FormSectionLayoutDefinition { Columns = 3, Group = "primary" },
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
                    Layout = new FormFieldLayoutDefinition { WidthHint = "TwoThirds", Group = "notes" },
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

Assert(FormLayoutResolver.ResolveSectionColumns(visibleSections[0]) == 3, "Section layout columns resolve from metadata.");
Assert(FormLayoutResolver.ResolveFieldWidthHint(visibleSections[0].Fields.First(f => f.Id == "managerNotes")) == "TwoThirds", "Field width hint resolves from metadata.");
Assert(FormLayoutResolver.ResolveSectionColumns(new FormSectionDefinition { Layout = new FormSectionLayoutDefinition { Columns = 12 } }) == 4, "Section layout columns use safe upper fallback.");
Assert(FormLayoutResolver.ResolveSectionColumns(new FormSectionDefinition { Layout = new FormSectionLayoutDefinition { Columns = 0 } }) == 1, "Section layout columns use safe lower fallback.");
Assert(FormLayoutResolver.ResolveFieldWidthHint(new FormFieldDefinition { Layout = new FormFieldLayoutDefinition { WidthHint = "INVALID" } }) == "Auto", "Field width hint uses safe fallback for unknown values.");

var localizedDefinition = DemoFormFactory.CreateDefaultDefinition();
localizedDefinition.DefaultCulture = "en-US";
localizedDefinition.Title = "Travel request";
localizedDefinition.Description = "Default description";
localizedDefinition.LocalizedTitles["fr-FR"] = "Demande de voyage";
localizedDefinition.LocalizedDescriptions["fr"] = "Description francaise";
localizedDefinition.Sections[0].LocalizedTitles["fr-FR"] = "Details de la demande";
localizedDefinition.Sections[0].LocalizedDescriptions["fr-FR"] = "Informations principales";
localizedDefinition.Sections[0].Fields[0].LocalizedLabels["fr-FR"] = "Nom de l'employe";
localizedDefinition.Sections[0].Fields[0].LocalizedPlaceholders["fr"] = "Entrez le nom";
localizedDefinition.Sections[0].Fields[0].LocalizedHelpTexts["fr-FR"] = "Aide locale";
localizedDefinition.Sections[0].Fields[0].LocalizedValidationHints["fr-FR"] = "Format attendu";
localizedDefinition.Sections[0].Fields[3].Options[0].LocalizedLabels["fr-FR"] = "Oui";

Assert(FormLocalizationResolver.ResolveText(localizedDefinition.Title, localizedDefinition.LocalizedTitles, "fr-FR", localizedDefinition.DefaultCulture) == "Demande de voyage", "Localization resolver picks exact culture value.");
Assert(FormLocalizationResolver.ResolveText(localizedDefinition.Description, localizedDefinition.LocalizedDescriptions, "fr-CA", localizedDefinition.DefaultCulture) == "Description francaise", "Localization resolver falls back to language code.");
Assert(FormLocalizationResolver.ResolveText("fallback", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), "fr-FR", localizedDefinition.DefaultCulture) == "fallback", "Localization resolver returns base text when no localization exists.");

var localizedForm = await app.SaveDraftAsync(new SaveDraftRequest
{
    Name = "Localized form",
    Description = "Localization draft",
    Slug = "localized-form",
    AccessMode = FormAccessMode.Public,
    Definition = localizedDefinition
});

var localizedBuilderState = await app.GetBuilderStateAsync(localizedForm.Id);
Assert(localizedBuilderState.Form.DraftDefinition.DefaultCulture == "en-US", "Default culture persists in draft definition.");
Assert(localizedBuilderState.Form.DraftDefinition.LocalizedTitles.TryGetValue("fr-FR", out var frTitle) && frTitle == "Demande de voyage", "Form localized title persists in draft definition.");
Assert(localizedBuilderState.Form.DraftDefinition.Sections[0].LocalizedTitles.TryGetValue("fr-FR", out var frSectionTitle) && frSectionTitle == "Details de la demande", "Section localized title persists in draft definition.");
Assert(localizedBuilderState.Form.DraftDefinition.Sections[0].Fields[0].LocalizedLabels.TryGetValue("fr-FR", out var frLabel) && frLabel == "Nom de l'employe", "Field localized label persists in draft definition.");
Assert(localizedBuilderState.Form.DraftDefinition.Sections[0].Fields[3].Options[0].LocalizedLabels.TryGetValue("fr-FR", out var frOptionLabel) && frOptionLabel == "Oui", "Option localized label persists in draft definition.");

var localizedVersion = await app.PublishAsync(localizedForm.Id);
var localizedSnapshot = serializer.Deserialize(localizedVersion.DefinitionJson);
Assert(localizedSnapshot.LocalizedTitles.TryGetValue("fr-FR", out var snapshotTitle) && snapshotTitle == "Demande de voyage", "Published snapshot preserves form localized title.");
Assert(localizedSnapshot.Sections[0].Fields[0].LocalizedPlaceholders.TryGetValue("fr", out var snapshotPlaceholder) && snapshotPlaceholder == "Entrez le nom", "Published snapshot preserves localized field placeholder.");

// Test: Invalid default culture is rejected
var invalidCultureDefinition = DemoFormFactory.CreateDefaultDefinition();
invalidCultureDefinition.DefaultCulture = "invalid-culture";
var invalidDefaultCultureBlocked = false;
string? invalidCultureError = null;
try
{
    await app.SaveDraftAsync(new SaveDraftRequest
    {
        Name = "Invalid culture form",
        Description = "Should fail save",
        Slug = "invalid-culture-form",
        AccessMode = FormAccessMode.Public,
        Definition = invalidCultureDefinition
    });
}
catch (InvalidOperationException ex)
{
    invalidDefaultCultureBlocked = true;
    invalidCultureError = ex.Message;
}
Assert(invalidDefaultCultureBlocked, "Save draft blocks invalid default culture.");
Assert(invalidCultureError?.Contains("not a valid culture") == true, $"Invalid culture error message is descriptive: {invalidCultureError}");

// Test: Invalid culture in localized titles is also rejected
var invalidLocalizedCulture = DemoFormFactory.CreateDefaultDefinition();
invalidLocalizedCulture.LocalizedTitles["xyz-XYZ"] = "Invalid culture form title";
var invalidLocalizedBlocked = false;
try
{
    await app.SaveDraftAsync(new SaveDraftRequest
    {
        Name = "Invalid localized culture form",
        Description = "Should fail save",
        Slug = "invalid-localized-culture-form",
        AccessMode = FormAccessMode.Public,
        Definition = invalidLocalizedCulture
    });
}
catch (InvalidOperationException ex)
{
    invalidLocalizedBlocked = true;
    invalidCultureError = ex.Message;
}
Assert(invalidLocalizedBlocked, "Save draft blocks invalid culture in localized titles.");
Assert(invalidCultureError?.Contains("not a valid culture") == true, $"Invalid localized culture error is descriptive: {invalidCultureError}");

var brandingDefinition = DemoFormFactory.CreateDefaultDefinition();
brandingDefinition.Branding.LogoUrl = "https://cdn.example.com/logo.png";
brandingDefinition.Branding.LogoFileRef = "assets/branding/logo.png";
brandingDefinition.Branding.HeroImageUrl = "https://cdn.example.com/hero.jpg";
brandingDefinition.Branding.HeroImageFileRef = "assets/branding/hero.jpg";
brandingDefinition.Branding.AccentColor = "#1a2b3c";
brandingDefinition.Branding.SurfaceColor = "#fefefe";
brandingDefinition.Branding.TextColor = "#101820";
brandingDefinition.Branding.ButtonRadius = "12px";
brandingDefinition.Branding.HeroText = "  Branded hero text  ";

var brandingForm = await app.SaveDraftAsync(new SaveDraftRequest
{
    Name = "Branding form",
    Description = "Branding draft",
    Slug = "branding-form",
    AccessMode = FormAccessMode.Public,
    Definition = brandingDefinition
});

var brandingState = await app.GetBuilderStateAsync(brandingForm.Id);
var persistedBranding = brandingState.Form.DraftDefinition.Branding;
Assert(persistedBranding.AccentColor == "#1a2b3c", "Branding accent color persists and is normalized.");
Assert(persistedBranding.SurfaceColor == "#fefefe", "Branding surface color persists and is normalized.");
Assert(persistedBranding.TextColor == "#101820", "Branding text color persists and is normalized.");
Assert(persistedBranding.ButtonRadius == "12px", "Branding button radius persists.");
Assert(persistedBranding.HeroText == "Branded hero text", "Branding hero text is trimmed on save.");

var brandingVersion = await app.PublishAsync(brandingForm.Id);
var brandingSnapshot = serializer.Deserialize(brandingVersion.DefinitionJson);
Assert(brandingSnapshot.Branding.HeroImageUrl == "https://cdn.example.com/hero.jpg", "Branding hero image URL is preserved in published snapshot.");
Assert(brandingSnapshot.Branding.LogoFileRef == "assets/branding/logo.png", "Branding logo file reference is preserved in published snapshot.");

var invalidBrandingDefinition = DemoFormFactory.CreateDefaultDefinition();
invalidBrandingDefinition.Branding.AccentColor = "red";
var invalidBrandingBlocked = false;
try
{
    await app.SaveDraftAsync(new SaveDraftRequest
    {
        Name = "Invalid branding form",
        Description = "Should fail save",
        Slug = "invalid-branding-form",
        AccessMode = FormAccessMode.Public,
        Definition = invalidBrandingDefinition
    });
}
catch (InvalidOperationException)
{
    invalidBrandingBlocked = true;
}
Assert(invalidBrandingBlocked, "Save draft blocks invalid branding color tokens.");

var invalidAssetDefinition = DemoFormFactory.CreateDefaultDefinition();
invalidAssetDefinition.Branding.LogoUrl = "javascript:alert(1)";
var invalidAssetBlocked = false;
try
{
    await app.SaveDraftAsync(new SaveDraftRequest
    {
        Name = "Invalid asset form",
        Description = "Should fail save",
        Slug = "invalid-asset-form",
        AccessMode = FormAccessMode.Public,
        Definition = invalidAssetDefinition
    });
}
catch (InvalidOperationException)
{
    invalidAssetBlocked = true;
}
Assert(invalidAssetBlocked, "Save draft blocks invalid branding asset URLs.");

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
consistencyDefinition.Sections[0].VisibilityRules = new VisibilityConditionDefinition
{
    Join = VisibilityJoinOperator.And,
    Rules =
    [
        new VisibilityRuleDefinition { FieldId = "employeeName", Operator = VisibilityRuleOperator.Equals, Value = "Jordan" },
        new VisibilityRuleDefinition { FieldId = "destination", Operator = VisibilityRuleOperator.Contains, Value = "sea" }
    ]
};
consistencyDefinition.Sections[0].Fields.Reverse();
var consistentField = consistencyDefinition.Sections[0].Fields[0];
consistentField.Placeholder = "Type notes";
consistentField.HelpText = "Include budget context.";
consistentField.RegexPattern = "^[A-Za-z0-9 ,.?!-]+$";
consistentField.DefaultValue = "Default note";
consistentField.ValidationHint = "Use letters, numbers, punctuation.";
consistentField.VisibilityCondition = "needsHotel=yes";
consistentField.VisibilityRules = new VisibilityConditionDefinition
{
    Join = VisibilityJoinOperator.Or,
    Rules =
    [
        new VisibilityRuleDefinition { FieldId = "needsHotel", Operator = VisibilityRuleOperator.Equals, Value = "yes" },
        new VisibilityRuleDefinition { FieldId = "hotelNotes", Operator = VisibilityRuleOperator.Empty }
    ]
};
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
Assert(persistedSection.VisibilityRules is not null && persistedSection.VisibilityRules.Rules.Count == 2, "Section structured visibility rules persist with draft save.");
Assert(persistedField.Placeholder == "Type notes", "Field placeholder persists with draft save.");
Assert(persistedField.HelpText == "Include budget context.", "Field help text persists with draft save.");
Assert(persistedField.RegexPattern == "^[A-Za-z0-9 ,.?!-]+$", "Field regex pattern persists with draft save.");
Assert(persistedField.DefaultValue == "Default note", "Field default value persists with draft save.");
Assert(persistedField.ValidationHint == "Use letters, numbers, punctuation.", "Field validation hint persists with draft save.");
Assert(persistedField.VisibilityCondition == "needsHotel=yes", "Field visibility condition persists with draft save.");
Assert(persistedField.VisibilityRules is not null && persistedField.VisibilityRules.Join == VisibilityJoinOperator.Or && persistedField.VisibilityRules.Rules.Count == 2, "Field structured visibility rules persist with draft save.");
Assert(persistedField.Options.Count == 2 && persistedField.Options[0].Value == "alpha" && persistedField.Options[1].Value == "beta", "Field options persist in order with draft save.");

var consistencyVersion = await app.PublishAsync(consistencyForm.Id);
var snapshotDefinition = serializer.Deserialize(consistencyVersion.DefinitionJson);
var snapshotSectionRules = snapshotDefinition.Sections[0].VisibilityRules;
var snapshotFieldRules = snapshotDefinition.Sections[0].Fields[0].VisibilityRules;
Assert(snapshotSectionRules is not null && snapshotSectionRules.Rules.Count == 2, "Published snapshot preserves section structured visibility rules.");
Assert(snapshotFieldRules is not null && snapshotFieldRules.Rules.Count == 2, "Published snapshot preserves field structured visibility rules.");

var quoteStored = await app.StoreFileAsync(new FileUploadRequest
{
    FileName = "quote.pdf",
    ContentType = "application/pdf",
    Content = "quote-pdf"u8.ToArray()
});
var receiptStored = await app.StoreFileAsync(new FileUploadRequest
{
    FileName = "receipt.pdf",
    ContentType = "application/pdf",
    Content = "receipt-pdf"u8.ToArray()
});

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
    ],
    Files =
    [
        new SubmittedFileInput
        {
            FieldId = "receiptUpload",
            File = quoteStored
        },
        new SubmittedFileInput
        {
            FieldId = "receiptUpload",
            File = receiptStored
        }
    ]
});

Assert(entry.Status == EntryStatus.NeedsApproval, "Submission with approver enters approval state.");
Assert(entry.Revisions.Count == 1, "Initial submission creates revision 1.");
Assert(entry.Files.Count == 2, "Submission stores multiple files for same field.");
Assert(entry.Files.All(file => file.RevisionNumber == 1), "Initial submission stores file revision linkage metadata.");
Assert(entry.Files.All(file => file.UploadedByUserId != Guid.Empty), "Initial submission stores file uploader user id metadata.");
Assert(entry.Files.All(file => !string.IsNullOrWhiteSpace(file.UploadedByEmail)), "Initial submission stores file uploader email metadata.");
Assert(entry.Files.All(file => file.Sha256.Length == 64), "Initial submission stores file hash metadata.");
Assert(entry.Answers.TryGetValue("receiptUpload", out var uploadedFilesAnswer) && uploadedFilesAnswer == "quote.pdf, receipt.pdf", "Submission answer stores joined file names for multi-file field.");
Assert(primaryNotifier.AssignedNotificationKeys.Contains($"assigned:{entry.Id}:{entry.ApprovalSteps[0].Id}"), "Submission triggers approver assignment notification with idempotency key.");
var fakeTelemetry = (FakeOperationalTelemetry)provider.GetRequiredService<IOperationalTelemetry>();
Assert(fakeTelemetry.UploadSuccessCount > 0, "Telemetry mock captures upload success events in core flow.");

var revised = await app.ReviseEntryAsync(entry.Id, new Dictionary<string, string?>
{
    ["employeeName"] = "Jordan",
    ["destination"] = "Seattle"
});
Assert(revised.Revisions.Count == 2, "Revision appends history instead of overwrite.");

var overwriteDefinition = DemoFormFactory.CreateDefaultDefinition();
var overwriteForm = await app.SaveDraftAsync(new SaveDraftRequest
{
    Name = "Overwrite test form",
    Description = "Overwrite policy",
    Slug = "overwrite-test",
    Definition = overwriteDefinition,
    AccessMode = FormAccessMode.Public,
    EditMode = SubmissionEditMode.OverwriteLatest
});
await app.PublishAsync(overwriteForm.Id);

var overwriteEntry = await app.SubmitEntryAsync(overwriteForm.Id, new SubmitEntryRequest
{
    Answers = new Dictionary<string, string?>
    {
        ["employeeName"] = "Morgan",
        ["destination"] = "Austin"
    }
});

var overwriteRevised = await app.ReviseEntryAsync(overwriteEntry.Id, new Dictionary<string, string?>
{
    ["employeeName"] = "Morgan",
    ["destination"] = "Chicago"
});

Assert(overwriteRevised.Revisions.Count == 1, "Overwrite mode keeps a single latest revision.");
Assert(overwriteRevised.Answers.TryGetValue("destination", out var overwriteDestination) && overwriteDestination == "Chicago", "Overwrite mode replaces current answers with latest edit.");

var draftSaved = await app.SaveDraftSubmissionAsync(form.Id, new SaveDraftSubmissionRequest
{
    Answers = new Dictionary<string, string?>
    {
        ["employeeName"] = "Jordan",
        ["destination"] = "Boston"
    },
    Approvers =
    [
        new ApproverInput { Name = "Draft Approver", Email = "draft-approver@example.com" }
    ],
    Files =
    [
        new SubmittedFileInput
        {
            FieldId = "receiptUpload",
            File = quoteStored
        }
    ]
});
Assert(draftSaved.Status == EntryStatus.Draft, "Save draft creates draft entry state.");

var resumedDraft = await app.GetDraftSubmissionAsync(form.Id);
Assert(resumedDraft is not null && resumedDraft.Id == draftSaved.Id, "Draft resume returns current user's latest draft for form.");

var draftUpdated = await app.SaveDraftSubmissionAsync(form.Id, new SaveDraftSubmissionRequest
{
    DraftEntryId = draftSaved.Id,
    Answers = new Dictionary<string, string?>
    {
        ["employeeName"] = "Jordan",
        ["destination"] = "Miami"
    },
    Approvers =
    [
        new ApproverInput { Name = "Draft Approver", Email = "draft-approver@example.com" }
    ],
    Files =
    [
        new SubmittedFileInput
        {
            FieldId = "receiptUpload",
            File = receiptStored
        }
    ]
});
Assert(draftUpdated.Id == draftSaved.Id && draftUpdated.Revisions.Count >= 2, "Save draft updates existing draft and appends revision history.");
Assert(draftUpdated.Files.All(file => file.RevisionNumber == draftUpdated.Revisions.Count), "Draft file metadata records revision linkage.");
Assert(draftUpdated.Files.All(file => file.UploadedByUserId != Guid.Empty && !string.IsNullOrWhiteSpace(file.UploadedByEmail)), "Draft file metadata stores uploader identity.");
Assert(draftUpdated.Files.All(file => !string.IsNullOrWhiteSpace(file.Sha256)), "Draft file metadata stores file hash.");

var finalizedFromDraft = await app.SubmitEntryAsync(form.Id, new SubmitEntryRequest
{
    DraftEntryId = draftSaved.Id,
    Answers = new Dictionary<string, string?>
    {
        ["employeeName"] = "Jordan",
        ["destination"] = "Miami"
    },
    Approvers =
    [
        new ApproverInput { Name = "Draft Approver", Email = "draft-approver@example.com" }
    ]
});
Assert(finalizedFromDraft.Status == EntryStatus.NeedsApproval, "Final submit can transition a draft to submitted workflow.");
Assert(finalizedFromDraft.Files.All(file => file.RevisionNumber == finalizedFromDraft.Revisions.Count), "Submitted file metadata keeps revision linkage.");

var staleDraftStoredFile = await app.StoreFileAsync(new FileUploadRequest
{
    FileName = "stale-draft.txt",
    ContentType = "text/plain",
    Content = "stale"u8.ToArray()
});
var staleDraft = await app.SaveDraftSubmissionAsync(form.Id, new SaveDraftSubmissionRequest
{
    Answers = new Dictionary<string, string?>
    {
        ["employeeName"] = "Jordan",
        ["destination"] = "Archive"
    },
    Files =
    [
        new SubmittedFileInput
        {
            FieldId = "receiptUpload",
            File = staleDraftStoredFile
        }
    ]
});
staleDraft.SubmittedUtc = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(15));
await repository.SaveEntryAsync(staleDraft);

var cleanupResult = await app.CleanupStaleDraftFilesAsync(TimeSpan.FromDays(7));
Assert(cleanupResult.DeletedDraftEntries >= 1, "Stale draft cleanup removes old draft entries.");
Assert(cleanupResult.DeletedFiles >= 1, "Stale draft cleanup removes orphaned files.");

var exportedPdf = await app.ExportEntryPdfAsync(entry.Id);
Assert(exportedPdf.ContentType == "application/pdf", "Entry PDF export returns PDF content type.");
Assert(exportedPdf.Content.Length > 0, "Entry PDF export returns bytes.");
var exportedPdfText = System.Text.Encoding.UTF8.GetString(exportedPdf.Content);
Assert(exportedPdfText.Contains("ANSWERS", StringComparison.Ordinal), "Entry PDF export includes answer section.");
Assert(exportedPdfText.Contains("APPROVAL AUDIT", StringComparison.Ordinal), "Entry PDF export includes approval audit section.");

var approved = await app.ApproveStepAsync(entry.Id, revised.ApprovalSteps[0].Id, "Lead");
Assert(approved.Status == EntryStatus.Approved, "Final approval marks entry approved.");
Assert(primaryNotifier.ApprovedEntryIds.Contains(entry.Id), "Approved entry notification is sent.");

var openedFile = await app.OpenEntryFileAsync(entry.Id, entry.Files[0].Id);
using (openedFile.Content)
using (var openedReader = new StreamReader(openedFile.Content))
{
    var openedContent = await openedReader.ReadToEndAsync();
    Assert(!string.IsNullOrWhiteSpace(openedContent), "Authorized file download returns stored content.");
}

var workflowForm = await app.SaveDraftAsync(new SaveDraftRequest
{
    Name = "Workflow form",
    Description = "Reject/resubmit",
    Slug = "workflow-form",
    AccessMode = FormAccessMode.Public,
    Definition = DemoFormFactory.CreateDefaultDefinition()
});
await app.PublishAsync(workflowForm.Id);

var workflowEntry = await app.SubmitEntryAsync(workflowForm.Id, new SubmitEntryRequest
{
    Answers = new Dictionary<string, string?>
    {
        ["employeeName"] = "Case",
        ["destination"] = "Berlin"
    },
    Approvers =
    [
        new ApproverInput { Name = "Step1", Email = "step1@example.com" },
        new ApproverInput { Name = "Step2", Email = "step2@example.com" }
    ]
});

var outOfOrderBlocked = false;
try
{
    await app.ApproveStepAsync(workflowEntry.Id, workflowEntry.ApprovalSteps.OrderByDescending(s => s.Order).First().Id, "Late");
}
catch (InvalidOperationException)
{
    outOfOrderBlocked = true;
}
Assert(outOfOrderBlocked, "Approval workflow blocks out-of-order step transitions.");

var rejected = await app.RejectStepAsync(workflowEntry.Id, workflowEntry.ApprovalSteps.OrderBy(s => s.Order).First().Id, "Missing policy details");
Assert(rejected.Status == EntryStatus.Rejected, "Reject step moves entry to rejected status.");
Assert(primaryNotifier.RejectedEntryIds.Contains(workflowEntry.Id), "Rejected entry notification is sent.");
Assert(rejected.ApprovalSteps.First(s => s.Order == 1).Status == ApprovalStepStatus.Rejected, "Rejected step status is persisted.");
Assert(rejected.ApprovalAuditTrail.Any(a => a.Action == ApprovalAuditAction.StepRejected && !string.IsNullOrWhiteSpace(a.Reason)), "Reject step appends audit event with reason.");

var postRejectApproveBlocked = false;
try
{
    await app.ApproveStepAsync(rejected.Id, rejected.ApprovalSteps.OrderBy(s => s.Order).Last().Id, "Should fail");
}
catch (InvalidOperationException)
{
    postRejectApproveBlocked = true;
}
Assert(postRejectApproveBlocked, "Approval is blocked after entry reaches rejected terminal state.");

var resubmitted = await app.ResubmitEntryAsync(rejected.Id, new ResubmitEntryRequest
{
    Answers = new Dictionary<string, string?>
    {
        ["employeeName"] = "Case",
        ["destination"] = "Rome"
    },
    Approvers =
    [
        new ApproverInput { Name = "Resubmitted Step", Email = "step1@example.com" }
    ]
});
Assert(resubmitted.Status == EntryStatus.NeedsApproval, "Resubmission transitions entry back to review state.");
Assert(resubmitted.ApprovalSteps.Count == 1 && resubmitted.ApprovalSteps[0].Status == ApprovalStepStatus.Pending, "Resubmission resets approval steps to new pending workflow.");
Assert(resubmitted.ApprovalAuditTrail.Any(a => a.Action == ApprovalAuditAction.Resubmitted), "Resubmission appends audit event.");

var approvedAfterResubmission = await app.ApproveStepAsync(resubmitted.Id, resubmitted.ApprovalSteps[0].Id, "Manager Sign");
Assert(approvedAfterResubmission.ApprovalAuditTrail.Any(a => a.Action == ApprovalAuditAction.StepApproved && !string.IsNullOrWhiteSpace(a.Signature)), "Approve step appends audit event with signature.");

var auditTrail = approvedAfterResubmission.ApprovalAuditTrail.OrderBy(a => a.OccurredUtc).ToList();
Assert(auditTrail.Count >= 3, "Approval audit trail keeps append-only event history.");
Assert(auditTrail[0].OccurredUtc <= auditTrail[^1].OccurredUtc, "Approval audit trail events are sortable by timestamp.");

var dashboard = await app.GetDashboardAsync();
Assert(dashboard.CurrentUser.IsAuthenticated, "Dashboard includes authenticated current user profile.");

var pagedCoreEntries = await app.QueryEntriesAsync(new EntryQueryOptions
{
    FormId = form.Id,
    Limit = 2
});
Assert(pagedCoreEntries.Count <= 2, "Core query options apply limit for large datasets.");

var largeForm = await app.SaveDraftAsync(new SaveDraftRequest
{
    Name = "Large form",
    Description = "Performance regression checks.",
    Slug = "large-form-core",
    Definition = BuildLargeDefinition(12, 25),
    AccessMode = FormAccessMode.Public
});
var largeVersion = await app.PublishAsync(largeForm.Id);
var largePublished = await app.GetPublishedFormAsync(largeForm.Publication.Slug);
Assert(largePublished is not null && largePublished.Definition.Sections.Count == 12, "Large-form metadata remains loadable under baseline sizing.");
Assert(!string.IsNullOrWhiteSpace(largeVersion.DefinitionJson), "Large-form publish snapshot is generated for regression checks.");

var fallback = FormLocalizationResolver.ResolveText(
    "base-text",
    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    "ar-SA",
    "en-US");
Assert(fallback == "base-text", "Localization fallback returns base text when translation is unavailable.");

var detail = await app.GetEntryDetailAsync(entry.Id);
Assert(detail is not null && detail.CanView, "Authorized user can view entry detail.");

// negative authorization checks
services = new ServiceCollection();
services.AddBlazorWebFormsCore();
services.AddSingleton<IFormsRepository>(repository);
services.AddSingleton<ICurrentUserContext, FakeAnonymousCurrentUserContext>();
services.AddSingleton<IEmailNotifier>(new FakeEmailNotifier());
services.AddSingleton<IFileStorage, FakeFileStorage>();
services.AddSingleton<IPdfExporter, FakePdfExporter>();
services.AddSingleton<IAntiAbuseGuard, FakeAntiAbuseGuard>();
services.AddSingleton<IOperationalTelemetry, FakeOperationalTelemetry>();

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

var anonymousFileAccessBlocked = false;
try
{
    await anonymousApp.OpenEntryFileAsync(entry.Id, entry.Files[0].Id);
}
catch (InvalidOperationException)
{
    anonymousFileAccessBlocked = true;
}
Assert(anonymousFileAccessBlocked, "Anonymous user cannot download entry files.");

// admin bypass checks
services = new ServiceCollection();
services.AddBlazorWebFormsCore();
services.AddSingleton<IFormsRepository>(repository);
services.AddSingleton<ICurrentUserContext, FakeAdminCurrentUserContext>();
services.AddSingleton<IEmailNotifier>(new FakeEmailNotifier());
services.AddSingleton<IFileStorage, FakeFileStorage>();
services.AddSingleton<IPdfExporter, FakePdfExporter>();
services.AddSingleton<IAntiAbuseGuard, FakeAntiAbuseGuard>();
services.AddSingleton<IOperationalTelemetry, FakeOperationalTelemetry>();

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
services.AddSingleton<IPdfExporter, FakePdfExporter>();
services.AddSingleton<IAntiAbuseGuard, FakeAntiAbuseGuard>();
services.AddSingleton<IOperationalTelemetry, FakeOperationalTelemetry>();

await using var approverProvider = services.BuildServiceProvider();
var approverApp = approverProvider.GetRequiredService<FormsApplicationService>();

var approverFlowEntry = await app.SubmitEntryAsync(form.Id, new SubmitEntryRequest
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

var approvedByApprover = await approverApp.ApproveStepAsync(approverFlowEntry.Id, approverFlowEntry.ApprovalSteps[0].Id, "Lead");
Assert(approvedByApprover.Status == EntryStatus.Approved, "Approver can approve assigned step.");
var approverCanViewDetail = await approverApp.GetEntryDetailAsync(approverFlowEntry.Id);
Assert(approverCanViewDetail is not null && approverCanViewDetail.CanView, "Assigned approver can view entry detail and file metadata.");

services = new ServiceCollection();
var step1User = new UserProfile
{
    UserId = Guid.NewGuid(),
    IsAuthenticated = true,
    DisplayName = "Step1 User",
    Email = "step1@example.com",
    Roles = [FormPermissionRole.Approver]
};
services.AddBlazorWebFormsCore();
services.AddSingleton<IFormsRepository>(repository);
services.AddSingleton<ICurrentUserContext>(new FakeFixedCurrentUserContext(step1User));
services.AddSingleton<IEmailNotifier>(new FakeEmailNotifier());
services.AddSingleton<IFileStorage, FakeFileStorage>();
services.AddSingleton<IPdfExporter, FakePdfExporter>();
services.AddSingleton<IAntiAbuseGuard, FakeAntiAbuseGuard>();
services.AddSingleton<IOperationalTelemetry, FakeOperationalTelemetry>();

await using var step1Provider = services.BuildServiceProvider();
var step1App = step1Provider.GetRequiredService<FormsApplicationService>();

var approverRejectEntry = await app.SubmitEntryAsync(workflowForm.Id, new SubmitEntryRequest
{
    Answers = new Dictionary<string, string?>
    {
        ["employeeName"] = "Case",
        ["destination"] = "Madrid"
    },
    Approvers =
    [
        new ApproverInput { Name = "Step1", Email = "step1@example.com" }
    ]
});

var rejectedByApprover = await step1App.RejectStepAsync(approverRejectEntry.Id, approverRejectEntry.ApprovalSteps.OrderBy(s => s.Order).First().Id, "Need correction");
Assert(rejectedByApprover.Status == EntryStatus.Rejected, "Assigned approver can reject current pending step.");

var reminderEntry = await app.SubmitEntryAsync(workflowForm.Id, new SubmitEntryRequest
{
    Answers = new Dictionary<string, string?>
    {
        ["employeeName"] = "Reminder",
        ["destination"] = "Lisbon"
    },
    Approvers =
    [
        new ApproverInput { Name = "Reminder Step", Email = "reminder@example.com" }
    ]
});
Assert(reminderEntry.Status == EntryStatus.NeedsApproval, "Reminder fixture entry is in review state.");

var reminderCountFirst = await app.SendApprovalRemindersAsync(workflowForm.Id);
Assert(reminderCountFirst > 0, "Reminder hook sends reminders for pending approvals.");

var reminderCountSecond = await app.SendApprovalRemindersAsync(workflowForm.Id);
Assert(reminderCountSecond > 0, "Reminder hook can be invoked repeatedly without throwing.");

var uniqueReminderKeys = primaryNotifier.ReminderNotificationKeys.Distinct(StringComparer.Ordinal).Count();
Assert(uniqueReminderKeys == primaryNotifier.ReminderNotificationKeys.Count, "Reminder notifier idempotency keys avoid duplicate sends in notifier implementation.");
Assert(fakeTelemetry.PdfSuccessCount > 0, "Telemetry mock captures PDF export success events in core flow.");

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
services.AddSingleton<IPdfExporter, FakePdfExporter>();
services.AddSingleton<IAntiAbuseGuard, FakeAntiAbuseGuard>();
services.AddSingleton<IOperationalTelemetry, FakeOperationalTelemetry>();

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
services.AddSingleton<IPdfExporter, FakePdfExporter>();
services.AddSingleton<IAntiAbuseGuard, FakeAntiAbuseGuard>();
services.AddSingleton<IOperationalTelemetry, FakeOperationalTelemetry>();

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

static FormDefinition BuildLargeDefinition(int sectionCount, int fieldsPerSection)
{
    var definition = new FormDefinition
    {
        Title = "Large regression form",
        Description = "Generated for performance checks.",
        DefaultCulture = "en-US"
    };

    for (var sectionIndex = 0; sectionIndex < sectionCount; sectionIndex++)
    {
        var section = new FormSectionDefinition
        {
            Id = $"sec-{sectionIndex + 1}",
            Title = $"Section {sectionIndex + 1}",
            Description = "Generated section"
        };

        for (var fieldIndex = 0; fieldIndex < fieldsPerSection; fieldIndex++)
        {
            section.Fields.Add(new FormFieldDefinition
            {
                Id = $"f-{sectionIndex + 1}-{fieldIndex + 1}",
                Label = $"Field {sectionIndex + 1}-{fieldIndex + 1}",
                Kind = FormFieldKind.Text,
                Placeholder = "Value"
            });
        }

        definition.Sections.Add(section);
    }

    return definition;
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

        query = query.OrderByDescending(x => x.SubmittedUtc);

        if (options.Offset > 0)
        {
            query = query.Skip(options.Offset);
        }

        if (options.Limit > 0)
        {
            query = query.Take(options.Limit);
        }

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
        var staleDraftIds = entries.Values
            .Where(e => e.Status == EntryStatus.Draft && e.SubmittedUtc < olderThanUtc)
            .Select(e => e.Id)
            .ToList();

        foreach (var staleId in staleDraftIds)
        {
            entries.Remove(staleId);
        }

        return Task.FromResult(staleDraftIds.Count);
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
    public List<string> AssignedNotificationKeys { get; } = [];
    public List<string> ReminderNotificationKeys { get; } = [];
    public List<Guid> ApprovedEntryIds { get; } = [];
    public List<Guid> RejectedEntryIds { get; } = [];
    private readonly HashSet<string> sentKeys = new(StringComparer.Ordinal);

    public Task NotifyManagersAsync(FormAggregate form, EntryRecord entry, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<string?> NotifyApproverAssignedAsync(FormAggregate form, EntryRecord entry, ApprovalStepRecord step, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (sentKeys.Add($"assigned:{idempotencyKey}"))
        {
            AssignedNotificationKeys.Add(idempotencyKey);
        }

        return Task.FromResult<string?>(null);
    }

    public Task<string?> NotifyApproverReminderAsync(FormAggregate form, EntryRecord entry, ApprovalStepRecord step, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (sentKeys.Add($"reminder:{idempotencyKey}"))
        {
            ReminderNotificationKeys.Add(idempotencyKey);
        }

        return Task.FromResult<string?>(null);
    }

    public Task NotifyEntryApprovedAsync(FormAggregate form, EntryRecord entry, CancellationToken cancellationToken = default)
    {
        ApprovedEntryIds.Add(entry.Id);
        return Task.CompletedTask;
    }

    public Task NotifyEntryRejectedAsync(FormAggregate form, EntryRecord entry, ApprovalStepRecord step, CancellationToken cancellationToken = default)
    {
        RejectedEntryIds.Add(entry.Id);
        return Task.CompletedTask;
    }

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
    private readonly Dictionary<string, byte[]> filesByRelativePath = new(StringComparer.OrdinalIgnoreCase);

    public Task<StoredFile> SaveAsync(FileUploadRequest request, CancellationToken cancellationToken = default)
    {
        var stored = new StoredFile
        {
            FileName = request.FileName,
            ContentType = request.ContentType,
            Length = request.Content.LongLength,
            RelativePath = request.FileName,
            Sha256 = ComputeSha256Hex(request.Content)
        };

        filesByRelativePath[stored.RelativePath] = request.Content;
        return Task.FromResult(stored);
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        if (!filesByRelativePath.TryGetValue(relativePath, out var content))
        {
            throw new FileNotFoundException("Requested fake file not found.", relativePath);
        }

        return Task.FromResult<Stream>(new MemoryStream(content, writable: false));
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        filesByRelativePath.Remove(relativePath);
        return Task.CompletedTask;
    }

    private static string ComputeSha256Hex(byte[] content)
    {
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant();
    }
}

internal sealed class FakePdfExporter : IPdfExporter
{
    public Task<byte[]> ExportEntryAsync(FormAggregate form, EntryRecord entry, CancellationToken cancellationToken = default)
    {
        var payload = $"PDF\nForm:{form.Name}\nEntry:{entry.Id}\nANSWERS\n{string.Join("\n", entry.Answers.Select(a => $"{a.Key}:{a.Value}"))}\nAPPROVAL AUDIT";
        return Task.FromResult(System.Text.Encoding.UTF8.GetBytes(payload));
    }
}

internal sealed class FakeAntiAbuseGuard : IAntiAbuseGuard
{
    public Task CheckUploadAllowedAsync(UserProfile user, FileUploadRequest request, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task CheckOutboundNotificationAllowedAsync(string channel, string recipient, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

internal sealed class FakeOperationalTelemetry : IOperationalTelemetry
{
    public int UploadSuccessCount { get; private set; }
    public int UploadFailureCount { get; private set; }
    public int PdfSuccessCount { get; private set; }
    public int PdfFailureCount { get; private set; }
    public int EmailSuccessCount { get; private set; }
    public int EmailFailureCount { get; private set; }
    public int FailureEvents { get; private set; }

    public void TrackUpload(string source, long bytes, bool success)
    {
        if (success)
        {
            UploadSuccessCount++;
        }
        else
        {
            UploadFailureCount++;
        }
    }

    public void TrackPdfExport(string source, long bytes, bool success)
    {
        if (success)
        {
            PdfSuccessCount++;
        }
        else
        {
            PdfFailureCount++;
        }
    }

    public void TrackEmailDelivery(string channel, bool success)
    {
        if (success)
        {
            EmailSuccessCount++;
        }
        else
        {
            EmailFailureCount++;
        }
    }

    public void TrackFailure(string area, string operation, string reason)
    {
        FailureEvents++;
    }
}
