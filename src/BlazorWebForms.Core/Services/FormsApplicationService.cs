using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;
using System.Text.RegularExpressions;

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
        var user = currentUserContext.GetCurrentUser();
        var forms = await repository.GetFormsAsync(cancellationToken);
        var entries = await repository.GetEntriesAsync(null, null, cancellationToken);

        return new DashboardViewModel
        {
            Forms = forms.OrderBy(f => f.Name).ToList(),
            RecentEntries = entries.OrderByDescending(e => e.SubmittedUtc).Take(10).ToList(),
            CurrentUser = user
        };
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

        var user = currentUserContext.GetCurrentUser();
        var form = await repository.GetFormAsync(entry.FormId, cancellationToken)
                   ?? throw new InvalidOperationException("Form not found.");
        var isApprover = string.Equals(step.ApproverEmail, user.Email, StringComparison.OrdinalIgnoreCase);
        if (!(permissionEvaluator.CanManageForm(form, user) || isApprover || user.Roles.Contains(FormPermissionRole.Admin)))
        {
            throw new InvalidOperationException("Current user cannot approve this step.");
        }

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
        return await fileStorage.SaveAsync(request, cancellationToken);
    }

    public async Task<StoredFile> StoreFileAsync(FileUploadInput input, CancellationToken cancellationToken = default)
    {
        return await fileStorage.SaveAsync(input.Request, cancellationToken);
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

        invitation.Status = InvitationStatus.Accepted;
        invitation.UpdatedUtc = DateTimeOffset.UtcNow;
        invitation.UpdatedByUserId = user.UserId;
        await repository.SaveInvitationAsync(invitation, cancellationToken);
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

    private static IEnumerable<string> GetConditionReferences(string? condition, VisibilityConditionDefinition? rules)
    {
        if (rules is not null)
        {
            foreach (var rule in rules.Rules)
            {
                if (!string.IsNullOrWhiteSpace(rule.FieldId))
                {
                    yield return rule.FieldId;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(condition))
        {
            yield break;
        }

        var parts = condition.Split('=', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]))
        {
            yield return parts[0];
        }
    }
}
