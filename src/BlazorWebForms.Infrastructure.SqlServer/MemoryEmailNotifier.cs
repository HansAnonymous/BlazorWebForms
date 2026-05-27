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

    public Task NotifyInvitationCreatedAsync(FormAggregate form, FormInvitation invitation, CancellationToken cancellationToken = default)
    {
        Messages.Add($"Invitation created for '{invitation.Email}' on form '{form.Name}' with role '{invitation.Role}'. Token '{invitation.Token}'.");
        return Task.CompletedTask;
    }

    public Task NotifyInvitationAcceptedAsync(FormAggregate form, FormInvitation invitation, CancellationToken cancellationToken = default)
    {
        Messages.Add($"Invitation accepted by '{invitation.Email}' on form '{form.Name}'.");
        return Task.CompletedTask;
    }

    public Task NotifyInvitationRevokedAsync(FormAggregate form, FormInvitation invitation, CancellationToken cancellationToken = default)
    {
        Messages.Add($"Invitation revoked for '{invitation.Email}' on form '{form.Name}'.");
        return Task.CompletedTask;
    }
}
