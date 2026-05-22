using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;
using BlazorWebForms.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebForms.Infrastructure.SqlServer;

internal sealed class EfFormsRepository : IFormsRepository
{
    private readonly BlazorWebFormsDbContext db;
    private readonly IFormDefinitionSerializer serializer;

    public EfFormsRepository(BlazorWebFormsDbContext db, IFormDefinitionSerializer serializer)
    {
        this.db = db;
        this.serializer = serializer;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var requiresMigration = false;
        try
        {
            var hasAppliedMigrations = await db.Database
                .SqlQueryRaw<int>("""
                    SELECT CAST(CASE
                        WHEN OBJECT_ID(N'__EFMigrationsHistory', N'U') IS NOT NULL
                             AND EXISTS (SELECT 1 FROM [__EFMigrationsHistory])
                        THEN 1 ELSE 0 END AS int) AS [Value]
                    """)
                .SingleAsync(cancellationToken) == 1;

            var formsTableExists = await db.Database
                .SqlQueryRaw<int>("""
                    SELECT CAST(CASE
                        WHEN OBJECT_ID(N'Forms', N'U') IS NOT NULL
                        THEN 1 ELSE 0 END AS int) AS [Value]
                    """)
                .SingleAsync(cancellationToken) == 1;

            requiresMigration = hasAppliedMigrations || !formsTableExists;
        }
        catch
        {
            requiresMigration = true;
        }

        if (requiresMigration)
        {
            try
            {
                await db.Database.MigrateAsync(cancellationToken);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("PendingModelChangesWarning", StringComparison.Ordinal))
            {
                await db.Database.EnsureCreatedAsync(cancellationToken);
            }
        }

        if (await db.Forms.AnyAsync(cancellationToken))
        {
            // if forms exist but no entries, seed demo entry to ensure demo data present
            if (!await db.Entries.AnyAsync(cancellationToken))
            {
                var existingForm = await db.Forms.Include(f => f.Versions).FirstOrDefaultAsync(cancellationToken);
                if (existingForm is not null && existingForm.Versions.Any())
                {
                    var seedEntry = new EntryEntity
                    {
                        FormId = existingForm.Id,
                        FormVersionId = existingForm.Versions.First().Id,
                        SubmittedBy = "Taylor Submitter",
                        SubmittedByEmail = "taylor@example.com",
                        Status = (int)EntryStatus.NeedsApproval,
                        SubmittedUtc = DateTimeOffset.UtcNow
                    };

                    seedEntry.SearchIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["employeeName"] = "Taylor Submitter",
                        ["destination"] = "Phoenix"
                    };

                    seedEntry.Revisions.Add(new EntryRevisionEntity
                    {
                        RevisionNumber = 1,
                        EditedBy = "Taylor Submitter",
                        EditedUtc = seedEntry.SubmittedUtc,
                        Answers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["employeeName"] = "Taylor Submitter",
                            ["destination"] = "Phoenix"
                        }
                    });

                    seedEntry.ApprovalSteps.Add(new ApprovalStepEntity
                    {
                        Order = 1,
                        ApproverName = "Casey Manager",
                        ApproverEmail = "casey@example.com",
                        Status = (int)ApprovalStepStatus.Pending
                    });

                    foreach (var kv in seedEntry.SearchIndex)
                    {
                        seedEntry.SearchIndexEntries.Add(new EntrySearchIndexEntity
                        {
                            Id = Guid.NewGuid(),
                            Key = kv.Key,
                            Value = kv.Value
                        });
                    }

                    db.Entries.Add(seedEntry);
                    await db.SaveChangesAsync(cancellationToken);
                }
            }

