namespace BlazorWebForms.Infrastructure.SqlServer;

public sealed class BlazorWebFormsSqlServerOptions
{
    public string ConnectionString { get; set; } = "Server=(localdb)\\MSSQLLocalDB;Database=BlazorWebForms;";
    public string StorageRoot { get; set; } = Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads");
    public string SchemaName { get; set; } = "bwf";
    public bool EnableLocalFileStorage { get; set; } = true;
    public long DefaultMaxUploadBytes { get; set; } = 10 * 1024 * 1024;
    public int DefaultMaxFilesPerField { get; set; } = 1;
    public bool EnableDraftCleanup { get; set; } = true;
    public TimeSpan DraftRetentionPeriod { get; set; } = TimeSpan.FromDays(30);
    public bool RequireAdminForDraftCleanup { get; set; } = true;
    public bool EnableTextBasedPdfExporter { get; set; } = true;
    public int PdfMaxAnswerRows { get; set; } = 500;
    public int PdfMaxAuditRows { get; set; } = 250;
    public int PdfMaxBytes { get; set; } = 1_000_000;
    public bool EnableOutboundEmail { get; set; }
    public int EmailRetryCount { get; set; } = 3;
    public int EmailRetryDelayMs { get; set; } = 200;
    public bool EnableGraphIntegration { get; set; }
    public int MaxUploadsPerMinute { get; set; } = 30;
    public int MaxNotificationsPerMinute { get; set; } = 120;
    public string DataResidenceRegion { get; set; } = "local-dev";

    public string EmailProviderStrategy { get; set; } = "DryRun";
    public string EmailFromAddress { get; set; } = "noreply@example.com";
    public string EmailFromDisplayName { get; set; } = "BlazorWebForms";

    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool SmtpEnableSsl { get; set; } = true;
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }

    public string? SendGridApiKey { get; set; }

    public string GraphMailEndpoint { get; set; } = "https://graph.microsoft.com/v1.0/users/{sender}/sendMail";
    public string? GraphSender { get; set; }
    public string? GraphAccessToken { get; set; }
}
