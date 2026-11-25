using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;
using BlazorWebForms.Core.Services;
using Microsoft.Extensions.DependencyInjection;

internal static class HandoffRegressionTests
{
    public static async Task RunAsync()
    {
        var repo = new FakeRepository { DetachedEntries = true };
        var users = new MutableUserContext();
        var notifier = new FakeEmailNotifier();
        var services = new ServiceCollection();
        services.AddBlazorWebFormsCore();
        services.AddSingleton<IFormsRepository>(repo);
        services.AddSingleton<ICurrentUserContext>(users);
        services.AddSingleton<IEmailNotifier>(notifier);
        services.AddSingleton<IFileStorage, FakeFileStorage>();
        services.AddSingleton<IPdfExporter, FakePdfExporter>();
        services.AddSingleton<IAntiAbuseGuard, FakeAntiAbuseGuard>();
        services.AddSingleton<IOperationalTelemetry, FakeOperationalTelemetry>();
        await using var provider = services.BuildServiceProvider();
        var app = provider.GetRequiredService<FormsApplicationService>();
        var serializer = provider.GetRequiredService<IFormDefinitionSerializer>();
        var definition = new FormDefinition
        {
            Title = "Handoff",
            Metadata = { ["form.settings"] = "{\"templatedFormEnabled\":true,\"handoffSteps\":[{\"id\":\"one\",\"name\":\"First\"},{\"id\":\"two\",\"name\":\"Second\"},{\"id\":\"three\",\"name\":\"Third\"}]}" },
            Sections = [new FormSectionDefinition { Fields = [Field("first", "Initiator"), Field("middle", "Step:two"), Field("last", "Recipient"), Field("attachment", "Step:one"), Field("any", "Any")] }]
        };
        definition.Sections[0].Fields.Single(f => f.Id == "middle").Required = true;
        definition.Sections[0].Fields.Single(f => f.Id == "last").Required = true;
        var hidden = Field("hiddenRequired", "Step:two");
        hidden.Required = true;
        hidden.VisibilityCondition = "any=show";
        definition.Sections[0].Fields.Add(hidden);
        var requiredCheckbox = Field("checkbox", "Step:one");
        requiredCheckbox.Kind = FormFieldKind.Checkbox;
        requiredCheckbox.Required = true;
        definition.Sections[0].Fields.Add(requiredCheckbox);
        var readonlyRequired = Field("readonly", "Step:one");
        readonlyRequired.ReadOnly = true;
        readonlyRequired.Required = true;
        definition.Sections[0].Fields.Add(readonlyRequired);
        var information = Field("information", "Step:one");
        information.Kind = FormFieldKind.Custom;
        information.CustomKind = "information";
        information.Required = true;
        definition.Sections[0].Fields.Add(information);
        var calculated = Field("calculated", "Step:three");
        calculated.Kind = FormFieldKind.Calculated;
        calculated.FormulaExpression = "1+2";
        definition.Sections[0].Fields.Add(calculated);
        var form = new FormAggregate { Name = "Handoff", Publication = { AccessMode = FormAccessMode.Authenticated } };
        form.Versions.Add(new FormVersionRecord { VersionNumber = 1, DefinitionJson = serializer.Serialize(definition) });
        await repo.SaveFormAsync(form);
        users.Email = "first@example.com";
        await Throws(() => app.SaveDraftSubmissionAsync(form.Id, new() { Answers = new() { ["last"] = "prefill" } }), "Non-current prefill rejected");
        var file = new SubmittedFileInput { FieldId = "attachment", File = new StoredFile { FileName = "one.txt", RelativePath = "one.txt", Sha256 = "hash" } };
        var draft = await app.SaveDraftSubmissionAsync(form.Id, new()
        {
            Answers = new() { ["first"] = "original", ["__handoff.stepId"] = "three", ["__templated.recipientEmail"] = "first@example.com", ["__handoff.extra"] = "evil", ["calculated"] = "spoof" },
            Files = [file]
        });
        var id = draft.Id;
        Check(draft.Answers["__handoff.stepId"] == "one" && !draft.Answers.ContainsKey("__handoff.extra") && !draft.Answers.ContainsKey("__templated.recipientEmail") && draft.Answers["calculated"] == "3", "Spoofed metadata and calculated values ignored");
        await Throws(() => app.SubmitEntryAsync(form.Id, new() { DraftEntryId = id, Answers = new() { ["__handoff.stepId"] = "three" } }), "First step cannot submit");
        await Throws(() => app.ReviseEntryAsync(id, new() { ["__handoff.stepId"] = "three" }), "Draft revision bypass blocked");
        var firstTransfer = await app.TransferDraftSubmissionWithResultAsync(form.Id, new() { DraftEntryId = id, RecipientEmail = "second@example.com" });
        Check(firstTransfer.NotificationOutcome == HandoffNotificationOutcome.Sent && notifier.HandoffRecipients.SequenceEqual(["second@example.com"]), "Transfer reports successful notification");
        await Throws(() => app.SaveDraftSubmissionAsync(form.Id, new() { DraftEntryId = id }), "Former owner cannot save");
        Check(await app.GetDraftSubmissionAsync(form.Id, id) is null, "Former owner cannot retrieve by id");
        users.Email = "second@example.com";
        Check((await app.GetDraftSubmissionAsync(form.Id, id))?.Id == id, "Recipient can retrieve by id");
        await Throws(() => app.TransferDraftSubmissionAsync(form.Id, new() { DraftEntryId = id, RecipientEmail = "third@example.com" }), "Required current-step answer blocks transfer");
        await Throws(() => app.SaveDraftSubmissionAsync(form.Id, new() { DraftEntryId = id, Answers = new() { ["first"] = "tamper" } }), "Protected answer blocked");
        await Throws(() => app.SaveDraftSubmissionAsync(form.Id, new() { DraftEntryId = id, Files = [file] }), "Protected file blocked");
        await Throws(() => app.SaveDraftSubmissionAsync(form.Id, new() { DraftEntryId = id, Answers = new() { ["ATTACHMENT"] = null } }), "Case-insensitive protected file removal blocked");
        draft = await app.SaveDraftSubmissionAsync(form.Id, new() { DraftEntryId = id, Answers = new() { ["middle"] = "middle value", ["__handoff.stepId"] = "three", ["__templated.initiatorEmail"] = "evil@example.com" } });
        Check(draft.Answers["__handoff.stepId"] == "two" && draft.Answers["__templated.initiatorEmail"] == "first@example.com" && draft.Answers["first"] == "original" && draft.Files.Count == 1, "Stored metadata answers and files survive save");
        await Throws(() => app.SubmitEntryAsync(form.Id, new() { DraftEntryId = id }), "Middle step cannot submit");
        notifier.FailHandoff = true;
        var transferResult = await app.TransferDraftSubmissionWithResultAsync(form.Id, new() { DraftEntryId = id, RecipientEmail = "third@example.com" });
        Check(transferResult.NotificationOutcome == HandoffNotificationOutcome.Failed, "Notification failure is exposed separately from successful transfer");
        notifier.FailHandoff = false;
        Check((await repo.GetEntryAsync(id))!.SubmittedByEmail == "third@example.com", "Notification failure does not undo or report failure for persisted transfer");
        users.Email = "third@example.com";
        await Throws(() => app.TransferDraftSubmissionAsync(form.Id, new() { DraftEntryId = id, RecipientEmail = "fourth@example.com" }), "Final step cannot transfer");
        await Throws(() => app.SaveDraftSubmissionAsync(form.Id, new() { DraftEntryId = id, Answers = new() { ["middle"] = "tamper" } }), "Middle step locked for final actor");
        await Throws(() => app.SubmitEntryAsync(form.Id, new() { DraftEntryId = id }), "Required final answer blocks submit");
        var submitted = await app.SubmitEntryAsync(form.Id, new() { DraftEntryId = id, Answers = new() { ["last"] = "final", ["__handoff.stepId"] = "one" } });
        Check(submitted.Status == EntryStatus.Submitted && submitted.Answers["__handoff.stepId"] == "three" && submitted.Answers["middle"] == "middle value" && submitted.Files.Single().FileName == "one.txt", "Final submit preserves answers files and trusted step");
        await Throws(() => app.ReviseEntryAsync(id, new() { ["first"] = "tamper" }), "Submitted revision enforces step permissions");

        users.Email = "first@example.com";
        var removable = await app.SaveDraftSubmissionAsync(form.Id, new() { Answers = new() { ["first"] = "remove file" }, Files = [file] });
        removable = await app.SaveDraftSubmissionAsync(form.Id, new() { DraftEntryId = removable.Id, Answers = new() { ["first"] = "still present" } });
        Check(removable.Files.Count == 1, "Omitted file field preserves file");
        removable = await app.SaveDraftSubmissionAsync(form.Id, new() { DraftEntryId = removable.Id, Answers = new() { ["attachment"] = null } });
        Check(removable.Files.Count == 0 && removable.Answers["attachment"] is null, "Explicitly clearing editable file removes file metadata");

        var raceDraft = await app.SaveDraftSubmissionAsync(form.Id, new() { DraftEntryId = removable.Id, Answers = new() { ["first"] = "race" } });
        repo.WriteBarrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Race(Func<Task> action) { try { await action(); return true; } catch (InvalidOperationException) { return false; } }
        var races = await Task.WhenAll(
            Race(() => app.SaveDraftSubmissionAsync(form.Id, new() { DraftEntryId = raceDraft.Id, Answers = new() { ["first"] = "stale save" } })),
            Race(() => app.TransferDraftSubmissionAsync(form.Id, new() { DraftEntryId = raceDraft.Id, RecipientEmail = "race@example.com" })));
        repo.WriteBarrier = null;
        Check(races.Count(x => x) == 1, "Simultaneous save and transfer have exactly one winner");
        var persistedRace = await repo.GetEntryAsync(raceDraft.Id);
        Check(persistedRace is not null && (races[1] ? persistedRace.SubmittedByEmail == "race@example.com" && persistedRace.Answers["__handoff.stepId"] == "two" : persistedRace.SubmittedByEmail == "first@example.com" && persistedRace.Answers["first"] == "stale save"), "Stale loser cannot overwrite winning ownership or answers");
        users.Email = "third@example.com";
        var finalRace = new EntryRecord { FormId = form.Id, FormVersionId = form.Versions[0].Id, Status = EntryStatus.Draft, SubmittedByEmail = users.Email, Answers = new(StringComparer.OrdinalIgnoreCase) { ["__handoff.stepId"] = "three", ["__templated.initiatorEmail"] = "first@example.com", ["last"] = "ready" } };
        await repo.SaveEntryAsync(finalRace);
        repo.WriteBarrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finalRaces = await Task.WhenAll(
            Race(() => app.SaveDraftSubmissionAsync(form.Id, new() { DraftEntryId = finalRace.Id, Answers = new() { ["last"] = "edited" } })),
            Race(() => app.SubmitEntryAsync(form.Id, new() { DraftEntryId = finalRace.Id })));
        repo.WriteBarrier = null;
        Check(finalRaces.Count(x => x) == 1, "Simultaneous final save and submit have exactly one winner");
        var finalPersisted = (await repo.GetEntryAsync(finalRace.Id))!;
        Check(finalRaces[1] ? finalPersisted.Status == EntryStatus.Submitted && finalPersisted.Answers["last"] == "ready" : finalPersisted.Status == EntryStatus.Draft && finalPersisted.Answers["last"] == "edited", "Stale save cannot reopen submitted draft");
        users.Email = "first@example.com";
        var versionDraft = await app.SaveDraftSubmissionAsync(form.Id, new());
        form.Versions.Add(new FormVersionRecord { VersionNumber = 2, DefinitionJson = serializer.Serialize(definition) });
        await Throws(() => app.SaveDraftSubmissionAsync(form.Id, new() { DraftEntryId = versionDraft.Id }), "Version change blocks save");
        await Throws(() => app.TransferDraftSubmissionAsync(form.Id, new() { DraftEntryId = versionDraft.Id, RecipientEmail = "second@example.com" }), "Version change blocks transfer");
        await Throws(() => app.SubmitEntryAsync(form.Id, new() { DraftEntryId = versionDraft.Id }), "Version change blocks submit");

        var secondOwnedDraft = new EntryRecord { FormId = form.Id, FormVersionId = form.Versions[1].Id, Status = EntryStatus.Draft, SubmittedByEmail = users.Email, Answers = new() { ["__handoff.stepId"] = "one" } };
        await repo.SaveEntryAsync(secondOwnedDraft);
        Check((await app.GetDraftSubmissionAsync(form.Id, versionDraft.Id))?.Id == versionDraft.Id && (await app.GetDraftSubmissionAsync(form.Id, secondOwnedDraft.Id))?.Id == secondOwnedDraft.Id, "Multiple owned drafts can be selected by id");
        users.Email = "intruder@example.com";
        Check(await app.GetDraftSubmissionAsync(form.Id, secondOwnedDraft.Id) is null, "Another user cannot retrieve draft by id");
        await Throws(() => app.SaveDraftSubmissionAsync(form.Id, new() { DraftEntryId = secondOwnedDraft.Id }), "Another user cannot save draft by id");
        users.Email = "first@example.com";

        var bad = new FormDefinition { Metadata = { ["form.settings"] = "{\"templatedFormEnabled\":true,\"handoffSteps\":[{\"id\":\"a\",\"name\":\"A\"}]}" } };
        var badForm = new FormAggregate { Publication = { AccessMode = FormAccessMode.Authenticated } };
        badForm.Versions.Add(new FormVersionRecord { VersionNumber = 1, DefinitionJson = serializer.Serialize(bad) });
        await repo.SaveFormAsync(badForm);
        await Throws(() => app.SaveDraftSubmissionAsync(badForm.Id, new()), "Invalid step configuration rejected");

        var plainForm = new FormAggregate { Publication = { AccessMode = FormAccessMode.Authenticated } };
        plainForm.Versions.Add(new FormVersionRecord { VersionNumber = 1, DefinitionJson = serializer.Serialize(new FormDefinition()) });
        await repo.SaveFormAsync(plainForm);
        var plain = await app.SaveDraftSubmissionAsync(plainForm.Id, new() { Answers = new() { ["a"] = "one", ["b"] = "two" }, Files = [file] });
        plain = await app.SaveDraftSubmissionAsync(plainForm.Id, new() { DraftEntryId = plain.Id, Answers = new() { ["a"] = "changed" } });
        Check(!plain.Answers.ContainsKey("b") && plain.Files.Count == 0, "Non-workflow omitted answers and files retain replacement semantics");

        var legacy = new FormDefinition { Metadata = { ["form.settings"] = "{\"templatedFormEnabled\":true}" }, Sections = [new FormSectionDefinition { Fields = [Field("from", "Initiator"), Field("to", "Recipient")] }] };
        var legacyForm = new FormAggregate { Publication = { AccessMode = FormAccessMode.Authenticated } };
        legacyForm.Versions.Add(new FormVersionRecord { VersionNumber = 1, DefinitionJson = serializer.Serialize(legacy) });
        await repo.SaveFormAsync(legacyForm);
        var legacyDraft = await app.SaveDraftSubmissionAsync(legacyForm.Id, new() { Answers = new() { ["from"] = "first" } });
        await Throws(() => app.SubmitEntryAsync(legacyForm.Id, new() { DraftEntryId = legacyDraft.Id, Answers = new() { ["__templated.recipientEmail"] = "first@example.com" } }), "Legacy recipient metadata cannot be spoofed");
        await app.TransferDraftSubmissionAsync(legacyForm.Id, new() { DraftEntryId = legacyDraft.Id, RecipientEmail = "second@example.com" });
        users.Email = "second@example.com";
        var legacyVersion = new FormVersionRecord { VersionNumber = 2, DefinitionJson = serializer.Serialize(legacy) };
        legacyForm.Versions.Add(legacyVersion);
        var legacyUpgrade = await app.UpgradeHandoffDraftAsync(legacyForm.Id, legacyDraft.Id, legacyVersion.Id);
        Check(legacyUpgrade.Draft?.FormVersionId == legacyVersion.Id && legacyUpgrade.Draft.Answers["__templated.recipientEmail"] == users.Email, "Legacy recipient draft upgrades without changing ownership");
        var legacySubmitted = await app.SubmitEntryAsync(legacyForm.Id, new() { DraftEntryId = legacyDraft.Id, Answers = new() { ["to"] = "second" } });
        Check(legacySubmitted.Answers["from"] == "first" && legacySubmitted.Answers["to"] == "second", "Upgraded legacy handoff remains functional");
        await TestDraftUpgradeAsync(app, repo, users, serializer);
        Console.WriteLine("Handoff regression tests passed.");
    }

