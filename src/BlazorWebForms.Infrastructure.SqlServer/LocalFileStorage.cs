using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;
using System.Security.Cryptography;

namespace BlazorWebForms.Infrastructure.SqlServer;

internal sealed class LocalFileStorage(BlazorWebFormsSqlServerOptions options) : IFileStorage
{
    public async Task<StoredFile> SaveAsync(FileUploadRequest request, CancellationToken cancellationToken = default)
    {
        if (!options.EnableLocalFileStorage)
        {
            throw new InvalidOperationException("Local file storage is disabled in current environment.");
        }

        if (request.Content.LongLength <= 0)
        {
            throw new InvalidOperationException("Uploaded file content is empty.");
        }

        var maxAllowedBytes = ResolveMaxAllowedBytes(request.MaxAllowedBytes);
        if (request.Content.LongLength > maxAllowedBytes)
        {
            throw new InvalidOperationException($"Uploaded file exceeds configured limit of {maxAllowedBytes} bytes.");
        }

        var storageRoot = Path.GetFullPath(options.StorageRoot);
        Directory.CreateDirectory(storageRoot);

        var originalName = Path.GetFileName(request.FileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(originalName))
        {
            originalName = "upload.bin";
        }

        var extension = Path.GetExtension(originalName);
        var safeExtension = string.IsNullOrWhiteSpace(extension)
            ? ".bin"
            : new string(extension.Where(c => char.IsLetterOrDigit(c) || c == '.').ToArray()).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(safeExtension) || safeExtension == ".")
        {
            safeExtension = ".bin";
        }

        ValidateExtension(safeExtension, request.AllowedExtensions);

        var contentType = NormalizeContentType(request.ContentType);
        ValidateMimeType(contentType, request.AllowedMimeTypes);
        var safeOriginalName = NormalizeOriginalFileName(originalName);
        var dateFolder = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyyMMdd");
        var storageName = $"{Guid.NewGuid():N}{safeExtension}";

        var stored = new StoredFile
        {
            FileName = safeOriginalName,
            ContentType = contentType,
            Length = request.Content.LongLength,
            RelativePath = Path.Combine(dateFolder, storageName),
            Sha256 = ComputeSha256Hex(request.Content)
        };

        var fullPath = ResolvePathUnderRoot(storageRoot, stored.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, request.Content, cancellationToken);
        return stored;
    }

    private long ResolveMaxAllowedBytes(long? requestMaxAllowedBytes)
    {
        if (options.DefaultMaxUploadBytes <= 0)
        {
            throw new InvalidOperationException("Default max upload size must be greater than zero.");
        }

        if (!requestMaxAllowedBytes.HasValue || requestMaxAllowedBytes.Value <= 0)
        {
            return options.DefaultMaxUploadBytes;
        }

        return Math.Min(options.DefaultMaxUploadBytes, requestMaxAllowedBytes.Value);
    }

    private static void ValidateExtension(string extension, IReadOnlyCollection<string> allowedExtensions)
    {
        if (allowedExtensions.Count == 0)
        {
            return;
        }

        var normalized = allowedExtensions
            .Select(NormalizeExtension)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (normalized.Count == 0)
        {
            return;
        }

        if (!normalized.Contains(extension))
        {
            throw new InvalidOperationException($"File extension '{extension}' is not allowed.");
        }
    }

    private static void ValidateMimeType(string contentType, IReadOnlyCollection<string> allowedMimeTypes)
    {
        if (allowedMimeTypes.Count == 0)
        {
            return;
        }

        var normalized = allowedMimeTypes
            .Select(value => value.Trim().ToLowerInvariant())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);

        if (normalized.Count == 0)
        {
            return;
        }

        if (!normalized.Contains(contentType))
        {
            throw new InvalidOperationException($"File content type '{contentType}' is not allowed.");
        }
    }

    private static string NormalizeExtension(string extension)
    {
        var value = extension.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.StartsWith('.') ? value : $".{value}";
    }

    private static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return "application/octet-stream";
        }

        var value = contentType.Trim().ToLowerInvariant();
        return value.Contains('/') ? value : "application/octet-stream";
    }

    private static string NormalizeOriginalFileName(string originalName)
    {
        var safeChars = originalName
            .Where(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' || c == ' ')
            .ToArray();

        var sanitized = new string(safeChars).Trim();
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return "upload.bin";
        }

        return sanitized.Length > 200 ? sanitized[..200] : sanitized;
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        if (!options.EnableLocalFileStorage)
        {
            throw new InvalidOperationException("Local file storage is disabled in current environment.");
        }

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidOperationException("Relative file path is required.");
        }

        var fullPath = ResolvePathUnderRoot(Path.GetFullPath(options.StorageRoot), relativePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Requested file was not found.", relativePath);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 64 * 1024, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        if (!options.EnableLocalFileStorage)
        {
            throw new InvalidOperationException("Local file storage is disabled in current environment.");
        }

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return Task.CompletedTask;
        }

        var fullPath = ResolvePathUnderRoot(Path.GetFullPath(options.StorageRoot), relativePath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public static string ComputeSha256Hex(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    }

    private static string ResolvePathUnderRoot(string storageRoot, string relativePath)
    {
        var normalizedRoot = storageRoot.EndsWith(Path.DirectorySeparatorChar)
            ? storageRoot
            : storageRoot + Path.DirectorySeparatorChar;

        var normalizedRelativePath = relativePath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

        var fullPath = Path.GetFullPath(Path.Combine(normalizedRoot, normalizedRelativePath));
        var relative = Path.GetRelativePath(normalizedRoot, fullPath);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Requested file path is invalid.");
        }

        return fullPath;
    }
}
