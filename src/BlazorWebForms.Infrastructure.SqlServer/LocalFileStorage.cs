using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Infrastructure.SqlServer;

internal sealed class LocalFileStorage(BlazorWebFormsSqlServerOptions options) : IFileStorage
{
    public async Task<StoredFile> SaveAsync(FileUploadRequest request, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(options.StorageRoot);

        var stored = new StoredFile
        {
            FileName = request.FileName,
            ContentType = request.ContentType,
            Length = request.Content.LongLength,
            RelativePath = Path.Combine(DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyyMMdd"), $"{Guid.NewGuid():N}-{request.FileName}")
        };

        var fullPath = Path.Combine(options.StorageRoot, stored.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, request.Content, cancellationToken);
        return stored;
    }
}
