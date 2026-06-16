using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Core.Services;

public static class EntryFileMapper
{
    public static List<EntryFileRecord> MapFiles(
        IReadOnlyList<SubmittedFileInput> files,
        Guid uploadedByUserId,
        string uploadedByEmail,
        int revisionNumber)
    {
        return files
            .Where(f => !string.IsNullOrWhiteSpace(f.FieldId))
            .Select(submittedFile => new EntryFileRecord
            {
                FieldId = submittedFile.FieldId,
                FileName = submittedFile.File.FileName,
                ContentType = submittedFile.File.ContentType,
                Length = submittedFile.File.Length,
                RelativePath = submittedFile.File.RelativePath,
                Sha256 = submittedFile.File.Sha256,
                UploadedByUserId = uploadedByUserId,
                UploadedByEmail = uploadedByEmail,
                RevisionNumber = revisionNumber,
                UploadedUtc = DateTimeOffset.UtcNow
            })
            .ToList();
    }

    public static void ApplyFileAnswers(
        Dictionary<string, string?> answers,
        IReadOnlyList<SubmittedFileInput> files)
    {
        foreach (var filesByField in files
                     .Where(f => !string.IsNullOrWhiteSpace(f.FieldId))
                     .GroupBy(f => f.FieldId, StringComparer.OrdinalIgnoreCase))
        {
            answers[filesByField.Key] = string.Join(", ", filesByField.Select(f => f.File.FileName));
        }
    }
}
