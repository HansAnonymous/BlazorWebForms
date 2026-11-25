using System.Globalization;
using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;
using BlazorWebForms.Core.Services;
using BlazorWebForms.Core.Tests;
using Microsoft.Extensions.DependencyInjection;

internal static class DurationRegressionTests
{
    public static async Task RunAsync()
    {
        var checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks++;
        }
        async Task Reject(Func<Task> action, string message)
        {
            try { await action(); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Duration") || ex.Message.Contains("Required field")) { checks++; return; }
            throw new InvalidOperationException(message);
        }
        var field = new FormFieldDefinition { Id = "tenure", Label = "Tenure", Kind = FormFieldKind.Duration, Required = true };
        var range = new DateRangeValue(new(2020, 2, 29), new(2021, 2, 28));
        var raw = range.Serialize();
        Check(raw == "{\"start\":\"2020-02-29\",\"end\":\"2021-02-28\"}", "Duration stores exactly two ISO dates.");
        Check(DateRangeValue.TryParse(raw, out var parsed) && parsed == range && parsed.IsComplete, "Duration round-trip preserves exact dates.");
        Check(new DateRangeValue(null, null).Serialize() is null, "Clearing both dates clears the answer.");
        Check(DateRangeValue.TryParse(new DateRangeValue(range.Start, null).Serialize(), out parsed) && !parsed.IsComplete, "Partial ranges can be parsed for draft editing.");
        Check(!DateRangeValue.TryParse("not-json", out _) && !DateRangeValue.TryParse("null", out _), "Malformed durations are rejected.");
        Check(!new DateRangeValue(range.End, range.Start).IsComplete, "Reversed dates are invalid.");
        Check(TemporalFieldHelper.FormatDurationAnswer(field, raw) == "1 year", "Leap-year anniversary is a complete calendar year.");
        Check(TemporalFieldHelper.FormatDuration(field, new(2026, 1, 31), new(2026, 2, 28)) == "1 month", "Month-end anniversary is one complete month.");
        Check(TemporalFieldHelper.FormatDuration(field, new(2026, 1, 31), new(2026, 2, 27)) == "0 months", "A day before the anniversary is not a completed month.");
        TemporalFieldHelper.SetFormat(field, TemporalFieldHelper.DurationFormatKey, "years-months-days");
        Check(TemporalFieldHelper.FormatDuration(field, new(2023, 8, 20), new(2026, 11, 24)) == "3 years, 3 months, 4 days", "Calendar components have exact expected values.");
        Check(TemporalFieldHelper.FormatDuration(field, new(2026, 11, 24), new(2026, 11, 24)) == "0 days", "Same-day duration is zero days.");
        TemporalFieldHelper.SetFormat(field, TemporalFieldHelper.DurationFormatKey, "months");
        Check(TemporalFieldHelper.FormatDuration(field, new(2023, 8, 20), new(2026, 11, 24)) == "39 months", "Total months are completed calendar months.");
        TemporalFieldHelper.SetFormat(field, TemporalFieldHelper.DurationFormatKey, "days");
        Check(TemporalFieldHelper.FormatDurationAnswer(field, raw) == "365 days", "Total days use actual day numbers.");
        TemporalFieldHelper.SetFormat(field, TemporalFieldHelper.DurationFormatKey, "range");
        TemporalFieldHelper.SetFormat(field, TemporalFieldHelper.DateFormatKey, "MM/dd/yyyy");
        Check(TemporalFieldHelper.FormatDurationAnswer(field, raw, CultureInfo.InvariantCulture) == "02/29/2020 through 02/28/2021", "Range formatting applies the selected date pattern to both dates.");
        Check(TemporalFieldHelper.FormatDate(field, new(2026, 11, 24), culture: CultureInfo.InvariantCulture) == "11/24/2026", "Date formatting is shared with date ranges.");
        Check(raw == range.Serialize(), "Changing formatting does not mutate stored dates.");
        Check((int)FormFieldKind.MatrixMulti == 17 && (int)FormFieldKind.Duration == 18, "Duration is appended without changing persisted enum values.");

        var repo = new FakeRepository { DetachedEntries = true };
        var services = new ServiceCollection();
        services.AddBlazorWebFormsCore();
        services.AddSingleton<IFormsRepository>(repo);
        services.AddSingleton<ICurrentUserContext, TestAuthenticatedUserContext>();
        services.AddSingleton<IEmailNotifier, FakeEmailNotifier>();
        services.AddSingleton<IFileStorage, FakeFileStorage>();
        services.AddSingleton<IPdfExporter, FakePdfExporter>();
        services.AddSingleton<IAntiAbuseGuard, FakeAntiAbuseGuard>();
        services.AddSingleton<IOperationalTelemetry, FakeOperationalTelemetry>();
        await using var provider = services.BuildServiceProvider();
        var app = provider.GetRequiredService<FormsApplicationService>();
        var serializer = provider.GetRequiredService<IFormDefinitionSerializer>();
        var definition = new FormDefinition { Title = "Durations", Sections = [new() { Fields = [field, new() { Id = "optional", Label = "Optional duration", Kind = FormFieldKind.Duration }] }] };
        var form = await app.SaveDraftAsync(new() { Name = "Durations", Slug = $"duration-{Guid.NewGuid():N}", AccessMode = FormAccessMode.Public, Definition = definition });
        await app.PublishAsync(form.Id);
        var clone = serializer.Deserialize(serializer.Serialize(definition));
        Check(clone.Sections[0].Fields[0].Kind == FormFieldKind.Duration && clone.Sections[0].Fields[0].Metadata[TemporalFieldHelper.DateFormatKey] == "MM/dd/yyyy", "Definition round-trip preserves Duration and format metadata.");
        foreach (var invalid in new string?[] { null, "garbage", "{}", "{\"start\":\"2020-02-29\"}", "{\"start\":\"2021-02-28\",\"end\":\"2020-02-29\"}" })
            await Reject(() => app.SubmitEntryAsync(form.Id, new() { Answers = new() { [field.Id] = invalid } }), "Invalid Duration submission was accepted.");
        await Reject(() => app.SubmitEntryAsync(form.Id, new() { Answers = new() { [field.Id] = raw, ["optional"] = "{}" } }), "Invalid optional Duration submission was accepted.");
        var draft = await app.SaveDraftSubmissionAsync(form.Id, new() { Answers = new() { [field.Id] = new DateRangeValue(range.Start, null).Serialize() } });
        Check(draft.Status == EntryStatus.Draft && DateRangeValue.TryParse(draft.Answers[field.Id], out parsed) && parsed.Start == range.Start && parsed.End is null, "Draft retains partial date input.");
        await Reject(() => app.SubmitEntryAsync(form.Id, new() { DraftEntryId = draft.Id, Answers = draft.Answers }), "Partial draft was submitted.");
        Check((await repo.GetEntryAsync(draft.Id))!.Status == EntryStatus.Draft, "Failed Duration validation leaves persisted draft unchanged.");
        var entry = await app.SubmitEntryAsync(form.Id, new() { DraftEntryId = draft.Id, Answers = new() { [field.Id] = raw } });
        Check(entry.Status == EntryStatus.Submitted && entry.Answers[field.Id] == raw, "Complete range submits with raw ISO date storage.");
        await Reject(() => app.ReviseEntryAsync(entry.Id, new() { [field.Id] = "{}" }), "Invalid Duration revision was accepted.");
        Check((await repo.GetEntryAsync(entry.Id))!.Answers[field.Id] == raw, "Invalid revision does not mutate persisted answers.");
        entry.Status = EntryStatus.Rejected;
        await repo.SaveEntryAsync(entry);
        await Reject(() => app.ResubmitEntryAsync(entry.Id, new() { Answers = new() { [field.Id] = "{}" } }), "Invalid Duration resubmission was accepted.");
        Check((await repo.GetEntryAsync(entry.Id))!.Status == EntryStatus.Rejected, "Invalid resubmission preserves rejected state.");

        var handoffDefinition = new FormDefinition
        {
            Title = "Duration handoff",
            Metadata = { ["form.settings"] = "{\"templatedFormEnabled\":true,\"handoffSteps\":[{\"id\":\"one\",\"name\":\"First\"},{\"id\":\"two\",\"name\":\"Second\"}]}" },
            Sections = [new() { Fields = [
                new() { Id = "tenure", Label = "Tenure", Kind = FormFieldKind.Duration, Required = true, Metadata = { ["field.settings"] = "{\"editableBy\":\"Step:one\"}" } },
                new() { Id = "optional", Label = "Optional duration", Kind = FormFieldKind.Duration, Metadata = { ["field.settings"] = "{\"editableBy\":\"Step:one\"}" } },
                new() { Id = "future", Label = "Later duration", Kind = FormFieldKind.Duration, Required = true, Metadata = { ["field.settings"] = "{\"editableBy\":\"Step:two\"}" } }
            ] }]
        };
        var handoff = await app.SaveDraftAsync(new() { Name = "Duration handoff", Slug = $"duration-handoff-{Guid.NewGuid():N}", Definition = handoffDefinition });
        await app.PublishAsync(handoff.Id);
        var partial = await app.SaveDraftSubmissionAsync(handoff.Id, new() { Answers = new() { ["tenure"] = "{\"start\":\"2020-02-29\"}" } });
        await Reject(() => app.TransferDraftSubmissionAsync(handoff.Id, new() { DraftEntryId = partial.Id, RecipientEmail = "next@example.invalid" }), "Required partial Duration allowed handoff.");
        await app.SaveDraftSubmissionAsync(handoff.Id, new() { DraftEntryId = partial.Id, Answers = new() { ["tenure"] = raw, ["optional"] = "{}" } });
        await Reject(() => app.TransferDraftSubmissionAsync(handoff.Id, new() { DraftEntryId = partial.Id, RecipientEmail = "next@example.invalid" }), "Partial optional Duration became locked after handoff.");
        await app.SaveDraftSubmissionAsync(handoff.Id, new() { DraftEntryId = partial.Id, Answers = new() { ["optional"] = null } });
        var transferred = await app.TransferDraftSubmissionAsync(handoff.Id, new() { DraftEntryId = partial.Id, RecipientEmail = "next@example.invalid" });
        Check(transferred.SubmittedByEmail == "next@example.invalid" && transferred.Answers["tenure"] == raw, "Complete Duration permits handoff and preserves dates without requiring future actor's Duration.");
        Console.WriteLine($"Duration regression tests passed: {checks} checks.");
    }
}
