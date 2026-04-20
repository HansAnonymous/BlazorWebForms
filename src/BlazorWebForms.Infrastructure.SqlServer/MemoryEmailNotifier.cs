using BlazorWebForms.Core.Abstractions;
using BlazorWebForms.Core.Models;

namespace BlazorWebForms.Infrastructure.SqlServer;

internal sealed class MemoryEmailNotifier : IEmailNotifier
{
    public List<string> Messages { get; } = [];

    public Task NotifyManagersAsync(FormAggregate form, EntryRecord entry, CancellationToken cancellationToken = default)
    {
        Messages.Add($"Notify managers for form '{form.Name}' about entry '{entry.Id}'.");
        return Task.CompletedTask;
    }
}
