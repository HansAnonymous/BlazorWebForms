namespace BlazorWebForms.Infrastructure.SqlServer;

public sealed class SqlServerSchemaDescriptor(BlazorWebFormsSqlServerOptions options)
{
    public string GetCreateScript() =>
        $"""
        IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'{options.SchemaName}')
        BEGIN
            EXEC('CREATE SCHEMA [{options.SchemaName}]');
        END;

        CREATE TABLE [{options.SchemaName}].[Forms] (
            [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            [Key] NVARCHAR(MAX) NOT NULL,
            [Name] NVARCHAR(MAX) NOT NULL,
            [Description] NVARCHAR(2000) NOT NULL,
            [OwnerUserId] UNIQUEIDENTIFIER NOT NULL,
            [CreatedUtc] DATETIMEOFFSET NOT NULL,
            [UpdatedUtc] DATETIMEOFFSET NOT NULL,
            [DraftDefinitionJson] NVARCHAR(MAX) NULL,
            [PublicationSlug] NVARCHAR(MAX) NOT NULL,
            [PublicationDomain] NVARCHAR(MAX) NOT NULL,
            [PublicationAccessMode] INT NOT NULL,
            [PublicationSendSubmissionCopyToSubmitter] BIT NOT NULL,
            [PublicationEditMode] INT NOT NULL CONSTRAINT [DF_Forms_PublicationEditMode] DEFAULT(0),
            [PublicationOpenUtc] DATETIMEOFFSET NULL,
            [PublicationCloseUtc] DATETIMEOFFSET NULL,
            [PublicationNotYetOpenMessage] NVARCHAR(2000) NOT NULL CONSTRAINT [DF_Forms_PublicationNotYetOpenMessage] DEFAULT(N''),
            [PublicationClosedMessage] NVARCHAR(2000) NOT NULL CONSTRAINT [DF_Forms_PublicationClosedMessage] DEFAULT(N''),
            [PublicationMaxSubmissions] INT NULL,
            [PublicationCapReachedMessage] NVARCHAR(2000) NOT NULL CONSTRAINT [DF_Forms_PublicationCapReachedMessage] DEFAULT(N''),
            [PublicationConfirmationMessage] NVARCHAR(4000) NOT NULL CONSTRAINT [DF_Forms_PublicationConfirmationMessage] DEFAULT(N''),
            [PublicationConfirmationRedirectUrl] NVARCHAR(2048) NOT NULL CONSTRAINT [DF_Forms_PublicationConfirmationRedirectUrl] DEFAULT(N''),
            [PublicationAccessPasswordHash] NVARCHAR(256) NOT NULL CONSTRAINT [DF_Forms_PublicationAccessPasswordHash] DEFAULT(N''),
            [PublicationRequireCaptcha] BIT NOT NULL CONSTRAINT [DF_Forms_PublicationRequireCaptcha] DEFAULT(0),
            [PublicationAutoSaveIntervalSeconds] INT NULL,
            [RowVersion] ROWVERSION NOT NULL
        );

        CREATE TABLE [{options.SchemaName}].[FormVersions] (
            [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            [FormId] UNIQUEIDENTIFIER NOT NULL,
            [VersionNumber] INT NOT NULL,
            [DefinitionJson] NVARCHAR(MAX) NOT NULL,
            [CreatedUtc] DATETIMEOFFSET NOT NULL,
            CONSTRAINT [FK_FormVersions_Forms_FormId] FOREIGN KEY ([FormId]) REFERENCES [{options.SchemaName}].[Forms]([Id]) ON DELETE CASCADE
        );

        CREATE TABLE [{options.SchemaName}].[Entries] (
            [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            [FormId] UNIQUEIDENTIFIER NOT NULL,
            [FormVersionId] UNIQUEIDENTIFIER NOT NULL,
            [SubmittedBy] NVARCHAR(256) NOT NULL,
            [SubmittedByEmail] NVARCHAR(256) NOT NULL,
            [SubmittedUtc] DATETIMEOFFSET NOT NULL,
            [StartedUtc] DATETIMEOFFSET NULL,
            [Status] INT NOT NULL,
            [Answers] NVARCHAR(MAX) NOT NULL,
            [SearchIndex] NVARCHAR(MAX) NOT NULL,
            [Score] DECIMAL(18,2) NULL,
            [QuizPassed] BIT NULL,
            [RowVersion] ROWVERSION NOT NULL,
            CONSTRAINT [FK_Entries_Forms_FormId] FOREIGN KEY ([FormId]) REFERENCES [{options.SchemaName}].[Forms]([Id]) ON DELETE CASCADE
        );

        CREATE TABLE [{options.SchemaName}].[EntryRevisions] (
            [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            [EntryId] UNIQUEIDENTIFIER NOT NULL,
            [RevisionNumber] INT NOT NULL,
            [EditedBy] NVARCHAR(MAX) NOT NULL,
            [EditedUtc] DATETIMEOFFSET NOT NULL,
            [Answers] NVARCHAR(MAX) NOT NULL,
            CONSTRAINT [FK_EntryRevisions_Entries_EntryId] FOREIGN KEY ([EntryId]) REFERENCES [{options.SchemaName}].[Entries]([Id]) ON DELETE CASCADE
        );

        CREATE TABLE [{options.SchemaName}].[ApprovalSteps] (
            [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            [EntryId] UNIQUEIDENTIFIER NOT NULL,
            [Order] INT NOT NULL,
            [ApproverId] NVARCHAR(256) NOT NULL,
            [ApproverName] NVARCHAR(MAX) NOT NULL,
            [ApproverEmail] NVARCHAR(MAX) NOT NULL,
            [AcceptorMode] INT NOT NULL CONSTRAINT [DF_ApprovalSteps_AcceptorMode] DEFAULT(0),
            [AcceptorsJson] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_ApprovalSteps_AcceptorsJson] DEFAULT(N'[]'),
            [Instructions] NVARCHAR(4000) NOT NULL CONSTRAINT [DF_ApprovalSteps_Instructions] DEFAULT(N''),
            [Status] INT NOT NULL,
            [Signature] NVARCHAR(MAX) NULL,
            [RejectionReason] NVARCHAR(2000) NULL,
            [CompletedUtc] DATETIMEOFFSET NULL,
            [DelegatedToEmail] NVARCHAR(256) NULL,
            [DelegatedToName] NVARCHAR(256) NULL,
            [DelegatedUtc] DATETIMEOFFSET NULL,
            CONSTRAINT [FK_ApprovalSteps_Entries_EntryId] FOREIGN KEY ([EntryId]) REFERENCES [{options.SchemaName}].[Entries]([Id]) ON DELETE CASCADE
        );

        CREATE TABLE [{options.SchemaName}].[EntryFiles] (
            [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            [EntryId] UNIQUEIDENTIFIER NOT NULL,
            [FieldId] NVARCHAR(128) NOT NULL,
            [FileName] NVARCHAR(260) NOT NULL,
            [ContentType] NVARCHAR(128) NOT NULL,
            [RelativePath] NVARCHAR(512) NOT NULL,
            [Length] BIGINT NOT NULL,
            [Sha256] NVARCHAR(64) NOT NULL,
            [UploadedByUserId] UNIQUEIDENTIFIER NOT NULL,
            [UploadedByEmail] NVARCHAR(256) NOT NULL,
            [RevisionNumber] INT NOT NULL,
            [UploadedUtc] DATETIMEOFFSET NOT NULL,
            CONSTRAINT [FK_EntryFiles_Entries_EntryId] FOREIGN KEY ([EntryId]) REFERENCES [{options.SchemaName}].[Entries]([Id]) ON DELETE CASCADE
        );

        CREATE TABLE [{options.SchemaName}].[EntrySearchIndex] (
            [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            [EntryId] UNIQUEIDENTIFIER NOT NULL,
            [Key] NVARCHAR(450) NOT NULL,
            [Value] NVARCHAR(450) NOT NULL,
            CONSTRAINT [FK_EntrySearchIndex_Entries_EntryId] FOREIGN KEY ([EntryId]) REFERENCES [{options.SchemaName}].[Entries]([Id]) ON DELETE CASCADE
        );

        CREATE TABLE [{options.SchemaName}].[ApprovalAuditEvents] (
            [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            [EntryId] UNIQUEIDENTIFIER NOT NULL,
            [Action] INT NOT NULL,
            [ApprovalStepId] UNIQUEIDENTIFIER NULL,
            [ActorUserId] UNIQUEIDENTIFIER NOT NULL,
            [ActorDisplayName] NVARCHAR(256) NOT NULL,
            [Signature] NVARCHAR(1024) NULL,
            [Reason] NVARCHAR(2000) NULL,
            [CorrelationId] NVARCHAR(128) NULL,
            [OccurredUtc] DATETIMEOFFSET NOT NULL,
            CONSTRAINT [FK_ApprovalAuditEvents_Entries_EntryId] FOREIGN KEY ([EntryId]) REFERENCES [{options.SchemaName}].[Entries]([Id]) ON DELETE CASCADE
        );

        CREATE TABLE [{options.SchemaName}].[FormPermissions] (
            [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            [FormId] UNIQUEIDENTIFIER NOT NULL,
            [UserId] UNIQUEIDENTIFIER NOT NULL,
            [DisplayName] NVARCHAR(MAX) NOT NULL,
            [Role] INT NOT NULL,
            [ScopeType] NVARCHAR(32) NOT NULL,
            [ScopeValue] NVARCHAR(128) NULL,
            [UpdatedByUserId] UNIQUEIDENTIFIER NOT NULL,
            [UpdatedUtc] DATETIMEOFFSET NOT NULL,
            CONSTRAINT [FK_FormPermissions_Forms_FormId] FOREIGN KEY ([FormId]) REFERENCES [{options.SchemaName}].[Forms]([Id]) ON DELETE CASCADE
        );

        CREATE TABLE [{options.SchemaName}].[FormNotifications] (
            [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            [FormId] UNIQUEIDENTIFIER NOT NULL,
            [Email] NVARCHAR(MAX) NOT NULL,
            [OnSubmission] BIT NOT NULL,
            [OnApproval] BIT NOT NULL,
            CONSTRAINT [FK_FormNotifications_Forms_FormId] FOREIGN KEY ([FormId]) REFERENCES [{options.SchemaName}].[Forms]([Id]) ON DELETE CASCADE
        );

        CREATE TABLE [{options.SchemaName}].[FormInvitations] (
            [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
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
            CONSTRAINT [FK_FormInvitations_Forms_FormId] FOREIGN KEY ([FormId]) REFERENCES [{options.SchemaName}].[Forms]([Id]) ON DELETE CASCADE
        );

        CREATE TABLE [{options.SchemaName}].[FormWebhooks] (
            [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            [FormId] UNIQUEIDENTIFIER NOT NULL,
            [Url] NVARCHAR(2048) NOT NULL,
            [Secret] NVARCHAR(256) NOT NULL CONSTRAINT [DF_FormWebhooks_Secret] DEFAULT(N''),
            [TriggerEventsJson] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_FormWebhooks_TriggerEventsJson] DEFAULT(N'[]'),
            [HeadersJson] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_FormWebhooks_HeadersJson] DEFAULT(NCHAR(123) + NCHAR(125)),
            [IsEnabled] BIT NOT NULL CONSTRAINT [DF_FormWebhooks_IsEnabled] DEFAULT(1),
            CONSTRAINT [FK_FormWebhooks_Forms_FormId] FOREIGN KEY ([FormId]) REFERENCES [{options.SchemaName}].[Forms]([Id]) ON DELETE CASCADE
        );

        CREATE TABLE [{options.SchemaName}].[FormAnalytics] (
            [FormId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            [ViewCount] BIGINT NOT NULL CONSTRAINT [DF_FormAnalytics_ViewCount] DEFAULT(0),
            [StartCount] BIGINT NOT NULL CONSTRAINT [DF_FormAnalytics_StartCount] DEFAULT(0),
            [SubmissionCount] BIGINT NOT NULL CONSTRAINT [DF_FormAnalytics_SubmissionCount] DEFAULT(0),
            [AbandonCount] BIGINT NOT NULL CONSTRAINT [DF_FormAnalytics_AbandonCount] DEFAULT(0),
            [AverageCompletionSeconds] FLOAT NOT NULL CONSTRAINT [DF_FormAnalytics_AverageCompletionSeconds] DEFAULT(0),
            [LastUpdatedUtc] DATETIMEOFFSET NULL,
            CONSTRAINT [FK_FormAnalytics_Forms_FormId] FOREIGN KEY ([FormId]) REFERENCES [{options.SchemaName}].[Forms]([Id]) ON DELETE CASCADE
        );

        CREATE UNIQUE INDEX [IX_Forms_Key] ON [{options.SchemaName}].[Forms] ([Key]);
        CREATE UNIQUE INDEX [IX_Forms_PublicationSlug] ON [{options.SchemaName}].[Forms] ([PublicationSlug]);
        CREATE INDEX [IX_Forms_UpdatedUtc] ON [{options.SchemaName}].[Forms] ([UpdatedUtc]);

        CREATE UNIQUE INDEX [IX_FormVersions_FormId_VersionNumber] ON [{options.SchemaName}].[FormVersions] ([FormId], [VersionNumber]);

        CREATE INDEX [IX_Entries_FormId] ON [{options.SchemaName}].[Entries] ([FormId]);
        CREATE INDEX [IX_Entries_FormId_SubmittedUtc] ON [{options.SchemaName}].[Entries] ([FormId], [SubmittedUtc]);
        CREATE INDEX [IX_Entries_FormId_Status_SubmittedUtc] ON [{options.SchemaName}].[Entries] ([FormId], [Status], [SubmittedUtc]);
        CREATE INDEX [IX_Entries_FormId_Status_SubmittedByEmail_SubmittedUtc] ON [{options.SchemaName}].[Entries] ([FormId], [Status], [SubmittedByEmail], [SubmittedUtc]);
        CREATE INDEX [IX_Entries_Status] ON [{options.SchemaName}].[Entries] ([Status]);
        CREATE INDEX [IX_Entries_SubmittedUtc] ON [{options.SchemaName}].[Entries] ([SubmittedUtc]);

        CREATE UNIQUE INDEX [IX_EntryRevisions_EntryId_RevisionNumber] ON [{options.SchemaName}].[EntryRevisions] ([EntryId], [RevisionNumber]);

        CREATE UNIQUE INDEX [IX_ApprovalSteps_EntryId_Order] ON [{options.SchemaName}].[ApprovalSteps] ([EntryId], [Order]);
        CREATE INDEX [IX_ApprovalSteps_Status] ON [{options.SchemaName}].[ApprovalSteps] ([Status]);

        CREATE INDEX [IX_ApprovalAuditEvents_EntryId_OccurredUtc] ON [{options.SchemaName}].[ApprovalAuditEvents] ([EntryId], [OccurredUtc]);
        CREATE INDEX [IX_ApprovalAuditEvents_EntryId_Action] ON [{options.SchemaName}].[ApprovalAuditEvents] ([EntryId], [Action]);
        CREATE INDEX [IX_ApprovalAuditEvents_CorrelationId] ON [{options.SchemaName}].[ApprovalAuditEvents] ([CorrelationId]);

        CREATE INDEX [IX_EntryFiles_EntryId] ON [{options.SchemaName}].[EntryFiles] ([EntryId]);
        CREATE INDEX [IX_EntryFiles_EntryId_FieldId] ON [{options.SchemaName}].[EntryFiles] ([EntryId], [FieldId]);
        CREATE UNIQUE INDEX [IX_EntryFiles_RelativePath] ON [{options.SchemaName}].[EntryFiles] ([RelativePath]);

        CREATE INDEX [IX_EntrySearchIndex_EntryId] ON [{options.SchemaName}].[EntrySearchIndex] ([EntryId]);
        CREATE INDEX [IX_EntrySearchIndex_EntryId_Key] ON [{options.SchemaName}].[EntrySearchIndex] ([EntryId], [Key]);
        CREATE INDEX [IX_EntrySearchIndex_Key_Value] ON [{options.SchemaName}].[EntrySearchIndex] ([Key], [Value]);

        CREATE UNIQUE INDEX [IX_FormPermissions_FormId_UserId] ON [{options.SchemaName}].[FormPermissions] ([FormId], [UserId]);
        CREATE INDEX [IX_FormPermissions_FormId_ScopeType_ScopeValue] ON [{options.SchemaName}].[FormPermissions] ([FormId], [ScopeType], [ScopeValue]);

        CREATE UNIQUE INDEX [IX_FormNotifications_FormId_Email] ON [{options.SchemaName}].[FormNotifications] ([FormId], [Email]);

        CREATE UNIQUE INDEX [IX_FormInvitations_Token] ON [{options.SchemaName}].[FormInvitations] ([Token]);
        CREATE INDEX [IX_FormInvitations_FormId_Email_Status] ON [{options.SchemaName}].[FormInvitations] ([FormId], [Email], [Status]);
        CREATE INDEX [IX_FormWebhooks_FormId_IsEnabled] ON [{options.SchemaName}].[FormWebhooks] ([FormId], [IsEnabled]);
        """;
}