    private static async Task TestDraftUpgradeAsync(FormsApplicationService app, FakeRepository repo, MutableUserContext users, IFormDefinitionSerializer serializer)
    {
        var original = new FormDefinition
        {
            Metadata = { ["form.settings"] = "{\"templatedFormEnabled\":true,\"handoffSteps\":[{\"id\":\"one\",\"name\":\"First\"},{\"id\":\"two\",\"name\":\"Second\"}]}" },
            Sections = [new FormSectionDefinition { Fields = [Field("first", "Initiator"), Field("second", "Recipient"), Field("attachment", "Initiator")] }]
        };
        var form = new FormAggregate { Publication = { AccessMode = FormAccessMode.Authenticated } };
        var v1 = new FormVersionRecord { VersionNumber = 1, DefinitionJson = serializer.Serialize(original) };
        form.Versions.Add(v1);
        await repo.SaveFormAsync(form);
        users.Email = "first@example.com";
        var file = new SubmittedFileInput { FieldId = "attachment", File = new StoredFile { FileName = "one.txt", RelativePath = "one.txt", Sha256 = "hash" } };
        var draft = await app.SaveDraftSubmissionAsync(form.Id, new() { Answers = new() { ["first"] = "answer" }, Files = [file] });
        await app.TransferDraftSubmissionAsync(form.Id, new() { DraftEntryId = draft.Id, RecipientEmail = "second@example.com" });
        var updated = serializer.Deserialize(v1.DefinitionJson);
        updated.Title = "Updated title";
        updated.Sections[0].Fields.Add(Field("new", "Recipient"));
        var v2 = new FormVersionRecord { VersionNumber = 2, DefinitionJson = serializer.Serialize(updated) };
        form.Versions.Add(v2);
        await Throws(() => app.UpgradeHandoffDraftAsync(form.Id, draft.Id, v2.Id), "Former owner cannot upgrade transferred draft");
        users.Email = "second@example.com";
        await Throws(() => app.UpgradeHandoffDraftAsync(form.Id, draft.Id, v1.Id), "Upgrade rejects stale displayed version");
        var upgrade = await app.UpgradeHandoffDraftAsync(form.Id, draft.Id, v2.Id);
        Check(upgrade.IncompatibilityReason is null && upgrade.Draft is not null, "Compatible draft upgrades");
        var loaded = (await repo.GetEntryAsync(draft.Id))!;
        Check(loaded.FormVersionId == v2.Id && loaded.SubmittedByEmail == users.Email && loaded.Answers["first"] == "answer" && loaded.Answers["__handoff.stepId"] == "two" && loaded.Files.Single().FileName == "one.txt", "Upgrade preserves ownership, stage, answers and files");
        Check(loaded.Revisions.Count == 3, "Upgrade creates an audit revision");
        await app.SaveDraftSubmissionAsync(form.Id, new() { DraftEntryId = draft.Id, Answers = new() { ["second"] = "complete" } });

        var v3 = new FormVersionRecord { VersionNumber = 3, DefinitionJson = v2.DefinitionJson };
        form.Versions.Add(v3);
        repo.WriteBarrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> UpgradeRace() { try { return (await app.UpgradeHandoffDraftAsync(form.Id, draft.Id, v3.Id)).Draft is not null; } catch (InvalidOperationException) { return false; } }
        var races = await Task.WhenAll(UpgradeRace(), UpgradeRace());
        repo.WriteBarrier = null;
        Check(races.Count(success => success) == 1, "Concurrent upgrades have one winner");
        loaded = (await repo.GetEntryAsync(draft.Id))!;
        Check(loaded.FormVersionId == v3.Id && loaded.Answers["second"] == "complete" && loaded.Files.Count == 1, "Concurrent upgrade preserves committed data");

        var changed = serializer.Deserialize(v3.DefinitionJson);
        changed.Sections[0].Fields.Single(f => f.Id == "first").Kind = FormFieldKind.Number;
        var v4 = new FormVersionRecord { VersionNumber = 4, DefinitionJson = serializer.Serialize(changed) };
        form.Versions.Add(v4);
        var incompatible = await app.UpgradeHandoffDraftAsync(form.Id, draft.Id, v4.Id);
        Check(incompatible.Draft is null && incompatible.IncompatibilityReason is not null, "Changed question blocks upgrade");
        Check((await repo.GetEntryAsync(draft.Id))!.FormVersionId == v3.Id, "Failed upgrade leaves old draft unchanged");

        var newRequired = serializer.Deserialize(v3.DefinitionJson);
        var earlier = Field("newRequired", "Initiator");
        earlier.Required = true;
        newRequired.Sections[0].Fields.Add(earlier);
        var v5 = new FormVersionRecord { VersionNumber = 5, DefinitionJson = serializer.Serialize(newRequired) };
        form.Versions.Add(v5);
        Check((await app.UpgradeHandoffDraftAsync(form.Id, draft.Id, v5.Id)).Draft is null, "New required earlier-stage question blocks upgrade");

        var reordered = serializer.Deserialize(v3.DefinitionJson);
        reordered.Metadata["form.settings"] = "{\"templatedFormEnabled\":true,\"handoffSteps\":[{\"id\":\"two\",\"name\":\"Second\"},{\"id\":\"one\",\"name\":\"First\"}]}";
        var v6 = new FormVersionRecord { VersionNumber = 6, DefinitionJson = serializer.Serialize(reordered) };
        form.Versions.Add(v6);
        Check((await app.UpgradeHandoffDraftAsync(form.Id, draft.Id, v6.Id)).Draft is null, "Reordered stages block upgrade");
        await Throws(() => app.SaveDraftSubmissionAsync(form.Id, new() { StartNew = true, ExpectedFormVersionId = v5.Id }), "Restart rejects stale version");
        await Throws(() => app.SaveDraftSubmissionAsync(form.Id, new() { StartNew = true, DraftEntryId = draft.Id }), "Restart cannot specify existing draft");
        var fresh = await app.SaveDraftSubmissionAsync(form.Id, new() { StartNew = true, ExpectedFormVersionId = v6.Id });
        Check(fresh.Id != draft.Id && fresh.FormVersionId == v6.Id && fresh.Answers["__handoff.stepId"] == "two" && !fresh.Answers.ContainsKey("first") && fresh.Files.Count == 0, "Restart creates a separate empty first-stage draft");
        loaded = (await repo.GetEntryAsync(draft.Id))!;
        Check(loaded.FormVersionId == v3.Id && loaded.Answers["first"] == "answer" && loaded.Files.Count == 1, "Restart leaves original draft intact");
    }

    private static FormFieldDefinition Field(string id, string editableBy) => new() { Id = id, Label = id, Metadata = { ["field.settings"] = $"{{\"editableBy\":\"{editableBy}\"}}" } };
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static async Task Throws(Func<Task> action, string message) { try { await action(); } catch (InvalidOperationException) { return; } throw new InvalidOperationException(message); }
    private sealed class MutableUserContext : ICurrentUserContext
    {
        public string Email { get; set; } = "";
        public UserProfile GetCurrentUser() => new() { UserId = Guid.Parse("e57747ad-83ef-43f1-a0b0-df913e572c77"), IsAuthenticated = true, Email = Email, DisplayName = Email, Roles = [FormPermissionRole.Submitter] };
    }
}
