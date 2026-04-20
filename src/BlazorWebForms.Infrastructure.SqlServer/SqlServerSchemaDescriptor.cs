namespace BlazorWebForms.Infrastructure.SqlServer;

public sealed class SqlServerSchemaDescriptor(BlazorWebFormsSqlServerOptions options)
{
    public string GetCreateScript() =>
        $"""
        CREATE SCHEMA [{options.SchemaName}];

        CREATE TABLE [{options.SchemaName}].[Forms] (
            [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            [FormKey] NVARCHAR(128) NOT NULL,
            [Name] NVARCHAR(256) NOT NULL,
            [Description] NVARCHAR(MAX) NOT NULL,
            [OwnerUserId] UNIQUEIDENTIFIER NOT NULL,
            [CreatedUtc] DATETIMEOFFSET NOT NULL,
            [UpdatedUtc] DATETIMEOFFSET NOT NULL
        );

        CREATE TABLE [{options.SchemaName}].[FormVersions] (
            [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            [FormId] UNIQUEIDENTIFIER NOT NULL,
            [VersionNumber] INT NOT NULL,
            [DefinitionJson] NVARCHAR(MAX) NOT NULL,
            [CreatedUtc] DATETIMEOFFSET NOT NULL
        );

        CREATE TABLE [{options.SchemaName}].[FormPublications] (
            [FormId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            [Slug] NVARCHAR(128) NOT NULL,
            [Domain] NVARCHAR(256) NOT NULL,
            [AccessMode] NVARCHAR(32) NOT NULL
        );

        CREATE TABLE [{options.SchemaName}].[Entries] (
            [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            [FormId] UNIQUEIDENTIFIER NOT NULL,
            [FormVersionId] UNIQUEIDENTIFIER NOT NULL,
            [SubmittedBy] NVARCHAR(256) NOT NULL,
            [SubmittedByEmail] NVARCHAR(256) NOT NULL,
            [SubmittedUtc] DATETIMEOFFSET NOT NULL,
            [Status] NVARCHAR(32) NOT NULL,
            [AnswersJson] NVARCHAR(MAX) NOT NULL
        );

        CREATE TABLE [{options.SchemaName}].[EntryRevisions] (
            [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            [EntryId] UNIQUEIDENTIFIER NOT NULL,
            [RevisionNumber] INT NOT NULL,
            [EditedBy] NVARCHAR(256) NOT NULL,
            [EditedUtc] DATETIMEOFFSET NOT NULL,
            [AnswersJson] NVARCHAR(MAX) NOT NULL
        );

        CREATE TABLE [{options.SchemaName}].[Approvals] (
            [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            [EntryId] UNIQUEIDENTIFIER NOT NULL,
            [ApprovalOrder] INT NOT NULL,
            [ApproverName] NVARCHAR(256) NOT NULL,
            [ApproverEmail] NVARCHAR(256) NOT NULL,
            [Status] NVARCHAR(32) NOT NULL,
            [Signature] NVARCHAR(MAX) NULL,
            [CompletedUtc] DATETIMEOFFSET NULL
        );

        CREATE TABLE [{options.SchemaName}].[Files] (
            [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            [FileName] NVARCHAR(260) NOT NULL,
            [ContentType] NVARCHAR(128) NOT NULL,
            [RelativePath] NVARCHAR(512) NOT NULL,
            [Length] BIGINT NOT NULL
        );

        CREATE TABLE [{options.SchemaName}].[Permissions] (
            [FormId] UNIQUEIDENTIFIER NOT NULL,
            [UserId] UNIQUEIDENTIFIER NOT NULL,
            [Role] NVARCHAR(32) NOT NULL
        );

        CREATE TABLE [{options.SchemaName}].[Notifications] (
            [FormId] UNIQUEIDENTIFIER NOT NULL,
            [Email] NVARCHAR(256) NOT NULL,
            [OnSubmission] BIT NOT NULL,
            [OnApproval] BIT NOT NULL
        );

        CREATE INDEX IX_Entries_FormId_SubmittedUtc ON [{options.SchemaName}].[Entries] ([FormId], [SubmittedUtc] DESC);
        CREATE INDEX IX_FormPublications_Slug ON [{options.SchemaName}].[FormPublications] ([Slug]);
        """;
}
