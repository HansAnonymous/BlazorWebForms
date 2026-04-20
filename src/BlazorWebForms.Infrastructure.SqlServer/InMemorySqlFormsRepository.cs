using System.Collections.Concurrent;
using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;
using BlazorWebForms.Core.Services;

namespace BlazorWebForms.Infrastructure.SqlServer;

internal sealed class InMemorySqlFormsRepository(IFormDefinitionSerializer serializer) : IFormsRepository
{
    private readonly ConcurrentDictionary<Guid, FormAggregate> forms = new();
    private readonly ConcurrentDictionary<Guid, EntryRecord> entries = new();
    private int seeded;

    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref seeded, 1) == 1)
        {
            return Task.CompletedTask;
        }

        var form = new FormAggregate
        {
            Name = "Expense approval",
            Description = "Demo form showing builder, publishing, sequential approvals, and historical rendering.",
            Key = "expense-approval",
            OwnerUserId = DemoCurrentUserContext.DefaultUserId,
            DraftDefinition = DemoFormFactory.CreateDefaultDefinition(),
            Permissions =
            [
                new FormPermissionGrant
                {
                    UserId = DemoCurrentUserContext.DefaultUserId,
                    DisplayName = "Casey Manager",
                    Role = FormPermissionRole.Owner
                }
            ],
            Notifications =
            [
                new FormNotificationRule { Email = "forms@example.com" }
            ],
            Publication = new FormPublication
            {
                Slug = "expense-approval",
                AccessMode = FormAccessMode.Public,
                Domain = "demo.local"
            }
        };

        form.Versions.Add(new FormVersionRecord
        {
            VersionNumber = 1,
            DefinitionJson = serializer.Serialize(form.DraftDefinition)
        });

        forms[form.Id] = CloneForm(form);

        var entry = new EntryRecord
        {
            FormId = form.Id,
            FormVersionId = form.Versions[0].Id,
            SubmittedBy = "Taylor Submitter",
            SubmittedByEmail = "taylor@example.com",
            Status = EntryStatus.NeedsApproval,
            Answers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["employeeName"] = "Taylor Submitter",
                ["destination"] = "Phoenix",
                ["travelDate"] = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
                ["needsHotel"] = "yes",
                ["hotelNotes"] = "Two nights near airport.",
                ["managerSignature"] = "Pending",
                ["receiptUpload"] = "estimate.pdf"
            },
            SearchIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["employeeName"] = "Taylor Submitter",
                ["destination"] = "Phoenix"
            },
            ApprovalSteps =
            [
                new ApprovalStepRecord
                {
                    Order = 1,
                    ApproverName = "Casey Manager",
                    ApproverEmail = "casey@example.com"
                }
            ],
            Revisions =
            [
                new EntryRevisionRecord
                {
                    RevisionNumber = 1,
                    EditedBy = "Taylor Submitter",
                    Answers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["employeeName"] = "Taylor Submitter",
                        ["destination"] = "Phoenix"
                    }
                }
            ]
        };

        entries[entry.Id] = CloneEntry(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FormAggregate>> GetFormsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FormAggregate>>(forms.Values.Select(CloneForm).ToList());

    public Task<FormAggregate?> GetFormAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        forms.TryGetValue(formId, out var form);
        return Task.FromResult(form is null ? null : CloneForm(form));
    }

    public Task<FormAggregate?> GetFormBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var form = forms.Values.FirstOrDefault(candidate =>
            string.Equals(candidate.Publication.Slug, slug, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(form is null ? null : CloneForm(form));
    }

    public Task SaveFormAsync(FormAggregate form, CancellationToken cancellationToken = default)
    {
        forms[form.Id] = CloneForm(form);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EntryRecord>> GetEntriesAsync(Guid? formId, string? search, CancellationToken cancellationToken = default)
    {
        IEnumerable<EntryRecord> query = entries.Values;

        if (formId.HasValue)
        {
            query = query.Where(entry => entry.FormId == formId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(entry =>
                entry.SearchIndex.Values.Any(value => value.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                entry.SubmittedBy.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IReadOnlyList<EntryRecord>>(query.Select(CloneEntry).OrderByDescending(x => x.SubmittedUtc).ToList());
    }

    public Task<EntryRecord?> GetEntryAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        entries.TryGetValue(entryId, out var entry);
        return Task.FromResult(entry is null ? null : CloneEntry(entry));
    }

    public Task SaveEntryAsync(EntryRecord entry, CancellationToken cancellationToken = default)
    {
        entries[entry.Id] = CloneEntry(entry);
        return Task.CompletedTask;
    }

    private static FormAggregate CloneForm(FormAggregate form) =>
        new()
        {
            Id = form.Id,
            Key = form.Key,
            Name = form.Name,
            Description = form.Description,
            OwnerUserId = form.OwnerUserId,
            CreatedUtc = form.CreatedUtc,
            UpdatedUtc = form.UpdatedUtc,
            DraftDefinition = CloneDefinition(form.DraftDefinition),
            Publication = new FormPublication
            {
                Slug = form.Publication.Slug,
                Domain = form.Publication.Domain,
                AccessMode = form.Publication.AccessMode,
                SendSubmissionCopyToSubmitter = form.Publication.SendSubmissionCopyToSubmitter
            },
            Versions = form.Versions.Select(version => new FormVersionRecord
            {
                Id = version.Id,
                VersionNumber = version.VersionNumber,
                CreatedUtc = version.CreatedUtc,
                DefinitionJson = version.DefinitionJson
            }).ToList(),
            Permissions = form.Permissions.Select(permission => new FormPermissionGrant
            {
                UserId = permission.UserId,
                DisplayName = permission.DisplayName,
                Role = permission.Role
            }).ToList(),
            Notifications = form.Notifications.Select(notification => new FormNotificationRule
            {
                Email = notification.Email,
                OnApproval = notification.OnApproval,
                OnSubmission = notification.OnSubmission
            }).ToList()
        };

    private static FormDefinition CloneDefinition(FormDefinition definition) =>
        new()
        {
            Title = definition.Title,
            Description = definition.Description,
            Branding = new BrandingDefinition
            {
                AccentColor = definition.Branding.AccentColor,
                HeroText = definition.Branding.HeroText,
                LogoUrl = definition.Branding.LogoUrl
            },
            LocalizedTitles = new Dictionary<string, string>(definition.LocalizedTitles, StringComparer.OrdinalIgnoreCase),
            Sections = definition.Sections.Select(section => new FormSectionDefinition
            {
                Id = section.Id,
                Title = section.Title,
                Description = section.Description,
                VisibilityCondition = section.VisibilityCondition,
                Fields = section.Fields.Select(field => new FormFieldDefinition
                {
                    Id = field.Id,
                    Kind = field.Kind,
                    Label = field.Label,
                    Placeholder = field.Placeholder,
                    HelpText = field.HelpText,
                    Required = field.Required,
                    Searchable = field.Searchable,
                    RegexPattern = field.RegexPattern,
                    VisibilityCondition = field.VisibilityCondition,
                    Options = field.Options.Select(option => new FormFieldOption
                    {
                        Value = option.Value,
                        Label = option.Label
                    }).ToList()
                }).ToList()
            }).ToList()
        };

    private static EntryRecord CloneEntry(EntryRecord entry) =>
        new()
        {
            Id = entry.Id,
            FormId = entry.FormId,
            FormVersionId = entry.FormVersionId,
            SubmittedBy = entry.SubmittedBy,
            SubmittedByEmail = entry.SubmittedByEmail,
            SubmittedUtc = entry.SubmittedUtc,
            Status = entry.Status,
            Answers = new Dictionary<string, string?>(entry.Answers, StringComparer.OrdinalIgnoreCase),
            SearchIndex = new Dictionary<string, string>(entry.SearchIndex, StringComparer.OrdinalIgnoreCase),
            Revisions = entry.Revisions.Select(revision => new EntryRevisionRecord
            {
                Id = revision.Id,
                RevisionNumber = revision.RevisionNumber,
                EditedBy = revision.EditedBy,
                EditedUtc = revision.EditedUtc,
                Answers = new Dictionary<string, string?>(revision.Answers, StringComparer.OrdinalIgnoreCase)
            }).ToList(),
            ApprovalSteps = entry.ApprovalSteps.Select(step => new ApprovalStepRecord
            {
                Id = step.Id,
                Order = step.Order,
                ApproverName = step.ApproverName,
                ApproverEmail = step.ApproverEmail,
                Status = step.Status,
                Signature = step.Signature,
                CompletedUtc = step.CompletedUtc
            }).ToList()
        };
}
