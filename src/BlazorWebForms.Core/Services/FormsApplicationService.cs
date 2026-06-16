using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BlazorWebForms.Core.Services;

public sealed class FormsApplicationService(
    IFormsRepository repository,
    IFormDefinitionSerializer serializer,
    IPermissionEvaluator permissionEvaluator,
    ICurrentUserContext currentUserContext,
    IEmailNotifier emailNotifier,
    IFileStorage fileStorage,
    IPdfExporter pdfExporter,
    ICoreMetadataCache metadataCache,
    IAntiAbuseGuard antiAbuseGuard,
    IOperationalTelemetry telemetry)
{
    private const int DefaultAdminEntryPageSize = 100;

    public async Task SeedAsync(CancellationToken cancellationToken = default) =>
        await repository.SeedAsync(cancellationToken);

    public async Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var user = currentUserContext.GetCurrentUser();
        if (metadataCache.TryGetDashboard(user.UserId, out var cachedDashboard))
        {
            return cachedDashboard;
        }

        var forms = await repository.GetFormsAsync(cancellationToken);
        var entries = await repository.QueryEntriesAsync(new EntryQueryOptions { Limit = 10 }, cancellationToken);

        var dashboard = new DashboardViewModel
        {
            Forms = forms.OrderBy(f => f.Name).ToList(),
            RecentEntries = entries.OrderByDescending(e => e.SubmittedUtc).ToList(),
            CurrentUser = user
        };

        metadataCache.SetDashboard(user.UserId, dashboard);
        return dashboard;
    }

    public async Task<BuilderState> GetBuilderStateAsync(Guid? formId, CancellationToken cancellationToken = default)
    {
        var user = currentUserContext.GetCurrentUser();
        var form = formId.HasValue
            ? await repository.GetFormAsync(formId.Value, cancellationToken)
            : CreateEmptyForm(user);

        form ??= CreateEmptyForm(user);

        if (!permissionEvaluator.CanManageForm(form, user))
        {
            throw new InvalidOperationException("Current user cannot access this form in builder.");
        }

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

        if (!permissionEvaluator.CanManageForm(form, user))
        {
            throw new InvalidOperationException("Current user cannot edit this form.");
        }

        form.Name = request.Name;
        form.Description = request.Description;
        form.Key = request.Slug;
        form.Publication.Slug = request.Slug;
        form.Publication.AccessMode = request.AccessMode;
        form.Publication.EditMode = request.EditMode;
        SanitizeAndValidateBranding(request.Definition.Branding);
        ValidateLocalizationPayload(request.Definition);
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
                Role = FormPermissionRole.Owner,
                ScopeType = "Form"
            });
        }

        await repository.SaveFormAsync(form, cancellationToken);
        metadataCache.InvalidateForms();
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

        ValidateDefinitionForPublish(form.DraftDefinition);

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
        metadataCache.InvalidateForms();
        return version;
    }

    public async Task<PublishedFormViewModel?> GetPublishedFormAsync(string slug, CancellationToken cancellationToken = default)
    {
        if (metadataCache.TryGetPublishedForm(slug, out var cachedViewModel))
        {
            return cachedViewModel;
        }

        var form = await repository.GetFormBySlugAsync(slug, cancellationToken);
        if (form is null || form.Versions.Count == 0)
        {
            return null;
        }

        var version = form.Versions.OrderByDescending(v => v.VersionNumber).First();
        var viewModel = new PublishedFormViewModel
        {
            Form = form,
            Version = version,
            Definition = serializer.Deserialize(version.DefinitionJson),
            CanSubmit = permissionEvaluator.CanSubmitForm(form, currentUserContext.GetCurrentUser())
        };

        metadataCache.SetPublishedForm(slug, viewModel);
        return viewModel;
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
        EntryRecord? draft = null;
        if (request.DraftEntryId.HasValue)
        {
            draft = await repository.GetEntryAsync(request.DraftEntryId.Value, cancellationToken);
            if (draft is null || draft.FormId != formId || draft.Status != EntryStatus.Draft ||
                !string.Equals(draft.SubmittedByEmail, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Draft submission was not found for current user.");
            }
        }

        var entry = draft ?? new EntryRecord
        {
            FormId = form.Id,
            FormVersionId = version.Id,
            SubmittedBy = user.DisplayName,
            SubmittedByEmail = user.Email,
            SubmittedUtc = DateTimeOffset.UtcNow,
            Answers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        };

        entry.FormVersionId = version.Id;
        entry.SubmittedBy = user.DisplayName;
        entry.SubmittedByEmail = user.Email;
        entry.SubmittedUtc = DateTimeOffset.UtcNow;
        entry.Answers = new Dictionary<string, string?>(request.Answers, StringComparer.OrdinalIgnoreCase);
        entry.Status = request.Approvers.Count > 0 ? EntryStatus.NeedsApproval : EntryStatus.Submitted;
        var revisionNumber = entry.Revisions.Count + 1;

        entry.SearchIndex = SearchIndexBuilder.Build(definition, entry.Answers);

        entry.Files = EntryFileMapper.MapFiles(request.Files, user.UserId, user.Email, revisionNumber);
        EntryFileMapper.ApplyFileAnswers(entry.Answers, request.Files);

        entry.Revisions.Add(new EntryRevisionRecord
        {
            RevisionNumber = revisionNumber,
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
        metadataCache.InvalidateForms();
        await emailNotifier.NotifyManagersAsync(form, entry, cancellationToken);
        await NotifyPendingApproverAssignmentsAsync(form, entry, cancellationToken);
        return entry;
    }

    public async Task<EntryRecord> SaveDraftSubmissionAsync(Guid formId, SaveDraftSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        var form = await repository.GetFormAsync(formId, cancellationToken)
                   ?? throw new InvalidOperationException("Form not found.");
        var user = currentUserContext.GetCurrentUser();

        if (!permissionEvaluator.CanSubmitForm(form, user))
        {
            throw new InvalidOperationException("Current user cannot save draft submissions for this form.");
        }

        EntryRecord? entry;
        if (request.DraftEntryId.HasValue)
        {
            entry = await repository.GetEntryAsync(request.DraftEntryId.Value, cancellationToken)
                    ?? throw new InvalidOperationException("Draft submission not found.");
            if (entry.FormId != formId || entry.Status != EntryStatus.Draft ||
                !string.Equals(entry.SubmittedByEmail, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Draft submission is not accessible to current user.");
            }
        }
        else
        {
            entry = await repository.GetDraftEntryAsync(formId, user.Email, cancellationToken);
        }

        var version = form.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault()
                      ?? throw new InvalidOperationException("Form has not been published.");

        entry ??= new EntryRecord
        {
            FormId = form.Id,
            FormVersionId = version.Id,
            SubmittedBy = user.DisplayName,
            SubmittedByEmail = user.Email,
            SubmittedUtc = DateTimeOffset.UtcNow,
            Answers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            Status = EntryStatus.Draft
        };

        entry.FormVersionId = version.Id;
        entry.SubmittedBy = user.DisplayName;
        entry.SubmittedByEmail = user.Email;
        entry.SubmittedUtc = DateTimeOffset.UtcNow;
        entry.Status = EntryStatus.Draft;
        entry.Answers = new Dictionary<string, string?>(request.Answers, StringComparer.OrdinalIgnoreCase);
        entry.SearchIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var revisionNumber = entry.Revisions.Count + 1;

        entry.Files = EntryFileMapper.MapFiles(request.Files, user.UserId, user.Email, revisionNumber);
        EntryFileMapper.ApplyFileAnswers(entry.Answers, request.Files);

        var definition = serializer.Deserialize(version.DefinitionJson);
        entry.SearchIndex = SearchIndexBuilder.Build(definition, entry.Answers);

        entry.ApprovalSteps = request.Approvers
            .Where(a => !string.IsNullOrWhiteSpace(a.Email))
            .Select((approver, index) => new ApprovalStepRecord
            {
                Order = index + 1,
                ApproverName = approver.Name,
                ApproverEmail = approver.Email
            })
            .ToList();

        entry.Revisions.Add(new EntryRevisionRecord
        {
            RevisionNumber = revisionNumber,
            EditedBy = user.DisplayName,
            EditedUtc = entry.SubmittedUtc,
            Answers = new Dictionary<string, string?>(entry.Answers, StringComparer.OrdinalIgnoreCase)
        });

        await repository.SaveEntryAsync(entry, cancellationToken);
        metadataCache.InvalidateForms();
        if (entry.ApprovalSteps.Count > 0)
        {
            await NotifyPendingApproverAssignmentsAsync(form, entry, cancellationToken);
        }
        return entry;
    }

    public async Task<EntryRecord?> GetDraftSubmissionAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        var form = await repository.GetFormAsync(formId, cancellationToken)
                   ?? throw new InvalidOperationException("Form not found.");
        var user = currentUserContext.GetCurrentUser();
        if (!permissionEvaluator.CanSubmitForm(form, user))
        {
            throw new InvalidOperationException("Current user cannot access draft submissions for this form.");
        }

        return await repository.GetDraftEntryAsync(formId, user.Email, cancellationToken);
    }

    public async Task<EntryRecord?> GetLatestUserEntryAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        var form = await repository.GetFormAsync(formId, cancellationToken)
                   ?? throw new InvalidOperationException("Form not found.");
        var user = currentUserContext.GetCurrentUser();
        if (!permissionEvaluator.CanSubmitForm(form, user))
        {
            throw new InvalidOperationException("Current user cannot access submission history for this form.");
        }

        var entries = await repository.QueryEntriesAsync(new EntryQueryOptions
        {
            FormId = formId
        }, cancellationToken);

        return entries
            .Where(e => string.Equals(e.SubmittedByEmail, user.Email, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.SubmittedUtc)
            .FirstOrDefault();
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
        entry.SearchIndex = SearchIndexBuilder.Build(definition, entry.Answers);

        entry.Revisions.Add(new EntryRevisionRecord
        {
            RevisionNumber = entry.Revisions.Count + 1,
            EditedBy = user.DisplayName,
            EditedUtc = DateTimeOffset.UtcNow,
            Answers = new Dictionary<string, string?>(answers, StringComparer.OrdinalIgnoreCase)
        });

        if (form.Publication.EditMode == SubmissionEditMode.OverwriteLatest)
        {
            var latest = entry.Revisions.OrderByDescending(r => r.RevisionNumber).First();
            entry.Answers = new Dictionary<string, string?>(latest.Answers, StringComparer.OrdinalIgnoreCase);
            entry.Revisions =
            [
                new EntryRevisionRecord
                {
                    RevisionNumber = 1,
                    EditedBy = latest.EditedBy,
                    EditedUtc = latest.EditedUtc,
                    Answers = new Dictionary<string, string?>(latest.Answers, StringComparer.OrdinalIgnoreCase)
                }
            ];
        }

        await repository.SaveEntryAsync(entry, cancellationToken);
        metadataCache.InvalidateForms();
        return entry;
    }

    public async Task<EntryRecord> ApproveStepAsync(Guid entryId, Guid stepId, string signature, CancellationToken cancellationToken = default)
    {
        var entry = await repository.GetEntryAsync(entryId, cancellationToken)
                    ?? throw new InvalidOperationException("Entry not found.");
        var guard = await ApprovalStepGuard.ValidateAsync(entry, stepId, repository, currentUserContext, permissionEvaluator, cancellationToken);
        var step = guard.Step;
        var form = guard.Form;
        var user = guard.User;

        step.Status = ApprovalStepStatus.Approved;
        step.Signature = signature;
        step.RejectionReason = null;
        step.CompletedUtc = DateTimeOffset.UtcNow;
        entry.ApprovalAuditTrail.Add(new ApprovalAuditEvent
        {
            Action = ApprovalAuditAction.StepApproved,
            ApprovalStepId = step.Id,
            ActorUserId = user.UserId,
            ActorDisplayName = user.DisplayName,
            Signature = signature,
            OccurredUtc = DateTimeOffset.UtcNow
        });

        if (entry.ApprovalSteps.All(candidate => candidate.Status == ApprovalStepStatus.Approved))
        {
            entry.Status = EntryStatus.Approved;
        }

        await repository.SaveEntryAsync(entry, cancellationToken);
        metadataCache.InvalidateForms();
        if (entry.Status == EntryStatus.Approved)
        {
            await emailNotifier.NotifyEntryApprovedAsync(form, entry, cancellationToken);
        }
        return entry;
    }

    public async Task<EntryRecord> RejectStepAsync(Guid entryId, Guid stepId, string reason, CancellationToken cancellationToken = default)
    {
        var entry = await repository.GetEntryAsync(entryId, cancellationToken)
                    ?? throw new InvalidOperationException("Entry not found.");

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Rejection reason is required.");
        }

        var guard = await ApprovalStepGuard.ValidateAsync(entry, stepId, repository, currentUserContext, permissionEvaluator, cancellationToken);
        var step = guard.Step;
        var form = guard.Form;
        var user = guard.User;

        step.Status = ApprovalStepStatus.Rejected;
        step.RejectionReason = reason.Trim();
        step.Signature = null;
        step.CompletedUtc = DateTimeOffset.UtcNow;
        entry.Status = EntryStatus.Rejected;
        entry.ApprovalAuditTrail.Add(new ApprovalAuditEvent
        {
            Action = ApprovalAuditAction.StepRejected,
            ApprovalStepId = step.Id,
            ActorUserId = user.UserId,
            ActorDisplayName = user.DisplayName,
            Reason = step.RejectionReason,
            OccurredUtc = DateTimeOffset.UtcNow
        });

        await repository.SaveEntryAsync(entry, cancellationToken);
        metadataCache.InvalidateForms();
        await emailNotifier.NotifyEntryRejectedAsync(form, entry, step, cancellationToken);
        return entry;
    }

    public async Task<EntryRecord> ResubmitEntryAsync(Guid entryId, ResubmitEntryRequest request, CancellationToken cancellationToken = default)
    {
        var entry = await repository.GetEntryAsync(entryId, cancellationToken)
                    ?? throw new InvalidOperationException("Entry not found.");
        if (entry.Status != EntryStatus.Rejected)
        {
            throw new InvalidOperationException("Only rejected entries can be resubmitted.");
        }

        var user = currentUserContext.GetCurrentUser();
        if (!string.Equals(entry.SubmittedByEmail, user.Email, StringComparison.OrdinalIgnoreCase) &&
            !user.Roles.Contains(FormPermissionRole.Admin))
        {
            throw new InvalidOperationException("Current user cannot resubmit this entry.");
        }

        var form = await repository.GetFormAsync(entry.FormId, cancellationToken)
                   ?? throw new InvalidOperationException("Form not found.");
        var version = form.Versions.FirstOrDefault(v => v.Id == entry.FormVersionId)
                      ?? throw new InvalidOperationException("Form version not found for entry.");
        var definition = serializer.Deserialize(version.DefinitionJson);

        entry.Answers = new Dictionary<string, string?>(request.Answers, StringComparer.OrdinalIgnoreCase);
        entry.SearchIndex = SearchIndexBuilder.Build(definition, entry.Answers);

        entry.ApprovalSteps = request.Approvers
            .Where(a => !string.IsNullOrWhiteSpace(a.Email))
            .Select((approver, index) => new ApprovalStepRecord
            {
                Order = index + 1,
                ApproverName = approver.Name,
                ApproverEmail = approver.Email,
                Status = ApprovalStepStatus.Pending
            })
            .ToList();

        entry.Status = entry.ApprovalSteps.Count > 0 ? EntryStatus.NeedsApproval : EntryStatus.Submitted;
        entry.Revisions.Add(new EntryRevisionRecord
        {
            RevisionNumber = entry.Revisions.Count + 1,
            EditedBy = user.DisplayName,
            EditedUtc = DateTimeOffset.UtcNow,
            Answers = new Dictionary<string, string?>(entry.Answers, StringComparer.OrdinalIgnoreCase)
        });
        entry.ApprovalAuditTrail.Add(new ApprovalAuditEvent
        {
            Action = ApprovalAuditAction.Resubmitted,
            ActorUserId = user.UserId,
            ActorDisplayName = user.DisplayName,
            OccurredUtc = DateTimeOffset.UtcNow
        });

        await repository.SaveEntryAsync(entry, cancellationToken);
        metadataCache.InvalidateForms();
        if (entry.ApprovalSteps.Count > 0)
        {
            await NotifyPendingApproverAssignmentsAsync(form, entry, cancellationToken);
        }
        return entry;
    }

    public async Task<int> SendApprovalRemindersAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        var form = await repository.GetFormAsync(formId, cancellationToken)
                   ?? throw new InvalidOperationException("Form not found.");
        var user = currentUserContext.GetCurrentUser();
        if (!permissionEvaluator.CanManageForm(form, user) && !user.Roles.Contains(FormPermissionRole.Admin))
        {
            throw new InvalidOperationException("Current user cannot send approval reminders for this form.");
        }

        var entries = await repository.QueryEntriesAsync(new EntryQueryOptions
        {
            FormId = formId,
            Status = EntryStatus.NeedsApproval
        }, cancellationToken);

        var sent = 0;
        foreach (var entry in entries)
        {
            var pending = entry.ApprovalSteps
                .Where(s => s.Status == ApprovalStepStatus.Pending)
                .OrderBy(s => s.Order)
                .FirstOrDefault();

            if (pending is null)
            {
                continue;
            }

            var key = $"reminder:{entry.Id}:{pending.Id}:{DateTimeOffset.UtcNow:yyyyMMdd}";
            var correlationId = await emailNotifier.NotifyApproverReminderAsync(form, entry, pending, key, cancellationToken);
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                entry.ApprovalAuditTrail.Add(new ApprovalAuditEvent
                {
                    Action = ApprovalAuditAction.GraphApproverReminder,
                    ApprovalStepId = pending.Id,
                    ActorUserId = user.UserId,
                    ActorDisplayName = user.DisplayName,
                    CorrelationId = correlationId,
                    OccurredUtc = DateTimeOffset.UtcNow
                });
                await repository.SaveEntryAsync(entry, cancellationToken);
                metadataCache.InvalidateForms();
            }
            sent++;
        }

        return sent;
    }

    public Task<IReadOnlyList<EntryRecord>> SearchEntriesAsync(Guid? formId, string? search, CancellationToken cancellationToken = default) =>
        repository.GetEntriesAsync(formId, search, cancellationToken);

    public Task<IReadOnlyList<EntryRecord>> QueryEntriesAsync(EntryQueryOptions options, CancellationToken cancellationToken = default) =>
        repository.QueryEntriesAsync(NormalizeQueryOptions(options), cancellationToken);

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
        if (!permissionEvaluator.CanViewEntry(form, entry, user))
        {
            return null;
        }

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
        var user = currentUserContext.GetCurrentUser();
        try
        {
            await antiAbuseGuard.CheckUploadAllowedAsync(user, request, cancellationToken);
            var stored = await fileStorage.SaveAsync(request, cancellationToken);
            telemetry.TrackUpload("forms-service", stored.Length, success: true);
            return stored;
        }
        catch
        {
            telemetry.TrackUpload("forms-service", request.Content.LongLength, success: false);
            telemetry.TrackFailure("forms", "upload", "store-file-failed");
            throw;
        }
    }

    public async Task<StoredFile> StoreFileAsync(FileUploadInput input, CancellationToken cancellationToken = default)
    {
        return await fileStorage.SaveAsync(input.Request, cancellationToken);
    }

    public async Task<EntryFileDownload> OpenEntryFileAsync(Guid entryId, Guid fileId, CancellationToken cancellationToken = default)
    {
        var entry = await repository.GetEntryAsync(entryId, cancellationToken)
                    ?? throw new InvalidOperationException("Entry not found.");
        var form = await repository.GetFormAsync(entry.FormId, cancellationToken)
                   ?? throw new InvalidOperationException("Form not found.");
        var user = currentUserContext.GetCurrentUser();

        if (!permissionEvaluator.CanViewEntry(form, entry, user))
        {
            throw new InvalidOperationException("Current user cannot view files for this entry.");
        }

        var file = entry.Files.FirstOrDefault(f => f.Id == fileId)
                   ?? throw new InvalidOperationException("File not found for entry.");

        var content = await fileStorage.OpenReadAsync(file.RelativePath, cancellationToken);
        return new EntryFileDownload
        {
            FileName = file.FileName,
            ContentType = file.ContentType,
            Content = content
        };
    }

    public async Task<FileCleanupResult> CleanupStaleDraftFilesAsync(TimeSpan draftAgeThreshold, CancellationToken cancellationToken = default)
    {
        if (draftAgeThreshold <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Draft age threshold must be greater than zero.");
        }

        var cutoffUtc = DateTimeOffset.UtcNow.Subtract(draftAgeThreshold);
        var orphanedFiles = await repository.GetOrphanedFilesAsync(cutoffUtc, cancellationToken);
        var deletedFiles = 0;

        foreach (var orphanedFile in orphanedFiles)
        {
            try
            {
                await fileStorage.DeleteAsync(orphanedFile.RelativePath, cancellationToken);
                deletedFiles++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                telemetry.TrackFailure("cleanup", "file-delete", ex.GetType().Name);
            }
        }

        var deletedDraftEntries = await repository.DeleteDraftEntriesOlderThanAsync(cutoffUtc, cancellationToken);
        if (deletedDraftEntries > 0 || deletedFiles > 0)
        {
            metadataCache.InvalidateForms();
        }
        return new FileCleanupResult
        {
            DeletedDraftEntries = deletedDraftEntries,
            DeletedFiles = deletedFiles
        };
    }

    public async Task<EntryPdfExport> ExportEntryPdfAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        var entry = await repository.GetEntryAsync(entryId, cancellationToken)
                    ?? throw new InvalidOperationException("Entry not found.");
        var form = await repository.GetFormAsync(entry.FormId, cancellationToken)
                   ?? throw new InvalidOperationException("Form not found.");
        var user = currentUserContext.GetCurrentUser();
        if (!permissionEvaluator.CanViewEntry(form, entry, user))
        {
            throw new InvalidOperationException("Current user cannot export this entry.");
        }

        byte[] bytes;
        try
        {
            bytes = await pdfExporter.ExportEntryAsync(form, entry, cancellationToken);
            telemetry.TrackPdfExport("forms-service", bytes.LongLength, success: true);
        }
        catch
        {
            telemetry.TrackPdfExport("forms-service", 0, success: false);
            telemetry.TrackFailure("forms", "pdf-export", "export-failed");
            throw;
        }
        return new EntryPdfExport
        {
            FileName = $"entry-{entry.Id:N}.pdf",
            ContentType = "application/pdf",
            Content = bytes
        };
    }

    public async Task<FormInvitation> CreateInvitationAsync(CreateInvitationRequest request, CancellationToken cancellationToken = default)
    {
        var user = currentUserContext.GetCurrentUser();
        var form = await repository.GetFormAsync(request.FormId, cancellationToken)
                   ?? throw new InvalidOperationException("Form not found.");

        if (!permissionEvaluator.CanManageForm(form, user))
        {
            throw new InvalidOperationException("Current user cannot manage invitations for this form.");
        }

        var existingInvitations = await repository.GetInvitationsAsync(request.FormId, cancellationToken);
        var duplicatePending = existingInvitations.Any(i =>
            i.Status == InvitationStatus.Pending &&
            i.ExpiresUtc > DateTimeOffset.UtcNow &&
            i.Role == request.Role &&
            string.Equals(i.Email, request.Email, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(i.ScopeType, request.ScopeType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(i.ScopeValue ?? string.Empty, request.ScopeValue ?? string.Empty, StringComparison.OrdinalIgnoreCase));

        if (duplicatePending)
        {
            throw new InvalidOperationException("A pending invitation already exists for this user and scope.");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new InvalidOperationException("Invitation email is required.");
        }

        if (request.ValidFor <= TimeSpan.Zero || request.ValidFor > TimeSpan.FromDays(30))
        {
            throw new InvalidOperationException("Invitation validity must be between 1 second and 30 days.");
        }

        var invitation = new FormInvitation
        {
            FormId = request.FormId,
            Email = request.Email.Trim(),
            Role = request.Role,
            ScopeType = string.IsNullOrWhiteSpace(request.ScopeType) ? "Form" : request.ScopeType,
            ScopeValue = request.ScopeValue,
            Token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('='),
            ExpiresUtc = DateTimeOffset.UtcNow.Add(request.ValidFor),
            Status = InvitationStatus.Pending,
            CreatedByUserId = user.UserId,
            CreatedUtc = DateTimeOffset.UtcNow
        };

        await repository.SaveInvitationAsync(invitation, cancellationToken);
        metadataCache.InvalidateForms();
        await emailNotifier.NotifyInvitationCreatedAsync(form, invitation, cancellationToken);
        return invitation;
    }

    public Task<IReadOnlyList<FormInvitation>> GetInvitationsAsync(Guid formId, CancellationToken cancellationToken = default) =>
        repository.GetInvitationsAsync(formId, cancellationToken);

    public async Task<FormInvitation> AcceptInvitationAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Invitation token is required.");
        }

        var user = currentUserContext.GetCurrentUser();
        if (!user.IsAuthenticated)
        {
            throw new InvalidOperationException("Current user must be authenticated to accept invitations.");
        }

        var invitation = await repository.GetInvitationByTokenAsync(token, cancellationToken)
                         ?? throw new InvalidOperationException("Invitation not found.");

        if (invitation.Status != InvitationStatus.Pending)
        {
            throw new InvalidOperationException("Invitation is no longer pending.");
        }

        if (invitation.ExpiresUtc <= DateTimeOffset.UtcNow)
        {
            invitation.Status = InvitationStatus.Expired;
            invitation.UpdatedUtc = DateTimeOffset.UtcNow;
            invitation.UpdatedByUserId = user.UserId;
            await repository.SaveInvitationAsync(invitation, cancellationToken);
            metadataCache.InvalidateForms();
            throw new InvalidOperationException("Invitation has expired.");
        }

        var form = await repository.GetFormAsync(invitation.FormId, cancellationToken)
                   ?? throw new InvalidOperationException("Form not found.");

        var existing = form.Permissions.FirstOrDefault(p => p.UserId == user.UserId);
        if (existing is null)
        {
            form.Permissions.Add(new FormPermissionGrant
            {
                UserId = user.UserId,
                DisplayName = user.DisplayName,
                Role = invitation.Role,
                ScopeType = invitation.ScopeType,
                ScopeValue = invitation.ScopeValue
            });
        }
        else
        {
            existing.Role = invitation.Role;
            existing.ScopeType = invitation.ScopeType;
            existing.ScopeValue = invitation.ScopeValue;
        }

        await repository.SaveFormAsync(form, cancellationToken);
        metadataCache.InvalidateForms();

        invitation.Status = InvitationStatus.Accepted;
        invitation.UpdatedUtc = DateTimeOffset.UtcNow;
        invitation.UpdatedByUserId = user.UserId;
        await repository.SaveInvitationAsync(invitation, cancellationToken);
        metadataCache.InvalidateForms();
        await emailNotifier.NotifyInvitationAcceptedAsync(form, invitation, cancellationToken);
        return invitation;
    }

    public async Task<FormInvitation> RevokeInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        var user = currentUserContext.GetCurrentUser();
        var invitation = await repository.GetInvitationAsync(invitationId, cancellationToken)
                         ?? throw new InvalidOperationException("Invitation not found.");

        var form = await repository.GetFormAsync(invitation.FormId, cancellationToken)
                   ?? throw new InvalidOperationException("Form not found.");
        if (!permissionEvaluator.CanManageForm(form, user))
        {
            throw new InvalidOperationException("Current user cannot revoke invitations for this form.");
        }

        invitation.Status = InvitationStatus.Revoked;
        invitation.UpdatedByUserId = user.UserId;
        invitation.UpdatedUtc = DateTimeOffset.UtcNow;
        await repository.SaveInvitationAsync(invitation, cancellationToken);
        metadataCache.InvalidateForms();
        await emailNotifier.NotifyInvitationRevokedAsync(form, invitation, cancellationToken);
        return invitation;
    }

    private static FormAggregate CreateEmptyForm(UserProfile user)
    {
        if (!user.IsAuthenticated)
        {
            throw new InvalidOperationException("Anonymous users cannot create forms.");
        }

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

    private static void ValidateDefinitionForPublish(FormDefinition definition)
    {
        if (definition.SchemaVersion <= 0 || definition.SchemaVersion > FormDefinition.CurrentSchemaVersion)
        {
            throw new InvalidOperationException("Draft definition schema version is invalid for publish.");
        }

        ValidateLocalizationPayload(definition);
        SanitizeAndValidateBranding(definition.Branding);

        if (definition.Sections.Count == 0)
        {
            throw new InvalidOperationException("At least one section is required before publish.");
        }

        var sectionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fieldIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var section in definition.Sections)
        {
            if (string.IsNullOrWhiteSpace(section.Id))
            {
                throw new InvalidOperationException("Section id is required.");
            }

            if (!sectionIds.Add(section.Id))
            {
                throw new InvalidOperationException($"Duplicate section id '{section.Id}' found.");
            }

            if (section.Fields.Count == 0)
            {
                throw new InvalidOperationException($"Section '{section.Title}' must include at least one field.");
            }

            foreach (var field in section.Fields)
            {
                if (string.IsNullOrWhiteSpace(field.Id))
                {
                    throw new InvalidOperationException("Field id is required.");
                }

                if (!fieldIds.Add(field.Id))
                {
                    throw new InvalidOperationException($"Duplicate field id '{field.Id}' found.");
                }

                if (!string.IsNullOrWhiteSpace(field.RegexPattern))
                {
                    try
                    {
                        _ = new Regex(field.RegexPattern);
                    }
                    catch (ArgumentException)
                    {
                        throw new InvalidOperationException($"Field '{field.Label}' has an invalid regex pattern.");
                    }
                }

                if (field.Kind is FormFieldKind.Select or FormFieldKind.Radio)
                {
                    if (field.Options.Count == 0)
                    {
                        throw new InvalidOperationException($"Field '{field.Label}' must define at least one option.");
                    }

                    var optionValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var option in field.Options)
                    {
                        if (string.IsNullOrWhiteSpace(option.Value))
                        {
                            throw new InvalidOperationException($"Field '{field.Label}' includes an option with empty value.");
                        }

                        if (!optionValues.Add(option.Value))
                        {
                            throw new InvalidOperationException($"Field '{field.Label}' contains duplicate option values.");
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(field.DefaultValue) && !optionValues.Contains(field.DefaultValue))
                    {
                        throw new InvalidOperationException($"Field '{field.Label}' default value must match an option value.");
                    }
                }
            }
        }

        foreach (var section in definition.Sections)
        {
            foreach (var sectionRef in GetConditionReferences(section.VisibilityCondition, section.VisibilityRules))
            {
                if (!fieldIds.Contains(sectionRef))
                {
                    throw new InvalidOperationException($"Section '{section.Title}' visibility condition references unknown field '{sectionRef}'.");
                }
            }

            foreach (var field in section.Fields)
            {
                foreach (var fieldRef in GetConditionReferences(field.VisibilityCondition, field.VisibilityRules))
                {
                    if (!fieldIds.Contains(fieldRef))
                    {
                        throw new InvalidOperationException($"Field '{field.Label}' visibility condition references unknown field '{fieldRef}'.");
                    }
                }
            }
        }
    }

    private static IEnumerable<string> GetConditionReferences(string? condition, VisibilityConditionDefinition? rules) =>
        ConditionReferenceHelper.GetConditionReferences(condition, rules);

    private static void ValidateLocalizationPayload(FormDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.DefaultCulture))
        {
            throw new InvalidOperationException("Default culture is required.");
        }

        EnsureValidCulture(definition.DefaultCulture, "Default culture");
        ValidateLocalizationMap(definition.LocalizedTitles, "Form localized titles");
        ValidateLocalizationMap(definition.LocalizedDescriptions, "Form localized descriptions");

        foreach (var section in definition.Sections)
        {
            ValidateLocalizationMap(section.LocalizedTitles, $"Section '{section.Title}' localized titles");
            ValidateLocalizationMap(section.LocalizedDescriptions, $"Section '{section.Title}' localized descriptions");

            foreach (var field in section.Fields)
            {
                ValidateLocalizationMap(field.LocalizedLabels, $"Field '{field.Label}' localized labels");
                ValidateLocalizationMap(field.LocalizedPlaceholders, $"Field '{field.Label}' localized placeholders");
                ValidateLocalizationMap(field.LocalizedHelpTexts, $"Field '{field.Label}' localized help texts");
                ValidateLocalizationMap(field.LocalizedValidationHints, $"Field '{field.Label}' localized validation hints");

                foreach (var option in field.Options)
                {
                    ValidateLocalizationMap(option.LocalizedLabels, $"Field '{field.Label}' option '{option.Value}' localized labels");
                }
            }
        }
    }

    private static void ValidateLocalizationMap(IReadOnlyDictionary<string, string> map, string scope)
    {
        foreach (var pair in map)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new InvalidOperationException($"{scope} contains an empty culture key.");
            }

            EnsureValidCulture(pair.Key, $"{scope} culture '{pair.Key}'");

            if (string.IsNullOrWhiteSpace(pair.Value))
            {
                throw new InvalidOperationException($"{scope} has empty localized text for culture '{pair.Key}'.");
            }
        }
    }

    private static void EnsureValidCulture(string culture, string scope)
    {
        try
        {
            _ = CultureInfo.GetCultureInfo(culture);
        }
        catch (CultureNotFoundException)
        {
            throw new InvalidOperationException($"{scope} is not a valid culture.");
        }
    }

    private static void SanitizeAndValidateBranding(BrandingDefinition branding)
    {
        branding.LogoUrl = NormalizeHttpAssetUrl(branding.LogoUrl, "Branding logo URL");
        branding.HeroImageUrl = NormalizeHttpAssetUrl(branding.HeroImageUrl, "Branding hero image URL");
        branding.LogoFileRef = NormalizeFileRef(branding.LogoFileRef, "Branding logo file ref");
        branding.HeroImageFileRef = NormalizeFileRef(branding.HeroImageFileRef, "Branding hero image file ref");
        branding.AccentColor = NormalizeColorToken(branding.AccentColor, "Branding accent color", "#0f766e");
        branding.SurfaceColor = NormalizeColorToken(branding.SurfaceColor, "Branding surface color", "#ffffff");
        branding.TextColor = NormalizeColorToken(branding.TextColor, "Branding text color", "#124040");
        branding.ButtonRadius = NormalizeRadiusToken(branding.ButtonRadius, "Branding button radius");
        branding.HeroText = (branding.HeroText ?? string.Empty).Trim();
    }

    private static string NormalizeHttpAssetUrl(string? raw, string scope)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var value = raw.Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException($"{scope} must be an absolute http/https URL.");
        }

        return uri.ToString();
    }

    private static string NormalizeFileRef(string? raw, string scope)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var value = raw.Trim();
        if (value.Length > 256 || value.Contains("..", StringComparison.Ordinal) || value.StartsWith('/'))
        {
            throw new InvalidOperationException($"{scope} is invalid.");
        }

        if (!Regex.IsMatch(value, "^[A-Za-z0-9_./-]+$"))
        {
            throw new InvalidOperationException($"{scope} can only include letters, numbers, underscore, dot, slash, and dash.");
        }

        return value;
    }

    private static string NormalizeColorToken(string? raw, string scope, string fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        var value = raw.Trim();
        if (!Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$"))
        {
            throw new InvalidOperationException($"{scope} must be a 6-digit hex color (for example #0f766e).");
        }

        return value.ToLowerInvariant();
    }

    private static string NormalizeRadiusToken(string? raw, string scope)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "999px";
        }

        var value = raw.Trim().ToLowerInvariant();
        if (!Regex.IsMatch(value, "^(0|[0-9]{1,3}(px|rem|%))$"))
        {
            throw new InvalidOperationException($"{scope} must be 0 or a numeric px/rem/% value.");
        }

        return value;
    }

    private static EntryQueryOptions NormalizeQueryOptions(EntryQueryOptions options)
    {
        options.Offset = Math.Max(0, options.Offset);
        if (options.Limit <= 0)
        {
            options.Limit = DefaultAdminEntryPageSize;
        }
        else
        {
            options.Limit = Math.Min(options.Limit, 500);
        }

        return options;
    }

    private async Task NotifyPendingApproverAssignmentsAsync(FormAggregate form, EntryRecord entry, CancellationToken cancellationToken)
    {
        if (entry.Status != EntryStatus.NeedsApproval)
        {
            return;
        }

        var pending = entry.ApprovalSteps
            .Where(s => s.Status == ApprovalStepStatus.Pending)
            .OrderBy(s => s.Order)
            .FirstOrDefault();

        if (pending is null)
        {
            return;
        }

        var key = $"assigned:{entry.Id}:{pending.Id}";
        var correlationId = await emailNotifier.NotifyApproverAssignedAsync(form, entry, pending, key, cancellationToken);
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            var user = currentUserContext.GetCurrentUser();
            entry.ApprovalAuditTrail.Add(new ApprovalAuditEvent
            {
                Action = ApprovalAuditAction.GraphApproverAssigned,
                ApprovalStepId = pending.Id,
                ActorUserId = user.UserId,
                ActorDisplayName = user.DisplayName,
                CorrelationId = correlationId,
                OccurredUtc = DateTimeOffset.UtcNow
            });
            await repository.SaveEntryAsync(entry, cancellationToken);
            metadataCache.InvalidateForms();
        }
    }
}
