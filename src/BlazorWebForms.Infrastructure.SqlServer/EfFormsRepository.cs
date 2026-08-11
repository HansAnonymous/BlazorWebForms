using System.Text.Json;
using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;
using BlazorWebForms.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorWebForms.Infrastructure.SqlServer;

internal sealed class EfFormsRepository : IFormsRepository
{
    private readonly BlazorWebFormsDbContext db;
    private readonly IFormDefinitionSerializer serializer;
    private readonly ILogger<EfFormsRepository> logger;

    public EfFormsRepository(BlazorWebFormsDbContext db, IFormDefinitionSerializer serializer, ILogger<EfFormsRepository> logger)
    {
        this.db = db;
        this.serializer = serializer;
        this.logger = logger;
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
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Probe query can fail when database/table doesn't exist yet — this is expected on first run.
            logger.LogDebug(ex, "Database probe failed during seed. Falling back to migration path.");
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
                logger.LogWarning(ex, "Pending model changes detected during migration. Falling back to EnsureCreated.");
                await db.Database.EnsureCreatedAsync(cancellationToken);
            }
        }

        await EnsureLegacySchemaCompatibilityAsync(cancellationToken);

        if (await db.Forms.AnyAsync(cancellationToken))
        {
            // if forms exist but no entries, seed demo entry to ensure demo data present
            if (!await db.Entries.AnyAsync(cancellationToken))
            {
                var existingForm = await db.Forms.Include(f => f.Versions).FirstOrDefaultAsync(cancellationToken);
                if (existingForm is not null && existingForm.Versions.Any())
                {
                    var seedEntry = DemoEntrySeedHelper.CreateDemoEntry(existingForm.Id, existingForm.Versions.First().Id);
                    db.Entries.Add(seedEntry);
                    await db.SaveChangesAsync(cancellationToken);
                    logger.LogInformation("Seeded demo entry for existing form {FormId}.", existingForm.Id);
                }
            }

            logger.LogInformation("Seed completed with existing forms present.");
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
                Domain = "demo.local",
                EditMode = SubmissionEditMode.ImmutableRevisions
            },
            Permissions =
            [
                new FormPermissionGrant
                {
                    UserId = DemoCurrentUserContext.DefaultUserId,
                    DisplayName = "Casey Manager",
                    Role = FormPermissionRole.Owner,
                    ScopeType = "Form"
                },
                new FormPermissionGrant
                {
                    UserId = Guid.Parse("b7f79ea6-f95f-49e8-b6d7-5c3d6d4c6bf9"),
                    DisplayName = "Alex Admin",
                    Role = FormPermissionRole.Admin,
                    ScopeType = "Global"
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
            PublicationSendSubmissionCopyToSubmitter = form.Publication.SendSubmissionCopyToSubmitter,
            PublicationEditMode = (int)form.Publication.EditMode
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
                Role = (int)p.Role,
                ScopeType = string.IsNullOrWhiteSpace(p.ScopeType) ? "Form" : p.ScopeType,
                ScopeValue = p.ScopeValue,
                UpdatedByUserId = form.OwnerUserId,
                UpdatedUtc = DateTimeOffset.UtcNow
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
        var entry = DemoEntrySeedHelper.CreateDemoEntry(entity.Id, entity.Versions.First().Id);
        db.Entries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded default form and demo entry for new database.");
    }

    private Task EnsureLegacySchemaCompatibilityAsync(CancellationToken cancellationToken)
    {
        return db.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'Forms', N'U') IS NOT NULL
               AND COL_LENGTH(N'Forms', N'RowVersion') IS NULL
            BEGIN
                ALTER TABLE [Forms] ADD [RowVersion] ROWVERSION NOT NULL;
            END;

            IF OBJECT_ID(N'Entries', N'U') IS NOT NULL
               AND COL_LENGTH(N'Entries', N'RowVersion') IS NULL
            BEGIN
                ALTER TABLE [Entries] ADD [RowVersion] ROWVERSION NOT NULL;
            END;

            IF OBJECT_ID(N'EntryFiles', N'U') IS NULL
               AND OBJECT_ID(N'Entries', N'U') IS NOT NULL
            BEGIN
                CREATE TABLE [EntryFiles] (
                    [Id] UNIQUEIDENTIFIER NOT NULL,
                    [EntryId] UNIQUEIDENTIFIER NOT NULL,
                    [FieldId] NVARCHAR(128) NOT NULL,
                    [FileName] NVARCHAR(260) NOT NULL,
                    [ContentType] NVARCHAR(128) NOT NULL,
                    [Length] BIGINT NOT NULL,
                    [RelativePath] NVARCHAR(512) NOT NULL,
                    [Sha256] NVARCHAR(64) NOT NULL CONSTRAINT [DF_EntryFiles_Sha256] DEFAULT N'',
                    [UploadedByUserId] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_EntryFiles_UploadedByUserId] DEFAULT ('00000000-0000-0000-0000-000000000000'),
                    [UploadedByEmail] NVARCHAR(256) NOT NULL CONSTRAINT [DF_EntryFiles_UploadedByEmail] DEFAULT N'',
                    [RevisionNumber] INT NOT NULL CONSTRAINT [DF_EntryFiles_RevisionNumber] DEFAULT (1),
                    [UploadedUtc] DATETIMEOFFSET NOT NULL,
                    CONSTRAINT [PK_EntryFiles] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_EntryFiles_Entries_EntryId] FOREIGN KEY ([EntryId]) REFERENCES [Entries]([Id]) ON DELETE CASCADE
                );

                CREATE INDEX [IX_EntryFiles_EntryId] ON [EntryFiles] ([EntryId]);
                CREATE INDEX [IX_EntryFiles_EntryId_FieldId] ON [EntryFiles] ([EntryId], [FieldId]);
                CREATE UNIQUE INDEX [IX_EntryFiles_RelativePath] ON [EntryFiles] ([RelativePath]);
            END;

            IF OBJECT_ID(N'EntryFiles', N'U') IS NOT NULL
               AND COL_LENGTH(N'EntryFiles', N'Sha256') IS NULL
            BEGIN
                ALTER TABLE [EntryFiles]
                ADD [Sha256] NVARCHAR(64) NOT NULL
                    CONSTRAINT [DF_EntryFiles_Sha256] DEFAULT N'';
            END;

            IF OBJECT_ID(N'EntryFiles', N'U') IS NOT NULL
               AND COL_LENGTH(N'EntryFiles', N'UploadedByUserId') IS NULL
            BEGIN
                ALTER TABLE [EntryFiles]
                ADD [UploadedByUserId] UNIQUEIDENTIFIER NOT NULL
                    CONSTRAINT [DF_EntryFiles_UploadedByUserId] DEFAULT ('00000000-0000-0000-0000-000000000000');
            END;

            IF OBJECT_ID(N'EntryFiles', N'U') IS NOT NULL
               AND COL_LENGTH(N'EntryFiles', N'UploadedByEmail') IS NULL
            BEGIN
                ALTER TABLE [EntryFiles]
                ADD [UploadedByEmail] NVARCHAR(256) NOT NULL
                    CONSTRAINT [DF_EntryFiles_UploadedByEmail] DEFAULT N'';
            END;

            IF OBJECT_ID(N'EntryFiles', N'U') IS NOT NULL
               AND COL_LENGTH(N'EntryFiles', N'RevisionNumber') IS NULL
            BEGIN
                ALTER TABLE [EntryFiles]
                ADD [RevisionNumber] INT NOT NULL
                    CONSTRAINT [DF_EntryFiles_RevisionNumber] DEFAULT (1);
            END;

            IF OBJECT_ID(N'EntryFiles', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_EntryFiles_RelativePath'
                      AND object_id = OBJECT_ID(N'EntryFiles', N'U'))
            BEGIN
                CREATE UNIQUE INDEX [IX_EntryFiles_RelativePath] ON [EntryFiles] ([RelativePath]);
            END;

            IF OBJECT_ID(N'FormPermissions', N'U') IS NOT NULL
               AND COL_LENGTH(N'FormPermissions', N'ScopeType') IS NULL
            BEGIN
                ALTER TABLE [FormPermissions]
                ADD [ScopeType] NVARCHAR(32) NOT NULL
                    CONSTRAINT [DF_FormPermissions_ScopeType] DEFAULT N'Form';
            END;

            IF OBJECT_ID(N'FormPermissions', N'U') IS NOT NULL
               AND COL_LENGTH(N'FormPermissions', N'ScopeValue') IS NULL
            BEGIN
                ALTER TABLE [FormPermissions]
                ADD [ScopeValue] NVARCHAR(128) NULL;
            END;

            IF OBJECT_ID(N'FormPermissions', N'U') IS NOT NULL
               AND COL_LENGTH(N'FormPermissions', N'UpdatedByUserId') IS NULL
            BEGIN
                ALTER TABLE [FormPermissions]
                ADD [UpdatedByUserId] UNIQUEIDENTIFIER NOT NULL
                    CONSTRAINT [DF_FormPermissions_UpdatedByUserId] DEFAULT ('00000000-0000-0000-0000-000000000000');
            END;

            IF OBJECT_ID(N'FormPermissions', N'U') IS NOT NULL
               AND COL_LENGTH(N'FormPermissions', N'UpdatedUtc') IS NULL
            BEGIN
                ALTER TABLE [FormPermissions]
                ADD [UpdatedUtc] DATETIMEOFFSET NOT NULL
                    CONSTRAINT [DF_FormPermissions_UpdatedUtc] DEFAULT (SYSUTCDATETIME());
            END;

            IF OBJECT_ID(N'FormPermissions', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_FormPermissions_FormId_ScopeType_ScopeValue'
                      AND object_id = OBJECT_ID(N'FormPermissions', N'U'))
            BEGIN
                CREATE INDEX [IX_FormPermissions_FormId_ScopeType_ScopeValue]
                    ON [FormPermissions] ([FormId], [ScopeType], [ScopeValue]);
            END;

            IF OBJECT_ID(N'FormInvitations', N'U') IS NULL
               AND OBJECT_ID(N'Forms', N'U') IS NOT NULL
            BEGIN
                CREATE TABLE [FormInvitations] (
                    [Id] UNIQUEIDENTIFIER NOT NULL,
                    [FormId] UNIQUEIDENTIFIER NOT NULL,
                    [Email] NVARCHAR(256) NOT NULL,
                    [Role] INT NOT NULL,
                    [ScopeType] NVARCHAR(32) NOT NULL,
                    [ScopeValue] NVARCHAR(128) NULL,
                    [Token] NVARCHAR(128) NOT NULL,
                    [ExpiresUtc] DATETIMEOFFSET NOT NULL,
                    [Status] INT NOT NULL,
                    [CreatedByUserId] UNIQUEIDENTIFIER NOT NULL,
                    [CreatedUtc] DATETIMEOFFSET NOT NULL,
                    [UpdatedByUserId] UNIQUEIDENTIFIER NULL,
                    [UpdatedUtc] DATETIMEOFFSET NULL,
                    CONSTRAINT [PK_FormInvitations] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_FormInvitations_Forms_FormId] FOREIGN KEY ([FormId]) REFERENCES [Forms]([Id]) ON DELETE CASCADE
                );

                CREATE UNIQUE INDEX [IX_FormInvitations_Token] ON [FormInvitations] ([Token]);
                CREATE INDEX [IX_FormInvitations_FormId_Email_Status] ON [FormInvitations] ([FormId], [Email], [Status]);
            END;

            IF OBJECT_ID(N'ApprovalSteps', N'U') IS NOT NULL
               AND COL_LENGTH(N'ApprovalSteps', N'RejectionReason') IS NULL
            BEGIN
                ALTER TABLE [ApprovalSteps]
                ADD [RejectionReason] NVARCHAR(2000) NULL;
            END;

            IF OBJECT_ID(N'ApprovalSteps', N'U') IS NOT NULL
               AND COL_LENGTH(N'ApprovalSteps', N'ApproverId') IS NULL
            BEGIN
                ALTER TABLE [ApprovalSteps]
                ADD [ApproverId] NVARCHAR(256) NOT NULL
                    CONSTRAINT [DF_ApprovalSteps_ApproverId] DEFAULT (N'');
            END;

            IF OBJECT_ID(N'Forms', N'U') IS NOT NULL
               AND COL_LENGTH(N'Forms', N'PublicationEditMode') IS NULL
            BEGIN
                ALTER TABLE [Forms]
                ADD [PublicationEditMode] INT NOT NULL
                    CONSTRAINT [DF_Forms_PublicationEditMode] DEFAULT (0);
            END;

            IF OBJECT_ID(N'Entries', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Entries_FormId_Status_SubmittedByEmail_SubmittedUtc'
                      AND object_id = OBJECT_ID(N'Entries', N'U'))
            BEGIN
                CREATE INDEX [IX_Entries_FormId_Status_SubmittedByEmail_SubmittedUtc]
                    ON [Entries] ([FormId], [Status], [SubmittedByEmail], [SubmittedUtc]);
            END;

            IF OBJECT_ID(N'ApprovalAuditEvents', N'U') IS NULL
               AND OBJECT_ID(N'Entries', N'U') IS NOT NULL
            BEGIN
                CREATE TABLE [ApprovalAuditEvents] (
                    [Id] UNIQUEIDENTIFIER NOT NULL,
                    [EntryId] UNIQUEIDENTIFIER NOT NULL,
                    [Action] INT NOT NULL,
                    [ApprovalStepId] UNIQUEIDENTIFIER NULL,
                    [ActorUserId] UNIQUEIDENTIFIER NOT NULL,
                    [ActorDisplayName] NVARCHAR(256) NOT NULL,
                    [Signature] NVARCHAR(1024) NULL,
                    [Reason] NVARCHAR(2000) NULL,
                    [CorrelationId] NVARCHAR(128) NULL,
                    [OccurredUtc] DATETIMEOFFSET NOT NULL,
                    CONSTRAINT [PK_ApprovalAuditEvents] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ApprovalAuditEvents_Entries_EntryId] FOREIGN KEY ([EntryId]) REFERENCES [Entries]([Id]) ON DELETE CASCADE
                );

                CREATE INDEX [IX_ApprovalAuditEvents_EntryId_OccurredUtc] ON [ApprovalAuditEvents] ([EntryId], [OccurredUtc]);
                CREATE INDEX [IX_ApprovalAuditEvents_EntryId_Action] ON [ApprovalAuditEvents] ([EntryId], [Action]);
                CREATE INDEX [IX_ApprovalAuditEvents_CorrelationId] ON [ApprovalAuditEvents] ([CorrelationId]);
            END;

            IF OBJECT_ID(N'ApprovalAuditEvents', N'U') IS NOT NULL
               AND COL_LENGTH(N'ApprovalAuditEvents', N'CorrelationId') IS NULL
            BEGIN
                ALTER TABLE [ApprovalAuditEvents]
                ADD [CorrelationId] NVARCHAR(128) NULL;
            END;

            IF OBJECT_ID(N'ApprovalAuditEvents', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_ApprovalAuditEvents_CorrelationId'
                      AND object_id = OBJECT_ID(N'ApprovalAuditEvents', N'U'))
            BEGIN
                CREATE INDEX [IX_ApprovalAuditEvents_CorrelationId] ON [ApprovalAuditEvents] ([CorrelationId]);
            END;
            """,
            cancellationToken);
    }

    public async Task<IReadOnlyList<FormAggregate>> GetFormsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await db.Forms
            .Include(f => f.Versions)
            .Include(f => f.Permissions)
            .Include(f => f.Notifications)
            .Include(f => f.Webhooks)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var results = new List<FormAggregate>(entities.Count);
        foreach (var entity in entities)
        {
            results.Add(ToAggregate(entity));
        }

        return results;
    }

    public async Task<FormAggregate?> GetFormAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        var entity = await db.Forms
            .Include(f => f.Versions)
            .Include(f => f.Permissions)
            .Include(f => f.Notifications)
            .Include(f => f.Webhooks)
            .AsSplitQuery()
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
            .Include(f => f.Webhooks)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.PublicationSlug == slug, cancellationToken);
        return entity is null ? null : ToAggregate(entity);
    }

    public async Task SaveFormAsync(FormAggregate form, CancellationToken cancellationToken = default)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var existing = await db.Forms
            .Include(f => f.Versions)
            .Include(f => f.Permissions)
            .Include(f => f.Notifications)
            .Include(f => f.Webhooks)
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
        existing.PublicationEditMode = (int)form.Publication.EditMode;
        existing.PublicationOpenUtc = form.Publication.OpenUtc;
        existing.PublicationCloseUtc = form.Publication.CloseUtc;
        existing.PublicationNotYetOpenMessage = form.Publication.NotYetOpenMessage;
        existing.PublicationClosedMessage = form.Publication.ClosedMessage;
        existing.PublicationMaxSubmissions = form.Publication.MaxSubmissions;
        existing.PublicationCapReachedMessage = form.Publication.CapReachedMessage;
        existing.PublicationConfirmationMessage = form.Publication.ConfirmationMessage;
        existing.PublicationConfirmationRedirectUrl = form.Publication.ConfirmationRedirectUrl;
        existing.PublicationAccessPasswordHash = form.Publication.AccessPasswordHash;
        existing.PublicationRequireCaptcha = form.Publication.RequireCaptcha;
        existing.PublicationAutoSaveIntervalSeconds = form.Publication.AutoSaveIntervalSeconds;

        // Form versions are immutable snapshots; append new ones only.
        var existingVersionIds = existing.Versions.Select(v => v.Id).ToHashSet();
        foreach (var v in form.Versions)
        {
            if (existingVersionIds.Contains(v.Id))
            {
                continue;
            }

            db.FormVersions.Add(new FormVersionEntity
            {
                Id = v.Id,
                FormId = existing.Id,
                VersionNumber = v.VersionNumber,
                CreatedUtc = v.CreatedUtc,
                DefinitionJson = v.DefinitionJson
            });
        }

        var desiredPermissionsByUserId = form.Permissions
            .Where(p => p.UserId != Guid.Empty)
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var existingPermission in existing.Permissions.ToList())
        {
            if (!desiredPermissionsByUserId.ContainsKey(existingPermission.UserId))
            {
                db.FormPermissions.Remove(existingPermission);
            }
        }

        foreach (var desired in desiredPermissionsByUserId.Values)
        {
            var match = existing.Permissions.FirstOrDefault(p => p.UserId == desired.UserId);
            if (match is null)
            {
                existing.Permissions.Add(new FormPermissionEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = desired.UserId,
                    DisplayName = desired.DisplayName,
                    Role = (int)desired.Role,
                    ScopeType = string.IsNullOrWhiteSpace(desired.ScopeType) ? "Form" : desired.ScopeType,
                    ScopeValue = desired.ScopeValue,
                    UpdatedByUserId = form.OwnerUserId,
                    UpdatedUtc = DateTimeOffset.UtcNow
                });
                continue;
            }

            match.DisplayName = desired.DisplayName;
            match.Role = (int)desired.Role;
            match.ScopeType = string.IsNullOrWhiteSpace(desired.ScopeType) ? "Form" : desired.ScopeType;
            match.ScopeValue = desired.ScopeValue;
            match.UpdatedByUserId = form.OwnerUserId;
            match.UpdatedUtc = DateTimeOffset.UtcNow;
        }

        var desiredNotificationsByEmail = form.Notifications
            .Where(n => !string.IsNullOrWhiteSpace(n.Email))
            .GroupBy(n => n.Email.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var existingNotification in existing.Notifications.ToList())
        {
            if (!desiredNotificationsByEmail.ContainsKey(existingNotification.Email))
            {
                db.FormNotifications.Remove(existingNotification);
            }
        }

        foreach (var desired in desiredNotificationsByEmail.Values)
        {
            var match = existing.Notifications.FirstOrDefault(n =>
                string.Equals(n.Email, desired.Email, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                existing.Notifications.Add(new FormNotificationEntity
                {
                    Id = Guid.NewGuid(),
                    Email = desired.Email.Trim(),
                    OnSubmission = desired.OnSubmission,
                    OnApproval = desired.OnApproval
                });
                continue;
            }

            match.OnSubmission = desired.OnSubmission;
            match.OnApproval = desired.OnApproval;
        }

        var desiredWebhooksById = form.Webhooks.Where(w => !string.IsNullOrWhiteSpace(w.Url)).ToDictionary(w => w.Id);
        foreach (var existingWebhook in existing.Webhooks.ToList())
        {
            if (!desiredWebhooksById.ContainsKey(existingWebhook.Id))
            {
                db.FormWebhooks.Remove(existingWebhook);
            }
        }

        foreach (var desired in desiredWebhooksById.Values)
        {
            var match = existing.Webhooks.FirstOrDefault(w => w.Id == desired.Id);
            var triggerEventsJson = JsonSerializer.Serialize(desired.TriggerEvents);
            var headersJson = JsonSerializer.Serialize(desired.Headers);

            if (match is null)
            {
                existing.Webhooks.Add(new FormWebhookEntity
                {
                    Id = desired.Id,
                    FormId = existing.Id,
                    Url = desired.Url,
                    Secret = desired.Secret,
                    TriggerEventsJson = triggerEventsJson,
                    HeadersJson = headersJson,
                    IsEnabled = desired.IsEnabled
                });
                continue;
            }

            match.Url = desired.Url;
            match.Secret = desired.Secret;
            match.TriggerEventsJson = triggerEventsJson;
            match.HeadersJson = headersJson;
            match.IsEnabled = desired.IsEnabled;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "Concurrency conflict while saving form {FormId}.", form.Id);
            throw new InvalidOperationException("The form was updated by another user. Reload and retry.", ex);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Database update failed while saving form {FormId}.", form.Id);
            throw new InvalidOperationException("The form could not be saved due to a database update failure.", ex);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EntryRecord>> GetEntriesAsync(Guid? formId, string? search, CancellationToken cancellationToken = default)
    {
        return await QueryEntriesAsync(new EntryQueryOptions
        {
            FormId = formId,
            Search = search
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<EntryRecord>> QueryEntriesAsync(EntryQueryOptions options, CancellationToken cancellationToken = default)
    {
        var q = db.Entries
            .Include(e => e.Revisions)
            .Include(e => e.ApprovalSteps)
            .Include(e => e.Files)
            .Include(e => e.SearchIndexEntries)
            .AsSplitQuery()
            .AsNoTracking();

        if (options.FormId.HasValue)
            q = q.Where(e => e.FormId == options.FormId.Value);

        if (options.Status.HasValue)
            q = q.Where(e => e.Status == (int)options.Status.Value);

        if (options.SubmittedFromUtc.HasValue)
            q = q.Where(e => e.SubmittedUtc >= options.SubmittedFromUtc.Value);

        if (options.SubmittedToUtc.HasValue)
            q = q.Where(e => e.SubmittedUtc <= options.SubmittedToUtc.Value);

        if (!string.IsNullOrWhiteSpace(options.IndexedFieldId) && !string.IsNullOrWhiteSpace(options.IndexedFieldValue))
        {
            var indexedFieldId = options.IndexedFieldId.Trim();
            var indexedFieldValue = options.IndexedFieldValue.Trim();
            q = q.Where(e => e.SearchIndexEntries.Any(s => s.Key == indexedFieldId && s.Value.Contains(indexedFieldValue)));
        }

        if (!string.IsNullOrWhiteSpace(options.Search))
        {
            var searchTerm = options.Search.Trim();
            q = q.Where(e =>
                e.SubmittedBy.Contains(searchTerm) ||
                e.SearchIndexEntries.Any(s => s.Value.Contains(searchTerm)));
        }

        q = q.OrderByDescending(e => e.SubmittedUtc);

        if (options.Offset > 0)
        {
            q = q.Skip(options.Offset);
        }

        if (options.Limit > 0)
        {
            q = q.Take(options.Limit);
        }

        var list = await q.ToListAsync(cancellationToken);
        var records = new List<EntryRecord>(list.Count);
        foreach (var entity in list)
        {
            records.Add(ToEntryRecord(entity));
        }

        return records;
    }

    public async Task<EntryRecord?> GetEntryAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        var entity = await db.Entries
            .Include(e => e.Revisions)
            .Include(e => e.ApprovalSteps)
            .Include(e => e.ApprovalAuditTrail)
            .Include(e => e.Files)
            .Include(e => e.SearchIndexEntries)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == entryId, cancellationToken);
        return entity is null ? null : ToEntryRecord(entity);
    }

    public async Task<EntryRecord?> GetDraftEntryAsync(Guid formId, string submittedByEmail, CancellationToken cancellationToken = default)
    {
        var entity = await db.Entries
            .Include(e => e.Revisions)
            .Include(e => e.ApprovalSteps)
            .Include(e => e.ApprovalAuditTrail)
            .Include(e => e.Files)
            .Include(e => e.SearchIndexEntries)
            .AsSplitQuery()
            .AsNoTracking()
            .Where(e => e.FormId == formId && e.Status == (int)EntryStatus.Draft)
            .OrderByDescending(e => e.SubmittedUtc)
            .FirstOrDefaultAsync(e => e.SubmittedByEmail == submittedByEmail, cancellationToken);

        return entity is null ? null : ToEntryRecord(entity);
    }

    public async Task SaveEntryAsync(EntryRecord entry, CancellationToken cancellationToken = default)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var existing = await db.Entries
            .Include(e => e.Revisions)
            .Include(e => e.ApprovalSteps)
            .Include(e => e.Files)
            .Include(e => e.SearchIndexEntries)
            .AsSplitQuery()
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
        existing.StartedUtc = entry.StartedUtc;
        existing.Status = (int)entry.Status;
        existing.Answers = new Dictionary<string, string?>(entry.Answers, StringComparer.OrdinalIgnoreCase);
        existing.SearchIndex = new Dictionary<string, string>(entry.SearchIndex, StringComparer.OrdinalIgnoreCase);
        existing.Score = entry.Score;
        existing.QuizPassed = entry.QuizPassed;

        var existingRevisionIds = existing.Revisions.Select(r => r.Id).ToHashSet();
        foreach (var r in entry.Revisions)
        {
            if (existingRevisionIds.Contains(r.Id))
            {
                continue;
            }

            db.EntryRevisions.Add(new EntryRevisionEntity
            {
                Id = r.Id,
                EntryId = existing.Id,
                RevisionNumber = r.RevisionNumber,
                EditedBy = r.EditedBy,
                EditedUtc = r.EditedUtc,
                Answers = new Dictionary<string, string?>(r.Answers, StringComparer.OrdinalIgnoreCase)
            });
        }

        var approvalStepsById = existing.ApprovalSteps.ToDictionary(a => a.Id);
        foreach (var a in entry.ApprovalSteps)
        {
            if (!approvalStepsById.TryGetValue(a.Id, out var existingStep))
            {
                existing.ApprovalSteps.Add(new ApprovalStepEntity
                {
                    Id = a.Id,
                    Order = a.Order,
                    ApproverId = a.ApproverId,
                    ApproverName = a.ApproverName,
                    ApproverEmail = a.ApproverEmail,
                    AcceptorMode = (int)a.AcceptorMode,
                    AcceptorsJson = JsonSerializer.Serialize(a.Acceptors),
                    Instructions = a.Instructions,
                    Status = (int)a.Status,
                    Signature = a.Signature,
                    RejectionReason = a.RejectionReason,
                    CompletedUtc = a.CompletedUtc,
                    DelegatedToEmail = a.DelegatedToEmail,
                    DelegatedToName = a.DelegatedToName,
                    DelegatedUtc = a.DelegatedUtc
                });
                continue;
            }

            existingStep.Order = a.Order;
            existingStep.ApproverId = a.ApproverId;
            existingStep.ApproverName = a.ApproverName;
            existingStep.ApproverEmail = a.ApproverEmail;
            existingStep.AcceptorMode = (int)a.AcceptorMode;
            existingStep.AcceptorsJson = JsonSerializer.Serialize(a.Acceptors);
            existingStep.Instructions = a.Instructions;
            existingStep.Status = (int)a.Status;
            existingStep.Signature = a.Signature;
            existingStep.RejectionReason = a.RejectionReason;
            existingStep.CompletedUtc = a.CompletedUtc;
            existingStep.DelegatedToEmail = a.DelegatedToEmail;
            existingStep.DelegatedToName = a.DelegatedToName;
            existingStep.DelegatedUtc = a.DelegatedUtc;
        }

        foreach (var existingStep in existing.ApprovalSteps.ToList())
        {
            if (entry.ApprovalSteps.All(a => a.Id != existingStep.Id))
            {
                db.ApprovalSteps.Remove(existingStep);
            }
        }

        var existingAuditIds = await db.ApprovalAuditEvents
            .AsNoTracking()
            .Where(a => a.EntryId == existing.Id)
            .Select(a => a.Id)
            .ToHashSetAsync(cancellationToken);
        foreach (var audit in entry.ApprovalAuditTrail.OrderBy(a => a.OccurredUtc))
        {
            if (existingAuditIds.Contains(audit.Id))
            {
                continue;
            }

            db.ApprovalAuditEvents.Add(new ApprovalAuditEventEntity
            {
                Id = audit.Id,
                EntryId = existing.Id,
                Action = (int)audit.Action,
                ApprovalStepId = audit.ApprovalStepId,
                ActorUserId = audit.ActorUserId,
                ActorDisplayName = audit.ActorDisplayName,
                Signature = audit.Signature,
                Reason = audit.Reason,
                CorrelationId = audit.CorrelationId,
                OccurredUtc = audit.OccurredUtc
            });
        }

        var existingSearchByKey = existing.SearchIndexEntries.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in entry.SearchIndex)
        {
            if (existingSearchByKey.TryGetValue(kv.Key, out var searchEntity))
            {
                searchEntity.Value = kv.Value ?? string.Empty;
                continue;
            }

            existing.SearchIndexEntries.Add(new EntrySearchIndexEntity
            {
                Id = Guid.NewGuid(),
                Key = kv.Key,
                Value = kv.Value ?? string.Empty
            });
        }

        foreach (var existingSearch in existing.SearchIndexEntries.ToList())
        {
            if (!entry.SearchIndex.ContainsKey(existingSearch.Key))
            {
                db.EntrySearchIndex.Remove(existingSearch);
            }
        }

        var filesById = existing.Files.ToDictionary(f => f.Id);
        foreach (var file in entry.Files)
        {
            if (!filesById.TryGetValue(file.Id, out var existingFile))
            {
                existing.Files.Add(new EntryFileMetadataEntity
                {
                    Id = file.Id,
                    FieldId = file.FieldId,
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    Length = file.Length,
                    RelativePath = file.RelativePath,
                    Sha256 = file.Sha256,
                    UploadedByUserId = file.UploadedByUserId,
                    UploadedByEmail = file.UploadedByEmail,
                    RevisionNumber = file.RevisionNumber,
                    UploadedUtc = file.UploadedUtc
                });
                continue;
            }

            existingFile.FieldId = file.FieldId;
            existingFile.FileName = file.FileName;
            existingFile.ContentType = file.ContentType;
            existingFile.Length = file.Length;
            existingFile.RelativePath = file.RelativePath;
            existingFile.Sha256 = file.Sha256;
            existingFile.UploadedByUserId = file.UploadedByUserId;
            existingFile.UploadedByEmail = file.UploadedByEmail;
            existingFile.RevisionNumber = file.RevisionNumber;
            existingFile.UploadedUtc = file.UploadedUtc;
        }

        foreach (var existingFile in existing.Files.ToList())
        {
            if (entry.Files.All(f => f.Id != existingFile.Id))
            {
                db.EntryFiles.Remove(existingFile);
            }
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "Concurrency conflict while saving entry {EntryId}.", entry.Id);
            throw new InvalidOperationException("The entry was updated by another user. Reload and retry.", ex);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Database update failed while saving entry {EntryId}.", entry.Id);
            throw new InvalidOperationException("The entry could not be saved due to a database update failure.", ex);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EntryFileRecord>> GetOrphanedFilesAsync(DateTimeOffset olderThanUtc, CancellationToken cancellationToken = default)
    {
        var files = await db.EntryFiles
            .AsNoTracking()
            .Where(file => file.Entry != null && file.Entry.Status == (int)EntryStatus.Draft && file.Entry.SubmittedUtc < olderThanUtc)
            .OrderBy(file => file.UploadedUtc)
            .Select(file => new EntryFileRecord
            {
                Id = file.Id,
                FieldId = file.FieldId,
                FileName = file.FileName,
                ContentType = file.ContentType,
                Length = file.Length,
                RelativePath = file.RelativePath,
                Sha256 = file.Sha256,
                UploadedByUserId = file.UploadedByUserId,
                UploadedByEmail = file.UploadedByEmail,
                RevisionNumber = file.RevisionNumber,
                UploadedUtc = file.UploadedUtc
            })
            .ToListAsync(cancellationToken);

        return files;
    }

    public async Task<int> DeleteDraftEntriesOlderThanAsync(DateTimeOffset olderThanUtc, CancellationToken cancellationToken = default)
    {
        var staleDrafts = await db.Entries
            .Where(entry => entry.Status == (int)EntryStatus.Draft && entry.SubmittedUtc < olderThanUtc)
            .ToListAsync(cancellationToken);

        if (staleDrafts.Count == 0)
        {
            return 0;
        }

        db.Entries.RemoveRange(staleDrafts);
        await db.SaveChangesAsync(cancellationToken);
        return staleDrafts.Count;
    }

    public async Task<IReadOnlyList<FormInvitation>> GetInvitationsAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        var query = db.FormInvitations.AsNoTracking();
        if (formId != Guid.Empty)
        {
            query = query.Where(i => i.FormId == formId);
        }

        var invitations = await query.OrderByDescending(i => i.CreatedUtc).ToListAsync(cancellationToken);
        return invitations.Select(ToInvitation).ToList();
    }

    public async Task<FormInvitation?> GetInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        var invitation = await db.FormInvitations.AsNoTracking().FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken);
        return invitation is null ? null : ToInvitation(invitation);
    }

    public async Task<FormInvitation?> GetInvitationByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var invitation = await db.FormInvitations.AsNoTracking().FirstOrDefaultAsync(i => i.Token == token, cancellationToken);
        return invitation is null ? null : ToInvitation(invitation);
    }

    public async Task SaveInvitationAsync(FormInvitation invitation, CancellationToken cancellationToken = default)
    {
        var existing = await db.FormInvitations.FirstOrDefaultAsync(i => i.Id == invitation.Id, cancellationToken);
        if (existing is null)
        {
            existing = new FormInvitationEntity
            {
                Id = invitation.Id
            };
            db.FormInvitations.Add(existing);
        }

        existing.FormId = invitation.FormId;
        existing.Email = invitation.Email;
        existing.Role = (int)invitation.Role;
        existing.ScopeType = invitation.ScopeType;
        existing.ScopeValue = invitation.ScopeValue;
        existing.Token = invitation.Token;
        existing.ExpiresUtc = invitation.ExpiresUtc;
        existing.Status = (int)invitation.Status;
        existing.CreatedByUserId = invitation.CreatedByUserId;
        existing.CreatedUtc = invitation.CreatedUtc;
        existing.UpdatedByUserId = invitation.UpdatedByUserId;
        existing.UpdatedUtc = invitation.UpdatedUtc;

        await db.SaveChangesAsync(cancellationToken);
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
                SendSubmissionCopyToSubmitter = entity.PublicationSendSubmissionCopyToSubmitter,
                EditMode = entity.PublicationEditMode == 0
                    ? SubmissionEditMode.ImmutableRevisions
                    : (SubmissionEditMode)entity.PublicationEditMode,
                OpenUtc = entity.PublicationOpenUtc,
                CloseUtc = entity.PublicationCloseUtc,
                NotYetOpenMessage = entity.PublicationNotYetOpenMessage,
                ClosedMessage = entity.PublicationClosedMessage,
                MaxSubmissions = entity.PublicationMaxSubmissions,
                CapReachedMessage = entity.PublicationCapReachedMessage,
                ConfirmationMessage = entity.PublicationConfirmationMessage,
                ConfirmationRedirectUrl = entity.PublicationConfirmationRedirectUrl,
                AccessPasswordHash = entity.PublicationAccessPasswordHash,
                RequireCaptcha = entity.PublicationRequireCaptcha,
                AutoSaveIntervalSeconds = entity.PublicationAutoSaveIntervalSeconds
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
                Role = (FormPermissionRole)p.Role,
                ScopeType = p.ScopeType,
                ScopeValue = p.ScopeValue
            }).ToList(),
            Notifications = entity.Notifications.Select(n => new FormNotificationRule
            {
                Email = n.Email,
                OnSubmission = n.OnSubmission,
                OnApproval = n.OnApproval
            }).ToList(),
            Webhooks = entity.Webhooks.Select(w => new FormWebhookDefinition
            {
                Id = w.Id,
                Url = w.Url,
                Secret = w.Secret,
                TriggerEvents = string.IsNullOrWhiteSpace(w.TriggerEventsJson)
                    ? [WebhookTriggerEvent.EntrySubmitted]
                    : (JsonSerializer.Deserialize<List<WebhookTriggerEvent>>(w.TriggerEventsJson) ?? [WebhookTriggerEvent.EntrySubmitted]),
                Headers = string.IsNullOrWhiteSpace(w.HeadersJson)
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : (JsonSerializer.Deserialize<Dictionary<string, string>>(w.HeadersJson) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
                IsEnabled = w.IsEnabled
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
            StartedUtc = entity.StartedUtc,
            Status = (EntryStatus)entity.Status,
            Answers = new Dictionary<string, string?>(entity.Answers, StringComparer.OrdinalIgnoreCase),
            SearchIndex = new Dictionary<string, string>(entity.SearchIndex, StringComparer.OrdinalIgnoreCase),
            Score = entity.Score,
            QuizPassed = entity.QuizPassed,
            Files = entity.Files.Select(f => new EntryFileRecord
            {
                Id = f.Id,
                FieldId = f.FieldId,
                FileName = f.FileName,
                ContentType = f.ContentType,
                Length = f.Length,
                RelativePath = f.RelativePath,
                Sha256 = f.Sha256,
                UploadedByUserId = f.UploadedByUserId,
                UploadedByEmail = f.UploadedByEmail,
                RevisionNumber = f.RevisionNumber,
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
                ApproverId = a.ApproverId,
                ApproverName = a.ApproverName,
                ApproverEmail = a.ApproverEmail,
                AcceptorMode = (ApprovalStepAcceptorMode)a.AcceptorMode,
                Acceptors = string.IsNullOrWhiteSpace(a.AcceptorsJson)
                    ? []
                    : (JsonSerializer.Deserialize<List<ApprovalAcceptor>>(a.AcceptorsJson) ?? []),
                Instructions = a.Instructions,
                Status = (ApprovalStepStatus)a.Status,
                Signature = a.Signature,
                RejectionReason = a.RejectionReason,
                CompletedUtc = a.CompletedUtc,
                DelegatedToEmail = a.DelegatedToEmail,
                DelegatedToName = a.DelegatedToName,
                DelegatedUtc = a.DelegatedUtc
            }).ToList(),
            ApprovalAuditTrail = entity.ApprovalAuditTrail
                .OrderBy(a => a.OccurredUtc)
                .Select(a => new ApprovalAuditEvent
                {
                    Id = a.Id,
                    Action = (ApprovalAuditAction)a.Action,
                    ApprovalStepId = a.ApprovalStepId,
                ActorUserId = a.ActorUserId,
                ActorDisplayName = a.ActorDisplayName,
                Signature = a.Signature,
                Reason = a.Reason,
                CorrelationId = a.CorrelationId,
                OccurredUtc = a.OccurredUtc
            }).ToList()
        };
    }

    private static FormInvitation ToInvitation(FormInvitationEntity entity)
    {
        return new FormInvitation
        {
            Id = entity.Id,
            FormId = entity.FormId,
            Email = entity.Email,
            Role = (FormPermissionRole)entity.Role,
            ScopeType = entity.ScopeType,
            ScopeValue = entity.ScopeValue,
            Token = entity.Token,
            ExpiresUtc = entity.ExpiresUtc,
            Status = (InvitationStatus)entity.Status,
            CreatedByUserId = entity.CreatedByUserId,
            CreatedUtc = entity.CreatedUtc,
            UpdatedByUserId = entity.UpdatedByUserId,
            UpdatedUtc = entity.UpdatedUtc
        };
    }
}
