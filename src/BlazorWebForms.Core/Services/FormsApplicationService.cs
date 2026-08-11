using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BlazorWebForms.Core.Services;

public sealed class FormsApplicationService(
    IFormsRepository repository,
    IFormDefinitionSerializer serializer,
    IPermissionEvaluator permissionEvaluator,
    ICurrentUserContext currentUserContext,
    IEnumerable<IFormPrefillProvider> prefillProviders,
    IEnumerable<ICustomFieldHandler> customFieldHandlers,
    IEmailNotifier emailNotifier,
    IFileStorage fileStorage,
    IPdfExporter pdfExporter,
    ICoreMetadataCache metadataCache,
    IAntiAbuseGuard antiAbuseGuard,
    IOperationalTelemetry telemetry,
    IWebhookDispatcher webhookDispatcher,
    IFormulaEvaluator formulaEvaluator,
    ICaptchaValidator captchaValidator,
    IFormAnalyticsStore analyticsStore,
    ILogger<FormsApplicationService> logger)
{
    private const int DefaultAdminEntryPageSize = 100;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Forms seed started.");
        await repository.SeedAsync(cancellationToken);
        logger.LogInformation("Forms seed completed.");
    }

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
        form.Publication.OpenUtc = request.OpenUtc;
        form.Publication.CloseUtc = request.CloseUtc;
        form.Publication.NotYetOpenMessage = request.NotYetOpenMessage;
        form.Publication.ClosedMessage = request.ClosedMessage;
        form.Publication.MaxSubmissions = request.MaxSubmissions;
        form.Publication.CapReachedMessage = request.CapReachedMessage;
        form.Publication.ConfirmationMessage = request.ConfirmationMessage;
        form.Publication.ConfirmationRedirectUrl = request.ConfirmationRedirectUrl;
        form.Publication.RequireCaptcha = request.RequireCaptcha;
        form.Publication.AutoSaveIntervalSeconds = request.AutoSaveIntervalSeconds;
        if (request.AccessPasswordPlainText is not null)
        {
            if (!string.IsNullOrWhiteSpace(request.AccessPasswordPlainText))
            {
                form.Publication.AccessPasswordHash = HashAccessPassword(request.AccessPasswordPlainText);
            }
            else if (request.FormId.HasValue)
            {
                // Explicit empty string clears the password
                form.Publication.AccessPasswordHash = string.Empty;
            }
        }
        SanitizeAndValidateBranding(request.Definition.Branding);
        ValidateLocalizationPayload(request.Definition);
        form.DraftDefinition = request.Definition;
        form.UpdatedUtc = DateTimeOffset.UtcNow;
        form.Notifications = request.NotificationEmails
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(email => new FormNotificationRule { Email = email.Trim() })
            .ToList();
        form.Webhooks = request.Webhooks
            .Where(w => !string.IsNullOrWhiteSpace(w.Url))
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
        logger.LogInformation("Form draft saved for FormId {FormId} by UserId {UserId}.", form.Id, user.UserId);
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
        logger.LogInformation("Form published for FormId {FormId} with Version {VersionNumber} by UserId {UserId}.", form.Id, version.VersionNumber, user.UserId);
        metadataCache.InvalidateForms();
        return version;
    }

    public async Task<PublishedFormViewModel?> GetPublishedFormAsync(string slug, CancellationToken cancellationToken = default)
    {
        if (metadataCache.TryGetPublishedForm(slug, out var cachedViewModel))
        {
            await analyticsStore.TrackViewAsync(cachedViewModel.Form.Id, cancellationToken);
            return cachedViewModel;
        }

        var form = await repository.GetFormBySlugAsync(slug, cancellationToken);
        if (form is null || form.Versions.Count == 0)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var version = form.Versions.OrderByDescending(v => v.VersionNumber).First();
        var definition = serializer.Deserialize(version.DefinitionJson);

        // Scheduling and access-cap checks
        string? blockReason = null;
        if (form.Publication.OpenUtc.HasValue && now < form.Publication.OpenUtc.Value)
        {
            blockReason = !string.IsNullOrWhiteSpace(form.Publication.NotYetOpenMessage)
                ? form.Publication.NotYetOpenMessage
                : $"This form opens on {form.Publication.OpenUtc.Value:f} UTC.";
        }
        else if (form.Publication.CloseUtc.HasValue && now > form.Publication.CloseUtc.Value)
        {
            blockReason = !string.IsNullOrWhiteSpace(form.Publication.ClosedMessage)
                ? form.Publication.ClosedMessage
                : "This form is no longer accepting submissions.";
        }
        else if (form.Publication.MaxSubmissions.HasValue)
        {
            var submissionCount = await repository.QueryEntriesAsync(new EntryQueryOptions
            {
                FormId = form.Id,
                Status = EntryStatus.Submitted,
                Limit = form.Publication.MaxSubmissions.Value + 1
            }, cancellationToken);
            if (submissionCount.Count >= form.Publication.MaxSubmissions.Value)
            {
                blockReason = !string.IsNullOrWhiteSpace(form.Publication.CapReachedMessage)
                    ? form.Publication.CapReachedMessage
                    : "This form has reached its maximum number of responses.";
            }
        }

        var viewModel = new PublishedFormViewModel
        {
            Form = form,
            Version = version,
            Definition = definition,
            CanSubmit = permissionEvaluator.CanSubmitForm(form, currentUserContext.GetCurrentUser()),
            IsAcceptingSubmissions = blockReason is null,
            AccessBlockReason = blockReason
        };

        metadataCache.SetPublishedForm(slug, viewModel);
        await analyticsStore.TrackViewAsync(form.Id, cancellationToken);
        return viewModel;
    }

    public async Task<Dictionary<string, string?>> ResolvePrefillAnswersAsync(
        FormDefinition definition,
        IReadOnlyDictionary<string, string?>? existingAnswers = null,
        string? employeeEmail = null,
        CancellationToken cancellationToken = default)
    {
        return await ResolvePrefillAnswersCoreAsync(definition, existingAnswers, employeeEmail, includeClaimProviders: true, cancellationToken);
    }

    public async Task<EntryRecord> CreateManagerPrefilledDraftAsync(Guid formId, ManagerPrefillDraftRequest request, CancellationToken cancellationToken = default)
    {
        var form = await repository.GetFormAsync(formId, cancellationToken)
                   ?? throw new InvalidOperationException("Form not found.");
        var manager = currentUserContext.GetCurrentUser();

        if (!permissionEvaluator.CanManageForm(form, manager))
        {
            throw new InvalidOperationException("Current user cannot prefill drafts for this form.");
        }

        if (string.IsNullOrWhiteSpace(request.SubmitterEmail))
        {
            throw new InvalidOperationException("Submitter email is required for manager-prefilled drafts.");
        }

        var version = form.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault()
                      ?? throw new InvalidOperationException("Form has not been published.");
        var definition = serializer.Deserialize(version.DefinitionJson);
        var answers = await ResolvePrefillAnswersCoreAsync(
            definition,
            request.Answers,
            request.SubmitterEmail.Trim(),
            includeClaimProviders: false,
            cancellationToken);

        var entry = new EntryRecord
        {
            FormId = form.Id,
            FormVersionId = version.Id,
            SubmittedBy = string.IsNullOrWhiteSpace(request.SubmitterName) ? request.SubmitterEmail.Trim() : request.SubmitterName.Trim(),
            SubmittedByEmail = request.SubmitterEmail.Trim(),
            SubmittedUtc = DateTimeOffset.UtcNow,
            Status = EntryStatus.Draft,
            Answers = answers
        };
        var revisionNumber = entry.Revisions.Count + 1;
        entry.Files = EntryFileMapper.MapFiles(request.Files, manager.UserId, manager.Email, revisionNumber);
        EntryFileMapper.ApplyFileAnswers(entry.Answers, request.Files);
        entry.SearchIndex = SearchIndexBuilder.Build(definition, entry.Answers);
        entry.ApprovalSteps = request.Approvers
            .Where(approver => !string.IsNullOrWhiteSpace(approver.Email))
            .Select((approver, index) => new ApprovalStepRecord
            {
                Order = index + 1,
                ApproverId = approver.Id,
                ApproverName = !string.IsNullOrEmpty(approver.DisplayName) ? approver.DisplayName : approver.Name,
                ApproverEmail = approver.Email
            })
            .ToList();
        entry.Revisions.Add(new EntryRevisionRecord
        {
            RevisionNumber = revisionNumber,
            EditedBy = manager.DisplayName,
            EditedUtc = entry.SubmittedUtc,
            Answers = new Dictionary<string, string?>(entry.Answers, StringComparer.OrdinalIgnoreCase)
        });

        await repository.SaveEntryAsync(entry, cancellationToken);
        metadataCache.InvalidateForms();
        return entry;
    }

    public async Task<FormSubmissionResult> SubmitEntryAsync(Guid formId, SubmitEntryRequest request, CancellationToken cancellationToken = default)
    {
        var form = await repository.GetFormAsync(formId, cancellationToken)
                   ?? throw new InvalidOperationException("Form not found.");
        var user = currentUserContext.GetCurrentUser();

        if (!permissionEvaluator.CanSubmitForm(form, user))
        {
            throw new InvalidOperationException("Current user cannot submit this form.");
        }

        // ── Access guards ────────────────────────────────────────────────────
        var now = DateTimeOffset.UtcNow;
        if (form.Publication.OpenUtc.HasValue && now < form.Publication.OpenUtc.Value)
        {
            throw new InvalidOperationException(
                !string.IsNullOrWhiteSpace(form.Publication.NotYetOpenMessage)
                    ? form.Publication.NotYetOpenMessage
                    : "This form is not yet open for submissions.");
        }
        if (form.Publication.CloseUtc.HasValue && now > form.Publication.CloseUtc.Value)
        {
            throw new InvalidOperationException(
                !string.IsNullOrWhiteSpace(form.Publication.ClosedMessage)
                    ? form.Publication.ClosedMessage
                    : "This form is closed.");
        }
        if (form.Publication.MaxSubmissions.HasValue)
        {
            var existing = await repository.QueryEntriesAsync(new EntryQueryOptions
            {
                FormId = form.Id,
                Status = EntryStatus.Submitted,
                Limit = form.Publication.MaxSubmissions.Value + 1
            }, cancellationToken);
            if (existing.Count >= form.Publication.MaxSubmissions.Value)
            {
                throw new InvalidOperationException(
                    !string.IsNullOrWhiteSpace(form.Publication.CapReachedMessage)
                        ? form.Publication.CapReachedMessage
                        : "This form has reached its maximum number of responses.");
            }
        }

        // ── Password check ───────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(form.Publication.AccessPasswordHash))
        {
            if (string.IsNullOrWhiteSpace(request.AccessPassword))
            {
                throw new InvalidOperationException("An access password is required to submit this form.");
            }
            var supplied = HashAccessPassword(request.AccessPassword);
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(supplied),
                    Encoding.UTF8.GetBytes(form.Publication.AccessPasswordHash)))
            {
                throw new InvalidOperationException("Incorrect access password.");
            }
        }

        // ── CAPTCHA ──────────────────────────────────────────────────────────
        if (form.Publication.RequireCaptcha)
        {
            var captchaToken = request.CaptchaToken ?? string.Empty;
            var captchaValid = await captchaValidator.ValidateAsync(captchaToken, cancellationToken);
            if (!captchaValid)
            {
                throw new InvalidOperationException("CAPTCHA validation failed. Please try again.");
            }
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
        entry.StartedUtc = request.StartedUtc ?? entry.StartedUtc;
        entry.Answers = new Dictionary<string, string?>(request.Answers, StringComparer.OrdinalIgnoreCase);

        // ── Formula / calculated fields ──────────────────────────────────────
        ApplyFormulaFields(definition, entry.Answers);

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

        // ── Approval steps — with calculated routing ─────────────────────────
        entry.ApprovalSteps = BuildApprovalSteps(request, definition, entry.Answers);

        // ── Quiz / scoring ───────────────────────────────────────────────────
        if (definition.IsQuizMode)
        {
            entry.Score = ComputeQuizScore(definition, entry.Answers);
            entry.QuizPassed = definition.PassScore.HasValue ? entry.Score >= definition.PassScore.Value : null;
        }

        await repository.SaveEntryAsync(entry, cancellationToken);
        metadataCache.InvalidateForms();
        await emailNotifier.NotifyManagersAsync(form, entry, cancellationToken);
        await NotifyPendingApproverAssignmentsAsync(form, entry, cancellationToken);
        await webhookDispatcher.DispatchAsync(form, WebhookTriggerEvent.EntrySubmitted, entry, cancellationToken);

        var completionTime = entry.StartedUtc.HasValue
            ? (TimeSpan?)(entry.SubmittedUtc - entry.StartedUtc.Value)
            : null;
        await analyticsStore.TrackSubmissionAsync(form.Id, completionTime, cancellationToken);

        return new FormSubmissionResult
        {
            Entry = entry,
            Score = entry.Score,
            QuizPassed = entry.QuizPassed,
            ConfirmationMessage = form.Publication.ConfirmationMessage,
            ConfirmationRedirectUrl = form.Publication.ConfirmationRedirectUrl
        };
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
                ApproverId = approver.Id,
                ApproverName = !string.IsNullOrEmpty(approver.DisplayName) ? approver.DisplayName : approver.Name,
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
                ApproverId = approver.Id,
                ApproverName = !string.IsNullOrEmpty(approver.DisplayName) ? approver.DisplayName : approver.Name,
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

    private async Task<Dictionary<string, string?>> ResolvePrefillAnswersCoreAsync(
        FormDefinition definition,
        IReadOnlyDictionary<string, string?>? existingAnswers,
        string? subjectEmail,
        bool includeClaimProviders,
        CancellationToken cancellationToken)
    {
        var user = currentUserContext.GetCurrentUser();
        var answers = existingAnswers is null
            ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string?>(existingAnswers, StringComparer.OrdinalIgnoreCase);

        foreach (var field in definition.Sections.SelectMany(section => section.Fields))
        {
            if (field.Prefill.ApplyWhenEmpty &&
                answers.TryGetValue(field.Id, out var existingValue) &&
                !string.IsNullOrWhiteSpace(existingValue))
            {
                continue;
            }

            var value = await ResolvePrefillValueAsync(definition, field, user, answers, subjectEmail, includeClaimProviders, cancellationToken);
            if (!string.IsNullOrWhiteSpace(value))
            {
                answers[field.Id] = value;
            }
        }

        return answers;
    }

    private async Task<string?> ResolvePrefillValueAsync(
        FormDefinition definition,
        FormFieldDefinition field,
        UserProfile user,
        IReadOnlyDictionary<string, string?> answers,
        string? subjectEmail,
        bool includeClaimProviders,
        CancellationToken cancellationToken)
    {
        if (!includeClaimProviders && field.Prefill.Source == PrefillSourceKind.Claim)
        {
            return null;
        }

        var provider = prefillProviders.FirstOrDefault(provider =>
            ProviderMatches(field.Prefill, provider) &&
            provider.CanResolve(field.Prefill));

        if (provider is null)
        {
            return null;
        }

        return await provider.ResolveAsync(new FormPrefillRequest
        {
            Definition = definition,
            Field = field,
            Requester = user,
            SubjectEmail = subjectEmail,
            ExistingAnswers = answers
        }, cancellationToken);
    }

    private static bool ProviderMatches(FormFieldPrefillDefinition prefill, IFormPrefillProvider provider)
    {
        if (prefill.Source == PrefillSourceKind.Custom)
        {
            return !string.IsNullOrWhiteSpace(prefill.ProviderKey) &&
                   string.Equals(prefill.ProviderKey, provider.ProviderKey, StringComparison.OrdinalIgnoreCase);
        }

        return string.IsNullOrWhiteSpace(prefill.ProviderKey) ||
               string.Equals(prefill.ProviderKey, provider.ProviderKey, StringComparison.OrdinalIgnoreCase);
    }

    private void ValidateDefinitionForPublish(FormDefinition definition)
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

                if (field.Kind == FormFieldKind.RepeatableList)
                {
                    if (field.MinItems is < 0)
                    {
                        throw new InvalidOperationException($"Field '{field.Label}' minimum item count cannot be negative.");
                    }

                    if (field.MaxItems is < 1)
                    {
                        throw new InvalidOperationException($"Field '{field.Label}' maximum item count must be at least one.");
                    }

                    if (field.MinItems.HasValue && field.MaxItems.HasValue && field.MinItems.Value > field.MaxItems.Value)
                    {
                        throw new InvalidOperationException($"Field '{field.Label}' minimum item count cannot exceed maximum item count.");
                    }

                    if (field.RepeatableColumns.Count > 0)
                    {
                        var columnIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var column in field.RepeatableColumns)
                        {
                            if (string.IsNullOrWhiteSpace(column.Id))
                            {
                                throw new InvalidOperationException($"Field '{field.Label}' contains a repeatable column with an empty id.");
                            }

                            if (!columnIds.Add(column.Id))
                            {
                                throw new InvalidOperationException($"Field '{field.Label}' contains duplicate repeatable column id '{column.Id}'.");
                            }

                            if (string.IsNullOrWhiteSpace(column.Label))
                            {
                                throw new InvalidOperationException($"Field '{field.Label}' column '{column.Id}' must have a label.");
                            }
                        }
                    }
                }

                if (field.Kind == FormFieldKind.RankedChoice)
                {
                    if (field.Options.Count < 2)
                    {
                        throw new InvalidOperationException($"Field '{field.Label}' must define at least two options for ranked choice.");
                    }

                    var rankedOptionValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var option in field.Options)
                    {
                        if (string.IsNullOrWhiteSpace(option.Value))
                        {
                            throw new InvalidOperationException($"Field '{field.Label}' includes a ranked choice option with an empty value.");
                        }

                        if (!rankedOptionValues.Add(option.Value))
                        {
                            throw new InvalidOperationException($"Field '{field.Label}' contains duplicate ranked choice option values.");
                        }
                    }

                    if (field.RankCount.HasValue)
                    {
                        if (field.RankCount.Value < 1)
                        {
                            throw new InvalidOperationException($"Field '{field.Label}' rank count must be at least one.");
                        }

                        if (field.RankCount.Value > field.Options.Count)
                        {
                            throw new InvalidOperationException($"Field '{field.Label}' rank count cannot exceed the number of options.");
                        }
                    }
                }

                if (field.Kind == FormFieldKind.Number)
                {
                    if (field.NumberDisplayKind == NumberDisplayKind.Unit && string.IsNullOrWhiteSpace(field.NumberUnit))
                    {
                        throw new InvalidOperationException($"Field '{field.Label}' number unit label is required when display kind is Unit.");
                    }

                    if (field.MinValue.HasValue && field.MaxValue.HasValue && field.MinValue.Value > field.MaxValue.Value)
                    {
                        throw new InvalidOperationException($"Field '{field.Label}' minimum value cannot exceed maximum value.");
                    }

                    if (field.NumberStep.HasValue && field.NumberStep.Value <= 0)
                    {
                        throw new InvalidOperationException($"Field '{field.Label}' number step must be greater than zero.");
                    }
                }

                if ((field.Prefill.Source is PrefillSourceKind.Claim or PrefillSourceKind.Employee) && string.IsNullOrWhiteSpace(field.Prefill.Key))
                {
                    throw new InvalidOperationException($"Field '{field.Label}' prefill key is required.");
                }

                if (field.Prefill.Source == PrefillSourceKind.Custom && string.IsNullOrWhiteSpace(field.Prefill.ProviderKey))
                {
                    throw new InvalidOperationException($"Field '{field.Label}' custom prefill provider key is required.");
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

                if (field.Kind == FormFieldKind.Custom)
                {
                    if (string.IsNullOrWhiteSpace(field.CustomKind))
                    {
                        throw new InvalidOperationException($"Field '{field.Label}' must specify a CustomKind when Kind is Custom.");
                    }

                    var handler = customFieldHandlers.FirstOrDefault(h =>
                        string.Equals(h.Kind, field.CustomKind, StringComparison.OrdinalIgnoreCase));
                    handler?.ValidateDefinition(field);
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

                foreach (var column in field.RepeatableColumns)
                {
                    ValidateLocalizationMap(column.LocalizedLabels, $"Field '{field.Label}' column '{column.Id}' localized labels");
                    ValidateLocalizationMap(column.LocalizedPlaceholders, $"Field '{field.Label}' column '{column.Id}' localized placeholders");
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

    // ── Analytics ─────────────────────────────────────────────────────────────

    public async Task TrackFormAnalyticsAsync(TrackFormAnalyticsRequest request, CancellationToken cancellationToken = default)
    {
        switch (request.Event)
        {
            case FormAnalyticsEvent.View:
                await analyticsStore.TrackViewAsync(request.FormId, cancellationToken);
                break;
            case FormAnalyticsEvent.Start:
                await analyticsStore.TrackStartAsync(request.FormId, cancellationToken);
                break;
            case FormAnalyticsEvent.Submission:
                var completionTime = request.CompletionSeconds.HasValue
                    ? (TimeSpan?)TimeSpan.FromSeconds(request.CompletionSeconds.Value)
                    : null;
                await analyticsStore.TrackSubmissionAsync(request.FormId, completionTime, cancellationToken);
                break;
            case FormAnalyticsEvent.Abandon:
                await analyticsStore.TrackAbandonAsync(request.FormId, cancellationToken);
                break;
        }
    }

    public async Task<FormAnalyticsViewModel?> GetFormAnalyticsAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        var form = await repository.GetFormAsync(formId, cancellationToken);
        if (form is null)
        {
            return null;
        }
        var user = currentUserContext.GetCurrentUser();
        if (!permissionEvaluator.CanManageForm(form, user) && !user.Roles.Contains(FormPermissionRole.Admin))
        {
            return null;
        }
        var summary = await analyticsStore.GetSummaryAsync(formId, cancellationToken);
        return new FormAnalyticsViewModel { Form = form, Summary = summary };
    }

    // ── Formula field helpers ────────────────────────────────────────────────

    private void ApplyFormulaFields(FormDefinition definition, Dictionary<string, string?> answers)
    {
        foreach (var field in definition.Sections.SelectMany(s => s.Fields))
        {
            if ((field.Kind == FormFieldKind.Calculated || field.Kind == FormFieldKind.Hidden)
                && !string.IsNullOrWhiteSpace(field.FormulaExpression))
            {
                answers[field.Id] = formulaEvaluator.Evaluate(field.FormulaExpression, answers);
            }
        }
    }

    // ── Quiz / scoring helpers ───────────────────────────────────────────────

    private static decimal ComputeQuizScore(FormDefinition definition, IReadOnlyDictionary<string, string?> answers)
    {
        var total = 0m;
        foreach (var field in definition.Sections.SelectMany(s => s.Fields))
        {
            if (!answers.TryGetValue(field.Id, out var answer) || answer is null)
            {
                continue;
            }

            // Per-option points (Radio, Select, Checkbox)
            foreach (var option in field.Options)
            {
                if (option.Points.HasValue &&
                    answer.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                          .Any(v => string.Equals(v, option.Value, StringComparison.OrdinalIgnoreCase)))
                {
                    total += option.Points.Value * field.ScoreWeight;
                }
            }

            // Field-level correct answer
            if (!string.IsNullOrWhiteSpace(field.CorrectAnswer) && field.Options.Count == 0)
            {
                if (string.Equals(answer, field.CorrectAnswer, StringComparison.OrdinalIgnoreCase))
                {
                    total += 1m * field.ScoreWeight;
                }
            }
        }
        return total;
    }

    // ── Approval step builder (with calculated routing) ──────────────────────

    private List<ApprovalStepRecord> BuildApprovalSteps(
        SubmitEntryRequest request,
        FormDefinition definition,
        IReadOnlyDictionary<string, string?> answers)
    {
        // If the form definition has a workflow, resolve calculated expressions first
        var workflowSteps = definition.ApprovalWorkflow?.Steps ?? [];

        var steps = request.Approvers
            .Select((approver, index) =>
            {
                var workflowStep = index < workflowSteps.Count ? workflowSteps[index] : null;

                // Calculated routing: expression overrides the static approver from the request
                var resolvedEmail = approver.Email;
                var resolvedName = !string.IsNullOrEmpty(approver.DisplayName) ? approver.DisplayName : approver.Name;
                if (workflowStep is not null)
                {
                    if (!string.IsNullOrWhiteSpace(workflowStep.ApproverEmailExpression))
                    {
                        resolvedEmail = formulaEvaluator.Evaluate(workflowStep.ApproverEmailExpression, answers)
                                        ?? approver.Email;
                    }
                    if (!string.IsNullOrWhiteSpace(workflowStep.ApproverNameExpression))
                    {
                        resolvedName = formulaEvaluator.Evaluate(workflowStep.ApproverNameExpression, answers)
                                       ?? resolvedName;
                    }
                }

                return new ApprovalStepRecord
                {
                    Order = index + 1,
                    ApproverId = approver.Id,
                    ApproverName = resolvedName,
                    ApproverEmail = resolvedEmail,
                    Instructions = workflowStep?.Instructions ?? string.Empty,
                    AcceptorMode = request.StepAcceptors.ContainsKey(index)
                        ? ApprovalStepAcceptorMode.AnyOf
                        : ApprovalStepAcceptorMode.Single,
                    Acceptors = request.StepAcceptors.TryGetValue(index, out var acceptors)
                        ? acceptors.Select(a => new ApprovalAcceptor { Id = a.Id, Name = a.Name, Email = a.Email }).ToList()
                        : []
                };
            })
            .ToList();

        return steps;
    }

    // ── Password hashing ─────────────────────────────────────────────────────

    private static string HashAccessPassword(string plainText)
    {
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
