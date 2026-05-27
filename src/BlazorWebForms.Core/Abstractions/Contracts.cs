using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Core.Abstractions;

public interface IFormDefinitionSerializer
{
    string Serialize(FormDefinition definition);
    FormDefinition Deserialize(string json);
}

public interface IConditionEvaluator
{
    bool IsVisible(string? expression, IReadOnlyDictionary<string, string?> answers);
}

public interface IFieldComponentRegistry
{
    IReadOnlyCollection<FormFieldKind> SupportedFieldKinds { get; }
    bool Supports(FormFieldKind kind);
}

public interface IFileStorage
{
    Task<StoredFile> SaveAsync(FileUploadRequest request, CancellationToken cancellationToken = default);
}

public interface IPdfExporter
{
    Task<byte[]> ExportEntryAsync(FormAggregate form, EntryRecord entry, CancellationToken cancellationToken = default);
}

public interface IEmailNotifier
{
    Task NotifyManagersAsync(FormAggregate form, EntryRecord entry, CancellationToken cancellationToken = default);
}

public interface ICurrentUserContext
{
    UserProfile GetCurrentUser();
}

public interface IPermissionEvaluator
{
    bool CanManageForm(FormAggregate form, UserProfile user);
    bool CanSubmitForm(FormAggregate form, UserProfile user);
    bool CanViewEntry(FormAggregate form, EntryRecord entry, UserProfile user);
}

public interface IFormsRepository
{
    Task SeedAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FormAggregate>> GetFormsAsync(CancellationToken cancellationToken = default);
    Task<FormAggregate?> GetFormAsync(Guid formId, CancellationToken cancellationToken = default);
    Task<FormAggregate?> GetFormBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task SaveFormAsync(FormAggregate form, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EntryRecord>> GetEntriesAsync(Guid? formId, string? search, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EntryRecord>> QueryEntriesAsync(EntryQueryOptions options, CancellationToken cancellationToken = default);
    Task<EntryRecord?> GetEntryAsync(Guid entryId, CancellationToken cancellationToken = default);
    Task SaveEntryAsync(EntryRecord entry, CancellationToken cancellationToken = default);
}