            return;
        }

        var form = new FormAggregate
        {
            Name = "Expense approval",
            Description = "Demo form showing builder, publishing, sequential approvals, and historical rendering.",
            Key = "expense-approval",
            OwnerUserId = DemoCurrentUserContext.DefaultUserId,
            DraftDefinition = DemoFormFactory.CreateDefaultDefinition(),
            Publication = new FormPublication
            {
                Slug = "expense-approval",
                AccessMode = FormAccessMode.Public,
                Domain = "demo.local"
            },
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
            ]
        };

        form.Versions.Add(new FormVersionRecord
        {
            VersionNumber = 1,
            DefinitionJson = serializer.Serialize(form.DraftDefinition)
        });

        // persist
        var entity = new FormEntity
        {
            Id = form.Id,
            Key = form.Key,
            Name = form.Name,
            Description = form.Description,
            OwnerUserId = form.OwnerUserId,
            CreatedUtc = form.CreatedUtc,
            UpdatedUtc = form.UpdatedUtc,
            DraftDefinitionJson = serializer.Serialize(form.DraftDefinition),
            PublicationSlug = form.Publication.Slug,
            PublicationDomain = form.Publication.Domain,
            PublicationAccessMode = (int)form.Publication.AccessMode,
            PublicationSendSubmissionCopyToSubmitter = form.Publication.SendSubmissionCopyToSubmitter
        };

        foreach (var v in form.Versions)
        {
            entity.Versions.Add(new FormVersionEntity
            {
                Id = v.Id,
                VersionNumber = v.VersionNumber,
                CreatedUtc = v.CreatedUtc,
                DefinitionJson = v.DefinitionJson
            });
        }

        foreach (var p in form.Permissions)
        {
            entity.Permissions.Add(new FormPermissionEntity
            {
                Id = p.UserId == Guid.Empty ? Guid.NewGuid() : Guid.NewGuid(),
                UserId = p.UserId,
                DisplayName = p.DisplayName,
                Role = (int)p.Role
            });
        }

        foreach (var n in form.Notifications)
        {
            entity.Notifications.Add(new FormNotificationEntity
            {
                Email = n.Email,
                OnSubmission = n.OnSubmission,
                OnApproval = n.OnApproval
            });
        }

        db.Forms.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        // seed a demo entry to match in-memory behavior
        var entry = new EntryEntity
        {
            FormId = entity.Id,
            FormVersionId = entity.Versions.First().Id,
            SubmittedBy = "Taylor Submitter",
            SubmittedByEmail = "taylor@example.com",
            Status = (int)EntryStatus.NeedsApproval,
            SubmittedUtc = DateTimeOffset.UtcNow
        };

        entry.SearchIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["employeeName"] = "Taylor Submitter",
            ["destination"] = "Phoenix"
        };

        entry.Revisions.Add(new EntryRevisionEntity
        {
            RevisionNumber = 1,
            EditedBy = "Taylor Submitter",
            EditedUtc = entry.SubmittedUtc,
            Answers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["employeeName"] = "Taylor Submitter",
                ["destination"] = "Phoenix"
            }
        });

        entry.ApprovalSteps.Add(new ApprovalStepEntity
        {
            Order = 1,
            ApproverName = "Casey Manager",
            ApproverEmail = "casey@example.com",
            Status = (int)ApprovalStepStatus.Pending
        });

        foreach (var kv in entry.SearchIndex)
        {
            entry.SearchIndexEntries.Add(new EntrySearchIndexEntity
            {
                Id = Guid.NewGuid(),
                Key = kv.Key,
                Value = kv.Value
            });
        }

        db.Entries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FormAggregate>> GetFormsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await db.Forms
            .Include(f => f.Versions)
            .Include(f => f.Permissions)
            .Include(f => f.Notifications)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities.Select(ToAggregate).ToList();
    }

    public async Task<FormAggregate?> GetFormAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        var entity = await db.Forms
            .Include(f => f.Versions)
            .Include(f => f.Permissions)
            .Include(f => f.Notifications)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == formId, cancellationToken);
        return entity is null ? null : ToAggregate(entity);
    }

    public async Task<FormAggregate?> GetFormBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var entity = await db.Forms
            .Include(f => f.Versions)
            .Include(f => f.Permissions)
            .Include(f => f.Notifications)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.PublicationSlug == slug, cancellationToken);
        return entity is null ? null : ToAggregate(entity);
    }

    public async Task SaveFormAsync(FormAggregate form, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var existing = await db.Forms
            .Include(f => f.Versions)
            .Include(f => f.Permissions)
            .Include(f => f.Notifications)
            .FirstOrDefaultAsync(f => f.Id == form.Id, cancellationToken);

        if (existing is null)
        {
            existing = new FormEntity { Id = form.Id };
            db.Forms.Add(existing);
        }

        existing.Key = form.Key;
        existing.Name = form.Name;
        existing.Description = form.Description;
        existing.OwnerUserId = form.OwnerUserId;
        existing.UpdatedUtc = form.UpdatedUtc;
        existing.DraftDefinitionJson = serializer.Serialize(form.DraftDefinition);
        existing.PublicationSlug = form.Publication.Slug;
        existing.PublicationDomain = form.Publication.Domain;
        existing.PublicationAccessMode = (int)form.Publication.AccessMode;
        existing.PublicationSendSubmissionCopyToSubmitter = form.Publication.SendSubmissionCopyToSubmitter;

        // replace child collections: versions, permissions, notifications
        db.RemoveRange(existing.Versions);
        existing.Versions.Clear();
        foreach (var v in form.Versions)
        {
            existing.Versions.Add(new FormVersionEntity
            {
                Id = v.Id,
                VersionNumber = v.VersionNumber,
                CreatedUtc = v.CreatedUtc,
                DefinitionJson = v.DefinitionJson
            });
        }

        db.RemoveRange(existing.Permissions);
        existing.Permissions.Clear();
        foreach (var p in form.Permissions)
        {
            existing.Permissions.Add(new FormPermissionEntity
            {
                Id = Guid.NewGuid(),
                UserId = p.UserId,
                DisplayName = p.DisplayName,
                Role = (int)p.Role
            });
        }

        db.RemoveRange(existing.Notifications);
        existing.Notifications.Clear();
        foreach (var n in form.Notifications)
        {
            existing.Notifications.Add(new FormNotificationEntity
            {
                Id = Guid.NewGuid(),
                Email = n.Email,
                OnSubmission = n.OnSubmission,
                OnApproval = n.OnApproval
            });
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new InvalidOperationException("The form was updated by another user. Reload and retry.", ex);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EntryRecord>> GetEntriesAsync(Guid? formId, string? search, CancellationToken cancellationToken = default)
    {
        var q = db.Entries
            .Include(e => e.Revisions)
            .Include(e => e.ApprovalSteps)
            .Include(e => e.Files)
            .Include(e => e.SearchIndexEntries)
            .AsNoTracking();

        if (formId.HasValue)
            q = q.Where(e => e.FormId == formId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();
            q = q.Where(e =>
                e.SubmittedBy.Contains(searchTerm) ||
                e.SearchIndexEntries.Any(s => s.Value.Contains(searchTerm)));
        }

        var list = await q.OrderByDescending(e => e.SubmittedUtc).ToListAsync(cancellationToken);

        return list.Select(ToEntryRecord).ToList();
    }

    public async Task<EntryRecord?> GetEntryAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        var entity = await db.Entries
            .Include(e => e.Revisions)
            .Include(e => e.ApprovalSteps)
            .Include(e => e.Files)
            .Include(e => e.SearchIndexEntries)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == entryId, cancellationToken);
        return entity is null ? null : ToEntryRecord(entity);
    }

    public async Task SaveEntryAsync(EntryRecord entry, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var existing = await db.Entries
            .Include(e => e.Revisions)
            .Include(e => e.ApprovalSteps)
            .Include(e => e.Files)
            .Include(e => e.SearchIndexEntries)
            .FirstOrDefaultAsync(e => e.Id == entry.Id, cancellationToken);

        if (existing is null)
        {
            existing = new EntryEntity { Id = entry.Id };
            db.Entries.Add(existing);
        }

        existing.FormId = entry.FormId;
        existing.FormVersionId = entry.FormVersionId;
        existing.SubmittedBy = entry.SubmittedBy;
        existing.SubmittedByEmail = entry.SubmittedByEmail;
        existing.SubmittedUtc = entry.SubmittedUtc;
        existing.Status = (int)entry.Status;
        existing.Answers = new Dictionary<string, string?>(entry.Answers, StringComparer.OrdinalIgnoreCase);
        existing.SearchIndex = new Dictionary<string, string>(entry.SearchIndex, StringComparer.OrdinalIgnoreCase);

        db.RemoveRange(existing.Revisions);
        existing.Revisions.Clear();
        foreach (var r in entry.Revisions)
        {
            existing.Revisions.Add(new EntryRevisionEntity
            {
                Id = r.Id,
                RevisionNumber = r.RevisionNumber,
                EditedBy = r.EditedBy,
                EditedUtc = r.EditedUtc,
                Answers = new Dictionary<string, string?>(r.Answers, StringComparer.OrdinalIgnoreCase)
            });
        }

        db.RemoveRange(existing.ApprovalSteps);
        existing.ApprovalSteps.Clear();
        foreach (var a in entry.ApprovalSteps)
        {
            existing.ApprovalSteps.Add(new ApprovalStepEntity
            {
                Id = a.Id,
                Order = a.Order,
                ApproverName = a.ApproverName,
                ApproverEmail = a.ApproverEmail,
                Status = (int)a.Status,
                Signature = a.Signature,
                CompletedUtc = a.CompletedUtc
            });
        }

        // update search index entries table for queryable search
        db.RemoveRange(existing.SearchIndexEntries);
        existing.SearchIndexEntries.Clear();
        foreach (var kv in entry.SearchIndex)
        {
            existing.SearchIndexEntries.Add(new EntrySearchIndexEntity
            {
                Id = Guid.NewGuid(),
                Key = kv.Key,
                Value = kv.Value ?? string.Empty
            });
        }

        db.RemoveRange(existing.Files);
        existing.Files.Clear();
        foreach (var file in entry.Files)
        {
            existing.Files.Add(new EntryFileMetadataEntity
            {
                Id = file.Id,
                FieldId = file.FieldId,
                FileName = file.FileName,
                ContentType = file.ContentType,
                Length = file.Length,
                RelativePath = file.RelativePath,
                UploadedUtc = file.UploadedUtc
            });
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new InvalidOperationException("The entry was updated by another user. Reload and retry.", ex);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private FormAggregate ToAggregate(FormEntity entity)
    {
        return new FormAggregate
        {
            Id = entity.Id,
            Key = entity.Key,
            Name = entity.Name,
            Description = entity.Description,
            OwnerUserId = entity.OwnerUserId,
            CreatedUtc = entity.CreatedUtc,
            UpdatedUtc = entity.UpdatedUtc,
            DraftDefinition = string.IsNullOrWhiteSpace(entity.DraftDefinitionJson) ? new FormDefinition() : serializer.Deserialize(entity.DraftDefinitionJson!),
            Publication = new FormPublication
            {
                Slug = entity.PublicationSlug,
                Domain = entity.PublicationDomain,
                AccessMode = (FormAccessMode)entity.PublicationAccessMode,
                SendSubmissionCopyToSubmitter = entity.PublicationSendSubmissionCopyToSubmitter
            },
            Versions = entity.Versions.OrderBy(v => v.VersionNumber).Select(v => new FormVersionRecord
            {
                Id = v.Id,
                VersionNumber = v.VersionNumber,
                CreatedUtc = v.CreatedUtc,
                DefinitionJson = v.DefinitionJson
            }).ToList(),
            Permissions = entity.Permissions.Select(p => new FormPermissionGrant
            {
                UserId = p.UserId,
                DisplayName = p.DisplayName,
                Role = (FormPermissionRole)p.Role
            }).ToList(),
            Notifications = entity.Notifications.Select(n => new FormNotificationRule
            {
                Email = n.Email,
                OnSubmission = n.OnSubmission,
                OnApproval = n.OnApproval
            }).ToList()
        };
    }

    private EntryRecord ToEntryRecord(EntryEntity entity)
    {
        return new EntryRecord
        {
            Id = entity.Id,
            FormId = entity.FormId,
            FormVersionId = entity.FormVersionId,
            SubmittedBy = entity.SubmittedBy,
            SubmittedByEmail = entity.SubmittedByEmail,
            SubmittedUtc = entity.SubmittedUtc,
            Status = (EntryStatus)entity.Status,
            Answers = new Dictionary<string, string?>(entity.Answers, StringComparer.OrdinalIgnoreCase),
            SearchIndex = new Dictionary<string, string>(entity.SearchIndex, StringComparer.OrdinalIgnoreCase),
            Files = entity.Files.Select(f => new EntryFileRecord
            {
                Id = f.Id,
                FieldId = f.FieldId,
                FileName = f.FileName,
                ContentType = f.ContentType,
                Length = f.Length,
                RelativePath = f.RelativePath,
                UploadedUtc = f.UploadedUtc
            }).ToList(),
            Revisions = entity.Revisions.Select(r => new EntryRevisionRecord
            {
                Id = r.Id,
                RevisionNumber = r.RevisionNumber,
                EditedBy = r.EditedBy,
                EditedUtc = r.EditedUtc,
                Answers = new Dictionary<string, string?>(r.Answers, StringComparer.OrdinalIgnoreCase)
            }).ToList(),
            ApprovalSteps = entity.ApprovalSteps.Select(a => new ApprovalStepRecord
            {
                Id = a.Id,
                Order = a.Order,
                ApproverName = a.ApproverName,
                ApproverEmail = a.ApproverEmail,
                Status = (ApprovalStepStatus)a.Status,
                Signature = a.Signature,
                CompletedUtc = a.CompletedUtc
            }).ToList()
        };
    }
}
