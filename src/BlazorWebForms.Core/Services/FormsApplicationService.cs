using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Core.Services;

public sealed class FormsApplicationService(
    IFormsRepository repository,
    IFormDefinitionSerializer serializer,
    IPermissionEvaluator permissionEvaluator,
    ICurrentUserContext currentUserContext,
    IEmailNotifier emailNotifier,
    IFileStorage fileStorage)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default) =>
        await repository.SeedAsync(cancellationToken);

    public async Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var forms = await repository.GetFormsAsync(cancellationToken);
        var entries = await repository.GetEntriesAsync(null, null, cancellationToken);

        return new DashboardViewModel
        {
            Forms = forms.OrderBy(f => f.Name).ToList(),
            RecentEntries = entries.OrderByDescending(e => e.SubmittedUtc).Take(10).ToList()
        };
    }

    public async Task<BuilderState> GetBuilderStateAsync(Guid? formId, CancellationToken cancellationToken = default)
    {
        var user = currentUserContext.GetCurrentUser();
        var form = formId.HasValue
            ? await repository.GetFormAsync(formId.Value, cancellationToken)
            : CreateEmptyForm(user);

        form ??= CreateEmptyForm(user);

        return new BuilderState
        {
            Form = form,
            CanManage = permissionEvaluator.CanManageForm(form, user)
        };
    }

    public async Task<FormAggregate> SaveDraftAsync(SaveDraftRequest request, CancellationToken cancellationToken = default)
    {
        var user = currentUserContext.GetCurrentUser();
        var form = request.FormId.HasValue
            ? await repository.GetFormAsync(request.FormId.Value, cancellationToken)
            : null;

        form ??= CreateEmptyForm(user);

        form.Name = request.Name;
        form.Description = request.Description;
        form.Key = request.Slug;
        form.Publication.Slug = request.Slug;
        form.Publication.AccessMode = request.AccessMode;
        form.DraftDefinition = request.Definition;
        form.UpdatedUtc = DateTimeOffset.UtcNow;
        form.Notifications = request.NotificationEmails
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(email => new FormNotificationRule { Email = email.Trim() })
            .ToList();

        if (!form.Permissions.Any())
        {
            form.Permissions.Add(new FormPermissionGrant
            {
                UserId = user.UserId,
                DisplayName = user.DisplayName,
                Role = FormPermissionRole.Owner
            });
        }

        await repository.SaveFormAsync(form, cancellationToken);
        return form;
    }

    public async Task<FormVersionRecord> PublishAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        var form = await repository.GetFormAsync(formId, cancellationToken)
                   ?? throw new InvalidOperationException("Form not found.");
        var user = currentUserContext.GetCurrentUser();

        if (!permissionEvaluator.CanManageForm(form, user))
        {
            throw new InvalidOperationException("Current user cannot publish this form.");
        }

        var nextVersion = form.Versions.Count == 0 ? 1 : form.Versions.Max(v => v.VersionNumber) + 1;
        var version = new FormVersionRecord
        {
            VersionNumber = nextVersion,
            DefinitionJson = serializer.Serialize(form.DraftDefinition),
            CreatedUtc = DateTimeOffset.UtcNow
        };

        form.Versions.Add(version);
        form.UpdatedUtc = DateTimeOffset.UtcNow;

        await repository.SaveFormAsync(form, cancellationToken);
        return version;
    }

    public async Task<PublishedFormViewModel?> GetPublishedFormAsync(string slug, CancellationToken cancellationToken = default)
    {
        var form = await repository.GetFormBySlugAsync(slug, cancellationToken);
        if (form is null || form.Versions.Count == 0)
        {
            return null;
        }

        var version = form.Versions.OrderByDescending(v => v.VersionNumber).First();
        return new PublishedFormViewModel
        {
            Form = form,
            Version = version,
            Definition = serializer.Deserialize(version.DefinitionJson),
            CanSubmit = permissionEvaluator.CanSubmitForm(form, currentUserContext.GetCurrentUser())
        };
    }

    public async Task<EntryRecord> SubmitEntryAsync(Guid formId, SubmitEntryRequest request, CancellationToken cancellationToken = default)
    {
        var form = await repository.GetFormAsync(formId, cancellationToken)
                   ?? throw new InvalidOperationException("Form not found.");
        var user = currentUserContext.GetCurrentUser();

        if (!permissionEvaluator.CanSubmitForm(form, user))
        {
            throw new InvalidOperationException("Current user cannot submit this form.");
        }

        var version = form.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault()
                      ?? throw new InvalidOperationException("Form has not been published.");

        var definition = serializer.Deserialize(version.DefinitionJson);
        var entry = new EntryRecord
        {
            FormId = form.Id,
            FormVersionId = version.Id,
            SubmittedBy = user.DisplayName,
            SubmittedByEmail = user.Email,
            SubmittedUtc = DateTimeOffset.UtcNow,
            Answers = new Dictionary<string, string?>(request.Answers, StringComparer.OrdinalIgnoreCase),
            Status = request.Approvers.Count > 0 ? EntryStatus.NeedsApproval : EntryStatus.Submitted
        };

        foreach (var field in definition.Sections.SelectMany(section => section.Fields).Where(field => field.Searchable))
        {
            if (entry.Answers.TryGetValue(field.Id, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                entry.SearchIndex[field.Id] = value!;
            }
        }

        foreach (var submittedFile in request.Files.Where(f => !string.IsNullOrWhiteSpace(f.FieldId)))
        {
            entry.Files.Add(new EntryFileRecord
            {
                FieldId = submittedFile.FieldId,
                FileName = submittedFile.File.FileName,
                ContentType = submittedFile.File.ContentType,
                Length = submittedFile.File.Length,
                RelativePath = submittedFile.File.RelativePath,
                UploadedUtc = DateTimeOffset.UtcNow
            });

            entry.Answers[submittedFile.FieldId] = submittedFile.File.FileName;
        }

        entry.Revisions.Add(new EntryRevisionRecord
        {
            RevisionNumber = 1,
            EditedBy = user.DisplayName,
            EditedUtc = entry.SubmittedUtc,
            Answers = new Dictionary<string, string?>(entry.Answers, StringComparer.OrdinalIgnoreCase)
        });

        entry.ApprovalSteps = request.Approvers
            .Select((approver, index) => new ApprovalStepRecord
            {
                Order = index + 1,
                ApproverName = approver.Name,
                ApproverEmail = approver.Email
            })
            .ToList();

        await repository.SaveEntryAsync(entry, cancellationToken);
        await emailNotifier.NotifyManagersAsync(form, entry, cancellationToken);
        return entry;
    }

    public async Task<EntryRecord> ReviseEntryAsync(Guid entryId, Dictionary<string, string?> answers, CancellationToken cancellationToken = default)
    {
        var entry = await repository.GetEntryAsync(entryId, cancellationToken)
                    ?? throw new InvalidOperationException("Entry not found.");
        var user = currentUserContext.GetCurrentUser();

        var form = await repository.GetFormAsync(entry.FormId, cancellationToken)
                   ?? throw new InvalidOperationException("Form not found.");

        var version = form.Versions.FirstOrDefault(v => v.Id == entry.FormVersionId)
                      ?? throw new InvalidOperationException("Form version not found for entry.");

        var definition = serializer.Deserialize(version.DefinitionJson);

        entry.Answers = new Dictionary<string, string?>(answers, StringComparer.OrdinalIgnoreCase);
        entry.SearchIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in definition.Sections.SelectMany(section => section.Fields).Where(field => field.Searchable))
        {
            if (entry.Answers.TryGetValue(field.Id, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                entry.SearchIndex[field.Id] = value!;
            }
        }

        entry.Revisions.Add(new EntryRevisionRecord
        {
            RevisionNumber = entry.Revisions.Count + 1,
            EditedBy = user.DisplayName,
            EditedUtc = DateTimeOffset.UtcNow,
            Answers = new Dictionary<string, string?>(answers, StringComparer.OrdinalIgnoreCase)
        });

        await repository.SaveEntryAsync(entry, cancellationToken);
        return entry;
    }

    public async Task<EntryRecord> ApproveStepAsync(Guid entryId, Guid stepId, string signature, CancellationToken cancellationToken = default)
    {
        var entry = await repository.GetEntryAsync(entryId, cancellationToken)
                    ?? throw new InvalidOperationException("Entry not found.");
        var step = entry.ApprovalSteps.FirstOrDefault(candidate => candidate.Id == stepId)
                   ?? throw new InvalidOperationException("Approval step not found.");

        step.Status = ApprovalStepStatus.Approved;
        step.Signature = signature;
        step.CompletedUtc = DateTimeOffset.UtcNow;

        if (entry.ApprovalSteps.All(candidate => candidate.Status == ApprovalStepStatus.Approved))
        {
            entry.Status = EntryStatus.Approved;
        }

        await repository.SaveEntryAsync(entry, cancellationToken);
        return entry;
    }

    public Task<IReadOnlyList<EntryRecord>> SearchEntriesAsync(Guid? formId, string? search, CancellationToken cancellationToken = default) =>
        repository.GetEntriesAsync(formId, search, cancellationToken);

    public Task<IReadOnlyList<EntryRecord>> QueryEntriesAsync(EntryQueryOptions options, CancellationToken cancellationToken = default) =>
        repository.QueryEntriesAsync(options, cancellationToken);

    public async Task<EntryDetailViewModel?> GetEntryDetailAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        var entry = await repository.GetEntryAsync(entryId, cancellationToken);
        if (entry is null)
        {
            return null;
        }

        var form = await repository.GetFormAsync(entry.FormId, cancellationToken);
        if (form is null)
        {
            return null;
        }

        FormDefinition definition;
        string? warning = null;

        var version = form.Versions.FirstOrDefault(candidate => candidate.Id == entry.FormVersionId);
        if (version is null)
        {
            definition = form.DraftDefinition;
            warning = "Historical form version was not found. Showing current draft definition for diagnostics.";
        }
        else
        {
            try
            {
                definition = serializer.Deserialize(version.DefinitionJson);
            }
            catch
            {
                definition = form.DraftDefinition;
                warning = "Historical form version is unavailable or invalid. Showing current draft definition for diagnostics.";
            }
        }

        var user = currentUserContext.GetCurrentUser();
        return new EntryDetailViewModel
        {
            Form = form,
            Entry = entry,
            Definition = definition,
            CanView = permissionEvaluator.CanViewEntry(form, entry, user),
            HistoricalRenderWarning = warning
        };
    }

    public async Task<StoredFile> StoreFileAsync(FileUploadRequest request, CancellationToken cancellationToken = default)
    {
        return await fileStorage.SaveAsync(request, cancellationToken);
    }

    public async Task<StoredFile> StoreFileAsync(FileUploadInput input, CancellationToken cancellationToken = default)
    {
        return await fileStorage.SaveAsync(input.Request, cancellationToken);
    }

    private static FormAggregate CreateEmptyForm(UserProfile user)
    {
        var form = new FormAggregate
        {
            OwnerUserId = user.UserId,
            Name = "New form",
            Description = "Describe purpose, versioning rules, and notification recipients.",
            Key = $"form-{Guid.NewGuid():N}"
        };

        form.Publication.Slug = form.Key;
        form.DraftDefinition = DemoFormFactory.CreateDefaultDefinition();
        return form;
    }
}
