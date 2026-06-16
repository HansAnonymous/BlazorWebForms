using BlazorWebForms.Core.Models;
using BlazorWebForms.Core.Services;

namespace BlazorWebForms.Infrastructure.SqlServer;

public sealed class DraftCleanupService(FormsApplicationService formsService, BlazorWebFormsSqlServerOptions options)
{
    public async Task<FileCleanupResult> CleanupAsync(CancellationToken cancellationToken = default)
    {
        if (!options.EnableDraftCleanup)
        {
            return new FileCleanupResult
            {
                DeletedDraftEntries = 0,
                DeletedFiles = 0
            };
        }

        if (options.DraftRetentionPeriod <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Draft retention period must be greater than zero.");
        }

        return await formsService.CleanupStaleDraftFilesAsync(options.DraftRetentionPeriod, cancellationToken);
    }
}
